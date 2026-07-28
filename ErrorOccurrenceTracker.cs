namespace RefreshVIR
{
    internal static class ErrorOccurrenceTracker
    {
        private static readonly object Sync = new();
        private static readonly Dictionary<string, int> ErrorCounts = new(StringComparer.Ordinal);
        private static readonly HashSet<string> ShownErrors = new(StringComparer.Ordinal);

        internal static bool Register(string key, out int occurrenceCount)
        {
            lock (Sync)
            {
                ErrorCounts.TryGetValue(key, out int count);
                count++;
                ErrorCounts[key] = count;
                occurrenceCount = count;

                if (ShownErrors.Contains(key))
                    return false;

                ShownErrors.Add(key);
                return true;
            }
        }

        internal static void Reset()
        {
            lock (Sync)
            {
                ErrorCounts.Clear();
                ShownErrors.Clear();
            }
        }
    }
}
