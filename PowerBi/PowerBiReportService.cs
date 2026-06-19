namespace RefreshVIR
{
    internal static class PowerBiReportService
    {
        internal static async Task<PowerBiWorkspaceSnapshot> LoadWorkspaceSnapshotAsync(
            PowerBiSession session,
            Guid workspaceId)
        {
            HttpClient httpClient = session.HttpClient;
            List<string> loadWarnings = new();

            Task<List<ReportItem>> reportsTask = PowerBiGroupApi.GetReportsAsync(httpClient, workspaceId);
            Task<List<DatasetItem>> datasetsTask = PowerBiGroupApi.GetDatasetsAsync(httpClient, workspaceId);
            Task<Dictionary<Guid, DateTime>> uploadsTask =
                PowerBiGroupApi.GetLastUploadByReportIdAsync(httpClient, workspaceId, loadWarnings);

            await Task.WhenAll(reportsTask, datasetsTask, uploadsTask);

            List<ReportItem> reports = await reportsTask;
            Dictionary<Guid, DatasetItem> datasetsById = (await datasetsTask).ToDictionary(d => d.Id);
            Dictionary<Guid, DateTime> lastUploadByReportId = await uploadsTask;

            Dictionary<Guid, DatasetRefreshInfo> refreshInfoByDataset = new();
            Dictionary<Guid, DatasetDatasourcesResult> datasourcesByDataset = new();

            Guid[] datasetIds = reports
                .Select(r => r.DatasetId)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToArray();

            if (datasetIds.Length > 0)
            {
                (Guid DatasetId, DatasetDatasourcesResult Result)[] datasourceResults = await Task.WhenAll(
                    datasetIds.Select(async datasetId =>
                    {
                        DatasetDatasourcesResult result =
                            await PowerBiDatasetApi.TryGetDatasourcesAsync(httpClient, workspaceId, datasetId);
                        return (datasetId, result);
                    }));

                foreach ((Guid datasetId, DatasetDatasourcesResult result) in datasourceResults)
                    datasourcesByDataset[datasetId] = result;

                (Guid DatasetId, DatasetRefreshInfo Info)[] refreshResults = await Task.WhenAll(
                    datasetIds
                        .Where(datasetId =>
                        {
                            datasetsById.TryGetValue(datasetId, out DatasetItem? dataset);
                            datasourcesByDataset.TryGetValue(
                                datasetId,
                                out DatasetDatasourcesResult? datasourcesResult);
                            return !PowerBiDatasetApi.HasEmbeddedReportData(
                                datasetId,
                                dataset,
                                datasourcesResult);
                        })
                        .Select(async datasetId => (
                            datasetId,
                            await PowerBiDatasetApi.GetDatasetRefreshInfoAsync(
                                httpClient,
                                workspaceId,
                                datasetId))));

                foreach ((Guid datasetId, DatasetRefreshInfo info) in refreshResults)
                    refreshInfoByDataset[datasetId] = info;
            }

            return BuildSnapshot(
                workspaceId,
                reports,
                datasetsById,
                refreshInfoByDataset,
                datasourcesByDataset,
                lastUploadByReportId,
                loadWarnings);
        }

        internal static async Task<PowerBiWorkspaceSnapshot> RefreshUploadTimesAsync(
            PowerBiSession session,
            PowerBiWorkspaceSnapshot snapshot)
        {
            List<string> loadWarnings = snapshot.LoadWarnings.ToList();
            Dictionary<Guid, DateTime> lastUploadByReportId =
                await PowerBiGroupApi.GetLastUploadByReportIdAsync(
                    session.HttpClient,
                    snapshot.WorkspaceId,
                    loadWarnings);

            List<PowerBiReportInfo> reports = snapshot.Reports
                .Select(report =>
                {
                    DateTime? lastUploadLocal = lastUploadByReportId.TryGetValue(
                        report.ReportId,
                        out DateTime lastUpload)
                        ? lastUpload
                        : report.LastUploadLocal;

                    if (lastUploadLocal == report.LastUploadLocal)
                        return report;

                    return new PowerBiReportInfo
                    {
                        ReportId = report.ReportId,
                        DatasetId = report.DatasetId,
                        ReportName = report.ReportName,
                        DatasetName = report.DatasetName,
                        ReportType = report.ReportType,
                        LastRefreshLocal = report.LastRefreshLocal,
                        NextRefreshLocal = report.NextRefreshLocal,
                        LastUploadLocal = lastUploadLocal,
                        RefreshEnabled = report.RefreshEnabled,
                        HasRefreshSchedule = report.HasRefreshSchedule,
                        HasEmbeddedReportData = report.HasEmbeddedReportData
                    };
                })
                .ToList();

            Dictionary<string, PowerBiReportInfo> reportsByName = new(StringComparer.OrdinalIgnoreCase);
            foreach (PowerBiReportInfo report in reports)
                reportsByName.TryAdd(report.ReportName, report);

            return new PowerBiWorkspaceSnapshot
            {
                WorkspaceId = snapshot.WorkspaceId,
                LoadedAt = DateTime.Now,
                Reports = reports,
                ReportsByName = reportsByName,
                LoadWarnings = loadWarnings
            };
        }

        internal static Task<PowerBiExistingReportInfo?> GetExistingReportAsync(
            PowerBiSession session,
            Guid workspaceId,
            string reportName,
            PowerBiWorkspaceSnapshot? snapshot = null)
        {
            if (snapshot != null && snapshot.WorkspaceId == workspaceId)
                return Task.FromResult(snapshot.TryGetExistingReportByName(reportName));

            return GetExistingReportFromApiAsync(session, workspaceId, reportName);
        }

        private static async Task<PowerBiExistingReportInfo?> GetExistingReportFromApiAsync(
            PowerBiSession session,
            Guid workspaceId,
            string reportName)
        {
            PowerBiWorkspaceSnapshot loadedSnapshot =
                await LoadWorkspaceSnapshotAsync(session, workspaceId);

            return loadedSnapshot.TryGetExistingReportByName(reportName);
        }

        internal static async Task<IReadOnlyList<PowerBiReportInfo>> GetWorkspaceReportsAsync(
            PowerBiSession session,
            Guid workspaceId)
        {
            PowerBiWorkspaceSnapshot snapshot = await LoadWorkspaceSnapshotAsync(session, workspaceId);
            return snapshot.Reports;
        }

        private static PowerBiWorkspaceSnapshot BuildSnapshot(
            Guid workspaceId,
            List<ReportItem> reports,
            Dictionary<Guid, DatasetItem> datasetsById,
            Dictionary<Guid, DatasetRefreshInfo> refreshInfoByDataset,
            Dictionary<Guid, DatasetDatasourcesResult> datasourcesByDataset,
            Dictionary<Guid, DateTime> lastUploadByReportId,
            List<string> loadWarnings)
        {
            List<PowerBiReportInfo> reportInfos = reports
                .Select(report => BuildReportInfo(
                    report,
                    datasetsById,
                    refreshInfoByDataset,
                    datasourcesByDataset,
                    lastUploadByReportId))
                .OrderBy(r => r.ReportName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            Dictionary<string, PowerBiReportInfo> reportsByName = new(StringComparer.OrdinalIgnoreCase);
            foreach (PowerBiReportInfo report in reportInfos)
                reportsByName.TryAdd(report.ReportName, report);

            return new PowerBiWorkspaceSnapshot
            {
                WorkspaceId = workspaceId,
                LoadedAt = DateTime.Now,
                Reports = reportInfos,
                ReportsByName = reportsByName,
                LoadWarnings = loadWarnings
            };
        }

        private static PowerBiReportInfo BuildReportInfo(
            ReportItem report,
            Dictionary<Guid, DatasetItem> datasetsById,
            Dictionary<Guid, DatasetRefreshInfo> refreshInfoByDataset,
            Dictionary<Guid, DatasetDatasourcesResult> datasourcesByDataset,
            Dictionary<Guid, DateTime> lastUploadByReportId)
        {
            DatasetItem? dataset = report.DatasetId != Guid.Empty
                && datasetsById.TryGetValue(report.DatasetId, out DatasetItem? found)
                    ? found
                    : null;

            refreshInfoByDataset.TryGetValue(report.DatasetId, out DatasetRefreshInfo? refreshInfo);
            datasourcesByDataset.TryGetValue(
                report.DatasetId,
                out DatasetDatasourcesResult? datasourcesResult);

            bool hasEmbeddedReportData = PowerBiDatasetApi.HasEmbeddedReportData(
                report.DatasetId,
                dataset,
                datasourcesResult);

            DateTime? lastUploadLocal = lastUploadByReportId.TryGetValue(report.Id, out DateTime lastUpload)
                ? lastUpload
                : null;

            return new PowerBiReportInfo
            {
                ReportId = report.Id,
                DatasetId = report.DatasetId,
                ReportName = report.Name ?? report.Id.ToString(),
                DatasetName = dataset?.Name ?? "—",
                ReportType = PowerBiApiClient.FormatReportType(report.ReportType),
                LastRefreshLocal = refreshInfo?.LastRefreshLocal,
                NextRefreshLocal = refreshInfo?.NextRefreshLocal,
                LastUploadLocal = lastUploadLocal,
                RefreshEnabled = refreshInfo?.RefreshEnabled ?? true,
                HasRefreshSchedule = refreshInfo?.HasSchedule ?? false,
                HasEmbeddedReportData = hasEmbeddedReportData
            };
        }
    }
}
