using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcadeLauncher.SW3
{
    public partial class MainWindow
    {
        // Animation state variables
        private bool isAnimating = false;
        private double startOffset;
        private double targetOffset;
        private DateTime animationStartTime;
        private readonly TimeSpan animationDuration = TimeSpan.FromMilliseconds(300); // 300 ms duration

        private void InitializeScrolling()
        {
            // No timers needed; we'll use CompositionTarget.Rendering for smooth scrolling
        }

        private void ScrollToSelectedItem()
        {
            if (gameItemsControl == null || scrollViewer == null) return;

            var selectedItem = gameItemsControl.ItemContainerGenerator.ContainerFromIndex(SelectedIndex) as FrameworkElement;
            if (selectedItem == null)
            {
                LogToFile($"ScrollToSelectedItem: Selected item at index {SelectedIndex} is null");
                return;
            }

            double rowTop = selectedItem.TranslatePoint(new System.Windows.Point(0, 0), gameItemsControl).Y;
            double rowBottom = rowTop + selectedItem.ActualHeight; // ActualHeight includes BorderThickness
            double containerHeight = scrollViewer.ViewportHeight; // Use dynamic ViewportHeight
            double currentScrollOffset = scrollViewer.VerticalOffset;
            double shadowDepth = 10; // ShadowDepth from DropShadowEffect
            double blurRadius = 10; // BlurRadius from DropShadowEffect
            double shadowExtent = shadowDepth + blurRadius; // Total vertical extent of the shadow
            double effectiveVisualBottom = rowBottom + shadowExtent; // Account for the border (already in ActualHeight) and shadow

            // Log values for debugging
            LogToFile($"ScrollToSelectedItem: RowTop={rowTop} (logical pixels), RowBottom={rowBottom} (logical pixels), EffectiveVisualBottom={effectiveVisualBottom} (logical pixels), CurrentOffset={currentScrollOffset} (logical pixels), ContainerHeight={containerHeight} (logical pixels), AdjustedVerticalGap={adjustedVerticalGap} (logical pixels), BorderThickness={borderThickness} (logical pixels), ActualVerticalGap={actualVerticalGap} (logical pixels)");

            // Update visibility check to use effectiveVisualBottom
            if (rowTop >= currentScrollOffset && effectiveVisualBottom <= currentScrollOffset + containerHeight)
            {
                LogToFile($"ScrollToSelectedItem: Row (including border and shadow) already fully visible, no scrolling needed.");
                return;
            }

            double newTargetOffset;
            int columns = settings.NumberOfColumns > 0 ? settings.NumberOfColumns : 7;
            int currentRow = SelectedIndex / columns;
            int totalRows = (int)Math.Ceiling((double)gameItemsControl.Items.Count / columns);
            bool isBottomRow = (currentRow == rows - 1); // Bottom row with actual games (excluding dummy row)

            if (effectiveVisualBottom > currentScrollOffset + containerHeight)
            {
                // Downward movement
                if (isBottomRow)
                {
                    // Bottom row: Align the bottom of the art box with bottomMargin below it
                    double bottomMargin = topMargin; // Bottom margin equals top margin
                    LogToFile($"Downward to bottom row {currentRow} - Variables: RowBottom={rowBottom} (logical pixels), BottomMargin={bottomMargin} (logical pixels), ContainerHeight={containerHeight} (logical pixels)");
                    double viewportBottom = rowBottom + bottomMargin;
                    newTargetOffset = viewportBottom - containerHeight;
                    LogToFile($"Downward to bottom row {currentRow} - Calculation: ViewportBottom = RowBottom + BottomMargin = {rowBottom} + {bottomMargin} = {viewportBottom} (logical pixels)");
                    LogToFile($"Downward to bottom row {currentRow} - Calculation: TargetOffset = ViewportBottom - ContainerHeight = {viewportBottom} - {containerHeight} = {newTargetOffset} (logical pixels)");
                }
                else
                {
                    // Non-bottom row: Use original scrolling logic (position viewport based on next row's top + visual gap)
                    int nextRowIndex = (currentRow + 1) * columns;
                    if (nextRowIndex >= gameItemsControl.Items.Count) nextRowIndex = gameItemsControl.Items.Count - 1; // Safety check
                    var nextRowItem = gameItemsControl.ItemContainerGenerator.ContainerFromIndex(nextRowIndex) as FrameworkElement;
                    if (nextRowItem != null)
                    {
                        double nextRowTop = nextRowItem.TranslatePoint(new System.Windows.Point(0, 0), gameItemsControl).Y;
                        double visualGap = actualVerticalGap + 2 * borderThickness; // Total gap between art boxes (grey gap + borders)
                        LogToFile($"Downward to row {currentRow} - Step 1 Variables: NextRowTop={nextRowTop} (logical pixels), ActualVerticalGap={actualVerticalGap} (logical pixels), BorderThickness={borderThickness} (logical pixels)");
                        LogToFile($"Downward to row {currentRow} - Step 1 Calculation: visualGap = ActualVerticalGap + 2 * BorderThickness = {actualVerticalGap} + 2 * {borderThickness} = {visualGap} (logical pixels)");
                        double viewportBottom = nextRowTop + visualGap;
                        LogToFile($"Downward to row {currentRow} - Step 2 Variables: NextRowTop={nextRowTop} (logical pixels), VisualGap={visualGap} (logical pixels)");
                        LogToFile($"Downward to row {currentRow} - Step 2 Calculation: viewportBottom = NextRowTop + VisualGap = {nextRowTop} + {visualGap} = {viewportBottom} (logical pixels)");
                        newTargetOffset = viewportBottom - containerHeight;
                        LogToFile($"Downward to row {currentRow} - Step 3 Variables: ViewportBottom={viewportBottom} (logical pixels), ContainerHeight={containerHeight} (logical pixels)");
                        LogToFile($"Downward to row {currentRow} - Step 3 Calculation: targetOffset = ViewportBottom - ContainerHeight = {viewportBottom} - {containerHeight} = {newTargetOffset} (logical pixels)");
                    }
                    else
                    {
                        // Fallback: Use the bottom of the current art box plus topMargin
                        LogToFile($"Downward to row {currentRow} - Fallback Variables: RowBottom={rowBottom} (logical pixels), TopMargin={topMargin} (logical pixels), ContainerHeight={containerHeight} (logical pixels)");
                        newTargetOffset = rowBottom + topMargin - containerHeight;
                        LogToFile($"Downward to row {currentRow} - Fallback Calculation: targetOffset = RowBottom + TopMargin - ContainerHeight = {rowBottom} + {topMargin} - {containerHeight} = {newTargetOffset} (logical pixels)");
                    }
                }
            }
            else
            {
                // Upward movement: Adjust the logic for the top row
                if (currentRow == 0)
                {
                    // Top row: Extend the art box to the very top pixel of the screen
                    // The ItemsControl has a top margin, and the Border has a top border thickness
                    // We need to offset by the negative of these values to align the Image's top edge with the screen's top
                    newTargetOffset = -(marginWidth + borderThickness); // Negative of the effective top margin
                    LogToFile($"Upward to top row {currentRow} - Setting targetOffset to {newTargetOffset} (logical pixels) to extend art box to the top pixel of the screen.");
                }
                else
                {
                    // Other rows: Use adjustedVerticalGap as in the first implementation
                    newTargetOffset = rowTop - adjustedVerticalGap;
                    LogToFile($"Upward to row {currentRow} - Using adjustedVerticalGap: targetOffset = RowTop - AdjustedVerticalGap = {rowTop} - {adjustedVerticalGap} = {newTargetOffset} (logical pixels)");
                }
            }

            double maxScrollOffset = gameItemsControl.ActualHeight - containerHeight;
            if (maxScrollOffset < 0) maxScrollOffset = 0;
            if (newTargetOffset < 0) newTargetOffset = 0;
            if (newTargetOffset > maxScrollOffset) newTargetOffset = maxScrollOffset;

            // Start the smooth scrolling animation
            StartSmoothScroll(currentScrollOffset, newTargetOffset, isBottomRow);
        }

        private void StartSmoothScroll(double start, double target, bool isBottomRow)
        {
            // Interrupt any ongoing animation
            if (isAnimating)
            {
                CompositionTarget.Rendering -= OnRendering;
                isAnimating = false;
            }

            // If the difference is negligible, jump directly to the target
            if (Math.Abs(start - target) < 1.0)
            {
                scrollViewer.ScrollToVerticalOffset(target);
                LogToFile($"SmoothScroll: Difference too small, jumping directly to TargetOffset={target} (logical pixels)");
                if (isBottomRow)
                {
                    ApplySideToSideAdjustment();
                }
                return;
            }

            // Set up the animation
            startOffset = start;
            targetOffset = target;
            animationStartTime = DateTime.Now;
            isAnimating = true;
            this.isBottomRow = isBottomRow; // Store for use in OnRendering

            // Start the animation by subscribing to CompositionTarget.Rendering
            CompositionTarget.Rendering += OnRendering;
            LogToFile($"SmoothScroll Started: StartOffset={startOffset}, TargetOffset={targetOffset}, IsBottomRow={isBottomRow}");
        }

        private bool isBottomRow; // Track if we're on the bottom row for side-to-side adjustment

        private void OnRendering(object? sender, EventArgs e)
        {
            if (!isAnimating) return;

            // Calculate the progress (0 to 1) based on elapsed time
            TimeSpan elapsed = DateTime.Now - animationStartTime;
            double progress = elapsed.TotalMilliseconds / animationDuration.TotalMilliseconds;

            if (progress >= 1.0)
            {
                // Animation complete
                scrollViewer.ScrollToVerticalOffset(targetOffset);
                LogToFile($"SmoothScroll Completed: FinalOffset={targetOffset} (logical pixels)");
                isAnimating = false;
                CompositionTarget.Rendering -= OnRendering;

                // Apply side-to-side adjustment if on the bottom row
                if (isBottomRow)
                {
                    ApplySideToSideAdjustment();
                }
                return;
            }

            // Apply ease-in-out function: t < 0.5 ? 2 * t * t : 1 - (-2 * t + 2)^2 / 2
            double easedProgress = progress < 0.5
                ? 2 * progress * progress
                : 1 - Math.Pow(-2 * progress + 2, 2) / 2;

            // Interpolate the offset
            double newOffset = startOffset + (targetOffset - startOffset) * easedProgress;
            scrollViewer.ScrollToVerticalOffset(newOffset);
        }

        private void ApplySideToSideAdjustment()
        {
            int columns = settings.NumberOfColumns > 0 ? settings.NumberOfColumns : 7;
            int itemsInLastRow = games.Count % columns == 0 ? columns : games.Count % columns;
            int firstIndexInLastRow = (rows - 1) * columns;

            // Ensure there are at least two items (real or dummy) to move between
            if (SelectedIndex < gameItemsControl.Items.Count - 1)
            {
                int originalIndex = SelectedIndex;
                int nextIndex = originalIndex + 1;
                LogToFile($"Simulating side-to-side movement: Moving from index {originalIndex} to {nextIndex} and back to adjust scroll position.");
                SelectedIndex = nextIndex;
                scrollViewer.UpdateLayout();
                SelectedIndex = originalIndex;
                scrollViewer.UpdateLayout();
                LogToFile($"After side-to-side adjustment: CurrentOffset={scrollViewer.VerticalOffset} (logical pixels)");

                // Adjust the scroll position downward to ensure the margin is exactly bottomMargin
                double bottomMargin = topMargin; // 26.879999999999995 logical pixels for 5 columns
                double shadowDepth = 10;
                double blurRadius = 10;
                double shadowExtent = shadowDepth + blurRadius;
                var selectedItem = gameItemsControl.ItemContainerGenerator.ContainerFromIndex(SelectedIndex) as FrameworkElement;
                if (selectedItem != null)
                {
                    double rowBottom = selectedItem.TranslatePoint(new System.Windows.Point(0, 0), gameItemsControl).Y + selectedItem.ActualHeight;
                    double effectiveVisualBottom = rowBottom + shadowExtent;
                    double initialViewportBottom = scrollViewer.VerticalOffset + scrollViewer.ViewportHeight;
                    double sideToSideMargin = initialViewportBottom - effectiveVisualBottom; // Actual margin after initial scroll
                    double additionalMargin = bottomMargin - sideToSideMargin; // Additional margin needed to reach bottomMargin
                    double adjustedOffset = scrollViewer.VerticalOffset + additionalMargin;
                    double adjustedMaxScrollOffset = gameItemsControl.ActualHeight - scrollViewer.ViewportHeight;
                    if (adjustedMaxScrollOffset < 0) adjustedMaxScrollOffset = 0;
                    if (adjustedOffset < 0) adjustedOffset = 0;
                    if (adjustedOffset > adjustedMaxScrollOffset) adjustedOffset = adjustedMaxScrollOffset;

                    scrollViewer.ScrollToVerticalOffset(adjustedOffset);
                    LogToFile($"After adding bottom margin: AdjustedOffset={adjustedOffset} (logical pixels), AdjustedMaxScrollOffset={adjustedMaxScrollOffset} (logical pixels)");
                }
                else
                {
                    LogToFile($"Selected item at index {SelectedIndex} is null during side-to-side adjustment");
                }
            }
            else
            {
                LogToFile($"Cannot simulate side-to-side movement: No adjacent item to move to at index {SelectedIndex}.");
            }
        }
    }
}