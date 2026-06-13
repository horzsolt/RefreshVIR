using Microsoft.Identity.Client;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RefreshVIR
{
    internal sealed class PowerBiWorkspace
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = "";
    }

    internal static class PowerBIService
    {
        private static readonly string[] Scopes =
            { "https://analysis.windows.net/powerbi/api/.default" };

        private static readonly Uri ApiBaseUri = new("https://api.powerbi.com/v1.0/myorg/");

        private static IPublicClientApplication? _clientApplication;

        public static async Task<IReadOnlyList<PowerBiWorkspace>> GetWorkspacesAsync()
        {
            using HttpClient httpClient = await CreateAuthorizedClientAsync();

            using HttpResponseMessage response =
                await httpClient.GetAsync("groups");

            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Munkaterületek lekérdezése sikertelen ({(int)response.StatusCode}): {body}");

            GroupsResponse? groups =
                JsonSerializer.Deserialize<GroupsResponse>(body, JsonOptions);

            return groups?.Value
                .Select(g => new PowerBiWorkspace
                {
                    Id = g.Id,
                    Name = g.Name ?? g.Id.ToString()
                })
                .OrderBy(g => g.Name)
                .ToList()
                ?? new List<PowerBiWorkspace>();
        }

        public static async Task PublishPbixAsync(
            Guid workspaceId,
            string pbixPath,
            string nameConflict,
            IProgress<string>? progress = null)
        {
            if (!File.Exists(pbixPath))
                throw new FileNotFoundException("A PBIX fájl nem található.", pbixPath);

            string fileName = Path.GetFileName(pbixPath);
            using HttpClient httpClient = await CreateAuthorizedClientAsync();

            progress?.Report("Fájl feltöltése...");

            await using FileStream fileStream = File.OpenRead(pbixPath);
            using MultipartFormDataContent form = new MultipartFormDataContent();
            using StreamContent fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(fileContent, "file", fileName);

            string importUrl =
                $"groups/{workspaceId}/imports?datasetDisplayName={Uri.EscapeDataString(fileName)}&nameConflict={Uri.EscapeDataString(nameConflict)}";

            using HttpResponseMessage uploadResponse =
                await httpClient.PostAsync(importUrl, form);

            string uploadBody = await uploadResponse.Content.ReadAsStringAsync();
            if (!uploadResponse.IsSuccessStatusCode)
                throw new InvalidOperationException($"Feltöltés sikertelen ({(int)uploadResponse.StatusCode}): {uploadBody}");

            ImportResponse? import =
                JsonSerializer.Deserialize<ImportResponse>(uploadBody, JsonOptions)
                ?? throw new InvalidOperationException("A Power BI válasz nem értelmezhető.");

            while (IsRunningImportState(import.ImportState))
            {
                progress?.Report($"Publikálás folyamatban ({import.ImportState})...");
                await Task.Delay(TimeSpan.FromSeconds(2));

                using HttpResponseMessage statusResponse =
                    await httpClient.GetAsync($"groups/{workspaceId}/imports/{import.Id}");

                string statusBody = await statusResponse.Content.ReadAsStringAsync();
                if (!statusResponse.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Státusz lekérdezése sikertelen ({(int)statusResponse.StatusCode}): {statusBody}");

                import = JsonSerializer.Deserialize<ImportResponse>(statusBody, JsonOptions)
                    ?? throw new InvalidOperationException("A Power BI státusz válasz nem értelmezhető.");
            }

            if (!string.Equals(import.ImportState, "Succeeded", StringComparison.OrdinalIgnoreCase))
            {
                string details = import.Error?.Code ?? import.ImportState ?? "Ismeretlen hiba";
                throw new InvalidOperationException($"Power BI publikálás sikertelen: {details}");
            }

            progress?.Report("Publikálás kész.");
        }

        private static bool IsRunningImportState(string? importState) =>
            string.Equals(importState, "Running", StringComparison.OrdinalIgnoreCase)
            || string.Equals(importState, "Publishing", StringComparison.OrdinalIgnoreCase);

        private static async Task<HttpClient> CreateAuthorizedClientAsync()
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

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private sealed class GroupsResponse
        {
            [JsonPropertyName("value")]
            public List<GroupItem> Value { get; set; } = new();
        }

        private sealed class GroupItem
        {
            [JsonPropertyName("id")]
            public Guid Id { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }
        }

        private sealed class ImportResponse
        {
            [JsonPropertyName("id")]
            public Guid Id { get; set; }

            [JsonPropertyName("importState")]
            public string? ImportState { get; set; }

            [JsonPropertyName("error")]
            public ImportErrorResponse? Error { get; set; }
        }

        private sealed class ImportErrorResponse
        {
            [JsonPropertyName("code")]
            public string? Code { get; set; }
        }
    }
}
