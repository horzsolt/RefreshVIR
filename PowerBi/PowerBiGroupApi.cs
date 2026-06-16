using System.Net;
using System.Text.Json;

namespace RefreshVIR
{
    internal static class PowerBiGroupApi
    {
        internal static async Task<IReadOnlyList<PowerBiWorkspace>> GetWorkspacesAsync()
        {
            using HttpClient httpClient = await PowerBiApiClient.CreateAuthorizedClientAsync();

            using HttpResponseMessage response =
                await httpClient.GetAsync("groups");

            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Munkaterületek lekérdezése sikertelen ({(int)response.StatusCode}): {body}");

            GroupsResponse? groups =
                JsonSerializer.Deserialize<GroupsResponse>(body, PowerBiApiClient.JsonOptions);

            Guid? stagingWorkspaceId = await PowerBiPipelineService.TryGetAppUpdateStagingWorkspaceIdAsync(httpClient);

            return groups?.Value
                .Where(g => stagingWorkspaceId == null || g.Id != stagingWorkspaceId.Value)
                .Select(g => new PowerBiWorkspace
                {
                    Id = g.Id,
                    Name = g.Name ?? g.Id.ToString()
                })
                .OrderBy(g => g.Name)
                .ToList()
                ?? new List<PowerBiWorkspace>();
        }

        internal static async Task<Dictionary<Guid, DateTime>> GetLastUploadByReportIdAsync(
            HttpClient httpClient,
            Guid workspaceId)
        {
            using HttpResponseMessage response =
                await httpClient.GetAsync($"groups/{workspaceId}/imports");

            if (!response.IsSuccessStatusCode)
                return new Dictionary<Guid, DateTime>();

            string body = await response.Content.ReadAsStringAsync();
            ImportsResponse? imports = JsonSerializer.Deserialize<ImportsResponse>(body, PowerBiApiClient.JsonOptions);
            if (imports?.Value == null)
                return new Dictionary<Guid, DateTime>();

            Dictionary<Guid, DateTime> lastUploadByReportId = new();
            foreach (ImportItem import in imports.Value)
            {
                DateTime? uploadTime = PowerBiApiClient.ToLocalApiDateTime(import.UpdatedDateTime)
                    ?? PowerBiApiClient.ToLocalApiDateTime(import.CreatedDateTime);
                if (!uploadTime.HasValue)
                    continue;

                foreach (ImportReportItem importReport in import.Reports)
                {
                    if (importReport.Id == Guid.Empty)
                        continue;

                    if (!lastUploadByReportId.TryGetValue(importReport.Id, out DateTime existing)
                        || uploadTime.Value > existing)
                    {
                        lastUploadByReportId[importReport.Id] = uploadTime.Value;
                    }
                }
            }

            return lastUploadByReportId;
        }

        internal static async Task<List<ReportItem>> GetReportsAsync(
            HttpClient httpClient,
            Guid workspaceId)
        {
            using HttpResponseMessage response =
                await httpClient.GetAsync($"groups/{workspaceId}/reports");

            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Riportok lekérdezése sikertelen ({(int)response.StatusCode}): {body}");

            ReportsResponse? reports = JsonSerializer.Deserialize<ReportsResponse>(body, PowerBiApiClient.JsonOptions);
            return reports?.Value ?? new List<ReportItem>();
        }

        internal static async Task<List<DatasetItem>> GetDatasetsAsync(
            HttpClient httpClient,
            Guid workspaceId)
        {
            using HttpResponseMessage response =
                await httpClient.GetAsync($"groups/{workspaceId}/datasets");

            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Adathalmazok lekérdezése sikertelen ({(int)response.StatusCode}): {body}");

            DatasetsResponse? datasets = JsonSerializer.Deserialize<DatasetsResponse>(body, PowerBiApiClient.JsonOptions);
            return datasets?.Value ?? new List<DatasetItem>();
        }

        internal static async Task ClearWorkspaceContentAsync(
            HttpClient httpClient,
            Guid workspaceId)
        {
            List<ReportItem> reports = await GetReportsAsync(httpClient, workspaceId);
            foreach (ReportItem report in reports)
            {
                using HttpResponseMessage response = await httpClient.DeleteAsync(
                    $"groups/{workspaceId}/reports/{report.Id}");

                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
                    continue;

                string body = await response.Content.ReadAsStringAsync();
                throw PowerBiApiClient.CreateDetailedException(
                    $"Staging riport törlése sikertelen ({report.Name ?? report.Id.ToString()})",
                    new Dictionary<string, string>
                    {
                        ["Operation"] = "Clear staging workspace",
                        ["Workspace ID"] = workspaceId.ToString(),
                        ["Report ID"] = report.Id.ToString()
                    },
                    body,
                    (int)response.StatusCode);
            }

            List<DatasetItem> datasets = await GetDatasetsAsync(httpClient, workspaceId);
            foreach (DatasetItem dataset in datasets)
            {
                using HttpResponseMessage response = await httpClient.DeleteAsync(
                    $"groups/{workspaceId}/datasets/{dataset.Id}");

                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
                    continue;

                string body = await response.Content.ReadAsStringAsync();
                throw PowerBiApiClient.CreateDetailedException(
                    $"Staging adathalmaz törlése sikertelen ({dataset.Name ?? dataset.Id.ToString()})",
                    new Dictionary<string, string>
                    {
                        ["Operation"] = "Clear staging workspace",
                        ["Workspace ID"] = workspaceId.ToString(),
                        ["Dataset ID"] = dataset.Id.ToString()
                    },
                    body,
                    (int)response.StatusCode);
            }
        }
    }
}
