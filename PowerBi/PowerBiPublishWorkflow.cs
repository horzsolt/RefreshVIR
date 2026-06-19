namespace RefreshVIR
{
    internal sealed class PowerBiPublishRequest
    {
        public required PowerBiSession Session { get; init; }
        public required PowerBiWorkspace Workspace { get; init; }
        public required string PbixPath { get; init; }
        public required string ReportName { get; init; }
        public PowerBiWorkspaceSnapshot? Snapshot { get; init; }
        public IProgress<string>? Progress { get; set; }
        public IProgress<AppUpdateProgressReport>? AppUpdateProgress { get; set; }
    }

    internal static class PowerBiPublishWorkflow
    {
        internal static PowerBiExistingReportInfo? TryGetExistingReportFromSnapshot(
            PowerBiPublishRequest request)
        {
            if (request.Snapshot?.WorkspaceId == request.Workspace.Id)
                return request.Snapshot.TryGetExistingReportByName(request.ReportName);

            return null;
        }

        internal static Task<PowerBiExistingReportInfo?> GetExistingReportAsync(
            PowerBiPublishRequest request) =>
            PowerBiReportService.GetExistingReportAsync(
                request.Session,
                request.Workspace.Id,
                request.ReportName,
                request.Snapshot);

        internal static Task PublishReportAsync(PowerBiPublishRequest request)
        {
            PowerBiReportInfo? report = request.Snapshot?.TryGetReportByName(request.ReportName);
            Guid? datasetId = report is { HasEmbeddedReportData: false, DatasetId: var id } && id != Guid.Empty
                ? id
                : null;

            return PowerBiPublishService.PublishPbixAsync(
                request.Session,
                request.Workspace.Id,
                request.PbixPath,
                datasetId,
                request.Progress);
        }

        internal static Task UpdateWorkspaceAppAsync(PowerBiPublishRequest request) =>
            PowerBiPipelineService.UpdateWorkspaceAppAsync(
                request.Session,
                request.Workspace.Id,
                request.AppUpdateProgress);

        internal static Task<PowerBiWorkspaceSnapshot> RefreshAfterPublishAsync(
            PowerBiSession session,
            PowerBiWorkspaceSnapshot snapshot) =>
            PowerBiReportService.RefreshUploadTimesAsync(session, snapshot);
    }
}
