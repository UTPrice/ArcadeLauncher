using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ArcadeLauncher.Core;

namespace ArcadeLauncher.SW3
{
    public partial class MainWindow
    {
        private SplashScreenWindow splashScreenWindow;

        private const float FadeDurationSeconds = 0.6f;
        private const float FadeInDurationSeconds = FadeDurationSeconds / 2;
        private const float FadeOutDurationSeconds = FadeDurationSeconds / 2;
        private const int ProgressDurationMs = 3000;
        private const int ProgressUpdateIntervalMs = 30;
        private const int FadeUpdateIntervalMs = 50;
        private const int T4FallbackTimeoutMs = 5000;

        private class SplashScreenWindow : Window
        {
            private readonly MainWindow parentWindow;
            public readonly Game game;
            private readonly double dpiScaleFactor;
            private readonly bool isLaunchPhase;
            private double currentOpacity;
            private bool isFadingIn;
            private bool isFadingOut;
            public int progressValue;
            public double scalingFactor;
            public int barHeight;
            public int progressDiameter;
            public float fontSize;
            public int screenHeightPhysical;
            public int screenWidthPhysical;
            public bool hasLoggedPositions;
            public int lastLoggedProgress;

            public System.Windows.Controls.Image splashImageControl;
            public Canvas canvas;
            public System.Windows.Shapes.Ellipse shadowArcEllipse;
            public System.Windows.Shapes.Path progressArcPath;
            public System.Windows.Shapes.Path? glowArcPath;
            public System.Windows.Controls.TextBlock gameNameText;
            public System.Windows.Controls.TextBlock progressText;
            public System.Windows.Shapes.Rectangle darkenedBar;

            private DispatcherTimer? fadeTimer;
            private DispatcherTimer? progressTimer;
            private DispatcherTimer? t4FallbackTimer;
            public Action onComplete;
            private readonly bool startFadeTimerOnConstruction;

            public SplashScreenWindow(MainWindow parentWindow, Game game, double dpiScaleFactor, Action onComplete, bool isLaunchPhase, bool startFadeTimer = true)
            {
                this.parentWindow = parentWindow;
                this.game = game;
                this.dpiScaleFactor = dpiScaleFactor;
                this.onComplete = onComplete;
                this.isLaunchPhase = isLaunchPhase;
                this.startFadeTimerOnConstruction = startFadeTimer;

                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                ShowInTaskbar = false;
                Topmost = true;
                Background = System.Windows.Media.Brushes.Black;
                Width = SystemParameters.PrimaryScreenWidth;
                Height = SystemParameters.PrimaryScreenHeight;
                Left = SystemParameters.PrimaryScreenWidth / dpiScaleFactor * 0;
                Top = 0;
                Visibility = Visibility.Hidden;
                parentWindow.LogToFile($"SplashScreenWindow created at {DateTime.Now:HH:mm:ss.fff}. Initial Visibility: {Visibility}, isLaunchPhase: {isLaunchPhase}, startFadeTimerOnConstruction: {startFadeTimerOnConstruction}");

                currentOpacity = 0;
                isFadingIn = true;
                isFadingOut = false;
                progressValue = 0;
                hasLoggedPositions = false;
                lastLoggedProgress = -1;

                canvas = new Canvas();
                Content = canvas;

                // Delegate UI initialization to rendering logic
                parentWindow.InitializeUI(this, canvas);

                SetupTimers();

                Closing += (s, e) =>
                {
                    fadeTimer?.Stop();
                    progressTimer?.Stop();
                    t4FallbackTimer?.Stop();
                    BeginAnimation(OpacityProperty, null); // Clear animations
                    parentWindow.LogToFile($"SplashScreenWindow closing at {DateTime.Now:HH:mm:ss.fff}. Timers stopped and animations cleared. Visibility: {Visibility}, Opacity: {Opacity}");
                    // Attempt to restore MainWindow if not in launch phase
                    if (!isLaunchPhase && parentWindow != null && parentWindow.IsLoaded)
                    {
                        parentWindow.Dispatcher.Invoke(() =>
                        {
                            parentWindow.Visibility = Visibility.Visible;
                            parentWindow.Opacity = 1;
                            parentWindow.Topmost = true;
                            parentWindow.Activate();
                            parentWindow.Focus();
                            parentWindow.LogToFile($"MainWindow restored on SplashScreenWindow closing at {DateTime.Now:HH:mm:ss.fff}. Visibility: {parentWindow.Visibility}, Opacity: {parentWindow.Opacity}, IsLoaded: {parentWindow.IsLoaded}");
                        });
                    }
                };
            }

            public void StartFadeTimer()
            {
                currentOpacity = 0;
                isFadingIn = true;
                Opacity = currentOpacity;
                parentWindow.LogToFile($"Starting fadeTimer at {DateTime.Now:HH:mm:ss.fff}. Initial Opacity: {Opacity}, isFadingIn: {isFadingIn}");
                fadeTimer!.Start();
            }

