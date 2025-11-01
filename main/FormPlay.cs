using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Windows.Forms;
using WMPLib;

namespace RhythmGame
{
    public class FormPlay : Form
    {
        private Image? backgroundImage;
        private Image? noteImage1;
        private Image? noteImage2;

        private class Note
        {
            public int X;
            public int Y;
            public Keys Key;
            public bool Hit;
            public int Time;
            public bool IsLong;
            public int Duration;
            public bool HoldActive;
            public int ImageType;
        }

        private class HighScore
        {
            public string Name { get; set; } = "";
            public int Score { get; set; }
            public int Level { get; set; }
        }

        private readonly WindowsMediaPlayer musicPlayer = new WindowsMediaPlayer();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly List<Note> notes = new List<Note>();
        private readonly Keys[] laneKeys = new Keys[4];
        private readonly int[] lanePositions = new int[] { 150, 275, 400, 525 };
        private readonly KeysConverter kc = new KeysConverter();
        private readonly HashSet<Keys> pressedKeys = new HashSet<Keys>();
        private int tick = 0;
        private int score = 0;
        private int baseSpeed = 5;
        private int sheetIndex = 0;
        private readonly List<Note> sheetNotes = new List<Note>();
        private WindowsMediaPlayer? hitSoundPlayer;
        private int perfectCount = 0;
        private int greatCount = 0;
        private int missCount = 0;
        private string lastJudgment = "";
        private DateTime lastJudgmentTime;
        private const int TargetY = 650;
        private const int NoteSize = 60;
        private string musicFolder = Path.Combine(Application.StartupPath, "Music");
        private string[] musicFiles = new string[0];
        private readonly Random random = new Random();
        private string[] shuffledPlaylist = new string[0];
        private int currentTrackIndex = 0;
        private string? lastSheetPath;
        private bool isPaused = false;
        private double pausePosition = 0;
        private int currentLevel = 1;
        private string playerName = "";
        private string HighScoreFile => $"highscores_level{currentLevel}.txt";
        private float speedMultiplier = 1f;  // Tốc độ nốt rơi
        private int levelThreshold = 800;    // Điểm tối thiểu qua màn
        private bool gameOver = false;       // Trạng thái thua

        public FormPlay()
        {
            DoubleBuffered = true;
            BackColor = Color.Black;
            Width = 700;
            Height = 800;
            Text = "Play Mode";
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            LoadDefaultBackground();
            LoadNoteImages();
            LoadConfig();
            PromptPlayerName();
            LoadMusicFiles();
            LoadRandomSheet();

            timer.Interval = 16;
            timer.Tick += GameLoop!;
            KeyDown += OnKeyDown!;
            KeyUp += OnKeyUp!;
            FormClosing += OnFormClosing!;
            musicPlayer.PlayStateChange += MusicPlayer_PlayStateChange;

            if (sheetNotes.Count > 0) timer.Start();
            else Close();
        }

        private void PromptPlayerName()
        {
            using (Form inputForm = new Form())
            {
                inputForm.Text = "Enter Your Name";
                inputForm.Size = new Size(300, 150);
                inputForm.FormBorderStyle = FormBorderStyle.FixedSingle;
                inputForm.MaximizeBox = false;
                inputForm.StartPosition = FormStartPosition.CenterParent;

                Label lblPrompt = new Label
                {
                    Text = "Please enter your name:",
                    Location = new Point(10, 20),
                    Size = new Size(260, 20)
                };

                TextBox txtName = new TextBox
                {
                    Location = new Point(10, 50),
                    Size = new Size(260, 30)
                };

                Button btnOk = new Button
                {
                    Text = "OK",
                    Location = new Point(100, 90),
                    Size = new Size(80, 30)
                };

                btnOk.Click += (s, e) =>
                {
                    playerName = string.IsNullOrWhiteSpace(txtName.Text) ? "Player" : txtName.Text.Trim();
                    inputForm.Close();
                };

                inputForm.Controls.Add(lblPrompt);
                inputForm.Controls.Add(txtName);
                inputForm.Controls.Add(btnOk);
                inputForm.ShowDialog(this);
            }
        }

        private void LoadDefaultBackground()
        {
            try
            {
                string bgPath = Path.Combine(Application.StartupPath, "Image", "BG_game.png");
                if (File.Exists(bgPath))
                    backgroundImage = Image.FromFile(bgPath);
                else
                    throw new FileNotFoundException("Không tìm thấy ảnh nền mặc định.");
            }
            catch
            {
                backgroundImage = new Bitmap(ClientSize.Width, ClientSize.Height);
                using (Graphics g = Graphics.FromImage(backgroundImage))
                    g.Clear(Color.Black);
            }
        }

