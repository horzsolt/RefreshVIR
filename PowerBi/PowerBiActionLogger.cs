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
    }
}
