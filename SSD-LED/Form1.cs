using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Timers;
using System.Windows.Forms;

namespace SSD_LED
{
    public partial class SSDLED : Form
    {
        private readonly NotifyIcon notifyIcon = new NotifyIcon();
        //ManagementClass driveDataClass = new ManagementClass("Win32_PerfFormattedData_PerfDisk_PhysicalDisk");
        Icon iconBlack;

        private PerformanceCounter _diskReadCounter = new PerformanceCounter();
        private PerformanceCounter _diskWriteCounter = new PerformanceCounter();

        private Int32 maxSpeedKBS = 1000;
        private bool endReading = true;

        private System.Timers.Timer readTimer;
        Thread readThread;
        private int tickCount = 0;
        private string diskSelectionPFCStr = null;


        public SSDLED()
        {
            InitializeComponent();

            //hide form
            this.Visible = false;

            iconBlack = CreateIcon(Color.Black);

            notifyIcon.Icon = iconBlack;
            notifyIcon.Visible = true;
            notifyIcon.Text = "Initializing...";

            //create menu items
            MenuItem info = new MenuItem("Preferences");
            MenuItem quit = new MenuItem("Exit");
            ContextMenu contextMenu = new ContextMenu();

            //add items to menu
            contextMenu.MenuItems.Add(info);
            contextMenu.MenuItems.Add(quit);

            //add menu to symbol
            notifyIcon.ContextMenu = contextMenu;

            //link click events
            quit.Click += exit_Click;
            info.Click += info_Click;
            notifyIcon.Click += info_Click;
            
            RefreshDriveList();

            label1.Text = NameAndVersion() + "  by SIRprise";

            loadSettings();

            maxSpeedKBS = trackBar1.Value;
            textBox1.Text = maxSpeedKBS + " KB/s";
            textBox2.Text = trackBar2.Value + " ms";

            readTimer = new System.Timers.Timer();
            readTimer.Elapsed += new ElapsedEventHandler(OnReadTimeOut);
            readTimer.Interval = 20000;
            readTimer.Enabled = false;

            try
            {
                SSDActivityPerfCount();
            }
            catch
            {
                MessageBox.Show("Error during initialization");
                Application.Exit();
            }
            timer1.Enabled = true;
        }

        private string NameAndVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + " v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        private void OnReadTimeOut(object sender, ElapsedEventArgs e)
        {
            readTimer.Enabled = false;
            endReading = true;
            readThread.Join();
            readThread = null;
            if (button4.InvokeRequired)
            {
                button4.Invoke((MethodInvoker)delegate { button4.Enabled = true; });
            }
            else
            {
                button4.Enabled = true;
            }
        }

        private Icon CreateIcon(Color color)
        {
            Icon icon = null;

            try
            {
                //create the icon to be written on
                Bitmap bitMapImage = new Bitmap(50, 50);
                Graphics graphicImage = Graphics.FromImage(bitMapImage);

                LinearGradientBrush lgb = new LinearGradientBrush(new Rectangle(0, 0, 50, 50), color, Color.FromArgb(color.A,(int)color.R/5,(int)color.G/5,(int)color.B/5), 0f, true);
                graphicImage.FillEllipse(lgb, new Rectangle(0, 0, 50, 50));


                icon = System.Drawing.Icon.FromHandle(bitMapImage.GetHicon());

                //cleanup
                graphicImage.Dispose();
                bitMapImage.Dispose();
            }
            catch
            {
            }
            return icon;
        }


        void info_Click(object sender, EventArgs e)
        {
            this.Visible = true;
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
        }

        void exit_Click(object sender, EventArgs e)
        {
            this.Visible = true;
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
            this.ShowInTaskbar = true;
            timer1.Enabled = false;
            /*
            if (driveDataClass != null)
            {
                driveDataClass.Dispose();
            }
             */
            Close();
            Application.Exit();
        }

