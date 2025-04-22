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

        // Configuration Parameters (Adjust these to tweak the layout)
        private const double BarHeightPercentage = 0.09; // Bar height as a percentage of screen height (default 9%)
        private const double BarPositionPercentage = 0.88; // Bar top position as a percentage of screen height from the top (default 88%, so bar bottom is at 97%)
        private const float BaseFontSize = 48f; // Base font size for game title in physical points at 1080p
        private const float BarBaseOpacity = 0.8f; // Base opacity of the title bar (default 80%)
        private const double FadeStartPercentage = 30.0; // Percentage of horizontal width from center where fade starts (default 30%)
        private const float FadeEndOpacity = 0.6f; // Opacity at the edges of the screen (default 60%)
        private const double ProgressBarOffsetPercentage = 0.02; // Offset between bar top and progress bar bottom as a percentage of screen height (default 2%)
        private const double ProgressBarDiameterPercentage = 0.06; // Progress bar diameter as a percentage of screen width (default 6%)
        private const float FadeDurationSeconds = 0.6f; // Total duration for fade transitions (0.6 seconds)
        private const float FadeInDurationSeconds = FadeDurationSeconds / 2; // Fade-in duration (0.3 seconds)
        private const float FadeOutDurationSeconds = FadeDurationSeconds / 2; // Fade-out duration (0.3 seconds)
        // Progress Meter Configuration
        private const double BaseCircleRadiusPercentage = 0.06; // Base shadow arc radius as a percentage of screen width (default 6%, defines center radius)
        private const float BaseCircleOpacity = 0.18f; // Opacity of the base shadow arc (default 18%)
        private const float BaseCircleFeatherDistance = 12.0f; // Feathering distance for base shadow arc edges in physical pixels (default 12)
        private const float BaseShadowRingThickness = 2.0f; // Thickness of the base shadow arc in physical pixels (default 2, thicker than ProgressArcThickness)
        private const float ProgressArcThickness = 5.0f; // Thickness of the cyan progress arc in physical pixels (default 5)
        private const float ProgressTextFontSizePercentage = 0.30f; // Progress text font size as a percentage of progress diameter in physical pixels (default 30%)
        private const float ProgressTextOutlineThickness = 1.5f; // Thickness of the text outline in physical pixels (default 1.5)
        private const bool ProgressArcGlowEnabled = true; // Enable/disable glow effect for cyan arc (default true)
        private const float ProgressArcGlowOpacity = 0.5f; // Opacity of the glow arc (default 50%)
        private const float ProgressArcGlowThicknessMultiplier = 1.5f; // Multiplier for glow arc thickness relative to ProgressArcThickness (default 1.5x)
        private const int ProgressDurationMs = 3000; // 3 seconds
        private const int ProgressUpdateIntervalMs = 30; // Update every 30ms for 1% increments
        private const int FadeUpdateIntervalMs = 50; // Update interval for fade animations
        // End Configuration Parameters

        private class SplashScreenWindow : Window
        {
            private readonly MainWindow parentWindow;
            private readonly Game game;
            private readonly double dpiScaleFactor;
            private readonly bool isLaunchPhase; // Flag to indicate if this is during game launch or exit
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

                // Window properties
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                ShowInTaskbar = false;
                Topmost = true; // Ensure the splash screen is on top of all other windows
                Background = System.Windows.Media.Brushes.Black;
                Width = SystemParameters.PrimaryScreenWidth;
                Height = SystemParameters.PrimaryScreenHeight;
                Left = SystemParameters.PrimaryScreenWidth / dpiScaleFactor * 0; // Monitor 1 (primary)
                Top = 0;
                Visibility = Visibility.Hidden; // Start hidden to prevent flash
                parentWindow.LogToFile($"SplashScreenWindow created. Initial Visibility: {Visibility}, isLaunchPhase: {isLaunchPhase}, startFadeTimerOnConstruction: {startFadeTimerOnConstruction}");

                // Initialize state
                currentOpacity = 0;
                isFadingIn = true;
                isFadingOut = false;
                progressValue = 0;
                hasLoggedPositions = false;
                lastLoggedProgress = -1;

                // Calculate physical resolution (logical * DPI scaling)
                screenWidthPhysical = (int)(Width * dpiScaleFactor);
                screenHeightPhysical = (int)(Height * dpiScaleFactor);

                // Calculate resolution-based scaling factor (relative to 1080p in physical pixels)
                scalingFactor = (double)screenHeightPhysical / 1080;

                // Calculate UI element sizes in physical pixels
                barHeight = (int)(screenHeightPhysical * BarHeightPercentage);
                progressDiameter = (int)(screenWidthPhysical * ProgressBarDiameterPercentage);
                fontSize = BaseFontSize * (float)scalingFactor / (float)dpiScaleFactor; // DPI compensation

                // Create the main canvas
                canvas = new Canvas();
                Content = canvas;

                // Load and display the splash image
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

                // Create the shadow arc ellipse with a RadialGradientBrush
                shadowArcEllipse = new System.Windows.Shapes.Ellipse();
                RenderOptions.SetEdgeMode(shadowArcEllipse, EdgeMode.Aliased);
                canvas.Children.Add(shadowArcEllipse);

                // Create the glow arc path (if enabled)
                if (ProgressArcGlowEnabled)
                {
                    glowArcPath = new System.Windows.Shapes.Path
                    {
                        Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)(ProgressArcGlowOpacity * 255), 0, 255, 255)),
                        StrokeThickness = ProgressArcThickness / dpiScaleFactor // Logical pixels
                    };
                    RenderOptions.SetEdgeMode(glowArcPath, EdgeMode.Aliased);
                    canvas.Children.Add(glowArcPath);
                }

                // Create the progress arc path
                progressArcPath = new System.Windows.Shapes.Path
                {
                    Stroke = System.Windows.Media.Brushes.Cyan,
                    StrokeThickness = ProgressArcThickness / dpiScaleFactor // Logical pixels
                };
                RenderOptions.SetEdgeMode(progressArcPath, EdgeMode.Aliased);
                canvas.Children.Add(progressArcPath);

                // Create the darkened bar
                darkenedBar = new System.Windows.Shapes.Rectangle();
                canvas.Children.Add(darkenedBar);

                // Create the game name text
                gameNameText = new System.Windows.Controls.TextBlock
                {
                    Foreground = System.Windows.Media.Brushes.White,
                    FontFamily = new System.Windows.Media.FontFamily("Microsoft Sans Serif"),
                    FontSize = fontSize,
                    TextAlignment = TextAlignment.Center
                };
                gameNameText.Text = game.DisplayName;
                canvas.Children.Add(gameNameText);

                // Create the progress text with outline using DropShadowEffect
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

                // Force layout pass to get correct ActualWidth and ActualHeight
                double progressFontSize = (progressDiameter * ProgressTextFontSizePercentage) / dpiScaleFactor; // DPI compensation
                progressText.FontSize = progressFontSize;
                progressText.Text = "0%"; // Set initial text to ensure proper sizing
                progressText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                progressText.Arrange(new Rect(progressText.DesiredSize));

                gameNameText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                gameNameText.Arrange(new Rect(gameNameText.DesiredSize));

                // Initialize the UI elements before starting the fade-in
                UpdateUI();

                // Set the overlays to full visibility before the fade-in starts
                splashImageControl.Opacity = 1;
                shadowArcEllipse.Opacity = 1;
                if (glowArcPath != null) glowArcPath.Opacity = 1;
                progressArcPath.Opacity = 1;
                darkenedBar.Opacity = 1;
                gameNameText.Opacity = 1;
                progressText.Opacity = 1;

                // Setup timers but don't start the fade timer unless specified
                SetupTimers();

                // Handle window closing
                Closing += (s, e) =>
                {
                    fadeTimer?.Stop();
                    progressTimer?.Stop();
                    parentWindow.LogToFile("SplashScreenWindow closing. Timers stopped.");
                };
            }

            public void StartFadeTimer()
            {
                currentOpacity = 0; // Reset opacity to ensure proper fade-in
                isFadingIn = true; // Reset fading state
                Opacity = currentOpacity;
                parentWindow.LogToFile($"Starting fadeTimer. Initial Opacity: {Opacity}, isFadingIn: {isFadingIn}");
                fadeTimer.Start();
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
                    parentWindow.LogToFile($"Loaded splash screen image: {imagePath}");
                }
                else
                {
                    splashImageControl.Source = null;
                    parentWindow.LogToFile("No splash screen image found, using black background.");
                }
            }

            private void SetupTimers()
            {
                // Fade timer for fade-in and fade-out
                fadeTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(FadeUpdateIntervalMs)
                };
                fadeTimer.Tick += (s, e) => FadeTimer_Tick();
                if (startFadeTimerOnConstruction)
                {
                    parentWindow.LogToFile("Starting fadeTimer on construction.");
                    fadeTimer.Start();
                }
                else
                {
                    parentWindow.LogToFile("fadeTimer not started on construction; awaiting StartFadeTimer call.");
                }

                // Progress timer for radial progress bar (not started yet)
                progressTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(ProgressUpdateIntervalMs) // 30ms for 1% increments
                };
                progressTimer.Tick += (s, e) => ProgressTimer_Tick();
            }

            private void FadeTimer_Tick()
            {
                double fadeStep;
                if (isFadingIn)
                {
                    fadeStep = FadeUpdateIntervalMs / (FadeInDurationSeconds * 1000); // Use fade-in duration (0.3s)
                }
                else
                {
                    fadeStep = FadeUpdateIntervalMs / (FadeOutDurationSeconds * 1000); // Use fade-out duration (0.3s)
                }

                if (isFadingIn)
                {
                    currentOpacity += fadeStep;
                    if (currentOpacity >= 1)
                    {
                        currentOpacity = 1;
                        isFadingIn = false;
                        // Start the progress bar now that fade-in is complete
                        progressTimer.Start();
                        parentWindow.LogToFile("Fade-in complete. Starting progressTimer.");
                    }
                }
                else if (isFadingOut)
                {
                    currentOpacity -= fadeStep;
                    if (currentOpacity <= 0)
                    {
                        currentOpacity = 0;
                        fadeTimer.Stop();
                        parentWindow.LogToFile("SplashScreenWindow fade-out complete, invoking onComplete.");
                        onComplete?.Invoke();
                        Close();
                    }
                }

                // Apply the opacity to the entire window, which will affect all elements
                Opacity = currentOpacity;
                parentWindow.LogToFile($"FadeTimer_Tick: isFadingIn={isFadingIn}, isFadingOut={isFadingOut}, currentOpacity={currentOpacity}, Window Opacity={Opacity}");
            }

            private void ProgressTimer_Tick()
            {
                progressValue++;
                if (progressValue >= 100)
                {
                    progressValue = 100;
                    progressTimer.Stop();
                    // During exit phase, start the cross-fade
                    if (!isLaunchPhase)
                    {
                        parentWindow.Dispatcher.Invoke(() =>
                        {
                            parentWindow.Visibility = Visibility.Visible;
                            parentWindow.Topmost = true;
                            parentWindow.Activate();
                            parentWindow.Focus();
                            parentWindow.LogToFile("MainWindow made visible and focused before splash screen fade-out (exit phase).");

                            // Start cross-fade: fade out splash screen, fade in MainWindow
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

                            // Update state to reflect that we're fading out
                            isFadingOut = true;
                            currentOpacity = 1; // Ensure the timer continues the fade-out
                            parentWindow.LogToFile("Starting cross-fade: SplashScreenWindow fade-out, MainWindow fade-in for exit phase.");
                        });
                    }
                    else
                    {
                        isFadingOut = true; // Start fading out normally during launch phase
                        parentWindow.LogToFile("Starting fade-out for launch phase.");
                    }
                }
                UpdateUI();
            }

            private void UpdateUI()
            {
                // Calculate progress meter position and size (convert physical to logical pixels)
                double progressX = (screenWidthPhysical - progressDiameter) / 2 / dpiScaleFactor; // Center horizontally
                double progressY = (screenHeightPhysical * BarPositionPercentage - progressDiameter - screenHeightPhysical * ProgressBarOffsetPercentage) / dpiScaleFactor;

                // Update the shadow arc using RadialGradientBrush
                double baseArcCenterRadius = screenWidthPhysical * BaseCircleRadiusPercentage; // Radius in physical pixels
                double baseArcCenterDiameter = baseArcCenterRadius * 2 / dpiScaleFactor; // Diameter in logical pixels
                double shadowArcThickness = BaseShadowRingThickness / dpiScaleFactor; // Logical pixels
                double featherDistance = BaseCircleFeatherDistance / dpiScaleFactor; // Logical pixels
                double totalDiameter = baseArcCenterDiameter + 2 * featherDistance + 2 * shadowArcThickness; // Total diameter including thickness and feathering
                double innerDiameter = baseArcCenterDiameter - 2 * shadowArcThickness; // Inner diameter of the ring (centerline - thickness)
                double outerDiameter = baseArcCenterDiameter + 2 * shadowArcThickness; // Outer diameter of the ring (centerline + thickness)

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
                double peakRadius = baseArcCenterDiameter / totalDiameter / 2; // Centerline of the shadow ring
                double featherStartRadius = (innerDiameter - featherDistance) / totalDiameter / 2;
                double featherEndRadius = (outerDiameter + featherDistance) / totalDiameter / 2;

                gradientBrush.GradientStops = new GradientStopCollection
                {
                    new GradientStop(Colors.Transparent, 0.0), // Fully transparent at the center
                    new GradientStop(Colors.Transparent, innerRadius), // Start of the ring (inner edge)
                    new GradientStop(Color.FromArgb((byte)(BaseCircleOpacity * 255), 0, 0, 0), peakRadius), // Peak opacity at the centerline
                    new GradientStop(Colors.Transparent, featherEndRadius) // Fade to transparent at the outer feather edge
                };
                shadowArcEllipse.Fill = gradientBrush;

                // Update the progress arc
                int sweepAngle = (int)(progressValue * 3.6);
                if (ProgressArcGlowEnabled && glowArcPath != null)
                {
                    var glowGeometry = CreateArcGeometry(progressX, progressY, progressDiameter / dpiScaleFactor, -90, sweepAngle);
                    glowArcPath.Data = glowGeometry;
                }
                var progressGeometry = CreateArcGeometry(progressX, progressY, progressDiameter / dpiScaleFactor, -90, sweepAngle);
                progressArcPath.Data = progressGeometry;

                // Update the darkened bar
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

                // Update the game name text
                Canvas.SetLeft(gameNameText, (screenWidthPhysical / dpiScaleFactor - gameNameText.ActualWidth) / 2);
                Canvas.SetTop(gameNameText, barY + (barHeight / dpiScaleFactor - gameNameText.ActualHeight) / 2);

                // Update the progress text with outline
                double progressFontSize = (progressDiameter * ProgressTextFontSizePercentage) / dpiScaleFactor; // DPI compensation
                progressText.FontSize = progressFontSize;
                progressText.Text = $"{progressValue}%";
                double textX = progressX + (progressDiameter / dpiScaleFactor - progressText.ActualWidth) / 2;
                double textY = progressY + (progressDiameter / dpiScaleFactor - progressText.ActualHeight) / 2;
                Canvas.SetLeft(progressText, textX);
                Canvas.SetTop(progressText, textY);

                // Log progress text position only at specific intervals (0%, 50%, 100%)
                if (progressValue == 0 || progressValue == 50 || progressValue == 100)
                {
                    if (progressValue != lastLoggedProgress)
                    {
                        parentWindow.LogToFile($"Progress Text Position (Physical Pixels): textX={textX * dpiScaleFactor}, textY={textY * dpiScaleFactor}, textWidth={progressText.ActualWidth * dpiScaleFactor}, textHeight={progressText.ActualHeight * dpiScaleFactor}, ProgressValue={progressValue}%");
                        lastLoggedProgress = progressValue;
                    }
                }

                // Log positions in physical pixels
                if (!hasLoggedPositions)
                {
                    parentWindow.LogToFile($"Bar Position (Physical Pixels): barY={barY * dpiScaleFactor}, barHeight={barHeight}, BottomEdge={(barY + barHeight / dpiScaleFactor) * dpiScaleFactor}, ScreenHeightPhysical={screenHeightPhysical}");
                    parentWindow.LogToFile($"Progress Meter Position (Physical Pixels): progressX={progressX * dpiScaleFactor}, progressY={progressY * dpiScaleFactor}, progressDiameter={progressDiameter}, baseShadowArcCenterDiameter={baseArcCenterDiameter * dpiScaleFactor}, baseShadowArcThickness={shadowArcThickness * dpiScaleFactor}");
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