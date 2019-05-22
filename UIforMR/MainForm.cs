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
            this.cProcess.Location = new Point(this.label1.Location.X + this.label1.Size.Width + 5, 10);
            this.cProcess.Size = new Size(500, 30);
            this.bSelect.Location = new Point(this.cProcess.Location.X + this.cProcess.Size.Width + 8, 7);
            this.bKernel.Size = this.bSelect.Size = new Size(120, 27);
            this.bKernel.Location = new Point(this.bSelect.Location.X + this.bSelect.Size.Width + 8, 7);
            this.Size = new Size(this.bKernel.Location.X + this.bKernel.Size.Width + 30, 600);

            // Tab : Process Info
            this.lProcessCHeader2.Width = this.lProcessInfo.Width - this.lProcessCHeader1.Width;
            
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
                            MessageBox.Show(String.Format("READ TYPE : {0:X8}", message.Type));
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
                if(SendControlMessage(TERMINATE_USER_THREAD, ref message))
                    isCommunicationThreadStarted = false;
            }
        }

        private void bConnect_Click(object sender, EventArgs e)
        {
            if(bConnect.Text == "Connect")
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
    }
}
