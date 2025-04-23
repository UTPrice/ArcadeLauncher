using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.IO;
using System.Windows.Shapes;
using System.Windows.Threading;
using ArcadeLauncher.Core;

namespace ArcadeLauncher.SW3
{
    public partial class MainWindow
    {
        private SplashScreenWindow splashScreenWindow;

        private const double BarHeightPercentage = 0.09;
        private const double BarPositionPercentage = 0.88;
        private const float BaseFontSize = 48f;
        private const float BarBaseOpacity = 0.8f;
        private const double FadeStartPercentage = 30.0;
        private const float FadeEndOpacity = 0.6f;
        private const double ProgressBarOffsetPercentage = 0.02;
        private const double ProgressBarDiameterPercentage = 0.06;
        private const float FadeDurationSeconds = 0.6f;
        private const float FadeInDurationSeconds = FadeDurationSeconds / 2;
        private const float FadeOutDurationSeconds = FadeDurationSeconds / 2;
        private const float FadeInDurationT3 = 0.8f;
        private const float FadeOutDurationT2 = 0.8f;
        private const double BaseCircleRadiusPercentage = 0.06;
        private const float BaseCircleOpacity = 0.18f;
        private const float BaseCircleFeatherDistance = 12.0f;
        private const float BaseShadowRingThickness = 2.0f;
        private const float ProgressArcThickness = 5.0f;
        private const float ProgressTextFontSizePercentage = 0.30f;
        private const float ProgressTextOutlineThickness = 1.5f;
        private const bool ProgressArcGlowEnabled = true;
        private const float ProgressArcGlowOpacity = 0.5f;
        private const float ProgressArcGlowThicknessMultiplier = 1.5f;
        private const int ProgressDurationMs = 3000;
        private const int ProgressUpdateIntervalMs = 30;
        private const int FadeUpdateIntervalMs = 50;

        private class SplashScreenWindow : Window
        {
            private readonly MainWindow parentWindow;
            private readonly Game game;
            private readonly double dpiScaleFactor;
            private readonly bool isLaunchPhase;
            private double currentOpacity;
            private bool isFadingIn;
            private bool isFadingOut;
            private int progressValue;
            private double scalingFactor;
            private int barHeight;
            private int progressDiameter;
            private float fontSize;
            private int screenHeightPhysical;
            private int screenWidthPhysical;
            private bool hasLoggedPositions;
            private int lastLoggedProgress;

            private System.Windows.Controls.Image splashImageControl;
            private Canvas canvas;
            private System.Windows.Shapes.Ellipse shadowArcEllipse;
            private System.Windows.Shapes.Path progressArcPath;
            private System.Windows.Shapes.Path glowArcPath;
            private System.Windows.Controls.TextBlock gameNameText;
            private System.Windows.Controls.TextBlock progressText;
            private System.Windows.Shapes.Rectangle darkenedBar;

            private DispatcherTimer fadeTimer;
            private DispatcherTimer progressTimer;
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

                screenWidthPhysical = (int)(Width * dpiScaleFactor);
                screenHeightPhysical = (int)(Height * dpiScaleFactor);

                scalingFactor = (double)screenHeightPhysical / 1080;

                barHeight = (int)(screenHeightPhysical * BarHeightPercentage);
                progressDiameter = (int)(screenWidthPhysical * ProgressBarDiameterPercentage);
                fontSize = BaseFontSize * (float)scalingFactor / (float)dpiScaleFactor;

                canvas = new Canvas();
                Content = canvas;

                splashImageControl = new System.Windows.Controls.Image
                {
                    Width = screenWidthPhysical / dpiScaleFactor,
                    Height = screenHeightPhysical / dpiScaleFactor,
                    Stretch = Stretch.Fill
                };
                RenderOptions.SetBitmapScalingMode(splashImageControl, BitmapScalingMode.HighQuality);
                LoadSplashImage();
                Canvas.SetLeft(splashImageControl, 0);
                Canvas.SetTop(splashImageControl, 0);
                canvas.Children.Add(splashImageControl);

