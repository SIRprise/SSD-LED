using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using Serilog;
using System.Configuration;

namespace SSD_LED
{
    public partial class SSDLED : Form
    {
#warning TODO: check if screen to restore window is availabe (case: docking station) + show tooltip with process which has most influence in RW

        private NotifyIcon notifyIcon;
        Color defaultColor = Color.Black;
        Color writeColor = Color.FromArgb(255,0,0);
        Color readColor = Color.FromArgb(0,255,0);
        bool enableMixColors = true;
        Icon iconDefault;
        Color oldIconColor;
        //ManagementClass driveDataClass = new ManagementClass("Win32_PerfFormattedData_PerfDisk_PhysicalDisk");

        private PerformanceCounter _diskReadCounter = new PerformanceCounter();
        private PerformanceCounter _diskWriteCounter = new PerformanceCounter();

        private Int32 maxSpeedKBS = 1000;
        private bool endReading = true;

        private System.Timers.Timer readTimer;
        Thread readThread;
        private int tickCount = 0;
        private string diskSelectionPFCStr = null;
        private int tickErrorCount = 0;
        private int tickErrorCountMax = 3;

        #region Debug hiding form...
        /*
        protected override void SetVisibleCore(bool value)
        {
            if (!this.IsHandleCreated)
            {
                value = false;
                CreateHandle();
            }
            base.SetVisibleCore(value);
        }
        
        public bool Visible
        {
            get => base.Visible;
            set => base.Visible = value;
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            this.Visible = false;
        }
        */
        #endregion

        public SSDLED()
        {
            InitializeComponent();

            //var test = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            //var logFilePath = Path.Combine(Environment.GetEnvironmentVariable("LocalAppData"), "log.txt");

            //var appConfigPath = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;

            var userConfigPath = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
            var logFilePath = Path.Combine(Path.GetDirectoryName(userConfigPath.FilePath), "SSDLED.log");



            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day, fileSizeLimitBytes: 1024*100)
                .CreateLogger();

            Log.Information("Starting "+ NameAndVersion());

            //this.Hide(); is useless here...

            oldIconColor = defaultColor;
            iconDefault = CreateIcon(defaultColor);
            initTrayIcon();
            notifyIcon.Text = "Initializing...";

            if(!RefreshDriveList())
            {
                DialogResult dialogResult = MessageBox.Show("Error during initialization of PhysicalDisk list - try again?", "Initialization failed...", MessageBoxButtons.YesNo);
                if (dialogResult == DialogResult.Yes)
                {
                    if (!RefreshDriveList())
                    {
                        Log.Error("Error during initialization of PhysicalDisk list");
                        Log.CloseAndFlush();
                        Application.Exit();
                    }
                }
                else if (dialogResult == DialogResult.No)
                {
                    Log.Error("Error during initialization of PhysicalDisk list");
                    Log.CloseAndFlush();
                    Application.Exit();
                }
            }

            label1.Text = NameAndVersion() + "  by SIRprise";

            if(loadSettings() == false)
                Log.Error("Error while parsing settings");

            maxSpeedKBS = trackBar1.Value;
            textBox1.Text = maxSpeedKBS + " KB/s";
            textBox2.Text = trackBar2.Value + " ms";

            readTimer = new System.Timers.Timer();
            readTimer.Elapsed += new ElapsedEventHandler(OnReadTimeOut);
            readTimer.Interval = 20000;
            readTimer.Enabled = false;

            try
            {
                if(SSDActivityPerfCount())
                {
                    tickErrorCount = 0;
                }
                else
                {
                    tickErrorCount++;
                }
            }
            catch
            {
                MessageBox.Show("Error during initialization");
                Log.Error("Error during initialization");
                Log.CloseAndFlush();
                Application.Exit();
            }
            timer1.Enabled = true;
            notifyIcon.Text = "";
            //MinimizeFootprint();
        }

