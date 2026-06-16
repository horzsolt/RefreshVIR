using System.Globalization;
using System.Text;

namespace RefreshVIR
{
    internal sealed class PowerBiWorkspace
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = "";
    }

    internal sealed class PowerBiReportInfo
    {
        public string ReportName { get; init; } = "";
        public string DatasetName { get; init; } = "";
        public string ReportType { get; init; } = "";
        public DateTime? LastRefreshLocal { get; init; }
        public DateTime? NextRefreshLocal { get; init; }
        public DateTime? LastUploadLocal { get; init; }
        public bool RefreshEnabled { get; init; } = true;
        public bool HasRefreshSchedule { get; init; }
        public bool HasEmbeddedReportData { get; init; }
        public bool RefreshDisabled =>
            HasRefreshSchedule && !RefreshEnabled && !HasEmbeddedReportData;

        public string DataSourceDisplay => HasEmbeddedReportData
            ? "Beágyazott adat"
            : DatasetName;

        public string LastRefreshDisplay => HasEmbeddedReportData
            ? "—"
            : FormatDateTime(LastRefreshLocal);
        public string NextRefreshDisplay => HasEmbeddedReportData
            ? "—"
            : FormatDateTime(NextRefreshLocal);
        public string LastUploadDisplay => FormatDateTime(LastUploadLocal);
        public string ScheduleDisplay => HasEmbeddedReportData
            ? "—"
            : HasRefreshSchedule
                ? (RefreshEnabled ? "Engedélyezve" : "Letiltva")
                : "—";

        private static string FormatDateTime(DateTime? value) =>
            value?.ToString("yyyy.MM.dd HH:mm", CultureInfo.CurrentCulture) ?? "—";
    }

    internal sealed class PowerBiExistingReportInfo
    {
        public string ReportName { get; init; } = "";
        public string ReportType { get; init; } = "";
        public string DataSourceDisplay { get; init; } = "";
        public DateTime? LastUploadLocal { get; init; }
        public DateTime? LastRefreshLocal { get; init; }
        public bool HasEmbeddedReportData { get; init; }

        public string BuildConfirmationMessage(string workspaceName)
        {
            StringBuilder text = new();
            text.AppendLine("Biztosan frissíteni szeretnéd a riportot?");
            text.AppendLine();
            text.AppendLine($"Riport: {ReportName}");
            text.AppendLine($"Munkaterület: {workspaceName}");
            text.AppendLine($"Típus: {ReportType}");
            text.AppendLine($"Adatforrás: {DataSourceDisplay}");
            text.AppendLine($"Utolsó feltöltés: {FormatDateTime(LastUploadLocal)}");
            if (!HasEmbeddedReportData)
                text.AppendLine($"Utolsó adatfrissítés: {FormatDateTime(LastRefreshLocal)}");
            return text.ToString().TrimEnd();
        }

        private static string FormatDateTime(DateTime? value) =>
            value?.ToString("yyyy.MM.dd HH:mm", CultureInfo.CurrentCulture) ?? "—";
    }
}
