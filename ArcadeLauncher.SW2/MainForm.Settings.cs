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
        private void SetupSettingsView()
        {
            mainPanel.Controls.Clear();
            mainPanel.AutoScroll = true;
            mainPanel.Padding = new Padding(0);

            double scalingFactor = (double)Screen.PrimaryScreen.WorkingArea.Height / 1080;

            int inputHeight;
            using (var tempPanel = new Panel { Visible = false })
            using (var tempTextBox = new CustomTextBox { Font = largeFont })
            {
                tempPanel.Controls.Add(tempTextBox);
                inputHeight = tempTextBox.Height;
            }

            int buttonWidth;
            using (var tempPanel = new Panel { Visible = false })
            using (var tempButton = new CustomButton { Text = "Capture", Font = largeFont })
            {
                tempPanel.Controls.Add(tempButton);
                buttonWidth = TextRenderer.MeasureText("Capture", largeFont).Width + (int)(15 * scalingFactor);
            }

            int labelWidth = (int)(250 * scalingFactor);
            int inputWidth = (int)(600 * scalingFactor);
            int buttonHeight = inputHeight;
            int labelHeight = inputHeight;
            int columnGap = (int)(10 * scalingFactor);
            int rowHeight = (int)(40 * scalingFactor);
            int currentTop = (int)(10 * scalingFactor);
            int buttonGap = (int)(5 * scalingFactor);

            int totalColumnsWidth = labelWidth + columnGap + inputWidth + columnGap + buttonWidth + buttonGap + buttonWidth;
            int rightOfGameListSpace = Screen.PrimaryScreen.WorkingArea.Width - gameList.Width;
            int unusedSpace = rightOfGameListSpace - totalColumnsWidth;
            int column1Left = gameList.Width + (unusedSpace / 2) - 357;

            Logger.LogToFile($"SetupSettingsView Calculations: ScreenWidth={Screen.PrimaryScreen.WorkingArea.Width}, GameListWidth={gameList.Width}, ScalingFactor={scalingFactor}");
            Logger.LogToFile($"SetupSettingsView Calculations: LabelWidth={labelWidth}, InputWidth={inputWidth}, ButtonWidth={buttonWidth}, ButtonHeight={buttonHeight}, ColumnGap={columnGap}, ButtonGap={buttonGap}, TotalColumnsWidth={totalColumnsWidth}");
            Logger.LogToFile($"SetupSettingsView Centering: RightOfGameListSpace={rightOfGameListSpace}, UnusedSpace={unusedSpace}, Column1LeftBeforeAdjustment={gameList.Width + (unusedSpace / 2)}, Column1LeftAfterAdjustment={column1Left}");

            int column2Left = column1Left + labelWidth + columnGap;
            int column3Left = column2Left + inputWidth + columnGap;

            var columnsLabel = new Label { Text = "Number of Columns", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0) };
            var columnsComboBox = new ComboBox { Top = currentTop, Left = column2Left, Width = inputWidth, Height = inputHeight, DropDownStyle = ComboBoxStyle.DropDownList, Font = largeFont };
            columnsComboBox.Items.AddRange(new object[] { 5, 6, 7, 8, 9 });
            columnsComboBox.SelectedItem = settings.NumberOfColumns > 0 ? settings.NumberOfColumns : 7;
            currentTop += rowHeight;

            var marqueeLabel = new Label { Text = "Default Marquee Image", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0) };
            string marqueeFileName = settings.DefaultMarqueeImage != null ? Path.GetFileName(settings.DefaultMarqueeImage) : "";
            string initialMarqueePath = string.IsNullOrEmpty(marqueeFileName) ? "" : Path.Combine(ArcadeLauncher.Core.Program.InstallDir, marqueeFileName);
            var marqueeTextBox = new TextBox
            {
                Text = initialMarqueePath,
                Top = currentTop,
                Left = column2Left,
                Width = inputWidth,
                Height = inputHeight,
                Font = largeFont,
                ReadOnly = true
            };
            var marqueeButton = new Button { Text = "Browse", Top = currentTop, Left = column3Left, Width = buttonWidth, Height = buttonHeight, Font = largeFont };
            currentTop += rowHeight;

            var controllerLabel = new Label { Text = "Default Controller Image", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0) };
            string controllerFileName = settings.DefaultControllerImage != null ? Path.GetFileName(settings.DefaultControllerImage) : "";
            string initialControllerPath = string.IsNullOrEmpty(controllerFileName) ? "" : Path.Combine(ArcadeLauncher.Core.Program.InstallDir, controllerFileName);
            var controllerTextBox = new TextBox
            {
                Text = initialControllerPath,
                Top = currentTop,
                Left = column2Left,
                Width = inputWidth,
                Height = inputHeight,
                Font = largeFont,
                ReadOnly = true
            };
            var controllerButton = new Button { Text = "Browse", Top = currentTop, Left = column3Left, Width = buttonWidth, Height = buttonHeight, Font = largeFont };
            currentTop += rowHeight;

            string initialMarqueeText = marqueeTextBox.Text;
            string initialControllerText = controllerTextBox.Text;

            var buttonLabels = new string[] { "Left", "Right", "Up", "Down", "Select", "Exit", "Kill" };
            var buttonTextBoxes = new TextBox[buttonLabels.Length];
            var clearButtons = new Button[buttonLabels.Length];
            var captureButtons = new Button[buttonLabels.Length];

            for (int i = 0; i < buttonLabels.Length; i++)
            {
                var buttonLabel = new Label { Text = $"Define {buttonLabels[i]} Button(s)", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0) };
                buttonTextBoxes[i] = new TextBox
                {
                    Top = currentTop,
                    Left = column2Left,
                    Width = inputWidth,
                    Height = inputHeight,
                    Font = largeFont,
                    ReadOnly = true
                };
                if (!settings.InputMappings.ContainsKey(buttonLabels[i]))
                {
                    settings.InputMappings[buttonLabels[i]] = new List<string>();
                    Logger.LogToFile($"Initialized missing key in InputMappings: {buttonLabels[i]}");
                }
                if (settings.InputMappings.TryGetValue(buttonLabels[i], out var mappings))
                {
                    buttonTextBoxes[i].Text = String.Join(", ", mappings.ToArray());
                }
                else
                {
                    buttonTextBoxes[i].Text = "";
                    Logger.LogToFile($"Failed to retrieve mappings for key: {buttonLabels[i]}");
                }
                clearButtons[i] = new Button { Text = "Clear", Top = currentTop, Left = column3Left, Width = buttonWidth, Height = buttonHeight, Font = largeFont };
                captureButtons[i] = new Button { Text = "Capture", Top = currentTop, Left = column3Left + buttonWidth + buttonGap, Width = buttonWidth, Height = buttonHeight, Font = largeFont };

                int index = i;
                clearButtons[i].Click += (s, e) =>
                {
                    try
                    {
                        settings.InputMappings[buttonLabels[index]] = new List<string>();
                        buttonTextBoxes[index].Text = "";
                        Logger.LogToFile($"Cleared mappings for {buttonLabels[index]}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogToFile($"Error clearing mappings for {buttonLabels[index]}: {ex.Message}");
                        MessageBox.Show($"Error clearing mappings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                captureButtons[i].Click += (s, e) =>
                {
                    try
                    {
                        var captureForm = new Form
                        {
                            Text = "",
                            Size = new Size((int)(300 * scalingFactor), (int)(150 * scalingFactor)),
                            StartPosition = FormStartPosition.CenterParent,
                            MinimizeBox = false,
                            MaximizeBox = false,
                            ShowIcon = false,
                            BackColor = ColorTranslator.FromHtml("#F3F3F3")
                        };
                        var captureLabel = new Label
                        {
                            Text = "Press a key to capture...",
                            AutoSize = true,
                            TextAlign = ContentAlignment.MiddleCenter,
                            Font = largeFont
                        };
                        captureForm.Controls.Add(captureLabel);

                        captureForm.Load += (sender, args) =>
                        {
                            captureLabel.Left = (captureForm.ClientSize.Width - captureLabel.Width) / 2;
                            int totalHeight = captureForm.Height;
                            int clientHeight = captureForm.ClientSize.Height;
                            int titleBarHeight = SystemInformation.CaptionHeight;
                            int topBorderHeight = SystemInformation.BorderSize.Height;
                            int bottomBorderHeight = SystemInformation.BorderSize.Height;
                            int nonClientHeightAbove = titleBarHeight + topBorderHeight;
                            int nonClientHeightBelow = bottomBorderHeight;
                            int visibleTotalHeight = clientHeight + nonClientHeightAbove + nonClientHeightBelow;
                            int measuredVisibleTotalHeight = 105;
                            int captureLabelHeight = captureLabel.Height;
                            int totalCenterY = measuredVisibleTotalHeight / 2;
                            int labelTop = totalCenterY - (captureLabelHeight / 2);
                            captureLabel.Top = labelTop - nonClientHeightAbove;

                            Logger.LogToFile($"Capture Key Pop-up Centering Diagnostics:");
                            Logger.LogToFile($"  Total Height (including shadow): {totalHeight}");
                            Logger.LogToFile($"  Client Height: {clientHeight}");
                            Logger.LogToFile($"  Title Bar Height: {titleBarHeight}");
                            Logger.LogToFile($"  Top Border Height: {topBorderHeight}");
                            Logger.LogToFile($"  Bottom Border Height: {bottomBorderHeight}");
                            Logger.LogToFile($"  Non-Client Height Above: {nonClientHeightAbove}");
                            Logger.LogToFile($"  Non-Client Height Below: {nonClientHeightBelow}");
                            Logger.LogToFile($"  Calculated Visible Total Height: {visibleTotalHeight}");
                            Logger.LogToFile($"  Measured Visible Total Height: {measuredVisibleTotalHeight}");
                            Logger.LogToFile($"  Label Height: {captureLabelHeight}");
                            Logger.LogToFile($"  Total Center Y (measured): {totalCenterY}");
                            Logger.LogToFile($"  Label Top (before adjustment): {labelTop}");
                            Logger.LogToFile($"  Final Label Top: {captureLabel.Top}");
                        };

                        captureForm.KeyPreview = true;
                        captureForm.KeyDown += (sender, args) =>
                        {
                            string keyString;
                            switch (args.KeyCode)
                            {
                                case Keys.Left:
                                    keyString = "LeftArrow";
                                    break;
                                case Keys.Right:
                                    keyString = "RightArrow";
                                    break;
                                case Keys.Up:
                                    keyString = "UpArrow";
                                    break;
                                case Keys.Down:
                                    keyString = "DownArrow";
                                    break;
                                default:
                                    keyString = args.KeyCode.ToString();
                                    break;
                            }
                            if (!settings.InputMappings[buttonLabels[index]].Contains(keyString, StringComparer.OrdinalIgnoreCase))
                            {
                                settings.InputMappings[buttonLabels[index]].Add(keyString);
                                buttonTextBoxes[index].Text = String.Join(", ", settings.InputMappings[buttonLabels[index]]);
                                Logger.LogToFile($"Captured key {keyString} for {buttonLabels[index]}");
                            }
                            captureForm.Close();
                        };
                        captureForm.ShowDialog();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogToFile($"Error capturing key for {buttonLabels[index]}: {ex.Message}");
                        MessageBox.Show($"Error capturing key: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                mainPanel.Controls.Add(buttonLabel);
                mainPanel.Controls.Add(buttonTextBoxes[i]);
                mainPanel.Controls.Add(clearButtons[i]);
                mainPanel.Controls.Add(captureButtons[i]);
                currentTop += rowHeight;
            }

            var killLabel = mainPanel.Controls.OfType<Label>().FirstOrDefault(l => l.Text == "Define Kill Button(s)");
            if (killLabel != null)
            {
                killLabel.Text = "Define Kill Button(s) (x2)";
            }

            var saveSettingsButton = new Button { Text = "Save Settings", Top = currentTop, Left = column1Left, Width = buttonWidth, Height = buttonHeight, Font = largeFont };
            var cancelSettingsButton = new Button { Text = "Cancel", Top = currentTop, Left = column1Left + buttonWidth + columnGap, Width = buttonWidth, Height = buttonHeight, Font = largeFont };

            // Add temporary button to test splash screen
            var testSplashButton = new Button
            {
                Text = "Test Splash Screen",
                Top = currentTop,
                Left = column1Left + (buttonWidth + columnGap) * 2,
                Width = buttonWidth,
                Height = buttonHeight,
                Font = largeFont
            };
            testSplashButton.Click += (s, e) =>
            {
                // Hardcode "Alan Wake" as the test game
                var testGame = games.FirstOrDefault(g => g.DisplayName == "Alan Wake");
                if (testGame != null)
                {
                    LaunchSplashScreen(testGame);
                }
                else
                {
                    MessageBox.Show("Test game 'Alan Wake' not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            currentTop += rowHeight;

            saveSettingsButton.Click += (s, e) =>
            {
                try
                {
                    settings.NumberOfColumns = (int)columnsComboBox.SelectedItem;

                    string targetDir = ArcadeLauncher.Core.Program.InstallDir;
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                        Logger.LogToFile($"Created directory: {targetDir}");
                    }

                    if (marqueeTextBox.Text != initialMarqueeText)
                    {
                        if (!string.IsNullOrEmpty(marqueeTextBox.Text) && File.Exists(marqueeTextBox.Text))
                        {
                            string targetMarqueePath = Path.Combine(targetDir, "default_marquee.png");
                            File.Copy(marqueeTextBox.Text, targetMarqueePath, true);
                            settings.DefaultMarqueeImage = "default_marquee.png";
                            marqueeTextBox.Text = targetMarqueePath;
                            Logger.LogToFile($"Copied default marquee image to: {targetMarqueePath}");
                        }
                        else if (!string.IsNullOrEmpty(marqueeTextBox.Text))
                        {
                            Logger.LogToFile($"Default marquee image file not found: {marqueeTextBox.Text}");
                            MessageBox.Show($"Default marquee image file not found: {marqueeTextBox.Text}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }

                    if (controllerTextBox.Text != initialControllerText)
                    {
                        if (!string.IsNullOrEmpty(controllerTextBox.Text) && File.Exists(controllerTextBox.Text))
                        {
                            string targetControllerPath = Path.Combine(targetDir, "default_controller.png");
                            File.Copy(controllerTextBox.Text, targetControllerPath, true);
                            settings.DefaultControllerImage = "default_controller.png";
                            controllerTextBox.Text = targetControllerPath;
                            Logger.LogToFile($"Copied default controller image to: {targetControllerPath}");
                        }
                        else if (!string.IsNullOrEmpty(controllerTextBox.Text))
                        {
                            Logger.LogToFile($"Default controller image file not found: {controllerTextBox.Text}");
                            MessageBox.Show($"Default controller image file not found: {controllerTextBox.Text}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }

                    DataManager.SaveSettings(settings);
                    Logger.LogToFile("Settings saved successfully.");
                    MessageBox.Show("Settings saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    SetupSettingsView();
                }
                catch (Exception ex)
                {
                    Logger.LogToFile($"Failed to save settings: {ex.Message}");
                    MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            cancelSettingsButton.Click += (s, e) =>
            {
                SetupSettingsView();
            };

            marqueeButton.Click += (s, e) =>
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp";
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        marqueeTextBox.Text = dialog.FileName;
                        Logger.LogToFile($"Selected default marquee image: {dialog.FileName}");
                    }
                }
            };

            controllerButton.Click += (s, e) =>
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp";
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        controllerTextBox.Text = dialog.FileName;
                        Logger.LogToFile($"Selected default controller image: {dialog.FileName}");
                    }
                }
            };

            mainPanel.Controls.Add(columnsLabel);
            mainPanel.Controls.Add(columnsComboBox);
            mainPanel.Controls.Add(marqueeLabel);
            mainPanel.Controls.Add(marqueeTextBox);
            mainPanel.Controls.Add(marqueeButton);
            mainPanel.Controls.Add(controllerLabel);
            mainPanel.Controls.Add(controllerTextBox);
            mainPanel.Controls.Add(controllerButton);
            mainPanel.Controls.Add(saveSettingsButton);
            mainPanel.Controls.Add(cancelSettingsButton);
            mainPanel.Controls.Add(testSplashButton); // Add the temporary button
            foreach (var textBox in buttonTextBoxes)
            {
                mainPanel.Controls.Add(textBox);
            }
            foreach (var clearButton in clearButtons)
            {
                mainPanel.Controls.Add(clearButton);
            }
            foreach (var captureButton in captureButtons)
            {
                mainPanel.Controls.Add(captureButton);
            }
        }
    }
}