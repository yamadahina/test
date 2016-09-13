//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

// This module contains code to do Kinect NUI initialization,
// processing, displaying players on screen, and sending updated player
// positions to the game portion for hit testing.

//yamadaaaaaaaaaaaaaaaa

namespace ShapeGame
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Media;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Threading;
    using Microsoft.Kinect;
    using Microsoft.Kinect.Toolkit;
    using Microsoft.Samples.Kinect.WpfViewers;




    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {
        public static readonly DependencyProperty KinectSensorManagerProperty =
            DependencyProperty.Register(
                "KinectSensorManager",
                typeof(KinectSensorManager),
                typeof(MainWindow),
                new PropertyMetadata(null));

        #region Private State
        private const int TimerResolution = 2;  // ms
        private const int NumIntraFrames = 3;
        private const int MaxShapes = 80;

        private int a = 0;                      //手が和太鼓に入ると"1"
        private int b = 0;                      //　　ドラムに入ると"1"
        private int kaeri = 0;                  //条件文に当てはまると"1"  
        //                                                                                             _____________
        private double[] Taiko1_X = new double[] { -0.9, -0.2 };    //  Taiko1 : 和太鼓   (-0.9,-0.10) |            | (-0.2,-0.10)
        private double[] Taiko1_Y = new double[] { -0.10, -0.86 };  //　                  (-0.9,-0.86) |____________| (-0.2,-0.86)
        //                                                                                              _____________
        private double[] Taiko2_X = new double[] { 0.016, 0.70 };   //  Taiko2 : ドラム   (0.016,-0.10) |            | (0.70,-0.10)
        private double[] Taiko2_Y = new double[] { -0.10, -0.86 };  //　                  (0.016,-0.86) |____________| (0.70,-0.86)

        private double[] HandX;                 //　[1]:HandRight(右)のX座標  [2]:HandLeft(左)のX座標
        private double[] HandY;                 //  [1]:HandRight(右)のY座標  [2]:HandLeft(左)のY座標

        private const double MaxFramerate = 70;
        private const double MinFramerate = 15;
        private const double MinShapeSize = 12;
        private const double MaxShapeSize = 90;
        private const double DefaultDropRate = 2.5;
        private const double DefaultDropSize = 32.0;
        private const double DefaultDropGravity = 1.0;

        private readonly Dictionary<int, Player> players = new Dictionary<int, Player>();
        
        private readonly SoundPlayer taikoSound = new SoundPlayer();　　　//taikoSoundの定義
        private readonly SoundPlayer drumSound = new SoundPlayer();       //drumSoundの定義

        private readonly KinectSensorChooser sensorChooser = new KinectSensorChooser();
        
        private DateTime lastFrameDrawn = DateTime.MinValue;
        private DateTime predNextFrame = DateTime.MinValue;
        private double actualFrameTime;

        private Skeleton[] skeletonData;

        private Rect playerBounds;
        private Rect screenRect;

        private double targetFramerate = MaxFramerate;
        private int frameCount;
        private bool runningGameThread;
        private int playersAlive;



        #endregion Private State

        #region ctor + Window Events

        public MainWindow()
        {
            this.KinectSensorManager = new KinectSensorManager();
            this.KinectSensorManager.KinectSensorChanged += this.KinectSensorChanged;
            this.DataContext = this.KinectSensorManager;

            InitializeComponent();

            this.SensorChooserUI.KinectSensorChooser = sensorChooser;
            sensorChooser.Start();

            var kinectSensorBinding = new Binding("Kinect") { Source = this.sensorChooser };
            BindingOperations.SetBinding(this.KinectSensorManager, KinectSensorManager.KinectSensorProperty, kinectSensorBinding);
    
            this.RestoreWindowState();

        }

        public KinectSensorManager KinectSensorManager
        {
            get { return (KinectSensorManager)GetValue(KinectSensorManagerProperty); }
            set { SetValue(KinectSensorManagerProperty, value); }
        }

        
        public int hikakutaiko1  //和太鼓と手の座標を比較する関数
        {
            get { 
            for(int i=0;i<2;i++){
                    /* HandRightとHandLeftのそれぞれが和太鼓の座標と重なったとき、1を返す */
                    if ( HandX[i] >= Taiko1_X[0] && HandX[i] <= Taiko1_X[1] && HandY[i] <= Taiko1_Y[0] && HandY[i] >= Taiko1_Y[1] ) return 1;
            }
                return 0;
            }

        }

        public int hikakutaiko2　//和太鼓と手の座標を比較する関数
        {
            get
            {
                for (int i = 0; i < 2; i++)
                {
                    /* HandRightとHandLeftのどちらが和太鼓の座標と異なるとき、1を返す */
                    if ( HandX[i] < Taiko1_X[0] || HandX[i] > Taiko1_X[1] || HandY[i] >= Taiko1_Y[0] || HandY[i] <= Taiko1_Y[1]) return 1;
                }
                return 0;
            }

        }

        public int hikakudrum1　//ドラムと手の座標を比較する関数
        {
            get
            {
                for (int i = 0; i < 2; i++)
                {
                    /* HandRightとHandLeftのそれぞれがドラムの座標と重なったとき、1を返す */
                    if ( HandX[i] >= Taiko2_X[0] && HandX[i] <= Taiko2_X[1] && HandY[i] <= Taiko2_Y[0] && HandY[i] >= Taiko2_Y[1]) return 1;
                }
                return 0;
            }

        }

        public int hikakudrum2　//ドラムと手の座標を比較する関数
        {
            get
            {
                for (int i = 0; i < 2; i++)
                {
                    /* HandRightとHandLeftのどちらがドラムの座標と異なるとき、1を返す */
                    if ( HandX[i] < Taiko2_X[0] || HandX[i] > Taiko2_X[1] || HandY[i] >= Taiko2_Y[0] || HandY[i] <= Taiko2_Y[1]) return 1;
                }
                return 0;
            }

        }

        // Since the timer resolution defaults to about 10ms precisely, we need to
        // increase the resolution to get framerates above between 50fps with any
        // consistency.
        [DllImport("Winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern int TimeBeginPeriod(uint period);

        private void RestoreWindowState()
        {
            // Restore window state to that last used
            Rect bounds = Properties.Settings.Default.PrevWinPosition;
            if (bounds.Right != bounds.Left)
            {
                this.Top = bounds.Top;
                this.Left = bounds.Left;
                this.Height = bounds.Height;
                this.Width = bounds.Width;
            }

            this.WindowState = (WindowState)Properties.Settings.Default.WindowState;
        }

        private void WindowLoaded(object sender, EventArgs e)
        {
            playfield.ClipToBounds = true;

            this.UpdatePlayfieldSize();
            
            this.taikoSound.Stream = Properties.Resources.Taiko_11;　　//VSのリソースに組み込んだwavファイルから参照する
            this.drumSound.Stream = Properties.Resources.Drum_8;       //VSのリソースに組み込んだwavファイルから参照する



            TimeBeginPeriod(TimerResolution);
            var myGameThread = new Thread(this.GameThread);
            myGameThread.SetApartmentState(ApartmentState.STA);
            myGameThread.Start();

        }

        private void WindowClosing(object sender, CancelEventArgs e)
        {
            sensorChooser.Stop();

            this.runningGameThread = false;
            Properties.Settings.Default.PrevWinPosition = this.RestoreBounds;
            Properties.Settings.Default.WindowState = (int)this.WindowState;
            Properties.Settings.Default.Save();
        }

        private void WindowClosed(object sender, EventArgs e)
        {
            this.KinectSensorManager.KinectSensor = null;
        }

        #endregion ctor + Window Events

        #region Kinect discovery + setup

        private void KinectSensorChanged(object sender, KinectSensorManagerEventArgs<KinectSensor> args)
        {
            if (null != args.OldValue)
            {
                this.UninitializeKinectServices(args.OldValue);
            }

            // Only enable this checkbox if we have a sensor
            enableAec.IsEnabled = null != args.NewValue;

            if (null != args.NewValue)
            {
                this.InitializeKinectServices(this.KinectSensorManager, args.NewValue);
            }
        }

        // Kinect enabled apps should customize which Kinect services it initializes here.
        private void InitializeKinectServices(KinectSensorManager kinectSensorManager, KinectSensor sensor)
        {
            // Application should enable all streams first.
            kinectSensorManager.ColorFormat = ColorImageFormat.RgbResolution640x480Fps30;
            kinectSensorManager.ColorStreamEnabled = true;

            sensor.SkeletonFrameReady += this.SkeletonsReady;
            kinectSensorManager.TransformSmoothParameters = new TransformSmoothParameters
            {
                Smoothing = 0.5f,
                Correction = 0.5f,
                Prediction = 0.5f,
                JitterRadius = 0.05f,
                MaxDeviationRadius = 0.04f
            };
            kinectSensorManager.SkeletonStreamEnabled = true;
            kinectSensorManager.KinectSensorEnabled = true;

            if (!kinectSensorManager.KinectSensorAppConflict)
            {
                // Start speech recognizer after KinectSensor started successfully.
                enableAec.Visibility = Visibility.Visible;
            }
        }

        // Kinect enabled apps should uninitialize all Kinect services that were initialized in InitializeKinectServices() here.
        private void UninitializeKinectServices(KinectSensor sensor)
        {
            sensor.SkeletonFrameReady -= this.SkeletonsReady;

            enableAec.Visibility = Visibility.Collapsed;
        }

        #endregion Kinect discovery + setup

        #region Kinect Skeleton processing
        private void SkeletonsReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    int skeletonSlot = 0;

                    if ((this.skeletonData == null) || (this.skeletonData.Length != skeletonFrame.SkeletonArrayLength))
                    {
                        this.skeletonData = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    }

                    skeletonFrame.CopySkeletonDataTo(this.skeletonData);

                    foreach (Skeleton skeleton in this.skeletonData)
                    {
                        // トラッキング
                        if (SkeletonTrackingState.Tracked == skeleton.TrackingState)
                        {
                            Player player;
                            if (this.players.ContainsKey(skeletonSlot))
                            {
                                player = this.players[skeletonSlot];
                            }
                            else
                            {
                                player = new Player(skeletonSlot);
                                player.SetBounds(this.playerBounds);
                                this.players.Add(skeletonSlot, player);
                            }

                            player.LastUpdated = DateTime.Now;

                            // Update player's bone and joint positions
                            if (skeleton.Joints.Count > 0)
                            {
                                player.IsAlive = true;

                                // Head, hands, feet (hit testing happens in order here)
                                player.UpdateJointPosition(skeleton.Joints, JointType.Head);
                                player.UpdateJointPosition(skeleton.Joints, JointType.HandLeft);
                                player.UpdateJointPosition(skeleton.Joints, JointType.HandRight);
                                player.UpdateJointPosition(skeleton.Joints, JointType.FootLeft);
                                player.UpdateJointPosition(skeleton.Joints, JointType.FootRight);

                                // Hands and arms
                                player.UpdateBonePosition(skeleton.Joints, JointType.HandRight, JointType.WristRight);
                                player.UpdateBonePosition(skeleton.Joints, JointType.WristRight, JointType.ElbowRight);
                                player.UpdateBonePosition(skeleton.Joints, JointType.ElbowRight, JointType.ShoulderRight);

                                player.UpdateBonePosition(skeleton.Joints, JointType.HandLeft, JointType.WristLeft);
                                player.UpdateBonePosition(skeleton.Joints, JointType.WristLeft, JointType.ElbowLeft);
                                player.UpdateBonePosition(skeleton.Joints, JointType.ElbowLeft, JointType.ShoulderLeft);

                                // Head and Shoulders
                                player.UpdateBonePosition(skeleton.Joints, JointType.ShoulderCenter, JointType.Head);
                                player.UpdateBonePosition(skeleton.Joints, JointType.ShoulderLeft, JointType.ShoulderCenter);
                                player.UpdateBonePosition(skeleton.Joints, JointType.ShoulderCenter, JointType.ShoulderRight);

                                // Legs
                                player.UpdateBonePosition(skeleton.Joints, JointType.HipLeft, JointType.KneeLeft);
                                player.UpdateBonePosition(skeleton.Joints, JointType.KneeLeft, JointType.AnkleLeft);
                                player.UpdateBonePosition(skeleton.Joints, JointType.AnkleLeft, JointType.FootLeft);

                                player.UpdateBonePosition(skeleton.Joints, JointType.HipRight, JointType.KneeRight);
                                player.UpdateBonePosition(skeleton.Joints, JointType.KneeRight, JointType.AnkleRight);
                                player.UpdateBonePosition(skeleton.Joints, JointType.AnkleRight, JointType.FootRight);

                                player.UpdateBonePosition(skeleton.Joints, JointType.HipLeft, JointType.HipCenter);
                                player.UpdateBonePosition(skeleton.Joints, JointType.HipCenter, JointType.HipRight);

                                // Spine
                                player.UpdateBonePosition(skeleton.Joints, JointType.HipCenter, JointType.ShoulderCenter);
                            }

                            
                            HandX = new double[] { skeleton.Joints[JointType.HandRight].Position.X, skeleton.Joints[JointType.HandLeft].Position.X };  //右手と左手のX座標を格納
                            HandY = new double[] { skeleton.Joints[JointType.HandRight].Position.Y, skeleton.Joints[JointType.HandLeft].Position.Y };　//右手と左手のY座標を格納
                            

                            /* -----大太鼓・始----- */
                            if ( (kaeri = hikakutaiko1) == 1 )
                            {
                                if (a == 0)
                                {
                                    a = 1;
                                    this.taikoSound.Play();
                                }
                            }
                            else if( (kaeri = hikakutaiko2) == 1 )
                            {
                                if (a == 1)
                                {
                                    a = 0;
                                }

                            }
                            /* -----大太鼓・終----- */


                            /* -----ドラム・始----- */
                            if ( (kaeri = hikakudrum1) == 1 )
                            {
                                if (b == 0)
                                {
                                    b = 1;
                                    this.drumSound.Play();
                                }
                            }
                            else if( (kaeri = hikakudrum2) == 1 )
                            {
                                if (b == 1)
                                {
                                    b = 0;
                                }

                            }
                            /* -----ドラム・終----- */


                            skeletonSlot++;
                        }
                    }
                }
            }



        }

        private void CheckPlayers()
        {
            foreach (var player in this.players)
            {
                if (!player.Value.IsAlive)
                {
                    // Player left scene since we aren't tracking it anymore, so remove from dictionary
                    this.players.Remove(player.Value.GetId());
                    break;
                }
            }

            // Count alive players
            int alive = this.players.Count(player => player.Value.IsAlive);

            if (alive != this.playersAlive)
            {
                this.playersAlive = alive;
            }
        }

        private void PlayfieldSizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.UpdatePlayfieldSize();
        }

        private void UpdatePlayfieldSize()
        {
            // Size of player wrt size of playfield, putting ourselves low on the screen.
            this.screenRect.X = 0;
            this.screenRect.Y = 0;
            this.screenRect.Width = this.playfield.ActualWidth;
            this.screenRect.Height = this.playfield.ActualHeight;

            this.playerBounds.X = 0;
            this.playerBounds.Width = this.playfield.ActualWidth;
            this.playerBounds.Y = this.playfield.ActualHeight * 0.2;
            this.playerBounds.Height = this.playfield.ActualHeight * 0.75;

            foreach (var player in this.players)
            {
                player.Value.SetBounds(this.playerBounds);
            }

            Rect fallingBounds = this.playerBounds;
            fallingBounds.Y = 0;
            fallingBounds.Height = playfield.ActualHeight;
        }
        #endregion Kinect Skeleton processing

        #region GameTimer/Thread
        private void GameThread()
        {
            this.runningGameThread = true;
            this.predNextFrame = DateTime.Now;
            this.actualFrameTime = 1000.0 / this.targetFramerate;

            // Try to dispatch at as constant of a framerate as possible by sleeping just enough since
            // the last time we dispatched.
            while (this.runningGameThread)
            {
                // Calculate average framerate.  
                DateTime now = DateTime.Now;
                if (this.lastFrameDrawn == DateTime.MinValue)
                {
                    this.lastFrameDrawn = now;
                }

                double ms = now.Subtract(this.lastFrameDrawn).TotalMilliseconds;
                this.actualFrameTime = (this.actualFrameTime * 0.95) + (0.05 * ms);
                this.lastFrameDrawn = now;

                // Adjust target framerate down if we're not achieving that rate
                this.frameCount++;
                if ((this.frameCount % 100 == 0) && (1000.0 / this.actualFrameTime < this.targetFramerate * 0.92))
                {
                    this.targetFramerate = Math.Max(MinFramerate, (this.targetFramerate + (1000.0 / this.actualFrameTime)) / 2);
                }

                if (now > this.predNextFrame)
                {
                    this.predNextFrame = now;
                }
                else
                {
                    double milliseconds = this.predNextFrame.Subtract(now).TotalMilliseconds;
                    if (milliseconds >= TimerResolution)
                    {
                        Thread.Sleep((int)(milliseconds + 0.5));
                    }
                }

                this.predNextFrame += TimeSpan.FromMilliseconds(1000.0 / this.targetFramerate);

                this.Dispatcher.Invoke(DispatcherPriority.Send, new Action<int>(this.HandleGameTimer), 0);
            }
        }

        private void HandleGameTimer(int param)
        {
            // Every so often, notify what our actual framerate is

            // Advance animations, and do hit testing.
            for (int i = 0; i < NumIntraFrames; ++i)
            {

                // Draw new Wpf scene by adding all objects to canvas
                playfield.Children.Clear();
                foreach (var player in this.players)
                {
                    player.Value.Draw(playfield.Children);
                }

                this.CheckPlayers();
            }
        }
        #endregion GameTimer/Thread

        #region Kinect Speech processing

        private void EnableAecChecked(object sender, RoutedEventArgs e)
        {
            var enableAecCheckBox = (CheckBox)sender;
        }


        #endregion Kinect Speech processing
    }
}
//C:\H28創造工学\ShapeGame\Properties\Resources.Designer.cs
