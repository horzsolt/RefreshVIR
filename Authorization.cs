namespace RefreshVIR
{
    internal static class Authorization
    {
        private static readonly HashSet<string> AllowedUsers =
            new(StringComparer.OrdinalIgnoreCase) { "GW0251", "GW0300" };

        public static bool IsAllowedToStartJobs() =>
            AllowedUsers.Contains(Environment.UserName);

        public static bool ConfirmAllowedToStartJobs(IWin32Window? owner = null) =>
            ConfirmAllowed(owner, "Önnek nincs joga frissítő job-ot indítani");

        public static bool ConfirmAllowedToPublishPowerBiReports(IWin32Window? owner = null) =>
            ConfirmAllowed(owner, "Önnek nincs joga Power BI riportot publikálni");

        private static bool ConfirmAllowed(IWin32Window? owner, string deniedMessage)
        {
            if (IsAllowedToStartJobs())
                return true;

            MessageBox.Show(
                owner,
                deniedMessage,
                "Figyelmeztetés",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return false;
        }
    }
}
