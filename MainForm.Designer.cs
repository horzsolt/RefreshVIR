namespace RefreshVIR
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Button btnRefreshGL;
        private System.Windows.Forms.Button btnExit;
        private System.Windows.Forms.ProgressBar progressBar1;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            btnRefreshGL = new Button();
            btnExit = new Button();
            progressBar1 = new ProgressBar();
            button1 = new Button();
            SuspendLayout();
            // 
            // btnRefreshGL
            // 
            btnRefreshGL.Location = new Point(30, 38);
            btnRefreshGL.Margin = new Padding(3, 4, 3, 4);
            btnRefreshGL.Name = "btnRefreshGL";
            btnRefreshGL.Size = new Size(120, 50);
            btnRefreshGL.TabIndex = 0;
            btnRefreshGL.Text = "Főkönyv";
            btnRefreshGL.UseVisualStyleBackColor = true;
            btnRefreshGL.Click += btnRefreshGL_Click;
            // 
            // btnExit
            // 
            btnExit.Location = new Point(313, 38);
            btnExit.Margin = new Padding(3, 4, 3, 4);
            btnExit.Name = "btnExit";
            btnExit.Size = new Size(120, 50);
            btnExit.TabIndex = 1;
            btnExit.Text = "Exit";
            btnExit.UseVisualStyleBackColor = true;
            btnExit.Click += btnExit_Click;
            // 
            // progressBar1
            // 
            progressBar1.Location = new Point(30, 112);
            progressBar1.Margin = new Padding(3, 4, 3, 4);
            progressBar1.MarqueeAnimationSpeed = 30;
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new Size(260, 25);
            progressBar1.Style = ProgressBarStyle.Marquee;
            progressBar1.TabIndex = 2;
            progressBar1.Visible = false;
            // 
            // button1
            // 
            button1.Location = new Point(172, 38);
            button1.Name = "button1";
            button1.Size = new Size(118, 50);
            button1.TabIndex = 3;
            button1.Text = "Státusz";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(611, 337);
            Controls.Add(button1);
            Controls.Add(progressBar1);
            Controls.Add(btnExit);
            Controls.Add(btnRefreshGL);
            Margin = new Padding(3, 4, 3, 4);
            Name = "MainForm";
            Text = "Controlling vezérlőpult";
            ResumeLayout(false);
        }

        #endregion

        private Button button1;
    }
}
