using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ArcadeLauncher.Core;

namespace ArcadeLauncher.SW2
{
    public partial class MainForm : Form
    {
        private void ReassignPreLaunchButtons()
        {
            Logger.LogToFile("Reassigning Pre-Launch button event handlers:");
            for (int i = 0; i < preLaunchCommandsList.Count; i++)
            {
                int index = i;
                // Clear existing handlers by removing the specific delegate
                preLaunchMinusButtons[index].Click -= PreLaunchMinusButton_Click;
                preLaunchPlusButtons[index].Click -= PreLaunchPlusButton_Click;

                // Assign new handlers
                preLaunchMinusButtons[index].Click += PreLaunchMinusButton_Click;
                preLaunchPlusButtons[index].Click += PreLaunchPlusButton_Click;

                Logger.LogToFile($"  Pre-Launch Line {i + 1}: Index={index}, Top={preLaunchCommandsList[i].Top}, MinusButton Top={preLaunchMinusButtons[i].Top}, PlusButton Top={preLaunchPlusButtons[i].Top}");
            }
        }

        private void PreLaunchMinusButton_Click(object sender, EventArgs e)
        {
            int index = preLaunchMinusButtons.IndexOf((Button)sender);
            Logger.LogToFile($"Pre-Launch Minus Button Clicked: Index={index}, Total Lines={preLaunchCommandsList.Count}");

            if (preLaunchCommandsList.Count > 1)
            {
                // Store references to the controls to remove
                var textBoxToRemove = preLaunchCommandsList[index];
                var minusButtonToRemove = preLaunchMinusButtons[index];
                var plusButtonToRemove = preLaunchPlusButtons[index];

                // Remove the elements from the lists
                preLaunchCommandsList.RemoveAt(index);
                preLaunchMinusButtons.RemoveAt(index);
                preLaunchPlusButtons.RemoveAt(index);

                // Remove the controls from the panel
                mainPanel.Controls.Remove(textBoxToRemove);
                mainPanel.Controls.Remove(minusButtonToRemove);
                mainPanel.Controls.Remove(plusButtonToRemove);

                Logger.LogToFile($"Removed Pre-Launch Line at Index={index}: Text='{textBoxToRemove.Text}', Top={textBoxToRemove.Top}");

                // Shift remaining controls up
                for (int j = index; j < preLaunchCommandsList.Count; j++)
                {
                    preLaunchCommandsList[j].Top -= inputHeight;
                    preLaunchMinusButtons[j].Top -= inputHeight;
                    preLaunchPlusButtons[j].Top -= inputHeight;
                }
                postCloseLabel.Top -= inputHeight;
                for (int j = 0; j < postCloseCommandsList.Count; j++)
                {
                    postCloseCommandsList[j].Top -= inputHeight;
                    postCloseMinusButtons[j].Top -= inputHeight;
                    postClosePlusButtons[j].Top -= inputHeight;
                }
                // Adjust Notes, In Progress, and Hide Mouse Cursor section positions
                if (notesLabel != null)
                {
                    notesLabel.Top -= inputHeight;
                }
                if (notesTextBox != null)
                {
                    notesTextBox.Top -= inputHeight;
                }
                var inProgressLabel = mainPanel.Controls.OfType<Label>().FirstOrDefault(c => c.Text == "In Progress");
                if (inProgressLabel != null)
                {
                    inProgressLabel.Top -= inputHeight;
                }
                var inProgressCheckBox = mainPanel.Controls.OfType<CheckBox>().FirstOrDefault(c => c.Name == "inProgressCheckBox");
                if (inProgressCheckBox != null)
                {
                    inProgressCheckBox.Top -= inputHeight;
                }
                var hideMouseCursorLabel = mainPanel.Controls.OfType<Label>().FirstOrDefault(c => c.Text == "Hide Mouse Cursor");
                if (hideMouseCursorLabel != null)
                {
                    hideMouseCursorLabel.Top -= inputHeight;
                }
                var hideMouseCursorCheckBox = mainPanel.Controls.OfType<CheckBox>().FirstOrDefault(c => c.Name == "hideMouseCursorCheckBox");
                if (hideMouseCursorCheckBox != null)
                {
                    hideMouseCursorCheckBox.Top -= inputHeight;
                }
                saveButton.Top -= inputHeight;
                cancelButton.Top -= inputHeight;

                // Reassign all handlers after deletion
                ReassignPreLaunchButtons();
            }
            else
            {
                preLaunchCommandsList[0].Text = "";
                Logger.LogToFile("Cleared the only Pre-Launch line since count is 1.");
            }
        }

