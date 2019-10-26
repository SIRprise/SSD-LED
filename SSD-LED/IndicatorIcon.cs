using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace SSD_LED
{
    class IndicatorIcon
    {
#warning TODO: different types of icons (LED, Gauge, Bargraph)
        static Icon trayIcon;


        static void ShowIcon(Icon icon)
        {
            DestroyLastTrayIconHandle();

            //...
        }

        static void DestroyLastTrayIconHandle()
        {

        }
    }
}
