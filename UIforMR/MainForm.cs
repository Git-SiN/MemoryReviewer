using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.Collections;


namespace UIforMR
{
    /// <summary>
    /// STRUCTURE
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, CharSet = CharSet.Unicode)]
    public struct MESSAGE_FORM
    {
        [FieldOffset(0)]
        public uint Type;
        [FieldOffset(0)]
        public uint Address;
        [FieldOffset(4)]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1020)]
        public byte[] bMessage;
        [FieldOffset(4)]
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 510)]
        public string uMessage;
    }

    public partial class MainForm : Form
    {
        internal const string dllName = "DllforMR.dll";

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ConnectToKernel();
        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool DisConnect();
        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ReceiveMessage(ref MESSAGE_FORM message);
        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CancelMyPendingIRPs();
        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SendControlMessage(byte ctlCode, ref MESSAGE_FORM message);

        private Thread CommunicationThread;
        private Thread CancellingThread;

        private bool isDriverLoaded = false;
        private volatile bool isOwnTermination = false;
        private bool IsCommunicationThreadStarted = false;
        public bool isCommunicationThreadStarted
        {
            get { return IsCommunicationThreadStarted; }
            set
            {
                IsCommunicationThreadStarted = value;
                if (IsCommunicationThreadStarted)
                    bConnect.Text = "Disconnect";
                else
                    bConnect.Text = "Connect";
            }
        }

        private sbyte alignedProcessList = 0;
        //////////////////////////////////////////////////////////////////////////
        //////////////////			Message.Type                //////////////////
        //////////////////////////////////////////////////////////////////////////
        public const uint INITIALIZE_COMMUNICATION = 0x80000000;
        public const byte TERMINATE_USER_THREAD = 0xFF;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            if (InitDevice())
            {
                isDriverLoaded = true;
                InitializeAppearance();
                GetProcess();

                CommunicationThread = new Thread(CommunicationRoutine);
                CommunicationThread.Start();

                if (CommunicationThread != null && CommunicationThread.ThreadState == ThreadState.Running)
                    return;
                else
                {
                    MessageBox.Show("Failed to Create the User Communication Thread.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DisConnect();
                }
            }

            Dispose();
            Close();
        }

        private void InitializeAppearance()
        {
            // MainForm...
            this.label1.Location = new Point(120, 13);
            this.tSelectedProcess.Location = new Point(this.label1.Location.X + this.label1.Size.Width + 5, 10);
            this.tSelectedProcess.Size = new Size(500, 30);
            this.bSelect.Location = new Point(this.tSelectedProcess.Location.X + this.tSelectedProcess.Size.Width + 8, 7);
            this.bKernel.Size = this.bSelect.Size = new Size(120, 27);
            this.bKernel.Location = new Point(this.bSelect.Location.X + this.bSelect.Size.Width + 8, 7);
            this.Size = new Size(this.bKernel.Location.X + this.bKernel.Size.Width + 30, 600);
        }

        private bool InitDevice()
        {
            int errCode = 0;

            if (!(File.Exists("loader.dll")))
            {
                MessageBox.Show("[ loader.dll ] is not found", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            else {
                if (!(File.Exists(dllName)))
                {
                    MessageBox.Show("[ " + dllName + " ] is not found", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                else {
                    if (!ConnectToKernel())
                    {
                        errCode = Marshal.GetLastWin32Error();

                        if (errCode == 5)
                            MessageBox.Show("Restart this Application with the ADMINISTRATOR Privilege.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        else
                        {
                            MessageBox.Show("Failed to Load my Driver. (" + String.Format("ErrCode : {0:d}", errCode) + ")", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        return false;
                    }
                    else
                        return true;
                }
            }
        }

        private void CommunicationRoutine()
        {
            MESSAGE_FORM message;

            do
            {
                message = new MESSAGE_FORM();
                if (ReceiveMessage(ref message))
                {
                    switch (message.Type)
                    {
                        case INITIALIZE_COMMUNICATION:
                            isCommunicationThreadStarted = true;
                            break;
                        default:
                            // For test...
                            //isCommunicationThreadStarted = false;
                            //MessageBox.Show(String.Format("READ TYPE : {0:X8}", message.Type));
                            break;
                    }
                }
                else
                    isCommunicationThreadStarted = false;
            } while (isCommunicationThreadStarted);

            if (!isOwnTermination)
            {
                isOwnTermination = false;
                MessageBox.Show("Disconnected with the Driver.\r\nIf you want to continue, You can find the CONNECT Button in the Menu.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (isDriverLoaded)
            {
                if (CommunicationThread != null && CommunicationThread.ThreadState == ThreadState.Running && isCommunicationThreadStarted)
                {
                    bConnect_Click(this, null);
                    CommunicationThread.Join();
                }

                DisConnect();
            }

            Application.ExitThread();
        }

        private void TerminateUserCommunicationThread()
        {
            if (CancelMyPendingIRPs())
            {
                MESSAGE_FORM message = new MESSAGE_FORM();
                if (SendControlMessage(TERMINATE_USER_THREAD, ref message))
                    isCommunicationThreadStarted = false;
            }
        }

        private void bConnect_Click(object sender, EventArgs e)
        {
            if (bConnect.Text == "Connect")
            {
                if (CommunicationThread != null && CommunicationThread.ThreadState == ThreadState.Running)
                    MessageBox.Show("The Communication Thread is already running.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                else
                {
                    CommunicationThread = new Thread(CommunicationRoutine);
                    CommunicationThread.Start();

                    if (!(CommunicationThread != null && CommunicationThread.ThreadState == ThreadState.Running))
                        MessageBox.Show("Failed to Connect with my Driver.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                if (CancellingThread != null && CancellingThread.ThreadState == ThreadState.Running)
                    MessageBox.Show("The Cancelling Thread is alreay running.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                else
                {
                    isOwnTermination = true;

                    CancellingThread = new Thread(TerminateUserCommunicationThread);
                    CancellingThread.Start();
                    CancellingThread.Join();

                    if (isCommunicationThreadStarted)
                        MessageBox.Show("Failed to Disconnect with my Driver.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void driverToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            if (isDriverLoaded)
            {
                bConnect.Enabled = true;
            }
            else
            {
                bConnect.Enabled = false;
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        private void bSelect_Click(object sender, EventArgs e)
        {
            if (bSelect.Text == "Select")
            {
                if(lvProcessList.SelectedItems.Count == 1)
                {
                    tSelectedProcess.Text = "[" + lvProcessList.SelectedItems[0].SubItems[1].Text.Trim(' ') + "] " + lvProcessList.SelectedItems[0].SubItems[0].Text;
                    if (lvProcessList.SelectedItems[0].SubItems[2].Text.Contains(":::"))
                        tSelectedProcess.Text += (" -" + lvProcessList.SelectedItems[0].SubItems[2].Text.Remove(0, 3));
                    bSelect.Text = "Deselect";
                    bSelect.BackColor = Color.LightCoral;
                    lvProcessList.Visible = false;
                    tSelectedProcess.Enabled = false;
                }
            }
            else
            {
                bSelect.Text = "Select";
                bSelect.BackColor = SystemColors.Control;
                lvProcessList.Visible = true;
                GetProcess();

                tSelectedProcess.Enabled = true;
                tSelectedProcess.Focus();
                tSelectedProcess.SelectAll();
            }
        }
        
        private void GetProcess()
        {
            List<ListViewItem> items = new List<ListViewItem>();
            ImageList icons = new ImageList();
            System.Diagnostics.Process[] processList = System.Diagnostics.Process.GetProcesses();

            lvProcessList.Items.Clear();
            lvProcessList.SmallImageList = icons;

            if (processList.Count() > 0)
            {
                foreach (System.Diagnostics.Process currentP in processList)
                {
                    ListViewItem currentItem = null;
                    try
                    {
                        string file = currentP.MainModule.FileName.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries).Last();
                        string path = currentP.MainModule.FileName.Remove(currentP.MainModule.FileName.Length - file.Length);

                        if (!icons.Images.ContainsKey(file))
                            icons.Images.Add(file, Icon.ExtractAssociatedIcon(currentP.MainModule.FileName));

                        currentItem = new ListViewItem(new string[] { file, String.Format("{0,5}", currentP.Id), (currentP.MainWindowTitle.Length > 0) ? ("::: " + currentP.MainWindowTitle) : currentP.MainModule.FileVersionInfo.FileDescription, currentP.MainModule.FileVersionInfo.CompanyName, path }, file);
                    }
                    catch (Win32Exception)
                    {
                        currentItem = new ListViewItem(new string[] { currentP.ProcessName, String.Format("{0,5}", currentP.Id), "", "", "" });
                    }
                    catch (InvalidOperationException) { }       // The Process Exited...
                    catch (SystemException ee)
                    {
                        MessageBox.Show("Failed to get the information of " + currentP.ProcessName + "\r\n" + ee.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    if (currentItem != null)
                        items.Add(currentItem);
                }

                if (items.Count > 0)
                {
                    lvProcessList.Items.AddRange(items.ToArray());
                    lvProcessList.Columns[alignedProcessList].Text = lvProcessList.Columns[alignedProcessList].Text.Remove(lvProcessList.Columns[alignedProcessList].Text.Length - 2);
                    lvProcessList.Columns[0].Text += " ▲";
                    lvProcessList.Sorting = SortOrder.Ascending;
                    alignedProcessList = 0;

                    lvProcessList.Columns[0].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
                    lvProcessList.Columns[1].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
                    lvProcessList.Columns[2].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
                    lvProcessList.Columns[3].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
                    lvProcessList.Columns[4].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
                    lvProcessList.ListViewItemSorter = new ListViewItemComparer();
                }
            }
        }

        private void lvProcessList_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            lvProcessList.Columns[alignedProcessList].Text = lvProcessList.Columns[alignedProcessList].Text.Remove(lvProcessList.Columns[alignedProcessList].Text.Length - 2);

            if (alignedProcessList == e.Column)
            {
                if (lvProcessList.Sorting == SortOrder.Ascending)
                    lvProcessList.Sorting = SortOrder.Descending;
                else
                    lvProcessList.Sorting = SortOrder.Ascending;
            }
            else
            {
                alignedProcessList = (sbyte)e.Column;
                lvProcessList.Sorting = SortOrder.Ascending;
            }

            if (lvProcessList.Sorting == SortOrder.Ascending)
                lvProcessList.Columns[e.Column].Text += " ▲";
            else
                lvProcessList.Columns[e.Column].Text += " ▼";

            lvProcessList.ListViewItemSorter = new ListViewItemComparer(e.Column, lvProcessList.Sorting);
        }

        private void tSelectedProcess_KeyUp(object sender, KeyEventArgs e)
        {
            ListViewItem item = null;
            
            if (e.KeyCode == Keys.Up)
            {
                e.SuppressKeyPress = true;

                if ((lvProcessList.SelectedItems.Count != 0) && (lvProcessList.SelectedItems[0].Index > 0))
                    item = lvProcessList.Items[lvProcessList.SelectedItems[0].Index - 1];
            }
            else if (e.KeyCode == Keys.Down)
            {
                e.SuppressKeyPress = true;
                
                if (lvProcessList.SelectedItems.Count == 0)
                    item = lvProcessList.Items[0];
                else {
                    if (lvProcessList.SelectedItems[0].Index < lvProcessList.Items.Count - 1)
                        item = lvProcessList.Items[lvProcessList.SelectedItems[0].Index + 1];
                }
            }
            else if ((e.KeyCode >= Keys.A) && (e.KeyCode <= Keys.Z))
            {
                if (tSelectedProcess.Text.Length > 0)
                    item = lvProcessList.FindItemWithText(tSelectedProcess.Text);
            }
            else if(e.KeyCode == Keys.Enter)
            {
                if(lvProcessList.SelectedItems.Count == 1)
                {
                    bSelect_Click(this, null);
                    return;
                }
            }

            if (item != null)
            {
                item.Selected = true;
                lvProcessList.EnsureVisible(item.Index);
            }
        }
    }



    ////////////////////////////////////////////////////////////////////////////////////
    class ListViewItemComparer : IComparer
    {
        private int col;
        private SortOrder order;

        public ListViewItemComparer()
        {
            col = 0;
            order = SortOrder.Ascending;
        }

        public ListViewItemComparer(int column, SortOrder or)
        {
            col = column;
            order = or;
        }

        public int Compare(object x, object y)
        {
            if (order == SortOrder.Ascending)
                return String.Compare(((ListViewItem)x).SubItems[col].Text, ((ListViewItem)y).SubItems[col].Text);
            else if (order == SortOrder.Descending)
                return -1 * (String.Compare(((ListViewItem)x).SubItems[col].Text, ((ListViewItem)y).SubItems[col].Text));
            else
                return 0;
        }
    }


}
