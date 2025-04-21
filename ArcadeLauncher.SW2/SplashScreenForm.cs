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
        private const double BarHeightPercentage = 0.09; // Bar height as a percentage of screen height (default 9%)
        private const double BarPositionPercentage = 0.88; // Bar top position as a percentage of screen height from the top (default 88%, so bar bottom is at 97%)
        private const float BaseFontSize = 32f; // Base font size in points before scaling (default 32pt at 1080p)
        private const float BarBaseOpacity = 0.8f; // Base opacity of the title bar (default 80%)
        private const double FadeStartPercentage = 30.0; // Percentage of horizontal width from center where fade starts (default 30%)
        private const float FadeEndOpacity = 0.6f; // Opacity at the edges of the screen (default 60%)
        private const double ProgressBarOffsetPercentage = 0.02; // Offset between bar top and progress bar bottom as a percentage of screen height (default 2%)
        private const double ProgressBarDiameterPercentage = 0.06; // Progress bar diameter as a percentage of screen width (default 6%)
        private const float FadeDurationSeconds = 0.6f; // Duration of fade-in and fade-out in seconds (default 0.6 seconds)
        // Progress Meter Configuration
        private const double BaseCircleDiameterPercentage = 0.06; // Base shadow arc diameter as a percentage of screen width (default 6%, defines center radius)
        private const float BaseCircleOpacity = 0.18f; // Opacity of the base shadow arc (default 18%)
        private const float BaseCircleFeatherDistance = 12.0f; // Feathering distance for base shadow arc edges in pixels (default 12, scaled by DPI)
        private const float BaseShadowRingThickness = 2.0f; // Thickness of the base shadow arc in pixels (default 2, scaled by DPI, thicker than ProgressArcThickness)
        private const float ProgressArcThickness = 5.0f; // Thickness of the cyan progress arc in pixels (default 5, scaled by DPI)
        private const float ProgressTextFontSizePercentage = 0.18f; // Progress text font size as a percentage of progress diameter (default 18%)
        private const float ProgressTextOutlineThickness = 1.5f; // Thickness of the text outline in pixels (default 1.5, scaled by DPI)
        private const bool ProgressArcGlowEnabled = true; // Enable/disable glow effect for cyan arc (default true)
        private const float ProgressArcGlowOpacity = 0.5f; // Opacity of the glow arc (default 50%)
        private const float ProgressArcGlowThicknessMultiplier = 1.5f; // Multiplier for glow arc thickness relative to ProgressArcThickness (default 1.5x)
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
            screenWidthPhysical = primaryScreen.Bounds.Width;
            screenHeightPhysical = primaryScreen.Bounds.Height;

            // Calculate logical resolution for reference
            int screenWidthLogical = (int)(screenWidthPhysical / dpiScalingFactor);
            int screenHeightLogical = (int)(screenHeightPhysical / dpiScalingFactor);

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

            fontSize = (float)(BaseFontSize * scalingFactor / dpiScalingFactor); // Compensate for DPI scaling
            Logger.LogToFile($"Font Size Calculation (Logical Pixels): BaseFontSize={BaseFontSize}, scalingFactor={scalingFactor}, dpiScalingFactor={dpiScalingFactor}, fontSize={fontSize}");

            // Start with 0 opacity for fade-in
            Opacity = 0;
            currentOpacity = 0;
            isFadingIn = true;
            isFadingOut = false;
            progressValue = 0;
            hasLoggedPositions = false;
            lastLoggedProgress = -1;
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
            double fadeStep = FadeIntervalMs / (FadeDurationSeconds * 1000); // FadeDurationSeconds is in seconds

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
                isFadingOut = true; // Start fading out immediately
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

            // Calculate progress meter position and size
            int progressX = (screenWidthPhysical - progressDiameter) / 2;
            int progressY = (int)(screenHeightPhysical * BarPositionPercentage) - progressDiameter - (int)(screenHeightPhysical * ProgressBarOffsetPercentage);

            // Draw the base shadow arc (thick, 360 degrees, radially feathered edges)
            int baseArcCenterDiameter = (int)(screenWidthPhysical * BaseCircleDiameterPercentage);
            float shadowArcThickness = BaseShadowRingThickness * (float)dpiScalingFactor;
            float featherDistance = BaseCircleFeatherDistance * (float)dpiScalingFactor;

            // Calculate the total width (thickness + feathering) and the number of steps for smoother feathering
            float totalWidth = shadowArcThickness + (2 * featherDistance);
            float stepSize = 0.1f * (float)dpiScalingFactor; // Step size of ~0.1 pixel, scaled by DPI
            int featherSteps = (int)Math.Ceiling(totalWidth / stepSize / 2); // Number of steps on each side
            float diameterStep = stepSize; // Diameter change per step
            float opacityStep = BaseCircleOpacity / featherSteps; // Linear opacity step for reference

            // Draw concentric arcs for the shadow ring with smoother feathering
            int baseArcX = progressX + (progressDiameter - baseArcCenterDiameter) / 2;
            int baseArcY = progressY + (progressDiameter - baseArcCenterDiameter) / 2;
            for (int i = -featherSteps; i <= featherSteps; i++)
            {
                float currentDiameter = baseArcCenterDiameter + (i * diameterStep * 2);
                if (currentDiameter <= 0) continue; // Skip if diameter becomes negative

                // Use a cosine-based easing function for smoother opacity transitions
                float t = (float)Math.Abs(i) / featherSteps; // Normalized position (0 at center, 1 at edge)
                float easedT = (float)(1.0 - Math.Cos(t * Math.PI)) / 2.0f; // Cosine easing (0 to 1)
                float currentOpacity = BaseCircleOpacity * (1.0f - easedT); // Apply easing to opacity
                if (currentOpacity < 0) currentOpacity = 0;

                int currentX = (int)(progressX + (progressDiameter - currentDiameter) / 2);
                int currentY = (int)(progressY + (progressDiameter - currentDiameter) / 2);
                using (var shadowPen = new Pen(Color.FromArgb((int)(currentOpacity * 255), 0, 0, 0), diameterStep * 4.0f))
                {
                    shadowPen.LineJoin = LineJoin.Round;
                    shadowPen.StartCap = LineCap.Round;
                    shadowPen.EndCap = LineCap.Round;
                    g.DrawArc(shadowPen, currentX, currentY, (int)currentDiameter, (int)currentDiameter, 0, 360);
                }
            }

            // Draw the progress arc
            float arcThickness = ProgressArcThickness * (float)dpiScalingFactor;
            int sweepAngle = (int)(progressValue * 3.6); // 360 degrees for 100%
            if (ProgressArcGlowEnabled)
            {
                // Draw glow arc (semi-transparent, wider)
                float glowThickness = arcThickness * ProgressArcGlowThicknessMultiplier;
                using (var glowPen = new Pen(Color.FromArgb((int)(ProgressArcGlowOpacity * 255), 0, 255, 255), glowThickness))
                {
                    g.DrawArc(glowPen, progressX, progressY, progressDiameter, progressDiameter, -90, sweepAngle);
                }
            }
            // Draw main cyan arc
            using (var arcPen = new Pen(Color.Cyan, arcThickness))
            {
                g.DrawArc(arcPen, progressX, progressY, progressDiameter, progressDiameter, -90, sweepAngle);
            }

            // Draw the darkened bar with horizontal fade-out
            int barY = (int)(screenHeightPhysical * BarPositionPercentage);
            using (var brush = new LinearGradientBrush(
                new Rectangle(0, barY, screenWidthPhysical, barHeight),
                Color.Black,
                Color.Black,
                LinearGradientMode.Horizontal))
            {
                // Define the gradient with a ColorBlend for symmetric fade-out
                float fadeStart = (float)(0.5 - (FadeStartPercentage / 100.0)); // Start of fade as a fraction of width from center
                float fadeEnd = (float)(0.5 + (FadeStartPercentage / 100.0)); // End of fade as a fraction of width from center
                var blend = new ColorBlend
                {
                    Positions = new[] { 0.0f, fadeStart, fadeEnd, 1.0f },
                    Colors = new[]
                    {
                        Color.FromArgb((int)(FadeEndOpacity * 255), 0, 0, 0), // Edge (left)
                        Color.FromArgb((int)(BarBaseOpacity * 255), 0, 0, 0), // Start of fade (left)
                        Color.FromArgb((int)(BarBaseOpacity * 255), 0, 0, 0), // Start of fade (right)
                        Color.FromArgb((int)(FadeEndOpacity * 255), 0, 0, 0)  // Edge (right)
                    }
                };
                brush.InterpolationColors = blend;
                g.FillRectangle(brush, 0, barY, screenWidthPhysical, barHeight);
            }

            // Draw the game name (Title Bar text)
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

            // Draw the percentage text with outline
            string progressText = $"{progressValue}%";
            float progressFontSize = (float)((progressDiameter * ProgressTextFontSizePercentage) / dpiScalingFactor); // Compensate for DPI scaling
            using (var font = new Font("Microsoft Sans Serif", progressFontSize))
            {
                var textSize = g.MeasureString(progressText, font);
                float textX = progressX + (progressDiameter - textSize.Width) / 2;
                float textY = progressY + (progressDiameter - textSize.Height) / 2;

                float outlineThickness = ProgressTextOutlineThickness * (float)dpiScalingFactor;
                // Draw outline by drawing text multiple times in black at 8 points around the text
                using (var outlineBrush = new SolidBrush(Color.Black))
                {
                    float diagonalOffset = outlineThickness * (float)Math.Sqrt(2) / 2; // Adjust for diagonal distance
                    // Cardinal directions
                    g.DrawString(progressText, font, outlineBrush, textX - outlineThickness, textY); // Left
                    g.DrawString(progressText, font, outlineBrush, textX + outlineThickness, textY); // Right
                    g.DrawString(progressText, font, outlineBrush, textX, textY - outlineThickness); // Up
                    g.DrawString(progressText, font, outlineBrush, textX, textY + outlineThickness); // Down
                    // Diagonal directions
                    g.DrawString(progressText, font, outlineBrush, textX - diagonalOffset, textY - diagonalOffset); // Up-Left
                    g.DrawString(progressText, font, outlineBrush, textX + diagonalOffset, textY - diagonalOffset); // Up-Right
                    g.DrawString(progressText, font, outlineBrush, textX - diagonalOffset, textY + diagonalOffset); // Down-Left
                    g.DrawString(progressText, font, outlineBrush, textX + diagonalOffset, textY + diagonalOffset); // Down-Right
                }
                // Draw main white text
                using (var textBrush = new SolidBrush(Color.White))
                {
                    g.DrawString(progressText, font, textBrush, textX, textY);
                }

                // Log progress text position only at specific intervals (0%, 50%, 100%)
                if (progressValue == 0 || progressValue == 50 || progressValue == 100)
                {
                    if (progressValue != lastLoggedProgress)
                    {
                        Logger.LogToFile($"Progress Text Position (Physical Pixels): textX={textX}, textY={textY}, textWidth={textSize.Width}, textHeight={textSize.Height}, ProgressValue={progressValue}%");
                        lastLoggedProgress = progressValue;
                    }
                }
            }

            // Log positions in physical pixels (since Graphics treats them as physical)
            if (!hasLoggedPositions)
            {
                Logger.LogToFile($"Bar Position (Physical Pixels): barY={barY}, barHeight={barHeight}, BottomEdge={barY + barHeight}, ScreenHeightPhysical={screenHeightPhysical}");
                Logger.LogToFile($"Progress Meter Position (Physical Pixels): progressX={progressX}, progressY={progressY}, progressDiameter={progressDiameter}, baseShadowArcCenterDiameter={baseArcCenterDiameter}, baseShadowArcThickness={shadowArcThickness}");
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