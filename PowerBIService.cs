namespace RefreshVIR

{

    internal static class PowerBIService

    {

        public static Task<PowerBiSession> CreateSessionAsync() =>

            PowerBiApiClient.CreateSessionAsync();



        public static Task<IReadOnlyList<PowerBiWorkspace>> GetWorkspacesAsync(PowerBiSession session) =>

            PowerBiGroupApi.GetWorkspacesAsync(session);



        public static Task<PowerBiWorkspaceSnapshot> LoadWorkspaceSnapshotAsync(

            PowerBiSession session,

            Guid workspaceId) =>

            PowerBiReportService.LoadWorkspaceSnapshotAsync(session, workspaceId);



        public static Task<PowerBiExistingReportInfo?> GetExistingReportAsync(

            PowerBiSession session,

            Guid workspaceId,

            string reportName,

            PowerBiWorkspaceSnapshot? snapshot = null) =>

            PowerBiReportService.GetExistingReportAsync(session, workspaceId, reportName, snapshot);



        public static Task PublishPbixAsync(

            PowerBiSession session,

            Guid workspaceId,

            string pbixPath,

            IProgress<string>? progress = null) =>

            PowerBiPublishService.PublishPbixAsync(session, workspaceId, pbixPath, progress: progress);



        public static Task UpdateWorkspaceAppAsync(

            PowerBiSession session,

            Guid workspaceId,

            IProgress<AppUpdateProgressReport>? progress = null) =>

            PowerBiPipelineService.UpdateWorkspaceAppAsync(session, workspaceId, progress);



        public static Task<PowerBiWorkspaceSnapshot> RefreshUploadTimesAsync(

            PowerBiSession session,

            PowerBiWorkspaceSnapshot snapshot) =>

            PowerBiReportService.RefreshUploadTimesAsync(session, snapshot);

    }

}