                shadowArcEllipse = new System.Windows.Shapes.Ellipse();
                RenderOptions.SetEdgeMode(shadowArcEllipse, EdgeMode.Aliased);
                canvas.Children.Add(shadowArcEllipse);

                if (ProgressArcGlowEnabled)
                {
                    glowArcPath = new System.Windows.Shapes.Path
                    {
                        Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)(ProgressArcGlowOpacity * 255), 0, 255, 255)),
                        StrokeThickness = ProgressArcThickness / dpiScaleFactor
                    };
                    RenderOptions.SetEdgeMode(glowArcPath, EdgeMode.Aliased);
                    canvas.Children.Add(glowArcPath);
                }

                progressArcPath = new System.Windows.Shapes.Path
                {
                    Stroke = System.Windows.Media.Brushes.Cyan,
                    StrokeThickness = ProgressArcThickness / dpiScaleFactor
                };
                RenderOptions.SetEdgeMode(progressArcPath, EdgeMode.Aliased);
                canvas.Children.Add(progressArcPath);

                darkenedBar = new System.Windows.Shapes.Rectangle();
                canvas.Children.Add(darkenedBar);

                gameNameText = new System.Windows.Controls.TextBlock
                {
                    Foreground = System.Windows.Media.Brushes.White,
                    FontFamily = new System.Windows.Media.FontFamily("Microsoft Sans Serif"),
                    FontSize = fontSize,
                    TextAlignment = TextAlignment.Center
                };
                gameNameText.Text = game.DisplayName;
                canvas.Children.Add(gameNameText);

                progressText = new System.Windows.Controls.TextBlock
                {
                    Foreground = System.Windows.Media.Brushes.White,
                    FontFamily = new System.Windows.Media.FontFamily("Microsoft Sans Serif"),
                    TextAlignment = TextAlignment.Center
                };
                var shadowEffect = new DropShadowEffect
                {
                    Color = System.Windows.Media.Colors.Black,
                    Direction = 0,
                    ShadowDepth = ProgressTextOutlineThickness,
                    Opacity = 1.0,
                    BlurRadius = 0
                };
                progressText.Effect = shadowEffect;
                canvas.Children.Add(progressText);

                double progressFontSize = (progressDiameter * ProgressTextFontSizePercentage) / dpiScaleFactor;
                progressText.FontSize = progressFontSize;
                progressText.Text = "0%";
                progressText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                progressText.Arrange(new Rect(progressText.DesiredSize));

                gameNameText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                gameNameText.Arrange(new Rect(gameNameText.DesiredSize));

                UpdateUI();

                splashImageControl.Opacity = 1;
                shadowArcEllipse.Opacity = 1;
                if (glowArcPath != null) glowArcPath.Opacity = 1;
                progressArcPath.Opacity = 1;
                darkenedBar.Opacity = 1;
                gameNameText.Opacity = 1;
                progressText.Opacity = 1;

                SetupTimers();

                Closing += (s, e) =>
                {
                    fadeTimer?.Stop();
                    progressTimer?.Stop();
                    parentWindow.LogToFile($"SplashScreenWindow closing at {DateTime.Now:HH:mm:ss.fff}. Timers stopped.");
                };
            }

            public void StartFadeTimer()
            {
                currentOpacity = 0;
                isFadingIn = true;
                Opacity = currentOpacity;
                parentWindow.LogToFile($"Starting fadeTimer at {DateTime.Now:HH:mm:ss.fff}. Initial Opacity: {Opacity}, isFadingIn: {isFadingIn}");
                fadeTimer.Start();
            }

            public void StartProgressTimer()
            {
                parentWindow.LogToFile($"Starting progressTimer for T1 at {DateTime.Now:HH:mm:ss.fff}.");
                progressTimer.Start();
            }

            public void StartFadeAnimation()
            {
                parentWindow.LogToFile($"Starting T3 fade-in animation with SineEase (EaseIn) at {DateTime.Now:HH:mm:ss.fff}. Initial Opacity: {Opacity}, Visibility: {Visibility}");
                BeginAnimation(OpacityProperty, null);
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
                    progressTimer.Start();
                    parentWindow.LogToFile($"ProgressTimer started for T3 at {DateTime.Now:HH:mm:ss.fff}.");
                    opacityTimer.Stop();
                    parentWindow.LogToFile($"T3 opacity timer stopped at {DateTime.Now:HH:mm:ss.fff}.");
                };
                Opacity = 0;
                BeginAnimation(OpacityProperty, fadeInAnimation);
                opacityTimer.Start();
            }

            private void LoadSplashImage()
            {
                string imagePath = null;
                if (screenHeightPhysical >= 2160 && game.SplashScreenPath.ContainsKey("4k"))
                {
                    imagePath = game.SplashScreenPath["4k"];
                }
                else if (screenHeightPhysical >= 1440 && game.SplashScreenPath.ContainsKey("1440p"))
                {
                    imagePath = game.SplashScreenPath["1440p"];
                }
                else if (game.SplashScreenPath.ContainsKey("1080p"))
                {
                    imagePath = game.SplashScreenPath["1080p"];
                }

                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                {
                    splashImageControl.Source = new BitmapImage(new Uri(imagePath, UriKind.Absolute));
                    parentWindow.LogToFile($"Loaded splash screen image at {DateTime.Now:HH:mm:ss.fff}: {imagePath}");
                }
                else
                {
                    splashImageControl.Source = null;
                    parentWindow.LogToFile($"No splash screen image found at {DateTime.Now:HH:mm:ss.fff}, using black background.");
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
                        progressTimer.Start();
                        parentWindow.LogToFile($"Fade-in complete at {DateTime.Now:HH:mm:ss.fff}. Starting progressTimer.");
                    }
                }
                else if (isFadingOut)
                {
                    currentOpacity -= fadeStep;
                    if (currentOpacity <= 0)
                    {
                        currentOpacity = 0;
                        fadeTimer.Stop();
                        parentWindow.LogToFile($"SplashScreenWindow fade-out complete at {DateTime.Now:HH:mm:ss.fff}, invoking onComplete.");
                        onComplete?.Invoke();
                        Close();
                    }
                }

                Opacity = currentOpacity;
                parentWindow.LogToFile($"FadeTimer_Tick at {DateTime.Now:HH:mm:ss.fff}: isFadingIn={isFadingIn}, isFadingOut={isFadingOut}, currentOpacity={currentOpacity}, Window Opacity={Opacity}");
            }

            private void ProgressTimer_Tick()
            {
                progressValue++;
                if (progressValue >= 100)
                {
                    progressValue = 100;
                    progressTimer.Stop();
                    if (!isLaunchPhase)
                    {
                        parentWindow.Dispatcher.Invoke(() =>
                        {
                            parentWindow.Visibility = Visibility.Visible;
                            parentWindow.Topmost = true;
                            parentWindow.Activate();
                            parentWindow.Focus();
                            parentWindow.LogToFile($"MainWindow made visible and focused before splash screen fade-out (exit phase) at {DateTime.Now:HH:mm:ss.fff}.");

                            var fadeOutAnimation = new DoubleAnimation
                            {
                                From = 1.0,
                                To = 0.0,
                                Duration = TimeSpan.FromSeconds(FadeOutDurationSeconds)
                            };
                            var fadeInAnimation = new DoubleAnimation
                            {
                                From = 0.0,
                                To = 1.0,
                                Duration = TimeSpan.FromSeconds(FadeOutDurationSeconds)
                            };

                            this.BeginAnimation(OpacityProperty, fadeOutAnimation);
                            parentWindow.BeginAnimation(OpacityProperty, fadeInAnimation);

                            isFadingOut = true;
                            currentOpacity = 1;
                            parentWindow.LogToFile($"Starting T3 cross-fade at {DateTime.Now:HH:mm:ss.fff}: SplashScreenWindow fade-out, MainWindow fade-in for exit phase.");
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
                                Duration = TimeSpan.FromSeconds(FadeOutDurationT2)
                            };
                            fadeOutAnimation.Completed += (s, e) =>
                            {
                                parentWindow.LogToFile($"SplashScreenWindow fade-out complete for launch phase (Transition 2) at {DateTime.Now:HH:mm:ss.fff}.");
                                onComplete?.Invoke();
                            };
                            this.BeginAnimation(OpacityProperty, fadeOutAnimation);
                            parentWindow.LogToFile($"Starting fade-out for launch phase (Transition 2) at {DateTime.Now:HH:mm:ss.fff}.");
                        });
                    }
                }
                UpdateUI();
            }

            private void UpdateUI()
            {
                double progressX = (screenWidthPhysical - progressDiameter) / 2 / dpiScaleFactor;
                double progressY = (screenHeightPhysical * BarPositionPercentage - progressDiameter - screenHeightPhysical * ProgressBarOffsetPercentage) / dpiScaleFactor;

                double baseArcCenterRadius = screenWidthPhysical * BaseCircleRadiusPercentage;
                double baseArcCenterDiameter = baseArcCenterRadius * 2 / dpiScaleFactor;
                double shadowArcThickness = BaseShadowRingThickness / dpiScaleFactor;
                double featherDistance = BaseCircleFeatherDistance / dpiScaleFactor;
                double totalDiameter = baseArcCenterDiameter + 2 * featherDistance + 2 * shadowArcThickness;
                double innerDiameter = baseArcCenterDiameter - 2 * shadowArcThickness;
                double outerDiameter = baseArcCenterDiameter + 2 * shadowArcThickness;

                shadowArcEllipse.Width = totalDiameter;
                shadowArcEllipse.Height = totalDiameter;
                Canvas.SetLeft(shadowArcEllipse, progressX + (progressDiameter / dpiScaleFactor - totalDiameter) / 2);
                Canvas.SetTop(shadowArcEllipse, progressY + (progressDiameter / dpiScaleFactor - totalDiameter) / 2);

                var gradientBrush = new RadialGradientBrush
                {
                    GradientOrigin = new System.Windows.Point(0.5, 0.5),
                    Center = new System.Windows.Point(0.5, 0.5),
                    RadiusX = 0.5,
                    RadiusY = 0.5
                };
                double innerRadius = innerDiameter / totalDiameter / 2;
                double peakRadius = baseArcCenterDiameter / totalDiameter / 2;
                double featherStartRadius = (innerDiameter - featherDistance) / totalDiameter / 2;
                double featherEndRadius = (outerDiameter + featherDistance) / totalDiameter / 2;

                gradientBrush.GradientStops = new GradientStopCollection
                {
                    new GradientStop(Colors.Transparent, 0.0),
                    new GradientStop(Colors.Transparent, innerRadius),
                    new GradientStop(Color.FromArgb((byte)(BaseCircleOpacity * 255), 0, 0, 0), peakRadius),
                    new GradientStop(Colors.Transparent, featherEndRadius)
                };
                shadowArcEllipse.Fill = gradientBrush;

                int sweepAngle = (int)(progressValue * 3.6);
                if (ProgressArcGlowEnabled && glowArcPath != null)
                {
                    var glowGeometry = CreateArcGeometry(progressX, progressY, progressDiameter / dpiScaleFactor, -90, sweepAngle);
                    glowArcPath.Data = glowGeometry;
                }
                var progressGeometry = CreateArcGeometry(progressX, progressY, progressDiameter / dpiScaleFactor, -90, sweepAngle);
                progressArcPath.Data = progressGeometry;

                int barY = (int)(screenHeightPhysical * BarPositionPercentage / dpiScaleFactor);
                darkenedBar.Width = screenWidthPhysical / dpiScaleFactor;
                darkenedBar.Height = barHeight / dpiScaleFactor;
                Canvas.SetLeft(darkenedBar, 0);
                Canvas.SetTop(darkenedBar, barY);
                var gradientBrushBar = new LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 0.5),
                    EndPoint = new System.Windows.Point(1, 0.5)
                };
                float fadeStart = (float)(0.5 - (FadeStartPercentage / 100.0));
                float fadeEnd = (float)(0.5 + (FadeStartPercentage / 100.0));
                gradientBrushBar.GradientStops = new GradientStopCollection
                {
                    new GradientStop(System.Windows.Media.Color.FromArgb((byte)(FadeEndOpacity * 255), 0, 0, 0), 0.0),
                    new GradientStop(System.Windows.Media.Color.FromArgb((byte)(BarBaseOpacity * 255), 0, 0, 0), fadeStart),
                    new GradientStop(System.Windows.Media.Color.FromArgb((byte)(BarBaseOpacity * 255), 0, 0, 0), fadeEnd),
                    new GradientStop(System.Windows.Media.Color.FromArgb((byte)(FadeEndOpacity * 255), 0, 0, 0), 1.0)
                };
                darkenedBar.Fill = gradientBrushBar;

                Canvas.SetLeft(gameNameText, (screenWidthPhysical / dpiScaleFactor - gameNameText.ActualWidth) / 2);
                Canvas.SetTop(gameNameText, barY + (barHeight / dpiScaleFactor - gameNameText.ActualHeight) / 2);

                double progressFontSize = (progressDiameter * ProgressTextFontSizePercentage) / dpiScaleFactor;
                progressText.FontSize = progressFontSize;
                progressText.Text = $"{progressValue}%";
                double textX = progressX + (progressDiameter / dpiScaleFactor - progressText.ActualWidth) / 2;
                double textY = progressY + (progressDiameter / dpiScaleFactor - progressText.ActualHeight) / 2;
                Canvas.SetLeft(progressText, textX);
                Canvas.SetTop(progressText, textY);

                if (progressValue == 0 || progressValue == 50 || progressValue == 100)
                {
                    if (progressValue != lastLoggedProgress)
                    {
                        parentWindow.LogToFile($"Progress Text Position (Physical Pixels) at {DateTime.Now:HH:mm:ss.fff}: textX={textX * dpiScaleFactor}, textY={textY * dpiScaleFactor}, textWidth={progressText.ActualWidth * dpiScaleFactor}, textHeight={progressText.ActualHeight * dpiScaleFactor}, ProgressValue={progressValue}%");
                        lastLoggedProgress = progressValue;
                    }
                }

                if (!hasLoggedPositions)
                {
                    parentWindow.LogToFile($"Bar Position (Physical Pixels) at {DateTime.Now:HH:mm:ss.fff}: barY={barY * dpiScaleFactor}, barHeight={barHeight}, BottomEdge={(barY + barHeight / dpiScaleFactor) * dpiScaleFactor}, ScreenHeightPhysical={screenHeightPhysical}");
                    parentWindow.LogToFile($"Progress Meter Position (Physical Pixels) at {DateTime.Now:HH:mm:ss.fff}: progressX={progressX * dpiScaleFactor}, progressY={progressY * dpiScaleFactor}, progressDiameter={progressDiameter}, baseShadowArcCenterDiameter={baseArcCenterDiameter * dpiScaleFactor}, baseShadowArcThickness={shadowArcThickness * dpiScaleFactor}");
                    hasLoggedPositions = true;
                }
            }

            private PathGeometry CreateArcGeometry(double x, double y, double diameter, double startAngle, double sweepAngle)
            {
                var geometry = new PathGeometry();
                var figures = new PathFigureCollection();
                var figure = new PathFigure();

                double centerX = x + diameter / 2;
                double centerY = y + diameter / 2;
                double radius = diameter / 2;
                double startAngleRad = startAngle * Math.PI / 180;
                double sweepAngleRad = sweepAngle * Math.PI / 180;

                double startX = centerX + radius * Math.Cos(startAngleRad);
                double startY = centerY + radius * Math.Sin(startAngleRad);
                figure.StartPoint = new System.Windows.Point(startX, startY);

                double endAngleRad = (startAngle + sweepAngle) * Math.PI / 180;
                double endX = centerX + radius * Math.Cos(endAngleRad);
                double endY = centerY + radius * Math.Sin(endAngleRad);

                var arcSegment = new ArcSegment
                {
                    Point = new System.Windows.Point(endX, endY),
                    Size = new System.Windows.Size(radius, radius),
                    SweepDirection = SweepDirection.Clockwise,
                    IsLargeArc = sweepAngle > 180
                };
                figure.Segments.Add(arcSegment);
                figures.Add(figure);
                geometry.Figures = figures;

                return geometry;
            }
        }
    }
}