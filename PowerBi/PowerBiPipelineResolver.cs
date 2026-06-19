using System.Net;
using System.Text.Json;

namespace RefreshVIR
{
    internal readonly record struct AppUpdatePipelineInfo(
        Guid PipelineId,
        string PipelineName,
        Guid StagingWorkspaceId);

    internal sealed class PowerBiPipelineResolver
    {
        private static readonly string[] AppUpdatePipelineDisplayNames =
        {
            "RefreshVIR_App_update",
            "RefreshVIR App Update"
        };

        internal const int AppUpdateStagingStageOrder = 0;
        internal const int AppUpdateTargetStageOrder = 1;

        private readonly HttpClient _httpClient;
        private AppUpdatePipelineInfo? _cachedPipeline;

        internal HttpClient HttpClient => _httpClient;

        internal PowerBiPipelineResolver(HttpClient httpClient) =>
            _httpClient = httpClient;

        internal async Task<AppUpdatePipelineInfo?> TryGetAppUpdatePipelineAsync()
        {
            if (_cachedPipeline.HasValue)
                return _cachedPipeline.Value;

            try
            {
                PipelineItem? pipeline = await FindConfiguredPipelineAsync();
                if (pipeline == null)
                    return null;

                pipeline = await GetPipelineByIdAsync(pipeline.Id);
                PipelineStageItem? stagingStage = pipeline.Stages?
                    .FirstOrDefault(stage => stage.Order == AppUpdateStagingStageOrder);

                if (stagingStage?.WorkspaceId is not Guid stagingWorkspaceId || stagingWorkspaceId == Guid.Empty)
                    return null;

                _cachedPipeline = new AppUpdatePipelineInfo(
                    pipeline.Id,
                    pipeline.DisplayName ?? pipeline.Id.ToString(),
                    stagingWorkspaceId);

                return _cachedPipeline.Value;
            }
            catch
            {
                return null;
            }
        }

        internal async Task<AppUpdatePipelineInfo> ResolveAppUpdatePipelineAsync()
        {
            AppUpdatePipelineInfo? pipeline = await TryGetAppUpdatePipelineAsync();
            if (pipeline.HasValue)
                return pipeline.Value;

            List<PipelineItem> pipelines = await GetPipelinesAsync();
            PipelineItem? configuredPipeline = FindConfiguredPipeline(pipelines);

            if (configuredPipeline == null
                && !string.IsNullOrWhiteSpace(Configuration.PowerBiAppUpdatePipelineId)
                && Guid.TryParse(Configuration.PowerBiAppUpdatePipelineId, out _))
            {
                throw new InvalidOperationException(
                    "A VIR_POWERBI_APP_UPDATE_PIPELINE_ID környezeti változóban megadott " +
                    "telepítési folyamat nem található.");
            }

            if (configuredPipeline == null)
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

            configuredPipeline = await GetPipelineByIdAsync(configuredPipeline.Id);
            PipelineStageItem? stagingStage = configuredPipeline.Stages?
                .FirstOrDefault(stage => stage.Order == AppUpdateStagingStageOrder);

            if (stagingStage?.WorkspaceId is not Guid stagingWorkspaceId || stagingWorkspaceId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    $"A(z) „{configuredPipeline.DisplayName ?? configuredPipeline.Id.ToString()}” telepítési folyamat " +
                    "Fejlesztés szakaszához staging munkaterületet kell rendelni.");
            }

            _cachedPipeline = new AppUpdatePipelineInfo(
                configuredPipeline.Id,
                configuredPipeline.DisplayName ?? configuredPipeline.Id.ToString(),
                stagingWorkspaceId);

            return _cachedPipeline.Value;
        }

        internal async Task<PipelineItem> GetPipelineByIdAsync(Guid pipelineId)
        {
            using HttpResponseMessage response =
                await _httpClient.GetAsync($"pipelines/{pipelineId}?$expand=stages");

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

        internal async Task<List<PipelineItem>> GetPipelinesAsync()
        {
            using HttpResponseMessage response =
                await _httpClient.GetAsync("pipelines?$expand=stages");

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

        private async Task<PipelineItem?> FindConfiguredPipelineAsync()
        {
            List<PipelineItem> pipelines = await GetPipelinesAsync();
            return FindConfiguredPipeline(pipelines);
        }

        private static PipelineItem? FindConfiguredPipeline(List<PipelineItem> pipelines)
        {
            if (!string.IsNullOrWhiteSpace(Configuration.PowerBiAppUpdatePipelineId)
                && Guid.TryParse(Configuration.PowerBiAppUpdatePipelineId, out Guid configuredPipelineId))
            {
                return pipelines.FirstOrDefault(item => item.Id == configuredPipelineId);
            }

            return pipelines.FirstOrDefault(item =>
                AppUpdatePipelineDisplayNames.Any(name =>
                    string.Equals(item.DisplayName, name, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
