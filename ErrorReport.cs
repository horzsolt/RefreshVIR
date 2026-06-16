using Microsoft.Data.SqlClient;
using System.Text;

namespace RefreshVIR
{
    internal sealed class ErrorReport
    {
        private const string UnknownDisplaySummary = "Ismeretlen hiba";
        private const string UnknownDetailSummary = "Unknown error";

        private readonly List<KeyValuePair<string, string>> contextEntries = new();
        private Exception? exception;
        private string? summary;

        public static string NormalizeDisplaySummary(string? value) =>
            string.IsNullOrWhiteSpace(value) ? UnknownDisplaySummary : value;

        public static ErrorReport FromSummary(string summaryText)
        {
            return new ErrorReport
            {
                summary = summaryText
            };
        }

        public static ErrorReport FromException(
            Exception ex,
            Action<ErrorReportBuilder>? configure = null)
        {
            ErrorReportBuilder builder = new();
            configure?.Invoke(builder);
            return builder.Build(ex);
        }

        public string ToDetailedText()
        {
            StringBuilder text = new();
            text.AppendLine("RefreshVIR error details");
            text.AppendLine(new string('=', 48));
            text.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            text.AppendLine($"User: {Environment.UserDomainName}\\{Environment.UserName}");
            text.AppendLine($"Machine: {Environment.MachineName}");
            text.AppendLine($"OS: {Environment.OSVersion}");
            text.AppendLine();

            string detailSummary = NormalizeDetailSummary(summary);
            text.AppendLine("Summary");
            text.AppendLine(new string('-', 20));
            text.AppendLine(detailSummary);
            text.AppendLine();

            List<KeyValuePair<string, string>> allContext = new(contextEntries);
            AppendExceptionContext(allContext, exception);

            if (allContext.Count > 0)
            {
                text.AppendLine("Context");
                text.AppendLine(new string('-', 20));
                foreach (KeyValuePair<string, string> entry in allContext)
                    text.AppendLine($"{entry.Key}: {entry.Value}");
                text.AppendLine();
            }

            if (exception != null)
            {
                text.AppendLine("Exception");
                text.AppendLine(new string('-', 20));
                AppendExceptionDetails(text, exception, 0);
            }

            return text.ToString().TrimEnd();
        }

        private static string NormalizeDetailSummary(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;

            return UnknownDetailSummary;
        }

        private static void AppendExceptionContext(
            List<KeyValuePair<string, string>> entries,
            Exception? ex)
        {
            if (ex == null)
                return;

            if (ex is DetailedApplicationException detailed)
            {
                foreach (KeyValuePair<string, string> item in detailed.Context)
                    AddContextEntry(entries, item.Key, item.Value);

                if (detailed.HttpStatusCode.HasValue)
                    AddContextEntry(entries, "HTTP status code", detailed.HttpStatusCode.Value.ToString());

                if (!string.IsNullOrWhiteSpace(detailed.ResponseBody))
                    AddContextEntry(entries, "API response", detailed.ResponseBody);
            }

            if (ex.InnerException != null)
                AppendExceptionContext(entries, ex.InnerException);
        }

        private static void AddContextEntry(
            List<KeyValuePair<string, string>> entries,
            string key,
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (entries.Any(entry =>
                    string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase)))
                return;

            entries.Add(new KeyValuePair<string, string>(key, value));
        }

        private static void AppendExceptionDetails(StringBuilder text, Exception ex, int depth)
        {
            string prefix = depth == 0 ? string.Empty : $"  [{depth}] ";
            text.AppendLine($"{prefix}Type: {ex.GetType().FullName}");
            text.AppendLine($"{prefix}Message: {NormalizeDetailSummary(ex.Message)}");

            if (ex is SqlException sqlEx)
            {
                text.AppendLine($"{prefix}SQL error number: {sqlEx.Number}");
                text.AppendLine($"{prefix}SQL state: {sqlEx.State}");
                text.AppendLine($"{prefix}SQL server: {sqlEx.Server}");
            }

            if (ex is DetailedApplicationException detailed)
            {
                if (detailed.HttpStatusCode.HasValue)
                    text.AppendLine($"{prefix}HTTP status code: {detailed.HttpStatusCode.Value}");

                if (!string.IsNullOrWhiteSpace(detailed.ResponseBody))
                {
                    text.AppendLine($"{prefix}API response:");
                    text.AppendLine(IndentBlock(detailed.ResponseBody, $"{prefix}  "));
                }
            }

            if (!string.IsNullOrWhiteSpace(ex.StackTrace))
            {
                text.AppendLine($"{prefix}Stack trace:");
                text.AppendLine(IndentBlock(ex.StackTrace, $"{prefix}  "));
            }

            if (ex.InnerException != null)
            {
                text.AppendLine();
                text.AppendLine($"{prefix}Inner exception:");
                AppendExceptionDetails(text, ex.InnerException, depth + 1);
            }
        }

        private static string IndentBlock(string value, string indent) =>
            string.Join(
                Environment.NewLine,
                value.Split(
                    new[] { "\r\n", "\n" },
                    StringSplitOptions.None)
                .Select(line => indent + line));

        internal sealed class ErrorReportBuilder
        {
            private readonly ErrorReport report = new();

            public ErrorReportBuilder Summary(string summaryText)
            {
                report.summary = summaryText;
                return this;
            }

            public ErrorReportBuilder Add(string key, string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return this;

                report.contextEntries.Add(new KeyValuePair<string, string>(key, value));
                return this;
            }

            public ErrorReport Build(Exception ex)
            {
                report.exception = ex;
                if (string.IsNullOrWhiteSpace(report.summary) && !string.IsNullOrWhiteSpace(ex.Message))
                    report.summary = ex.Message;
                return report;
            }
        }
    }
}
