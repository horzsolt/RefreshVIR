using System.Globalization;
using System.Text;
using System.Text.Json;

namespace RefreshVIR
{
    internal static class PowerBiDatasetApi
    {
        internal static async Task<DatasetRefreshInfo> GetDatasetRefreshInfoAsync(
            HttpClient httpClient,
            Guid workspaceId,
            Guid datasetId)
        {
            RefreshSchedule? schedule =
                await TryGetRefreshScheduleAsync(httpClient, workspaceId, datasetId);

            DateTime? lastRefresh = await TryGetLastRefreshAsync(httpClient, workspaceId, datasetId);

            return new DatasetRefreshInfo
            {
                LastRefreshLocal = lastRefresh,
                NextRefreshLocal = CalculateNextRefreshLocal(schedule),
                RefreshEnabled = schedule?.Enabled ?? true,
                HasSchedule = schedule != null
            };
        }

        internal static async Task<DatasetDatasourcesResult> TryGetDatasourcesAsync(
            HttpClient httpClient,
            Guid workspaceId,
            Guid datasetId)
        {
            using HttpResponseMessage response =
                await httpClient.GetAsync($"groups/{workspaceId}/datasets/{datasetId}/datasources");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new DatasetDatasourcesResult
                {
                    QuerySucceeded = true,
                    Datasources = new List<DatasourceItem>()
                };
            }

            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return new DatasetDatasourcesResult
                {
                    QuerySucceeded = false,
                    Datasources = new List<DatasourceItem>()
                };
            }

            DatasourcesResponse? datasources =
                JsonSerializer.Deserialize<DatasourcesResponse>(body, PowerBiApiClient.JsonOptions);

