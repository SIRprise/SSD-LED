using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Management;
using System.Text;
using System.Windows.Forms;

namespace SSD_LED
{
    public partial class SSDLED : Form
    {
        private readonly NotifyIcon notifyIcon = new NotifyIcon();
        ManagementClass driveDataClass = new ManagementClass("Win32_PerfFormattedData_PerfDisk_PhysicalDisk");
        Icon iconGreen;
        Icon iconRed;
        Icon iconBlack;

        PerformanceCounter hddIdleCnt = new PerformanceCounter("PhysicalDisk", "% Idle Time", "_Total");
        PerformanceCounter hddReadCnt = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
        PerformanceCounter hddWriteCnt = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
        private PerformanceCounter _diskReadCounter = new PerformanceCounter();
        private PerformanceCounter _diskWriteCounter = new PerformanceCounter();


        public SSDLED()
        {
            InitializeComponent();

            //create symbol in system tray
            //CreateIcon(0);

            iconGreen = CreateIcon(Color.Green);
            iconRed = CreateIcon(Color.Red);
            iconBlack = CreateIcon(Color.Black);

            notifyIcon.Icon = iconBlack;
            notifyIcon.Visible = true;
            notifyIcon.Text = "Initializing...";

            //create menu items
            MenuItem info = new MenuItem("SSD-LED by SIRprise");
            MenuItem info2 = new MenuItem("-------------------");
            //MenuItem hdd1 = new MenuItem("Watch C:\\");
            MenuItem quit = new MenuItem("Exit");
            ContextMenu contextMenu = new ContextMenu();

            //add items to menu
            contextMenu.MenuItems.Add(info);
            contextMenu.MenuItems.Add(info2);
            //contextMenu.MenuItems.Add(hdd1);
            contextMenu.MenuItems.Add(quit);

            //add menu to symbol
            notifyIcon.ContextMenu = contextMenu;

            //link click events
            quit.Click += exit_Click;
            //hdd1.Click += hdd1_Click;

            //hide form
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;

            timer1.Enabled = true;
        }

        private Icon CreateIcon(Color color)
        {
            Icon icon = null;

            try
            {
                //create the icon to be written on
                Bitmap bitMapImage = new Bitmap(50, 50);
                Graphics graphicImage = Graphics.FromImage(bitMapImage);

                graphicImage.FillEllipse(new SolidBrush(color), new Rectangle(0, 0, 50, 50));


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


        void hdd1_Click(object sender, EventArgs e)
        {
            timer1.Enabled = true;
        }

        void exit_Click(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            if (driveDataClass != null)
            {
                driveDataClass.Dispose();
            }
            Close();
        }

        public void SSDActivityPerfCount()
        {
            float bytesPSRead = GetCounterValue(_diskReadCounter, "PhysicalDisk", "Disk Read Bytes/sec", "_Total");
            float bytesPSWrite = GetCounterValue(_diskWriteCounter, "PhysicalDisk", "Disk Write Bytes/sec", "_Total");
            float bytesPS = bytesPSRead + bytesPSWrite;
            //Debug.WriteLine(bytesPS);
            //float bytesPS = hddReadCnt.NextValue();
            /*
            bytesPS /= (1024*10);
            bytesPS = bytesPS > 255 ? 255 : bytesPS;
            notifyIcon.Icon = CreateIcon(Color.FromArgb(0, (int)bytesPS, 0));
             */
            notifyIcon.Text = Math.Round(bytesPSRead / 1024, 2).ToString() + " KB/s read / " + Math.Round(bytesPSWrite / 1024, 2).ToString() + " KB/s write";
            bytesPSRead /= (1024*10);
            bytesPSRead = bytesPSRead > 255 ? 255 : bytesPSRead;
            bytesPSWrite /= (1024 * 10);
            bytesPSWrite = bytesPSWrite > 255 ? 255 : bytesPSWrite;
            notifyIcon.Icon = CreateIcon(Color.FromArgb((int)bytesPSWrite, (int)bytesPSRead, 0));
        }

        float GetCounterValue(PerformanceCounter pc, string categoryName, string counterName, string instanceName)
        {
            pc.CategoryName = categoryName;
            pc.CounterName = counterName;
            pc.InstanceName = instanceName;
            return pc.NextValue();
        }

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

        private void timer1_Tick(object sender, EventArgs e)
        {
            //SSDActivityWMI();
            SSDActivityPerfCount();
        }
    }
}