        private void PreLaunchPlusButton_Click(object sender, EventArgs e)
        {
            int index = preLaunchPlusButtons.IndexOf((Button)sender);
            Logger.LogToFile($"Pre-Launch Plus Button Clicked: Index={index}, Total Lines={preLaunchCommandsList.Count}");

            var newTextBox = new CustomTextBox { Top = preLaunchCommandsList[index].Top + inputHeight, Left = column2Left, Width = inputWidth, Height = inputHeight, Font = largeFont, Visible = true, Tag = "PreLaunch" };
            newTextBox.Text = "";
            preLaunchCommandsList.Insert(index + 1, newTextBox);
            var newMinusButton = new CustomButton { Text = "−", Top = newTextBox.Top, Left = column3Left, Width = smallButtonWidth, Height = smallButtonHeight, Font = largeFont, Visible = true };
            var newPlusButton = new CustomButton { Text = "+", Top = newTextBox.Top, Left = column3Left + smallButtonWidth + smallButtonGap, Width = smallButtonWidth, Height = smallButtonHeight, Font = largeFont, Visible = true };
            preLaunchMinusButtons.Insert(index + 1, newMinusButton);
            preLaunchPlusButtons.Insert(index + 1, newPlusButton);

            // Update the Top positions of all subsequent Pre-Launch commands
            for (int j = index + 2; j < preLaunchCommandsList.Count; j++)
            {
                preLaunchCommandsList[j].Top += inputHeight;
                preLaunchMinusButtons[j].Top += inputHeight;
                preLaunchPlusButtons[j].Top += inputHeight;
            }

            mainPanel.Controls.Add(newTextBox);
            mainPanel.Controls.Add(newMinusButton);
            mainPanel.Controls.Add(newPlusButton);

            postCloseLabel.Top += inputHeight;
            for (int j = 0; j < postCloseCommandsList.Count; j++)
            {
                postCloseCommandsList[j].Top += inputHeight;
                postCloseMinusButtons[j].Top += inputHeight;
                postClosePlusButtons[j].Top += inputHeight;
            }
            // Adjust Notes, In Progress, and Hide Mouse Cursor section positions
            if (notesLabel != null)
            {
                notesLabel.Top += inputHeight;
            }
            if (notesTextBox != null)
            {
                notesTextBox.Top += inputHeight;
            }
            var inProgressLabel = mainPanel.Controls.OfType<Label>().FirstOrDefault(c => c.Text == "In Progress");
            if (inProgressLabel != null)
            {
                inProgressLabel.Top += inputHeight;
            }
            var inProgressCheckBox = mainPanel.Controls.OfType<CheckBox>().FirstOrDefault(c => c.Name == "inProgressCheckBox");
            if (inProgressCheckBox != null)
            {
                inProgressCheckBox.Top += inputHeight;
            }
            var hideMouseCursorLabel = mainPanel.Controls.OfType<Label>().FirstOrDefault(c => c.Text == "Hide Mouse Cursor");
            if (hideMouseCursorLabel != null)
            {
                hideMouseCursorLabel.Top += inputHeight;
            }
            var hideMouseCursorCheckBox = mainPanel.Controls.OfType<CheckBox>().FirstOrDefault(c => c.Name == "hideMouseCursorCheckBox");
            if (hideMouseCursorCheckBox != null)
            {
                hideMouseCursorCheckBox.Top += inputHeight;
            }
            saveButton.Top += inputHeight;
            cancelButton.Top += inputHeight;

            // Reassign all handlers after insertion
            ReassignPreLaunchButtons();
        }

