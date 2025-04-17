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

            mainPanel.SuspendLayout();
            mainPanel.Controls.Clear();
            mainPanel.Tag = game;
            mainPanel.AutoScroll = true;
            mainPanel.Padding = new Padding(0);

            double scalingFactor = (double)Screen.PrimaryScreen.WorkingArea.Height / 1080;

            using (var tempPanel = new Panel { Visible = false })
            using (var tempTextBox = new CustomTextBox { Font = largeFont })
            {
                tempPanel.Controls.Add(tempTextBox);
                inputHeight = tempTextBox.Height;
            }

            int gameListWidth = (int)(375 * scalingFactor);
            gameList.Width = gameListWidth;
            int labelWidth = (int)(200 * scalingFactor);
            inputWidth = (int)(690 * 1.12 * scalingFactor);
            int labelHeight = inputHeight;

            int buttonWidth;
            using (var tempPanel = new Panel { Visible = false })
            using (var tempButton = new CustomButton { Text = "Browse", Font = largeFont })
            {
                tempPanel.Controls.Add(tempButton);
                buttonWidth = TextRenderer.MeasureText("Browse", largeFont).Width + (int)(20 * scalingFactor);
            }

            int buttonHeight = inputHeight;
            smallButtonWidth = inputHeight;
            smallButtonHeight = inputHeight;
            int saveCancelButtonWidth = (int)(100 * scalingFactor);
            int saveCancelButtonHeight = inputHeight;
            int columnGap = (int)(10 * scalingFactor);
            int rowHeight = (int)(50 * scalingFactor);
            smallButtonGap = (int)(5 * scalingFactor);
            int sectionGap = (int)(18 * scalingFactor);

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
            int marqueeHeight = (int)Math.Round(thumbnailWidth * (360.0 / 1920.0));
            double artBoxAspect = 4.0 / 3.0;
            double controllerAspect = 9.0 / 16.0; // Revert to original aspect ratio for preview height
            int artBoxHeight = (int)Math.Round(thumbnailWidth * artBoxAspect);
            int controllerHeight = (int)Math.Round(thumbnailWidth * controllerAspect);

            int screenWidth = Screen.PrimaryScreen.WorkingArea.Width;
            int artworkLeft = screenWidth - gameList.Width - thumbnailWidth - marginAndGapRounded;
            int artworkTop = marginAndGapRounded;

            Image artBoxImage = null;
            Image marqueeImage = null;
            Image splashScreenImage = null;
            if (game != null)
            {
                if (File.Exists(game.ArtBoxPath))
                {
                    using (var tempImage = Image.FromFile(game.ArtBoxPath))
                    {
                        artBoxImage = new Bitmap(tempImage);
                    }
                }
                string previewPath = game.MarqueePath.Replace(".png", "Preview.png");
                if (File.Exists(previewPath))
                {
                    Logger.LogToFile($"Loading preview image from: {previewPath}");
                    using (var tempImage = Image.FromFile(previewPath))
                    {
                        marqueeImage = new Bitmap(tempImage);
                    }
                }
                else if (File.Exists(game.MarqueePath))
                {
                    Logger.LogToFile($"Preview not found, falling back to full image: {game.MarqueePath}");
                    using (var tempImage = Image.FromFile(game.MarqueePath))
                    {
                        marqueeImage = new Bitmap(tempImage);
                    }
                }
                if (game.SplashScreenPath.ContainsKey("1440p") && File.Exists(game.SplashScreenPath["1440p"]))
                {
                    using (var tempImage = Image.FromFile(game.SplashScreenPath["1440p"]))
                    {
                        splashScreenImage = new Bitmap(tempImage);
                    }
                }
                else if (game.SplashScreenPath.ContainsKey("1080p") && File.Exists(game.SplashScreenPath["1080p"]))
                {
                    using (var tempImage = Image.FromFile(game.SplashScreenPath["1080p"]))
                    {
                        splashScreenImage = new Bitmap(tempImage);
                    }
                }
            }

            var artBoxPictureBox = new CustomPictureBox
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

            var marqueePictureBox = new CustomPictureBox
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

            var splashScreenPictureBox = new CustomPictureBox
            {
                Top = artworkTop,
                Left = artworkLeft,
                Width = thumbnailWidth,
                Height = controllerHeight, // Use the original controllerHeight for consistency
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.StretchImage,
                Padding = new Padding(0),
                Image = splashScreenImage
            };

            int currentTop = artBoxPictureBox.Top;

            int totalColumnsWidth = labelWidth + columnGap + inputWidth + columnGap + buttonWidth;
            int availableSpace = artworkLeft;
            int unusedSpace = availableSpace - totalColumnsWidth;
            int column1Left = unusedSpace / 2;

            column2Left = column1Left + labelWidth + columnGap;
            column3Left = column2Left + inputWidth + columnGap;

            Logger.LogToFile($"SetupGameView Centering: GameListWidth={gameList.Width}, ArtworkLeft={artworkLeft}, AvailableSpace={availableSpace}, TotalColumnsWidth={totalColumnsWidth}, UnusedSpace={unusedSpace}, Column1Left={column1Left}");

            var displayNameLabel = new Label { Text = "Display Name", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0) };
            var displayNameTextBox = new CustomTextBox { Text = game?.DisplayName ?? "", Top = currentTop, Left = column2Left, Width = inputWidth, Height = inputHeight, Font = largeFont, Name = "displayNameTextBox" };
            currentTop += sectionGap + inputHeight;

            var alphabetizeNameLabel = new Label { Text = "Alphabetizing Name", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0) };
            var alphabetizeNameTextBox = new CustomTextBox { Text = game?.AlphabetizeName ?? "", Top = currentTop, Left = column2Left, Width = inputWidth, Height = inputHeight, Font = largeFont, Name = "alphabetizeNameTextBox" };
            currentTop += sectionGap + inputHeight;

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
            };
            gameTypeComboBox.Items.Add("PC");
            gameTypeComboBox.Items.AddRange(plugins.Select(p => p.Name).ToArray());
            gameTypeComboBox.SelectedItem = game?.Type ?? "PC";

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
                    gameList.SelectedItem = game;
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

            gameList.GotFocus += gameListGotFocus;
            gameList.LostFocus += gameListLostFocus;
            gameTypeComboBox.GotFocus += comboBoxGotFocus;
            gameTypeComboBox.LostFocus += comboBoxLostFocus;
            gameTypeComboBox.DropDown += comboBoxDropDown;
            gameTypeComboBox.DropDownClosed += comboBoxDropDownClosed;
            gameTypeComboBox.SelectedIndexChanged += comboBoxSelectedIndexChanged;

            currentTop += sectionGap + inputHeight;

            var executablePathLabel = new Label { Text = "Executable Path", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0) };
            var executableTextBox = new CustomTextBox { Text = game?.ExecutablePath ?? "", Top = currentTop, Left = column2Left, Width = inputWidth, Height = inputHeight, Font = largeFont, Name = "executableTextBox" };
            var executableButton = new CustomButton { Text = "Browse", Top = currentTop, Left = column3Left, Width = buttonWidth, Height = buttonHeight, Font = largeFont };
            currentTop += sectionGap + inputHeight;

            var ledBlinkyLabel = new Label { Text = "LEDBlinky Animation", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0) };
            var ledBlinkyTextBox = new CustomTextBox { Text = game?.LEDBlinkyCommand ?? "", Top = currentTop, Left = column2Left, Width = inputWidth, Height = inputHeight, Font = largeFont, Name = "ledBlinkyTextBox" };
            currentTop += sectionGap + inputHeight;

            // Splash Screen Duration (moved between LEDBlinky and Pre-Launch)
            var splashDurationLabel = new Label
            {
                Text = "Splash Duration (s)",
                Top = currentTop,
                Left = column1Left,
                Width = labelWidth,
                Height = labelHeight,
                Font = largeFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0)
            };
            var splashDurationTextBox = new CustomTextBox
            {
                Top = currentTop,
                Left = column2Left,
                Width = inputWidth,
                Height = inputHeight,
                Font = largeFont,
                Text = game?.SplashDuration.ToString() ?? "3",
                Name = "splashDurationTextBox"
            };
            currentTop += inputHeight + sectionGap;

            var preLaunchLabel = new Label { Text = "Pre-Launch Commands", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0) };
            preLaunchCommandsList = new List<TextBox>();
            preLaunchMinusButtons = new List<Button>();
            preLaunchPlusButtons = new List<Button>();
            int preLaunchCount = game?.PreLaunchCommands?.Count > 0 ? game.PreLaunchCommands.Count : 1;
            Logger.LogToFile($"Loading Pre-Launch Commands: Count={preLaunchCount}, Commands=[{string.Join(", ", game?.PreLaunchCommands ?? new List<string>())}]");
            preLaunchFirstInputTop = currentTop;
            currentTop = preLaunchFirstInputTop;
            for (int i = 0; i < preLaunchCount; i++)
            {
                var commandTextBox = new CustomTextBox
                {
                    Top = currentTop,
                    Left = column2Left,
                    Width = inputWidth,
                    Height = inputHeight,
                    Font = largeFont,
                    Text = game?.PreLaunchCommands?.Count > i ? game.PreLaunchCommands[i] : "",
                    Visible = true,
                    Tag = "PreLaunch"
                };
                var minusButton = new CustomButton { Text = "−", Top = currentTop, Left = column3Left, Width = smallButtonWidth, Height = smallButtonHeight, Font = largeFont, Visible = true };
                var plusButton = new CustomButton { Text = "+", Top = currentTop, Left = column3Left + smallButtonWidth + smallButtonGap, Width = smallButtonWidth, Height = smallButtonHeight, Font = largeFont, Visible = true };
                preLaunchCommandsList.Add(commandTextBox);
                preLaunchMinusButtons.Add(minusButton);
                preLaunchPlusButtons.Add(plusButton);
                mainPanel.Controls.Add(commandTextBox);
                mainPanel.Controls.Add(minusButton);
                mainPanel.Controls.Add(plusButton);
                Logger.LogToFile($"Pre-Launch Command {i + 1}: Text='{commandTextBox.Text}', Top={commandTextBox.Top}");
                currentTop += inputHeight;
            }

            int lastPreLaunchTop = preLaunchCommandsList.Last().Top;
            int desiredPostCloseTop = lastPreLaunchTop + inputHeight + sectionGap;

            currentTop = desiredPostCloseTop;
            postCloseLabel = new Label { Text = "Post-Close Commands", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0) };
            postCloseCommandsList = new List<TextBox>();
            postCloseMinusButtons = new List<Button>();
            postClosePlusButtons = new List<Button>();
            int postCloseCount = game?.PostExitCommands?.Count > 0 ? game.PostExitCommands.Count : 1;
            Logger.LogToFile($"Loading Post-Close Commands: Count={postCloseCount}, Commands=[{string.Join(", ", game?.PostExitCommands ?? new List<string>())}]");
            postCloseFirstInputTop = currentTop;
            currentTop = postCloseFirstInputTop;
            for (int i = 0; i < postCloseCount; i++)
            {
                var commandTextBox = new CustomTextBox
                {
                    Top = currentTop,
                    Left = column2Left,
                    Width = inputWidth,
                    Height = inputHeight,
                    Font = largeFont,
                    Text = game?.PostExitCommands?.Count > i ? game.PostExitCommands[i] : "",
                    Visible = true,
                    Tag = "PostClose"
                };
                var minusButton = new CustomButton { Text = "−", Top = currentTop, Left = column3Left, Width = smallButtonWidth, Height = smallButtonHeight, Font = largeFont, Visible = true };
                var plusButton = new CustomButton { Text = "+", Top = currentTop, Left = column3Left + smallButtonWidth + smallButtonGap, Width = smallButtonWidth, Height = smallButtonHeight, Font = largeFont, Visible = true };
                postCloseCommandsList.Add(commandTextBox);
                postCloseMinusButtons.Add(minusButton);
                postClosePlusButtons.Add(plusButton);
                mainPanel.Controls.Add(commandTextBox);
                mainPanel.Controls.Add(minusButton);
                mainPanel.Controls.Add(plusButton);
                Logger.LogToFile($"Post-Close Command {i + 1}: Text='{commandTextBox.Text}', Top={commandTextBox.Top}");
                currentTop += inputHeight;
            }

            currentTop += sectionGap;
            notesLabel = new Label { Text = "Notes", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0) };
            notesTextBox = new CustomTextBox
            {
                Top = currentTop,
                Left = column2Left,
                Width = inputWidth,
                Height = inputHeight * 3,
                Font = largeFont,
                Multiline = true,
                AcceptsReturn = true,
                ScrollBars = ScrollBars.Vertical,
                Text = game?.Notes ?? "",
                Name = "notesTextBox"
            };
            currentTop += notesTextBox.Height + sectionGap;

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
                Checked = isNewGame ? true : (game?.IsInProgress ?? false),
                Name = "inProgressCheckBox"
            };
            currentTop += inputHeight + sectionGap;

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
                Checked = game?.HideMouseCursor ?? false,
                Name = "hideMouseCursorCheckBox"
            };
            currentTop += inputHeight + sectionGap;

            saveButton = new CustomButton { Text = "Save Game", Top = currentTop, Left = column1Left, Width = saveCancelButtonWidth, Height = saveCancelButtonHeight, Font = largeFont };
            cancelButton = new CustomButton { Text = "Cancel", Top = currentTop, Left = column1Left + saveCancelButtonWidth + columnGap, Width = saveCancelButtonWidth, Height = saveCancelButtonHeight, Font = largeFont };
            currentTop += sectionGap + inputHeight;

            if (gameList.SelectedItem != game && gameList.Items.Contains(game))
            {
                gameList.SelectedItem = game;
            }
            gameList.SelectedIndexChanged += selectedIndexChangedHandler;

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

            ReassignPreLaunchButtons();
            ReassignPostCloseButtons();

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
                        if (File.Exists(game.MarqueePath.Replace(".png", "Preview.png")))
                        {
                            try
                            {
                                File.Delete(game.MarqueePath.Replace(".png", "Preview.png"));
                                Logger.LogToFile($"Removed preview artwork file: {game.MarqueePath.Replace(".png", "Preview.png")}");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogToFile($"Failed to remove preview artwork file {game.MarqueePath.Replace(".png", "Preview.png")}: {ex.Message}");
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

            splashScreenPictureBox.Click += (s, e) =>
            {
                var contextMenu = new ContextMenuStrip();
                contextMenu.Items.Add("Search Online", null, async (sender, args) =>
                {
                    if (string.IsNullOrEmpty(displayNameTextBox.Text))
                    {
                        MessageBox.Show("Please enter a display name to search for a splash screen image.");
                        return;
                    }
                    await SearchCoverArt(displayNameTextBox.Text, splashScreenPictureBox, game, "SplashScreen");
                });
                contextMenu.Items.Add("Select File", null, (sender, args) => SelectImage(splashScreenPictureBox, "SplashScreen", game));
                if (splashScreenPictureBox.Image != null)
                {
                    contextMenu.Items.Add("Remove Artwork", null, (sender, args) =>
                    {
                        foreach (var path in game.SplashScreenPath.Values)
                        {
                            if (File.Exists(path))
                            {
                                try
                                {
                                    File.Delete(path);
                                    Logger.LogToFile($"Removed splash screen file: {path}");
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogToFile($"Failed to remove splash screen file {path}: {ex.Message}");
                                }
                            }
                        }
                        splashScreenPictureBox.Image?.Dispose();
                        splashScreenPictureBox.Image = null;
                        game.SplashScreenPath.Clear();
                        Logger.LogToFile("Cleared SplashScreenPath for game.");
                    });
                }
                contextMenu.Show(splashScreenPictureBox, splashScreenPictureBox.PointToClient(Cursor.Position));
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
                    gameToSave.DisplayName = mainPanel.Controls.Find("displayNameTextBox", true).FirstOrDefault()?.Text ?? "";
                    gameToSave.AlphabetizeName = mainPanel.Controls.Find("alphabetizeNameTextBox", true).FirstOrDefault()?.Text ?? "";
                    gameToSave.Type = (mainPanel.Controls.Find("gameTypeComboBox", true).FirstOrDefault() as ComboBox)?.SelectedItem?.ToString() ?? "PC";
                    gameToSave.ExecutablePath = mainPanel.Controls.Find("executableTextBox", true).FirstOrDefault()?.Text ?? "";
                    gameToSave.LEDBlinkyCommand = mainPanel.Controls.Find("ledBlinkyTextBox", true).FirstOrDefault()?.Text ?? "";
                    gameToSave.Notes = mainPanel.Controls.Find("notesTextBox", true).FirstOrDefault()?.Text ?? "";
                    gameToSave.IsInProgress = (mainPanel.Controls.Find("inProgressCheckBox", true).FirstOrDefault() as CheckBox)?.Checked ?? false;
                    gameToSave.HideMouseCursor = (mainPanel.Controls.Find("hideMouseCursorCheckBox", true).FirstOrDefault() as CheckBox)?.Checked ?? false;

                    if (int.TryParse(splashDurationTextBox.Text, out int splashDuration))
                    {
                        gameToSave.SplashDuration = Math.Max(2, Math.Min(10, splashDuration));
                    }
                    else
                    {
                        gameToSave.SplashDuration = 3;
                    }

                    gameToSave.PreLaunchCommands.Clear();
                    var preLaunchTextBoxes = mainPanel.Controls.OfType<TextBox>().Where(tb => tb.Tag?.ToString() == "PreLaunch" && tb.Visible).OrderBy(tb => tb.Top).ToList();
                    for (int i = 0; i < preLaunchTextBoxes.Count; i++)
                    {
                        var command = preLaunchTextBoxes[i].Text.Trim();
                        if (!string.IsNullOrEmpty(command))
                        {
                            gameToSave.PreLaunchCommands.Add(command);
                        }
                    }

                    gameToSave.PostExitCommands.Clear();
                    var postCloseTextBoxes = mainPanel.Controls.OfType<TextBox>().Where(tb => tb.Tag?.ToString() == "PostClose" && tb.Visible).OrderBy(tb => tb.Top).ToList();
                    for (int i = 0; i < postCloseTextBoxes.Count; i++)
                    {
                        var command = postCloseTextBoxes[i].Text.Trim();
                        if (!string.IsNullOrEmpty(command))
                        {
                            gameToSave.PostExitCommands.Add(command);
                        }
                    }

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
            mainPanel.Controls.Add(splashDurationLabel);
            mainPanel.Controls.Add(splashDurationTextBox);
            mainPanel.Controls.Add(preLaunchLabel);
            mainPanel.Controls.Add(postCloseLabel);
            mainPanel.Controls.Add(notesLabel);
            mainPanel.Controls.Add(notesTextBox);
            mainPanel.Controls.Add(inProgressLabel);
            mainPanel.Controls.Add(inProgressCheckBox);
            mainPanel.Controls.Add(hideMouseCursorLabel);
            mainPanel.Controls.Add(hideMouseCursorCheckBox);
            mainPanel.Controls.Add(saveButton);
            mainPanel.Controls.Add(cancelButton);
            mainPanel.Controls.Add(artBoxPictureBox);
            mainPanel.Controls.Add(marqueePictureBox);
            mainPanel.Controls.Add(splashScreenPictureBox);

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

            mainPanel.ResumeLayout();

            mainPanel.Visible = true;
            Logger.LogToFile("Set mainPanel.Visible = true after SetupGameView completed.");

            displayNameLabel.Focus();
            Logger.LogToFile("Forced focus to displayNameLabel to prevent gameList flicker.");

            if (ActiveControl == gameList)
            {
                Logger.LogToFile("Warning: gameList regained focus after forcing focus to displayNameLabel.");
                displayNameLabel.Focus();
            }
        }
    }
}