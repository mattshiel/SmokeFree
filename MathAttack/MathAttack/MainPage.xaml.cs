﻿using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using MathAttack.Class;
using Windows.UI;
using Microsoft.Graphics.Canvas.Text;
using Windows.Storage;
using System.Numerics;
using Windows.Devices.Sensors;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace MathAttack

{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // Game level resources
        private CanvasBitmap BG, StartScreen, ScoreScreen, Level1, Blast, MinusMonster, PlusMonster, ENEMY_IMG, Weapon, Boom;

        // Boundaries of the application view
        public static Rect boundaries;

        // Width and Height of canvas and scale width and height
        public static float DesignWidth = 1920;
        public static float DesignHeight = 1080;
        public static float scaleWidth, scaleHeight, pointX, pointY;
        private float photonX;
        private float photonY;

        // Game Score
        public static float gameScore;

        // Explosions
        private float boomX, boomY;
        // Value for how long the explosion image stays after an enemy dies
        private int boomCount = 60; // Frames Per Second


        // Round Timer
        private DispatcherTimer RoundTimer = new DispatcherTimer();

        // Enemy Timer
        private DispatcherTimer EnemyTimer = new DispatcherTimer();


        // List of projectiles image positions
        private List<float> blastXPos = new List<float>();
        private List<float> blastYPos = new List<float>();
        private List<float> blastXPosStartPos = new List<float>();
        private List<float> blastYPosStartPos = new List<float>();
        private List<float> percent = new List<float>();

        // List for enemy image positions
        private List<float> enemyXpos = new List<float>();
        private List<float> enemyYpos = new List<float>();

        // List  for enemy type
        private List<int> enemyType = new List<int>();

        // List for enemy direction
        private List<String> enemyDir = new List<String>();


        // List for enemy start positions
        private Random EnemyXStart = new Random(); // Enemy Type
        private Random EnemyYStart = new Random(); // Enemy Type

        // Weapon Position
        public float WeaponPosX;
        public float WeaponPosY;

        // Inclinometer
        private Inclinometer inclinometer;
        float roll, pitch, yaw;

        // FONT
        public static CanvasTextFormat textFormat = new CanvasTextFormat()
        {
            FontSize = 60,
            WordWrapping = CanvasWordWrapping.NoWrap // Mostly never used but handy if paragraphs are used in the future
        };

        // Random Number Generators
        private Random EnemyTypeRand = new Random(); // Enemy Type
        private Random EnemyGenIntervalRand = new Random(); // Generation Interval

        // Level of the game
        private float GameState = 0;

        // Timer starting value
        private int countdown = 60;

        // Controls when a round is over
        private bool RoundEnded = false;

        public MainPage()
        {
            this.InitializeComponent();
            // Fires when the window has changed its rendering size
            // Set the scale on page load
            Scaling.SetScale();
            Window.Current.SizeChanged += Current_SizeChanged;


            photonX = (float)boundaries.Width / 2;
            photonY = (float)boundaries.Height - (140f * scaleHeight);

            // Round Timer
            RoundTimer.Tick += RoundTimer_Tick;
            RoundTimer.Interval = new TimeSpan(0, 0, 1);

            // Enemy Timer
            EnemyTimer.Tick += EnemyTimer_Tick;
            // Controls intervals that spawn enemies
            EnemyTimer.Interval = new TimeSpan(0, 0, 0, 0, EnemyGenIntervalRand.Next(300, 2000));

            // Weapon Positions
            WeaponPosX = (float)boundaries.Width / 2 - (50 * scaleWidth);
            WeaponPosY = (float)boundaries.Height - (150 * scaleHeight); 

            // To Implement the inclinometre I followed this guide from Microsoft https://docs.microsoft.com/en-us/windows/uwp/devices-sensors/use-the-inclinometer
            // Grab the default inclinometre
            inclinometer = Inclinometer.GetDefault();

            if(inclinometer != null)
            {
                // Establish the report interval for all scenarios
                uint minReportInterval = inclinometer.MinimumReportInterval;
                uint reportInterval = minReportInterval > 16 ? minReportInterval : 16;

                //Establish the event handler
                inclinometer.ReadingChanged += new TypedEventHandler<Inclinometer, InclinometerReadingChangedEventArgs>(ReadingChanged);
            }
        }

        // Uncomment for Inclinometre 
        // Create the change event
        private async void ReadingChanged(Inclinometer sender, InclinometerReadingChangedEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                InclinometerReading reading = e.Reading;

                roll = reading.RollDegrees;
                pitch = reading.PitchDegrees;
                yaw = reading.YawDegrees;

                // Move right
                if(pitch > 0 && WeaponPosX < 1100 * scaleWidth)
                {
                    WeaponPosX = WeaponPosX + pitch;
                }
                // Else move left
                else if (pitch < 0 && WeaponPosX > 100 * scaleWidth)
                {
                    WeaponPosX = WeaponPosX + pitch;
                }
                
            });
        }

        private void Current_SizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            boundaries = ApplicationView.GetForCurrentView().VisibleBounds;

            // Everytime the window size changes reset the scale
             Scaling.SetScale();

            // Adjust projectiles for scaling
            photonX = (float)boundaries.Width / 2;
            photonY = (float)boundaries.Height - (140f * scaleHeight);

            WeaponPosX = (float)boundaries.Width / 2 - (50 * scaleWidth);
            WeaponPosY = (float)boundaries.Height - (150 * scaleHeight);
        }

        // Adapted from https://microsoft.github.io/Win2D/html/T_Microsoft_Graphics_Canvas_UI_Xaml_CanvasControl.htm
        private void GameCanvas_CreateResources(Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
        {
            // Calls CreateResourcesAsync Task and ensures could will not execute until the task completes
            args.TrackAsyncAction(CreateResourcesAsync(sender).AsAsyncAction());
        }

        // Handles asynchronous loading of images
        async Task CreateResourcesAsync(CanvasControl sender)
        {
            // Loads the demo start screen
            StartScreen = await CanvasBitmap.LoadAsync(sender, new Uri("ms-appx:///Assets/Images/start-screen.jpg"));

            // Loads Level 1 screen
            Level1 = await CanvasBitmap.LoadAsync(sender, new Uri("ms-appx:///Assets/Images/Level1.jpg"));

            // Loads the score screen
            ScoreScreen = await CanvasBitmap.LoadAsync(sender, new Uri("ms-appx:///Assets/Images/score-screen.jpg"));

            // Loads a blast projectile
            Blast = await CanvasBitmap.LoadAsync(sender, new Uri("ms-appx:///Assets/Images/blast.png"));

            // Load the subtraction symbol monster
            MinusMonster = await CanvasBitmap.LoadAsync(sender, new Uri("ms-appx:///Assets/Images/minusmonster.png"));

            // Load the addition symbol monster
            PlusMonster = await CanvasBitmap.LoadAsync(sender, new Uri("ms-appx:///Assets/Images/plusmonster.png"));

            // Load the weapon image
            Weapon = await CanvasBitmap.LoadAsync(sender, new Uri("ms-appx:///Assets/Images/weapon.png"));

            // Load explosion
            Boom = await CanvasBitmap.LoadAsync(sender, new Uri("ms-appx:///Assets/Images/boom.png"));

        }

        // Handles all drawing of the game and its resources
        private void GameCanvas_Draw(Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasDrawEventArgs args)
        {
            // Load initial Game State
            GSM();

            // Draw the start screen
            args.DrawingSession.DrawImage(Scaling.ScaleImage(BG));
            args.DrawingSession.DrawText(countdown.ToString(), 100, 100, Colors.Yellow);

            // Check if the round has ended, otherwise contine to draw level
            if (RoundEnded == true)
            {

                // Set the parametres for the final score to be drawn
                // Use a textLayout as opposed to just hard coding it in
                // Used this as a reference to help with font scaling https://stackoverflow.com/questions/30696838/how-to-calculate-the-size-of-a-piece-of-text-in-win2d
                CanvasTextLayout textLayout1 = new CanvasTextLayout(args.DrawingSession, gameScore.ToString(), textFormat, 0.0f, 0.0f);
                args.DrawingSession.DrawTextLayout(textLayout1, ((DesignWidth * scaleWidth) / 2) - ((float)textLayout1.DrawBounds.Width / 2), 650 * scaleHeight, Colors.White);

            }
            else
            {
                // Only draw enemies, weapons and projectiles if the start screen has been passed
                if (GameState > 0)
                {

                    // Draw the weapon first
                    args.DrawingSession.DrawImage(Scaling.ScaleImage(Weapon), WeaponPosX, WeaponPosY); 


                    // If the ship gets destroyed draw the explosion image
                    if (boomX > 0 && boomY > 0 && boomCount > 0)
                    {
                        args.DrawingSession.DrawImage(Scaling.ScaleImage(Boom), boomX, boomY);
                        boomCount -= 1;
                    }
                    else // Otherwise don't explode
                    {
                        boomCount = 60;
                        boomX = 0;
                        boomY = 0;
                    }
                    // Draw the enemies
                    for (int j = 0; j < enemyXpos.Count; j++)
                    {
                        if (enemyType[j] == 1) { ENEMY_IMG = MinusMonster; }

                        if (enemyType[j] == 2) { ENEMY_IMG = PlusMonster; }

                        // Change direction of enemy movement depending on how they spawn
                        // Connected code can be seen in the enemyTimer function
                        if (enemyDir[j].Equals("left"))
                        {
                            enemyXpos[j] -= 3;
                        }
                        else
                        {
                            enemyXpos[j] += 3;

                        }

                        // Move the enemies down, change value to change speed
                        enemyYpos[j] += 3;
                        args.DrawingSession.DrawImage(Scaling.ScaleImage(ENEMY_IMG), enemyXpos[j], enemyYpos[j]);
                    }

                    //Draw projectiles
                    for (int i = 0; i < blastXPos.Count; i++)
                    {

                        // Linear Interpolation for moving the projectiles
                        // Adapted from https://stackoverflow.com/questions/25276516/linear-interpolation-for-dummies
                        // THIS CODE IS FOR MOBILE ONLY OR DEVICES WITH AN ACCELEROMETRE
                         pointX = (blastXPos[i] + (blastXPos[i] - blastXPos[i]) * percent[i]);
                         pointY = (blastYPos[i] + (blastYPos[i] - blastYPos[i]) * percent[i]);

                        // THIS CODE ALLOWS FOR DESKTOPS, LAPTOPS ETC. TO SEE THE BLASTS MOVE
                        pointX = (photonX + (blastXPos[i] - photonX) * percent[i]);
                        pointY = (photonY + (blastYPos[i] - photonY) * percent[i]);

                        args.DrawingSession.DrawImage(Scaling.ScaleImage(Blast), pointX - (30 * scaleWidth), pointY - (30 * scaleHeight)); // Decrease by 30 to compensate for the offset of the mouse

                        // Increment the position of the projectile to give the appearance of movement
                        percent[i] += (0.040f);

                        // Check if the blast has hit an enemy (collision detection)
                        for (int h = 0; h < enemyXpos.Count; h++)
                        {
                            // If the blast hits an enemy, adjusted for image size of boom.png (185 x 175)
                            if (pointX >= enemyXpos[h] && pointX <= enemyXpos[h] + (185 * scaleWidth)
                                && pointY >= enemyYpos[h] && pointY <= enemyYpos[h] + (175 * scaleHeight))
                            {
                                boomX = pointX - ((185 / 2) * scaleWidth);
                                boomY = pointY - ((175 / 2) * scaleWidth);

                                // Remove the enemy image
                                enemyXpos.RemoveAt(h);
                                enemyYpos.RemoveAt(h);
                                enemyType.RemoveAt(h);
                                enemyDir.RemoveAt(h);

                                // Remove the blast image
                                blastXPos.RemoveAt(i);
                                blastYPos.RemoveAt(i);
                                blastXPosStartPos.RemoveAt(i);
                                blastYPosStartPos.RemoveAt(i);
                                percent.RemoveAt(i);

                                // Increment Score
                                gameScore = gameScore + 100;

                                break;
                            }
                        }

                        // If the projectile goes off the screen
                        if (pointY < 0f)
                        {
                            // Remove any projectiles that go off the top of the screen
                            blastXPos.RemoveAt(i);
                            blastYPos.RemoveAt(i);
                            blastXPosStartPos.RemoveAt(i);
                            blastYPosStartPos.RemoveAt(i);
                            percent.RemoveAt(i);
                        }
                    }
                }
            }
         
            

            // Redraw everything in the draw method (roughly 60fps)
            GameCanvas.Invalidate();
        }

        // Handles touch screen taps
        private void GameCanvas_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Display the score screen if the round ends
            if (RoundEnded == true)
            {

                GameState = 0;
                // Reset the round
                RoundEnded = false;
                countdown = 60;

                // Stop the enemy timer
                EnemyTimer.Stop();
                enemyXpos.Clear();
                enemyYpos.Clear();
                enemyType.Clear();
                enemyDir.Clear();

            }
            else
            {
                // If the screen is tapped/clicked go up one level
                if (GameState == 0)
                {
                    GameState += 1;
                    RoundTimer.Start();
                    EnemyTimer.Start();
                   

                } else if (GameState > 0)
                {
                    // Add the xy coordinates of a blast projectile from user mouse position
                    blastXPos.Add((float)e.GetPosition(GameCanvas).X);
                    blastYPos.Add((float)e.GetPosition(GameCanvas).Y);
                    blastXPosStartPos.Add((float)(WeaponPosX + (Weapon.Bounds.Width*scaleWidth / 2)));
                    blastYPosStartPos.Add((float)boundaries.Height - (65 * scaleHeight));
                    percent.Add(0f);
                }
            }
           
        }

        // The Game State Manager
        public void GSM()
        {
            // Shows the score screen if the round ends
            if (RoundEnded == true)
            {
                BG = ScoreScreen;
            }
            else
            {
                // Loads the Start Screen
                if (GameState == 0)
                {
                    BG = StartScreen;
                }

                // Loads Level 1
                else if (GameState == 1)
                {
                    BG = Level1;
                }
            }
            
        }

        // RoundTimer_Tick controls the decrementing round time
        private void RoundTimer_Tick(object sender, object e)
        {
            // Decrement the timer
            countdown -= 1;

            // Stops the timer once it reaches 0 and ends the round
            if (countdown < 1)
            {
                RoundTimer.Stop();
                RoundEnded = true;
            }
        }


        private void EnemyTimer_Tick(object sender, object e)
        {
            // Randomly choose what type of enemy to generate
            int eType = EnemyTypeRand.Next(1, 3);
            int startPosX = EnemyXStart.Next(0, (int)boundaries.Width);  // Starting position for enemies on the x-axis
            if (startPosX > boundaries.Width / 2)
            {
                enemyDir.Add("left");
            } else
            {
                enemyDir.Add("right");
            }

            // Where the enemies start on the x-axis
            enemyXpos.Add(startPosX);
            // Where the enemies start on the y-axis
            enemyYpos.Add(-50 * scaleHeight);
            // Assign the enemy type (1 or 2)
            enemyType.Add(eType);

            // Regenerate a random number so individual enemies spawn differently
            EnemyTimer.Interval = new TimeSpan(0, 0, 0, 0, EnemyGenIntervalRand.Next(500, 2000));
        }
    }
}
