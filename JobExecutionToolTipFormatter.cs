using System.Text;

namespace RefreshVIR
{
    internal static class JobExecutionToolTipFormatter
    {
        internal const int ErrorMessageWrapWidth = 88;
        internal const int MaxErrorMessageLines = 24;

        internal static string BuildExecutionToolTip(JobExecution execution)
        {
            double elapsedSeconds = Math.Max(0, (execution.FinishTime - execution.StartTime).TotalSeconds);
            string elapsed = SQLUtils.FormatHungarianDuration(elapsedSeconds);
            string status = SqlAgentStatusMapper.ToDisplayName(execution.RunStatus);

            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Job: {execution.JobName}");
            builder.AppendLine($"Futásidő: {elapsed}");
            builder.AppendLine($"Kezdés: {execution.StartTime:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"Befejezés: {execution.FinishTime:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"Státusz: {status}");

            if (execution.RunStatus == 0 && !string.IsNullOrWhiteSpace(execution.ErrorMessage))
            {
                builder.AppendLine("Hiba:");
                builder.Append(FormatErrorMessage(execution.ErrorMessage));
            }

            return builder.ToString().TrimEnd();
        }

        internal static string BuildCellToolTip(IEnumerable<JobExecution> executions)
        {
            StringBuilder builder = new StringBuilder();
            bool first = true;

            foreach (JobExecution execution in executions.OrderByDescending(e => e.StartTime))
            {
                if (!first)
                    builder.AppendLine().AppendLine("---");

                builder.Append(BuildExecutionToolTip(execution));
                first = false;
            }

            return builder.ToString();
        }

        internal static string FormatErrorMessage(string? errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
                return string.Empty;

            string normalized = NormalizeErrorMessage(errorMessage);
            List<string> wrappedLines = WrapParagraphs(
                normalized,
                ErrorMessageWrapWidth,
                MaxErrorMessageLines);

            return string.Join(Environment.NewLine, wrappedLines);
        }

        private static string NormalizeErrorMessage(string errorMessage)
        {
            string[] lines = errorMessage
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n');

            StringBuilder builder = new StringBuilder();

            foreach (string rawLine in lines)
            {
                string line = CollapseWhitespace(rawLine.Trim());
                if (line.Length == 0)
                {
                    if (builder.Length > 0 && !builder.ToString().EndsWith("\n\n", StringComparison.Ordinal))
                        builder.AppendLine();
                    continue;
                }

                builder.AppendLine(line);
            }

            return builder.ToString().TrimEnd();
        }

        private static string CollapseWhitespace(string value)
        {
            if (value.Length == 0)
                return value;

            StringBuilder builder = new StringBuilder(value.Length);
            bool previousWasSpace = false;

            foreach (char character in value)
            {
                if (char.IsWhiteSpace(character))
                {
                    if (!previousWasSpace)
                        builder.Append(' ');
                    previousWasSpace = true;
                    continue;
                }

                builder.Append(character);
                previousWasSpace = false;
            }

            return builder.ToString();
        }

        private static List<string> WrapParagraphs(
            string text,
            int wrapWidth,
            int maxLines)
        {
            List<string> result = new List<string>();
            bool truncated = false;

            foreach (string paragraph in text.Split('\n', StringSplitOptions.None))
            {
                if (result.Count >= maxLines)
                {
                    truncated = true;
                    break;
                }

                if (paragraph.Length == 0)
                {
                    if (result.Count > 0 && result[^1].Length > 0)
                        result.Add(string.Empty);
                    continue;
                }

                foreach (string wrappedLine in WrapSingleParagraph(paragraph, wrapWidth))
                {
                    if (result.Count >= maxLines)
                    {
                        truncated = true;
                        break;
                    }

                    result.Add(wrappedLine);
                }
            }

            if (truncated)
            {
                if (result.Count == maxLines && result[^1].Length > 0)
                    result[^1] = TrimLineToWidth(result[^1], wrapWidth - 3) + "...";

                result.Add("(... hibaüzenet csonkolva)");
            }

            return result;
        }

        private static IEnumerable<string> WrapSingleParagraph(string paragraph, int wrapWidth)
        {
            if (paragraph.Length <= wrapWidth)
            {
                yield return paragraph;
                yield break;
            }

            int index = 0;
            while (index < paragraph.Length)
            {
                int remaining = paragraph.Length - index;
                if (remaining <= wrapWidth)
                {
                    yield return paragraph[index..].Trim();
                    yield break;
                }

                int sliceLength = wrapWidth;
                int breakAt = FindBreakIndex(paragraph, index, sliceLength);

                if (breakAt <= index)
                    breakAt = Math.Min(index + wrapWidth, paragraph.Length);

                yield return paragraph[index..breakAt].TrimEnd();
                index = breakAt;

                while (index < paragraph.Length && char.IsWhiteSpace(paragraph[index]))
                    index++;
            }
        }

        private static int FindBreakIndex(string text, int start, int maxLength)
        {
            int end = Math.Min(start + maxLength, text.Length);
            int searchStart = Math.Max(start + 1, end - 24);

            for (int i = end; i >= searchStart; i--)
            {
                if (char.IsWhiteSpace(text[i - 1]))
                    return i - 1;

                if (IsSoftBreakCharacter(text[i - 1]))
                    return i;
            }

            return end;
        }

        private static bool IsSoftBreakCharacter(char character) =>
            character is ';' or ',' or '.' or ':' or ')' or ']' or '}';

        private static string TrimLineToWidth(string line, int maxWidth)
        {
            if (line.Length <= maxWidth)
                return line;

            return line[..maxWidth].TrimEnd();
        }
    }
}
