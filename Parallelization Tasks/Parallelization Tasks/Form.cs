﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Parallelization_Tasks
{
    public partial class Form : System.Windows.Forms.Form
    {
        public Form() { // constructor
            InitializeComponent();
            pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
        }

        // горизонтальное и вертикальное разрешение для Bitmap
        private int pixelWidth = 10240, pixelHeight = 7680;
        private byte bytesPerPixel { get; } = 4; // 1 пиксельное значение в 4 байтах
        private byte redValue, greenValue, blueValue;


        private void buttonStart_Click(object sender, EventArgs e)
        {
            pictureBox.Image = null; labelInfo.Text = null;

            try
            {
                SetResolution();

                /// создать "пустой" (all-zeros) 32bpp Bitmap объект для вывода графики
                using (Bitmap bmpRGB = new Bitmap(pixelWidth, pixelHeight, PixelFormat.Format32bppArgb))
                {
                    /// создать Rectangle и заблокировать растровое изображение в системной памяти
                    Rectangle rect = new Rectangle(0, 0, pixelWidth, pixelHeight);
                    BitmapData bmpData = bmpRGB.LockBits(rect, ImageLockMode.WriteOnly, bmpRGB.PixelFormat);

                    // генерирует случайные значения для цветности RGB графики
                    Random rand = new Random();
                    redValue = (byte)rand.Next(0xFF);
                    greenValue = (byte)rand.Next(0xFF);
                    blueValue = (byte)rand.Next(0xFF);

                    #region RUNNING IN FLOWS
                    // запуск таймера
                    Stopwatch watch = Stopwatch.StartNew(); // применяется для операций отсчета времени

                    byte tb = (byte)trackBar.Value;
                    if (radioButtonMulti.Checked == true)
                    {
                        // данные для всего изображения находятся между 0 и pixelWidth / 2
                        Task first = Task.Run(() => generateGraphData(ref bmpData, tb, 0, pixelWidth / 8)); // range is specified only for +X
                        Task second = Task.Run(() => generateGraphData(ref bmpData, tb, pixelWidth / 8, pixelWidth / 4));
                        Task third = Task.Run(() => generateGraphData(ref bmpData, tb, pixelWidth / 4, pixelWidth * 3 / 8));
                        Task fourth = Task.Run(() => generateGraphData(ref bmpData, tb, pixelWidth * 3 / 8, pixelWidth / 2));
                        Task.WaitAll(first, second, third, fourth); // task synchronization
                    }
                    if (radioButtonSingle.Checked == true)
                    {
                        generateGraphData(ref bmpData, tb, 0, pixelWidth / 2);
                    }

                    // отображение времени, затраченного на создание данных
                    duration.Text = $"Time (ms): {watch.ElapsedMilliseconds}";
                    #endregion

                    #region displaying
                    if (bmpData != null)
                        // разблокировка растрового изображения из системной памяти
                        bmpRGB.UnlockBits(bmpData);

                    // вывести графику в pictureBox
                    var memoryStream = new MemoryStream();
                    bmpRGB.Save(memoryStream, ImageFormat.Bmp);
                    pictureBox.Image = Image.FromStream(memoryStream);

                    memoryStream.Dispose();
                    #endregion
                }
            }
            catch (Exception ex) { labelInfo.Text = ex.Message; }
        }

        // генерирует данные для графики
        private void generateGraphData(ref BitmapData bmpData, byte tb, int partitionStart, int partitionEnd)
        {
            int Y = pixelHeight / 2, X = pixelWidth / 2;

            for (int x = partitionStart; x < partitionEnd; x++) // iterations = X
            {
                double p = Math.Sqrt(X * X - x * x);

                for (var i = p; i > -p; i -= 5) // value X for -Y and Y
                {
                    double r = Math.Sqrt(x * x + i * i) / X;
                    double q = (r - 1) * Math.Sin(tb * r);
                    double y = i / 3 + (q * Y);

                    #region plotXY
                    // изображение зеркально относительно оси X поэтому метод plotXY вызывается дважды
                    plotXY(ref bmpData, -x + (pixelWidth / 2), (int)(y + (pixelHeight / 2)));
                    plotXY(ref bmpData, x + (pixelWidth / 2), (int)(y + (pixelHeight / 2)));
                    #endregion
                }
            }
        }

        // устанавливает данные байтов для каждой точки,
        // значения байтов соответствуют X и Y координатам
        private void plotXY(ref BitmapData bmpData, int x, int y)
        {
            unsafe
            {
                // присвоить указателю адрес первых пиксельных данных
                byte* ptr = (byte*)bmpData.Scan0;

                // построение пиксела
                int index = (y * pixelWidth + x ) * bytesPerPixel;
                // назначить компонент RGB-значений к указателю
                ptr[index] = blueValue;
                ptr[index + 1] = greenValue;
                ptr[index + 2] = redValue;
                ptr[index + 3] = 0xBF;
            }
        }
    }
}
