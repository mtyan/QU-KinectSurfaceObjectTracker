﻿using System;
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
using Emgu.CV;
using Emgu.CV.UI;
using Emgu.CV.Structure;
using Emgu.Util;


namespace QU_KSOT
{
    /// <summary>
    /// Queen's University - Kinect Surface Object Tracker
    /// Version 1.0.7
    /// 
    /// Framework is based on Basic Depth Viewer by Julia Scharz
    /// http://www.codingbeta.com/?p=90
    /// 
    /// CHANGE LOG:
    /// ------------------
    /// [1.0.7] Committed: 2011-05-17
    ///     - Reformat and clean-up GUI.
    ///     - Add information?
    ///     
    ///     TODO:
    ///         - Make dimmable work. => Done!
    ///         = Make sure settings are large enough, dim_step = 10ish, time_step = 2 to 5.
    ///         
    /// 
    /// [1.0.6] Committed: 2011-05-16
    ///     1. Blob tracking
    /// 
    /// [1.0.5] Committed: 2011-05-16
    ///     1. Background subtraction
    ///     2. ROI bounds defined by four points.
    ///         - Allows for perspective skewed ROI (square vs. quadrialteral)
    /// 
    /// [1.0.4] Committed: 2011-05-15
    /// 
    /// [1.0.3] Committed: 2011-05-14
    /// Getting user information on coordinates and depth working.
    ///     1. Global minimum and maximum depth.
    ///         - In progress
    ///     2. Depth at user-defined coordinates (without the mouse.)
    ///         - In progress
    /// 
    /// Coordinates: 0,0 @ top left, 640,480 @ bottom right.
    ///         
    /// [1.0.2] Committed: 2011-05-13
    /// Working on getting depth.
    ///     1. Getting an Emgu.Matrix setup for holding depth values.
    ///     2. Display data.
    ///     
    /// TODO:
    ///     - Check matrix coordinates match up with the display coordinates.
    ///     = (0,0) at top left
    /// 
    /// [1.0.1] Committed: 2011-05-13
    /// Getting the x & y coordinates read. No depth yet.
    /// - Two options for coordinate output, the more complicated one looks a lot better.
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

        private Matrix<Double> depthMatrix;                 // EMGU.Matrix of the depth image.
        private Matrix<Double> backgroundDepthMatrix;       // Is the background image.
        private Matrix<Double> depthMatrixROI;              // EMGU.Matrix of the ROI of the depth image.
        private Matrix<Byte> depthMatrixROIByte;
        private Image<Gray, Double> backgroundDepthImage;   // background image
        private Image<Gray, Double> depthMatrixImage;       // EMGU.Image of the depth image.
        private Image<Gray, Byte> depthMatrixROIImage;      //
        private double globalMaximumDepth = 0;
        private double globalMinimumDepth = 99999;
        private double globalROIMaximumDepth = 0;
        private double globalROIMinimumDepth = 99999;
        private bool backgroundImageExists = false;

        #region Line Definitions
        private LineSegment2D[] filterBoundary = new LineSegment2D[4];
        private bool filterBoundariesExists = false;
        #endregion

        #region Blob Tracking

        private Emgu.CV.VideoSurveillance.BlobTrackerAutoParam<Gray> blobParam;
        private Emgu.CV.VideoSurveillance.BlobTrackerAuto<Gray> blobTracker;
        //Create font to be printed.
        private MCvFont font = new MCvFont(Emgu.CV.CvEnum.FONT.CV_FONT_HERSHEY_SIMPLEX, 0.5, 0.5);
        //Colours
        private Bgr[] targetColour = {new Bgr(255,0,0),new Bgr(0,255,0),new Bgr(0,0,255), new Bgr(255,255,0), new Bgr(255,0,255), new Bgr(0,255,255)};
        //Image
        private Image<Bgr, Byte> trackingImage;
        //Dimming Limit
        private int _tickDuration = 5;      //in milliseconds
        private int _dimCounter = 0;
        private bool _dimming = false;

        #endregion BlobTracking
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
            //Reset Global Depth Values
            globalMaximumDepth = 0;
            globalMinimumDepth = 99999;
            globalROIMaximumDepth = 0;
            globalROIMinimumDepth = 99999;

            // Get information about the depth image
            DepthMetaData depthMD = new DepthMetaData();