            public void StartProgressTimer()
            {
                parentWindow.LogToFile($"Starting progressTimer for {(isLaunchPhase ? "T1" : "T3")} at {DateTime.Now:HH:mm:ss.fff}.");
                progressTimer!.Start();
                if (!isLaunchPhase)
                {
                    // Start fallback timer to restore MainWindow if T4 doesn't start
                    t4FallbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(T4FallbackTimeoutMs) };
                    t4FallbackTimer.Tick += (s, e) =>
                    {
                        t4FallbackTimer.Stop();
                        parentWindow.LogToFile($"T4 fallback timer triggered at {DateTime.Now:HH:mm:ss.fff}. ProgressValue={progressValue}%. Restoring MainWindow.");
                        if (parentWindow != null && parentWindow.IsLoaded)
                        {
                            parentWindow.Dispatcher.Invoke(() =>
                            {
                                parentWindow.Visibility = Visibility.Visible;
                                parentWindow.Opacity = 1;
                                parentWindow.Topmost = true;
                                parentWindow.Activate();
                                parentWindow.Focus();
                                parentWindow.LogToFile($"MainWindow restored via T4 fallback at {DateTime.Now:HH:mm:ss.fff}. Visibility: {parentWindow.Visibility}, Opacity: {parentWindow.Opacity}, IsLoaded: {parentWindow.IsLoaded}");
                            });
                        }
                        Close();
                    };
                    t4FallbackTimer.Start();
                    parentWindow.LogToFile($"T4 fallback timer started at {DateTime.Now:HH:mm:ss.fff}.");
                }
            }

            private void SetupTimers()
            {
                fadeTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(FadeUpdateIntervalMs)
                };
                fadeTimer.Tick += (s, e) => FadeTimer_Tick();
                if (startFadeTimerOnConstruction)
                {
                    parentWindow.LogToFile($"Starting fadeTimer on construction at {DateTime.Now:HH:mm:ss.fff}.");
                    fadeTimer.Start();
                }
                else
                {
                    parentWindow.LogToFile($"fadeTimer not started on construction at {DateTime.Now:HH:mm:ss.fff}; awaiting StartFadeTimer or StartFadeAnimation call.");
                }

                progressTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(ProgressUpdateIntervalMs)
                };
                progressTimer.Tick += (s, e) => ProgressTimer_Tick();
            }

            private void FadeTimer_Tick()
            {
                double fadeStep;
                if (isFadingIn)
                {
                    fadeStep = FadeUpdateIntervalMs / (FadeInDurationSeconds * 1000);
                }
                else
                {
                    fadeStep = FadeUpdateIntervalMs / (FadeOutDurationSeconds * 1000);
                }

                if (isFadingIn)
                {
                    currentOpacity += fadeStep;
                    if (currentOpacity >= 1)
                    {
                        currentOpacity = 1;
                        isFadingIn = false;
                        progressTimer!.Start();
                        parentWindow.LogToFile($"Fade-in complete at {DateTime.Now:HH:mm:ss.fff}. Starting progressTimer.");
                    }
                }
                else if (isFadingOut)
                {
                    currentOpacity -= fadeStep;
                    if (currentOpacity <= 0)
                    {
                        currentOpacity = 0;
                        fadeTimer!.Stop();
                        parentWindow.LogToFile($"SplashScreenWindow fade-out complete at {DateTime.Now:HH:mm:ss.fff}, invoking onComplete.");
                        try
                        {
                            onComplete?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            parentWindow.LogToFile($"Error invoking onComplete at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                        }
                        Close();
                    }
                }

                Opacity = currentOpacity;
                parentWindow.LogToFile($"FadeTimer_Tick at {DateTime.Now:HH:mm:ss.fff}: isFadingIn={isFadingIn}, isFadingOut={isFadingOut}, currentOpacity={currentOpacity}, Window Opacity={Opacity}");
            }

            private void ProgressTimer_Tick()
            {
                try
                {
                    parentWindow.UpdateProgressMeter(this);
                    if (progressValue % 10 == 0 || progressValue == 100)
                    {
                        parentWindow.LogToFile($"ProgressTimer_Tick at {DateTime.Now:HH:mm:ss.fff}: ProgressValue={progressValue}%, isLaunchPhase={isLaunchPhase}, SplashScreenWindow Visibility={Visibility}, Opacity={Opacity}");
                    }

                    if (progressValue >= 100)
                    {
                        progressValue = 100;
                        progressTimer!.Stop();
                        t4FallbackTimer?.Stop();
                        parentWindow.LogToFile($"ProgressTimer stopped at {DateTime.Now:HH:mm:ss.fff}: ProgressValue={progressValue}%. Initiating {(isLaunchPhase ? "T2" : "T4")}.");

                        if (!isLaunchPhase)
                        {
                            parentWindow.PerformT4Fade(this);
                        }
                        else
                        {
                            parentWindow.PerformT2Fade(this, onComplete);
                        }
                    }
                }
                catch (Exception ex)
                {
                    parentWindow.LogToFile($"Error in ProgressTimer_Tick at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                    progressTimer!.Stop();
                    t4FallbackTimer?.Stop();
                    Close();
                }
            }
        }
    }
}