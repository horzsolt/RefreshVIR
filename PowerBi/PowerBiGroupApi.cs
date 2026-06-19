using System.Net;
using System.Text.Json;

namespace RefreshVIR
{
    internal static class PowerBiGroupApi
    {
        internal static async Task<IReadOnlyList<PowerBiWorkspace>> GetWorkspacesAsync(PowerBiSession session)
        {
            HttpClient httpClient = session.HttpClient;

            using HttpResponseMessage response =
                await httpClient.GetAsync("groups");

            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Munkaterületek lekérdezése sikertelen ({(int)response.StatusCode}): {body}");

            GroupsResponse? groups =
                JsonSerializer.Deserialize<GroupsResponse>(body, PowerBiApiClient.JsonOptions);

            Guid? stagingWorkspaceId = (await session.Pipeline.TryGetAppUpdatePipelineAsync())?.StagingWorkspaceId;

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

        internal static async Task<string> GetWorkspaceAccessEmailAsync(
            HttpClient httpClient,
            Guid workspaceId,
            string? workspaceName = null)
        {
            using HttpResponseMessage response =
                await httpClient.GetAsync($"groups/{workspaceId}/users");

            if (!response.IsSuccessStatusCode)
                return "";

            string body = await response.Content.ReadAsStringAsync();
            GroupUsersResponse? users =
                JsonSerializer.Deserialize<GroupUsersResponse>(body, PowerBiApiClient.JsonOptions);

            if (users?.Value == null || users.Value.Count == 0)
                return "";

            return ResolveWorkspaceAccessEmail(
                users.Value,
                Configuration.PowerBiUser.Trim(),
                workspaceName);
        }

        private static string ResolveWorkspaceAccessEmail(
            IReadOnlyList<GroupUserItem> users,
            string serviceAccount,
            string? workspaceName)
        {
            List<AccessEmailCandidate> candidates = users
                .Where(user => !IsServiceAccountUser(user, serviceAccount))
                .Where(user => !string.Equals(user.PrincipalType, "App", StringComparison.OrdinalIgnoreCase))
                .Select(user => new AccessEmailCandidate(user))
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Email))
                .ToList();

            AccessEmailCandidate? bestCandidate = candidates
                .OrderBy(candidate => ScoreAccessEmail(candidate, workspaceName))
                .ThenByDescending(candidate => candidate.IsGroup)
                .ThenByDescending(candidate => candidate.IsAdmin)
                .ThenBy(candidate => candidate.Email, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (bestCandidate != null)
                return bestCandidate.Email!;

            if (!string.IsNullOrWhiteSpace(serviceAccount))
                return serviceAccount;

            return "";
        }

        private static int ScoreAccessEmail(AccessEmailCandidate candidate, string? workspaceName)
        {
            string email = candidate.Email!;
            int score = 0;

            if (email.Contains("vir_gw@", StringComparison.OrdinalIgnoreCase)
                || email.StartsWith("vir_gw@", StringComparison.OrdinalIgnoreCase))
            {
                return -100;
            }

            if (email.EndsWith("@goodwillpharma365.hu", StringComparison.OrdinalIgnoreCase))
                score -= 40;

            if (!string.IsNullOrWhiteSpace(workspaceName)
                && email.Contains(NormalizeWorkspaceToken(workspaceName), StringComparison.OrdinalIgnoreCase))
            {
                score -= 20;
            }

            if (candidate.IsGroup)
                score -= 10;
            else
                score += 20;

            if (candidate.IsAdmin)
                score -= 5;
            else
                score += 5;

            if (email.Contains("ebond", StringComparison.OrdinalIgnoreCase))
                score += 100;

            return score;
        }

        private static string NormalizeWorkspaceToken(string workspaceName)
        {
            string normalized = workspaceName.Trim();
            int parenIndex = normalized.IndexOf('(');
            if (parenIndex > 0)
                normalized = normalized[..parenIndex];

            return normalized
                .Replace(" ", "", StringComparison.Ordinal)
                .Replace("-", "", StringComparison.Ordinal)
                .ToLowerInvariant();
        }

