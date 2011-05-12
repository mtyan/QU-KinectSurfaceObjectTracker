using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using xn;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Threading;
using System.IO;
using System.ComponentModel;


namespace QU_KSOT
{
    /// <summary>
    /// Queen's University - Kinect Surface Object Tracker
    /// Version 1.0.0
    /// 
    /// Framework is based on Basic Depth Viewer by Julia Scharz
    /// http://www.codingbeta.com/?p=90
    /// 
    /// CHANGE LOG:
    /// ------------------
    /// 
    /// [1.0.0] Committed: 2011-05-11 
    /// Just getting the basic system for depth view up and running as per Basic Depth Viewer tutorial.
    /// 
    /// 
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Member Variables
        private Context context;                    // OpenNI operations
        private DepthGenerator depth;               // Generates depth image
        private Bitmap bitmap;                      // The converted image.
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        #region Kinect Functions - Depth
        /// <summary>
        /// This method updates the image on the MainWindow page with the latest depth image.
        /// </summary>
        private unsafe void UpdateDepth()
        {
            // Get information about the depth image
            DepthMetaData depthMD = new DepthMetaData();

            // Lock the bitmap we will be copying to just in case. This will also give us a pointer to the bitmap.
            System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, this.bitmap.Width, this.bitmap.Height);
            BitmapData data = this.bitmap.LockBits(rect, ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            depth.GetMetaData(depthMD);

            // This will point to our depth image
            ushort* pDepth = (ushort*)this.depth.GetDepthMapPtr().ToPointer();

            // Go over the depth image and set the bitmap we're copying to based on our depth value.
            for (int y = 0; y < depthMD.YRes; ++y)
            {
                byte* pDest = (byte*)data.Scan0.ToPointer() + y * data.Stride;
                for (int x = 0; x < depthMD.XRes; ++x, ++pDepth, pDest += 3)
                {
                    // Change the color of the bitmap based on the depth value. You can make this
                    // whatever you want, my particular version is not that pretty.
                    pDest[0] = (byte)(*pDepth >> 0);
                    pDest[1] = (byte)(*pDepth >> 5);
                    pDest[2] = (byte)(*pDepth >> 0);
                }
            }

            this.bitmap.UnlockBits(data);

            // Update the image to have the bitmap image we just copied
            image1.Source = getBitmapImage(bitmap);
        }
        #endregion 

        #region Display Depth Map
        /// <summary>
        /// This method gets executed when the window loads. In it, we initialize our connection with Kinect
        /// and set up the timer which will update our depth image.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initialize the context from the configuration file
                this.context = new Context(@"..\..\data\openniconfig.xml");
                // Get the depth generator from the config file.
                this.depth = context.FindExistingNode(NodeType.Depth) as DepthGenerator;
                if (this.depth == null)
                    throw new Exception(@"Error in Data\openniconfig.xml. No depth node found.");
                MapOutputMode mapMode = this.depth.GetMapOutputMode();
                this.bitmap = new Bitmap((int)mapMode.nXRes, (int)mapMode.nYRes, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error initializing OpenNI.");
                MessageBox.Show(ex.Message);
                this.Close();
            }

            // Set the timer to update teh depth image every 10 ms.
            DispatcherTimer dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 10);
            dispatcherTimer.Start();
            Console.WriteLine("Finished loading");
        }

        /// <summary>
        /// This method gets executed every time the timer ticks, which is every 10 ms.
        /// In it we update the depth image.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                this.context.WaitAndUpdateAll();
            }
            catch (Exception)
            {
            }
            UpdateDepth();
        }

        /// This method takes in a bitmap and returns a BitmapImage which can be used to set the image source
        /// of an Image object. It is an annoying but necessary method to correctly display the depth image.
        public static BitmapImage getBitmapImage(Bitmap bitmap)
        {
            MemoryStream ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            BitmapImage bi = new BitmapImage();
            bi.BeginInit();
            ms.Seek(0, SeekOrigin.Begin);
            bi.StreamSource = ms;
            bi.EndInit();
            return bi;
        }

        private void image1_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {

        }
        #endregion

        #region Mouse Click Location
        #region INotifiedProperty Block
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;

            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion test

        #region ViewModelProperty: HorizontalClickPoint
        private double _horizontalClickPoint;
        public double HorizontalClickPoint
        {
            get
            {
                return _horizontalClickPoint;
            }

            set
            {
                _horizontalClickPoint = value;
                OnPropertyChanged("HorizontalClickPoint");
            }
        }
        #endregion test

        #region ViewModelProperty: VerticalClickPoint
        private double _verticalClickPoint;
        public double VerticalClickPoint
        {
            get
            {
                return _verticalClickPoint;
            }

            set
            {
                _verticalClickPoint = value;
                OnPropertyChanged("VerticalClickPoint");
            }
        }
        #endregion test

        public Bitmap theBitmap
        {
            get
            {
                return bitmap;
            }
            set
            {
                bitmap = value;
                OnPropertyChanged("theBitmap");
            }
        }//end Bitmap theBitmap

        private void image1_MouseUp(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Point clickPoint = e.GetPosition(image1);
            HorizontalClickPoint = clickPoint.X;
            VerticalClickPoint = clickPoint.Y;

            coord_X.Text = clickPoint.X.ToString();
            coord_Y.Text = clickPoint.Y.ToString();
        }
        #endregion
    }
}
