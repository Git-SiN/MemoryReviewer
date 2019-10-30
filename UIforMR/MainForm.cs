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
    public partial class MainForm : Form
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi, Size = 1036)]
        public class B_MESSAGE_FORM     // DEFAULT Type of MESSAGE_FORM.
        {
            public ushort Type;
            public ushort Res;
            public uint Address;
            public uint Size;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
            public byte[] bMessage;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode, Size = 1036)]
        public class U_MESSAGE_FORM
        {
            public ushort Type;
            public ushort Res;
            public uint Address;
            public uint Size;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
            public string uMessage;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode, Size = 1036)]
        public class R_MESSAGE_FORM
        {
            public ushort Type;
            public ushort Res;
            public uint Address;
            public uint Size;
            public REQUIRED_OFFSET Required;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode, Size = 516)]
        public class REQUIRED_OFFSET
        {
            public uint Offset;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string ObjectName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string FieldName;
        }

        internal const string dllName = "DllforMR.dll";

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ConnectToKernel();
        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool DisConnect();
        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CancelMyPendingIRPs();

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SendControlMessage(ushort ctlCode, [In, Out] B_MESSAGE_FORM message);
        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SendControlMessage(ushort ctlCode, [In, Out] U_MESSAGE_FORM message);
        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SendControlMessage(ushort ctlCode, [In, Out] R_MESSAGE_FORM message);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern bool ReceiveMessage([In, Out] B_MESSAGE_FORM message);

        /// WriteFile
        //[DllImport(dllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
        //private static extern void WriteMessage(ref bool pResult, [In, Out] R_MESSAGE_FORM message);


        //////////////////////////////////////////////////////////////////////////
        //////////////////			Message.Type                //////////////////
        //////////////////////////////////////////////////////////////////////////
        public const ushort INITIALIZE_COMMUNICATION = 0x800;
        public const ushort TERMINATE_USER_THREAD = 0x8FF;
        public const ushort URGENT_GET_REQUIRED_OFFSET = 0x4F0;

        public const byte RESPONSE_REQUIRED_OFFSET = 0x04;
        public const byte SET_TARGET_OBJECT = 0xF1;
        public const byte GET_BYTE_STREAM = 0x40;
        public const byte GET_KERNEL_OBJECT = 0x41;




        private KernelObjects kernelObjects = null;

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
                try
                {
                    IsCommunicationThreadStarted = value;
                    if (IsCommunicationThreadStarted)
                        bConnect.Text = "Disconnect";
                    else
                        bConnect.Text = "Connect";
                }
                catch (System.InvalidOperationException) { }    // Only need for debugging
            }
        }

        private sbyte alignedProcessList = 0;

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

                // For Test...
                //kernelObjects = new KernelObjects(this);
                //return;
                
                CommunicationThread = new Thread(CommunicationRoutine);
                CommunicationThread.Start();

                if (CommunicationThread != null && CommunicationThread.ThreadState == ThreadState.Running)
                {
                    kernelObjects = new KernelObjects(this);
                    return;
                }
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
            B_MESSAGE_FORM message;

            do
            {
                message = new B_MESSAGE_FORM();
                if (ReceiveMessage(message))
                {
                    switch (message.Type)
                    {
                        case INITIALIZE_COMMUNICATION:
                            isCommunicationThreadStarted = true;

                            // For Test...
                            //MessageBox.Show(ByteArrayToString((message as B_MESSAGE_FORM).bMessage, 1024));
                            break;
                        case URGENT_GET_REQUIRED_OFFSET:
                            GetRequiredOffsets((REQUIRED_OFFSET)ByteToStructure(message.bMessage, typeof(REQUIRED_OFFSET)));
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
                U_MESSAGE_FORM message = new U_MESSAGE_FORM();
                if (SendControlMessage(TERMINATE_USER_THREAD, message))
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
        /// <summary>
        /// Convert from 'Byte Array' to 'Structure'.
        /// </summary>
        internal object ByteToStructure(byte[] buffer, Type type, uint arrayCount = 0)
        {
            if (type == null)
                return null;

            int typeLength = Marshal.SizeOf(type);

            IntPtr buff = Marshal.AllocHGlobal(typeLength); 
            Marshal.Copy(buffer, (int)(arrayCount * typeLength), buff, typeLength); 
            object obj = Marshal.PtrToStructure(buff, type);

            Marshal.FreeHGlobal(buff);

            if (Marshal.SizeOf(obj) != typeLength)
            {
                return null;
            }

            return obj;
        }
        
        /// <summary>
        /// Convert from 'Byte Array' to 'String'.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="byteLength"></param>
        /// <returns></returns>
        internal string ByteArrayToString(byte[] buffer, int byteLength)
        {
            if (byteLength > 0)
            {
                int stringLength = 0;
                for(stringLength = 0; stringLength < byteLength; stringLength += 2)
                {
                    if ((buffer[stringLength] == 0) && (buffer[stringLength + 1] == 0))
                    {
                        stringLength /= 2;
                        break;
                    }
                }

                if(stringLength > 0)
                {
                    IntPtr buff = Marshal.AllocHGlobal((stringLength + 1) * 2); 
                    Marshal.Copy(buffer, 0, buff, (stringLength + 1) * 2); 
                    string result = Marshal.PtrToStringUni(buff, stringLength); 
                    Marshal.FreeHGlobal(buff);

                    if (result.Length == stringLength)
                        return result; 
                }
            }

            return null;
        }

        private void GetRequiredOffsets(REQUIRED_OFFSET Required)
        {
            R_MESSAGE_FORM message = new R_MESSAGE_FORM();
            message.Required = new REQUIRED_OFFSET();
            message.Type = RESPONSE_REQUIRED_OFFSET;

           if (Required != null)
            {
                message.Required.ObjectName = Required.ObjectName.Trim();
                message.Required.FieldName = Required.FieldName.Trim();

                // Query to 'KernelObjects' class.
                if ((message.Required.ObjectName.Length > 0) && (message.Required.FieldName.Length > 0))
                {
                    int tmp = KernelObjects.IndexOfThisObject(KernelObjects.Registered, message.Required.ObjectName);
                    if (tmp != -1)
                    {
                        tmp = KernelObjects.Registered[tmp].GetFieldOffset(message.Required.FieldName);
                        if (tmp != -1)
                            message.Required.Offset = (uint)tmp;
                    }
                }
            }

            if(message.Required.Offset == 0)
                message.Res = 0xFFFF;       // Signal for Failure.

            SendControlMessage(message.Type, message);
        }

        private void SendControlMessageThread(U_MESSAGE_FORM message)
        {
            if (!SendControlMessage(message.Type, message))
            {
                MessageBox.Show(String.Format("Failed to send a control message : 0x{0:4X}", message.Type), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SendControlMessageThread(R_MESSAGE_FORM message)
        {
            if (!SendControlMessage(message.Type, message))
            {
                MessageBox.Show(String.Format("Failed to send a control message : 0x{0:4X}", message.Type), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SendControlMessageThread(B_MESSAGE_FORM message)
        {
            if (!SendControlMessage(message.Type, message))
            {
                MessageBox.Show(String.Format("Failed to send a control message : 0x{0:4X}", message.Type), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void bSelect_Click(object sender, EventArgs e)
        {
            if (bSelect.Text == "Select")
            {
                if(lvProcessList.SelectedItems.Count == 1)
                {
                    U_MESSAGE_FORM message = new U_MESSAGE_FORM();
                    message.uMessage = lvProcessList.SelectedItems[0].SubItems[0].Text.Trim();
                    message.Res = Convert.ToUInt16(lvProcessList.SelectedItems[0].SubItems[1].Text.Trim());
                    message.Type = SET_TARGET_OBJECT;

                    if(SendControlMessage(SET_TARGET_OBJECT, message))
                    {
                        // Parse the EPROCESS.
                        //                        GetByteStreamFromKernel(GET_KERNEL_OBJECT, "_EPROCESS", 0);
                        ////////////////    확인 완료..    코드 정리만 하고 깃헙에 올리자..


                        tSelectedProcess.Text = "[" + lvProcessList.SelectedItems[0].SubItems[1].Text.Trim() + "] " + lvProcessList.SelectedItems[0].SubItems[0].Text;
                        if (lvProcessList.SelectedItems[0].SubItems[2].Text.Contains(":::"))
                            tSelectedProcess.Text += (" -" + lvProcessList.SelectedItems[0].SubItems[2].Text.Remove(0, 3));
                        bSelect.Text = "Deselect";
                        bSelect.BackColor = Color.LightCoral;
                        lvProcessList.Visible = false;
                        tSelectedProcess.Enabled = false;
                    }
                    else
                    {
                        MessageBox.Show("Failed to find this Process.", "Failed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
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
        
        private bool GetByteStreamFromKernel(byte Type, string ObjectName, uint StartAddress, uint Size = 0)
        {
            bool result = false;

            switch (Type){
                case GET_BYTE_STREAM:
                    if ((StartAddress != 0) && (Size != 0))
                    {
                        B_MESSAGE_FORM message = new B_MESSAGE_FORM();

                        message.Address = StartAddress;
                        message.Size = Size;
                        message.Type = Type;

                        result = SendControlMessage(Type, message);
                    }
                    break;
                case GET_KERNEL_OBJECT:
                    if (ObjectName != null)
                    {
                        U_MESSAGE_FORM message = new U_MESSAGE_FORM();
                        message.Size = kernelObjects.GetObjectSize(ObjectName);
                        if (message.Size != 0)
                        {
                            message.uMessage = ObjectName;
                            message.Type = Type;

                            result = SendControlMessage(Type, message);
                        }
                    }
                    break;
                default:
                    break;
            }

            return result;
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