        private void ReassignPostCloseButtons()
        {
            Logger.LogToFile("Reassigning Post-Close button event handlers:");
            for (int i = 0; i < postCloseCommandsList.Count; i++)
            {
                int index = i;
                // Clear existing handlers by removing the specific delegate
                postCloseMinusButtons[index].Click -= PostCloseMinusButton_Click;
                postClosePlusButtons[index].Click -= PostClosePlusButton_Click;

                // Assign new handlers
                postCloseMinusButtons[index].Click += PostCloseMinusButton_Click;
                postClosePlusButtons[index].Click += PostClosePlusButton_Click;

                Logger.LogToFile($"  Post-Close Line {i + 1}: Index={index}, Top={postCloseCommandsList[i].Top}, MinusButton Top={postCloseMinusButtons[i].Top}, PlusButton Top={postClosePlusButtons[i].Top}");
            }
        }

        private void PostCloseMinusButton_Click(object sender, EventArgs e)
        {
            int index = postCloseMinusButtons.IndexOf((Button)sender);
            Logger.LogToFile($"Post-Close Minus Button Clicked: Index={index}, Total Lines={postCloseCommandsList.Count}");

            if (postCloseCommandsList.Count > 1)
            {
                // Store references to the controls to remove
                var textBoxToRemove = postCloseCommandsList[index];
                var minusButtonToRemove = postCloseMinusButtons[index];
                var plusButtonToRemove = postClosePlusButtons[index];

                // Remove the elements from the lists
                postCloseCommandsList.RemoveAt(index);
                postCloseMinusButtons.RemoveAt(index);
                postClosePlusButtons.RemoveAt(index);

                // Remove the controls from the panel
                mainPanel.Controls.Remove(textBoxToRemove);
                mainPanel.Controls.Remove(minusButtonToRemove);
                mainPanel.Controls.Remove(plusButtonToRemove);

                Logger.LogToFile($"Removed Post-Close Line at Index={index}: Text='{textBoxToRemove.Text}', Top={textBoxToRemove.Top}");

                // Shift remaining controls up
                for (int j = index; j < postCloseCommandsList.Count; j++)
                {
                    postCloseCommandsList[j].Top -= inputHeight;
                    postCloseMinusButtons[j].Top -= inputHeight;
                    postClosePlusButtons[j].Top -= inputHeight;
                }
                // Adjust Notes, In Progress, and Hide Mouse Cursor section positions
                if (notesLabel != null)
                {
                    notesLabel.Top -= inputHeight;
                }
                if (notesTextBox != null)
                {
                    notesTextBox.Top -= inputHeight;
                }
                var inProgressLabel = mainPanel.Controls.OfType<Label>().FirstOrDefault(c => c.Text == "In Progress");
                if (inProgressLabel != null)
                {
                    inProgressLabel.Top -= inputHeight;
                }
                var inProgressCheckBox = mainPanel.Controls.OfType<CheckBox>().FirstOrDefault(c => c.Name == "inProgressCheckBox");
                if (inProgressCheckBox != null)
                {
                    inProgressCheckBox.Top -= inputHeight;
                }
                var hideMouseCursorLabel = mainPanel.Controls.OfType<Label>().FirstOrDefault(c => c.Text == "Hide Mouse Cursor");
                if (hideMouseCursorLabel != null)
                {
                    hideMouseCursorLabel.Top -= inputHeight;
                }
                var hideMouseCursorCheckBox = mainPanel.Controls.OfType<CheckBox>().FirstOrDefault(c => c.Name == "hideMouseCursorCheckBox");
                if (hideMouseCursorCheckBox != null)
                {
                    hideMouseCursorCheckBox.Top -= inputHeight;
                }
                saveButton.Top -= inputHeight;
                cancelButton.Top -= inputHeight;

                // Reassign all handlers after deletion
                ReassignPostCloseButtons();
            }
            else
            {
                postCloseCommandsList[0].Text = "";
                Logger.LogToFile("Cleared the only Post-Close line since count is 1.");
            }
        }

