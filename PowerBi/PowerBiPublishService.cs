using System.Net.Http.Headers;
using System.Text.Json;

namespace RefreshVIR
{
    internal static class PowerBiPublishService
    {
        internal static async Task PublishPbixAsync(
            Guid workspaceId,
            string pbixPath,
            IProgress<string>? progress = null)
        {
            if (!File.Exists(pbixPath))
                throw new FileNotFoundException("A PBIX fájl nem található.", pbixPath);

            string fileName = Path.GetFileName(pbixPath);
            Dictionary<string, string> publishContext = PowerBiApiClient.CreatePublishContext(workspaceId, pbixPath, fileName);
            using HttpClient httpClient = await PowerBiApiClient.CreateAuthorizedClientAsync();

            progress?.Report("Fájl feltöltése...");

            await using FileStream fileStream = File.OpenRead(pbixPath);
            using MultipartFormDataContent form = new MultipartFormDataContent();
            using StreamContent fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(fileContent, "file", fileName);

            string importUrl =
                $"groups/{workspaceId}/imports?datasetDisplayName={Uri.EscapeDataString(fileName)}&nameConflict=CreateOrOverwrite";

            using HttpResponseMessage uploadResponse =
                await httpClient.PostAsync(importUrl, form);

            string uploadBody = await uploadResponse.Content.ReadAsStringAsync();
            if (!uploadResponse.IsSuccessStatusCode)
            {
                throw PowerBiApiClient.CreateDetailedException(
                    $"Feltöltés sikertelen ({(int)uploadResponse.StatusCode})",
                    publishContext,
                    uploadBody,
                    (int)uploadResponse.StatusCode);
            }

            ImportResponse? import =
                JsonSerializer.Deserialize<ImportResponse>(uploadBody, PowerBiApiClient.JsonOptions)
                ?? throw PowerBiApiClient.CreateDetailedException(
                    "A Power BI válasz nem értelmezhető.",
                    publishContext,
                    uploadBody);

            string lastImportStatusBody = uploadBody;
            while (!PowerBiApiClient.IsTerminalImportState(import.ImportState))
            {
                if (import.Id == Guid.Empty)
                {
                    throw PowerBiApiClient.CreateDetailedException(
                        "A Power BI válasz nem tartalmaz import azonosítót.",
                        publishContext,
                        lastImportStatusBody);
                }

                string stateLabel = string.IsNullOrWhiteSpace(import.ImportState)
                    ? "Pending"
                    : import.ImportState;
                progress?.Report($"Publikálás folyamatban ({stateLabel})...");
                await Task.Delay(TimeSpan.FromSeconds(2));

                using HttpResponseMessage statusResponse =
                    await httpClient.GetAsync($"groups/{workspaceId}/imports/{import.Id}");

                string statusBody = await statusResponse.Content.ReadAsStringAsync();
                lastImportStatusBody = statusBody;
                if (!statusResponse.IsSuccessStatusCode)
                {
                    throw PowerBiApiClient.CreateDetailedException(
                        $"Státusz lekérdezése sikertelen ({(int)statusResponse.StatusCode})",
                        publishContext,
                        statusBody,
                        (int)statusResponse.StatusCode);
                }

                import = JsonSerializer.Deserialize<ImportResponse>(statusBody, PowerBiApiClient.JsonOptions)
                    ?? throw PowerBiApiClient.CreateDetailedException(
                        "A Power BI státusz válasz nem értelmezhető.",
                        publishContext,
                        statusBody);
            }

            if (!string.Equals(import.ImportState, "Succeeded", StringComparison.OrdinalIgnoreCase))
            {
                Dictionary<string, string> failureContext = new(publishContext)
                {
                    ["Import ID"] = import.Id.ToString(),
                    ["Import state"] = import.ImportState ?? ""
                };

                if (!string.IsNullOrWhiteSpace(import.Error?.Code))
                    failureContext["Power BI error code"] = import.Error.Code;

                string importErrorDetails = PowerBiApiClient.FormatImportErrorDetails(import.Error);
                if (!string.IsNullOrWhiteSpace(importErrorDetails))
                    failureContext["Power BI error details"] = importErrorDetails;

                throw PowerBiApiClient.CreateDetailedException(
                    $"Power BI publikálás sikertelen: {PowerBiApiClient.FormatImportFailureSummary(import)}",
                    failureContext,
                    lastImportStatusBody);
            }

            progress?.Report("Publikálás kész.");
        }
    }
}