        private void LoadNoteImages()
        {
            try
            {
                string note1Path = Path.Combine(Application.StartupPath, "Image", "notes1.png");
                string note2Path = Path.Combine(Application.StartupPath, "Image", "notes2.png");

                if (File.Exists(note1Path))
                    noteImage1 = Image.FromFile(note1Path);
                else
                    MessageBox.Show($"Không tìm thấy: {note1Path}", "Cảnh báo");

                if (File.Exists(note2Path))
                    noteImage2 = Image.FromFile(note2Path);
                else
                    MessageBox.Show($"Không tìm thấy: {note2Path}", "Cảnh báo");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải hình ảnh nốt nhạc: {ex.Message}", "Lỗi");
            }
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

                musicFolder = settings.GetValueOrDefault("musicFolder", Path.Combine(Application.StartupPath, "Music"));

                if (settings.TryGetValue("hitsound", out string? hsPath) && File.Exists(hsPath))
                {
                    hitSoundPlayer = new WindowsMediaPlayer();
                    hitSoundPlayer.URL = hsPath;
                    hitSoundPlayer.settings.volume = 100;
                    hitSoundPlayer.settings.autoStart = false;
                }

                if (settings.TryGetValue("background", out string? bgPath) && File.Exists(bgPath))
                {
                    try { backgroundImage = Image.FromFile(bgPath); } catch { }
                }

                if (settings.TryGetValue("key1", out string? key1) && key1 != null)
                    laneKeys[0] = (Keys?)kc.ConvertFromString(key1) ?? Keys.A;
                if (settings.TryGetValue("key2", out string? key2) && key2 != null)
                    laneKeys[1] = (Keys?)kc.ConvertFromString(key2) ?? Keys.S;
                if (settings.TryGetValue("key3", out string? key3) && key3 != null)
                    laneKeys[2] = (Keys?)kc.ConvertFromString(key3) ?? Keys.D;
                if (settings.TryGetValue("key4", out string? key4) && key4 != null)
                    laneKeys[3] = (Keys?)kc.ConvertFromString(key4) ?? Keys.F;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải config: {ex.Message}. Sử dụng phím mặc định.", "Lỗi Config");
            }
        }

