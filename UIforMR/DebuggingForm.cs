using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UIforMR
{
    public partial class DebuggingForm : Form
    {
        private Size buttonSize = new Size(80,25);
        private Size distance = new Size(4, 2);
        private int maxLengthOfLines = 0;
        
        public DebuggingForm()
        {
            InitializeComponent();

            FormType("Test Comment :", MessageBoxButtons.YesNo);
        }

        public DebuggingForm(string caption, string[] messages, MessageBoxButtons buttonType, string comment = null)
        {
            InitializeComponent();

            this.Text = caption;
            this.textBox.Lines = messages;

            foreach (string message in messages)
            {
                if (maxLengthOfLines < message.Length)
                    maxLengthOfLines = message.Length;
            }

            maxLengthOfLines = (int)(this.textBox.Font.Size * maxLengthOfLines * 0.84);
            FormType(comment, buttonType);
        }

        private void FormType(string comment, MessageBoxButtons type)
        {
            bYes.Size = bNo.Size = buttonSize;
            this.splitContainer1.Panel2MinSize = distance.Height * 2 + buttonSize.Height;

            if (comment != null)
            {
                this.lComment.Visible = true;
                this.lComment.Text = comment;

                if (maxLengthOfLines < this.lComment.Width)
                    maxLengthOfLines = this.lComment.Width;

                this.lComment.Location = new Point(distance.Width, distance.Height);
                this.splitContainer1.Panel2MinSize += (this.lComment.Height + distance.Height);
            }
            maxLengthOfLines += (distance.Width * 2);
            if (this.Width < maxLengthOfLines)
                this.Width = (maxLengthOfLines < Screen.PrimaryScreen.Bounds.Width) ? maxLengthOfLines : (Screen.PrimaryScreen.Bounds.Width - this.Padding.Left - this.Padding.Right);

            this.splitContainer1.SplitterDistance = this.splitContainer1.Height - (this.splitContainer1.Panel2MinSize + this.splitContainer1.SplitterWidth);


            switch (type) {
                case MessageBoxButtons.OK:
                    bNo.Visible = false;
                    bYes.Location = new Point(this.splitContainer1.Panel2.Width - buttonSize.Width - distance.Width, this.splitContainer1.Panel2.Height - buttonSize.Height - distance.Height);
                    bYes.Text = "OK";
                    break;
                case MessageBoxButtons.YesNo:
                    bNo.Location = new Point(this.splitContainer1.Panel2.Width - buttonSize.Width - distance.Width, this.splitContainer1.Panel2.Height - buttonSize.Height - distance.Height);
                    bNo.Text = "No";

                    bYes.Location = new Point(bNo.Location.X - buttonSize.Width - distance.Width, this.splitContainer1.Panel2.Height - buttonSize.Height - distance.Height);
                    bYes.Text = "Yes";
                    break;
                default:
                    bYes.Visible = bNo.Visible = false;
                    break;
            }

        }

        private void DebuggingForm_Load(object sender, EventArgs e)
        {
            if (KernelObjects.debuggingFormLocation != Point.Empty)
                this.Location = KernelObjects.debuggingFormLocation;
        }

        private void Button_Click(object sender, EventArgs e)
        {
            switch (((Button)sender).Text)
            {
                case "Yes":
                    this.DialogResult = DialogResult.Yes;
                    break;
                case "No":
                    this.DialogResult = DialogResult.No;
                    break;
                case "OK":
                    this.DialogResult = DialogResult.OK;
                    break;
                default:
                    this.DialogResult = DialogResult.Abort;
                    break;
            }

            KernelObjects.debuggingFormLocation = this.Location;
            return;
        }
    }
}
