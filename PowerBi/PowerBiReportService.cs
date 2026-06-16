namespace RefreshVIR
{
    internal static class PowerBiReportService
    {
        internal static async Task<PowerBiExistingReportInfo?> GetExistingReportAsync(
            Guid workspaceId,
            string reportName)
        {
            using HttpClient httpClient = await PowerBiApiClient.CreateAuthorizedClientAsync();
            List<ReportItem> reports = await PowerBiGroupApi.GetReportsAsync(httpClient, workspaceId);
            ReportItem? report = reports.FirstOrDefault(item =>
                string.Equals(item.Name, reportName, StringComparison.OrdinalIgnoreCase));

            if (report == null)
                return null;

            Dictionary<Guid, DateTime> lastUploadByReportId =
                await PowerBiGroupApi.GetLastUploadByReportIdAsync(httpClient, workspaceId);

            Dictionary<Guid, DatasetItem> datasetsById =
                (await PowerBiGroupApi.GetDatasetsAsync(httpClient, workspaceId))
                .ToDictionary(d => d.Id);

            datasetsById.TryGetValue(report.DatasetId, out DatasetItem? dataset);
            DatasetDatasourcesResult datasourcesResult =
                await PowerBiDatasetApi.TryGetDatasourcesAsync(httpClient, workspaceId, report.DatasetId);

            bool hasEmbeddedReportData = PowerBiDatasetApi.HasEmbeddedReportData(
                report.DatasetId,
                dataset,
                datasourcesResult);

            DateTime? lastRefreshLocal = null;
            if (!hasEmbeddedReportData && report.DatasetId != Guid.Empty)
                lastRefreshLocal = await PowerBiDatasetApi.TryGetLastRefreshAsync(httpClient, workspaceId, report.DatasetId);

            DateTime? lastUploadLocal = lastUploadByReportId.TryGetValue(report.Id, out DateTime lastUpload)
                ? lastUpload
                : null;

            return new PowerBiExistingReportInfo
            {
                ReportName = report.Name ?? report.Id.ToString(),
                ReportType = PowerBiApiClient.FormatReportType(report.ReportType),
                DataSourceDisplay = hasEmbeddedReportData
                    ? "Beágyazott adat"
                    : dataset?.Name ?? "—",
                LastUploadLocal = lastUploadLocal,
                LastRefreshLocal = lastRefreshLocal,
                HasEmbeddedReportData = hasEmbeddedReportData
            };
        }

        internal static async Task<IReadOnlyList<PowerBiReportInfo>> GetWorkspaceReportsAsync(Guid workspaceId)
        {
            using HttpClient httpClient = await PowerBiApiClient.CreateAuthorizedClientAsync();

            List<ReportItem> reports = await PowerBiGroupApi.GetReportsAsync(httpClient, workspaceId);
            Dictionary<Guid, DatasetItem> datasetsById =
                (await PowerBiGroupApi.GetDatasetsAsync(httpClient, workspaceId))
                .ToDictionary(d => d.Id);

            Dictionary<Guid, DatasetRefreshInfo> refreshInfoByDataset = new();
            Dictionary<Guid, DatasetDatasourcesResult> datasourcesByDataset = new();

            foreach (Guid datasetId in reports
                         .Select(r => r.DatasetId)
                         .Where(id => id != Guid.Empty)
                         .Distinct())
            {
                datasetsById.TryGetValue(datasetId, out DatasetItem? dataset);
                DatasetDatasourcesResult datasourcesResult =
                    await PowerBiDatasetApi.TryGetDatasourcesAsync(httpClient, workspaceId, datasetId);
                datasourcesByDataset[datasetId] = datasourcesResult;

                if (PowerBiDatasetApi.HasEmbeddedReportData(datasetId, dataset, datasourcesResult))
                    continue;

                refreshInfoByDataset[datasetId] =
                    await PowerBiDatasetApi.GetDatasetRefreshInfoAsync(httpClient, workspaceId, datasetId);
            }

            Dictionary<Guid, DateTime> lastUploadByReportId =
                await PowerBiGroupApi.GetLastUploadByReportIdAsync(httpClient, workspaceId);

            return reports
                .Select(report =>
                {
                    DatasetItem? dataset = report.DatasetId != Guid.Empty
                        && datasetsById.TryGetValue(report.DatasetId, out DatasetItem? found)
                            ? found
                            : null;

                    refreshInfoByDataset.TryGetValue(report.DatasetId, out DatasetRefreshInfo? refreshInfo);
                    datasourcesByDataset.TryGetValue(
                        report.DatasetId,
                        out DatasetDatasourcesResult datasourcesResult);

                    bool hasEmbeddedReportData = PowerBiDatasetApi.HasEmbeddedReportData(
                        report.DatasetId,
                        dataset,
                        datasourcesResult);

                    DateTime? lastUploadLocal = lastUploadByReportId.TryGetValue(report.Id, out DateTime lastUpload)
                        ? lastUpload
                        : null;

                    return new PowerBiReportInfo
                    {
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
                })
                .OrderBy(r => r.ReportName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }
}
