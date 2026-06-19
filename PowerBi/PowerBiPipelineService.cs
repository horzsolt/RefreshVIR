using System.Net;
using System.Text;
using System.Text.Json;

namespace RefreshVIR
{
    internal static class PowerBiPipelineService
    {
        internal static async Task UpdateWorkspaceAppAsync(
            Guid workspaceId,
            IProgress<AppUpdateProgressReport>? progress = null)
        {
            await using PowerBiSession session = await PowerBiApiClient.CreateSessionAsync();
            await UpdateWorkspaceAppAsync(session, workspaceId, progress);
        }

        internal static async Task UpdateWorkspaceAppAsync(
            PowerBiSession session,
            Guid workspaceId,
            IProgress<AppUpdateProgressReport>? progress = null)
        {
            AppUpdateProgressTracker tracker = new(progress);
            HttpClient httpClient = session.HttpClient;
            PowerBiPipelineResolver pipelineResolver = session.Pipeline;

            tracker.ReportPhaseStart("App frissítés: telepítési folyamat betöltése...");
            AppUpdatePipelineInfo pipeline = await pipelineResolver.ResolveAppUpdatePipelineAsync();
            tracker.ReportPhaseComplete("Telepítési folyamat betöltve.");
            tracker.AdvancePhase();

            tracker.ReportPhaseStart(
                $"App frissítés: munkaterület csatolása a(z) „{pipeline.PipelineName}” folyamathoz...");
            await EnsureWorkspaceAssignedToPipelineStageAsync(
                pipelineResolver,
                pipeline.PipelineId,
                PowerBiPipelineResolver.AppUpdateTargetStageOrder,
                workspaceId,
                tracker);
            tracker.ReportPhaseComplete("Munkaterület csatolva.");
            tracker.AdvancePhase();

            try
            {
                tracker.ReportPhaseStart("App frissítés: staging munkaterület előkészítése...");
                await PowerBiGroupApi.ClearWorkspaceContentAsync(
                    httpClient,
                    pipeline.StagingWorkspaceId,
                    tracker);
                tracker.ReportPhaseComplete("Staging munkaterület előkészítve.");
                tracker.AdvancePhase();

                tracker.ReportPhaseStart("App frissítés: adatmodellek ellenőrzése a cél munkaterületen...");
                await PowerBiDatasetApi.WaitForWorkspaceRefreshesToCompleteAsync(
                    httpClient,
                    workspaceId,
                    tracker);
                tracker.ReportPhaseComplete("Adatmodellek készen állnak.");
                tracker.AdvancePhase();

                await ExecutePipelineDeployAllWithRefreshRetryAsync(
                    httpClient,
                    pipelineResolver,
                    pipeline,
                    workspaceId,
                    sourceStageOrder: PowerBiPipelineResolver.AppUpdateTargetStageOrder,
                    isBackwardDeployment: true,
                    updateApp: false,
                    phaseLabel: "Tartalom másolása stagingre",
                    tracker);

                tracker.ReportPhaseStart("App frissítés: adatmodellek ellenőrzése telepítés előtt...");
                await PowerBiDatasetApi.WaitForWorkspaceRefreshesToCompleteAsync(
                    httpClient,
                    workspaceId,
                    tracker);
                tracker.ReportPhaseComplete("Adatmodellek készen állnak.");
                tracker.AdvancePhase();

                await ExecutePipelineDeployAllWithRefreshRetryAsync(
                    httpClient,
                    pipelineResolver,
                    pipeline,
                    workspaceId,
                    sourceStageOrder: PowerBiPipelineResolver.AppUpdateStagingStageOrder,
                    isBackwardDeployment: false,
                    updateApp: true,
                    phaseLabel: "App újraközzététele",
                    tracker);
            }
            finally
            {
                tracker.ReportPhaseStart("App frissítés: munkaterület leválasztása a telepítési folyamatról...");
                await TryUnassignPipelineStageAsync(
                    pipelineResolver,
                    pipeline.PipelineId,
                    PowerBiPipelineResolver.AppUpdateTargetStageOrder);
                tracker.ReportPhaseComplete("Munkaterület leválasztva.");
                tracker.AdvancePhase();
            }

            tracker.Report("App frissítés kész.", 100);
        }

