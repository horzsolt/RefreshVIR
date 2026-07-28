namespace RefreshVIR
{
    internal static class ErrorDialog
    {
        public static void ShowError(
            IWin32Window? owner,
            string title,
            string summary,
            ErrorReport? report = null)
        {
            summary = ErrorReport.NormalizeDisplaySummary(summary);
            using ErrorMessageForm dialog = new(title, summary);
            dialog.ShowDialog(owner);
            ReturnToMainScreen(owner);
        }

        public static void ShowError(
            IWin32Window? owner,
            string title,
            string summary,
            Exception exception,
            Action<ErrorReport.ErrorReportBuilder>? configure = null)
        {
            summary = ErrorReport.NormalizeDisplaySummary(
                string.IsNullOrWhiteSpace(summary) ? exception.Message : summary);
            ShowError(owner, title, summary, report: null);
        }

        public static void ShowErrorOnce(
            IWin32Window? owner,
            string title,
            string summary,
            Exception exception,
            Action<ErrorReport.ErrorReportBuilder>? configure = null)
        {
            if (TryRegisterOccurrence(title, summary, exception, out int occurrenceCount))
            {
                ShowError(
                    owner,
                    title,
                    AppendOccurrenceCount(summary, occurrenceCount),
                    exception,
                    configure);
            }
        }

        public static void ShowErrorOnce(
            IWin32Window? owner,
            string title,
            string summary,
            Action<ErrorReport.ErrorReportBuilder>? configure)
        {
            if (TryRegisterOccurrence(title, summary, null, out int occurrenceCount))
            {
                ShowError(
                    owner,
                    title,
                    AppendOccurrenceCount(summary, occurrenceCount));
            }
        }

        public static void ShowErrorOnce(
            IWin32Window? owner,
            string title,
            string summary,
            ErrorReport? report = null)
        {
            if (TryRegisterOccurrence(title, summary, null, out int occurrenceCount))
            {
                ShowError(
                    owner,
                    title,
                    AppendOccurrenceCount(summary, occurrenceCount),
                    report);
            }
        }

        public static void ShowError(
            IWin32Window? owner,
            string title,
            string summary,
            Action<ErrorReport.ErrorReportBuilder> configure)
        {
            summary = ErrorReport.NormalizeDisplaySummary(summary);
            ShowError(owner, title, summary, report: null);
        }

        private static void ReturnToMainScreen(IWin32Window? owner)
        {
            if (owner is not Form form || form.IsDisposed || form is MainForm)
                return;

            form.Close();
        }

        private static bool TryRegisterOccurrence(
            string title,
            string summary,
            Exception? exception,
            out int occurrenceCount)
        {
            return ErrorOccurrenceTracker.Register(
                BuildErrorKey(title, summary, exception),
                out occurrenceCount);
        }

        private static string AppendOccurrenceCount(string summary, int occurrenceCount)
        {
            summary = ErrorReport.NormalizeDisplaySummary(summary);

            if (occurrenceCount <= 1)
                return summary;

            return
                $"{summary}{Environment.NewLine}{Environment.NewLine}Előfordulások száma: {occurrenceCount}";
        }

        private static string BuildErrorKey(string title, string summary, Exception? exception)
        {
            string exceptionType = exception?.GetType().FullName ?? string.Empty;
            string exceptionMessage = exception?.Message ?? string.Empty;
            return $"{title}\u001f{exceptionType}\u001f{exceptionMessage}\u001f{summary}";
        }
    }

    internal sealed class ErrorMessageForm : Form
    {
        public ErrorMessageForm(string title, string summary)
        {
            summary = ErrorReport.NormalizeDisplaySummary(summary);

            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);
            MinimumSize = new Size(420, 140);
            MaximumSize = new Size(640, 400);

            TableLayoutPanel layout = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 2,
                Dock = DockStyle.Fill
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            PictureBox iconBox = new PictureBox
            {
                Image = SystemIcons.Error.ToBitmap(),
                SizeMode = PictureBoxSizeMode.CenterImage,
                Dock = DockStyle.Fill,
                MinimumSize = new Size(32, 32)
            };

            TextBox messageTextBox = new TextBox
            {
                Text = summary,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = SystemColors.Window,
                ScrollBars = ScrollBars.Vertical,
                MinimumSize = new Size(320, 72),
                MaximumSize = new Size(480, 260),
                WordWrap = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Margin = new Padding(8, 4, 0, 12),
                TabStop = true
            };

            FlowLayoutPanel buttonPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 0)
            };

            Button okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                AutoSize = true,
                MinimumSize = new Size(88, 28),
                Margin = new Padding(8, 0, 0, 0)
            };

            buttonPanel.Controls.Add(okButton);

            layout.Controls.Add(iconBox, 0, 0);
            layout.Controls.Add(messageTextBox, 1, 0);
            layout.SetColumnSpan(buttonPanel, 2);
            layout.Controls.Add(buttonPanel, 0, 1);

            AcceptButton = okButton;
            CancelButton = okButton;

            Controls.Add(layout);

            Shown += (_, _) =>
            {
                messageTextBox.SelectAll();
                messageTextBox.Focus();
            };
        }
    }
}
