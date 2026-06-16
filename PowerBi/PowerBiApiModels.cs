using System.Text.Json.Serialization;

namespace RefreshVIR
{
    internal sealed class GroupsResponse
    {
        [JsonPropertyName("value")]
        public List<GroupItem> Value { get; set; } = new();
    }

    internal sealed class GroupItem
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    internal sealed class ImportResponse
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("importState")]
        public string? ImportState { get; set; }

        [JsonPropertyName("error")]
        public ImportErrorResponse? Error { get; set; }
    }

    internal sealed class ImportErrorResponse
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("details")]
        public List<ImportErrorDetailItem> Details { get; set; } = new();
    }

    internal sealed class ImportErrorDetailItem
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("detailType")]
        public string? DetailType { get; set; }
    }

    internal sealed class ReportsResponse
    {
        [JsonPropertyName("value")]
        public List<ReportItem> Value { get; set; } = new();
    }

    internal sealed class ReportItem
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("datasetId")]
        public Guid DatasetId { get; set; }

        [JsonPropertyName("reportType")]
        public string? ReportType { get; set; }
    }

    internal sealed class ImportsResponse
    {
        [JsonPropertyName("value")]
        public List<ImportItem> Value { get; set; } = new();
    }

    internal sealed class ImportItem
    {
        [JsonPropertyName("createdDateTime")]
        public string? CreatedDateTime { get; set; }

        [JsonPropertyName("updatedDateTime")]
        public string? UpdatedDateTime { get; set; }

        [JsonPropertyName("reports")]
        public List<ImportReportItem> Reports { get; set; } = new();
    }

    internal sealed class ImportReportItem
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    internal sealed class DatasetsResponse
    {
        [JsonPropertyName("value")]
        public List<DatasetItem> Value { get; set; } = new();
    }

    internal sealed class DatasetItem
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("isRefreshable")]
        public bool? IsRefreshable { get; set; }

        [JsonPropertyName("isOnPremGatewayRequired")]
        public bool? IsOnPremGatewayRequired { get; set; }

        [JsonPropertyName("targetStorageMode")]
        public string? TargetStorageMode { get; set; }

        [JsonPropertyName("contentProviderType")]
        public string? ContentProviderType { get; set; }
    }

    internal sealed class RefreshSchedule
    {
        [JsonPropertyName("days")]
        public List<string> Days { get; set; } = new();

        [JsonPropertyName("times")]
        public List<string> Times { get; set; } = new();

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("localTimeZoneId")]
        public string? LocalTimeZoneId { get; set; }
    }

    internal sealed class RefreshesResponse
    {
        [JsonPropertyName("value")]
        public List<RefreshHistoryItem> Value { get; set; } = new();
    }

    internal sealed class RefreshHistoryItem
    {
        [JsonPropertyName("startTime")]
        public string? StartTime { get; set; }

        [JsonPropertyName("endTime")]
        public string? EndTime { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    internal sealed class DatasetRefreshInfo
    {
        public DateTime? LastRefreshLocal { get; init; }
        public DateTime? NextRefreshLocal { get; init; }
        public bool RefreshEnabled { get; init; } = true;
        public bool HasSchedule { get; init; }
    }

    internal sealed class DatasetDatasourcesResult
    {
        public bool QuerySucceeded { get; init; }
        public List<DatasourceItem> Datasources { get; init; } = new();
    }

    internal sealed class DatasourcesResponse
    {
        [JsonPropertyName("value")]
        public List<DatasourceItem> Value { get; set; } = new();
    }

    internal sealed class DatasourceItem
    {
        [JsonPropertyName("datasourceType")]
        public string? DatasourceType { get; set; }

        [JsonPropertyName("gatewayId")]
        public string? GatewayId { get; set; }

        [JsonPropertyName("connectionDetails")]
        public DatasourceConnectionDetails? ConnectionDetails { get; set; }
    }

    internal sealed class DatasourceConnectionDetails
    {
        [JsonPropertyName("server")]
        public string? Server { get; set; }

        [JsonPropertyName("database")]
        public string? Database { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }
    }

    internal sealed class PipelinesResponse
    {
        [JsonPropertyName("value")]
        public List<PipelineItem> Value { get; set; } = new();
    }

    internal sealed class PipelineItem
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("stages")]
        public List<PipelineStageItem>? Stages { get; set; }
    }

    internal sealed class PipelineStageItem
    {
        [JsonPropertyName("order")]
        public int Order { get; set; }

        [JsonPropertyName("workspaceId")]
        public Guid? WorkspaceId { get; set; }
    }

    internal sealed class AssignWorkspaceRequest
    {
        [JsonPropertyName("workspaceId")]
        public Guid WorkspaceId { get; set; }
    }

    internal sealed class DeployAllRequest
    {
        [JsonPropertyName("sourceStageOrder")]
        public int SourceStageOrder { get; set; }

        [JsonPropertyName("isBackwardDeployment")]
        public bool IsBackwardDeployment { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }

        [JsonPropertyName("options")]
        public DeploymentOptionsRequest? Options { get; set; }

        [JsonPropertyName("updateAppSettings")]
        public UpdateAppSettingsRequest? UpdateAppSettings { get; set; }
    }

    internal sealed class DeploymentOptionsRequest
    {
        [JsonPropertyName("allowCreateArtifact")]
        public bool AllowCreateArtifact { get; set; }

        [JsonPropertyName("allowOverwriteArtifact")]
        public bool AllowOverwriteArtifact { get; set; }
    }

    internal sealed class UpdateAppSettingsRequest
    {
        [JsonPropertyName("updateAppInTargetWorkspace")]
        public bool UpdateAppInTargetWorkspace { get; set; }
    }

    internal sealed class PipelineOperationResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("executionPlan")]
        public PipelineExecutionPlanResponse? ExecutionPlan { get; set; }
    }

    internal sealed class PipelineExecutionPlanResponse
    {
        [JsonPropertyName("steps")]
        public List<PipelineExecutionStepResponse>? Steps { get; set; }
    }

    internal sealed class PipelineExecutionStepResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("error")]
        public PipelineExecutionErrorResponse? Error { get; set; }
    }

    internal sealed class PipelineExecutionErrorResponse
    {
        [JsonPropertyName("errorCode")]
        public string? ErrorCode { get; set; }

        [JsonPropertyName("errorDetails")]
        public string? ErrorDetails { get; set; }
    }
}
