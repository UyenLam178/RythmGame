//FormCreateLevel.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using WMPLib;

namespace RhythmGame
{
    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }

    public class Note
    {
        public int Lane; // 0, 1, 2, or 3
        public int WorldY; // Y position in world coordinates
        public int Duration;
        public bool IsLong => Duration > 0;
        public int Order;

        private const int NoteBaseHeight = 60;

        public Rectangle GetRect(int x, int scrollOffset)
        {
            int width = 60;
            int height = IsLong ? Duration : NoteBaseHeight;
            int drawY = IsLong ? WorldY - (Duration - NoteBaseHeight) - scrollOffset : WorldY - scrollOffset;
            return new Rectangle(x - width / 2, drawY, width, height);
        }
    }

    public class FormHelpMenu : Form
    {
        public FormHelpMenu(Keys[] currentKeys)
        {
            Width = 400;
            Height = 400;
            Text = "Hướng dẫn";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            Label lblHelpText = new Label
            {
                Text = $"Hướng dẫn:\n- Nhấn {currentKeys[0]}, {currentKeys[1]}, {currentKeys[2]}, {currentKeys[3]} để thêm nốt tại Target Line.\n- Click và kéo để **di chuyển nốt**.\n- Giữ **Shift + Click và kéo** để thay đổi độ dài nốt.\n- Click chọn nốt, sau đó nhấn **Delete/Backspace** hoặc click nút **Delete Note** để xóa.\n- Sử dụng **thanh cuộn dọc** hoặc **cuộn chuột** để di chuyển timeline và đặt nốt ở các vị trí khác.\n- Hitsound hỗ trợ cả file **WAV** và **MP3**.\n- Nhấn **Play Test** để thử màn chơi, nốt sẽ di chuyển qua vạch đỏ trước khi biến mất.\n- Nhấn **Pause Test** để tạm dừng (giữ nguyên sheet như lúc tạo), và **Resume Test** để tiếp tục.",
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 12),
                Location = new Point(10, 10),
                Size = new Size(360, 350),
                AutoSize = false
            };
            Controls.Add(lblHelpText);
        }
    }

    public class FormCreateLevel : Form
    {
        private readonly WindowsMediaPlayer musicPlayer = new WindowsMediaPlayer();
        private readonly WindowsMediaPlayer hitSoundPlayer = new WindowsMediaPlayer();
        private readonly System.Windows.Forms.Timer gameTimer = new System.Windows.Forms.Timer();
        private readonly List<Note> notes = new List<Note>();
        private readonly DoubleBufferedPanel gamePanel = new DoubleBufferedPanel();
        private readonly VScrollBar vScrollBar = new VScrollBar();
        private readonly Label lblTimer = new Label();
        private readonly Label[] laneLabels = new Label[4];
        private int tick = 0;
        private bool isPlayTest = false;
        private bool isPaused = false;
        private int scrollOffset = 0;
        private int pausedScrollOffset = 0;
        private double pausedMusicPosition = 0;

        private const int NoteSpeed = 5;
        private const int TargetY = 400;
        private const int MsToPixelRatio = 1;
        private const int MouseWheelScrollStep = 50;

        private Keys[] laneKeys = new Keys[4];
        private readonly int[] lanePositions = new int[] { 100, 200, 300, 400 };
        private readonly KeysConverter kc = new KeysConverter();
        private HashSet<Keys> pressedKeys = new HashSet<Keys>();
        private Note? selectedNote = null;
        private Point mouseOffset;
        private int initialMouseY;
        private int initialDuration;
        private readonly Dictionary<Note, int> initialYStates = new Dictionary<Note, int>();
        private Button? btnImportMusic;
        private Button? btnImportHitSound;
        private Button? btnPlayTest;
        private Button? btnPauseTest;
        private Button? btnSaveSheet;
        private Button? btnBackMenu;
        private Button? btnDeleteNote;
        private Button? btnHelp;
        private string currentMusicPath = "";
        private string currentHitSoundPath = "";
        private FormHelpMenu? helpMenu;

        public FormCreateLevel()
        {
            Width = 620;
            Height = 1000;
            Text = "🎵 Trình tạo màn chơi";
            BackColor = Color.Black;
            DoubleBuffered = true;
            KeyPreview = true;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            AutoScaleMode = AutoScaleMode.None;

            LoadConfig();
            InitUI();

            gamePanel.Paint += GamePanel_Paint;
            gamePanel.MouseDown += GamePanel_MouseDown;
            gamePanel.MouseMove += GamePanel_MouseMove;
            gamePanel.MouseUp += GamePanel_MouseUp;
            gamePanel.MouseWheel += GamePanel_MouseWheel;
            vScrollBar.Scroll += VScrollBar_Scroll;

            KeyDown += Form_KeyDown;
            KeyUp += Form_KeyUp;

            gameTimer.Interval = 16;
            gameTimer.Tick += GameTimer_Tick;

            hitSoundPlayer.settings.volume = 50;
        }

        private void LoadConfig()
        {
            laneKeys[0] = Keys.A;
            laneKeys[1] = Keys.S;
            laneKeys[2] = Keys.D;
            laneKeys[3] = Keys.F;

            string configFile = "config.txt";
            if (!File.Exists(configFile)) return;

            try
            {
                var settings = File.ReadAllLines(configFile)
                    .Where(line => line.Contains("="))
                    .Select(line => line.Split('='))
                    .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());

                if (settings.TryGetValue("hitsound", out string? hsPath) && File.Exists(hsPath) &&
                    (hsPath.ToLower().EndsWith(".wav") || hsPath.ToLower().EndsWith(".mp3")))
                {
                    currentHitSoundPath = hsPath;
                    hitSoundPlayer.URL = currentHitSoundPath;
                }

                if (settings.TryGetValue("key1", out string? key1)) laneKeys[0] = (Keys)kc.ConvertFromString(key1);
                if (settings.TryGetValue("key2", out string? key2)) laneKeys[1] = (Keys)kc.ConvertFromString(key2);
                if (settings.TryGetValue("key3", out string? key3)) laneKeys[2] = (Keys)kc.ConvertFromString(key3);
                if (settings.TryGetValue("key4", out string? key4)) laneKeys[3] = (Keys)kc.ConvertFromString(key4);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải config: {ex.Message}. Sử dụng phím mặc định.", "Lỗi Config");
            }
        }

        private void InitUI()
        {
            lblTimer.Text = "00:00";
            lblTimer.ForeColor = Color.White;
            lblTimer.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            lblTimer.Location = new Point(Width - 140, 10);
            lblTimer.AutoSize = true;
            Controls.Add(lblTimer);

            for (int i = 0; i < 4; i++)
            {
                laneLabels[i] = new Label
                {
                    Text = laneKeys[i].ToString(),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 16, FontStyle.Bold),
                    AutoSize = true,
                    Location = new Point(lanePositions[i] - 10, TargetY + 20)
                };
                gamePanel.Controls.Add(laneLabels[i]);
            }

            int buttonWidth = 130;
            int buttonHeight = 40;
            int startY = 480;
            int startX = 10;
            int spacing = 10;
            int maxButtonsPerRow = 4;

            btnImportMusic = new Button { Text = "Import Music", Size = new Size(buttonWidth, buttonHeight), ForeColor = Color.White, BackColor = Color.DarkGray, Font = new Font("Segoe UI", 10) };
            btnImportHitSound = new Button { Text = "Import HitSound", Size = new Size(buttonWidth, buttonHeight), ForeColor = Color.White, BackColor = Color.DarkGray, Font = new Font("Segoe UI", 10) };
            btnPlayTest = new Button { Text = "Play Test", Size = new Size(buttonWidth, buttonHeight), ForeColor = Color.White, BackColor = Color.DarkGreen, Font = new Font("Segoe UI", 10) };
            btnPauseTest = new Button { Text = "Pause Test", Size = new Size(buttonWidth, buttonHeight), ForeColor = Color.White, BackColor = Color.DarkRed, Font = new Font("Segoe UI", 10), Enabled = false };
            btnSaveSheet = new Button { Text = "Save Sheet", Size = new Size(buttonWidth, buttonHeight), ForeColor = Color.White, BackColor = Color.DarkGray, Font = new Font("Segoe UI", 10) };
            btnBackMenu = new Button { Text = "Back to Menu", Size = new Size(buttonWidth, buttonHeight), ForeColor = Color.White, BackColor = Color.DarkGray, Font = new Font("Segoe UI", 10) };
            btnDeleteNote = new Button { Text = "Delete Note", Size = new Size(buttonWidth, buttonHeight), ForeColor = Color.White, BackColor = Color.DarkGray, Font = new Font("Segoe UI", 10) };
            btnHelp = new Button { Text = "Help", Size = new Size(buttonWidth, buttonHeight), ForeColor = Color.White, BackColor = Color.DarkGray, Font = new Font("Segoe UI", 10) };

            Button[] buttons = new Button[8] { btnImportMusic, btnImportHitSound, btnPlayTest, btnPauseTest, btnSaveSheet, btnDeleteNote, btnBackMenu, btnHelp };

            for (int i = 0; i < buttons.Length; i++)
            {
                int actualRow = i < 4 ? 0 : 1;
                int actualCol = i % 4;
                buttons[i].Location = new Point(startX + actualCol * (buttonWidth + spacing), startY + actualRow * (buttonHeight + spacing));
                gamePanel.Controls.Add(buttons[i]);
            }

            btnImportMusic.Click += BtnImportMusic_Click;
            btnImportHitSound.Click += BtnImportHitSound_Click;
            btnPlayTest.Click += BtnPlayTest_Click;
            btnPauseTest.Click += BtnPauseTest_Click;
            btnSaveSheet.Click += BtnSaveSheet_Click;
            btnBackMenu.Click += BtnBackMenu_Click;
            btnDeleteNote.Click += BtnDeleteNote_Click;
            btnHelp.Click += BtnHelp_Click;

            gamePanel.Size = new Size(Width - 20, Height - 50);
            gamePanel.Location = new Point(0, 50);
            gamePanel.BackColor = Color.Black;
            Controls.Add(gamePanel);

            vScrollBar.Width = 20;
            vScrollBar.Height = Height - 50;
            vScrollBar.Location = new Point(Width - 20, 50);
            vScrollBar.Minimum = 0;
            vScrollBar.Maximum = 100000;
            vScrollBar.SmallChange = 10;
            vScrollBar.LargeChange = 100;
            vScrollBar.Value = 0;
            scrollOffset = 0;
            Controls.Add(vScrollBar);
        }

        private void GamePanel_MouseWheel(object? sender, MouseEventArgs e)
        {
            int delta = e.Delta > 0 ? -MouseWheelScrollStep : MouseWheelScrollStep;
            int newValue = vScrollBar.Value + delta;
            newValue = Math.Max(vScrollBar.Minimum, Math.Min(newValue, vScrollBar.Maximum - vScrollBar.LargeChange + 1));
            vScrollBar.Value = newValue;
            scrollOffset = newValue;
            gamePanel.Invalidate();
        }

        private void VScrollBar_Scroll(object? sender, ScrollEventArgs e)
        {
            scrollOffset = e.NewValue;
            gamePanel.Invalidate();
        }

        private void BtnImportMusic_Click(object? sender, EventArgs e)
        {
            using OpenFileDialog ofd = new OpenFileDialog { Filter = "Audio Files|*.wav;*.mp3", Title = "Select Music File", CheckFileExists = true };
            if (ofd.ShowDialog() == DialogResult.OK && File.Exists(ofd.FileName))
            {
                currentMusicPath = ofd.FileName;
                MessageBox.Show($"Imported music: {Path.GetFileName(currentMusicPath)}", "Import Music");
            }
            else
            {
                MessageBox.Show("Please select a valid WAV or MP3 file.", "Import Music Error");
            }
        }

        private void BtnImportHitSound_Click(object? sender, EventArgs e)
        {
            using OpenFileDialog ofd = new OpenFileDialog { Filter = "Audio Files|*.wav;*.mp3", Title = "Select HitSound File", CheckFileExists = true };
            if (ofd.ShowDialog() == DialogResult.OK && File.Exists(ofd.FileName) &&
                (Path.GetExtension(ofd.FileName).ToLower() == ".wav" || Path.GetExtension(ofd.FileName).ToLower() == ".mp3"))
            {
                currentHitSoundPath = ofd.FileName;
                try
                {
                    hitSoundPlayer.URL = currentHitSoundPath;
                    MessageBox.Show($"Imported hitsound: {Path.GetFileName(currentHitSoundPath)}", "Import HitSound");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading hitsound: {ex.Message}", "Import HitSound Error");
                }
            }
            else
            {
                MessageBox.Show("Please select a valid WAV or MP3 file.", "Import HitSound Error");
            }
        }

        private void BtnPlayTest_Click(object? sender, EventArgs e)
        {
            if (notes.Count == 0)
            {
                MessageBox.Show("No notes to play. Add some notes first!", "Play Test");
                return;
            }

            if (isPaused)
            {
                // Resume from paused state
                isPaused = false;
                btnPlayTest!.Text = "Play Test";
                btnPauseTest!.Enabled = true;

                foreach (var n in notes)
                {
                    if (initialYStates.ContainsKey(n))
                    {
                        n.WorldY = initialYStates[n] - TargetY + (tick * NoteSpeed);
                    }
                }
                scrollOffset = pausedScrollOffset;

                try
                {
                    if (!string.IsNullOrEmpty(currentMusicPath))
                    {
                        musicPlayer.controls.currentPosition = pausedMusicPosition;
                        musicPlayer.controls.play();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error resuming music: {ex.Message}", "Play Test Error");
                    PauseTest();
                    return;
                }

                gameTimer.Start();
                gamePanel.Invalidate();
                return;
            }

            // Start new play test
            musicPlayer.controls.stop();
            hitSoundPlayer.controls.stop();
            gameTimer.Stop();

            tick = 0;
            isPlayTest = true;
            isPaused = false;

            initialYStates.Clear();
            foreach (var n in notes)
            {
                initialYStates[n] = n.WorldY;
                n.WorldY = n.WorldY - TargetY;
            }

            if (!string.IsNullOrEmpty(currentMusicPath))
            {
                try
                {
                    musicPlayer.URL = currentMusicPath;
                    musicPlayer.controls.play();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error playing music: {ex.Message}", "Play Test Error");
                    PauseTest();
                    return;
                }
            }
            else
            {
                MessageBox.Show("No music file imported. Only hitsound will play.", "Play Test");
            }

            btnPlayTest!.Text = "Play Test";
            btnPlayTest!.Enabled = false;
            btnPauseTest!.Enabled = true;

            gameTimer.Start();
        }

        private void BtnPauseTest_Click(object? sender, EventArgs e)
        {
            PauseTest();
        }

        private void PauseTest()
        {
            if (!isPlayTest || isPaused) return;

            gameTimer.Stop();
            isPaused = true;
            pausedScrollOffset = scrollOffset;

            try
            {
                pausedMusicPosition = musicPlayer.controls.currentPosition;
                musicPlayer.controls.pause();
                hitSoundPlayer.controls.stop();
            }
            catch { }

            // Restore notes to original positions
            foreach (var n in notes.ToList()) // Use ToList to avoid collection modified exception
            {
                if (initialYStates.ContainsKey(n))
                {
                    n.WorldY = initialYStates[n];
                }
            }

            btnPlayTest!.Text = "Resume Test";
            btnPlayTest!.Enabled = true;
            btnPauseTest!.Enabled = false;

            gamePanel.Invalidate();
        }

        private void BtnSaveSheet_Click(object? sender, EventArgs e)
        {
            if (notes.Count == 0)
            {
                MessageBox.Show("No notes to save. Add some notes first!", "Save Sheet");
                return;
            }

            string sheetsFolder = Path.Combine(Application.StartupPath, "sheets");
            if (!Directory.Exists(sheetsFolder)) Directory.CreateDirectory(sheetsFolder);
            string fileName = $"sheet_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string filePath = Path.Combine(sheetsFolder, fileName);

            try
            {
                var sortedNotes = notes.OrderBy(n => n.WorldY).ToList();

                using StreamWriter w = new StreamWriter(filePath);
                foreach (var n in sortedNotes)
                {
                    int noteTimeMs = (n.WorldY - TargetY) * MsToPixelRatio;
                    int durationMs = n.Duration * MsToPixelRatio;

                    if (noteTimeMs >= 0)
                    {
                        w.WriteLine($"{noteTimeMs},{n.Lane},{n.IsLong},{durationMs}");
                    }
                }

                MessageBox.Show($"Saved sheet to {filePath}. Only notes at or after the target line (Y={TargetY}) were saved.", "Save Sheet");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving sheet: {ex.Message}", "Save Sheet Error");
            }
        }

        private void BtnBackMenu_Click(object? sender, EventArgs e)
        {
            try { musicPlayer.controls.stop(); } catch { }
            try { hitSoundPlayer.controls.stop(); } catch { }
            gameTimer.Stop();
            isPlayTest = false;
            isPaused = false;
            tick = 0;
            pausedMusicPosition = 0;
            pausedScrollOffset = 0;
            foreach (var n in notes.ToList())
            {
                if (initialYStates.ContainsKey(n))
                {
                    n.WorldY = initialYStates[n];
                }
            }
            btnPlayTest!.Text = "Play Test";
            btnPlayTest!.Enabled = true;
            btnPauseTest!.Enabled = false;
            gamePanel.Invalidate();
            Close();
        }

        private void BtnDeleteNote_Click(object? sender, EventArgs e)
        {
            DeleteSelectedNote();
        }

        private void BtnHelp_Click(object? sender, EventArgs e)
        {
            if (helpMenu == null || helpMenu.IsDisposed)
            {
                helpMenu = new FormHelpMenu(this.laneKeys);
                helpMenu.Show(this);
            }
            else
            {
                helpMenu.BringToFront();
            }
        }

        private void DeleteSelectedNote()
        {
            if (selectedNote != null)
            {
                notes.Remove(selectedNote);
                initialYStates.Remove(selectedNote);
                selectedNote = null;
                gamePanel.Invalidate();
            }
        }

        private void GameTimer_Tick(object? sender, EventArgs e)
        {
            if (!isPlayTest || isPaused) return;
            tick++;

            int elapsedTimeMs = tick * gameTimer.Interval;
            lblTimer.Text = TimeSpan.FromMilliseconds(elapsedTimeMs).ToString(@"mm\:ss");

            int deltaY = NoteSpeed;

            foreach (var n in notes)
            {
                n.WorldY += deltaY;
            }

            if (notes.Any())
            {
                int minY = notes.Min(n => n.WorldY);
                int maxY = notes.Max(n => n.WorldY + (n.IsLong ? n.Duration : 0));
                if (maxY > TargetY + scrollOffset)
                {
                    scrollOffset = maxY - TargetY;
                    vScrollBar.Value = Math.Min(scrollOffset, vScrollBar.Maximum - vScrollBar.LargeChange + 1);
                }
                else if (minY < TargetY + scrollOffset - gamePanel.Height)
                {
                    scrollOffset = Math.Max(0, minY - TargetY + gamePanel.Height);
                    vScrollBar.Value = Math.Min(scrollOffset, vScrollBar.Maximum - vScrollBar.LargeChange + 1);
                }
            }

            if (musicPlayer.playState == WMPLib.WMPPlayState.wmppsStopped || musicPlayer.playState == WMPLib.WMPPlayState.wmppsMediaEnded)
            {
                if (!notes.Any(n => n.WorldY <= TargetY + scrollOffset))
                {
                    PauseTest();
                }
            }

            gamePanel.Invalidate();
        }

        private void Form_KeyDown(object? sender, KeyEventArgs e)
        {
            if (isPlayTest) return;

            if (selectedNote != null && (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back))
            {
                DeleteSelectedNote();
                e.Handled = true;
                return;
            }

            if (pressedKeys.Contains(e.KeyCode)) return;
            pressedKeys.Add(e.KeyCode);

            int lane = KeyToLane(e.KeyCode);
            if (lane < 0) return;

            Note n = new Note
            {
                Lane = lane,
                WorldY = TargetY + scrollOffset,
                Duration = 0,
                Order = notes.Count
            };
            notes.Add(n);
            initialYStates[n] = n.WorldY;

            try
            {
                if (!string.IsNullOrEmpty(currentHitSoundPath))
                {
                    hitSoundPlayer.controls.stop();
                    hitSoundPlayer.controls.currentPosition = 0;
                    hitSoundPlayer.controls.play();
                }
            }
            catch { }
            gamePanel.Invalidate();
        }

        private void Form_KeyUp(object? sender, KeyEventArgs e)
        {
            pressedKeys.Remove(e.KeyCode);
        }

        private void GamePanel_MouseDown(object? sender, MouseEventArgs e)
        {
            if (isPlayTest) return;

            selectedNote = null;

            foreach (var n in notes.OrderByDescending(note => note.WorldY))
            {
                int x = lanePositions[n.Lane];
                if (n.GetRect(x, scrollOffset).Contains(e.Location))
                {
                    selectedNote = n;
                    mouseOffset = new Point(e.X - n.GetRect(x, scrollOffset).X, e.Y - n.GetRect(x, scrollOffset).Y);
                    initialMouseY = e.Y;
                    initialDuration = selectedNote.Duration;
                    break;
                }
            }
            gamePanel.Invalidate();
        }

        private void GamePanel_MouseMove(object? sender, MouseEventArgs e)
        {
            if (isPlayTest) return;

            if (selectedNote != null && e.Button == MouseButtons.Left)
            {
                if (Control.ModifierKeys.HasFlag(Keys.Shift))
                {
                    int deltaY = e.Y - initialMouseY;
                    int newDuration = initialDuration + deltaY;
                    if (newDuration < 0) newDuration = 0;
                    selectedNote.Duration = newDuration < 20 ? 0 : newDuration;
                }
                else
                {
                    selectedNote.WorldY = e.Y - mouseOffset.Y + scrollOffset;
                    initialYStates[selectedNote] = selectedNote.WorldY;
                }
                gamePanel.Invalidate();
            }
        }

        private void GamePanel_MouseUp(object? sender, MouseEventArgs e)
        {
            gamePanel.Invalidate();
        }

        private int KeyToLane(Keys k)
        {
            for (int i = 0; i < 4; i++)
            {
                if (laneKeys[i] == k) return i;
            }
            return -1;
        }

        private void GamePanel_Paint(object? sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            g.DrawLine(Pens.Red, 0, TargetY, gamePanel.Width, TargetY);

            foreach (int x in lanePositions)
            {
                g.DrawLine(Pens.Gray, x - 30, 0, x - 30, gamePanel.Height);
                g.DrawLine(Pens.Gray, x + 30, 0, x + 30, gamePanel.Height);
            }

            foreach (int x in lanePositions)
            {
                DrawRoundedRectangle(g, Brushes.DarkGray, x - 40, TargetY + 10, 80, 20, 10);
            }

            foreach (var n in notes.OrderBy(note => note.WorldY))
            {
                int x = lanePositions[n.Lane];
                Brush brush = n.IsLong ? Brushes.Cyan : Brushes.Yellow;

                Rectangle rect = n.GetRect(x, scrollOffset);

                if (rect.Y + rect.Height >= 0 && rect.Y <= gamePanel.Height) // Only draw visible notes
                {
                    if (n == selectedNote)
                    {
                        using (Pen selectionPen = new Pen(Color.Red, 3))
                        {
                            DrawRoundedRectangle(g, brush, rect.X, rect.Y, rect.Width, rect.Height, 10);
                            g.DrawPath(selectionPen, GetRoundedRectanglePath(rect.X, rect.Y, rect.Width, rect.Height, 10));
                        }
                    }
                    else
                    {
                        DrawRoundedRectangle(g, brush, rect.X, rect.Y, rect.Width, rect.Height, 10);
                    }
                }
            }
        }

        private GraphicsPath GetRoundedRectanglePath(float x, float y, float width, float height, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(x, y, radius * 2, radius * 2, 180, 90);
            path.AddArc(x + width - radius * 2, y, radius * 2, radius * 2, 270, 90);
            path.AddArc(x + width - radius * 2, y + height - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(x, y + height - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void DrawRoundedRectangle(Graphics g, Brush brush, float x, float y, float width, float height, float radius)
        {
            g.FillPath(brush, GetRoundedRectanglePath(x, y, width, height, radius));
        }
    }
}