using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using ArcadeLauncher.Core;

namespace ArcadeLauncher.SW2
{
    public class SplashScreenForm : Form
    {
        // Configuration Parameters (Adjust these to tweak the layout)
        private const double BarHeightPercentage = 0.10; // Bar height as a percentage of screen height (default 15%)
        private const double BarPositionPercentage = 0.88; // Bar top position as a percentage of screen height from the top (default 85%, so bar bottom is at 100%)
        private const float BaseFontSize = 32f; // Base font size in points before scaling (default 36pt at 1080p)
        private const double ProgressBarOffsetPercentage = 0.02; // Offset between bar top and progress bar bottom as a percentage of screen height (default 1%)
        private const double ProgressBarDiameterPercentage = 0.06; // Progress bar diameter as a percentage of screen width (default 15%)
        // End Configuration Parameters

        private readonly Game game;
        private readonly Settings settings;
        private readonly string signalFilePath;
        private Image splashImage;
        private Timer fadeTimer;
        private Timer progressTimer;
        private double currentOpacity;
        private bool isFadingIn;
        private bool isFadingOut;
        private bool isPaused; // TEMPORARY PAUSE FOR TESTING - REMOVE LATER
        private int progressValue;
        private double scalingFactor;
        private double dpiScalingFactor;
        private int barHeight;
        private int progressDiameter;
        private float fontSize;
        private int screenHeightPhysical;
        private int screenWidthPhysical;
        private bool hasLoggedPositions; // To track if we've logged static positions
        private int lastLoggedProgress; // To track the last logged progress value
        private const int ProgressDurationMs = 3000; // 3 seconds
        private const int FadeIntervalMs = 50;
        private const double FadeStartPercentage = 0.1; // 10% of bar width for fade
        private const float FadeEndOpacity = 0f; // Fade to fully transparent at edges

        public SplashScreenForm(Game game, Settings settings)
        {
            this.game = game;
            this.settings = settings;
            signalFilePath = Path.Combine(ArcadeLauncher.Core.Program.InstallDir, "splash_active.txt");

            // Disable auto-scaling since we're handling DPI manually
            AutoScaleMode = AutoScaleMode.None;

            InitializeForm();
            LoadSplashImage();
            SetupTimers();
        }

        private void InitializeForm()
        {
            // Set form properties for full-screen display on Monitor 1
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.Black;
            DoubleBuffered = true;
            TopMost = true; // Ensure the form is on top of all other windows, including the taskbar

            // Position on Monitor 1 (primary screen) and cover the entire screen
            var primaryScreen = Screen.PrimaryScreen;
            Bounds = primaryScreen.Bounds; // Set bounds to cover the entire screen, including taskbar

            // Calculate DPI scaling factor
            using (Graphics g = this.CreateGraphics())
            {
                dpiScalingFactor = g.DpiY / 96.0; // Default DPI is 96 at 100% scaling
            }
            Logger.LogToFile($"SplashScreenForm DPI: DpiY={dpiScalingFactor * 96}, DpiScalingFactor={dpiScalingFactor}");

            // Physical resolution (Bounds reports physical pixels in this context)
            screenWidthPhysical = primaryScreen.Bounds.Width; // 1920 physical pixels
            screenHeightPhysical = primaryScreen.Bounds.Height; // 1080 physical pixels

            // Calculate logical resolution for reference
            int screenWidthLogical = (int)(screenWidthPhysical / dpiScalingFactor); // 1920 / 1.25 = 1536 logical pixels
            int screenHeightLogical = (int)(screenHeightPhysical / dpiScalingFactor); // 1080 / 1.25 = 864 logical pixels

            // Log dimensions
            Logger.LogToFile($"Screen Bounds (Physical Pixels): Width={screenWidthPhysical}, Height={screenHeightPhysical}");
            Logger.LogToFile($"Screen Bounds (Logical Pixels, calculated): Width={screenWidthLogical}, Height={screenHeightLogical}");
            Logger.LogToFile($"Working Area (Physical Pixels, reported): Width={primaryScreen.WorkingArea.Width}, Height={primaryScreen.WorkingArea.Height}");
            Logger.LogToFile($"Form ClientSize (Physical Pixels, reported): Width={ClientSize.Width}, Height={ClientSize.Height}");

            // Calculate resolution-based scaling factor (relative to 1080p in physical pixels)
            scalingFactor = (double)screenHeightPhysical / 1080;

            // Calculate UI element sizes in physical pixels (since Graphics treats them as physical)
            barHeight = (int)(screenHeightPhysical * BarHeightPercentage);
            Logger.LogToFile($"Bar Height Calculation (Physical Pixels): screenHeightPhysical={screenHeightPhysical}, BarHeightPercentage={BarHeightPercentage}, barHeight={barHeight}");

            progressDiameter = (int)(screenWidthPhysical * ProgressBarDiameterPercentage);
            Logger.LogToFile($"Progress Diameter Calculation (Physical Pixels): screenWidthPhysical={screenWidthPhysical}, ProgressBarDiameterPercentage={ProgressBarDiameterPercentage}, progressDiameter={progressDiameter}");

            fontSize = BaseFontSize * (float)scalingFactor;
            Logger.LogToFile($"Font Size Calculation (Physical Pixels): BaseFontSize={BaseFontSize}, scalingFactor={scalingFactor}, fontSize={fontSize}");

            // Start with 0 opacity for fade-in
            Opacity = 0;
            currentOpacity = 0;
            isFadingIn = true;
            isFadingOut = false;
            isPaused = false; // TEMPORARY PAUSE FOR TESTING - REMOVE LATER
            progressValue = 0;
            hasLoggedPositions = false;
            lastLoggedProgress = -1;

            // TEMPORARY PAUSE FOR TESTING - REMOVE LATER
            // Enable key input for the form to detect keypress
            KeyPreview = true;
            KeyDown += SplashScreenForm_KeyDown;
        }

