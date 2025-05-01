using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data; // For Binding and IValueConverter
using System.Windows.Media;
using System.Windows.Media.Effects; // For DropShadowEffect
using System.Drawing;
using System.IO;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging; // For BitmapImage

namespace ArcadeLauncher.SW3
{
    // Converter to compare two values for equality
    public class EqualityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values.Length != 2 || values[0] == null || values[1] == null)
                return false;

            return values[0].Equals(values[1]);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public partial class MainWindow
    {
        private double marginWidth; // Class-level field for effective margin calculations

        private void SetupUI()
        {
            int columns = settings.NumberOfColumns > 0 ? settings.NumberOfColumns : 7; // Match SW1's default of 7 columns
            LogToFile($"Number of Columns (columns) = {columns}");

            double baseBorderThickness = 5; // Base thickness of the highlight border (before DPI scaling)
            LogToFile($"Base Border Thickness (baseBorderThickness) = {baseBorderThickness} (logical pixels) = {baseBorderThickness * dpiScaleFactor} (physical pixels)");

            // Set border thickness to a consistent physical size (8 physical pixels) across DPI settings
            double targetPhysicalBorderThickness = 8; // Target 8 physical pixels for consistency
            borderThickness = targetPhysicalBorderThickness / dpiScaleFactor; // Logical pixels, DPI-compensated
            LogToFile($"Border Thickness (borderThickness) = {borderThickness} (logical pixels) = {borderThickness * dpiScaleFactor} (physical pixels)");

            // Step 1: Get the screen width in logical pixels (already DPI-adjusted)
            double totalWidth = SystemParameters.PrimaryScreenWidth; // Logical pixels, DPI-adjusted
            LogToFile($"Step 1 - Screen Width: totalWidth={totalWidth} (logical pixels)");
            LogToFile($"ClientSize.Width (for reference): {ActualWidth} (logical pixels)");
            LogToFile($"Total Width (totalWidth) = {totalWidth} (logical pixels) = {totalWidth * dpiScaleFactor} (physical pixels)");

            // Step 2: Allocate percentages using the physical width
            double artBoxPercentage = 0.825; // 82.5% for art boxes
            LogToFile($"Art Box Percentage (artBoxPercentage) = {artBoxPercentage}");

            double emptySpacePercentage = 0.175; // 17.5% for empty space
            LogToFile($"Empty Space Percentage (emptySpacePercentage) = {emptySpacePercentage}");

            // Calculate art box and empty space in logical pixels
            double totalArtBoxWidth = totalWidth * artBoxPercentage;
            LogToFile($"Total Art Box Width (totalArtBoxWidth) = {totalArtBoxWidth} (logical pixels) = {totalArtBoxWidth * dpiScaleFactor} (physical pixels)");

            double totalEmptySpace = totalWidth * emptySpacePercentage;
            LogToFile($"Total Empty Space (totalEmptySpace) = {totalEmptySpace} (logical pixels) = {totalEmptySpace * dpiScaleFactor} (physical pixels)");

            // Calculate art box width (including borders)
            double boxWidth = totalArtBoxWidth / columns;
            LogToFile($"Box Width with Border (boxWidth) = {boxWidth} (logical pixels) = {boxWidth * dpiScaleFactor} (physical pixels)");

            // Calculate empty space sections
            int numberOfSections = (columns - 1) * 2 + 2; // (n-1)*2 + 2 sections
            LogToFile($"Number of Sections (numberOfSections) = {numberOfSections}");

            double emptySpaceSection = totalEmptySpace / numberOfSections;
            LogToFile($"Empty Space Section Width (emptySpaceSection) = {emptySpaceSection} (logical pixels) = {emptySpaceSection * dpiScaleFactor} (physical pixels)");

            // Set margins
            marginWidth = emptySpaceSection; // Assign to the class-level field
            LogToFile($"Margin Width (marginWidth) = {marginWidth} (logical pixels) = {marginWidth * dpiScaleFactor} (physical pixels)");

            topMargin = emptySpaceSection;
            LogToFile($"Top Margin (topMargin) = {topMargin} (logical pixels) = {topMargin * dpiScaleFactor} (physical pixels)");

            double bottomMargin = topMargin; // Bottom margin equals top margin
            LogToFile($"Bottom Margin (bottomMargin) = {bottomMargin} (logical pixels) = {bottomMargin * dpiScaleFactor} (physical pixels)");

            // Calculate vertical gap (adjusted to achieve 29 pixels of grey between art boxes at 125% DPI)
            double verticalGap = topMargin * 1.5104166666666667; // This was previously set to target 29 pixels, but we'll override adjustedVerticalGap
            LogToFile($"Vertical Gap (verticalGap) = {verticalGap} (logical pixels) = {verticalGap * dpiScaleFactor} (physical pixels)");

            // Calculate box height using the exact aspect ratio of 4/3 applied to the image's width (excluding borders)
            double imageWidth = boxWidth - 2 * borderThickness; // Width of the image excluding borders
            LogToFile($"Image Width Excluding Borders (imageWidth) = {imageWidth} (logical pixels) = {imageWidth * dpiScaleFactor} (physical pixels)");

            double imageHeight = imageWidth * (4.0 / 3.0); // Apply 4/3 aspect ratio to the image width
            LogToFile($"Image Height (imageHeight) = {imageHeight} (logical pixels) = {imageHeight * dpiScaleFactor} (physical pixels)");

            double boxHeight = imageHeight + 2 * borderThickness; // Add borders to get total height
            LogToFile($"Box Height with Border (boxHeight) = {boxHeight} (logical pixels) = {boxHeight * dpiScaleFactor} (physical pixels)");

            // Total calculated width is the full screen width
            totalCalculatedWidth = totalWidth;
            LogToFile($"Total Calculated Width (totalCalculatedWidth) = {totalCalculatedWidth} (logical pixels) = {totalCalculatedWidth * dpiScaleFactor} (physical pixels)");

            // Set adjustedVerticalGap to a DPI-compensated value equivalent to -8 logical pixels at 125% DPI
            double targetPhysicalOverlap = -10; // -8 logical pixels at 125% DPI = -10 physical pixels
            LogToFile($"Target Physical Overlap (targetPhysicalOverlap) = {targetPhysicalOverlap} (physical pixels)");

            adjustedVerticalGap = targetPhysicalOverlap / dpiScaleFactor; // DPI-compensated overlap
            LogToFile($"Adjusted Vertical Gap (adjustedVerticalGap) = {adjustedVerticalGap} (logical pixels) = {adjustedVerticalGap * dpiScaleFactor} (physical pixels)");

            // Safeguard: Ensure the overlap doesn't exceed the BorderThickness on both sides to prevent clipping
            double minAdjustedVerticalGap = -2 * borderThickness;
            LogToFile($"Minimum Adjusted Vertical Gap (minAdjustedVerticalGap) = {minAdjustedVerticalGap} (logical pixels) = {minAdjustedVerticalGap * dpiScaleFactor} (physical pixels)");

            if (adjustedVerticalGap < minAdjustedVerticalGap)
            {
                adjustedVerticalGap = minAdjustedVerticalGap;
                LogToFile($"Adjusted Vertical Gap Clamped (adjustedVerticalGap) = {adjustedVerticalGap} (logical pixels) = {adjustedVerticalGap * dpiScaleFactor} (physical pixels)");
            }

            double expectedVerticalGap = adjustedVerticalGap + 2 * borderThickness;
            LogToFile($"Expected Vertical Gap (adjustedVerticalGap + 2 * borderThickness) = {expectedVerticalGap} (logical pixels) = {expectedVerticalGap * dpiScaleFactor} (physical pixels)");

            // Calculate the number of rows needed for actual games
            rows = (int)Math.Ceiling((double)games.Count / columns); // Set the class-level field
            LogToFile($"Number of Rows for Actual Games (rows) = {rows}");

            // Add one extra row for dummy items to ensure side-to-side movement is possible
            int totalRows = rows + 1;
            LogToFile($"Total Rows including Dummy Row (totalRows) = {totalRows}");

            // Preload all images at the exact size
            preloadedImages = new List<Bitmap>(games.Count);
            LogToFile($"Preloading images for {games.Count} games...");
            for (int i = 0; i < games.Count; i++)
            {
                Bitmap? compositeImage = null;
                if (File.Exists(games[i].ArtBoxPath))
                {
                    using (Bitmap coverImage = new Bitmap(games[i].ArtBoxPath))
                    {
                        // Use the physical pixel dimensions for the bitmap
                        int physicalBoxWidth = (int)(boxWidth * dpiScaleFactor);
                        LogToFile($"Physical Box Width for Image {i} (physicalBoxWidth) = {physicalBoxWidth} (physical pixels)");
                        int physicalBoxHeight = (int)(boxHeight * dpiScaleFactor);
                        LogToFile($"Physical Box Height for Image {i} (physicalBoxHeight) = {physicalBoxHeight} (physical pixels)");
                        compositeImage = CreateCompositeImage(coverImage, physicalBoxWidth, physicalBoxHeight);
                    }
                }
                preloadedImages.Add(compositeImage ?? new Bitmap(1, 1)); // Add a 1x1 dummy bitmap if null
            }
            LogToFile($"Finished preloading images.");

            // Set up the ItemsControl layout using UniformGrid
            var itemsPanel = new ItemsPanelTemplate();
            var uniformGrid = new FrameworkElementFactory(typeof(UniformGrid));
            uniformGrid.SetValue(UniformGrid.ColumnsProperty, columns); // Use the dynamic number of columns
            uniformGrid.SetValue(UniformGrid.WidthProperty, totalCalculatedWidth); // Set the width to the full screen width
            uniformGrid.SetValue(UniformGrid.RowsProperty, totalRows); // Explicitly set the total number of rows
            uniformGrid.SetValue(FrameworkElement.MarginProperty, new Thickness(0)); // Remove any default margin
            itemsPanel.VisualTree = uniformGrid;
            gameItemsControl.ItemsPanel = itemsPanel;

            // Add a style to manage the border color of the selected item
            var itemContainerStyle = new Style(typeof(Border));
            itemContainerStyle.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(borderThickness)));
            itemContainerStyle.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(85, 85, 85)))); // Background color #555555
            itemContainerStyle.Setters.Add(new Setter(Border.FocusVisualStyleProperty, null)); // Disable the default focus visual style
                                                                                               // Use a DataTrigger with a MultiBinding and EqualityConverter to compare Tag with SelectedIndex
            var trigger = new DataTrigger();
            var multiBinding = new MultiBinding
            {
                Converter = new EqualityConverter()
            };
            multiBinding.Bindings.Add(new Binding("Tag") { RelativeSource = new RelativeSource(RelativeSourceMode.Self) });
            multiBinding.Bindings.Add(new Binding("SelectedIndex") { Source = this });
            trigger.Binding = multiBinding;
            trigger.Value = true; // Trigger when the converter returns true (i.e., Tag == SelectedIndex)
            trigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 255, 255, 255)))); // Semi-transparent white
            itemContainerStyle.Triggers.Add(trigger);
            gameItemsControl.ItemContainerStyle = itemContainerStyle;

            // Define a style for the Image with DropShadowEffect
            var imageStyle = new Style(typeof(System.Windows.Controls.Image));
            imageStyle.Setters.Add(new Setter(System.Windows.Controls.Image.EffectProperty, new DropShadowEffect
            {
                ShadowDepth = 10, // 10 pixels offset
                Direction = 315, // Angle (315 degrees for bottom-right shadow)
                Color = System.Windows.Media.Colors.Black, // Black shadow
                Opacity = 0.5, // 50% opacity
                BlurRadius = 10 // Softer shadow edges
            }));
            // Add a DataTrigger to remove the shadow when the parent Border is selected
            var shadowTrigger = new DataTrigger();
            var shadowMultiBinding = new MultiBinding
            {
                Converter = new EqualityConverter()
            };
            shadowMultiBinding.Bindings.Add(new Binding("Tag") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Border), 1) });
            shadowMultiBinding.Bindings.Add(new Binding("SelectedIndex") { Source = this });
            shadowTrigger.Binding = shadowMultiBinding;
            shadowTrigger.Value = true; // Trigger when the converter returns true (i.e., Tag == SelectedIndex)
            shadowTrigger.Setters.Add(new Setter(System.Windows.Controls.Image.EffectProperty, null));
            imageStyle.Triggers.Add(shadowTrigger);
            gameItemsControl.ItemContainerStyle = itemContainerStyle;

            // Add art boxes
            var items = new List<FrameworkElement>();

            // Add actual games
            for (int i = 0; i < games.Count; i++)
            {
                var game = games[i];
                int row = i / columns;
                int col = i % columns;

                // Outer Border: Handles the highlight effect
                var outerBorder = new Border
                {
                    Tag = i, // Use Tag to identify the index for highlighting
                    Margin = new Thickness(0, 0, 0, row == rows - 1 ? 0 : adjustedVerticalGap), // Apply adjusted vertical gap to the bottom (except last row)
                    HorizontalAlignment = HorizontalAlignment.Center, // Center the art box in the cell
                    VerticalAlignment = VerticalAlignment.Top, // Align to the top to remove extra padding above
                    Focusable = true, // Make the Border focusable for keyboard navigation
                    ClipToBounds = false // Allow the shadow to render outside the Border's bounds
                };

                if (preloadedImages[i] != null)
                {
                    var bitmapImage = new BitmapImage();
                    using (var stream = new MemoryStream())
                    {
                        preloadedImages[i].Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                        stream.Position = 0;
                        bitmapImage.BeginInit();
                        bitmapImage.StreamSource = stream;
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze(); // Freeze the BitmapImage to optimize rendering
                    }
                    var artBoxImage = new System.Windows.Controls.Image
                    {
                        Source = bitmapImage,
                        Stretch = Stretch.Fill,
                        Width = boxWidth - 2 * borderThickness, // Reduce size to fit within BorderThickness
                        Height = boxHeight - 2 * borderThickness, // Reduce size to fit within BorderThickness
                        Style = imageStyle // Apply the style with the shadow
                    };
                    LogToFile($"Art Box Image Width for Image {i} (artBoxImage.Width) = {artBoxImage.Width} (logical pixels) = {artBoxImage.Width * dpiScaleFactor} (physical pixels)");
                    LogToFile($"Art Box Image Height for Image {i} (artBoxImage.Height) = {artBoxImage.Height} (logical pixels) = {artBoxImage.Height * dpiScaleFactor} (physical pixels)");

                    outerBorder.Child = artBoxImage;
                }
                else
                {
                    outerBorder.Background = new SolidColorBrush(System.Windows.Media.Colors.Gray);
                    outerBorder.Child = new TextBlock
                    {
                        Text = "No Image",
                        Foreground = new SolidColorBrush(System.Windows.Media.Colors.White),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 12 * dpiScaleFactor // Scale font size for high-DPI displays
                    };
                }

                outerBorder.MouseLeftButtonDown += (s, e) => LaunchGame(game);
                items.Add(outerBorder);
            }

            // Add dummy items to fill out the bottom row and an additional row
            int totalItems = totalRows * columns;
            int dummyItemsToAdd = totalItems - games.Count;
            LogToFile($"Adding {dummyItemsToAdd} dummy items to fill out the bottom row and an additional row.");

            for (int i = games.Count; i < totalItems; i++)
            {
                int row = i / columns;
                int col = i % columns;

                // Create a dummy border that is invisible and non-interactive
                var dummyBorder = new Border
                {
                    Tag = i,
                    Margin = new Thickness(0, 0, 0, row == totalRows - 1 ? 0 : adjustedVerticalGap),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Focusable = false, // Not focusable
                    Visibility = Visibility.Hidden, // Invisible
                    Width = boxWidth,
                    Height = boxHeight
                };

                items.Add(dummyBorder);
            }

            gameItemsControl.ItemsSource = items;
            // Apply the top margin, no horizontal margins since UniformGrid spans the full width
            gameItemsControl.Margin = new Thickness(0, topMargin, 0, 0);
            LogToFile($"ItemsControl Top Margin (gameItemsControl.Margin.Top) = {gameItemsControl.Margin.Top} (logical pixels) = {gameItemsControl.Margin.Top * dpiScaleFactor} (physical pixels)");

            // Set the total height of the ItemsControl, accounting for the BorderThickness and bottom margin
            double rowHeight = boxHeight + 2 * borderThickness + adjustedVerticalGap;
            LogToFile($"Row Height (rowHeight) = {rowHeight} (logical pixels) = {rowHeight * dpiScaleFactor} (physical pixels)");

            gameItemsControl.Height = topMargin + (totalRows * rowHeight) + bottomMargin; // Use totalRows to include the dummy row
            LogToFile($"ItemsControl Height (gameItemsControl.Height) = {gameItemsControl.Height} (logical pixels) = {gameItemsControl.Height * dpiScaleFactor} (physical pixels)");

            // No left or right margins on the MarginBorder since UniformGrid spans the full width
            Canvas.SetLeft(marginBorder, 0);
            Canvas.SetTop(marginBorder, 0);
            marginBorder.Width = totalCalculatedWidth; // Full screen width
            LogToFile($"MarginBorder Width (marginBorder.Width) = {marginBorder.Width} (logical pixels) = {marginBorder.Width * dpiScaleFactor} (physical pixels)");

            // Constrain the MarginBorder's height to the window's visible height to enable scrolling
            marginBorder.Height = this.ActualHeight;
            LogToFile($"MarginBorder Height (marginBorder.Height) = {marginBorder.Height} (logical pixels) = {marginBorder.Height * dpiScaleFactor} (physical pixels)");
        }
    }
}