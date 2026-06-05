namespace RefreshVIR
{
    internal static class JobAuthorization
    {
        private static readonly HashSet<string> AllowedUsers =
            new(StringComparer.OrdinalIgnoreCase) { "GW0251", "GW0300" };

        public static bool IsAllowedToStartJobs() =>
            AllowedUsers.Contains(Environment.UserName);

        public static bool ConfirmAllowedToStartJobs(IWin32Window? owner = null)
        {
            if (IsAllowedToStartJobs())
                return true;

            MessageBox.Show(
                owner,
                "Önnek nincs joga frissítő job-ot indítani",
                "Figyelmeztetés",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return false;
        }
    }
}
