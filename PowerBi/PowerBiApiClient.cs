using Microsoft.Identity.Client;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace RefreshVIR
{
    internal static class PowerBiApiClient
    {
        private static readonly string[] Scopes =
            { "https://analysis.windows.net/powerbi/api/.default" };

        private static readonly Uri ApiBaseUri = new("https://api.powerbi.com/v1.0/myorg/");

        private static IPublicClientApplication? _clientApplication;

        internal static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        internal static async Task<HttpClient> CreateAuthorizedClientAsync()
        {
            string? configError = Configuration.GetPowerBiConfigurationError();
            if (configError != null)
                throw new InvalidOperationException(configError);

            string accessToken = await AcquireAccessTokenAsync();

            HttpClient httpClient = new HttpClient
            {
                BaseAddress = ApiBaseUri
            };
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            return httpClient;
        }

        private static async Task<string> AcquireAccessTokenAsync()
        {
            IPublicClientApplication app = _clientApplication ??= PublicClientApplicationBuilder
                .Create(Configuration.PowerBiClientId)
                .WithAuthority(
                    AzureCloudInstance.AzurePublic,
                    Configuration.PowerBiTenantId)
                .Build();

            try
            {
                AuthenticationResult result = await app
                    .AcquireTokenByUsernamePassword(
                        Scopes,
                        Configuration.PowerBiUser,
                        Configuration.PowerBiPassword)
                    .ExecuteAsync();

                return result.AccessToken;
            }
            catch (MsalServiceException ex)
            {
                throw new InvalidOperationException(
                    $"Power BI bejelentkezés sikertelen: {ex.Message}",
                    ex);
            }
            catch (MsalClientException ex)
            {
                throw new InvalidOperationException(
                    $"Power BI bejelentkezés sikertelen: {ex.Message}",
                    ex);
            }
        }

        internal static DateTime? ParseApiDateTime(string? value) =>
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed)
                ? parsed
                : null;

        internal static DateTime? ToLocalApiDateTime(string? value)
        {
            DateTime? timestamp = ParseApiDateTime(value);
            return timestamp.HasValue
                ? DateTime.SpecifyKind(timestamp.Value, DateTimeKind.Utc).ToLocalTime()
                : null;
        }

        internal static DetailedApplicationException CreateDetailedException(
            string message,
            Dictionary<string, string>? context = null,
            string? responseBody = null,
            int? httpStatusCode = null,
            Exception? innerException = null) =>
            new(message, context, responseBody, httpStatusCode, innerException);

        internal static string FormatReportType(string? reportType) =>
            reportType switch
            {
                "PaginatedReport" => "Lapozható riport",
                "PowerBIReport" => "Power BI riport",
                null or "" => "—",
                _ => reportType
            };

        internal static bool ContainsIgnoreCase(string? value, string fragment) =>
            value != null && value.Contains(fragment, StringComparison.OrdinalIgnoreCase);

        internal static Dictionary<string, string> CreatePublishContext(
            Guid workspaceId,
            string pbixPath,
            string fileName) =>
            new()
            {
                ["Operation"] = "Power BI PBIX publish",
                ["Workspace ID"] = workspaceId.ToString(),
                ["PBIX file"] = pbixPath,
                ["File name"] = fileName,
                ["Import URL"] =
                    $"groups/{workspaceId}/imports?datasetDisplayName={Uri.EscapeDataString(fileName)}&nameConflict=CreateOrOverwrite"
            };

        internal static string FormatImportFailureSummary(ImportResponse import) =>
            import.Error?.Code
            ?? import.ImportState
            ?? "Missing import state in API response";

        internal static string FormatImportErrorDetails(ImportErrorResponse? error)
        {
            if (error?.Details == null || error.Details.Count == 0)
                return "";

            return string.Join(
                " | ",
                error.Details
                    .Select(detail => detail.Code)
                    .Where(code => !string.IsNullOrWhiteSpace(code)));
        }

        internal static bool IsTerminalImportState(string? importState) =>
            string.Equals(importState, "Succeeded", StringComparison.OrdinalIgnoreCase)
            || string.Equals(importState, "Failed", StringComparison.OrdinalIgnoreCase);
    }
}
