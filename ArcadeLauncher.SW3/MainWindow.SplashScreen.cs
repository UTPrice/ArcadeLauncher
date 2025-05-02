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
        private const float FadeInDurationT3 = 0.8f;
        private const float FadeOutDurationT2 = 0.8f;
        private const int ProgressDurationMs = 3000;
        private const int ProgressUpdateIntervalMs = 30;
        private const int FadeUpdateIntervalMs = 50;
        private const float T4FadeOutDuration = 0.8f;
        private const float T4FadeInDuration = 0.8f;
        private const float T4OverlapPercentage = 0.98f;
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
            private DispatcherTimer? fadeOutTimer;
            private DispatcherTimer? fadeInTimer;
            private DispatcherTimer? overlapTimer;
            private DispatcherTimer? t4FallbackTimer;
            private Action onComplete;
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
                    fadeOutTimer?.Stop();
                    fadeInTimer?.Stop();
                    overlapTimer?.Stop();
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

            public void StartFadeAnimation()
            {
                // Clear any existing animations
                BeginAnimation(OpacityProperty, null);
                parentWindow.LogToFile($"Starting T3 fade-in animation with SineEase (EaseIn) at {DateTime.Now:HH:mm:ss.fff}. Initial Opacity: {Opacity}, Visibility: {Visibility}");
                var fadeInAnimation = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromSeconds(FadeInDurationT3),
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn }
                };
                var opacityTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                opacityTimer.Tick += (s, e) =>
                {
                    parentWindow.LogToFile($"T3 opacity update at {DateTime.Now:HH:mm:ss.fff}: SplashScreenWindow Opacity={Opacity}");
                };
                fadeInAnimation.Completed += (s, e) =>
                {
                    parentWindow.LogToFile($"T3 fade-in animation completed at {DateTime.Now:HH:mm:ss.fff}. Final Opacity: {Opacity}, Visibility: {Visibility}");
                    try
                    {
                        progressTimer!.Start();
                        parentWindow.LogToFile($"ProgressTimer started for T3 at {DateTime.Now:HH:mm:ss.fff}.");
                    }
                    catch (Exception ex)
                    {
                        parentWindow.LogToFile($"Error starting ProgressTimer for T3 at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                    }
                    opacityTimer.Stop();
                    parentWindow.LogToFile($"T3 opacity timer stopped at {DateTime.Now:HH:mm:ss.fff}.");
                };
                Opacity = 0;
                try
                {
                    BeginAnimation(OpacityProperty, fadeInAnimation);
                    opacityTimer.Start();
                }
                catch (Exception ex)
                {
                    parentWindow.LogToFile($"Error starting T3 fade-in animation at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
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
                            parentWindow.Dispatcher.Invoke(() =>
                            {
                                parentWindow.LogToFile($"Preparing T4 at {DateTime.Now:HH:mm:ss.fff}: MainWindow IsLoaded={parentWindow?.IsLoaded}, Visibility={parentWindow?.Visibility}, Opacity={parentWindow?.Opacity}, SplashScreenWindow Visibility={Visibility}, Opacity={Opacity}");

                                if (parentWindow == null || !parentWindow.IsLoaded)
                                {
                                    parentWindow?.LogToFile($"T4 aborted: MainWindow is null or not loaded at {DateTime.Now:HH:mm:ss.fff}. Closing SplashScreenWindow.");
                                    Close();
                                    return;
                                }

                                // Ensure SplashScreenWindow is visible, topmost, and rendered
                                Visibility = Visibility.Visible;
                                Opacity = 1.0;
                                Topmost = true;
                                parentWindow.Topmost = false; // Ensure MainWindow is behind during T4A
                                Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
                                parentWindow.LogToFile($"T4 SplashScreenWindow state set at {DateTime.Now:HH:mm:ss.fff}: Visibility={Visibility}, Opacity={Opacity}, Topmost={Topmost}, Render forced.");

                                // Clear any existing animations
                                BeginAnimation(OpacityProperty, null);

                                // T4A: SplashScreenWindow fade-out with SineEase (EaseOut) over 0.8s
                                fadeOutTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                                var fadeOutAnimation = new DoubleAnimation
                                {
                                    From = 1.0,
                                    To = 0.0,
                                    Duration = TimeSpan.FromSeconds(T4FadeOutDuration),
                                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
                                };

                                // T4B: MainWindow fade-in with SineEase (EaseIn) over 0.8s, starting at 98% of fade-out
                                parentWindow.Opacity = 0;
                                parentWindow.Visibility = Visibility.Visible;
                                fadeInTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                                var fadeInAnimation = new DoubleAnimation
                                {
                                    From = 0.0,
                                    To = 1.0,
                                    Duration = TimeSpan.FromSeconds(T4FadeInDuration),
                                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn }
                                };

                                // Log opacity updates for T4A fade-out
                                fadeOutTimer.Tick += (s, e) =>
                                {
                                    parentWindow.LogToFile($"T4 SplashScreenWindow fade-out opacity update at {DateTime.Now:HH:mm:ss.fff}: Opacity={Opacity}, Visibility={Visibility}");
                                };

                                // Log opacity updates for T4B fade-in
                                fadeInTimer.Tick += (s, e) =>
                                {
                                    parentWindow.LogToFile($"T4 MainWindow fade-in opacity update at {DateTime.Now:HH:mm:ss.fff}: Opacity={parentWindow.Opacity}, Visibility={parentWindow.Visibility}, IsLoaded={parentWindow.IsLoaded}");
                                };

                                // Start T4A fade-out
                                parentWindow.LogToFile($"Starting T4 SplashScreenWindow fade-out (SineEase, EaseOut, {T4FadeOutDuration}s) at {DateTime.Now:HH:mm:ss.fff}.");
                                try
                                {
                                    fadeOutTimer.Start();
                                    BeginAnimation(OpacityProperty, fadeOutAnimation);
                                }
                                catch (Exception ex)
                                {
                                    parentWindow.LogToFile($"Error starting T4 SplashScreenWindow fade-out at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                                    Close();
                                }

                                // Start T4B fade-in at 98% of fade-out duration (0.8s * 0.98 = 0.784s)
                                overlapTimer = new DispatcherTimer
                                {
                                    Interval = TimeSpan.FromSeconds(T4FadeOutDuration * T4OverlapPercentage)
                                };
                                overlapTimer.Tick += (s, e) =>
                                {
                                    overlapTimer.Stop();
                                    try
                                    {
                                        parentWindow.Topmost = true;
                                        parentWindow.Activate();
                                        parentWindow.Focus();
                                        parentWindow.LogToFile($"Starting T4 MainWindow fade-in (SineEase, {T4FadeInDuration}s) at {DateTime.Now:HH:mm:ss.fff}. Visibility: {parentWindow.Visibility}, Opacity: {parentWindow.Opacity}, Overlap: {T4OverlapPercentage}, IsLoaded: {parentWindow.IsLoaded}");
                                        Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
                                        parentWindow.LogToFile($"MainWindow render forced for T4 fade-in at {DateTime.Now:HH:mm:ss.fff}.");
                                        fadeInTimer.Start();
                                        parentWindow.BeginAnimation(OpacityProperty, fadeInAnimation);
                                    }
                                    catch (Exception ex)
                                    {
                                        parentWindow.LogToFile($"Error starting T4 MainWindow fade-in at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                                        Close();
                                    }
                                };
                                overlapTimer.Start();

                                // Finalize T4A fade-out
                                fadeOutAnimation.Completed += (s, e) =>
                                {
                                    fadeOutTimer.Stop();
                                    Visibility = Visibility.Hidden;
                                    parentWindow.LogToFile($"T4 SplashScreenWindow fade-out completed at {DateTime.Now:HH:mm:ss.fff}. Visibility: {Visibility}, Opacity: {Opacity}");
                                };

                                // Finalize T4B fade-in and cleanup
                                fadeInAnimation.Completed += (s, e) =>
                                {
                                    fadeInTimer.Stop();
                                    parentWindow.LogToFile($"T4 MainWindow fade-in completed at {DateTime.Now:HH:mm:ss.fff}. Visibility: {parentWindow.Visibility}, Opacity: {parentWindow.Opacity}, IsLoaded: {parentWindow.IsLoaded}");
                                    try
                                    {
                                        parentWindow.Topmost = true;
                                        parentWindow.Activate();
                                        parentWindow.Focus();
                                        parentWindow.LogToFile($"MainWindow made visible and focused after T4 at {DateTime.Now:HH:mm:ss.fff}.");
                                        // Resume XInput polling after T4
                                        if (parentWindow.xInputTimer != null)
                                        {
                                            parentWindow.xInputTimer.Start();
                                            parentWindow.LogToFile($"XInput polling started after T4 at {DateTime.Now:HH:mm:ss.fff}.");
                                        }
                                        Close();
                                        parentWindow.LogToFile($"SplashScreenWindow closed after T4 at {DateTime.Now:HH:mm:ss.fff}.");
                                        onComplete?.Invoke();
                                    }
                                    catch (Exception ex)
                                    {
                                        parentWindow.LogToFile($"Error finalizing T4 at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                                    }
                                };
                            });
                        }
                        else
                        {
                            parentWindow.Dispatcher.Invoke(() =>
                            {
                                var fadeOutAnimation = new DoubleAnimation
                                {
                                    From = 1.0,
                                    To = 0.0,
                                    Duration = TimeSpan.FromSeconds(FadeOutDurationT2),
                                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
                                };
                                fadeOutAnimation.Completed += (s, e) =>
                                {
                                    parentWindow.LogToFile($"SplashScreenWindow fade-out complete for launch phase (Transition 2) at {DateTime.Now:HH:mm:ss.fff}.");
                                    try
                                    {
                                        onComplete?.Invoke();
                                    }
                                    catch (Exception ex)
                                    {
                                        parentWindow.LogToFile($"Error invoking onComplete for T2 at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                                    }
                                };
                                try
                                {
                                    this.BeginAnimation(OpacityProperty, fadeOutAnimation);
                                    parentWindow.LogToFile($"Starting fade-out for launch phase (Transition 2) with SineEase (EaseOut) at {DateTime.Now:HH:mm:ss.fff}.");
                                }
                                catch (Exception ex)
                                {
                                    parentWindow.LogToFile($"Error starting T2 fade-out at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                                }
                            });
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