        // TEMPORARY PAUSE FOR TESTING - REMOVE LATER
        private void SplashScreenForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (isPaused)
            {
                isPaused = false;
                isFadingOut = true;
                Logger.LogToFile("Key pressed - resuming splash screen fade-out.");
            }
        }

        private void LoadSplashImage()
        {
            // Select the appropriate image variant based on resolution (use physical pixels for image selection)
            var primaryScreen = Screen.PrimaryScreen;
            string imagePath = null;

            if (primaryScreen.Bounds.Height >= 2160 && game.SplashScreenPath.ContainsKey("4k"))
            {
                imagePath = game.SplashScreenPath["4k"];
            }
            else if (primaryScreen.Bounds.Height >= 1440 && game.SplashScreenPath.ContainsKey("1440p"))
            {
                imagePath = game.SplashScreenPath["1440p"];
            }
            else if (game.SplashScreenPath.ContainsKey("1080p"))
            {
                imagePath = game.SplashScreenPath["1080p"];
            }

            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                splashImage = Image.FromFile(imagePath);
                Logger.LogToFile($"Loaded splash screen image: {imagePath}");
            }
            else
            {
                // Fallback to a black background if no image is available
                splashImage = new Bitmap(screenWidthPhysical, screenHeightPhysical);
                using (var g = Graphics.FromImage(splashImage))
                {
                    g.Clear(Color.Black);
                }
                Logger.LogToFile("No splash screen image found, using black background.");
            }
        }

        private void SetupTimers()
        {
            // Fade timer for fade-in and fade-out
            fadeTimer = new Timer
            {
                Interval = FadeIntervalMs
            };
            fadeTimer.Tick += FadeTimer_Tick;
            fadeTimer.Start();

            // Progress timer for radial progress bar (not started yet)
            progressTimer = new Timer
            {
                Interval = ProgressDurationMs / 100 // Update every 1% of duration
            };
            progressTimer.Tick += ProgressTimer_Tick;
        }

        private void FadeTimer_Tick(object sender, EventArgs e)
        {
            double fadeStep = FadeIntervalMs / (settings.FadeDuration * 1000); // FadeDuration is in seconds

            if (isFadingIn)
            {
                currentOpacity += fadeStep;
                if (currentOpacity >= 1)
                {
                    currentOpacity = 1;
                    isFadingIn = false;
                    // Create signal file for SW3
                    try
                    {
                        File.WriteAllText(signalFilePath, "Splash screen active");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogToFile($"Failed to create splash signal file: {ex.Message}");
                    }
                    // Start the progress bar now that fade-in is complete
                    progressTimer.Start();
                }
            }
            else if (isFadingOut)
            {
                currentOpacity -= fadeStep;
                if (currentOpacity <= 0)
                {
                    currentOpacity = 0;
                    fadeTimer.Stop();
                    // Delete signal file for SW3
                    try
                    {
                        if (File.Exists(signalFilePath))
                        {
                            File.Delete(signalFilePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogToFile($"Failed to delete splash signal file: {ex.Message}");
                    }
                    Close();
                }
            }

            Opacity = currentOpacity;
        }

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            progressValue++;
            if (progressValue >= 100)
            {
                progressValue = 100;
                progressTimer.Stop();
                // TEMPORARY PAUSE FOR TESTING - REMOVE LATER
                // Pause instead of immediately starting fade-out
                isPaused = true;
                Logger.LogToFile("Progress bar reached 100% - pausing for keypress.");
            }
            Invalidate(); // Redraw the progress bar
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // Log the Graphics transform to check for unexpected scaling or offsets
            if (!hasLoggedPositions)
            {
                var transform = g.Transform;
                Logger.LogToFile($"Graphics Transform: ScaleX={transform.Elements[0]}, ScaleY={transform.Elements[3]}, OffsetX={transform.Elements[4]}, OffsetY={transform.Elements[5]}");
            }

            // Draw the splash screen image
            if (splashImage != null)
            {
                g.DrawImage(splashImage, 0, 0, screenWidthPhysical, screenHeightPhysical);
            }

            // Position the bar using BarPositionPercentage (in physical pixels)
            int barY = (int)(screenHeightPhysical * BarPositionPercentage);

            // Log positions in physical pixels (since Graphics treats them as physical)
            if (!hasLoggedPositions)
            {
                Logger.LogToFile($"Bar Position (Physical Pixels): barY={barY}, barHeight={barHeight}, BottomEdge={barY + barHeight}, ScreenHeightPhysical={screenHeightPhysical}");
            }

            // Draw the darkened bar
            using (var brush = new LinearGradientBrush(
                new Rectangle(0, barY, screenWidthPhysical, barHeight),
                Color.FromArgb(204, 0, 0, 0), // 80% opacity black
                Color.Transparent,
                LinearGradientMode.Horizontal))
            {
                // Set the gradient stops for fading edges
                brush.SetSigmaBellShape((float)FadeStartPercentage, FadeEndOpacity);
                brush.SetSigmaBellShape(1f - (float)FadeStartPercentage, FadeEndOpacity);
                g.FillRectangle(brush, 0, barY, screenWidthPhysical, barHeight);
            }

            // Draw the game name
            string gameName = game.DisplayName;
            using (var font = new Font("Microsoft Sans Serif", fontSize))
            using (var textBrush = new SolidBrush(Color.White))
            {
                var textSize = g.MeasureString(gameName, font);
                float textX = (screenWidthPhysical - textSize.Width) / 2;
                float textY = barY + (barHeight - textSize.Height) / 2;
                if (!hasLoggedPositions)
                {
                    Logger.LogToFile($"Game Name Position (Physical Pixels): textX={textX}, textY={textY}, textWidth={textSize.Width}, textHeight={textSize.Height}");
                }
                g.DrawString(gameName, font, textBrush, textX, textY);
            }

            // Draw the radial progress bar
            int progressX = (screenWidthPhysical - progressDiameter) / 2;
            int progressY = barY - progressDiameter - (int)(screenHeightPhysical * ProgressBarOffsetPercentage);
            if (!hasLoggedPositions)
            {
                Logger.LogToFile($"Progress Bar Position (Physical Pixels): progressX={progressX}, progressY={progressY}, progressDiameter={progressDiameter}");
            }
            using (var outlinePen = new Pen(Color.Cyan, 4))
            {
                // Draw the outline circle
                g.DrawEllipse(outlinePen, progressX, progressY, progressDiameter, progressDiameter);

                // Draw the filled progress
                using (var fillBrush = new SolidBrush(Color.FromArgb(128, 128, 128))) // Gray fill
                {
                    int sweepAngle = (int)(progressValue * 3.6); // 360 degrees for 100%
                    g.FillPie(fillBrush, progressX, progressY, progressDiameter, progressDiameter, -90, sweepAngle);
                }

                // Draw the percentage text
                string progressText = $"{progressValue}%";
                using (var font = new Font("Microsoft Sans Serif", fontSize * 0.5f))
                using (var textBrush = new SolidBrush(Color.White))
                {
                    var textSize = g.MeasureString(progressText, font);
                    float textX = progressX + (progressDiameter - textSize.Width) / 2;
                    float textY = progressY + (progressDiameter - textSize.Height) / 2;
                    // Log progress text position only at specific intervals (0%, 50%, 100%)
                    if (progressValue == 0 || progressValue == 50 || progressValue == 100)
                    {
                        if (progressValue != lastLoggedProgress)
                        {
                            Logger.LogToFile($"Progress Text Position (Physical Pixels): textX={textX}, textY={textY}, textWidth={textSize.Width}, textHeight={textSize.Height}, ProgressValue={progressValue}%");
                            lastLoggedProgress = progressValue;
                        }
                    }
                    g.DrawString(progressText, font, textBrush, textX, textY);
                }
            }

            // Mark that we've logged the static positions
            hasLoggedPositions = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                splashImage?.Dispose();
                fadeTimer?.Dispose();
                progressTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}