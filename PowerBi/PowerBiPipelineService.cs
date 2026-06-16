using System.Net;
using System.Text;
using System.Text.Json;

namespace RefreshVIR
{
    internal static class PowerBiPipelineService
    {
        private static readonly string[] AppUpdatePipelineDisplayNames =
        {
            "RefreshVIR_App_update",
            "RefreshVIR App Update"
        };
        private const int AppUpdateStagingStageOrder = 0;
        private const int AppUpdateTargetStageOrder = 1;

        internal static async Task UpdateWorkspaceAppAsync(
            Guid workspaceId,
            IProgress<string>? progress = null)
        {
            progress?.Report("App frissítése...");

            using HttpClient httpClient = await PowerBiApiClient.CreateAuthorizedClientAsync();

            progress?.Report("App frissítés előkészítése...");
            (Guid pipelineId, string pipelineName, Guid stagingWorkspaceId) =
                await ResolveAppUpdatePipelineAsync(httpClient);

            progress?.Report("Munkaterület csatolása...");
            await EnsureWorkspaceAssignedToPipelineStageAsync(
                httpClient,
                pipelineId,
                AppUpdateTargetStageOrder,
                workspaceId);

            try
            {
                progress?.Report("Staging munkaterület előkészítése...");
                await PowerBiGroupApi.ClearWorkspaceContentAsync(httpClient, stagingWorkspaceId);

                progress?.Report("App tartalom szinkronizálása...");
                await ExecutePipelineDeployAllAsync(
                    httpClient,
                    pipelineId,
                    pipelineName,
                    workspaceId,
                    sourceStageOrder: AppUpdateTargetStageOrder,
                    isBackwardDeployment: true,
                    updateApp: false,
                    progress);

                progress?.Report("App újraközzététele...");
                await ExecutePipelineDeployAllAsync(
                    httpClient,
                    pipelineId,
                    pipelineName,
                    workspaceId,
                    sourceStageOrder: AppUpdateStagingStageOrder,
                    isBackwardDeployment: false,
                    updateApp: true,
                    progress);
            }
            finally
            {
                await TryUnassignPipelineStageAsync(
                    httpClient,
                    pipelineId,
                    AppUpdateTargetStageOrder);
            }

            progress?.Report("App frissítés kész.");
        }

        internal static async Task<Guid?> TryGetAppUpdateStagingWorkspaceIdAsync(HttpClient httpClient)
        {
            try
            {
                List<PipelineItem> pipelines = await GetPipelinesAsync(httpClient);
                PipelineItem? pipeline = null;

                if (!string.IsNullOrWhiteSpace(Configuration.PowerBiAppUpdatePipelineId)
                    && Guid.TryParse(Configuration.PowerBiAppUpdatePipelineId, out Guid configuredPipelineId))
                {
                    pipeline = pipelines.FirstOrDefault(item => item.Id == configuredPipelineId);
                }
                else
                {
                    pipeline = pipelines.FirstOrDefault(item =>
                        AppUpdatePipelineDisplayNames.Any(name =>
                            string.Equals(item.DisplayName, name, StringComparison.OrdinalIgnoreCase)));
                }

                if (pipeline == null)
                    return null;

                pipeline = await GetPipelineByIdAsync(httpClient, pipeline.Id);
                PipelineStageItem? stagingStage = pipeline.Stages?
                    .FirstOrDefault(stage => stage.Order == AppUpdateStagingStageOrder);

                return stagingStage?.WorkspaceId is Guid workspaceId && workspaceId != Guid.Empty
                    ? workspaceId
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static async Task<(Guid PipelineId, string PipelineName, Guid StagingWorkspaceId)>
            ResolveAppUpdatePipelineAsync(HttpClient httpClient)
        {
            List<PipelineItem> pipelines = await GetPipelinesAsync(httpClient);
            PipelineItem? pipeline = null;

            if (!string.IsNullOrWhiteSpace(Configuration.PowerBiAppUpdatePipelineId)
                && Guid.TryParse(Configuration.PowerBiAppUpdatePipelineId, out Guid configuredPipelineId))
            {
                pipeline = pipelines.FirstOrDefault(item => item.Id == configuredPipelineId);
                if (pipeline == null)
                {
                    throw new InvalidOperationException(
                        "A VIR_POWERBI_APP_UPDATE_PIPELINE_ID környezeti változóban megadott " +
                        "telepítési folyamat nem található.");
                }
            }
            else
            {
                pipeline = pipelines.FirstOrDefault(item =>
                    AppUpdatePipelineDisplayNames.Any(name =>
                        string.Equals(item.DisplayName, name, StringComparison.OrdinalIgnoreCase)));
            }

            if (pipeline == null)
            {
                throw new InvalidOperationException(
                    "Az app automatikus frissítéséhez egyszeri RefreshVIR infrastruktúra szükséges.\n\n" +
                    "1. Hozz létre egy Power BI telepítési folyamatot „RefreshVIR_App_update” néven.\n" +
                    "2. Rendeld hozzá egy staging munkaterületet a Fejlesztés (Development) szakaszhoz.\n" +
                    "3. A Teszt (Test) és Éles (Production) szakaszt hagyd üresen.\n" +
                    "4. Add meg a VIR_POWERBI_APP_UPDATE_PIPELINE_ID környezeti változót, " +
                    "vagy használd pontosan a fenti folyamatnevet.\n\n" +
                    "A munkaterületen már korábban legalább egyszer manuálisan közzétett app szükséges.");
            }

            pipeline = await GetPipelineByIdAsync(httpClient, pipeline.Id);

            PipelineStageItem? stagingStage = pipeline.Stages?
                .FirstOrDefault(stage => stage.Order == AppUpdateStagingStageOrder);

            if (stagingStage?.WorkspaceId is not Guid stagingWorkspaceId || stagingWorkspaceId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    $"A(z) „{pipeline.DisplayName ?? pipeline.Id.ToString()}” telepítési folyamat " +
                    "Fejlesztés szakaszához staging munkaterületet kell rendelni.");
            }

            return (pipeline.Id, pipeline.DisplayName ?? pipeline.Id.ToString(), stagingWorkspaceId);
        }

        private static async Task<List<PipelineItem>> GetPipelinesAsync(HttpClient httpClient)
        {
            using HttpResponseMessage response =
                await httpClient.GetAsync("pipelines?$expand=stages");

            string body = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException(
                    "Az app frissítéséhez Pipeline.Read.All, Pipeline.ReadWrite.All és Pipeline.Deploy " +
                    "API jogosultság szükséges az Azure alkalmazásregisztrációban.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Telepítési folyamatok lekérdezése sikertelen ({(int)response.StatusCode}): {body}");
            }

            PipelinesResponse? pipelines =
                JsonSerializer.Deserialize<PipelinesResponse>(body, PowerBiApiClient.JsonOptions);

            return pipelines?.Value ?? new List<PipelineItem>();
        }