            // Lock the bitmap we will be copying to just in case. This will also give us a pointer to the bitmap.
            System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, this.bitmap.Width, this.bitmap.Height);
            BitmapData data = this.bitmap.LockBits(rect, ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            depth.GetMetaData(depthMD);

            // This will point to our depth image
            ushort* pDepth = (ushort*)this.depth.GetDepthMapPtr().ToPointer();
            System.Drawing.Point point;

            // Go over the depth image and set the bitmap we're copying to based on our depth value.
            for (int y = 0; y < depthMD.YRes; ++y)
            {
                byte* pDest = (byte*)data.Scan0.ToPointer() + y * data.Stride;
                for (int x = 0; x < depthMD.XRes; ++x, ++pDepth, pDest += 3)
                {
                    // Change the color of the bitmap based on the depth value. You can make this
                    // whatever you want, my particular version is not that pretty.
                    pDest[0] = (byte)(*pDepth >> 1);
                    pDest[1] = (byte)(*pDepth >> 0);
                    pDest[2] = (byte)(*pDepth >> 2);

                    // Write vales to depth matrix. 
                    // TODO: Check coordinate system. top/left = origin
                    depthMatrix[y, x] = *pDepth;

                    //Using line filters:
                    if (filterBoundariesExists)
                    {
                        point = new System.Drawing.Point(x,y);      //declare current point

                        int temp0 = filterBoundary[0].Side(point);
                        int temp1 = filterBoundary[1].Side(point);
                        int temp2 = filterBoundary[2].Side(point);
                        int temp3 = filterBoundary[3].Side(point);

                        //if((filterBoundary[0].Side(point)<0)||(filterBoundary[1].Side(point)>0)||(filterBoundary[2].Side(point)>0)||(filterBoundary[3].Side(point)<0))
                        if (!((temp0 > 0)&&(temp2 < 0)&&(temp1 > 0)&&(temp3 < 0)))
                        {
                            //Outside bounds, set to zero
                            depthMatrixROI[y,x] = 0;
                        }
                        else
                        {
                            //Inside the bounds of the table
                            if (depthMatrix[y,x] != 0)
                            {
                                if (backgroundImageExists)
                                {
                                    //Background subtraction (scaled)
                                    depthMatrixROI[y, x] = Math.Pow((backgroundDepthMatrix[y,x] - depthMatrix[y, x]),slider1.Value);
                                }
                                else
                                {
                                    depthMatrixROI[y, x] = depthMatrix[y, x];
                                }

                                // ROI Min & Max
                                if ((depthMatrixROI[y, x] < globalROIMinimumDepth) && (depthMatrixROI[y, x] > 0)) globalROIMinimumDepth = depthMatrixROI[y, x];
                                if (depthMatrixROI[y, x] > globalROIMaximumDepth) globalROIMaximumDepth = depthMatrixROI[y, x];
                            }
                            else
                            {
                                depthMatrixROI[y,x] = 1;
                            }
                        }
                    }

                    //Global Min & Max
                    if ((depthMatrix[y, x] < globalMinimumDepth)&&(depthMatrix[y,x]>0)) globalMinimumDepth = depthMatrix[y, x];
                    if (depthMatrix[y, x] > globalMaximumDepth) globalMaximumDepth = depthMatrix[y, x];

                }//end for - x
            }//end for - y

            this.bitmap.UnlockBits(data);

            // Update the image to have the bitmap image we just copied
            image1.Source = getBitmapImage(bitmap);

            // Update display variables for Global Depth values
            DisplayGlobalMaximumDepth = globalMaximumDepth;
            DisplayGlobalMinimumDepth = globalMinimumDepth;

            //if (backgroundImageExists)
            //{
            //    //Matrix<Double> temp = new Matrix<Double>(480, 640);
            //    //temp = backgroundDepthMatrix - depthMatrix;
            //    //temp *= 10000;
            //    //temp.CopyTo(depthMatrixImage);

            //    Image<Gray, Double> temp = new Image<Gray, Double>(640, 480);
            //    depthMatrix.CopyTo(depthMatrixImage);
            //    temp = backgroundDepthImage - depthMatrixImage;

            //    //temp = temp.Pow(0.8);

            //    image2.Source = getBitmapImage(temp.Bitmap);
                
            //}
            if (filterBoundariesExists)
            {
                //Calculate the slope.
                double m = ((255 - 5) / (globalROIMaximumDepth - globalROIMinimumDepth));

                //Scale use depthMatrixROI to depthMatrixROIByte
                for (int y = 0; y < depthMD.YRes; ++y)
                {
                    for (int x = 0; x < depthMD.XRes; ++x)
                    {
                        if (depthMatrixROI[y, x] < 2)  //Then this is outside the region.
                        {
                            depthMatrixROIByte[y, x] = 0;
                        }
                        else
                        {
                            depthMatrixROIByte[y, x] = (byte)((m * (depthMatrixROI[y, x] - globalROIMinimumDepth)) + 255);
                        }
                    }
                }

                depthMatrixROIByte.CopyTo(depthMatrixROIImage);
                
                image2.Source = getBitmapImage(depthMatrixROIImage.Bitmap);


                #region Blob Tracking Algorithm

                //Binary Thresholded image, reduces noise even more.
                depthMatrixROIImage = depthMatrixROIImage.ThresholdBinary(new Gray(slider2.Value), new Gray(255));
                //using a foreground mask.
                blobTracker.Process(depthMatrixROIImage, depthMatrixROIImage);

                //blobTracker.Process(depthMatrixROIImage);

                //Image<Gray, Byte> tempImage = new Image<Gray, Byte>(640, 480, new Gray(0));
                //Image<Gray, Byte> tempImage = blobTracker.ForgroundMask;

                //blobTracker.Process(depthMatrixROIImage, tempImage);

                // Dimming
                if (_dimming)
                {
                    if (_dimCounter > progressBar1.Maximum)
                    {
                        Image<Bgr,Byte> temp = trackingImage - new Image<Bgr, Byte>(640, 480, new Bgr(slider_DimStep.Value, slider_DimStep.Value, slider_DimStep.Value));
                        //temp.CopyTo(trackingImage);
                        trackingImage = temp.Clone();
                        _dimCounter = 0;
                    }
                    else
                    {
                        _dimCounter++;
                    }

                    progressBar1.Value = _dimCounter;
                }

                // Tag each blob
                foreach (MCvBlob blob in blobTracker)
                {
                    try
                    {
                        // Draw Rectangle
                        //tempImage.Draw(System.Drawing.Rectangle.Round(blob), new Gray(250), 2);
                        trackingImage.Draw(System.Drawing.Rectangle.Round(blob), targetColour[blob.ID], 2);

                        // Draw Text
                        //tempImage.Draw(blob.ID.ToString(), ref font, System.Drawing.Point.Round(blob.Center), new Gray(200));
                    }
                    catch (IndexOutOfRangeException ex_Index)
                    {
                        MessageBox.Show(ex_Index.Message);
                        MessageBox.Show("Tracking limit reached, application terminating...");
                        this.Close();
                    }
                }

                //Set image3 to the blob tracking.
                //image2.Source = getBitmapImage(depthMatrixROIImage.Bitmap);
                image3.Source = getBitmapImage(trackingImage.Bitmap);

                #endregion
            }//end if(filterBoundariesExists)
            else
            {
                // Create image
                depthMatrix.CopyTo(depthMatrixImage);
                image2.Source = getBitmapImage(depthMatrixImage.Bitmap);  
            }    
        }//end UpdateDepth()
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
                
                // Initialize Matrixes
                this.depthMatrix = new Matrix<Double>(480, 640);
                this.depthMatrixROI = new Matrix<Double>(480, 640);
                this.depthMatrixROIByte = new Matrix<Byte>(480, 640);
                this.backgroundDepthMatrix = new Matrix<Double>(480, 640);
                // Initialize Images
                this.depthMatrixImage = new Image<Gray,double>(640,480);
                this.depthMatrixROIImage = new Image<Gray, byte>(640, 480);
                this.backgroundDepthImage = new Image<Gray, double>(640, 480);
                this.trackingImage = new Image<Bgr, byte>(640, 480, new Bgr(0, 0, 0));

                // Initialize Blob Tracking Components
                    // Blob Tracking Parameter
                blobParam = new Emgu.CV.VideoSurveillance.BlobTrackerAutoParam<Gray>();
                blobParam.BlobTracker = new Emgu.CV.VideoSurveillance.BlobTracker(Emgu.CV.CvEnum.BLOBTRACKER_TYPE.CC);
                blobParam.BlobDetector = new Emgu.CV.VideoSurveillance.BlobDetector(Emgu.CV.CvEnum.BLOB_DETECTOR_TYPE.CC);
                //blobParam.FGDetector = new Emgu.CV.VideoSurveillance.FGDetector<Gray>(Emgu.CV.CvEnum.FORGROUND_DETECTOR_TYPE.FGD);
                //blobParam.FGTrainFrames = 10;
                    // Blob Tracking initialization
                blobTracker = new Emgu.CV.VideoSurveillance.BlobTrackerAuto<Gray>(blobParam);

                progressBar1.Minimum = 0;
                progressBar1.Maximum = ((Convert.ToInt16(textBoxLifespan.Text) * 4) / _tickDuration);
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
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, _tickDuration);
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

        #region ViewModelProperty: DepthClickPoint
        private double _depthClickPoint;
        public double DepthClickPoint
        {
            get
            {
                return _depthClickPoint;
            }

            set
            {
                _depthClickPoint = value;
                OnPropertyChanged("DepthClickPoint");
            }
        }
        #endregion test

        private void image1_MouseUp(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Point clickPoint = e.GetPosition(image1);
            HorizontalClickPoint = (double)Convert.ToInt16(clickPoint.X);
            VerticalClickPoint = clickPoint.Y;
            DepthClickPoint = depthMatrix[(int)VerticalClickPoint, (int)HorizontalClickPoint];
        }
        #endregion

        #region Global Depth Information
        #region ViewModelProperty: DisplayGlobalMaximumDepth
        private double _displayGlobalMaximumDepth;
        public double DisplayGlobalMaximumDepth
        {
            get
            {
                return _displayGlobalMaximumDepth;
            }

            set
            {
                _displayGlobalMaximumDepth = value;
                OnPropertyChanged("DisplayGlobalMaximumDepth");
            }
        }
        #endregion ViewModelProperty: DisplayGlobalMaximumDepth
        #region ViewModelProperty: DisplayGlobalMinimumDepth;
        private double _displayGlobalMinimumDepth;
        public double DisplayGlobalMinimumDepth
        {
            get
            {
                return _displayGlobalMinimumDepth;
            }

            set
            {
                _displayGlobalMinimumDepth = value;
                OnPropertyChanged("DisplayGlobalMinimumDepth");
            }
        }
        #endregion test

        private void coord_X_TextChanged(object sender, TextChangedEventArgs e)
        {
            //coord_D.Text = depthMatrix[Convert.ToInt16(coord_X.Text), Convert.ToInt16(coord_Y.Text)].ToString();
            try
            {
                if ((coord_X != null) && (coord_Y != null) && (coord_D != null))
                {
                    int temp = Convert.ToInt16(coord_X.Text);
                    temp++;
                    //coord_D.Text = temp.ToString();
                    coord_D.Text = depthMatrix[Convert.ToInt16(coord_Y.Text), Convert.ToInt16(coord_X.Text)].ToString();
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }
        }
        #endregion

        //When clicked, the current image becomes the background image.
        private void button1_Click(object sender, RoutedEventArgs e)
        {
            if (filterBoundariesExists)
            {
                backgroundImageExists = true;

                backgroundDepthMatrix = new Matrix<Double>(480, 640);
                depthMatrixROI.CopyTo(backgroundDepthMatrix);
                backgroundDepthMatrix.CopyTo(backgroundDepthImage);
                
                image3.Source = getBitmapImage(backgroundDepthImage.Bitmap);
            }
            else
            {
                MessageBox.Show("Please define ROI");
            }
        }

        #region ROI Definition
        private void button_UpdateFilter_Click(object sender, RoutedEventArgs e)
        {
            filterBoundariesExists = true;

            System.Drawing.Point P1 = new System.Drawing.Point(Convert.ToInt16(textBox_P1_x.Text), Convert.ToInt16(textBox_P1_y.Text));
            System.Drawing.Point P2 = new System.Drawing.Point(Convert.ToInt16(textBox_P2_x.Text), Convert.ToInt16(textBox_P2_y.Text));
            System.Drawing.Point P3 = new System.Drawing.Point(Convert.ToInt16(textBox_P3_x.Text), Convert.ToInt16(textBox_P3_y.Text));
            System.Drawing.Point P4 = new System.Drawing.Point(Convert.ToInt16(textBox_P4_x.Text), Convert.ToInt16(textBox_P4_y.Text));


            filterBoundary[0] = new LineSegment2D(P1, P2);
            filterBoundary[1] = new LineSegment2D(P2, P4);
            filterBoundary[2] = new LineSegment2D(P3, P4);
            filterBoundary[3] = new LineSegment2D(P1, P3);
        }

        #region Quick Update Boundary Points
        private void button_P1_Click(object sender, RoutedEventArgs e)
        {
            textBox_P1_x.Text = HorizontalClickPoint.ToString();
            textBox_P1_y.Text = VerticalClickPoint.ToString();
        }

        private void button_P2_Click(object sender, RoutedEventArgs e)
        {
            textBox_P2_x.Text = HorizontalClickPoint.ToString();
            textBox_P2_y.Text = VerticalClickPoint.ToString();
        }

        private void button_P3_Click(object sender, RoutedEventArgs e)
        {
            textBox_P3_x.Text = HorizontalClickPoint.ToString();
            textBox_P3_y.Text = VerticalClickPoint.ToString();
        }

        private void button_P4_Click(object sender, RoutedEventArgs e)
        {
            textBox_P4_x.Text = HorizontalClickPoint.ToString();
            textBox_P4_y.Text = VerticalClickPoint.ToString();
        }
        #endregion

        private void checkBox1_Checked(object sender, RoutedEventArgs e)
        {
            _dimming = true;
        }

        #endregion

        private void dimming_Unchecked(object sender, RoutedEventArgs e)
        {
            _dimming = false;
        }

        private void textBoxLifespan_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                progressBar1.Maximum = ((Convert.ToInt16(textBoxLifespan.Text) * 4) / _tickDuration);
            }
            catch (Exception ex1)
            {
                MessageBox.Show(ex1.Message);
            }
        }
    }
}
