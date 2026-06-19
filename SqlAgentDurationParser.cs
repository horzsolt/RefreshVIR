namespace RefreshVIR
{
    internal static class SqlAgentDurationParser
    {
        internal static double ToTotalSeconds(int runDuration)
        {
            (int hours, int minutes, int seconds) = Split(runDuration);
            return hours * 3600 + minutes * 60 + seconds;
        }

        internal static DateTime AddToDateTime(DateTime start, int runDuration)
        {
            (int hours, int minutes, int seconds) = Split(runDuration);
            return start.AddHours(hours).AddMinutes(minutes).AddSeconds(seconds);
        }

        internal static (int Hours, int Minutes, int Seconds) Split(int runDuration) =>
            (runDuration / 10000, (runDuration / 100) % 100, runDuration % 100);
    }
}
