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

        //values normalized 0..100
        private void paintBars(Graphics g, int width, int height, int numberOfBars, int[] values)
        {
            Color[] barColors = new Color[values.Length];
            Random rnd = new Random();
            for (int i = 0; i < barColors.Length; i++)
            {
                barColors[i] = Color.FromArgb(rnd.Next(256), rnd.Next(256), rnd.Next(256));
            }

            //paint bars
            for (int i = 0; i < numberOfBars; i++)
            {
                g.FillRectangle(new SolidBrush(barColors[i]), (width / numberOfBars) * i, y: height - (int)(height / 100f * values[i]), (width / numberOfBars) - 1, height);
            }
        }

        //value normalized 0..100
        private void paintGauge(Graphics g, int width, int height, int in_value)
        {
            Color colorScale = Color.White;
            Color colorNeedle = Color.White;
            const int minValue = 0;
            const int maxValue = 100;
            const int needleThickness = 2;
            int needleRadius = width / 2;
            Point needleCenterPoint = new Point(width / 2, height);
            const int scaleAngleOffset = 180;
            const int scaleAngleRange = 180;
            const int scaleInterception = 10;
            const string text = "R";
            const int fontSize = 16;

            //paint background / scala
            g.FillEllipse(new SolidBrush(Color.Red), new Rectangle(0, height - needleRadius, width, height));
            g.DrawArc(new Pen(Color.White, needleThickness), new Rectangle(0, height - needleRadius, width, height), scaleAngleOffset, scaleAngleRange);
            Point startPoint, endPoint;

            for (int i = 1; i < scaleInterception; i++)
            {
                int angleInterceptionDeg = scaleAngleOffset + (scaleAngleRange / scaleInterception) * i;
                double angleInterceptionRad = angleInterceptionDeg * Math.PI / 180;
                startPoint = new Point((int)(needleCenterPoint.X + (0.9 * needleRadius) * Math.Cos(angleInterceptionRad)),
                                         (int)(needleCenterPoint.Y + (0.9 * needleRadius) * Math.Sin(angleInterceptionRad)));
                endPoint = new Point((int)(needleCenterPoint.X + (1.1 * needleRadius) * Math.Cos(angleInterceptionRad)),
                                         (int)(needleCenterPoint.Y + (1.1 * needleRadius) * Math.Sin(angleInterceptionRad)));

                g.DrawLine(new Pen(colorScale, needleThickness), startPoint.X, startPoint.Y, endPoint.X, endPoint.Y);
            }

            //paint string in the middle
            Font drawFont = new Font("Arial", fontSize, FontStyle.Bold);

            float size_in_pixels = drawFont.SizeInPoints / 72 * 72;//* g.DpiX;
            SolidBrush drawBrush = new SolidBrush(colorScale);

            StringFormat drawFormat = new StringFormat();
            drawFormat.Alignment = StringAlignment.Center;

            //g.DrawString(text, drawFont, drawBrush, needleCenterPoint.X, needleCenterPoint.Y - (int)(needleRadius * 0.3), drawFormat);

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddString(text, this.Font.FontFamily,
                    (int)drawFont.Style, (int)(size_in_pixels / 100 * (needleRadius * 5)), new Point(needleCenterPoint.X, needleCenterPoint.Y - (int)(needleRadius * 0.8)),
                    drawFormat);
                //g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(drawBrush, path);
            }

            //paint pointer

            //limit-check
            int value = Math.Max(minValue, in_value);
            value = Math.Min(maxValue, in_value);

            int valueRange = maxValue - minValue;
            int needleAngleDeg = (scaleAngleOffset + (value - minValue) * scaleAngleRange / valueRange);
            double needleAngleRad = needleAngleDeg * Math.PI / 180;

            endPoint = new Point((int)(needleCenterPoint.X + needleRadius * Math.Cos(needleAngleRad)),
                                     (int)(needleCenterPoint.Y + needleRadius * Math.Sin(needleAngleRad)));

            g.DrawLine(new Pen(colorNeedle, needleThickness), needleCenterPoint.X, needleCenterPoint.Y, endPoint.X, endPoint.Y);

            //fill pointer area
            g.FillPie(drawBrush, new Rectangle(0, height - needleRadius, width, height), scaleAngleOffset, needleAngleDeg - scaleAngleOffset);
            //g.DrawLine(new Pen(colorScale, needleThickness), 0, height - needleRadius, width, height);
        }
    }
}
