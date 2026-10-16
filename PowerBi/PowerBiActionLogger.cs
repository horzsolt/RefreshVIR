namespace RefreshVIR
{
    internal static class PowerBiActionLogger
    {
        internal static void Log(
            string action,
            PowerBiWorkspace? workspace = null,
            string? reportName = null,
            string? pbixPath = null,
            string? detail = null)
        {
            List<string> parts = new() { action };

            if (!string.IsNullOrWhiteSpace(reportName))
                parts.Add($"riport={reportName}");

            if (!string.IsNullOrWhiteSpace(pbixPath))
                parts.Add($"pbix={pbixPath}");

            if (workspace != null)
                parts.Add($"munkaterület={workspace.Name}");

            if (!string.IsNullOrWhiteSpace(detail))
                parts.Add(detail);

            SQLUtils.LogAction(string.Join(" | ", parts));
        }

        internal static void LogFailure(
            string action,
            Exception exception,
            PowerBiWorkspace? workspace = null,
            string? reportName = null,
            string? pbixPath = null)
        {
            Log(action, workspace, reportName, pbixPath, FormatExceptionDetail(exception));
        }

        internal static string FormatExceptionDetail(Exception exception)
        {
            List<string> parts = new() { $"hiba={exception.Message}" };

            if (exception is DetailedApplicationException detailed)
            {
                if (detailed.HttpStatusCode.HasValue)
                    parts.Add($"http={detailed.HttpStatusCode.Value}");

                string? apiErrorCode = PowerBiApiClient.TryExtractErrorCode(detailed.ResponseBody);
                if (!string.IsNullOrWhiteSpace(apiErrorCode))
                    parts.Add($"apiError={apiErrorCode}");

                foreach (KeyValuePair<string, string> entry in detailed.Context)
                {
                    if (string.IsNullOrWhiteSpace(entry.Value))
                        continue;

                    parts.Add($"{SanitizeKey(entry.Key)}={entry.Value}");
                }

                if (!string.IsNullOrWhiteSpace(detailed.ResponseBody))
                    parts.Add($"apiResponse={CollapseWhitespace(detailed.ResponseBody)}");
            }

            if (exception.InnerException != null
                && !string.IsNullOrWhiteSpace(exception.InnerException.Message))
            {
                parts.Add($"inner={CollapseWhitespace(exception.InnerException.Message)}");
            }

            return string.Join(" | ", parts);
        }

        private static string SanitizeKey(string key) =>
            key.Replace(' ', '_').Replace('=', '_');

        private static string CollapseWhitespace(string value) =>
            string.Join(
                ' ',
                value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
