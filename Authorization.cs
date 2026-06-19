namespace RefreshVIR
{
    internal static class Authorization
    {
        private static readonly HashSet<string> AllowedUsers =
            new(StringComparer.OrdinalIgnoreCase) { "GW0251", "GW0300" };

        private static bool IsCurrentUserAllowed() =>
            AllowedUsers.Contains(Environment.UserDomainName)
            || AllowedUsers.Contains(Environment.UserName);

        public static bool IsAllowedToStartJobs() =>
            IsCurrentUserAllowed();

        public static bool IsAllowedToPublishReports() =>
            IsCurrentUserAllowed();

        public static bool ConfirmAllowedToStartJobs(IWin32Window? owner = null) =>
            ConfirmAllowed(owner, "Önnek nincs joga frissítő job-ot indítani", IsAllowedToStartJobs);

        public static bool ConfirmAllowedToPublishPowerBiReports(IWin32Window? owner = null) =>
            ConfirmAllowed(owner, "Önnek nincs joga Power BI riportot publikálni", IsAllowedToPublishReports);

        private static bool ConfirmAllowed(
            IWin32Window? owner,
            string deniedMessage,
            Func<bool> isAllowed)
        {
            if (isAllowed())
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
