using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ArcadeLauncher.Core;

namespace ArcadeLauncher.SW2
{
    public partial class MainForm : Form
    {
        // Removed duplicate definition: private TextBox notesTextBox;

        private void SetupGameView(Game game, bool isNewGame)
        {
            // Dispose of existing PictureBox images before clearing the panel
            foreach (var pictureBox in mainPanel.Controls.OfType<PictureBox>())
            {
                if (pictureBox.Image != null)
                {
                    pictureBox.Image.Dispose();
                    pictureBox.Image = null;
                }
            }

            // Define event handlers as delegates to ensure proper unsubscription
            EventHandler gameListGotFocus = (s, e) =>
            {
                Logger.LogToFile("Game List received focus.");
            };
            EventHandler gameListLostFocus = (s, e) =>
            {
                Logger.LogToFile("Game List lost focus.");
            };

            // Unsubscribe existing event handlers for gameList to prevent accumulation
            gameList.GotFocus -= gameListGotFocus;
            gameList.LostFocus -= gameListLostFocus;

            // Temporarily unsubscribe from SelectedIndexChanged to prevent gameList redraw
            gameList.SelectedIndexChanged -= selectedIndexChangedHandler;

            // Hide the mainPanel to prevent partial rendering during transition
            mainPanel.Visible = false;
            Logger.LogToFile("Set mainPanel.Visible = false to prevent partial rendering during SetupGameView.");

            mainPanel.SuspendLayout(); // Suspend layout updates to prevent flickering (Ticket 04)
            mainPanel.Controls.Clear();
            mainPanel.Tag = game;
            mainPanel.AutoScroll = true;
            mainPanel.Padding = new Padding(0);

            // Calculate scaling factor based on screen height (reference: 1080p)
            double scalingFactor = (double)Screen.PrimaryScreen.WorkingArea.Height / 1080;

            // Determine actual input height by creating a temporary TextBox in an off-screen Panel
            using (var tempPanel = new Panel { Visible = false }) // Off-screen panel
            using (var tempTextBox = new CustomTextBox { Font = largeFont })
            {
                tempPanel.Controls.Add(tempTextBox); // Add to off-screen panel
                inputHeight = tempTextBox.Height; // Get the actual rendered height
            }

            // Scale all fixed-size elements
            int gameListWidth = (int)(375 * scalingFactor);
            gameList.Width = gameListWidth;
            int labelWidth = (int)(200 * scalingFactor); // Increased from 150 to 200 to prevent clipping
            inputWidth = (int)(690 * 1.12 * scalingFactor); // 690 * 1.12 = 772.8, rounded to 773, then scaled
            int labelHeight = inputHeight; // Match label height to input box height

            // Calculate the required width for the "Browse" button using an off-screen panel to prevent flicker
            int buttonWidth;
            using (var tempPanel = new Panel { Visible = false }) // Off-screen panel
            using (var tempButton = new CustomButton { Text = "Browse", Font = largeFont })
            {
                tempPanel.Controls.Add(tempButton); // Add to off-screen panel
                buttonWidth = TextRenderer.MeasureText("Browse", largeFont).Width + (int)(20 * scalingFactor); // Add padding for button margins
            }

            int buttonHeight = inputHeight; // Match input box height
            smallButtonWidth = inputHeight; // Make Plus/Minus buttons square
            smallButtonHeight = inputHeight; // Match input box height
            int saveCancelButtonWidth = (int)(100 * scalingFactor);
            int saveCancelButtonHeight = inputHeight; // Match input box height
            int columnGap = (int)(10 * scalingFactor);
            int rowHeight = (int)(50 * scalingFactor);
            smallButtonGap = (int)(5 * scalingFactor); // Gap between minus and plus buttons (reduced from 28 to 5 pixels)
            int sectionGap = (int)(18 * scalingFactor); // Desired gap between sections (18 pixels at 1080p, scaled)

            // Artwork (right side) - Calculate first to get artBoxPictureBox.Top
            int topRibbonHeight = toolStrip.Height;
            int taskbarHeight = Screen.PrimaryScreen.Bounds.Height - Screen.PrimaryScreen.WorkingArea.Height;
            int titleBarHeight = SystemInformation.CaptionHeight;
            int availableHeight = Screen.PrimaryScreen.WorkingArea.Height - topRibbonHeight - titleBarHeight;
            double marginAllocation = availableHeight * 0.1;
            double marginAndGap = marginAllocation / 4;
            int marginAndGapRounded = (int)Math.Round(marginAndGap);
            double artboxAllocation = availableHeight - (4 * marginAndGapRounded);
            double totalHeightRatio = 4.0 / 3.0 + 3.0 / 16.0 + 9.0 / 16.0;
            int thumbnailWidth = (int)Math.Round(artboxAllocation / totalHeightRatio);
            double artBoxAspect = 4.0 / 3.0;
            double marqueeAspect = 3.0 / 16.0;
            double controllerAspect = 9.0 / 16.0;
            int artBoxHeight = (int)Math.Round(thumbnailWidth * artBoxAspect);
            int marqueeHeight = (int)Math.Round(thumbnailWidth * marqueeAspect);
            int controllerHeight = (int)Math.Round(thumbnailWidth * controllerAspect);

            int screenWidth = Screen.PrimaryScreen.WorkingArea.Width;
            int artworkLeft = screenWidth - gameList.Width - thumbnailWidth - marginAndGapRounded;
            int artworkTop = marginAndGapRounded;

            // Preload artwork images to avoid flickering during layout (Ticket 04)
            Image artBoxImage = null;
            Image marqueeImage = null;
            Image controllerImage = null;
            if (game != null)
            {
                if (File.Exists(game.ArtBoxPath))
                {
                    using (var tempImage = Image.FromFile(game.ArtBoxPath))
                    {
                        artBoxImage = new Bitmap(tempImage); // Create a new Bitmap to avoid keeping the file handle open
                    }
                }
                if (File.Exists(game.MarqueePath))
                {
                    using (var tempImage = Image.FromFile(game.MarqueePath))
                    {
                        marqueeImage = new Bitmap(tempImage);
                    }
                }
                if (File.Exists(game.ControllerLayoutPath))
                {
                    using (var tempImage = Image.FromFile(game.ControllerLayoutPath))
                    {
                        controllerImage = new Bitmap(tempImage);
                    }
                }
            }

            var artBoxPictureBox = new CustomPictureBox // Use CustomPictureBox
            {
                Top = artworkTop,
                Left = artworkLeft,
                Width = thumbnailWidth,
                Height = artBoxHeight,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.StretchImage,
                Padding = new Padding(0),
                Image = artBoxImage
            };
            artworkTop += artBoxHeight + marginAndGapRounded;

            var marqueePictureBox = new CustomPictureBox // Use CustomPictureBox
            {
                Top = artworkTop,
                Left = artworkLeft,
                Width = thumbnailWidth,
                Height = marqueeHeight,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.StretchImage,
                Padding = new Padding(0),
                Image = marqueeImage
            };
            artworkTop += marqueeHeight + marginAndGapRounded;

            var controllerPictureBox = new CustomPictureBox // Use CustomPictureBox
            {
                Top = artworkTop,
                Left = artworkLeft,
                Width = thumbnailWidth,
                Height = controllerHeight,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.StretchImage,
                Padding = new Padding(0),
                Image = controllerImage
            };

            // Column 1 and 2 controls - Align first input box with first artbox
            int currentTop = artBoxPictureBox.Top;

            // Calculate the total width of the columns (Label + Gap + Input + Gap + Browse Button)
            int totalColumnsWidth = labelWidth + columnGap + inputWidth + columnGap + buttonWidth;

            // Calculate the available space between the right edge of the Game List and the left edge of the art boxes
            int availableSpace = artworkLeft; // artworkLeft is already the distance between Game List and art boxes

            // Center the columns by calculating the unused space
            int unusedSpace = availableSpace - totalColumnsWidth;
            int column1Left = unusedSpace / 2; // Center the columns

            column2Left = column1Left + labelWidth + columnGap;
            column3Left = column2Left + inputWidth + columnGap;

            // Log the centering calculation for debugging
            Logger.LogToFile($"SetupGameView Centering: GameListWidth={gameList.Width}, ArtworkLeft={artworkLeft}, AvailableSpace={availableSpace}, TotalColumnsWidth={totalColumnsWidth}, UnusedSpace={unusedSpace}, Column1Left={column1Left}");

            // Display Name
            var displayNameLabel = new Label { Text = "Display Name", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0) };
            var displayNameTextBox = new CustomTextBox { Text = game?.DisplayName ?? "", Top = currentTop, Left = column2Left, Width = inputWidth, Height = inputHeight, Font = largeFont, Name = "displayNameTextBox" }; // Use CustomTextBox
            currentTop += sectionGap + inputHeight; // Adjust gap to 18 pixels

