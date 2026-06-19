namespace RefreshVIR
{
    internal static class SqlAgentStatusMapper
    {
        internal static string ToDisplayName(int status) => status switch
        {
            0 => "Failed",
            1 => "Succeeded",
            2 => "Retry",
            3 => "Canceled",
            4 => "In Progress",
            _ => "Unknown"
        };
    }
}