        private static async Task EnsureWorkspaceAssignedToPipelineStageAsync(
            PowerBiPipelineResolver pipelineResolver,
            Guid pipelineId,
            int stageOrder,
            Guid workspaceId,
            AppUpdateProgressTracker tracker)
        {
            PipelineItem pipeline = await pipelineResolver.GetPipelineByIdAsync(pipelineId);
            PipelineStageItem? stage = pipeline.Stages?
                .FirstOrDefault(item => item.Order == stageOrder);

            if (stage?.WorkspaceId == workspaceId)
            {
                tracker.Report("Munkaterület már csatolva van a telepítési folyamathoz.");
                return;
            }

            if (stage?.WorkspaceId is Guid existingWorkspaceId && existingWorkspaceId != Guid.Empty)
            {
                tracker.Report("Korábbi munkaterület leválasztása a telepítési folyamatról...");
                await UnassignPipelineStageAsync(pipelineResolver.HttpClient, pipelineId, stageOrder);
            }

            tracker.Report("Munkaterület hozzárendelése a telepítési folyamathoz...");
            await AssignWorkspaceToPipelineStageAsync(
                pipelineResolver.HttpClient,
                pipelineId,
                stageOrder,
                workspaceId);
        }

        private static async Task AssignWorkspaceToPipelineStageAsync(
            HttpClient httpClient,
            Guid pipelineId,
            int stageOrder,
            Guid workspaceId)
        {
            AssignWorkspaceRequest request = new() { WorkspaceId = workspaceId };
            string requestJson = JsonSerializer.Serialize(request, PowerBiApiClient.JsonOptions);

            using HttpResponseMessage response = await httpClient.PostAsync(
                $"pipelines/{pipelineId}/stages/{stageOrder}/assignWorkspace",
                new StringContent(requestJson, Encoding.UTF8, "application/json"));

            string responseBody = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException(
                    "A munkaterület telepítési folyamathoz rendeléséhez Pipeline.ReadWrite.All " +
                    "és Workspace.ReadWrite.All API jogosultság szükséges.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw PowerBiApiClient.CreateDetailedException(
                    $"Munkaterület hozzárendelése sikertelen ({(int)response.StatusCode})",
                    new Dictionary<string, string>
                    {
                        ["Operation"] = "Assign workspace to pipeline stage",
                        ["Pipeline ID"] = pipelineId.ToString(),
                        ["Stage order"] = stageOrder.ToString(),
                        ["Workspace ID"] = workspaceId.ToString()
                    },
                    responseBody,
                    (int)response.StatusCode);
            }
        }

