namespace UIforMR
{
    partial class DebuggingForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.button1 = new System.Windows.Forms.Button();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.textBox = new System.Windows.Forms.RichTextBox();
            this.lComment = new System.Windows.Forms.Label();
            this.bNo = new System.Windows.Forms.Button();
            this.bYes = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(0, 0);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 0;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainer1.IsSplitterFixed = true;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.textBox);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.BackColor = System.Drawing.SystemColors.Window;
            this.splitContainer1.Panel2.Controls.Add(this.lComment);
            this.splitContainer1.Panel2.Controls.Add(this.bNo);
            this.splitContainer1.Panel2.Controls.Add(this.bYes);
            this.splitContainer1.Panel2MinSize = 60;
            this.splitContainer1.Size = new System.Drawing.Size(578, 338);
            this.splitContainer1.SplitterDistance = 277;
            this.splitContainer1.SplitterWidth = 1;
            this.splitContainer1.TabIndex = 0;
            // 
            // textBox
            // 
            this.textBox.BackColor = System.Drawing.SystemColors.Window;
            this.textBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBox.Font = new System.Drawing.Font("Consolas", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBox.Location = new System.Drawing.Point(0, 0);
            this.textBox.Name = "textBox";
            this.textBox.ReadOnly = true;
            this.textBox.Size = new System.Drawing.Size(578, 277);
            this.textBox.TabIndex = 0;
            this.textBox.Text = "";
            // 
            // lComment
            // 
            this.lComment.AutoSize = true;
            this.lComment.Font = new System.Drawing.Font("Consolas", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lComment.ForeColor = System.Drawing.Color.Black;
            this.lComment.Location = new System.Drawing.Point(12, 9);
            this.lComment.Name = "lComment";
            this.lComment.Size = new System.Drawing.Size(109, 23);
            this.lComment.TabIndex = 2;
            this.lComment.Text = "Comment :";
            this.lComment.Visible = false;
            // 
            // bNo
            // 
            this.bNo.FlatAppearance.BorderColor = System.Drawing.Color.OrangeRed;
            this.bNo.FlatAppearance.BorderSize = 2;
            this.bNo.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.bNo.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.bNo.ForeColor = System.Drawing.Color.OrangeRed;
            this.bNo.Location = new System.Drawing.Point(495, 26);
            this.bNo.Name = "bNo";
            this.bNo.Size = new System.Drawing.Size(80, 25);
            this.bNo.TabIndex = 2;
            this.bNo.Text = "No";
            this.bNo.UseVisualStyleBackColor = true;
            this.bNo.Click += new System.EventHandler(this.Button_Click);
            // 
            // bYes
            // 
            this.bYes.FlatAppearance.BorderColor = System.Drawing.Color.RoyalBlue;
            this.bYes.FlatAppearance.BorderSize = 2;
            this.bYes.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.bYes.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.bYes.ForeColor = System.Drawing.Color.RoyalBlue;
            this.bYes.Location = new System.Drawing.Point(329, 19);
            this.bYes.Name = "bYes";
            this.bYes.Size = new System.Drawing.Size(80, 25);
            this.bYes.TabIndex = 1;
            this.bYes.Text = "Yes";
            this.bYes.UseVisualStyleBackColor = true;
            this.bYes.Click += new System.EventHandler(this.Button_Click);
            // 
            // DebuggingForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.AutoScroll = true;
            this.BackColor = System.Drawing.SystemColors.ActiveBorder;
            this.ClientSize = new System.Drawing.Size(578, 338);
            this.Controls.Add(this.splitContainer1);
            this.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Name = "DebuggingForm";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Load += new System.EventHandler(this.DebuggingForm_Load);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Button bYes;
        private System.Windows.Forms.Label lComment;
        private System.Windows.Forms.Button bNo;
        private System.Windows.Forms.RichTextBox textBox;
    }
}