            return new DatasetDatasourcesResult
            {
                QuerySucceeded = true,
                Datasources = datasources?.Value ?? new List<DatasourceItem>()
            };
        }

        internal static async Task<DateTime?> TryGetLastRefreshAsync(
            HttpClient httpClient,
            Guid workspaceId,
            Guid datasetId)
        {
            using HttpResponseMessage response =
                await httpClient.GetAsync($"groups/{workspaceId}/datasets/{datasetId}/refreshes?$top=5");

            if (!response.IsSuccessStatusCode)
                return null;

            string body = await response.Content.ReadAsStringAsync();
            RefreshesResponse? refreshes =
                JsonSerializer.Deserialize<RefreshesResponse>(body, PowerBiApiClient.JsonOptions);

            RefreshHistoryItem? latest = refreshes?.Value
                .Where(r => string.Equals(r.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.EndTime ?? r.StartTime)
                .FirstOrDefault();

            if (latest == null)
                return null;

            DateTime? timestamp = PowerBiApiClient.ParseApiDateTime(latest.EndTime)
                ?? PowerBiApiClient.ParseApiDateTime(latest.StartTime);
            return timestamp.HasValue
                ? DateTime.SpecifyKind(timestamp.Value, DateTimeKind.Utc).ToLocalTime()
                : null;
        }

        internal static async Task WaitForWorkspaceRefreshesToCompleteAsync(
            HttpClient httpClient,
            Guid workspaceId,
            AppUpdateProgressTracker? tracker = null,
            TimeSpan? timeout = null)
        {
            TimeSpan waitTimeout = timeout ?? TimeSpan.FromMinutes(15);
            TimeSpan pollInterval = TimeSpan.FromSeconds(5);
            DateTime deadline = DateTime.UtcNow + waitTimeout;

            List<DatasetItem> datasets = await PowerBiGroupApi.GetDatasetsAsync(httpClient, workspaceId);
            List<DatasetItem> refreshableDatasets = datasets
                .Where(dataset => dataset.IsRefreshable != false)
                .ToList();

            if (refreshableDatasets.Count == 0)
                return;

            int pollCount = 0;
            while (DateTime.UtcNow < deadline)
            {
                List<string> inProgressNames = new();

                foreach (DatasetItem dataset in refreshableDatasets)
                {
                    if (await IsDatasetRefreshInProgressAsync(httpClient, workspaceId, dataset.Id))
                    {
                        inProgressNames.Add(
                            string.IsNullOrWhiteSpace(dataset.Name)
                                ? dataset.Id.ToString()
                                : dataset.Name);
                    }
                }

                if (inProgressNames.Count == 0)
                {
                    tracker?.Report("Adatmodellek készen állnak az app frissítéshez.");
                    return;
                }

                pollCount++;
                int waitPercent = Math.Clamp(pollCount * 10, 10, 90);
                tracker?.Report(
                    $"Adatmodell frissítés folyamatban ({string.Join(", ", inProgressNames)})...",
                    waitPercent);
                await Task.Delay(pollInterval);
            }

            throw new TimeoutException(
                "Az adatmodell frissítése túllépte az időkorlátot. " +
                "Várj, amíg a frissítés befejeződik, majd próbáld újra az app frissítést.");
        }

        internal static async Task<bool> IsDatasetRefreshInProgressAsync(
            HttpClient httpClient,
            Guid workspaceId,
            Guid datasetId)
        {
            RefreshHistoryItem? latest =
                await TryGetLatestRefreshAsync(httpClient, workspaceId, datasetId);

            return latest != null && IsRefreshInProgress(latest);
        }

        private static async Task<RefreshHistoryItem?> TryGetLatestRefreshAsync(
            HttpClient httpClient,
            Guid workspaceId,
            Guid datasetId)
        {
            using HttpResponseMessage response =
                await httpClient.GetAsync($"groups/{workspaceId}/datasets/{datasetId}/refreshes?$top=1");

            if (!response.IsSuccessStatusCode)
                return null;

            string body = await response.Content.ReadAsStringAsync();
            RefreshesResponse? refreshes =
                JsonSerializer.Deserialize<RefreshesResponse>(body, PowerBiApiClient.JsonOptions);

            return refreshes?.Value.FirstOrDefault();
        }

        private static bool IsRefreshInProgress(RefreshHistoryItem refresh)
        {
            if (!string.IsNullOrWhiteSpace(refresh.Status))
            {
                if (string.Equals(refresh.Status, "InProgress", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(refresh.Status, "Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(refresh.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(refresh.Status, "Failed", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(refresh.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(refresh.Status, "TimedOut", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(refresh.Status, "Disabled", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return string.IsNullOrWhiteSpace(refresh.EndTime);
        }

        internal static bool HasEmbeddedReportData(
            Guid datasetId,
            DatasetItem? dataset,
            DatasetDatasourcesResult? datasourcesResult)
        {
            if (datasetId == Guid.Empty)
                return true;

            if (datasourcesResult?.QuerySucceeded == true)
            {
                if (datasourcesResult.Datasources.Count == 0)
                    return true;

                return !HasExternalDataSources(datasourcesResult.Datasources);
            }

            if (dataset == null)
                return true;

            if (dataset.IsRefreshable == true)
                return false;

            return !UsesExternalDataConnection(dataset);
        }

        internal static async Task<RefreshScheduleSnapshot?> TryDisableRefreshScheduleForPublishAsync(
            HttpClient httpClient,
            Guid workspaceId,
            Guid datasetId)
        {
            RefreshSchedule? schedule = await TryGetRefreshScheduleAsync(httpClient, workspaceId, datasetId);
            if (schedule == null || !schedule.Enabled)
                return null;

            await DisableRefreshScheduleAsync(httpClient, workspaceId, datasetId);

            return new RefreshScheduleSnapshot
            {
                WasDisabledForPublish = true,
                Schedule = CloneRefreshSchedule(schedule)
            };
        }

        internal static async Task RestoreRefreshScheduleAfterPublishAsync(
            HttpClient httpClient,
            Guid workspaceId,
            Guid datasetId,
            RefreshScheduleSnapshot? snapshot)
        {
            if (snapshot?.WasDisabledForPublish != true)
                return;

            await PatchRefreshScheduleAsync(httpClient, workspaceId, datasetId, snapshot.Schedule);
        }

        private static async Task DisableRefreshScheduleAsync(
            HttpClient httpClient,
            Guid workspaceId,
            Guid datasetId)
        {
            // Power BI rejects disable requests that include any field besides enabled.
            const string json = """{"value":{"enabled":false}}""";

            using HttpRequestMessage httpRequest = new(
                HttpMethod.Patch,
                $"groups/{workspaceId}/datasets/{datasetId}/refreshSchedule")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using HttpResponseMessage response = await httpClient.SendAsync(httpRequest);
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Adatmodell frissítési ütemezés kikapcsolása sikertelen ({(int)response.StatusCode}): {body}");
            }
        }

        private static RefreshSchedule CloneRefreshSchedule(RefreshSchedule schedule) =>
            new()
            {
                Days = schedule.Days.ToList(),
                Times = schedule.Times.ToList(),
                Enabled = schedule.Enabled,
                LocalTimeZoneId = schedule.LocalTimeZoneId,
                NotifyOption = schedule.NotifyOption
            };

        private static async Task PatchRefreshScheduleAsync(
            HttpClient httpClient,
            Guid workspaceId,
            Guid datasetId,
            RefreshSchedule schedule)
        {
            RefreshScheduleRequest request = new() { Value = schedule };
            string json = JsonSerializer.Serialize(request, PowerBiApiClient.JsonOptions);

            using HttpRequestMessage httpRequest = new(
                HttpMethod.Patch,
                $"groups/{workspaceId}/datasets/{datasetId}/refreshSchedule")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using HttpResponseMessage response = await httpClient.SendAsync(httpRequest);
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Adatmodell frissítési ütemezés módosítása sikertelen ({(int)response.StatusCode}): {body}");
            }
        }

        private static async Task<RefreshSchedule?> TryGetRefreshScheduleAsync(
            HttpClient httpClient,
            Guid workspaceId,
            Guid datasetId)
        {
            using HttpResponseMessage response =
                await httpClient.GetAsync($"groups/{workspaceId}/datasets/{datasetId}/refreshSchedule");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return null;

            return JsonSerializer.Deserialize<RefreshSchedule>(body, PowerBiApiClient.JsonOptions);
        }

        private static bool HasExternalDataSources(IReadOnlyList<DatasourceItem> datasources)
        {
            foreach (DatasourceItem datasource in datasources)
            {
                if (!IsEmbeddedOnlyDatasource(datasource))
                    return true;
            }

            return false;
        }

        private static bool IsEmbeddedOnlyDatasource(DatasourceItem datasource)
        {
            if (string.Equals(datasource.DatasourceType, "File", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(datasource.DatasourceType, "Web", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(datasource.ConnectionDetails?.Url))
                return true;

            return string.IsNullOrWhiteSpace(datasource.GatewayId)
                && string.IsNullOrWhiteSpace(datasource.ConnectionDetails?.Server)
                && string.IsNullOrWhiteSpace(datasource.ConnectionDetails?.Url)
                && string.IsNullOrWhiteSpace(datasource.ConnectionDetails?.Database);
        }

        private static bool UsesExternalDataConnection(DatasetItem dataset)
        {
            if (dataset.IsOnPremGatewayRequired == true)
                return true;

            if (PowerBiApiClient.ContainsIgnoreCase(dataset.TargetStorageMode, "DirectQuery")
                || PowerBiApiClient.ContainsIgnoreCase(dataset.TargetStorageMode, "Composite"))
                return true;

            if (PowerBiApiClient.ContainsIgnoreCase(dataset.ContentProviderType, "DirectQuery")
                || string.Equals(dataset.ContentProviderType, "RealTimeInPushMode", StringComparison.OrdinalIgnoreCase)
                || string.Equals(dataset.ContentProviderType, "RealTimeInStreamingMode", StringComparison.OrdinalIgnoreCase)
                || string.Equals(dataset.ContentProviderType, "RealTimeInPubNubMode", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static DateTime? CalculateNextRefreshLocal(RefreshSchedule? schedule)
        {
            if (schedule == null || !schedule.Enabled)
                return null;

            if (schedule.Days.Count == 0 || schedule.Times.Count == 0)
                return null;

            TimeZoneInfo timeZone;
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(schedule.LocalTimeZoneId ?? "UTC");
            }
            catch
            {
                timeZone = TimeZoneInfo.Utc;
            }

            DateTime nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
            DateTime? next = null;

            for (int dayOffset = 0; dayOffset <= 7; dayOffset++)
            {
                DateTime date = nowLocal.Date.AddDays(dayOffset);
                string dayName = date.DayOfWeek.ToString();

                if (!schedule.Days.Any(d => string.Equals(d, dayName, StringComparison.OrdinalIgnoreCase)))
                    continue;

                foreach (string timeText in schedule.Times)
                {
                    if (!TimeSpan.TryParse(timeText, CultureInfo.InvariantCulture, out TimeSpan timeOfDay))
                        continue;

                    DateTime candidate = date.Add(timeOfDay);
                    if (candidate <= nowLocal)
                        continue;

                    if (next == null || candidate < next)
                        next = candidate;
                }
            }

            return next;
        }
    }
}