            // Alphabetizing Name
            var alphabetizeNameLabel = new Label { Text = "Alphabetizing Name", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0) };
            var alphabetizeNameTextBox = new CustomTextBox { Text = game?.AlphabetizeName ?? "", Top = currentTop, Left = column2Left, Width = inputWidth, Height = inputHeight, Font = largeFont, Name = "alphabetizeNameTextBox" }; // Use CustomTextBox
            currentTop += sectionGap + inputHeight; // Adjust gap to 18 pixels

            // Game Type
            var gameTypeLabel = new Label { Text = "Game Type", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0) };
            var gameTypeComboBox = new CustomComboBox
            {
                Top = currentTop,
                Left = column2Left,
                Width = inputWidth,
                Height = inputHeight,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Name = "gameTypeComboBox",
                Font = largeFont
            }; // Use CustomComboBox
            gameTypeComboBox.Items.Add("PC");
            gameTypeComboBox.Items.AddRange(plugins.Select(p => p.Name).ToArray());
            gameTypeComboBox.SelectedItem = game?.Type ?? "PC";

            // Define event handlers for gameTypeComboBox
            EventHandler comboBoxGotFocus = (s, e) =>
            {
                Logger.LogToFile("Game Type ComboBox received focus.");
            };
            EventHandler comboBoxLostFocus = (s, e) =>
            {
                Logger.LogToFile("Game Type ComboBox lost focus.");
            };
            EventHandler comboBoxDropDown = (s, e) =>
            {
                gameList.SelectedIndexChanged -= selectedIndexChangedHandler;
                Logger.LogToFile("Unsubscribed from gameList.SelectedIndexChanged during combo box dropdown.");
            };
            EventHandler comboBoxDropDownClosed = (s, e) =>
            {
                if (gameList.SelectedItem != game && gameList.Items.Contains(game))
                {
                    gameList.SelectedItem = game; // Restore the selected game
                }
                gameList.SelectedIndexChanged += selectedIndexChangedHandler;
                Logger.LogToFile("Resubscribed to gameList.SelectedIndexChanged after combo box closed.");
            };
            EventHandler comboBoxSelectedIndexChanged = (s, e) =>
            {
                if (game != null && gameTypeComboBox.SelectedItem != null)
                {
                    game.Type = gameTypeComboBox.SelectedItem.ToString();
                    Logger.LogToFile($"Updated game.Type to: {game.Type}");
                }
            };

            // Subscribe to events for gameList and gameTypeComboBox
            gameList.GotFocus += gameListGotFocus;
            gameList.LostFocus += gameListLostFocus;
            gameTypeComboBox.GotFocus += comboBoxGotFocus;
            gameTypeComboBox.LostFocus += comboBoxLostFocus;
            gameTypeComboBox.DropDown += comboBoxDropDown;
            gameTypeComboBox.DropDownClosed += comboBoxDropDownClosed;
            gameTypeComboBox.SelectedIndexChanged += comboBoxSelectedIndexChanged;

            currentTop += sectionGap + inputHeight; // Adjust gap to 18 pixels

            // Executable Path
            var executablePathLabel = new Label { Text = "Executable Path", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0) };
            var executableTextBox = new CustomTextBox { Text = game?.ExecutablePath ?? "", Top = currentTop, Left = column2Left, Width = inputWidth, Height = inputHeight, Font = largeFont, Name = "executableTextBox" }; // Use CustomTextBox
            var executableButton = new CustomButton { Text = "Browse", Top = currentTop, Left = column3Left, Width = buttonWidth, Height = buttonHeight, Font = largeFont }; // Use CustomButton
            currentTop += sectionGap + inputHeight; // Adjust gap to 18 pixels

            // LEDBlinky Animation
            var ledBlinkyLabel = new Label { Text = "LEDBlinky Animation", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0) };
            var ledBlinkyTextBox = new CustomTextBox { Text = game?.LEDBlinkyCommand ?? "", Top = currentTop, Left = column2Left, Width = inputWidth, Height = inputHeight, Font = largeFont, Name = "ledBlinkyTextBox" }; // Use CustomTextBox
            currentTop += sectionGap + inputHeight; // Adjust gap to 18 pixels

            // Pre-Launch Commands
            var preLaunchLabel = new Label { Text = "Pre-Launch Commands", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0) };
            preLaunchCommandsList = new List<TextBox>();
            preLaunchMinusButtons = new List<Button>();
            preLaunchPlusButtons = new List<Button>();
            int preLaunchCount = game?.PreLaunchCommands?.Count > 0 ? game.PreLaunchCommands.Count : 1;
            Logger.LogToFile($"Loading Pre-Launch Commands: Count={preLaunchCount}, Commands=[{string.Join(", ", game?.PreLaunchCommands ?? new List<string>())}]");
            preLaunchFirstInputTop = currentTop; // Align first command with the label
            currentTop = preLaunchFirstInputTop;
            for (int i = 0; i < preLaunchCount; i++)
            {
                var commandTextBox = new CustomTextBox // Use CustomTextBox
                {
                    Top = currentTop,
                    Left = column2Left,
                    Width = inputWidth,
                    Height = inputHeight,
                    Font = largeFont,
                    Text = game?.PreLaunchCommands?.Count > i ? game.PreLaunchCommands[i] : "",
                    Visible = true,
                    Tag = "PreLaunch" // Tag to identify Pre-Launch commands
                };
                var minusButton = new CustomButton { Text = "−", Top = currentTop, Left = column3Left, Width = smallButtonWidth, Height = smallButtonHeight, Font = largeFont, Visible = true }; // Use CustomButton
                var plusButton = new CustomButton { Text = "+", Top = currentTop, Left = column3Left + smallButtonWidth + smallButtonGap, Width = smallButtonWidth, Height = smallButtonHeight, Font = largeFont, Visible = true }; // Use CustomButton
                preLaunchCommandsList.Add(commandTextBox);
                preLaunchMinusButtons.Add(minusButton);
                preLaunchPlusButtons.Add(plusButton);
                mainPanel.Controls.Add(commandTextBox);
                mainPanel.Controls.Add(minusButton);
                mainPanel.Controls.Add(plusButton);
                Logger.LogToFile($"Pre-Launch Command {i + 1}: Text='{commandTextBox.Text}', Top={commandTextBox.Top}");
                currentTop += inputHeight; // Use inputHeight for all commands to butt them up
            }

            // Calculate the desired Top position for the Post-Close label to achieve an 18-pixel gap
            int lastPreLaunchTop = preLaunchCommandsList.Last().Top;
            int desiredPostCloseTop = lastPreLaunchTop + inputHeight + sectionGap; // Last Pre-Launch Top + inputHeight + desired gap

            // Post-Close Commands
            currentTop = desiredPostCloseTop; // Set currentTop to achieve the 18-pixel gap
            postCloseLabel = new Label { Text = "Post-Close Commands", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0) };
            postCloseCommandsList = new List<TextBox>();
            postCloseMinusButtons = new List<Button>();
            postClosePlusButtons = new List<Button>();
            int postCloseCount = game?.PostExitCommands?.Count > 0 ? game.PostExitCommands.Count : 1;
            Logger.LogToFile($"Loading Post-Close Commands: Count={postCloseCount}, Commands=[{string.Join(", ", game?.PostExitCommands ?? new List<string>())}]");
            postCloseFirstInputTop = currentTop; // Align first command with the label
            currentTop = postCloseFirstInputTop;
            for (int i = 0; i < postCloseCount; i++)
            {
                var commandTextBox = new CustomTextBox // Use CustomTextBox
                {
                    Top = currentTop,
                    Left = column2Left,
                    Width = inputWidth,
                    Height = inputHeight,
                    Font = largeFont,
                    Text = game?.PostExitCommands?.Count > i ? game.PostExitCommands[i] : "",
                    Visible = true,
                    Tag = "PostClose" // Tag to identify Post-Close commands
                };
                var minusButton = new CustomButton { Text = "−", Top = currentTop, Left = column3Left, Width = smallButtonWidth, Height = smallButtonHeight, Font = largeFont, Visible = true }; // Use CustomButton
                var plusButton = new CustomButton { Text = "+", Top = currentTop, Left = column3Left + smallButtonWidth + smallButtonGap, Width = smallButtonWidth, Height = smallButtonHeight, Font = largeFont, Visible = true }; // Use CustomButton
                postCloseCommandsList.Add(commandTextBox);
                postCloseMinusButtons.Add(minusButton);
                postClosePlusButtons.Add(plusButton);
                mainPanel.Controls.Add(commandTextBox);
                mainPanel.Controls.Add(minusButton);
                mainPanel.Controls.Add(plusButton);
                Logger.LogToFile($"Post-Close Command {i + 1}: Text='{commandTextBox.Text}', Top={commandTextBox.Top}");
                currentTop += inputHeight; // Use inputHeight for all commands to butt them up
            }

            // Notes Section (Feature 3)
            currentTop += sectionGap; // Add gap after Post-Close Commands
            notesLabel = new Label { Text = "Notes", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0) }; // Assign to class-level field
            notesTextBox = new CustomTextBox
            {
                Top = currentTop,
                Left = column2Left,
                Width = inputWidth,
                Height = inputHeight * 3, // 3 lines tall for multiline input
                Font = largeFont,
                Multiline = true,
                AcceptsReturn = true,
                ScrollBars = ScrollBars.Vertical,
                Text = game?.Notes ?? "", // Load existing notes if any
                Name = "notesTextBox"
            };
            currentTop += notesTextBox.Height + sectionGap; // Adjust for the taller TextBox

            // In Progress Checkbox (below Notes, above Hide Mouse Cursor)
            var inProgressLabel = new Label
            {
                Text = "In Progress",
                Top = currentTop,
                Left = column1Left,
                Width = labelWidth,
                Height = labelHeight,
                Font = largeFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0)
            };
            var inProgressCheckBox = new CheckBox
            {
                Top = currentTop,
                Left = column2Left,
                Width = inputWidth,
                Height = inputHeight,
                Checked = isNewGame ? true : (game?.IsInProgress ?? false), // Default to checked for new games
                Name = "inProgressCheckBox"
            };
            currentTop += inputHeight + sectionGap; // Adjust for the checkbox height

            // Hide Mouse Cursor Checkbox (below In Progress, above Save/Cancel buttons)
            var hideMouseCursorLabel = new Label
            {
                Text = "Hide Mouse Cursor",
                Top = currentTop,
                Left = column1Left,
                Width = labelWidth,
                Height = labelHeight,
                Font = largeFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0)
            };
            var hideMouseCursorCheckBox = new CheckBox
            {
                Top = currentTop,
                Left = column2Left,
                Width = inputWidth,
                Height = inputHeight,
                Checked = game?.HideMouseCursor ?? false, // Default to unchecked unless specified
                Name = "hideMouseCursorCheckBox"
            };
            currentTop += inputHeight + sectionGap; // Adjust for the checkbox height

            // Save and Cancel Buttons (moved down to accommodate new Hide Mouse Cursor section)
            saveButton = new CustomButton { Text = "Save Game", Top = currentTop, Left = column1Left, Width = saveCancelButtonWidth, Height = saveCancelButtonHeight, Font = largeFont }; // Use CustomButton
            cancelButton = new CustomButton { Text = "Cancel", Top = currentTop, Left = column1Left + saveCancelButtonWidth + columnGap, Width = saveCancelButtonWidth, Height = saveCancelButtonHeight, Font = largeFont }; // Use CustomButton
            currentTop += sectionGap + inputHeight;

            // Restore the selected game and resubscribe to the event
            if (gameList.SelectedItem != game && gameList.Items.Contains(game))
            {
                gameList.SelectedItem = game; // Restore the selected game
            }
            gameList.SelectedIndexChanged += selectedIndexChangedHandler;

            // Auto-match Alphabetize Name to Display Name (Ticket 03)
            bool shouldSyncNames = string.IsNullOrEmpty(alphabetizeNameTextBox.Text);
            displayNameTextBox.TextChanged += (s, e) =>
            {
                if (shouldSyncNames)
                {
                    alphabetizeNameTextBox.Text = displayNameTextBox.Text;
                }
            };
            alphabetizeNameTextBox.TextChanged += (s, e) =>
            {
                if (shouldSyncNames && alphabetizeNameTextBox.Text != displayNameTextBox.Text)
                {
                    shouldSyncNames = false;
                }
            };

            // Assign initial event handlers for Pre-Launch and Post-Close commands
            ReassignPreLaunchButtons();
            ReassignPostCloseButtons();

            // Artwork context menus with Remove Artwork option (Ticket 19)
            artBoxPictureBox.Click += (s, e) =>
            {
                var contextMenu = new ContextMenuStrip();
                contextMenu.Items.Add("Search Online", null, async (sender, args) =>
                {
                    if (string.IsNullOrEmpty(displayNameTextBox.Text))
                    {
                        MessageBox.Show("Please enter a display name to search for cover art.");
                        return;
                    }
                    await SearchCoverArt(displayNameTextBox.Text, artBoxPictureBox, game);
                });
                contextMenu.Items.Add("Select File", null, (sender, args) => SelectImage(artBoxPictureBox, "ArtBox", game));
                if (artBoxPictureBox.Image != null)
                {
                    contextMenu.Items.Add("Remove Artwork", null, (sender, args) =>
                    {
                        if (File.Exists(game.ArtBoxPath))
                        {
                            try
                            {
                                File.Delete(game.ArtBoxPath);
                                Logger.LogToFile($"Removed artwork file: {game.ArtBoxPath}");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogToFile($"Failed to remove artwork file {game.ArtBoxPath}: {ex.Message}");
                            }
                        }
                        artBoxPictureBox.Image?.Dispose();
                        artBoxPictureBox.Image = null;
                        game.ArtBoxPath = "";
                        Logger.LogToFile("Cleared ArtBoxPath for game.");
                    });
                }
                contextMenu.Show(artBoxPictureBox, artBoxPictureBox.PointToClient(Cursor.Position));
            };

            marqueePictureBox.Click += (s, e) =>
            {
                var contextMenu = new ContextMenuStrip();
                contextMenu.Items.Add("Select File", null, (sender, args) => SelectImage(marqueePictureBox, "Marquee", game));
                if (marqueePictureBox.Image != null)
                {
                    contextMenu.Items.Add("Remove Artwork", null, (sender, args) =>
                    {
                        if (File.Exists(game.MarqueePath))
                        {
                            try
                            {
                                File.Delete(game.MarqueePath);
                                Logger.LogToFile($"Removed artwork file: {game.MarqueePath}");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogToFile($"Failed to remove artwork file {game.MarqueePath}: {ex.Message}");
                            }
                        }
                        marqueePictureBox.Image?.Dispose();
                        marqueePictureBox.Image = null;
                        game.MarqueePath = "";
                        Logger.LogToFile("Cleared MarqueePath for game.");
                    });
                }
                contextMenu.Show(marqueePictureBox, marqueePictureBox.PointToClient(Cursor.Position));
            };

            controllerPictureBox.Click += (s, e) =>
            {
                var contextMenu = new ContextMenuStrip();
                contextMenu.Items.Add("Select File", null, (sender, args) => SelectImage(controllerPictureBox, "ControllerLayout", game));
                if (controllerPictureBox.Image != null)
                {
                    contextMenu.Items.Add("Remove Artwork", null, (sender, args) =>
                    {
                        if (File.Exists(game.ControllerLayoutPath))
                        {
                            try
                            {
                                File.Delete(game.ControllerLayoutPath);
                                Logger.LogToFile($"Removed artwork file: {game.ControllerLayoutPath}");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogToFile($"Failed to remove artwork file {game.ControllerLayoutPath}: {ex.Message}");
                            }
                        }
                        controllerPictureBox.Image?.Dispose();
                        controllerPictureBox.Image = null;
                        game.ControllerLayoutPath = "";
                        Logger.LogToFile("Cleared ControllerLayoutPath for game.");
                    });
                }
                contextMenu.Show(controllerPictureBox, controllerPictureBox.PointToClient(Cursor.Position));
            };

            executableButton.Click += (s, e) =>
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Filter = "Executable Files|*.exe";
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        executableTextBox.Text = dialog.FileName;
                    }
                }
            };

            saveButton.Click += (s, e) =>
            {
                if (mainPanel.Tag is Game gameToSave)
                {
                    SaveGame(gameToSave, isNewGame);
                }
            };

            cancelButton.Click += (s, e) =>
            {
                if (gameList.SelectedItem is Game selectedGame)
                {
                    SetupGameView(selectedGame, false);
                }
                else
                {
                    mainPanel.Controls.Clear();
                }
            };

            mainPanel.Controls.Add(displayNameLabel);
            mainPanel.Controls.Add(displayNameTextBox);
            mainPanel.Controls.Add(alphabetizeNameLabel);
            mainPanel.Controls.Add(alphabetizeNameTextBox);
            mainPanel.Controls.Add(gameTypeLabel);
            mainPanel.Controls.Add(gameTypeComboBox);
            mainPanel.Controls.Add(executablePathLabel);
            mainPanel.Controls.Add(executableTextBox);
            mainPanel.Controls.Add(executableButton);
            mainPanel.Controls.Add(ledBlinkyLabel);
            mainPanel.Controls.Add(ledBlinkyTextBox);
            mainPanel.Controls.Add(preLaunchLabel);
            mainPanel.Controls.Add(postCloseLabel);
            mainPanel.Controls.Add(notesLabel);
            mainPanel.Controls.Add(notesTextBox);
            mainPanel.Controls.Add(inProgressLabel); // Add In Progress label
            mainPanel.Controls.Add(inProgressCheckBox); // Add In Progress checkbox
            mainPanel.Controls.Add(hideMouseCursorLabel); // Add Hide Mouse Cursor label
            mainPanel.Controls.Add(hideMouseCursorCheckBox); // Add Hide Mouse Cursor checkbox
            mainPanel.Controls.Add(saveButton);
            mainPanel.Controls.Add(cancelButton);
            mainPanel.Controls.Add(artBoxPictureBox);
            mainPanel.Controls.Add(marqueePictureBox);
            mainPanel.Controls.Add(controllerPictureBox);

            foreach (var control in preLaunchCommandsList)
            {
                mainPanel.Controls.Add(control);
            }
            foreach (var control in preLaunchMinusButtons)
            {
                mainPanel.Controls.Add(control);
            }
            foreach (var control in preLaunchPlusButtons)
            {
                mainPanel.Controls.Add(control);
            }

            foreach (var control in postCloseCommandsList)
            {
                mainPanel.Controls.Add(control);
            }
            foreach (var control in postCloseMinusButtons)
            {
                mainPanel.Controls.Add(control);
            }
            foreach (var control in postClosePlusButtons)
            {
                mainPanel.Controls.Add(control);
            }

            mainPanel.ResumeLayout(); // Resume layout updates after all controls are added (Ticket 04)

            // Restore mainPanel visibility after layout is complete
            mainPanel.Visible = true;
            Logger.LogToFile("Set mainPanel.Visible = true after SetupGameView completed.");

            // Force focus to a non-interactive control to prevent gameList flicker on first combo box interaction
            displayNameLabel.Focus();
            Logger.LogToFile("Forced focus to displayNameLabel to prevent gameList flicker.");

            // Verify focus state
            if (ActiveControl == gameList)
            {
                Logger.LogToFile("Warning: gameList regained focus after forcing focus to displayNameLabel.");
                displayNameLabel.Focus(); // Force focus again if necessary
            }
        }
    }
}