        #region icon stuff
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = CharSet.Auto)]
        extern static bool DestroyIcon(IntPtr handle);

        private void initTrayIcon()
        {
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = iconDefault;
            notifyIcon.Visible = true;

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
            notifyIcon.MouseClick += notify_Click;
        }

        private void removeTrayIcon()
        {
            notifyIcon.Visible = false;
            notifyIcon.Icon = null;
            notifyIcon.Dispose();
        }

        private Icon CreateIcon(Color color)
        {
            Icon icon = null;

            //create the icon to be written on
            Bitmap bitMapImage = new Bitmap(50, 50);
            Graphics graphicImage = Graphics.FromImage(bitMapImage);

            LinearGradientBrush lgb = new LinearGradientBrush(new Rectangle(0, 0, 50, 50), color, Color.FromArgb(color.A, (int)color.R / 5, (int)color.G / 5, (int)color.B / 5), 0f, true);
            graphicImage.FillEllipse(lgb, new Rectangle(0, 0, 50, 50));

            IntPtr hBmp = IntPtr.Zero;
            //sometimes there is an gdi+ error...because we get no more handles...or we lose handle just before getting icon(!)          
            try
            {
                hBmp = bitMapImage.GetHicon();
                icon = System.Drawing.Icon.FromHandle(hBmp);
            }
            catch
            {
                icon = iconDefault;
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
            }

            //cleanup
            /*
            //MS seemed to forget to free the handle in the destructor... but if we do it, we lose our icon!?
            //https://stackoverflow.com/questions/12026664/a-generic-error-occurred-in-gdi-when-calling-bitmap-gethicon
            Thread.MemoryBarrier();
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            Application.DoEvents();
            if(hBmp != IntPtr.Zero)
                DestroyIcon(hBmp);
                */
            graphicImage.Dispose();
            bitMapImage.Dispose();

            if (icon.Size.Height == 0)
            {
                //here we are, if we destroy the handle...
                icon = iconDefault;
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
            }

            return icon;
        }
        #endregion

        #region controller / core logic
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

        public bool SSDActivityPerfCount()
        {
            float bytesPSRead=0f;
            float bytesPSWrite=0f;

            if (!checkBox1.Checked || (diskSelectionPFCStr == null))
            {
                try
                {
                    bytesPSRead = GetCounterValue(_diskReadCounter, "PhysicalDisk", "Disk Read Bytes/sec", "_Total");
                    bytesPSWrite = GetCounterValue(_diskWriteCounter, "PhysicalDisk", "Disk Write Bytes/sec", "_Total");
                }
                catch (Exception ex)
                {
                    Log.Error("PhysicalDisk->_Total could not be captured - Exception here: {exc}", ex);
                    return false;
                }
            }
            else
            {
                try
                {
                    bytesPSRead = GetCounterValue(_diskReadCounter, "PhysicalDisk", "Disk Read Bytes/sec", diskSelectionPFCStr);
                    bytesPSWrite = GetCounterValue(_diskWriteCounter, "PhysicalDisk", "Disk Write Bytes/sec", diskSelectionPFCStr);
                }
                catch (Exception ex)
                {
                    Log.Error("PhysicalDisk->{selectionString} could not captured - Exception here: {exc}", diskSelectionPFCStr ,ex);
                    return false;
                }
            }

            //this cases should never happen (but happened probably caused by uninitialized floats bytesPSRead/Write - fixed now)
            if (bytesPSRead<0)
            {
                Log.Error("PhysicalDisk _diskReadCounter was negative:{numb}", bytesPSRead);
                return false;
            }

            if (bytesPSWrite < 0)
            {
                Log.Error("PhysicalDisk _diskWriteCounter was negative:{numb}", bytesPSWrite);
                return false;
            }

            //notifyIcon.Text = Math.Round(bytesPSRead / 1024, 2).ToString() + " KB/s read / " + Math.Round(bytesPSWrite / 1024, 2).ToString() + " KB/s write";

            Color newColor = CalculateColor(bytesPSRead, bytesPSWrite);
            if (newColor == Color.Empty)
                return false;

            Icon temp = notifyIcon.Icon;

            //create only new icon if it changed
            if (oldIconColor != newColor)
            {
                notifyIcon.Icon = CreateIcon(newColor);
                if (temp != iconDefault)
                {
                    //if we don't do this, windows refuses after 10000
                    DestroyIcon(temp.Handle);
                    temp.Dispose();
                }
                oldIconColor = newColor;
            }

            //notifyIcon.Visible = false;
            //notifyIcon.Visible = true;

            updateChartWindow(bytesPSRead, bytesPSWrite, newColor);
            return true;
        }

        private Color CalculateColor(float bytesPSRead, float bytesPSWrite)
        {
            if(enableMixColors)
            {
                #warning div by 0 possible!!!
                int scaledKBSRead = 0;
                int scaledKBSWrite = 0;
                try
                {
                    scaledKBSRead = (int)((bytesPSRead / 1024) / maxSpeedKBS * 255);
                    scaledKBSWrite = (int)((bytesPSWrite / 1024) / maxSpeedKBS * 255);
                }
                catch (Exception ex)
                {
                    Log.Error("division by 0 - Exception here: {exc}",ex);
                    return Color.Empty;
                }
                scaledKBSRead = scaledKBSRead > 255 ? 255 : scaledKBSRead;
                scaledKBSWrite = scaledKBSWrite > 255 ? 255 : scaledKBSWrite;

                if(scaledKBSRead < 0)
                {
                    scaledKBSRead = 0;
                    Log.Error("scaledKBSRead < 0 --> {numb}",scaledKBSRead);
                }

                if(scaledKBSWrite < 0)
                {
                    scaledKBSWrite = 0;
                    Log.Error("scaledKBSWrite < 0 --> {numb}", scaledKBSWrite);
                }


                int R = (int)(((readColor.R / 255f) * scaledKBSRead + (writeColor.R / 255f) * scaledKBSWrite));// / 2);
                int G = (int)(((readColor.G / 255f) * scaledKBSRead + (writeColor.G / 255f) * scaledKBSWrite));// / 2);
                int B = (int)(((readColor.B / 255f) * scaledKBSRead + (writeColor.B / 255f) * scaledKBSWrite));// / 2);
                R = R > 255 ? 255 : R;
                G = G > 255 ? 255 : G;
                B = B > 255 ? 255 : B;
                return Color.FromArgb(R, G, B);
            }
            else
            {
                if( (((bytesPSRead / 1024) / maxSpeedKBS) > 0) || (((bytesPSRead / 1024) / maxSpeedKBS) > 0))
                {
                    if(bytesPSRead>bytesPSWrite)
                    {
                        return readColor;
                    }
                    else
                    {
                        return writeColor;
                    }
                }
                return defaultColor;
            }
        }

        private void updateChartWindow(float bytesPSRead, float bytesPSWrite,Color buttonColor)
        {
            int scaledMBSRead = (int)(bytesPSRead / (1024 * 1024) + 0.5);
            int scaledMBSWrite = (int)(bytesPSWrite / (1024 * 1024) + 0.5);
            scaledMBSRead = scaledMBSRead < 1 ? 1 : scaledMBSRead;
            scaledMBSWrite = scaledMBSWrite < 1 ? 1 : scaledMBSWrite;

            int maxTickCountChart = 100;
            if ((this.WindowState != FormWindowState.Minimized) && (this.Visible == true))
            {
                button3.BackColor = buttonColor;
                chart1.Series["Read"].Points.AddXY(tickCount, scaledMBSRead);
                chart1.Series["Write"].Points.AddXY(tickCount, scaledMBSWrite);

                if (tickCount == maxTickCountChart)
                {
                    tickCount = -1;
                    //avoid icon is getting lost...but crashs if context menu is opened at the wrong time...
                    //removeTrayIcon();
                    //initTrayIcon();
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

                tickCount++;
            }
            else
            {
                tickCount = 100;
            }
            
        }
        #endregion

        #region helpers
        /*
        [System.Runtime.InteropServices.DllImport("psapi.dll", CharSet = CharSet.Auto)]
        extern static int EmptyWorkingSet(IntPtr hwProc);

        static void MinimizeFootprint()
        {
            EmptyWorkingSet(Process.GetCurrentProcess().Handle);
        }
        */

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

        private string NameAndVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + " v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        private void ReadSpeed(string sDir)
        {
            //1st try: iterate through directories and read...
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



        private bool RefreshDriveList()
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
            string[] instanceNames;
            try
            {
                instanceNames = pfcCat.GetInstanceNames();
            }
            catch
            {
                return false;
            }
            comboBox1.Items.AddRange(instanceNames);
            Log.Information("Found the following Physical Disks: {DriveInstances}", instanceNames);
            if(instanceNames.Length<1)
            {
                MessageBox.Show("Error during initialization: No physical disks found!");
                Log.Error("Error during initialization: No physical disks found!");
                return false;
            }
            return true;
        }

        private void SetStartup(bool create)
        {
            RegistryKey rk = Registry.CurrentUser.OpenSubKey
                ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            if (create)
                rk.SetValue("SSD-LED", Application.ExecutablePath);
            else
                rk.DeleteValue("SSD-LED", false);
        }
        #endregion

        #region events
        private void timer1_Tick(object sender, EventArgs e)
        {
            //SSDActivityWMI();
            if (SSDActivityPerfCount())
            {
                tickErrorCount = 0;
            }
            else
            {
                tickErrorCount++;
            }

            if(tickErrorCount>=tickErrorCountMax)
            {
                timer1.Enabled = false;

                MessageBox.Show("Something went wrong... exiting...");
                Log.Information("Too many errors in sequence -> Exiting...");
                Log.CloseAndFlush();
                Application.Exit();
            }

            //check health() via MSFT_StorageReliabilityCounter class
        }

        private void SSDLED_Load(object sender, EventArgs e)
        {
            //workaround: visibility is ALWAYS true after constructor
            this.Visible = false;
        }

        private void SSDLED_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.ApplicationExitCall)
            {
                removeTrayIcon();
                base.OnFormClosing(e);
            }
            else
            {
                e.Cancel = true;
                this.WindowState = FormWindowState.Minimized;
                this.Visible = false;
            }
        }

        void notify_Click(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                info_Click(this, null);
            }
        }

        void info_Click(object sender, EventArgs e)
        {
            //toggle
            if ((this.Visible == false) && (this.WindowState == FormWindowState.Minimized))
            {
                //unhide
                this.Visible = true;
                //ensure it is really in front
                this.WindowState = FormWindowState.Minimized;
                this.WindowState = FormWindowState.Normal;
                this.BringToFront();
            }
            else
            {
                //hide
                button2_Click(sender, e);
            }
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
            //Close();
            removeTrayIcon();

            Log.Information("Exiting...");
            Log.CloseAndFlush();
            Application.Exit();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            //close -> hide
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Visible = false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //apply / save
            saveSettings();
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

        private void button4_Click(object sender, EventArgs e)
        {
            //read for x seconds
            if (endReading == true)
            {
                button4.Enabled = false;
                endReading = false;
                readTimer.Enabled = true;
                readThread = new Thread(delegate () { ReadSpeed(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)); });
                readThread.Start();
            }
        }

        private void trackBar1_ValueChanged(object sender, EventArgs e)
        {
            maxSpeedKBS = trackBar1.Value;
            maxSpeedKBS = (maxSpeedKBS > 0) ? maxSpeedKBS : 1;
            textBox1.Text = maxSpeedKBS + " KB/s";
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
                Log.Information("Changed single drive monitoring - checked status: " + checkBox1.Checked);
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            string backupSelectionStr = diskSelectionPFCStr;
            try
            {
                diskSelectionPFCStr = GetInstanceNameByDriveIndex(comboBox1.SelectedIndex);
                Debug.WriteLine("Selected: " + diskSelectionPFCStr);
                Log.Information("Selected single drive: " + comboBox1.Text + " which is index " + comboBox1.SelectedIndex + " of the drive list");
            }
            catch
            {
                Log.Error("Changed drive selection didn't work!");
                //unselect
                comboBox1.SelectedIndex = -1;
                diskSelectionPFCStr = backupSelectionStr;
                checkBox1.Checked = false;
                MessageBox.Show("Error while selection - aborting...");
            }
        }

        #endregion

        #region settings load and save
        private bool loadSettings()
        {
            Log.Information("Loading settings:");
            Log.Information("-----------------");

            int tempInt;
            bool tempBool;
            try
            {
                int.TryParse(Properties.Settings.Default["MaxSpeed"].ToString(), out maxSpeedKBS);
                trackBar1.Value = maxSpeedKBS;
                Log.Information("max KBS: " + maxSpeedKBS);
                int.TryParse(Properties.Settings.Default["RefreshIntervall"].ToString(), out tempInt);
                timer1.Interval = tempInt;
                trackBar2.Value = tempInt;
                Log.Information("RefreshInterval: " + tempInt);
                bool.TryParse(Properties.Settings.Default["DriveSelectedChecked"].ToString(), out tempBool);
                Log.Information("SingleDrive monitoring: " + tempBool);
                if (tempBool)
                {
                    diskSelectionPFCStr = Properties.Settings.Default["DriveSelected"].ToString();
                    try
                    {
                        PerformanceCounterCategory tempPfcCat = new PerformanceCounterCategory("PhysicalDisk");
                        string[] instNames;
                        try
                        {
                            instNames = tempPfcCat.GetInstanceNames();

                            //plausi check if string is possible and drive available
                            bool markerFound = false;
                            foreach(string drv in instNames)
                            {
                                if (drv.Equals(diskSelectionPFCStr))
                                {
                                    markerFound = true;
                                    Log.Information("Successfully parsed single drive monitoring instance " + diskSelectionPFCStr);
                                }
                            }
                            if (markerFound == false)
                            {
                                Log.Error("Error finding monitored drive!");
                                throw new Exception();
                            }
                        }
                        catch
                        {
                            Log.Error("Error while getting physical disk list for plausi check!");
                            diskSelectionPFCStr = null;
                            tempBool = false;
                        }
                    }
                    catch
                    {
                        Log.Error("Error while getting physical disk category");
                        diskSelectionPFCStr = null;
                        tempBool = false;
                    }
                }
                else
                {
                    diskSelectionPFCStr = null;
                }
                checkBox1.Checked = tempBool;

                defaultColor = ColorTranslator.FromHtml(Properties.Settings.Default["ColorDefault"].ToString());
                iconDefault = CreateIcon(defaultColor);
                tbColorDefault.BackColor = defaultColor;
                tbColorDefault.Text = defaultColor.ToString();
                Log.Information("ColorDefault: " + defaultColor.ToString());

                readColor = ColorTranslator.FromHtml(Properties.Settings.Default["ColorRead"].ToString());
                tbColorRead.BackColor = readColor;
                tbColorRead.Text = readColor.ToString();
                Log.Information("ColorRead: " + readColor.ToString());

                writeColor = ColorTranslator.FromHtml(Properties.Settings.Default["ColorWrite"].ToString());
                tbColorWrite.BackColor = writeColor;
                tbColorWrite.Text = writeColor.ToString();
                Log.Information("ColorWrite: " + writeColor.ToString());

                Log.Information("-----------------");
                return true;
            }
            catch
            {
                Log.Information("-----------------");
                return false;
            }
        }

        private string ToHexValue(Color color)
        {
            return "#" + color.R.ToString("X2") +
                         color.G.ToString("X2") +
                         color.B.ToString("X2");
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
            Properties.Settings.Default["ColorDefault"] = ToHexValue(defaultColor); //ColorTranslator.ToHtml(defaultColor);
            Properties.Settings.Default["ColorRead"] = ToHexValue(readColor);
            Properties.Settings.Default["ColorWrite"] = ToHexValue(writeColor);
            Properties.Settings.Default.Save();
        }

        //change autostart setting
        private void button5_Click(object sender, EventArgs e)
        {
            SetStartup(checkBox2.Checked);
        }

        #endregion

        private void button6_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog1 = new ColorDialog();
            if (colorDialog1.ShowDialog() == DialogResult.OK)
            {
                tbColorDefault.BackColor = colorDialog1.Color;
                tbColorDefault.Text = colorDialog1.Color.ToString();
            }
        }

        private void btnColorRead_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog1 = new ColorDialog();
            if (colorDialog1.ShowDialog() == DialogResult.OK)
            {
                tbColorRead.BackColor = colorDialog1.Color;
                tbColorRead.Text = colorDialog1.Color.ToString();
            }
        }

        private void btnColorWrite_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog1 = new ColorDialog();
            if (colorDialog1.ShowDialog() == DialogResult.OK)
            {
                tbColorWrite.BackColor = colorDialog1.Color;
                tbColorWrite.Text = colorDialog1.Color.ToString();
            }
        }

        private void btnApplyColor_Click(object sender, EventArgs e)
        {
            //apply code
            defaultColor = tbColorDefault.BackColor;
            readColor = tbColorRead.BackColor;
            writeColor = tbColorWrite.BackColor;

            iconDefault = CreateIcon(defaultColor);
            enableMixColors = checkBox3.Checked;
        }
    }


}
