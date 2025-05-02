using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.IO;
using System.Windows.Shapes;
using ArcadeLauncher.Core;

namespace ArcadeLauncher.SW3
{
    public partial class MainWindow
    {
        private const double BarHeightPercentage = 0.09;
        private const double BarPositionPercentage = 0.88;
        private const float BaseFontSize = 48.0F;
        private const float BarBaseOpacity = 0.8F;
        private const double FadeStartPercentage = 30.0;
        private const float FadeEndOpacity = 0.6F;
        private const double ProgressBarOffsetPercentage = 0.02;
        private const double ProgressBarDiameterPercentage = 0.06;
        private const float BaseCircleRadiusPercentage = 0.06F;
        private const float BaseCircleOpacity = 0.18F;
        private const float BaseCircleFeatherDistance = 12.0F;
        private const float BaseShadowRingThickness = 2.0F;
        private const float ProgressArcThickness = 5.0F;
        private const float ProgressTextFontSizePercentage = 0.30F;
        private const float ProgressTextOutlineThickness = 1.5F;
        private const bool ProgressArcGlowEnabled = true;
        private const float ProgressArcGlowOpacity = 0.5F;

        private void InitializeUI(SplashScreenWindow splashScreen, Canvas canvas)
        {
            var screenWidthPhysical = (int)(splashScreen.Width * dpiScaleFactor);
            var screenHeightPhysical = (int)(splashScreen.Height * dpiScaleFactor);
            splashScreen.screenWidthPhysical = screenWidthPhysical;
            splashScreen.screenHeightPhysical = screenHeightPhysical;
            splashScreen.scalingFactor = (double)screenHeightPhysical / 1080;
            splashScreen.barHeight = (int)(screenHeightPhysical * BarHeightPercentage);
            splashScreen.progressDiameter = (int)(screenWidthPhysical * ProgressBarDiameterPercentage);
            splashScreen.fontSize = BaseFontSize * (float)splashScreen.scalingFactor / (float)dpiScaleFactor;

            splashScreen.canvas = canvas;
            splashScreen.splashImageControl = new System.Windows.Controls.Image
            {
                Width = screenWidthPhysical / dpiScaleFactor,
                Height = screenHeightPhysical / dpiScaleFactor,
                Stretch = Stretch.Fill
            };
            RenderOptions.SetBitmapScalingMode(splashScreen.splashImageControl, BitmapScalingMode.HighQuality);
            LoadSplashImage(splashScreen);
            Canvas.SetLeft(splashScreen.splashImageControl, 0);
            Canvas.SetTop(splashScreen.splashImageControl, 0);
            canvas.Children.Add(splashScreen.splashImageControl);

            splashScreen.shadowArcEllipse = new System.Windows.Shapes.Ellipse();
            RenderOptions.SetEdgeMode(splashScreen.shadowArcEllipse, EdgeMode.Aliased);
            canvas.Children.Add(splashScreen.shadowArcEllipse);

            if (ProgressArcGlowEnabled)
            {
                splashScreen.glowArcPath = new System.Windows.Shapes.Path
                {
                    Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)(ProgressArcGlowOpacity * 255), 0, 255, 255)),
                    StrokeThickness = ProgressArcThickness / dpiScaleFactor
                };
                RenderOptions.SetEdgeMode(splashScreen.glowArcPath, EdgeMode.Aliased);
                canvas.Children.Add(splashScreen.glowArcPath);
            }

            splashScreen.progressArcPath = new System.Windows.Shapes.Path
            {
                Stroke = System.Windows.Media.Brushes.Cyan,
                StrokeThickness = ProgressArcThickness / dpiScaleFactor
            };
            RenderOptions.SetEdgeMode(splashScreen.progressArcPath, EdgeMode.Aliased);
            canvas.Children.Add(splashScreen.progressArcPath);

            splashScreen.darkenedBar = new System.Windows.Shapes.Rectangle();
            canvas.Children.Add(splashScreen.darkenedBar);

            splashScreen.gameNameText = new System.Windows.Controls.TextBlock
            {
                Foreground = System.Windows.Media.Brushes.White,
                FontFamily = new System.Windows.Media.FontFamily("Microsoft Sans Serif"),
                FontSize = splashScreen.fontSize,
                TextAlignment = TextAlignment.Center
            };
            splashScreen.gameNameText.Text = splashScreen.game.DisplayName;
            canvas.Children.Add(splashScreen.gameNameText);

            splashScreen.progressText = new System.Windows.Controls.TextBlock
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
            splashScreen.progressText.Effect = shadowEffect;
            canvas.Children.Add(splashScreen.progressText);

            double progressFontSize = (splashScreen.progressDiameter * ProgressTextFontSizePercentage) / dpiScaleFactor;
            splashScreen.progressText.FontSize = progressFontSize;
            splashScreen.progressText.Text = "0%";
            splashScreen.progressText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            splashScreen.progressText.Arrange(new Rect(splashScreen.progressText.DesiredSize));

            splashScreen.gameNameText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            splashScreen.gameNameText.Arrange(new Rect(splashScreen.gameNameText.DesiredSize));

            UpdateUI(splashScreen);

            splashScreen.splashImageControl.Opacity = 1;
            splashScreen.shadowArcEllipse.Opacity = 1;
            if (splashScreen.glowArcPath != null) splashScreen.glowArcPath.Opacity = 1;
            splashScreen.progressArcPath.Opacity = 1;
            splashScreen.darkenedBar.Opacity = 1;
            splashScreen.gameNameText.Opacity = 1;
            splashScreen.progressText.Opacity = 1;
        }

        private void LoadSplashImage(SplashScreenWindow splashScreen)
        {
            string? imagePath = null;
            if (splashScreen.screenHeightPhysical >= 2160 && splashScreen.game.SplashScreenPath.ContainsKey("4k"))
            {
                imagePath = splashScreen.game.SplashScreenPath["4k"];
            }
            else if (splashScreen.screenHeightPhysical >= 1440 && splashScreen.game.SplashScreenPath.ContainsKey("1440p"))
            {
                imagePath = splashScreen.game.SplashScreenPath["1440p"];
            }
            else if (splashScreen.game.SplashScreenPath.ContainsKey("1080p"))
            {
                imagePath = splashScreen.game.SplashScreenPath["1080p"];
            }

            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                splashScreen.splashImageControl.Source = new BitmapImage(new Uri(imagePath, UriKind.Absolute));
                LogToFile($"Loaded splash screen image at {DateTime.Now:HH:mm:ss.fff}: {imagePath}");
            }
            else
            {
                splashScreen.splashImageControl.Source = null;
                LogToFile($"No splash screen image found at {DateTime.Now:HH:mm:ss.fff}, using black background.");
            }
        }

        private void UpdateUI(SplashScreenWindow splashScreen)
        {
            double progressX = (splashScreen.screenWidthPhysical - splashScreen.progressDiameter) / 2 / dpiScaleFactor;
            double progressY = (splashScreen.screenHeightPhysical * BarPositionPercentage - splashScreen.progressDiameter - splashScreen.screenHeightPhysical * ProgressBarOffsetPercentage) / dpiScaleFactor;

            double baseArcCenterRadius = splashScreen.screenWidthPhysical * BaseCircleRadiusPercentage;
            double baseArcCenterDiameter = baseArcCenterRadius * 2 / dpiScaleFactor;
            double shadowArcThickness = BaseShadowRingThickness / dpiScaleFactor;
            double featherDistance = BaseCircleFeatherDistance / dpiScaleFactor;
            double totalDiameter = baseArcCenterDiameter + 2 * featherDistance + 2 * shadowArcThickness;
            double innerDiameter = baseArcCenterDiameter - 2 * shadowArcThickness;
            double outerDiameter = baseArcCenterDiameter + 2 * shadowArcThickness;

            splashScreen.shadowArcEllipse.Width = totalDiameter;
            splashScreen.shadowArcEllipse.Height = totalDiameter;
            Canvas.SetLeft(splashScreen.shadowArcEllipse, progressX + (splashScreen.progressDiameter / dpiScaleFactor - totalDiameter) / 2);
            Canvas.SetTop(splashScreen.shadowArcEllipse, progressY + (splashScreen.progressDiameter / dpiScaleFactor - totalDiameter) / 2);

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
            splashScreen.shadowArcEllipse.Fill = gradientBrush;

            int sweepAngle = (int)(splashScreen.progressValue * 3.6);
            if (ProgressArcGlowEnabled && splashScreen.glowArcPath != null)
            {
                var glowGeometry = CreateArcGeometry(splashScreen, progressX, progressY, splashScreen.progressDiameter / dpiScaleFactor, -90, sweepAngle);
                splashScreen.glowArcPath.Data = glowGeometry;
            }
            var progressGeometry = CreateArcGeometry(splashScreen, progressX, progressY, splashScreen.progressDiameter / dpiScaleFactor, -90, sweepAngle);
            splashScreen.progressArcPath.Data = progressGeometry;

            int barY = (int)(splashScreen.screenHeightPhysical * BarPositionPercentage / dpiScaleFactor);
            splashScreen.darkenedBar.Width = splashScreen.screenWidthPhysical / dpiScaleFactor;
            splashScreen.darkenedBar.Height = splashScreen.barHeight / dpiScaleFactor;
            Canvas.SetLeft(splashScreen.darkenedBar, 0);
            Canvas.SetTop(splashScreen.darkenedBar, barY);
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
            splashScreen.darkenedBar.Fill = gradientBrushBar;

            Canvas.SetLeft(splashScreen.gameNameText, (splashScreen.screenWidthPhysical / dpiScaleFactor - splashScreen.gameNameText.ActualWidth) / 2);
            Canvas.SetTop(splashScreen.gameNameText, barY + (splashScreen.barHeight / dpiScaleFactor - splashScreen.gameNameText.ActualHeight) / 2);

            double progressFontSize = (splashScreen.progressDiameter * ProgressTextFontSizePercentage) / dpiScaleFactor;
            splashScreen.progressText.FontSize = progressFontSize;
            splashScreen.progressText.Text = $"{splashScreen.progressValue}%";
            double textX = progressX + (splashScreen.progressDiameter / dpiScaleFactor - splashScreen.progressText.ActualWidth) / 2;
            double textY = progressY + (splashScreen.progressDiameter / dpiScaleFactor - splashScreen.progressText.ActualHeight) / 2;
            Canvas.SetLeft(splashScreen.progressText, textX);
            Canvas.SetTop(splashScreen.progressText, textY);

            if (splashScreen.progressValue == 0 || splashScreen.progressValue == 50 || splashScreen.progressValue == 100)
            {
                if (splashScreen.progressValue != splashScreen.lastLoggedProgress)
                {
                    LogToFile($"Progress Text Position (Physical Pixels) at {DateTime.Now:HH:mm:ss.fff}: textX={textX * dpiScaleFactor}, textY={textY * dpiScaleFactor}, textWidth={splashScreen.progressText.ActualWidth * dpiScaleFactor}, textHeight={splashScreen.progressText.ActualHeight * dpiScaleFactor}, ProgressValue={splashScreen.progressValue}%");
                    splashScreen.lastLoggedProgress = splashScreen.progressValue;
                }
            }

            if (!splashScreen.hasLoggedPositions)
            {
                LogToFile($"Bar Position (Physical Pixels) at {DateTime.Now:HH:mm:ss.fff}: barY={barY * dpiScaleFactor}, barHeight={splashScreen.barHeight}, BottomEdge={(barY + splashScreen.barHeight / dpiScaleFactor) * dpiScaleFactor}, ScreenHeightPhysical={splashScreen.screenHeightPhysical}");
                LogToFile($"Progress Meter Position (Physical Pixels) at {DateTime.Now:HH:mm:ss.fff}: progressX={textX * dpiScaleFactor}, textY={textY * dpiScaleFactor}, progressDiameter={splashScreen.progressDiameter}, baseShadowArcCenterDiameter={baseArcCenterDiameter * dpiScaleFactor}, baseShadowArcThickness={shadowArcThickness * dpiScaleFactor}");
                splashScreen.hasLoggedPositions = true;
            }
        }

        private void UpdateProgressMeter(SplashScreenWindow splashScreen)
        {
            splashScreen.progressValue++;
            UpdateUI(splashScreen);
        }

        private PathGeometry CreateArcGeometry(SplashScreenWindow splashScreen, double x, double y, double diameter, double startAngle, double sweepAngle)
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