        private void PostClosePlusButton_Click(object sender, EventArgs e)
        {
            int index = postClosePlusButtons.IndexOf((Button)sender);
            Logger.LogToFile($"Post-Close Plus Button Clicked: Index={index}, Total Lines={postCloseCommandsList.Count}");

            var newTextBox = new CustomTextBox { Top = postCloseCommandsList[index].Top + inputHeight, Left = column2Left, Width = inputWidth, Height = inputHeight, Font = largeFont, Visible = true, Tag = "PostClose" };
            newTextBox.Text = "";
            postCloseCommandsList.Insert(index + 1, newTextBox);
            var newMinusButton = new CustomButton { Text = "−", Top = newTextBox.Top, Left = column3Left, Width = smallButtonWidth, Height = smallButtonHeight, Font = largeFont, Visible = true };
            var newPlusButton = new CustomButton { Text = "+", Top = newTextBox.Top, Left = column3Left + smallButtonWidth + smallButtonGap, Width = smallButtonWidth, Height = smallButtonHeight, Font = largeFont, Visible = true };
            postCloseMinusButtons.Insert(index + 1, newMinusButton);
            postClosePlusButtons.Insert(index + 1, newPlusButton);

            // Update the Top positions of all subsequent Post-Close commands
            for (int j = index + 2; j < postCloseCommandsList.Count; j++)
            {
                postCloseCommandsList[j].Top += inputHeight;
                postCloseMinusButtons[j].Top += inputHeight;
                postClosePlusButtons[j].Top += inputHeight;
            }

            mainPanel.Controls.Add(newTextBox);
            mainPanel.Controls.Add(newMinusButton);
            mainPanel.Controls.Add(newPlusButton);

            // Adjust Notes, In Progress, and Hide Mouse Cursor section positions
            if (notesLabel != null)
            {
                notesLabel.Top += inputHeight;
            }
            if (notesTextBox != null)
            {
                notesTextBox.Top += inputHeight;
            }
            var inProgressLabel = mainPanel.Controls.OfType<Label>().FirstOrDefault(c => c.Text == "In Progress");
            if (inProgressLabel != null)
            {
                inProgressLabel.Top += inputHeight;
            }
            var inProgressCheckBox = mainPanel.Controls.OfType<CheckBox>().FirstOrDefault(c => c.Name == "inProgressCheckBox");
            if (inProgressCheckBox != null)
            {
                inProgressCheckBox.Top += inputHeight;
            }
            var hideMouseCursorLabel = mainPanel.Controls.OfType<Label>().FirstOrDefault(c => c.Text == "Hide Mouse Cursor");
            if (hideMouseCursorLabel != null)
            {
                hideMouseCursorLabel.Top += inputHeight;
            }
            var hideMouseCursorCheckBox = mainPanel.Controls.OfType<CheckBox>().FirstOrDefault(c => c.Name == "hideMouseCursorCheckBox");
            if (hideMouseCursorCheckBox != null)
            {
                hideMouseCursorCheckBox.Top += inputHeight;
            }
            saveButton.Top += inputHeight;
            cancelButton.Top += inputHeight;

            // Reassign all handlers after insertion
            ReassignPostCloseButtons();
        }