        public void SSDActivityPerfCount()
        {
            float bytesPSRead;
            float bytesPSWrite;

            if (!checkBox1.Checked || (diskSelectionPFCStr == null))
            {
                bytesPSRead = GetCounterValue(_diskReadCounter, "PhysicalDisk", "Disk Read Bytes/sec", "_Total");
                bytesPSWrite = GetCounterValue(_diskWriteCounter, "PhysicalDisk", "Disk Write Bytes/sec", "_Total");
            }
            else
            {

                bytesPSRead = GetCounterValue(_diskReadCounter, "PhysicalDisk", "Disk Read Bytes/sec", diskSelectionPFCStr);
                bytesPSWrite = GetCounterValue(_diskWriteCounter, "PhysicalDisk", "Disk Write Bytes/sec", diskSelectionPFCStr);
            }

            notifyIcon.Text = Math.Round(bytesPSRead / 1024, 2).ToString() + " KB/s read / " + Math.Round(bytesPSWrite / 1024, 2).ToString() + " KB/s write";

            int scaledKBSRead = (int)((bytesPSRead / 1024) / maxSpeedKBS * 255);
            int scaledKBSWrite = (int)((bytesPSWrite / 1024) / maxSpeedKBS * 255);
            scaledKBSRead = scaledKBSRead > 255 ? 255 : scaledKBSRead;
            scaledKBSWrite = scaledKBSWrite > 255 ? 255 : scaledKBSWrite;
            notifyIcon.Icon = CreateIcon(Color.FromArgb(scaledKBSWrite, scaledKBSRead, 0));

            button3.BackColor = Color.FromArgb(scaledKBSWrite, scaledKBSRead, 0);
            int scaledMBSRead = (int)(bytesPSRead / (1024 * 1024) + 0.5);
            int scaledMBSWrite = (int)(bytesPSWrite / (1024 * 1024) + 0.5);
            scaledMBSRead = scaledMBSRead < 1 ? 1 : scaledMBSRead;
            scaledMBSWrite = scaledMBSWrite < 1 ? 1 : scaledMBSWrite;
            
            //if ((this.WindowState != FormWindowState.Minimized) && (this.Visible == true))
            //{
                chart1.Series["Read"].Points.AddXY(tickCount, scaledMBSRead);
                chart1.Series["Write"].Points.AddXY(tickCount, scaledMBSWrite);               

                int maxTickCountChart = 100;
                if (tickCount == maxTickCountChart)
                {
                    tickCount = -1;
                    chart1.Series["Read"].Points.Clear();
                    chart1.Series["Write"].Points.Clear();
                }
                else
                {
                    if (chart1.ChartAreas["ChartArea1"].AxisX.Maximum != maxTickCountChart)
                    {
                        chart1.ChartAreas["ChartArea1"].AxisX.Maximum = maxTickCountChart;
                        chart1.ChartAreas["ChartArea1"].AxisX2.Maximum = maxTickCountChart;
                    }
                }
            //}
            tickCount++;
        }

        float GetCounterValue(PerformanceCounter pc, string categoryName, string counterName, string instanceName)
        {
            pc.CategoryName = categoryName;
            pc.CounterName = counterName;
            pc.InstanceName = instanceName;
            return pc.NextValue();
        }

        string GetInstanceNameByDriveIndex(int driveIndex)
        {
            PerformanceCounterCategory pfcCat = new PerformanceCounterCategory("PhysicalDisk");
            return pfcCat.GetInstanceNames()[driveIndex];
        }

        /*
        public void SSDActivityWMI()
        {
            ManagementObjectCollection driveDataClassCollection = driveDataClass.GetInstances();
            foreach (ManagementObject obj in driveDataClassCollection)
            {
                if (obj["Name"].ToString() == "_Total")
                {
                    Int64 bytesPS = Convert.ToInt64(obj["DiskBytesPersec"]);
                    if (bytesPS > 100)
                    {
                        bytesPS /= 1024;
                        bytesPS = bytesPS > 255 ? 255 : bytesPS;
                        notifyIcon.Icon = CreateIcon(Color.FromArgb(0,(int)bytesPS,0));//iconGreen;
                        notifyIcon.Text = bytesPS + " KB/s";
                    }
                    else
                    {
                        notifyIcon.Icon = iconBlack;
                    }
                }

            }
        }
         */

        private void timer1_Tick(object sender, EventArgs e)
        {
            //SSDActivityWMI();
            SSDActivityPerfCount();

            //check health() via MSFT_StorageReliabilityCounter class
        }

        private void SSDLED_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.ApplicationExitCall)
            {
                e.Cancel = true;
                this.WindowState = FormWindowState.Minimized;
                this.Visible = false;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //apply / save
            saveSettings();
        }

