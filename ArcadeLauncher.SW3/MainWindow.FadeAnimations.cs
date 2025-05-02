using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;
using ArcadeLauncher.Core;

namespace ArcadeLauncher.SW3
{
    public partial class MainWindow
    {
        private const float T1FadeOutDuration = 0.8f;
        private const float T1FadeInDuration = 0.8f;
        private const float T1OverlapPercentage = 0.98f;
        private const float FadeOutDurationT2 = 0.8f;
        private const float FadeInDurationT3 = 0.8f;
        private const float T4FadeOutDuration = 0.8f;
        private const float T4FadeInDuration = 0.8f;
        private const float T4OverlapPercentage = 0.98f;

        private void PerformT1Fade(Game game, int screenHeightPhysical)
        {
            // T1 Fade-Out: MainWindow fades out with SineEase (EaseOut) over 0.8s
            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromSeconds(T1FadeOutDuration),
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
            };

            // T1 Fade-In: SplashScreenWindow fades in with SineEase over 0.8s, starting at 98% of fade-out
            var fadeInAnimation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(T1FadeInDuration),
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn }
            };

            // Log opacity updates for fade-out
            var fadeOutTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            fadeOutTimer.Tick += (s, e) =>
            {
                LogToFile($"T1 MainWindow fade-out opacity update at {DateTime.Now:HH:mm:ss.fff}: Opacity={Opacity}, Visibility={Visibility}, IsLoaded={IsLoaded}");
            };

            // Start fade-out after a brief delay to ensure rendering
            var startDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.05) };
            startDelayTimer.Tick += (s, e) =>
            {
                startDelayTimer.Stop();
                LogToFile($"Starting T1 MainWindow fade-out (SineEase, EaseOut, {T1FadeOutDuration}s) at {DateTime.Now:HH:mm:ss.fff}. Visibility: {Visibility}, Opacity: {Opacity}, IsLoaded: {IsLoaded}");
                try
                {
                    fadeOutTimer.Start();
                    this.BeginAnimation(OpacityProperty, fadeOutAnimation);
                }
                catch (Exception ex)
                {
                    LogToFile($"Error starting T1 MainWindow fade-out at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                }
            };
            startDelayTimer.Start();

            // Start fade-in at 98% of fade-out duration (0.8s * 0.98 = 0.784s)
            var overlapTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(T1FadeOutDuration * T1OverlapPercentage)
            };
            overlapTimer.Tick += (s, e) =>
            {
                overlapTimer.Stop();
                splashScreenWindow.Opacity = 0;
                splashScreenWindow.Visibility = Visibility.Visible;
                splashScreenWindow.Show();
                var splashHandle = new System.Windows.Interop.WindowInteropHelper(splashScreenWindow).Handle;
                try
                {
                    SetForegroundWindow(splashHandle);
                    splashScreenWindow.Activate();
                    splashScreenWindow.Focus();
                    LogToFile($"Starting T1 SplashScreenWindow fade-in (SineEase, {T1FadeInDuration}s) at {DateTime.Now:HH:mm:ss.fff}. Visibility: {splashScreenWindow.Visibility}, Opacity: {splashScreenWindow.Opacity}, Handle: {splashHandle}, Overlap: {T1OverlapPercentage}");
                    Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
                    LogToFile($"SplashScreenWindow render forced for T1 fade-in at {DateTime.Now:HH:mm:ss.fff}.");
                    splashScreenWindow.BeginAnimation(OpacityProperty, fadeInAnimation);
                }
                catch (Exception ex)
                {
                    LogToFile($"Error starting T1 SplashScreenWindow fade-in at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                }
            };
            overlapTimer.Start();

            // Finalize fade-out
            fadeOutAnimation.Completed += (s, e) =>
            {
                fadeOutTimer.Stop();
                Visibility = Visibility.Hidden;
                LogToFile($"T1 MainWindow fade-out completed at {DateTime.Now:HH:mm:ss.fff}. Visibility: {Visibility}, Opacity: {Opacity}, IsLoaded: {IsLoaded}");

                // Stop XInput polling after T1 fade-out
                if (xInputTimer != null)
                {
                    xInputTimer.Stop();
                    LogToFile($"XInput polling stopped at LaunchGame after T1 fade-out at {DateTime.Now:HH:mm:ss.fff}.");
                }

                // Update marquee and controller images after fade-out
                if (marqueeWindow != null && marqueeWindow.Content is Grid marqueeGrid)
                {
                    string marqueePath = game.MarqueePath ?? System.IO.Path.Combine(Program.InstallDir, "default_marquee.png");
                    FadeImage(marqueeGrid, new BitmapImage(new Uri(marqueePath, UriKind.Absolute)));
                }

                if (controllerWindow != null && controllerWindow.Content is Grid controllerGrid)
                {
                    string? splashImagePath = null;
                    if (screenHeightPhysical >= 2160 && game.SplashScreenPath.ContainsKey("4k"))
                    {
                        splashImagePath = game.SplashScreenPath["4k"];
                    }
                    else if (screenHeightPhysical >= 1440 && game.SplashScreenPath.ContainsKey("1440p"))
                    {
                        splashImagePath = game.SplashScreenPath["1440p"];
                    }
                    else if (game.SplashScreenPath.ContainsKey("1080p"))
                    {
                        splashImagePath = game.SplashScreenPath["1080p"];
                    }

                    if (!string.IsNullOrEmpty(splashImagePath) && File.Exists(splashImagePath))
                    {
                        FadeImage(controllerGrid, new BitmapImage(new Uri(splashImagePath, UriKind.Absolute)));
                    }
                    else
                    {
                        string controllerDefaultPath = System.IO.Path.Combine(Program.InstallDir, "default_controller.png");
                        FadeImage(controllerGrid, new BitmapImage(new Uri(controllerDefaultPath, UriKind.Absolute)));
                    }
                }
            };

            // Start progress timer after fade-in
            fadeInAnimation.Completed += (s, e) =>
            {
                LogToFile($"T1 SplashScreenWindow fade-in completed at {DateTime.Now:HH:mm:ss.fff}. Starting progress timer.");
                try
                {
                    splashScreenWindow.StartProgressTimer();
                }
                catch (Exception ex)
                {
                    LogToFile($"Error starting progress timer after T1 fade-in at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                }
            };
        }

        private void PerformT2Fade(SplashScreenWindow splashScreen, Action onComplete)
        {
            splashScreen.Dispatcher.Invoke(() =>
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
                    LogToFile($"SplashScreenWindow fade-out complete for launch phase (Transition 2) at {DateTime.Now:HH:mm:ss.fff}.");
                    try
                    {
                        onComplete?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"Error invoking onComplete for T2 at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                    }
                };
                try
                {
                    splashScreen.BeginAnimation(OpacityProperty, fadeOutAnimation);
                    LogToFile($"Starting fade-out for launch phase (Transition 2) with SineEase (EaseOut) at {DateTime.Now:HH:mm:ss.fff}.");
                }
                catch (Exception ex)
                {
                    LogToFile($"Error starting T2 fade-out at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                }
            });
        }

        private void PerformT3Fade(IntPtr splashHandle)
        {
            // Clear any existing animations
            splashScreenWindow.BeginAnimation(OpacityProperty, null);
            LogToFile($"Starting T3 fade-in animation with SineEase (EaseIn) at {DateTime.Now:HH:mm:ss.fff}. Initial Opacity: {splashScreenWindow.Opacity}, Visibility: {splashScreenWindow.Visibility}");
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
                LogToFile($"T3 opacity update at {DateTime.Now:HH:mm:ss.fff}: SplashScreenWindow Opacity={splashScreenWindow.Opacity}");
            };
            fadeInAnimation.Completed += (s, e) =>
            {
                LogToFile($"T3 fade-in animation completed at {DateTime.Now:HH:mm:ss.fff}. Final Opacity: {splashScreenWindow.Opacity}, Visibility: {splashScreenWindow.Visibility}");
                try
                {
                    splashScreenWindow.StartProgressTimer();
                    LogToFile($"ProgressTimer started for T3 at {DateTime.Now:HH:mm:ss.fff}.");
                }
                catch (Exception ex)
                {
                    LogToFile($"Error starting ProgressTimer for T3 at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                }
                opacityTimer.Stop();
                LogToFile($"T3 opacity timer stopped at {DateTime.Now:HH:mm:ss.fff}.");
            };
            splashScreenWindow.Opacity = 0;
            try
            {
                splashScreenWindow.BeginAnimation(OpacityProperty, fadeInAnimation);
                opacityTimer.Start();
            }
            catch (Exception ex)
            {
                LogToFile($"Error starting T3 fade-in animation at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
            }

            // Stop any existing focus timer
            if (focusTimer != null)
            {
                focusTimer.Stop();
                LogToFile($"Stopped existing T3 focus timer at {DateTime.Now:HH:mm:ss.fff}.");
                focusTimer = null;
            }

            focusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            int focusAttempts = 0;
            focusTimer.Tick += (s, e) =>
            {
                focusAttempts++;
                var currentForeground = GetForegroundWindow();
                if (currentForeground != splashHandle)
                {
                    try
                    {
                        SetForegroundWindow(splashHandle);
                        LogToFile($"T3 focus attempt {focusAttempts} at {DateTime.Now:HH:mm:ss.fff}: SetForegroundWindow called for SplashScreenWindow, current foreground was {currentForeground}, target handle: {splashHandle}");
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"Error in T3 focus attempt {focusAttempts} at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                    }
                }
                if (focusAttempts >= 20)
                {
                    focusTimer.Stop();
                    focusTimer = null;
                    LogToFile($"T3 focus loop stopped at {DateTime.Now:HH:mm:ss.fff} after {focusAttempts} attempts.");
                }
            };
            focusTimer.Start();
            LogToFile($"T3 focus loop started for SplashScreenWindow at {DateTime.Now:HH:mm:ss.fff}.");
        }

        private void PerformT4Fade(SplashScreenWindow splashScreen)
        {
            Dispatcher.Invoke(() =>
            {
                LogToFile($"Preparing T4 at {DateTime.Now:HH:mm:ss.fff}: MainWindow IsLoaded={IsLoaded}, Visibility={Visibility}, Opacity={Opacity}, SplashScreenWindow Visibility={splashScreen.Visibility}, Opacity={splashScreen.Opacity}");

                if (!IsLoaded)
                {
                    LogToFile($"T4 aborted: MainWindow is not loaded at {DateTime.Now:HH:mm:ss.fff}. Closing SplashScreenWindow.");
                    splashScreen.Close();
                    return;
                }

                // Ensure SplashScreenWindow is visible, topmost, and rendered
                splashScreen.Visibility = Visibility.Visible;
                splashScreen.Opacity = 1.0;
                splashScreen.Topmost = true;
                Topmost = false; // Ensure MainWindow is behind during T4A
                Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
                LogToFile($"T4 SplashScreenWindow state set at {DateTime.Now:HH:mm:ss.fff}: Visibility={splashScreen.Visibility}, Opacity={splashScreen.Opacity}, Topmost={splashScreen.Topmost}, Render forced.");

                // Clear any existing animations
                splashScreen.BeginAnimation(OpacityProperty, null);

                // T4A: SplashScreenWindow fade-out with SineEase (EaseOut) over 0.8s
                var fadeOutTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                var fadeOutAnimation = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.0,
                    Duration = TimeSpan.FromSeconds(T4FadeOutDuration),
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
                };

                // T4B: MainWindow fade-in with SineEase (EaseIn) over 0.8s, starting at 98% of fade-out
                Opacity = 0;
                Visibility = Visibility.Visible;
                var fadeInTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
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
                    LogToFile($"T4 SplashScreenWindow fade-out opacity update at {DateTime.Now:HH:mm:ss.fff}: Opacity={splashScreen.Opacity}, Visibility={splashScreen.Visibility}");
                };

                // Log opacity updates for T4B fade-in
                fadeInTimer.Tick += (s, e) =>
                {
                    LogToFile($"T4 MainWindow fade-in opacity update at {DateTime.Now:HH:mm:ss.fff}: Opacity={Opacity}, Visibility={Visibility}, IsLoaded={IsLoaded}");
                };

                // Start T4A fade-out
                LogToFile($"Starting T4 SplashScreenWindow fade-out (SineEase, EaseOut, {T4FadeOutDuration}s) at {DateTime.Now:HH:mm:ss.fff}.");
                try
                {
                    fadeOutTimer.Start();
                    splashScreen.BeginAnimation(OpacityProperty, fadeOutAnimation);
                }
                catch (Exception ex)
                {
                    LogToFile($"Error starting T4 SplashScreenWindow fade-out at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                    splashScreen.Close();
                }

                // Start T4B fade-in at 98% of fade-out duration (0.8s * 0.98 = 0.784s)
                var overlapTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(T4FadeOutDuration * T4OverlapPercentage)
                };
                overlapTimer.Tick += (s, e) =>
                {
                    overlapTimer.Stop();
                    try
                    {
                        Topmost = true;
                        Activate();
                        Focus();
                        LogToFile($"Starting T4 MainWindow fade-in (SineEase, {T4FadeInDuration}s) at {DateTime.Now:HH:mm:ss.fff}. Visibility: {Visibility}, Opacity: {Opacity}, Overlap: {T4OverlapPercentage}, IsLoaded: {IsLoaded}");
                        Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
                        LogToFile($"MainWindow render forced for T4 fade-in at {DateTime.Now:HH:mm:ss.fff}.");
                        fadeInTimer.Start();
                        BeginAnimation(OpacityProperty, fadeInAnimation);
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"Error starting T4 MainWindow fade-in at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                        splashScreen.Close();
                    }
                };
                overlapTimer.Start();

                // Finalize T4A fade-out
                fadeOutAnimation.Completed += (s, e) =>
                {
                    fadeOutTimer.Stop();
                    splashScreen.Visibility = Visibility.Hidden;
                    LogToFile($"T4 SplashScreenWindow fade-out completed at {DateTime.Now:HH:mm:ss.fff}. Visibility: {splashScreen.Visibility}, Opacity: {splashScreen.Opacity}");
                };

                // Finalize T4B fade-in and cleanup
                fadeInAnimation.Completed += (s, e) =>
                {
                    fadeInTimer.Stop();
                    LogToFile($"T4 MainWindow fade-in completed at {DateTime.Now:HH:mm:ss.fff}. Visibility: {Visibility}, Opacity: {Opacity}, IsLoaded: {IsLoaded}");
                    try
                    {
                        Topmost = true;
                        Activate();
                        Focus();
                        LogToFile($"MainWindow made visible and focused after T4 at {DateTime.Now:HH:mm:ss.fff}.");
                        // Resume XInput polling after T4
                        if (xInputTimer != null)
                        {
                            xInputTimer.Start();
                            LogToFile($"XInput polling started after T4 at {DateTime.Now:HH:mm:ss.fff}.");
                        }
                        splashScreen.Close();
                        LogToFile($"SplashScreenWindow closed after T4 at {DateTime.Now:HH:mm:ss.fff}.");
                        splashScreen.onComplete?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"Error finalizing T4 at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                    }
                };
            });
        }
    }
}