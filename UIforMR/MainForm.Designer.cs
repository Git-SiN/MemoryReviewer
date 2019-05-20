namespace UIforMR
{
    partial class MainForm
    {
        /// <summary>
        /// 필수 디자이너 변수입니다.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 사용 중인 모든 리소스를 정리합니다.
        /// </summary>
        /// <param name="disposing">관리되는 리소스를 삭제해야 하면 true이고, 그렇지 않으면 false입니다.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 디자이너에서 생성한 코드

        /// <summary>
        /// 디자이너 지원에 필요한 메서드입니다. 
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마세요.
        /// </summary>
        private void InitializeComponent()
        {
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bMenuDump = new System.Windows.Forms.ToolStripMenuItem();
            this.configureToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.driverToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bConnect = new System.Windows.Forms.ToolStripMenuItem();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.bKernel = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.bSelect = new System.Windows.Forms.Button();
            this.cProcess = new System.Windows.Forms.ComboBox();
            this.tProcess = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.lProcessInfo = new System.Windows.Forms.ListView();
            this.lProcessCHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.lProcessCHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.tThreads = new System.Windows.Forms.TabPage();
            this.menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tProcess.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Font = new System.Drawing.Font("Consolas", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.menuStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.toolsToolStripMenuItem,
            this.configureToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Padding = new System.Windows.Forms.Padding(3, 2, 0, 2);
            this.menuStrip1.Size = new System.Drawing.Size(1200, 32);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(66, 28);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // toolsToolStripMenuItem
            // 
            this.toolsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.bMenuDump});
            this.toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            this.toolsToolStripMenuItem.Size = new System.Drawing.Size(77, 28);
            this.toolsToolStripMenuItem.Text = "Tools";
            // 
            // bMenuDump
            // 
            this.bMenuDump.Name = "bMenuDump";
            this.bMenuDump.Size = new System.Drawing.Size(214, 30);
            this.bMenuDump.Text = "Memory Dump";
            // 
            // configureToolStripMenuItem
            // 
            this.configureToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.driverToolStripMenuItem});
            this.configureToolStripMenuItem.Name = "configureToolStripMenuItem";
            this.configureToolStripMenuItem.Size = new System.Drawing.Size(165, 28);
            this.configureToolStripMenuItem.Text = "Configuration";
            // 
            // driverToolStripMenuItem
            // 
            this.driverToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.bConnect});
            this.driverToolStripMenuItem.Name = "driverToolStripMenuItem";
            this.driverToolStripMenuItem.Size = new System.Drawing.Size(211, 30);
            this.driverToolStripMenuItem.Text = "Driver";
            this.driverToolStripMenuItem.DropDownOpening += new System.EventHandler(this.driverToolStripMenuItem_DropDownOpening);
            // 
            // bConnect
            // 
            this.bConnect.Name = "bConnect";
            this.bConnect.Size = new System.Drawing.Size(211, 30);
            this.bConnect.Text = "Connect";
            this.bConnect.Click += new System.EventHandler(this.bConnect_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.IsSplitterFixed = true;
            this.splitContainer1.Location = new System.Drawing.Point(0, 32);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.bKernel);
            this.splitContainer1.Panel1.Controls.Add(this.label1);
            this.splitContainer1.Panel1.Controls.Add(this.bSelect);
            this.splitContainer1.Panel1.Controls.Add(this.cProcess);
            this.splitContainer1.Panel1MinSize = 35;
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.tProcess);
            this.splitContainer1.Size = new System.Drawing.Size(1200, 568);
            this.splitContainer1.SplitterDistance = 40;
            this.splitContainer1.SplitterWidth = 1;
            this.splitContainer1.TabIndex = 1;
            // 
            // bKernel
            // 
            this.bKernel.Location = new System.Drawing.Point(0, 0);
            this.bKernel.Name = "bKernel";
            this.bKernel.Size = new System.Drawing.Size(75, 23);
            this.bKernel.TabIndex = 3;
            this.bKernel.Text = "Kernel";
            this.bKernel.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(190, 22);
            this.label1.TabIndex = 4;
            this.label1.Text = "Active Processes :";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // bSelect
            // 
            this.bSelect.Location = new System.Drawing.Point(0, 0);
            this.bSelect.Name = "bSelect";
            this.bSelect.Size = new System.Drawing.Size(75, 23);
            this.bSelect.TabIndex = 1;
            this.bSelect.Text = "Select";
            this.bSelect.UseVisualStyleBackColor = true;
            // 
            // cProcess
            // 
            this.cProcess.FormattingEnabled = true;
            this.cProcess.Location = new System.Drawing.Point(0, 0);
            this.cProcess.Name = "cProcess";
            this.cProcess.Size = new System.Drawing.Size(121, 30);
            this.cProcess.TabIndex = 0;
            // 
            // tProcess
            // 
            this.tProcess.Controls.Add(this.tabPage1);
            this.tProcess.Controls.Add(this.tThreads);
            this.tProcess.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tProcess.Location = new System.Drawing.Point(0, 0);
            this.tProcess.Name = "tProcess";
            this.tProcess.SelectedIndex = 0;
            this.tProcess.Size = new System.Drawing.Size(1198, 525);
            this.tProcess.TabIndex = 0;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.lProcessInfo);
            this.tabPage1.Location = new System.Drawing.Point(4, 31);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(1190, 490);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Process Info";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // lProcessInfo
            // 
            this.lProcessInfo.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.lProcessCHeader1,
            this.lProcessCHeader2});
            this.lProcessInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lProcessInfo.FullRowSelect = true;
            this.lProcessInfo.GridLines = true;
            this.lProcessInfo.Location = new System.Drawing.Point(3, 3);
            this.lProcessInfo.Name = "lProcessInfo";
            this.lProcessInfo.Size = new System.Drawing.Size(1184, 484);
            this.lProcessInfo.TabIndex = 0;
            this.lProcessInfo.UseCompatibleStateImageBehavior = false;
            this.lProcessInfo.View = System.Windows.Forms.View.Details;
            // 
            // lProcessCHeader1
            // 
            this.lProcessCHeader1.Text = "Name";
            this.lProcessCHeader1.Width = 250;
            // 
            // lProcessCHeader2
            // 
            this.lProcessCHeader2.Text = "Description";
            this.lProcessCHeader2.Width = 300;
            // 
            // tThreads
            // 
            this.tThreads.Location = new System.Drawing.Point(4, 28);
            this.tThreads.Name = "tThreads";
            this.tThreads.Padding = new System.Windows.Forms.Padding(3);
            this.tThreads.Size = new System.Drawing.Size(1190, 493);
            this.tThreads.TabIndex = 1;
            this.tThreads.Text = "Threads Info";
            this.tThreads.UseVisualStyleBackColor = true;
            // 
            // MainForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(1200, 600);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.menuStrip1);
            this.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MainMenuStrip = this.menuStrip1;
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "MainForm";
            this.Text = "MemoryReviewer";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.tProcess.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button bSelect;
        private System.Windows.Forms.ComboBox cProcess;
        private System.Windows.Forms.TabControl tProcess;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tThreads;
        private System.Windows.Forms.Button bKernel;
        private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bMenuDump;
        private System.Windows.Forms.ListView lProcessInfo;
        private System.Windows.Forms.ColumnHeader lProcessCHeader1;
        private System.Windows.Forms.ColumnHeader lProcessCHeader2;
        private System.Windows.Forms.ToolStripMenuItem configureToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem driverToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bConnect;
    }
}

