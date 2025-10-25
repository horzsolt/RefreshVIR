using System.Data;
using System.Windows.Forms;

namespace RefreshVIR
{
    public partial class GLRefreshForm : Form
    {
        private RadioButton fullRadio;
        private RadioButton incrementalRadio;
        private Button runJobButton;
        private DataGridView grid;
        private Button closeButton;
        private string connectionString;

        public GLRefreshForm(string connectionString)
        {
            InitializeComponent();

            this.connectionString = connectionString;

            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Text = "Főkönyvi adatok frissítése QAD-ból";
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                    this.Close();
            };

            Panel topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40
            };

            fullRadio = new RadioButton
            {
                Text = "Teljes",
                Left = 10,
                Top = 10
            };

            fullRadio.Click += RadioButton_CheckedChanged;

            incrementalRadio = new RadioButton
            {
                Text = "Növekményes",
                Checked = true,
                Left = 150,
                Top = 10,
                Width = 150
            };
            incrementalRadio.Click += RadioButton_CheckedChanged;

            runJobButton = new Button
            {
                Text = "Frissítés indítása",
                Left = 350,
                Top = 7,
                Height = 25,
                Width = 300
            };

            runJobButton.Click += RunJobButton_Click;

            topPanel.Controls.Add(fullRadio);
            topPanel.Controls.Add(incrementalRadio);
            topPanel.Controls.Add(runJobButton);

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            closeButton = new Button
            {
                Text = "<< Vissza",
                Dock = DockStyle.Bottom,
                Height = 40
            };
            closeButton.Click += (s, e) => this.Close();

            this.Controls.Add(grid);
            this.Controls.Add(closeButton);
            this.Controls.Add(topPanel);

            this.Load += StoredProcStatusForm_Load;
        }

        private async void RunJobButton_Click(object sender, EventArgs e)
        {
            string jobName = fullRadio.Checked ? "QAD_GL_frissites" : "QAD_GL_INC_frissites";
            try
            {
                this.UseWaitCursor = true;
                if (SQLUtils.IsJobRunning(jobName, connectionString)) {
                    this.UseWaitCursor = false;
                    MessageBox.Show($"A választott frissítés '{jobName}' jelenleg már fut.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                await SQLUtils.ExecuteJobAsync(jobName, connectionString);
                this.UseWaitCursor = false;
                MessageBox.Show($"Frissítés '{jobName}' elindítva. Bezárhatja az ablakot a frissítés a háttérben fut.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                this.UseWaitCursor = false;
                MessageBox.Show("A frissítés elindítása sikertelen:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.UseWaitCursor = false;
            }
        }

        private void StoredProcStatusForm_Load(object sender, EventArgs e)
        {
            LoadGrid();
        }

        private void RadioButton_CheckedChanged(object sender, EventArgs e)
        {
            LoadGrid();
        }

        private void StoredProcStatusForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
                e.Handled = true;
            }
        }

        private void LoadGrid()
        {
            string spName = fullRadio.Checked ? "QAD_GL_frissites" : "QAD_GL_INC_frissites";

            try
            {
                this.UseWaitCursor = true;
                DataTable dt = SQLUtils.GetStoredProcExecutionDetails(spName, connectionString, 14);
                grid.DataSource = dt;

                grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                grid.MultiSelect = false;
                grid.DefaultCellStyle.SelectionBackColor = Color.LightBlue;
                grid.DefaultCellStyle.SelectionForeColor = Color.Black;

                // Header formatting
                grid.EnableHeadersVisualStyles = false;
                grid.ColumnHeadersDefaultCellStyle.BackColor = Color.LightGray;
                grid.ColumnHeadersDefaultCellStyle.Font = new Font(grid.Font, FontStyle.Bold);
                grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

                // Center-align duration/runtime columns by index
                if (grid.Columns.Count > 4)
                {
                    grid.Columns[2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter; // Last Execution Duration
                    grid.Columns[3].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter; // Average Runtime
                }
            }
            finally
            {
                this.UseWaitCursor = false;
            }
        }
    }
}