        private static async Task EnsureWorkspaceAssignedToPipelineStageAsync(
            HttpClient httpClient,
            Guid pipelineId,
            int stageOrder,
            Guid workspaceId)
        {
            PipelineItem pipeline = await GetPipelineByIdAsync(httpClient, pipelineId);
            PipelineStageItem? stage = pipeline.Stages?
                .FirstOrDefault(item => item.Order == stageOrder);

            if (stage?.WorkspaceId == workspaceId)
                return;

            if (stage?.WorkspaceId is Guid existingWorkspaceId && existingWorkspaceId != Guid.Empty)
            {
                await UnassignPipelineStageAsync(httpClient, pipelineId, stageOrder);
            }

            await AssignWorkspaceToPipelineStageAsync(
                httpClient,
                pipelineId,
                stageOrder,
                workspaceId);
        }

        private static async Task<PipelineItem> GetPipelineByIdAsync(
            HttpClient httpClient,
            Guid pipelineId)
        {
            using HttpResponseMessage response =
                await httpClient.GetAsync($"pipelines/{pipelineId}?$expand=stages");

            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Telepítési folyamat lekérdezése sikertelen ({(int)response.StatusCode}): {body}");
            }

            PipelineItem? pipeline = JsonSerializer.Deserialize<PipelineItem>(body, PowerBiApiClient.JsonOptions);

            return pipeline
                ?? throw new InvalidOperationException(
                    $"A telepítési folyamat ({pipelineId}) nem található.");
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
            HttpClient httpClient,
            Guid pipelineId,
            int stageOrder)
        {
            try
            {
                await UnassignPipelineStageAsync(httpClient, pipelineId, stageOrder);
            }
            catch
            {
                // Best-effort cleanup; app update already succeeded or failed earlier.
            }
        }

        private static async Task ExecutePipelineDeployAllAsync(
            HttpClient httpClient,
            Guid pipelineId,
            string pipelineName,
            Guid workspaceId,
            int sourceStageOrder,
            bool isBackwardDeployment,
            bool updateApp,
            IProgress<string>? progress)
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

            string requestJson = JsonSerializer.Serialize(request, PowerBiApiClient.JsonOptions);
            using HttpResponseMessage response = await httpClient.PostAsync(
                $"pipelines/{pipelineId}/deployAll",
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
                        ["Pipeline"] = pipelineName,
                        ["Pipeline ID"] = pipelineId.ToString(),
                        ["Source stage order"] = sourceStageOrder.ToString(),
                        ["Backward deployment"] = isBackwardDeployment.ToString()
                    },
                    responseBody,
                    (int)response.StatusCode);
            }

            Guid operationId = ParsePipelineOperationId(responseBody);
            await WaitForPipelineOperationAsync(httpClient, pipelineId, operationId, progress);
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
            IProgress<string>? progress)
        {
            TimeSpan pollInterval = TimeSpan.FromSeconds(2);
            TimeSpan timeout = TimeSpan.FromMinutes(10);
            DateTime deadline = DateTime.UtcNow + timeout;

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
                    return;

                if (string.Equals(operation.Status, "Failed", StringComparison.OrdinalIgnoreCase))
                {
                    string details = operation.ExecutionPlan?.Steps?
                        .Where(step => string.Equals(step.Status, "Failed", StringComparison.OrdinalIgnoreCase))
                        .Select(step => step.Error?.ErrorDetails ?? step.Error?.ErrorCode)
                        .FirstOrDefault(detail => !string.IsNullOrWhiteSpace(detail))
                        ?? "Ismeretlen hiba";

                    throw new InvalidOperationException($"App frissítés sikertelen: {details}");
                }

                progress?.Report($"App frissítés folyamatban ({operation.Status})...");
                await Task.Delay(pollInterval);
            }

            throw new TimeoutException("Az app frissítés túllépte az időkorlátot.");
        }
    }
}