        private void LoadMusicFiles()
        {
            try
            {
                if (Directory.Exists(musicFolder))
                {
                    musicFiles = Directory.GetFiles(musicFolder, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(f => f.ToLower().EndsWith(".wav") || f.ToLower().EndsWith(".mp3"))
                        .ToArray();
                    if (musicFiles.Length == 0)
                    {
                        MessageBox.Show("Không tìm thấy file nhạc (.wav hoặc .mp3) trong thư mục đã chọn.", "Không có nhạc", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("Thư mục nhạc không tồn tại.", "Lỗi thư mục", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải danh sách file nhạc: {ex.Message}", "Lỗi tải nhạc", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShuffleAndPlayMusic()
        {
            if (musicFiles.Length == 0)
            {
                PlayDefaultMusic();
                return;
            }

            try
            {
                shuffledPlaylist = musicFiles.OrderBy(x => random.Next()).ToArray();
                currentTrackIndex = 0;
                PlayCurrentTrack();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xáo trộn nhạc: {ex.Message}", "Lỗi phát nhạc", MessageBoxButtons.OK, MessageBoxIcon.Error);
                PlayDefaultMusic();
            }
        }

        private void PlayCurrentTrack()
        {
            if (shuffledPlaylist.Length == 0 || currentTrackIndex >= shuffledPlaylist.Length) return;

            try
            {
                musicPlayer.URL = shuffledPlaylist[currentTrackIndex];
                musicPlayer.settings.autoStart = true;
                musicPlayer.settings.volume = 100;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi phát nhạc: {ex.Message}", "Lỗi phát nhạc", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MusicPlayer_PlayStateChange(int NewState)
        {
            if ((WMPPlayState)NewState == WMPPlayState.wmppsMediaEnded)
            {
                currentTrackIndex++;
                if (currentTrackIndex >= shuffledPlaylist.Length)
                {
                    shuffledPlaylist = musicFiles.OrderBy(x => random.Next()).ToArray();
                    currentTrackIndex = 0;
                }
                PlayCurrentTrack();
            }
        }

        private void LoadRandomSheet(bool replayCurrent = false)
        {
            string sheetsDir = Path.Combine(Application.StartupPath, "sheets");
            if (!Directory.Exists(sheetsDir))
            {
                Directory.CreateDirectory(sheetsDir);
                MessageBox.Show("Thư mục sheets trống. Vui lòng tạo ít nhất một sheet bằng FormCreateLevel.", "Lỗi Sheet");
                return;
            }

            string[] sheetFiles = Directory.GetFiles(sheetsDir, "*.txt");
            if (sheetFiles.Length == 0)
            {
                MessageBox.Show("Không tìm thấy sheet nào trong thư mục sheets.", "Lỗi Sheet");
                return;
            }

            string selectedSheet;
            if (replayCurrent && lastSheetPath != null && File.Exists(lastSheetPath))
            {
                selectedSheet = lastSheetPath;
            }
            else
            {
                string[] availableSheets = sheetFiles.Where(f => f != lastSheetPath).ToArray();
                if (availableSheets.Length == 0 && sheetFiles.Length == 1)
                    availableSheets = sheetFiles;

                if (availableSheets.Length == 0)
                {
                    MessageBox.Show("Không có sheet hợp lệ để tải.", "Lỗi Sheet");
                    return;
                }

                selectedSheet = availableSheets[random.Next(availableSheets.Length)];
                lastSheetPath = selectedSheet;
            }

            sheetNotes.Clear();
            try
            {
                foreach (var line in File.ReadAllLines(selectedSheet))
                {
                    var p = line.Split(',');
                    if (p.Length >= 4)
                    {
                        int time = int.Parse(p[0]);
                        int lane = int.Parse(p[1]);
                        bool isLong = bool.Parse(p[2]);
                        int duration = int.Parse(p[3]);

                        if (lane < 0 || lane > 3) continue;

                        Keys key = laneKeys[lane];
                        int xPos = lanePositions[lane];

                        sheetNotes.Add(new Note
                        {
                            Time = time,
                            Key = key,
                            IsLong = isLong,
                            Duration = duration,
                            X = xPos,
                            Y = -300,
                            ImageType = random.Next(1, 3)
                        });
                    }
                }

                if (sheetNotes.Count == 0)
                {
                    MessageBox.Show($"Sheet {Path.GetFileName(selectedSheet)} không có nốt nào.", "Lỗi Sheet");
                    return;
                }

                ShuffleAndPlayMusic();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải sheet {Path.GetFileName(selectedSheet)}: {ex.Message}", "Lỗi Sheet");
            }
        }

        private void GameLoop(object? s, EventArgs? e)
        {
            if (isPaused) return;

            tick++;
            int ms = (int)(tick * timer.Interval);

            while (sheetIndex < sheetNotes.Count && sheetNotes[sheetIndex].Time <= ms)
            {
                var n = sheetNotes[sheetIndex];
                notes.Add(new Note
                {
                    X = n.X,
                    Y = n.Y,
                    Key = n.Key,
                    IsLong = n.IsLong,
                    Duration = n.Duration,
                    Time = n.Time,
                    ImageType = n.ImageType
                });
                sheetIndex++;
            }

            foreach (var n in notes.ToArray())
            {
                n.Y += (int)(baseSpeed * speedMultiplier);

                if (!n.Hit && !n.IsLong && n.Y > TargetY + 120) // Mở rộng window miss cho nốt ngắn
                {
                    n.Hit = true;
                    missCount++;
                    lastJudgment = "Miss";
                    lastJudgmentTime = DateTime.Now;
                }
                else if (n.IsLong && !n.Hit && !n.HoldActive && n.Y > TargetY + NoteSize + 120) // Mở rộng cho nốt dài
                {
                    n.Hit = true;
                    missCount++;
                    lastJudgment = "Miss";
                    lastJudgmentTime = DateTime.Now;
                }
                else if (n.IsLong && n.HoldActive)
                {
                    int endY = n.Y + (int)((float)n.Duration / baseSpeed * speedMultiplier);
                    if (endY >= TargetY - 120 && endY <= TargetY + 120) // Mở rộng window judgment cho end hold
                    {
                        if (pressedKeys.Contains(n.Key))
                        {
                            int deltaY = Math.Abs(endY - TargetY);
                            if (deltaY <= 30) // Mở rộng perfect window
                            {
                                score += 100;
                                perfectCount++;
                                lastJudgment = "Perfect";
                            }
                            else if (deltaY <= 80) // Mở rộng great window
                            {
                                score += 50;
                                greatCount++;
                                lastJudgment = "Great";
                            }
                            else
                            {
                                missCount++;
                                lastJudgment = "Miss";
                            }
                            n.Hit = true;
                            n.HoldActive = false;
                            lastJudgmentTime = DateTime.Now;
                            PlayHitSound();
                        }
                        else
                        {
                            n.Hit = true;
                            n.HoldActive = false;
                            missCount++;
                            lastJudgment = "Miss";
                            lastJudgmentTime = DateTime.Now;
                        }
                    }
                    else if (endY > TargetY + 120) // Nếu qua window mà vẫn giữ, tự động cho Great để giảm miss
                    {
                        n.Hit = true;
                        n.HoldActive = false;
                        score += 50;
                        greatCount++;
                        lastJudgment = "Great";
                        lastJudgmentTime = DateTime.Now;
                        PlayHitSound();
                    }
                }
            }

            notes.RemoveAll(n => n.Y > Height + 100 && n.Hit);

            if (sheetIndex >= sheetNotes.Count && !notes.Any(n => !n.Hit))
            {
                SaveHighScore();
                if (score >= levelThreshold)
                {
                    ShowSummary();
                }
                else
                {
                    ShowGameOver();
                }
                return;
            }

            Invalidate();
        }

        private void SaveHighScore()
        {
            string highScoreFile = Path.Combine(Application.StartupPath, HighScoreFile);
            List<HighScore> highScores = new List<HighScore>();

            if (File.Exists(highScoreFile))
            {
                try
                {
                    foreach (var line in File.ReadAllLines(highScoreFile))
                    {
                        var parts = line.Split(',');
                        if (parts.Length == 3 && int.TryParse(parts[1], out int sc) && int.TryParse(parts[2], out int lvl))
                        {
                            highScores.Add(new HighScore { Name = parts[0], Score = sc, Level = lvl });
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi đọc {HighScoreFile}: {ex.Message}", "Lỗi High Score");
                }
            }

            highScores.Add(new HighScore { Name = playerName, Score = score, Level = currentLevel });

            try
            {
                using (StreamWriter writer = new StreamWriter(highScoreFile))
                {
                    foreach (var hs in highScores)
                    {
                        writer.WriteLine($"{hs.Name},{hs.Score},{hs.Level}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu {HighScoreFile}: {ex.Message}", "Lỗi High Score");
            }
        }

        private List<HighScore> GetTopHighScores()
        {
            string highScoreFile = Path.Combine(Application.StartupPath, HighScoreFile);
            List<HighScore> highScores = new List<HighScore>();

            if (File.Exists(highScoreFile))
            {
                try
                {
                    foreach (var line in File.ReadAllLines(highScoreFile))
                    {
                        var parts = line.Split(',');
                        if (parts.Length == 3 && int.TryParse(parts[1], out int sc) && int.TryParse(parts[2], out int lvl))
                        {
                            highScores.Add(new HighScore { Name = parts[0], Score = sc, Level = lvl });
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi đọc {HighScoreFile}: {ex.Message}", "Lỗi High Score");
                }
            }

            return highScores.OrderByDescending(h => h.Score).Take(10).ToList();
        }

        private (int HighScore, int Rank) GetHighScoreAndRank()
        {
            List<HighScore> highScores = GetTopHighScores();
            int highScore = highScores.Any() ? highScores.First().Score : score;
            int rank = highScores.TakeWhile(h => h.Score > score).Count() + 1;
            return (highScore, rank);
        }

        private void ShowLeaderboard()
        {
            List<HighScore> highScores = GetTopHighScores();
            string highestScoreText = highScores.Any()
                ? $"Highest Score: {highScores.First().Name} - {highScores.First().Score} (Level {highScores.First().Level})"
                : "Highest Score: None";
            string currentPlayerText = $"Current: {playerName} - {score} (Level {currentLevel})";
            string rankText = $"Rank: {GetHighScoreAndRank().Rank}";
            string leaderboardText = "\nLeaderboard:\n" + (highScores.Any()
                ? string.Join("\n", highScores.Select((hs, i) => $"{i + 1}. {hs.Name} - {hs.Score} (Level {hs.Level})"))
                : "No records yet.");

            string fullLeaderboard = $"{currentPlayerText}\n{highestScoreText}\n{rankText}\n{leaderboardText}";

            using (Form leaderboardForm = new Form())
            {
                leaderboardForm.Text = $"Leaderboard - Level {currentLevel}";
                leaderboardForm.Size = new Size(400, 500);
                leaderboardForm.FormBorderStyle = FormBorderStyle.FixedSingle;
                leaderboardForm.MaximizeBox = false;
                leaderboardForm.StartPosition = FormStartPosition.CenterParent;

                Label lblLeaderboard = new Label
                {
                    Text = fullLeaderboard,
                    Location = new Point(10, 10),
                    Size = new Size(360, 400),
                    Font = new Font("Segoe UI", 12)
                };

                Button btnClose = new Button
                {
                    Text = "Close",
                    Size = new Size(180, 40),
                    Location = new Point(110, 420),
                    Font = new Font("Segoe UI", 12, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = Color.DarkGray
                };

                btnClose.Click += (s, e) => { leaderboardForm.Close(); };

                leaderboardForm.Controls.Add(lblLeaderboard);
                leaderboardForm.Controls.Add(btnClose);
                leaderboardForm.ShowDialog(this);
            }
        }

        private void ShowSummary()
        {
            timer.Stop();
            musicPlayer.controls.stop();

            var (highScore, rank) = GetHighScoreAndRank();
            string summary = $"Player: {playerName}\n" +
                            $"Level: {currentLevel}\n" +
                            $"Score: {score}\n" +
                            $"Threshold: {levelThreshold}\n" +
                            $"Perfect: {perfectCount}\n" +
                            $"Great: {greatCount}\n" +
                            $"Miss: {missCount}\n" +
                            $"High Score: {highScore}\n" +
                            $"Rank: {rank}";

            using (Form summaryForm = new Form())
            {
                summaryForm.Text = "Level Summary";
                summaryForm.Size = new Size(300, 450);
                summaryForm.FormBorderStyle = FormBorderStyle.FixedSingle;
                summaryForm.MaximizeBox = false;
                summaryForm.StartPosition = FormStartPosition.CenterParent;

                Label lblSummary = new Label
                {
                    Text = summary,
                    Location = new Point(10, 10),
                    Size = new Size(260, 200),
                    Font = new Font("Segoe UI", 12)
                };

                Button btnLeaderboard = new Button
                {
                    Text = "Leaderboard",
                    Size = new Size(180, 40),
                    Location = new Point(50, 220),
                    Font = new Font("Segoe UI", 12, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = Color.DarkBlue
                };

                Button btnPlayAgain = new Button
                {
                    Text = "Play Again",
                    Size = new Size(180, 40),
                    Location = new Point(50, 270),
                    Font = new Font("Segoe UI", 12, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = Color.DarkGreen
                };

                Button btnNextLevel = new Button
                {
                    Text = "Next Level",
                    Size = new Size(180, 40),
                    Location = new Point(50, 320),
                    Font = new Font("Segoe UI", 12, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = Color.DarkBlue
                };

                Button btnBack = new Button
                {
                    Text = "Back to Menu",
                    Size = new Size(180, 40),
                    Location = new Point(50, 370),
                    Font = new Font("Segoe UI", 12, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = Color.DarkGray
                };

                if (score < levelThreshold)
                {
                    btnPlayAgain.Visible = false;
                    btnNextLevel.Visible = false;
                    btnLeaderboard.Visible = false;
                    btnBack.Text = "Exit";
                    btnBack.Location = new Point(50, 270);
                }
                else
                {
                    btnLeaderboard.Click += (s, e) => { ShowLeaderboard(); };

                    btnNextLevel.Click += (s, e) =>
                    {
                        notes.Clear();
                        sheetIndex = 0;
                        tick = 0;
                        score = 0;
                        perfectCount = 0;
                        greatCount = 0;
                        missCount = 0;
                        lastJudgment = "";
                        currentLevel++;
                        speedMultiplier = 1 + (currentLevel - 1) * 0.5f;
                        levelThreshold = 500 + (currentLevel - 1) * 200;
                        LoadRandomSheet();
                        if (sheetNotes.Count > 0) timer.Start();
                        summaryForm.Close();
                        Invalidate();
                    };
                }

                btnPlayAgain.Click += (s, e) =>
                {
                    notes.Clear();
                    sheetIndex = 0;
                    tick = 0;
                    score = 0;
                    perfectCount = 0;
                    greatCount = 0;
                    missCount = 0;
                    lastJudgment = "";
                    speedMultiplier = 1f;
                    levelThreshold = 500;
                    currentLevel = 1;
                    LoadRandomSheet(true);
                    if (sheetNotes.Count > 0) timer.Start();
                    summaryForm.Close();
                    Invalidate();
                };

                btnBack.Click += (s, e) => { Close(); };

                summaryForm.Controls.Add(lblSummary);
                summaryForm.Controls.Add(btnLeaderboard);
                summaryForm.Controls.Add(btnPlayAgain);
                summaryForm.Controls.Add(btnNextLevel);
                summaryForm.Controls.Add(btnBack);
                summaryForm.ShowDialog(this);
            }
        }

        private void ShowGameOver()
        {
            timer.Stop();
            musicPlayer.controls.stop();
            gameOver = true;

            string gameOverText = $"GAME OVER!\n\n" +
                                 $"Player: {playerName}\n" +
                                 $"Level: {currentLevel}\n" +
                                 $"Score: {score}\n" +
                                 $"Threshold: {levelThreshold}\n\n" +
                                 $"Bạn cần ít nhất {levelThreshold} điểm để qua màn {currentLevel}!\n" +
                                 $"Chơi lại từ đầu?";

            using (Form gameOverForm = new Form())
            {
                gameOverForm.Text = "GAME OVER - Level Failed";
                gameOverForm.Size = new Size(350, 350);
                gameOverForm.FormBorderStyle = FormBorderStyle.FixedSingle;
                gameOverForm.MaximizeBox = false;
                gameOverForm.StartPosition = FormStartPosition.CenterParent;
                gameOverForm.BackColor = Color.DarkRed;

                Label lblGameOver = new Label
                {
                    Text = gameOverText,
                    Location = new Point(10, 10),
                    Size = new Size(310, 180),
                    Font = new Font("Segoe UI", 12, FontStyle.Bold),
                    ForeColor = Color.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                Button btnRetry = new Button
                {
                    Text = "Chơi Lại Level Này",
                    Size = new Size(120, 40),
                    Location = new Point(50, 200),
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = Color.Orange
                };

                Button btnBack = new Button
                {
                    Text = "Về Menu",
                    Size = new Size(120, 40),
                    Location = new Point(180, 200),
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = Color.DarkGray
                };

                btnRetry.Click += (s, e) =>
                {
                    notes.Clear();
                    sheetIndex = 0;
                    tick = 0;
                    score = 0;
                    perfectCount = 0;
                    greatCount = 0;
                    missCount = 0;
                    lastJudgment = "";
                    gameOver = false;
                    LoadRandomSheet(true);
                    if (sheetNotes.Count > 0) timer.Start();
                    gameOverForm.Close();
                    Invalidate();
                };

                btnBack.Click += (s, e) => { Close(); };

                gameOverForm.Controls.Add(lblGameOver);
                gameOverForm.Controls.Add(btnRetry);
                gameOverForm.Controls.Add(btnBack);
                gameOverForm.ShowDialog(this);
            }
        }

        private void OnKeyDown(object? s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && !isPaused)
            {
                PauseGame();
                return;
            }

            int lane = KeyToLane(e.KeyCode);
            if (lane == -1) return;
            if (pressedKeys.Contains(e.KeyCode)) return;

            pressedKeys.Add(e.KeyCode);

            foreach (var n in notes.ToArray())
            {
                if (n.Hit || n.Key != e.KeyCode) continue;

                int deltaY = Math.Abs(n.Y + NoteSize / 2 - TargetY);
                if (deltaY <= 120) // Mở rộng window hit cho hold start
                {
                    if (n.IsLong)
                    {
                        if (deltaY <= 30)
                        {
                            n.HoldActive = true;
                            lastJudgment = "Perfect";
                            perfectCount++;
                        }
                        else if (deltaY <= 80)
                        {
                            n.HoldActive = true;
                            lastJudgment = "Great";
                            greatCount++;
                        }
                        else
                        {
                            n.HoldActive = true; // Vẫn active nhưng không cho judgment tốt, giảm miss
                            lastJudgment = "Good (Hold)";
                            greatCount++; // Cho điểm Great để giảm miss
                        }

                        if (n.HoldActive)
                        {
                            PlayHitSound();
                            lastJudgmentTime = DateTime.Now;
                            Invalidate();
                            break;
                        }
                    }
                    else
                    {
                        n.Hit = true;
                        if (deltaY <= 30)
                        {
                            score += 100;
                            perfectCount++;
                            lastJudgment = "Perfect";
                        }
                        else if (deltaY <= 80)
                        {
                            score += 50;
                            greatCount++;
                            lastJudgment = "Great";
                        }
                        else
                        {
                            score += 25; // Thêm Good để giảm miss cho nốt ngắn
                            greatCount++; // Sử dụng greatCount cho Good
                            lastJudgment = "Good";
                        }

                        lastJudgmentTime = DateTime.Now;
                        PlayHitSound();
                        break;
                    }
                }
            }

            Invalidate();
        }

        private void PauseGame()
        {
            if (isPaused) return;
            isPaused = true;
            timer.Stop();
            pausePosition = musicPlayer.controls.currentPosition;
            musicPlayer.controls.pause();

            Button btnResume = new Button
            {
                Text = "Resume",
                Size = new Size(180, 40),
                Location = new Point((Width - 180) / 2, Height / 2 - 100),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.DarkGreen
            };

            Button btnRestart = new Button
            {
                Text = "Restart",
                Size = new Size(180, 40),
                Location = new Point((Width - 180) / 2, Height / 2 - 50),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.DarkGray
            };

            Button btnExit = new Button
            {
                Text = "Exit",
                Size = new Size(180, 40),
                Location = new Point((Width - 180) / 2, Height / 2),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.DarkRed
            };

            btnResume.Click += (s, e) =>
            {
                isPaused = false;
                Controls.Remove(btnResume);
                Controls.Remove(btnRestart);
                Controls.Remove(btnExit);
                musicPlayer.controls.currentPosition = pausePosition;
                musicPlayer.controls.play();
                timer.Start();
                Invalidate();
            };

            btnRestart.Click += (s, e) =>
            {
                isPaused = false;
                Controls.Remove(btnResume);
                Controls.Remove(btnRestart);
                Controls.Remove(btnExit);
                notes.Clear();
                sheetIndex = 0;
                tick = 0;
                score = 0;
                perfectCount = 0;
                greatCount = 0;
                missCount = 0;
                lastJudgment = "";
                LoadRandomSheet(true);
                if (sheetNotes.Count > 0) timer.Start();
                Invalidate();
            };

            btnExit.Click += (s, e) => { Close(); };

            Controls.Add(btnResume);
            Controls.Add(btnRestart);
            Controls.Add(btnExit);
            btnResume.BringToFront();
            btnRestart.BringToFront();
            btnExit.BringToFront();
        }

        private void PlayHitSound()
        {
            if (hitSoundPlayer != null)
            {
                try
                {
                    hitSoundPlayer.controls.stop();
                    hitSoundPlayer.controls.play();
                }
                catch { }
            }
        }

        private void OnKeyUp(object? s, KeyEventArgs e)
        {
            pressedKeys.Remove(e.KeyCode);

            foreach (var n in notes.ToArray())
            {
                if (n.Key == e.KeyCode && n.IsLong && n.HoldActive)
                {
                    int endY = n.Y + (int)((float)n.Duration / baseSpeed * speedMultiplier);
                    if (endY < TargetY - 120) // Mở rộng threshold thả sớm
                    {
                        n.Hit = true;
                        n.HoldActive = false;
                        missCount++;
                        lastJudgment = "Miss";
                        lastJudgmentTime = DateTime.Now;
                    }
                    else if (endY >= TargetY - 120 && endY <= TargetY + 120)
                    {
                        n.Hit = true;
                        n.HoldActive = false;
                        int deltaY = Math.Abs(endY - TargetY);
                        if (deltaY <= 30)
                        {
                            score += 100;
                            perfectCount++;
                            lastJudgment = "Perfect";
                        }
                        else if (deltaY <= 80)
                        {
                            score += 50;
                            greatCount++;
                            lastJudgment = "Great";
                        }
                        else
                        {
                            score += 25; // Thêm Good cho thả không chính xác nhưng trong window
                            greatCount++;
                            lastJudgment = "Good";
                        }
                        lastJudgmentTime = DateTime.Now;
                        PlayHitSound();
                    }
                    else // Nếu thả muộn, vẫn cho Great để giảm miss
                    {
                        n.Hit = true;
                        n.HoldActive = false;
                        score += 50;
                        greatCount++;
                        lastJudgment = "Great (Late)";
                        lastJudgmentTime = DateTime.Now;
                        PlayHitSound();
                    }
                    Invalidate();
                    break;
                }
            }
        }

        private void PlayDefaultMusic()
        {
            try
            {
                string musicDir = Path.Combine(Application.StartupPath, "Music");
                if (!Directory.Exists(musicDir))
                {
                    Directory.CreateDirectory(musicDir);
                }

                string[] defaultSongs = Directory.GetFiles(musicDir, "*.mp3");
                if (defaultSongs.Length == 0)
                {
                    MessageBox.Show("Không tìm thấy bài nhạc nào trong thư mục Music.", "Thiếu nhạc");
                    return;
                }

                string randomSong = defaultSongs[random.Next(defaultSongs.Length)];
                musicPlayer.URL = randomSong;
                musicPlayer.settings.volume = 100;
                musicPlayer.controls.play();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi phát nhạc mặc định: {ex.Message}", "Lỗi nhạc");
            }
        }

        private void OnFormClosing(object? sender, FormClosingEventArgs e)
        {
            musicPlayer.controls.stop();
        }

        private int KeyToLane(Keys k)
        {
            for (int i = 0; i < 4; i++)
            {
                if (laneKeys[i] == k) return i;
            }
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            if (backgroundImage != null)
                g.DrawImage(backgroundImage, 0, 0, ClientSize.Width, ClientSize.Height);

            using (Pen lanePen = new Pen(Color.White, 2))
            {
                foreach (int x in lanePositions)
                {
                    g.DrawLine(lanePen, x - 30, 0, x - 30, Height);
                    g.DrawLine(lanePen, x + 30, 0, x + 30, Height);
                }
            }

            g.DrawLine(Pens.Red, 0, TargetY, Width, TargetY);

            for (int i = 0; i < 4; i++)
            {
                Keys key = laneKeys[i];
                int x = lanePositions[i];
                bool isPressed = pressedKeys.Contains(key);
                Brush brush = isPressed ? Brushes.Yellow : Brushes.DarkGray;

                g.FillRectangle(brush, x - 40, TargetY + 10, 80, 10);
                g.DrawString(key.ToString(), new Font("Arial", 16, FontStyle.Bold),
                    isPressed ? Brushes.Black : Brushes.White, x - 10, TargetY + 25);
            }

            foreach (var n in notes.OrderBy(note => note.Y))
            {
                if (n.Hit && !n.IsLong) continue;

                if (n.IsLong)
                {
                    int totalLength = (int)((n.Duration / baseSpeed + NoteSize) * speedMultiplier);
                    int drawY = n.Y - (totalLength - NoteSize);
                    Brush longNoteBrush = n.HoldActive ? Brushes.LimeGreen : Brushes.White;

                    g.FillRectangle(longNoteBrush, n.X - 30, drawY, 60, totalLength);
                    g.DrawRectangle(Pens.Black, n.X - 30, drawY, 60, totalLength);

                    if (n.HoldActive && n.Y <= TargetY + NoteSize)
                    {
                        g.DrawString("HOLD!", new Font("Arial", 10, FontStyle.Bold), Brushes.White, n.X - 25, n.Y - 20);
                    }

                    Image? headImage = n.ImageType == 1 ? noteImage1 : noteImage2;
                    if (headImage != null)
                    {
                        g.DrawImage(headImage, n.X - 30, n.Y, 60, NoteSize);
                    }
                    else
                    {
                        Brush headBrush = n.HoldActive ? Brushes.White : Brushes.OrangeRed;
                        g.FillEllipse(headBrush, n.X - 30, n.Y, 60, NoteSize);
                        g.DrawEllipse(Pens.Black, n.X - 30, n.Y, 60, NoteSize);
                    }
                }
                else
                {
                    Image? noteImg = n.ImageType == 1 ? noteImage1 : noteImage2;
                    if (noteImg != null)
                    {
                        g.DrawImage(noteImg, n.X - 30, n.Y, 60, 60);
                    }
                    else
                    {
                        g.FillEllipse(n.Hit ? Brushes.Gray : Brushes.Yellow, n.X - 30, n.Y, 60, 60);
                    }
                }
            }

            if (!string.IsNullOrEmpty(lastJudgment) && (DateTime.Now - lastJudgmentTime).TotalMilliseconds < 1000)
            {
                g.DrawString(lastJudgment, new Font("Arial", 24, FontStyle.Bold), Brushes.White,
                    (Width - g.MeasureString(lastJudgment, new Font("Arial", 24, FontStyle.Bold)).Width) / 2, 300);
            }

            g.DrawString($"Score: {score}", new Font("Arial", 20), Brushes.White, 10, 10);
            g.DrawString($"Speed: x{speedMultiplier:F1} | Level: {currentLevel}", new Font("Arial", 16), Brushes.Cyan, 10, 40);
            g.DrawString($"Threshold: {levelThreshold}", new Font("Arial", 16), Brushes.Orange, 10, 65);
        }
    }
}