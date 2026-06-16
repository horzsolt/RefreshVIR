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
            report ??= ErrorReport.FromSummary(summary);
            using ErrorMessageForm dialog = new(title, summary, report);
            dialog.ShowDialog(owner);
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
            ErrorReport report = ErrorReport.FromException(exception, configure);
            ShowError(owner, title, summary, report);
        }

        public static void ShowError(
            IWin32Window? owner,
            string title,
            string summary,
            Action<ErrorReport.ErrorReportBuilder> configure)
        {
            summary = ErrorReport.NormalizeDisplaySummary(summary);
            ErrorReport report = ErrorReport.FromException(new Exception(summary), configure);
            ShowError(owner, title, summary, report);
        }
    }

    internal sealed class ErrorMessageForm : Form
    {
        public ErrorMessageForm(string title, string summary, ErrorReport report)
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

            Label messageLabel = new Label
            {
                Text = summary,
                AutoSize = true,
                MaximumSize = new Size(480, 260),
                UseMnemonic = false,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Margin = new Padding(8, 4, 0, 12)
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

            Button detailsButton = new Button
            {
                Text = "Részletek",
                AutoSize = true,
                MinimumSize = new Size(88, 28),
                Margin = new Padding(8, 0, 0, 0)
            };
            detailsButton.Click += (_, _) =>
            {
                using ErrorDetailsForm detailsForm = new(title, report);
                detailsForm.ShowDialog(this);
            };

            buttonPanel.Controls.Add(okButton);
            buttonPanel.Controls.Add(detailsButton);

            layout.Controls.Add(iconBox, 0, 0);
            layout.Controls.Add(messageLabel, 1, 0);
            layout.SetColumnSpan(buttonPanel, 2);
            layout.Controls.Add(buttonPanel, 0, 1);

            AcceptButton = okButton;
            CancelButton = okButton;

            Controls.Add(layout);
        }
    }

    internal sealed class ErrorDetailsForm : Form
    {
        public ErrorDetailsForm(string title, ErrorReport report)
        {
            string detailedText = report.ToDetailedText();

            Text = $"{title} – details";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(520, 320);

            TextBox detailsTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 12F),
                Text = detailedText
            };

            Panel buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                Padding = new Padding(8)
            };

            Button copyButton = new Button
            {
                Text = "Részletek másolása",
                AutoSize = true,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            copyButton.Click += (_, _) =>
            {
                Clipboard.SetText(detailedText);
                string previousText = copyButton.Text;
                copyButton.Text = "Másolva!";
                copyButton.Enabled = false;
                System.Windows.Forms.Timer timer = new() { Interval = 1500 };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    copyButton.Text = previousText;
                    copyButton.Enabled = true;
                };
                timer.Start();
            };

            Button closeButton = new Button
            {
                Text = "Bezárás",
                DialogResult = DialogResult.OK,
                AutoSize = true,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };

            buttonPanel.Resize += (_, _) =>
            {
                closeButton.Location = new Point(buttonPanel.ClientSize.Width - closeButton.Width - 8, 8);
                copyButton.Location = new Point(closeButton.Left - copyButton.Width - 8, 8);
            };
            buttonPanel.Controls.Add(copyButton);
            buttonPanel.Controls.Add(closeButton);

            AcceptButton = closeButton;
            CancelButton = closeButton;

            Controls.Add(detailsTextBox);
            Controls.Add(buttonPanel);

            LayoutButtons();
            buttonPanel.Resize += (_, _) => LayoutButtons();

            void LayoutButtons()
            {
                closeButton.Location = new Point(buttonPanel.ClientSize.Width - closeButton.Width - 8, 8);
                copyButton.Location = new Point(closeButton.Left - copyButton.Width - 8, 8);
            }
        }
    }
}
