namespace RefreshVIR
{
    internal static class PowerBIService
    {
        public static Task<IReadOnlyList<PowerBiWorkspace>> GetWorkspacesAsync() =>
            PowerBiGroupApi.GetWorkspacesAsync();

        public static Task<PowerBiExistingReportInfo?> GetExistingReportAsync(
            Guid workspaceId,
            string reportName) =>
            PowerBiReportService.GetExistingReportAsync(workspaceId, reportName);

        public static Task PublishPbixAsync(
            Guid workspaceId,
            string pbixPath,
            IProgress<string>? progress = null) =>
            PowerBiPublishService.PublishPbixAsync(workspaceId, pbixPath, progress);

        public static Task UpdateWorkspaceAppAsync(
            Guid workspaceId,
            IProgress<string>? progress = null) =>
            PowerBiPipelineService.UpdateWorkspaceAppAsync(workspaceId, progress);

        public static Task<IReadOnlyList<PowerBiReportInfo>> GetWorkspaceReportsAsync(Guid workspaceId) =>
            PowerBiReportService.GetWorkspaceReportsAsync(workspaceId);
    }
}
