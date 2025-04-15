using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ArcadeLauncher.Core;

namespace ArcadeLauncher.SW2
{
    public partial class MainForm : Form
    {
        private void SetupUI()
        {
            this.Text = "Arcade Launcher Setup";
            this.FormBorderStyle = FormBorderStyle.Sizable; // Resizable window
            this.MaximizeBox = true;
            this.Size = new Size(800, 600);
            this.WindowState = FormWindowState.Maximized; // Default to full screen

            // Top ribbon with buttons
            toolStrip = new ToolStrip { Dock = DockStyle.Top, Font = largeFont }; // Set the font to largeFont
            var addGameButton = new ToolStripButton("Add Game");
            deleteGameButton = new ToolStripButton("Delete Game") { Enabled = false };
            var settingsButton = new ToolStripButton("Settings");
            toolStrip.Items.AddRange(new ToolStripItem[] { addGameButton, deleteGameButton, settingsButton });

            // Game list on the left
            gameList = new CustomListBox // Use CustomListBox for double-buffering (Ticket 04)
            {
                Dock = DockStyle.Left,
                Width = 300, // 50% wider than 200px
                IntegralHeight = false,
                Font = largeFont,
                DrawMode = DrawMode.OwnerDrawFixed // Enable custom drawing for background color
            };

            // Calculate the item height based on the font size plus padding
            using (var tempPanel = new Panel { Visible = false })
            using (var tempLabel = new Label { Font = largeFont, Text = "Sample Text" })
            {
                tempPanel.Controls.Add(tempLabel);
                int textHeight = TextRenderer.MeasureText("Sample Text", largeFont).Height;
                gameList.ItemHeight = textHeight + 8; // Add 8 pixels of padding for better spacing
            }

            foreach (var game in games)
            {
                gameList.Items.Add(game);
            }
            gameList.DisplayMember = "DisplayName";

            // Custom drawing for the Game List to highlight "In Progress" games
            gameList.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;

                var gameItem = gameList.Items[e.Index] as Game;
                if (gameItem == null) return;

                // Set the background color based on IsInProgress
                if (gameItem.IsInProgress)
                {
                    e.Graphics.FillRectangle(new SolidBrush(ColorTranslator.FromHtml("#FFC000")), e.Bounds); // Orange background for "In Progress" games
                }
                else
                {
                    e.Graphics.FillRectangle(SystemBrushes.Window, e.Bounds); // Default background for other games
                }

                // Draw the text
                string displayText = gameItem.DisplayName ?? "Unnamed Game";
                TextRenderer.DrawText(e.Graphics, displayText, gameList.Font, e.Bounds, SystemColors.ControlText, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

                // Draw the focus rectangle if the item is selected
                if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
                {
                    e.Graphics.DrawRectangle(SystemPens.Highlight, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
                }
            };

            selectedIndexChangedHandler = (s, e) =>
            {
                mainPanel.SuspendLayout(); // Suspend layout updates to prevent flickering (Ticket 04)
                if (gameList.SelectedItem is Game selectedGame)
                {
                    SetupGameView(selectedGame, false);
                    deleteGameButton.Enabled = true;
                }
                else
                {
                    mainPanel.Controls.Clear();
                    deleteGameButton.Enabled = false;
                }
                mainPanel.ResumeLayout(); // Resume layout updates (Ticket 04)
            };
            gameList.SelectedIndexChanged += selectedIndexChangedHandler;

            // Main panel for game view or settings
            mainPanel = new CustomPanel // Use CustomPanel for double-buffering (Ticket 04)
            {
                Dock = DockStyle.Fill
            };

            this.Controls.Clear();
            this.Controls.Add(mainPanel);
            this.Controls.Add(gameList);
            this.Controls.Add(toolStrip);

            // Add game button click event
            addGameButton.Click += (s, e) =>
            {
                var newGame = new Game
                {
                    DisplayName = "",
                    AlphabetizeName = "",
                    Type = "PC",
                    ExecutablePath = "",
                    ArtBoxPath = "",
                    MarqueePath = "",
                    ControllerLayoutPath = "",
                    EmulatorPlugin = "",
                    EmulatorPath = "",
                    RomPath = "",
                    CustomParameters = "",
                    PreLaunchCommands = new List<string> { "" },
                    PostExitCommands = new List<string> { "" },
                    LEDBlinkyCommand = "",
                    IsInProgress = true // Default to true for new games
                };
                // Generate a unique ID for the new game
                string gameId = Guid.NewGuid().ToString();
                gameIds[newGame] = gameId;
                Logger.LogToFile($"Created new game with ID: {gameId}, DisplayName: {newGame.DisplayName}");

                // Temporarily unsubscribe to prevent clearing the Main Area
                gameList.SelectedIndexChanged -= selectedIndexChangedHandler;
                gameList.SelectedIndex = -1; // Deselect any game in the list
                SetupGameView(newGame, true);
                gameList.SelectedIndexChanged += selectedIndexChangedHandler;
                deleteGameButton.Enabled = false;
            };

            // Delete game button click event
            deleteGameButton.Click += (s, e) =>
            {
                if (gameList.SelectedItem is Game selectedGame)
                {
                    var result = MessageBox.Show($"Are you sure you want to delete {selectedGame.DisplayName}?", "Confirm Delete", MessageBoxButtons.YesNo);
                    if (result == DialogResult.Yes)
                    {
                        Logger.LogToFile($"Deleting game: DisplayName={selectedGame.DisplayName}, ID={gameIds[selectedGame]}");
                        games.Remove(selectedGame);
                        gameList.Items.Remove(selectedGame);
                        gameIds.Remove(selectedGame); // Remove the game ID mapping
                        mainPanel.Controls.Clear();
                        DataManager.SaveGameData(new GameData { Games = games });
                        deleteGameButton.Enabled = false;
                    }
                }
            };

            // Settings button click event
            settingsButton.Click += (s, e) =>
            {
                gameList.SelectedIndex = -1; // Deselect any game
                deleteGameButton.Enabled = false;
                SetupSettingsView();
            };

            // Select the first game on startup
            if (games.Any())
            {
                gameList.SelectedIndex = 0;
            }
        }
    }
}