        private bool loadSettings()
        {
            int tempInt;
            bool tempBool;
            try
            {
                int.TryParse(Properties.Settings.Default["MaxSpeed"].ToString(), out maxSpeedKBS);
                trackBar1.Value = maxSpeedKBS;
                int.TryParse(Properties.Settings.Default["RefreshIntervall"].ToString(), out tempInt);
                timer1.Interval = tempInt;
                trackBar2.Value = tempInt;
                bool.TryParse(Properties.Settings.Default["DriveSelectedChecked"].ToString(), out tempBool);
                if (tempBool)
                {
                    diskSelectionPFCStr = Properties.Settings.Default["DriveSelected"].ToString();
                }
                else
                {
                    diskSelectionPFCStr = null;
                }
                checkBox1.Checked = tempBool;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void saveSettings()
        {
            Properties.Settings.Default["MaxSpeed"] = maxSpeedKBS;
            Properties.Settings.Default["RefreshIntervall"] = timer1.Interval;
            Properties.Settings.Default["DriveSelectedChecked"] = checkBox1.Checked;
            if (checkBox1.Checked)
            {
                Properties.Settings.Default["DriveSelected"] = diskSelectionPFCStr;
            }
            Properties.Settings.Default.Save();
        }

        private void ReadSpeed(string sDir)
        {
            /*
                [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
                private static extern SafeFileHandle CreateFile(string lpFileName, [MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess, [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode, IntPtr lpSecurityAttributes, [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition, [MarshalAs(UnmanagedType.U4)] FileAttributes dwFlagsAndAttributes, IntPtr hTemplateFile);

                [DllImport("kernel32.dll", SetLastError = true)]
                [return: MarshalAs(UnmanagedType.Bool)]
                private static extern bool CloseHandle(IntPtr hObject);
                
                FileOptions fileOptions = (FileOptions)(134217728 | (int.MinValue ) | (536870912));
                SafeFileHandle file = CreateFile(f, FileAccess.Read, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, (FileAttributes)fileOptions, IntPtr.Zero);
                if (file.IsInvalid)
                    return;
                FileStream fileStream = new FileStream(file, FileAccess.Read, 4096, false);
            */
            try
            {
                foreach (string f in Directory.GetFiles(sDir))
                {
                    byte[] buf = new byte[1024 * 1024];
                    try
                    {
                        int offset = 0;
                        while (endReading != false)
                        {
                            using (Stream file = File.OpenRead(f))
                            {
                                file.Read(buf, offset, 1024 * 1024);
                                offset += 1024 * 1024;
                            }
                        }
                    }
                    catch (Exception) { };
                    if (endReading)
                        break;
                }

                foreach (string d in Directory.GetDirectories(sDir))
                {
                    if (endReading)
                        break;
                    ReadSpeed(d);
                }
            }
            catch (System.Exception) { };
        }

        private void button4_Click(object sender, EventArgs e)
        {
            //read for x seconds
            if (endReading == true)
            {
                button4.Enabled = false;
                endReading = false;
                readTimer.Enabled = true;
                readThread = new Thread(delegate() { ReadSpeed(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)); });
                readThread.Start();
            }
        }

        private void trackBar1_ValueChanged(object sender, EventArgs e)
        {
            maxSpeedKBS = trackBar1.Value;
            textBox1.Text = maxSpeedKBS + " KB/s";
        }

        private void RefreshDriveList()
        {
            comboBox1.Items.Clear();
            /*
            foreach (string logicalDrive in Environment.GetLogicalDrives)
            {
                DriveInfo driveInfo = new DriveInfo(logicalDrive);
                if (driveInfo.IsReady)
                {
                    this.comboBox1.Items.Add((object)(logicalDrive.ToUpper() + " [" + driveInfo.DriveFormat.ToString() + "]"));//; " + (object)(driveInfo.TotalSize / 1024L / 1024L) + " / " + (object)(driveInfo.AvailableFreeSpace / 1024L / 1024L) + " MiB]"));
                }
            }
             */
            /*
            foreach (System.IO.DriveInfo drive in System.IO.DriveInfo.GetDrives())
            {
                if (drive.DriveType == System.IO.DriveType.Fixed) 
                {
                    comboBox1.Items.Add(drive.Name.ToString());
                }
            }
             */
            PerformanceCounterCategory pfcCat = new PerformanceCounterCategory("PhysicalDisk");
            comboBox1.Items.AddRange(pfcCat.GetInstanceNames());
        }

        private void trackBar2_ValueChanged(object sender, EventArgs e)
        {
            timer1.Interval = trackBar2.Value;
            textBox2.Text = trackBar2.Value + " ms";
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                comboBox1.Enabled = true;
                if (diskSelectionPFCStr != null)
                    foreach (var item in comboBox1.Items)
                        if (item.ToString().Equals(diskSelectionPFCStr))
                            comboBox1.SelectedItem = item;
            }
            else
            {
                comboBox1.Enabled = false;
                diskSelectionPFCStr = null;
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            diskSelectionPFCStr = GetInstanceNameByDriveIndex(comboBox1.SelectedIndex);
            Debug.WriteLine("Selected: " + diskSelectionPFCStr);
        }
    }
}
