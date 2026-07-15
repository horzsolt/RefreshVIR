using System.Text;

namespace RefreshVIR.Tests;

public class JobExecutionToolTipFormatterTests
{
    [Fact]
    public void FormatErrorMessage_WrapsLongSingleLineText()
    {
        string longMessage = GenerateLongMessage(
            "A SQL Agent lépés sikertelen volt, mert az adatbázis kapcsolat megszakadt a távoli szerveren.",
            12);

        string formatted = JobExecutionToolTipFormatter.FormatErrorMessage(longMessage);

        Assert.Contains(Environment.NewLine, formatted);
        Assert.All(
            formatted.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries),
            line => Assert.True(line.Length <= JobExecutionToolTipFormatter.ErrorMessageWrapWidth + 3));
    }

    [Fact]
    public void FormatErrorMessage_PreservesExistingLineBreaks()
    {
        string message =
            "Executed as user: DOMAIN\\service_account.\r\n" +
            "The job step failed because the OLE DB provider reported a catastrophic failure while opening rowset from table dbo.VeryLongTableName_WithManyColumns.\r\n" +
            "Additional information: Timeout expired. The timeout period elapsed prior to completion of the operation.";

        string formatted = JobExecutionToolTipFormatter.FormatErrorMessage(message);

        Assert.Contains("Executed as user:", formatted);
        Assert.Contains("The job step failed", formatted);
        Assert.Contains(Environment.NewLine, formatted);
        Assert.True(formatted.Split(Environment.NewLine).Length >= 3);
    }

    [Fact]
    public void FormatErrorMessage_BreaksVeryLongTokenWithoutSpaces()
    {
        string message =
            "Connection failed for endpoint https://very-long-subdomain.example.internal.corp.company.local/api/v2/import/batch/process?tenant=controlling&mode=full&retry=false&trace=0123456789abcdef";

        string formatted = JobExecutionToolTipFormatter.FormatErrorMessage(message);

        Assert.Contains(Environment.NewLine, formatted);
        Assert.True(formatted.Split(Environment.NewLine).Length >= 2);
    }

    [Fact]
    public void FormatErrorMessage_TruncatesExtremelyLongMessages()
    {
        string longMessage = GenerateLongMessage("Ismétlődő hiba részlet.", 200);

        string formatted = JobExecutionToolTipFormatter.FormatErrorMessage(longMessage);

        Assert.Contains("(... hibaüzenet csonkolva)", formatted);
        Assert.True(formatted.Split(Environment.NewLine).Length <= JobExecutionToolTipFormatter.MaxErrorMessageLines + 1);
    }

    [Fact]
    public void BuildExecutionToolTip_FormatsFailedJobWithReadableErrorBlock()
    {
        JobExecution execution = new JobExecution
        {
            JobName = "QAD import - controlling",
            StartTime = new DateTime(2026, 7, 14, 8, 15, 0),
            FinishTime = new DateTime(2026, 7, 14, 8, 47, 12),
            RunStatus = 0,
            ErrorMessage = GenerateLongMessage(
                "Hiba a forrás tábla betöltése közben: violation of PRIMARY KEY constraint 'PK_t_import_batch'.",
                8)
        };

        string tooltip = JobExecutionToolTipFormatter.BuildExecutionToolTip(execution);

        Assert.Contains("Státusz: Failed", tooltip);
        Assert.Contains("Hiba:", tooltip);
        Assert.Contains("QAD import - controlling", tooltip);
        Assert.DoesNotContain("Hiba: Hiba a forrás", tooltip);
        Assert.True(tooltip.IndexOf("Hiba:", StringComparison.Ordinal) < tooltip.IndexOf("forrás", StringComparison.Ordinal));
    }

    private static string GenerateLongMessage(string sentence, int repeatCount)
    {
        StringBuilder builder = new StringBuilder(sentence.Length * repeatCount);

        for (int i = 0; i < repeatCount; i++)
        {
            if (i > 0)
                builder.Append(' ');

            builder.Append(sentence);
            builder.Append(" (#");
            builder.Append(i + 1);
            builder.Append(')');
        }

        return builder.ToString();
    }
}