        private void SaveGame(Game game, bool isNewGame)
        {
            // Log the start of the save process
            Logger.LogToFile($"Saving game: ID={gameIds[game]}, DisplayName={game.DisplayName}, IsNewGame={isNewGame}");

            // Update game properties from UI controls
            var displayNameTextBox = mainPanel.Controls.OfType<CustomTextBox>().FirstOrDefault(c => c.Name == "displayNameTextBox");
            var alphabetizeNameTextBox = mainPanel.Controls.OfType<CustomTextBox>().FirstOrDefault(c => c.Name == "alphabetizeNameTextBox");
            var gameTypeComboBox = mainPanel.Controls.OfType<CustomComboBox>().FirstOrDefault(c => c.Name == "gameTypeComboBox");
            var executableTextBox = mainPanel.Controls.OfType<CustomTextBox>().FirstOrDefault(c => c.Name == "executableTextBox");
            var ledBlinkyTextBox = mainPanel.Controls.OfType<CustomTextBox>().FirstOrDefault(c => c.Name == "ledBlinkyTextBox");
            var inProgressCheckBox = mainPanel.Controls.OfType<CheckBox>().FirstOrDefault(c => c.Name == "inProgressCheckBox");
            var hideMouseCursorCheckBox = mainPanel.Controls.OfType<CheckBox>().FirstOrDefault(c => c.Name == "hideMouseCursorCheckBox");

            if (displayNameTextBox != null) game.DisplayName = displayNameTextBox.Text;
            if (alphabetizeNameTextBox != null) game.AlphabetizeName = alphabetizeNameTextBox.Text;
            if (gameTypeComboBox != null) game.Type = gameTypeComboBox.SelectedItem?.ToString() ?? "PC";
            if (executableTextBox != null) game.ExecutablePath = executableTextBox.Text;
            if (ledBlinkyTextBox != null) game.LEDBlinkyCommand = ledBlinkyTextBox.Text;
            if (notesTextBox != null) game.Notes = notesTextBox.Text; // Save the Notes field
            if (inProgressCheckBox != null) game.IsInProgress = inProgressCheckBox.Checked; // Save the In Progress status
            if (hideMouseCursorCheckBox != null) game.HideMouseCursor = hideMouseCursorCheckBox.Checked; // Save the Hide Mouse Cursor setting

            // Log the basic properties
            Logger.LogToFile($"Saved basic properties: DisplayName={game.DisplayName}, AlphabetizeName={game.AlphabetizeName}, Type={game.Type}, ExecutablePath={game.ExecutablePath}, LEDBlinkyCommand={game.LEDBlinkyCommand}, ArtBoxPath={game.ArtBoxPath}, Notes={game.Notes}, IsInProgress={game.IsInProgress}, HideMouseCursor={game.HideMouseCursor}");

            // Collect Pre-Launch Commands using Tag
            var preLaunchCommandsList = new List<string>();
            var preLaunchTextBoxes = mainPanel.Controls.OfType<CustomTextBox>()
                .Where(tb => tb.Tag != null && tb.Tag.ToString() == "PreLaunch")
                .OrderBy(tb => tb.Top)
                .ToList();

            Logger.LogToFile($"Found {preLaunchTextBoxes.Count} Pre-Launch text boxes.");
            for (int i = 0; i < preLaunchTextBoxes.Count; i++)
            {
                var textBox = preLaunchTextBoxes[i];
                preLaunchCommandsList.Add(textBox.Text);
                Logger.LogToFile($"Pre-Launch Command {i + 1}: Text='{textBox.Text}', Top={textBox.Top}");
            }
            game.PreLaunchCommands = preLaunchCommandsList;

            // Collect Post-Close Commands using Tag
            var postCloseCommandsList = new List<string>();
            var postCloseTextBoxes = mainPanel.Controls.OfType<CustomTextBox>()
                .Where(tb => tb.Tag != null && tb.Tag.ToString() == "PostClose")
                .OrderBy(tb => tb.Top)
                .ToList();

            Logger.LogToFile($"Found {postCloseTextBoxes.Count} Post-Close text boxes.");
            for (int i = 0; i < postCloseTextBoxes.Count; i++)
            {
                var textBox = postCloseTextBoxes[i];
                postCloseCommandsList.Add(textBox.Text);
                Logger.LogToFile($"Post-Close Command {i + 1}: Text='{textBox.Text}', Top={textBox.Top}");
            }
            game.PostExitCommands = postCloseCommandsList;

            // Log the final saved commands
            Logger.LogToFile($"Saved Pre-Launch Commands: [{string.Join(", ", game.PreLaunchCommands)}]");
            Logger.LogToFile($"Saved Post-Close Commands: [{string.Join(", ", game.PostExitCommands)}]");

            // Update the game in the list
            if (isNewGame)
            {
                Logger.LogToFile($"Adding new game to games list: {game.DisplayName}");
                games.Add(game);
                games = games.OrderBy(g => g.AlphabetizeName).ToList(); // Sort alphabetically by AlphabetizeName
            }
            else
            {
                var index = games.FindIndex(g => g.DisplayName == game.DisplayName);
                if (index >= 0)
                {
                    Logger.LogToFile($"Updating existing game at index {index}: {game.DisplayName}");
                    games[index] = game;
                    games = games.OrderBy(g => g.AlphabetizeName).ToList(); // Sort alphabetically by AlphabetizeName
                }
                else
                {
                    Logger.LogToFile($"Game not found in list, adding as new: {game.DisplayName}");
                    games.Add(game);
                    games = games.OrderBy(g => g.AlphabetizeName).ToList();
                }
            }

            Logger.LogToFile($"Saving game data to file. Total games: {games.Count}");
            DataManager.SaveGameData(new GameData { Games = games });
            Logger.LogToFile("Game data saved. Refreshing UI.");
            SetupUI();
        }
    }
}