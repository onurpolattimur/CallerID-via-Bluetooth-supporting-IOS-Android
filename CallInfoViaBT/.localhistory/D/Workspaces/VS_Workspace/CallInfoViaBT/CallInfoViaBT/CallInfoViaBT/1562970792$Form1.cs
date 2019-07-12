using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CallInfoViaBT
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
        }
        private Stream peerStream;
        private BluetoothClient client;

        private void button1_Click(object sender, EventArgs e)
        {
            if (client == null || client.Connected == false)
            {
                MessageBox.Show("Please make sure you have connected to device.");
                return;
            }
            string[] dialCmd = new string[6];
            dialCmd[0] = "AT+CMER\r";
            dialCmd[1] = "AT+CIND=?\r";
            dialCmd[3] = "ATD +"+TB_phoneNumber.Text+";\r";

            runCommand(dialCmd[0], peerStream);
            runCommand(dialCmd[1], peerStream);
            runCommand(dialCmd[3], peerStream);

        }

        private void runCommand(string command, Stream peerStream)
        {
            Byte[] sRes = new Byte[200];
            Byte[] dcB = System.Text.Encoding.ASCII.GetBytes(command);
            peerStream.Write(dcB, 0, dcB.Length);

        }

        public static String bytesToString(byte[] data)
        {
            String dataOut = "";
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] != 0x00)
                    dataOut += (char)data[i];
            }
            return dataOut;
        }

        private void connectButton_Click(object sender, EventArgs e)
        {
            var deviceAddr = discoveredDevices.SelectedValue.ToString();
            BluetoothAddress addr = BluetoothAddress.Parse(deviceAddr);
            BluetoothEndPoint rep = new BluetoothEndPoint(addr, BluetoothService.Handsfree);



            client.Connect(rep);
            peerStream = client.GetStream();
            backgroundWorker1.RunWorkerAsync();

            //Get call AT commands
            string[] dialCmd = new string[50];
            dialCmd[0] = "AT\r";
            dialCmd[1] = "AT+CMER=3,0,0,1,0\r";
            dialCmd[2] = "AT+CLIP=1\r";

            runCommand(dialCmd[0], peerStream);
            runCommand(dialCmd[1], peerStream);
            runCommand(dialCmd[2], peerStream);

        }

        private void closeConnection(object sender, FormClosedEventArgs e)
        {
            if (client != null && client.Connected == true)
            {
                peerStream.Close();
                client.Close();
            }

        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                Console.WriteLine("Waiting for response..");
                Byte[] sRes = new Byte[200];
                peerStream.Read(sRes, 0, 199);
                var resStr = bytesToString(sRes);
                Console.WriteLine(resStr);
                if (resStr.Contains("CLIP"))
                {
                    onCallWithCLIP(resStr);
                }
                else if (resStr.Contains("3,1"))
                {

                    onCall(true);

                }
                else if (resStr.Contains("3,3"))
                {
                    onCall(false);
                }


                Thread.Sleep(500);
            }
        }
        private void onCallWithCLIP(string resStr)
        {
            //YES! Now I have call information.
            Console.WriteLine("Call information: " + resStr);
            char[] phoneNumber = new char[16];

            int startPhoneNoIndex = resStr.IndexOf("\"");
            int endPhoneNoIndex = resStr.IndexOf("\"", startPhoneNoIndex + 1);
            resStr.CopyTo(startPhoneNoIndex + 1, phoneNumber, 0, endPhoneNoIndex - startPhoneNoIndex - 1);
            string strPhoneNumber = new string(phoneNumber);
            strPhoneNumber = strPhoneNumber.Trim('\0');
            Console.WriteLine("Tel no: " + strPhoneNumber);


            if (!resStr.Contains("CIEV")) return;
            string[] row = { DateTime.Now.ToShortTimeString(), strPhoneNumber, "N/A" };
            var listViewItem = new ListViewItem(row);
            LWCalls.Items.Add(listViewItem);
            sendNotification("There is a new incoming call from " + strPhoneNumber);
        }
        private void onCall(bool incoming)
        {
            try
            {
                string getCurrentCallCmd = "AT + CLCC ?\r";
                Byte[] sRes = new Byte[200];
                Byte[] dcB = System.Text.Encoding.ASCII.GetBytes(getCurrentCallCmd);
                peerStream.Write(dcB, 0, dcB.Length);
                peerStream.Read(sRes, 0, 199);
                var resStr = bytesToString(sRes);
                if (resStr.Contains("+CLCC"))
                {
                    //YES! Now I have call information.
                    Console.WriteLine("Call information: " + resStr);
                    char[] phoneNumber = new char[16];
                    char[] contactName = new char[256];

                    //Let's get phone number. Listen this now: https://open.spotify.com/track/1jr1BnpXymcf2i97mTR8tJ?si=Z1xdaa5gTu6kKSg3D3X8Qg
                    int startPhoneNoIndex = resStr.IndexOf("\"");
                    int endPhoneNoIndex = resStr.IndexOf("\"", startPhoneNoIndex + 1);
                    resStr.CopyTo(startPhoneNoIndex + 1, phoneNumber, 0, endPhoneNoIndex - startPhoneNoIndex - 1);
                    string strPhoneNumber = new string(phoneNumber);
                    strPhoneNumber = strPhoneNumber.Trim('\0');
                    Console.WriteLine("Tel no: " + strPhoneNumber);


                    //Now, it is time to get contact name, if the caller is not saved in phone. Now listen this: https://open.spotify.com/track/3G2zIBotS9OUCK5uaHN2hz?si=XgF-V-JLQ26ucMLy3JCc3Q

                    int startContactIndex = resStr.IndexOf("\"", endPhoneNoIndex + 1);
                    int endContactIndex = resStr.IndexOf("\"", startContactIndex + 1);
                    resStr.CopyTo(startContactIndex + 1, contactName, 0, endContactIndex - startContactIndex - 1);
                    string strContactName = new string(contactName);
                    strContactName = strContactName.Trim('\0');
                    Console.WriteLine("Contact Name: " + strContactName);


                    string[] row = { DateTime.Now.ToShortTimeString(), strPhoneNumber, strContactName };
                    var listViewItem = new ListViewItem(row);

                    if (incoming)
                    {
                        LWCalls.Items.Add(listViewItem);
                        sendNotification("There is a new incoming call from " + strPhoneNumber);

                    }

                    else
                    {
                        LWOutCalls.Items.Add(listViewItem);
                        sendNotification("There is a new outgoing call to " + strPhoneNumber);


                    }


                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }



        }

        private void findButton_Click(object sender, EventArgs e)
        {

            discoveredDevices.DisplayMember = "DeviceName";
            discoveredDevices.ValueMember = "DeviceAddress";

            Cursor.Current = Cursors.WaitCursor;
            var devices = client.DiscoverDevices();
            discoveredDevices.DataSource = devices;
            Cursor.Current = Cursors.Default;


            warningLabel.Visible = true;
            connectButton.Enabled = true;
            discoveredDevices.Enabled = true;
            callButton.Enabled = true;

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Console.SetOut(new MultiTextWriter(new ControlWriter(richTextBox1), Console.Out));
            client = new BluetoothClient();
            warningLabel.Visible = false;
            connectButton.Enabled = false;
            discoveredDevices.Enabled = false;
            callButton.Enabled = false;
        }
        private void sendNotification(string msg)
        {.
            NotifyIcon MyIcon = new NotifyIcon();
            MyIcon.Visible = true;
            MyIcon.Icon = System.Drawing.SystemIcons.Information;
            MyIcon.Text = "Call Information";
            MyIcon.BalloonTipTitle = "Call Information";
            MyIcon.BalloonTipText =msg;
            MyIcon.BalloonTipIcon = ToolTipIcon.Info;
            MyIcon.ShowBalloonTip(5000);
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            richTextBox1.SelectionStart = richTextBox1.Text.Length;
            richTextBox1.ScrollToCaret();
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            sendNotification("asd");
        }
    }
    public class MultiTextWriter : TextWriter
    {
        private IEnumerable<TextWriter> writers;
        public MultiTextWriter(IEnumerable<TextWriter> writers)
        {
            this.writers = writers.ToList();
        }
        public MultiTextWriter(params TextWriter[] writers)
        {
            this.writers = writers;
        }

        public override void Write(char value)
        {
            foreach (var writer in writers)
                writer.Write(value);
        }

        public override void Write(string value)
        {
            foreach (var writer in writers)
                writer.Write(value);
        }

        public override void Flush()
        {
            foreach (var writer in writers)
                writer.Flush();
        }

        public override void Close()
        {
            foreach (var writer in writers)
                writer.Close();
        }

        public override Encoding Encoding
        {
            get { return Encoding.ASCII; }
        }
    }
    public class ControlWriter : TextWriter
    {
        private Control richTextBox;
        public ControlWriter(Control textbox)
        {
            this.richTextBox = textbox;
        }

        public override void Write(char value)
        {
            richTextBox.Text += value;
        }

        public override void Write(string value)
        {
            richTextBox.Text += value;
        }

        public override Encoding Encoding
        {
            get { return Encoding.ASCII; }
        }
    }
}