        private static async Task UnassignPipelineStageAsync(
            HttpClient httpClient,
            Guid pipelineId,
            int stageOrder)
        {
            using HttpResponseMessage response = await httpClient.PostAsync(
                $"pipelines/{pipelineId}/stages/{stageOrder}/unassignWorkspace",
                null);

            string responseBody = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw PowerBiApiClient.CreateDetailedException(
                    $"Munkaterület leválasztása sikertelen ({(int)response.StatusCode})",
                    new Dictionary<string, string>
                    {
                        ["Operation"] = "Unassign workspace from pipeline stage",
                        ["Pipeline ID"] = pipelineId.ToString(),
                        ["Stage order"] = stageOrder.ToString()
                    },
                    responseBody,
                    (int)response.StatusCode);
            }
        }

        private static async Task TryUnassignPipelineStageAsync(
            PowerBiPipelineResolver pipelineResolver,
            Guid pipelineId,
            int stageOrder)
        {
            try
            {
                await UnassignPipelineStageAsync(pipelineResolver.HttpClient, pipelineId, stageOrder);
            }
            catch
            {
                // Best-effort cleanup; app update already succeeded or failed earlier.
            }
        }

        private static async Task ExecutePipelineDeployAllWithRefreshRetryAsync(
            HttpClient httpClient,
            PowerBiPipelineResolver pipelineResolver,
            AppUpdatePipelineInfo pipeline,
            Guid workspaceId,
            int sourceStageOrder,
            bool isBackwardDeployment,
            bool updateApp,
            string phaseLabel,
            AppUpdateProgressTracker tracker)
        {
            const int maxAttempts = 3;

            tracker.ReportPhaseStart($"{phaseLabel}...");

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (attempt > 1)
                        tracker.Report($"{phaseLabel}: újrapróbálkozás ({attempt}/{maxAttempts})...");

                    await ExecutePipelineDeployAllAsync(
                        httpClient,
                        pipelineResolver,
                        pipeline,
                        workspaceId,
                        sourceStageOrder,
                        isBackwardDeployment,
                        updateApp,
                        phaseLabel,
                        tracker);

                    tracker.ReportPhaseComplete($"{phaseLabel}: kész.");
                    tracker.AdvancePhase();
                    return;
                }
                catch (InvalidOperationException ex) when (
                    attempt < maxAttempts
                    && IsModelRefreshingFailure(ex.Message))
                {
                    tracker.Report($"{phaseLabel}: adatmodell frissítés miatt várakozás...");
                    await PowerBiDatasetApi.WaitForWorkspaceRefreshesToCompleteAsync(
                        httpClient,
                        workspaceId,
                        tracker);
                }
            }
        }

        private static bool IsModelRefreshingFailure(string message) =>
            message.Contains("model is refreshing", StringComparison.OrdinalIgnoreCase)
            || message.Contains("modell frissít", StringComparison.OrdinalIgnoreCase);

        private static async Task ExecutePipelineDeployAllAsync(
            HttpClient httpClient,
            PowerBiPipelineResolver pipelineResolver,
            AppUpdatePipelineInfo pipeline,
            Guid workspaceId,
            int sourceStageOrder,
            bool isBackwardDeployment,
            bool updateApp,
            string phaseLabel,
            AppUpdateProgressTracker tracker)
        {
            DeployAllRequest request = new()
            {
                SourceStageOrder = sourceStageOrder,
                IsBackwardDeployment = isBackwardDeployment,
                Note = "RefreshVIR app frissítés",
                Options = new DeploymentOptionsRequest
                {
                    AllowCreateArtifact = true,
                    AllowOverwriteArtifact = true
                },
                UpdateAppSettings = updateApp
                    ? new UpdateAppSettingsRequest { UpdateAppInTargetWorkspace = true }
                    : null
            };

            tracker.Report($"{phaseLabel}: telepítés indítása...");

            string requestJson = JsonSerializer.Serialize(request, PowerBiApiClient.JsonOptions);
            using HttpResponseMessage response = await httpClient.PostAsync(
                $"pipelines/{pipeline.PipelineId}/deployAll",
                new StringContent(requestJson, Encoding.UTF8, "application/json"));

            string responseBody = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException(
                    "Az app frissítéséhez Pipeline.Read.All és Pipeline.Deploy API jogosultság szükséges " +
                    "az Azure alkalmazásregisztrációban.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw PowerBiApiClient.CreateDetailedException(
                    $"App frissítés indítása sikertelen ({(int)response.StatusCode})",
                    new Dictionary<string, string>
                    {
                        ["Operation"] = updateApp
                            ? "Power BI app republish"
                            : "Power BI app sync",
                        ["Workspace ID"] = workspaceId.ToString(),
                        ["Pipeline"] = pipeline.PipelineName,
                        ["Pipeline ID"] = pipeline.PipelineId.ToString(),
                        ["Source stage order"] = sourceStageOrder.ToString(),
                        ["Backward deployment"] = isBackwardDeployment.ToString()
                    },
                    responseBody,
                    (int)response.StatusCode);
            }

            Guid operationId = ParsePipelineOperationId(responseBody);
            tracker.Report($"{phaseLabel}: telepítés elindítva, folyamat követése...");
            await WaitForPipelineOperationAsync(
                httpClient,
                pipeline.PipelineId,
                operationId,
                phaseLabel,
                tracker);
        }

        private static Guid ParsePipelineOperationId(string responseBody)
        {
            using JsonDocument document = JsonDocument.Parse(responseBody);
            JsonElement root = document.RootElement;

            if (root.TryGetProperty("id", out JsonElement idElement)
                && Guid.TryParse(idElement.GetString(), out Guid operationId))
            {
                return operationId;
            }

            if (root.TryGetProperty("value", out JsonElement valueElement)
                && valueElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in valueElement.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out JsonElement nestedId)
                        && Guid.TryParse(nestedId.GetString(), out Guid nestedOperationId))
                    {
                        return nestedOperationId;
                    }
                }
            }

            throw new InvalidOperationException("A telepítési folyamat válasza nem értelmezhető.");
        }

        private static async Task WaitForPipelineOperationAsync(
            HttpClient httpClient,
            Guid pipelineId,
            Guid operationId,
            string phaseLabel,
            AppUpdateProgressTracker tracker)
        {
            TimeSpan pollInterval = TimeSpan.FromSeconds(2);
            TimeSpan timeout = TimeSpan.FromMinutes(10);
            DateTime deadline = DateTime.UtcNow + timeout;
            string? lastReportedStatus = null;

            while (DateTime.UtcNow < deadline)
            {
                using HttpResponseMessage response = await httpClient.GetAsync(
                    $"pipelines/{pipelineId}/operations/{operationId}");

                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"App frissítés státusz lekérdezése sikertelen ({(int)response.StatusCode}): {body}");
                }

                PipelineOperationResponse? operation =
                    JsonSerializer.Deserialize<PipelineOperationResponse>(body, PowerBiApiClient.JsonOptions)
                    ?? throw new InvalidOperationException("Az app frissítés státusza nem értelmezhető.");

                if (string.Equals(operation.Status, "Succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    tracker.Report($"{phaseLabel}: kész.", 100);
                    return;
                }

                if (string.Equals(operation.Status, "Failed", StringComparison.OrdinalIgnoreCase))
                {
                    string details = operation.ExecutionPlan?.Steps?
                        .Where(step => string.Equals(step.Status, "Failed", StringComparison.OrdinalIgnoreCase))
                        .Select(step => step.Error?.ErrorDetails ?? step.Error?.ErrorCode)
                        .FirstOrDefault(detail => !string.IsNullOrWhiteSpace(detail))
                        ?? "Ismeretlen hiba";

                    throw new InvalidOperationException($"App frissítés sikertelen: {details}");
                }

                string statusMessage = FormatPipelineOperationProgress(operation, phaseLabel);
                int stepPercent = CalculatePipelineStepPercent(operation);
                if (!string.Equals(statusMessage, lastReportedStatus, StringComparison.Ordinal))
                {
                    tracker.Report(statusMessage, stepPercent);
                    lastReportedStatus = statusMessage;
                }
                else
                {
                    tracker.Report(statusMessage, stepPercent);
                }

                await Task.Delay(pollInterval);
            }

            throw new TimeoutException("Az app frissítés túllépte az időkorlátot.");
        }

        private static int CalculatePipelineStepPercent(PipelineOperationResponse operation)
        {
            List<PipelineExecutionStepResponse>? steps = operation.ExecutionPlan?.Steps;
            if (steps == null || steps.Count == 0)
                return 10;

            int completedSteps = steps.Count(step => IsSucceededStepStatus(step.Status));
            return Math.Clamp(completedSteps * 100 / steps.Count, 5, 95);
        }

        private static string FormatPipelineOperationProgress(
            PipelineOperationResponse operation,
            string phaseLabel)
        {
            List<PipelineExecutionStepResponse>? steps = operation.ExecutionPlan?.Steps;
            if (steps == null || steps.Count == 0)
                return $"{phaseLabel}: {TranslateOperationStatus(operation.Status)}";

            int completedSteps = steps.Count(step => IsSucceededStepStatus(step.Status));
            PipelineExecutionStepResponse? currentStep =
                steps.FirstOrDefault(step => IsInProgressStepStatus(step.Status))
                ?? steps.FirstOrDefault(step => !IsTerminalStepStatus(step.Status))
                ?? steps.Last();

            string stepLabel = FormatExecutionStep(currentStep);
            return $"{phaseLabel}: {stepLabel} ({completedSteps}/{steps.Count}, {TranslateOperationStatus(operation.Status)})";
        }

        private static string FormatExecutionStep(PipelineExecutionStepResponse step)
        {
            string? itemName = step.SourceAndTarget?.SourceItemDisplayName
                ?? step.SourceAndTarget?.TargetItemDisplayName;
            string? itemType = step.SourceAndTarget?.ItemType;

            if (!string.IsNullOrWhiteSpace(itemName) && !string.IsNullOrWhiteSpace(itemType))
                return $"{TranslateItemType(itemType)} — {itemName}";

            if (!string.IsNullOrWhiteSpace(step.Description))
                return TranslateStepDescription(step.Description);

            if (!string.IsNullOrWhiteSpace(itemName))
                return itemName;

            return TranslateOperationStatus(step.Status);
        }

        private static string TranslateItemType(string itemType) => itemType switch
        {
            "Report" => "Riport",
            "Dataset" => "Adathalmaz",
            "Dashboard" => "Irányítópult",
            "Dataflow" => "Adatfolyam",
            "PaginatedReport" => "Lapozható riport",
            "Datamart" => "Adatraktár",
            _ => itemType
        };

        private static string TranslateStepDescription(string description) => description switch
        {
            "ReportDeployment" => "Riport telepítése",
            "DatasetDeployment" => "Adathalmaz telepítése",
            "DashboardDeployment" => "Irányítópult telepítése",
            "DataflowDeployment" => "Adatfolyam telepítése",
            "PaginatedReportDeployment" => "Lapozható riport telepítése",
            "UpdateApp" => "App frissítése",
            _ => description
        };

        private static string TranslateOperationStatus(string? status) => status switch
        {
            "NotStarted" => "Indításra vár",
            "InProgress" => "Folyamatban",
            "Running" => "Folyamatban",
            "Succeeded" => "Kész",
            "Failed" => "Sikertelen",
            _ => string.IsNullOrWhiteSpace(status) ? "Ismeretlen" : status
        };

        private static bool IsSucceededStepStatus(string? status) =>
            string.Equals(status, "Succeeded", StringComparison.OrdinalIgnoreCase);

        private static bool IsInProgressStepStatus(string? status) =>
            string.Equals(status, "InProgress", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Running", StringComparison.OrdinalIgnoreCase);

        private static bool IsTerminalStepStatus(string? status) =>
            IsSucceededStepStatus(status)
            || string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Skipped", StringComparison.OrdinalIgnoreCase);
    }
}
