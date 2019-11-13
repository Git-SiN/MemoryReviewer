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
            this.modulesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.kernelObjectsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showOBJECTToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cbShowObject = new System.Windows.Forms.ToolStripComboBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tSelectedProcess = new System.Windows.Forms.TextBox();
            this.bKernel = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.bSelect = new System.Windows.Forms.Button();
            this.lvProcessList = new System.Windows.Forms.ListView();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader4 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader5 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.tabProcess = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.tvEprocess = new System.Windows.Forms.TreeView();
            this.lProcessInfo = new System.Windows.Forms.ListView();
            this.tThreads = new System.Windows.Forms.TabPage();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tabProcess.SuspendLayout();
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
            this.configureToolStripMenuItem,
            this.modulesToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Padding = new System.Windows.Forms.Padding(3, 2, 0, 2);
            this.menuStrip1.Size = new System.Drawing.Size(1200, 31);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(66, 27);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // toolsToolStripMenuItem
            // 
            this.toolsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.bMenuDump});
            this.toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            this.toolsToolStripMenuItem.Size = new System.Drawing.Size(77, 27);
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
            this.configureToolStripMenuItem.Size = new System.Drawing.Size(165, 27);
            this.configureToolStripMenuItem.Text = "Configuration";
            // 
            // driverToolStripMenuItem
            // 
            this.driverToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.bConnect});
            this.driverToolStripMenuItem.Name = "driverToolStripMenuItem";
            this.driverToolStripMenuItem.Size = new System.Drawing.Size(159, 30);
            this.driverToolStripMenuItem.Text = "Driver";
            this.driverToolStripMenuItem.DropDownOpening += new System.EventHandler(this.driverToolStripMenuItem_DropDownOpening);
            // 
            // bConnect
            // 
            this.bConnect.Name = "bConnect";
            this.bConnect.Size = new System.Drawing.Size(170, 30);
            this.bConnect.Text = "Connect";
            this.bConnect.Click += new System.EventHandler(this.bConnect_Click);
            // 
            // modulesToolStripMenuItem
            // 
            this.modulesToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.kernelObjectsToolStripMenuItem});
            this.modulesToolStripMenuItem.Name = "modulesToolStripMenuItem";
            this.modulesToolStripMenuItem.Size = new System.Drawing.Size(99, 27);
            this.modulesToolStripMenuItem.Text = "Modules";
            // 
            // kernelObjectsToolStripMenuItem
            // 
            this.kernelObjectsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addFileToolStripMenuItem,
            this.showOBJECTToolStripMenuItem});
            this.kernelObjectsToolStripMenuItem.Name = "kernelObjectsToolStripMenuItem";
            this.kernelObjectsToolStripMenuItem.Size = new System.Drawing.Size(236, 30);
            this.kernelObjectsToolStripMenuItem.Text = "KernelObjects";
            // 
            // addFileToolStripMenuItem
            // 
            this.addFileToolStripMenuItem.Name = "addFileToolStripMenuItem";
            this.addFileToolStripMenuItem.Size = new System.Drawing.Size(258, 30);
            this.addFileToolStripMenuItem.Text = "Add new OBJECTs";
            this.addFileToolStripMenuItem.Click += new System.EventHandler(this.addFileToolStripMenuItem_Click);
            // 
            // showOBJECTToolStripMenuItem
            // 
            this.showOBJECTToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.cbShowObject});
            this.showOBJECTToolStripMenuItem.Name = "showOBJECTToolStripMenuItem";
            this.showOBJECTToolStripMenuItem.Size = new System.Drawing.Size(258, 30);
            this.showOBJECTToolStripMenuItem.Text = "Show OBJECT";
            this.showOBJECTToolStripMenuItem.DropDownOpening += new System.EventHandler(this.showOBJECTToolStripMenuItem_DropDownOpening);
            // 
            // cbShowObject
            // 
            this.cbShowObject.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.cbShowObject.Font = new System.Drawing.Font("Consolas", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cbShowObject.Margin = new System.Windows.Forms.Padding(0);
            this.cbShowObject.Name = "cbShowObject";
            this.cbShowObject.Size = new System.Drawing.Size(255, 31);
            this.cbShowObject.Sorted = true;
            this.cbShowObject.SelectedIndexChanged += new System.EventHandler(this.cbShowObject_SelectedIndexChanged);
            this.cbShowObject.KeyDown += new System.Windows.Forms.KeyEventHandler(this.KeyLocker);
            this.cbShowObject.KeyUp += new System.Windows.Forms.KeyEventHandler(this.KeyLocker);
            // 
            // splitContainer1
            // 
            this.splitContainer1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.IsSplitterFixed = true;
            this.splitContainer1.Location = new System.Drawing.Point(0, 31);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.tSelectedProcess);
            this.splitContainer1.Panel1.Controls.Add(this.bKernel);
            this.splitContainer1.Panel1.Controls.Add(this.label1);
            this.splitContainer1.Panel1.Controls.Add(this.bSelect);
            this.splitContainer1.Panel1MinSize = 35;
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.lvProcessList);
            this.splitContainer1.Panel2.Controls.Add(this.tabProcess);
            this.splitContainer1.Size = new System.Drawing.Size(1200, 569);
            this.splitContainer1.SplitterDistance = 40;
            this.splitContainer1.SplitterWidth = 1;
            this.splitContainer1.TabIndex = 1;
            // 
            // tSelectedProcess
            // 
            this.tSelectedProcess.Location = new System.Drawing.Point(290, 1);
            this.tSelectedProcess.Name = "tSelectedProcess";
            this.tSelectedProcess.Size = new System.Drawing.Size(100, 29);
            this.tSelectedProcess.TabIndex = 0;
            this.tSelectedProcess.KeyUp += new System.Windows.Forms.KeyEventHandler(this.tSelectedProcess_KeyUp);
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
            this.label1.Size = new System.Drawing.Size(210, 22);
            this.label1.TabIndex = 4;
            this.label1.Text = "Selected Processes :";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // bSelect
            // 
            this.bSelect.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.bSelect.Location = new System.Drawing.Point(0, 0);
            this.bSelect.Name = "bSelect";
            this.bSelect.Size = new System.Drawing.Size(75, 23);
            this.bSelect.TabIndex = 1;
            this.bSelect.Text = "Select";
            this.bSelect.UseVisualStyleBackColor = true;
            this.bSelect.Click += new System.EventHandler(this.bSelect_Click);
            // 
            // lvProcessList
            // 
            this.lvProcessList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2,
            this.columnHeader3,
            this.columnHeader4,
            this.columnHeader5});
            this.lvProcessList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lvProcessList.FullRowSelect = true;
            this.lvProcessList.GridLines = true;
            this.lvProcessList.HideSelection = false;
            this.lvProcessList.Location = new System.Drawing.Point(0, 0);
            this.lvProcessList.MultiSelect = false;
            this.lvProcessList.Name = "lvProcessList";
            this.lvProcessList.Size = new System.Drawing.Size(1198, 526);
            this.lvProcessList.TabIndex = 0;
            this.lvProcessList.UseCompatibleStateImageBehavior = false;
            this.lvProcessList.View = System.Windows.Forms.View.Details;
            this.lvProcessList.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.lvProcessList_ColumnClick);
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "Process ▲";
            this.columnHeader1.Width = 150;
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "PID";
            this.columnHeader2.Width = 50;
            // 
            // columnHeader3
            // 
            this.columnHeader3.Text = "Description";
            this.columnHeader3.Width = 200;
            // 
            // columnHeader4
            // 
            this.columnHeader4.Text = "Company";
            this.columnHeader4.Width = 400;
            // 
            // columnHeader5
            // 
            this.columnHeader5.Text = "File Path";
            this.columnHeader5.Width = 400;
            // 
            // tabProcess
            // 
            this.tabProcess.Controls.Add(this.tabPage1);
            this.tabProcess.Controls.Add(this.tThreads);
            this.tabProcess.Controls.Add(this.tabPage2);
            this.tabProcess.Controls.Add(this.tabPage3);
            this.tabProcess.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabProcess.Location = new System.Drawing.Point(0, 0);
            this.tabProcess.Name = "tabProcess";
            this.tabProcess.SelectedIndex = 0;
            this.tabProcess.Size = new System.Drawing.Size(1198, 526);
            this.tabProcess.TabIndex = 0;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.tvEprocess);
            this.tabPage1.Controls.Add(this.lProcessInfo);
            this.tabPage1.Location = new System.Drawing.Point(4, 31);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(1190, 491);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Process Info";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // tvEprocess
            // 
            this.tvEprocess.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tvEprocess.FullRowSelect = true;
            this.tvEprocess.HideSelection = false;
            this.tvEprocess.Location = new System.Drawing.Point(3, 3);
            this.tvEprocess.Name = "tvEprocess";
            this.tvEprocess.Size = new System.Drawing.Size(1184, 485);
            this.tvEprocess.TabIndex = 1;
            // 
            // lProcessInfo
            // 
            this.lProcessInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lProcessInfo.FullRowSelect = true;
            this.lProcessInfo.GridLines = true;
            this.lProcessInfo.Location = new System.Drawing.Point(3, 3);
            this.lProcessInfo.Name = "lProcessInfo";
            this.lProcessInfo.Size = new System.Drawing.Size(1184, 485);
            this.lProcessInfo.TabIndex = 0;
            this.lProcessInfo.UseCompatibleStateImageBehavior = false;
            this.lProcessInfo.View = System.Windows.Forms.View.Details;
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
            // tabPage2
            // 
            this.tabPage2.Location = new System.Drawing.Point(4, 28);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(1190, 493);
            this.tabPage2.TabIndex = 2;
            this.tabPage2.Text = "tabPage2";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // tabPage3
            // 
            this.tabPage3.Location = new System.Drawing.Point(4, 28);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage3.Size = new System.Drawing.Size(1190, 493);
            this.tabPage3.TabIndex = 3;
            this.tabPage3.Text = "tabPage3";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.Filter = "MR_ files (MR_*.txt)|MR_*.txt|Text files (*.txt)|*.txt|All files (*.*)|*.*";
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
            this.tabProcess.ResumeLayout(false);
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
        private System.Windows.Forms.TabControl tabProcess;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tThreads;
        private System.Windows.Forms.Button bKernel;
        private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bMenuDump;
        private System.Windows.Forms.ListView lProcessInfo;
        private System.Windows.Forms.ToolStripMenuItem configureToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem driverToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bConnect;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.ListView lvProcessList;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader2;
        private System.Windows.Forms.ColumnHeader columnHeader3;
        private System.Windows.Forms.ColumnHeader columnHeader4;
        private System.Windows.Forms.ColumnHeader columnHeader5;
        private System.Windows.Forms.TextBox tSelectedProcess;
        private System.Windows.Forms.TreeView tvEprocess;
        private System.Windows.Forms.ToolStripMenuItem modulesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem kernelObjectsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addFileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showOBJECTToolStripMenuItem;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.ToolStripComboBox cbShowObject;
    }
}