        private static bool IsServiceAccountUser(GroupUserItem user, string serviceAccount)
        {
            if (string.IsNullOrWhiteSpace(serviceAccount))
                return false;

            string normalizedServiceAccount = NormalizeAccountReference(serviceAccount);

            foreach (string? candidate in new[] { user.EmailAddress, user.Identifier, user.DisplayName })
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                if (string.Equals(candidate, serviceAccount, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (string.Equals(
                        NormalizeAccountReference(candidate),
                        normalizedServiceAccount,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeAccountReference(string value)
        {
            value = value.Trim();
            int atIndex = value.IndexOf('@');
            return atIndex >= 0 ? value[..atIndex] : value;
        }

        private sealed class AccessEmailCandidate
        {
            internal AccessEmailCandidate(GroupUserItem user)
            {
                Email = ResolveEmail(user);
                IsGroup = string.Equals(user.PrincipalType, "Group", StringComparison.OrdinalIgnoreCase);
                IsAdmin = string.Equals(user.GroupUserAccessRight, "Admin", StringComparison.OrdinalIgnoreCase);
            }

            internal string? Email { get; }
            internal bool IsGroup { get; }
            internal bool IsAdmin { get; }
            internal bool HasAtSignEmail =>
                !string.IsNullOrWhiteSpace(Email) && Email.Contains('@');

            private static string? ResolveEmail(GroupUserItem user)
            {
                if (!string.IsNullOrWhiteSpace(user.EmailAddress))
                    return user.EmailAddress.Trim();

                if (!string.IsNullOrWhiteSpace(user.Identifier) && user.Identifier.Contains('@'))
                    return user.Identifier.Trim();

                if (!string.IsNullOrWhiteSpace(user.DisplayName) && user.DisplayName.Contains('@'))
                    return user.DisplayName.Trim();

                if (ContainsVirGwReference(user))
                    return "vir_gw@goodwillpharma365.hu";

                return null;
            }

            private static bool ContainsVirGwReference(GroupUserItem user)
            {
                foreach (string? value in new[] { user.DisplayName, user.Identifier, user.EmailAddress })
                {
                    if (!string.IsNullOrWhiteSpace(value)
                        && value.Contains("vir_gw", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        internal static async Task<Dictionary<Guid, DateTime>> GetLastUploadByReportIdAsync(
            HttpClient httpClient,
            Guid workspaceId,
            ICollection<string>? loadWarnings = null)
        {
            using HttpResponseMessage response =
                await httpClient.GetAsync($"groups/{workspaceId}/imports");

            if (!response.IsSuccessStatusCode)
            {
                loadWarnings?.Add("Import előzmények nem elérhetők.");
                return new Dictionary<Guid, DateTime>();
            }

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
            Guid workspaceId,
            AppUpdateProgressTracker? tracker = null)
        {
            List<ReportItem> reports = await GetReportsAsync(httpClient, workspaceId);
            int totalItems = reports.Count;
            List<DatasetItem> datasets = await GetDatasetsAsync(httpClient, workspaceId);
            totalItems += datasets.Count;

            int completedItems = 0;
            tracker?.Report($"Staging munkaterület: {reports.Count} riport törlése...");
            for (int index = 0; index < reports.Count; index++)
            {
                ReportItem report = reports[index];
                completedItems++;
                int percent = totalItems == 0 ? 100 : completedItems * 100 / totalItems;
                tracker?.ReportSubStep(
                    $"Staging: riport törlése — {report.Name ?? report.Id.ToString()}",
                    completedItems,
                    totalItems);
                tracker?.Report(
                    $"Staging munkaterület: riport törlése ({index + 1}/{reports.Count}) — {report.Name ?? report.Id.ToString()}",
                    percent);

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

            tracker?.Report($"Staging munkaterület: {datasets.Count} adathalmaz törlése...");
            for (int index = 0; index < datasets.Count; index++)
            {
                DatasetItem dataset = datasets[index];
                completedItems++;
                int percent = totalItems == 0 ? 100 : completedItems * 100 / totalItems;
                tracker?.Report(
                    $"Staging munkaterület: adathalmaz törlése ({index + 1}/{datasets.Count}) — {dataset.Name ?? dataset.Id.ToString()}",
                    percent);

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