//FormSettings.cs
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using WMPLib;

namespace RhythmGame
{
    public class FormSettings : Form
    {
        private const string ConfigFile = "config.txt";

        private readonly TextBox txtMusicFolder = new TextBox(); // Changed to folder selection
        private readonly TextBox txtHitSoundPath = new TextBox();
        private readonly TextBox txtBackgroundPath = new TextBox();
        private readonly TextBox txtKey1 = new TextBox();
        private readonly TextBox txtKey2 = new TextBox();
        private readonly TextBox txtKey3 = new TextBox();
        private readonly TextBox txtKey4 = new TextBox();
        private readonly WindowsMediaPlayer musicPlayer = new WindowsMediaPlayer();
        private string[] musicFiles = new string[0];
        private Random random = new Random();
        private int currentTrackIndex = 0;
        private string[] shuffledPlaylist = new string[0];

        public FormSettings()
        {
            Text = "⚙️ Cài Đặt";
            Width = 550;
            Height = 420;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = Color.FromArgb(30, 30, 40);
            ForeColor = Color.White;

            InitializeComponents();
            LoadSettings();
            LoadMusicFiles();
            ShuffleAndPlayMusic();
            musicPlayer.PlayStateChange += MusicPlayer_PlayStateChange;

            FormClosing += (s, e) =>
            {
                try { musicPlayer.controls.stop(); } catch { }
            };
        }

        private void InitializeComponents()
        {
            int labelWidth = 150;
            int textBoxWidth = 280;
            int buttonWidth = 80;
            int startX = 20;
            int startY = 20;
            int spacing = 35;

            // 1. Music Folder
            Controls.Add(CreateLabel("Thư mục nhạc:", startX, startY));
            txtMusicFolder.Location = new Point(startX + labelWidth, startY);
            txtMusicFolder.Width = textBoxWidth;
            txtMusicFolder.ReadOnly = true; // Prevent manual editing
            Controls.Add(txtMusicFolder);
            Controls.Add(CreateBrowseButton("BrowseMusicFolder", "Duyệt...", startX + labelWidth + textBoxWidth + 10, startY, BrowseMusicFolder_Click));

            // 2. HitSound Path
            startY += spacing;
            Controls.Add(CreateLabel("HitSound (.wav):", startX, startY));
            txtHitSoundPath.Location = new Point(startX + labelWidth, startY);
            txtHitSoundPath.Width = textBoxWidth;
            Controls.Add(txtHitSoundPath);
            Controls.Add(CreateBrowseButton("BrowseHitSound", "Duyệt...", startX + labelWidth + textBoxWidth + 10, startY, BrowseHitSound_Click));

            // 3. Background Path
            startY += spacing;
            Controls.Add(CreateLabel("Background (Play Mode):", startX, startY));
            txtBackgroundPath.Location = new Point(startX + labelWidth, startY);
            txtBackgroundPath.Width = textBoxWidth;
            Controls.Add(txtBackgroundPath);
            Controls.Add(CreateBrowseButton("BrowseBackground", "Duyệt...", startX + labelWidth + textBoxWidth + 10, startY, BrowseBackground_Click));

            // 4. Key Bindings
            startY += spacing + 10;
            Controls.Add(CreateLabel("Phím 1 (Lane 1):", startX, startY));
            Controls.Add(CreateKeyTextBox(txtKey1, startX + labelWidth, startY));

            startY += spacing;
            Controls.Add(CreateLabel("Phím 2 (Lane 2):", startX, startY));
            Controls.Add(CreateKeyTextBox(txtKey2, startX + labelWidth, startY));

            startY += spacing;
            Controls.Add(CreateLabel("Phím 3 (Lane 3):", startX, startY));
            Controls.Add(CreateKeyTextBox(txtKey3, startX + labelWidth, startY));

            startY += spacing;
            Controls.Add(CreateLabel("Phím 4 (Lane 4):", startX, startY));
            Controls.Add(CreateKeyTextBox(txtKey4, startX + labelWidth, startY));

            // Save and Close Buttons
            Button btnSave = new Button
            {
                Text = "💾 Lưu Cài Đặt",
                Width = 200,
                Height = 50,
                Location = new Point(Width / 2 - 210, Height - 100),
                BackColor = Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;
            Controls.Add(btnSave);

            Button btnClose = new Button
            {
                Text = "Đóng",
                Width = 200,
                Height = 50,
                Location = new Point(Width / 2 + 10, Height - 100),
                BackColor = Color.FromArgb(100, 100, 100),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => Close();
            Controls.Add(btnClose);

            // Hover effects
            AttachHoverEffect(btnSave, Color.FromArgb(90, 150, 200));
            AttachHoverEffect(btnClose, Color.FromArgb(150, 150, 150));
        }

        private TextBox CreateKeyTextBox(TextBox tb, int x, int y)
        {
            tb.Location = new Point(x, y);
            tb.Width = 50;
            tb.Font = new Font("Segoe UI", 10);
            tb.MaxLength = 1;
            tb.CharacterCasing = CharacterCasing.Upper;
            tb.TextAlign = HorizontalAlignment.Center;
            return tb;
        }

        private Label CreateLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y + 3),
                AutoSize = true,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
        }

        private Button CreateBrowseButton(string name, string text, int x, int y, EventHandler onClick)
        {
            Button btn = new Button
            {
                Name = name,
                Text = text,
                Location = new Point(x, y - 2),
                Width = 80,
                Height = 28,
                BackColor = Color.Gray,
                Font = new Font("Segoe UI", 9)
            };
            btn.Click += onClick;
            AttachHoverEffect(btn, Color.FromArgb(150, 150, 150));
            return btn;
        }

        private void AttachHoverEffect(Button btn, Color hoverColor)
        {
            Color defaultColor = btn.BackColor;
            btn.MouseEnter += (s, e) => { btn.BackColor = hoverColor; btn.Cursor = Cursors.Hand; };
            btn.MouseLeave += (s, e) => { btn.BackColor = defaultColor; btn.Cursor = Cursors.Default; };
        }

        private void LoadSettings()
        {
            if (!File.Exists(ConfigFile))
            {
                txtMusicFolder.Text = Path.Combine(Application.StartupPath, "Music");
                txtKey1.Text = "A";
                txtKey2.Text = "S";
                txtKey3.Text = "D";
                txtKey4.Text = "F";
                return;
            }

            try
            {
                var settings = File.ReadAllLines(ConfigFile)
                    .Where(line => line.Contains("="))
                    .Select(line => line.Split('='))
                    .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());

                txtMusicFolder.Text = settings.GetValueOrDefault("musicFolder", Path.Combine(Application.StartupPath, "Music"));
                txtHitSoundPath.Text = settings.GetValueOrDefault("hitsound", "");
                txtBackgroundPath.Text = settings.GetValueOrDefault("background", "");
                txtKey1.Text = settings.GetValueOrDefault("key1", "A");
                txtKey2.Text = settings.GetValueOrDefault("key2", "S");
                txtKey3.Text = settings.GetValueOrDefault("key3", "D");
                txtKey4.Text = settings.GetValueOrDefault("key4", "F");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải cài đặt: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveSettings()
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(ConfigFile, false))
                {
                    sw.WriteLine($"musicFolder={txtMusicFolder.Text}");
                    sw.WriteLine($"hitsound={txtHitSoundPath.Text}");
                    sw.WriteLine($"background={txtBackgroundPath.Text}");
                    sw.WriteLine($"key1={txtKey1.Text}");
                    sw.WriteLine($"key2={txtKey2.Text}");
                    sw.WriteLine($"key3={txtKey3.Text}");
                    sw.WriteLine($"key4={txtKey4.Text}");
                }
                MessageBox.Show("Đã lưu cài đặt!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu cài đặt: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BrowseMusicFolder_Click(object? sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog { Description = "Chọn thư mục chứa nhạc (.mp3, .wav)" })
            {
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtMusicFolder.Text = fbd.SelectedPath;
                    LoadMusicFiles(); // Reload music files from new folder
                    ShuffleAndPlayMusic(); // Play new shuffled playlist
                }
            }
        }

        private void BrowseHitSound_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "WAV Files|*.wav", Title = "Chọn file HitSound" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtHitSoundPath.Text = ofd.FileName;
                }
            }
        }

        private void BrowseBackground_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Image Files|*.jpg;*.png;*.bmp", Title = "Chọn ảnh nền" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtBackgroundPath.Text = ofd.FileName;
                }
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            SaveSettings();
        }

        private void LoadMusicFiles()
        {
            try
            {
                string musicFolder = txtMusicFolder.Text;
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
            if (musicFiles.Length == 0) return;

            try
            {
                shuffledPlaylist = musicFiles.OrderBy(x => random.Next()).ToArray();
                currentTrackIndex = 0;
                PlayCurrentTrack();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xáo trộn nhạc: {ex.Message}", "Lỗi phát nhạc", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PlayCurrentTrack()
        {
            if (shuffledPlaylist.Length == 0 || currentTrackIndex >= shuffledPlaylist.Length) return;

            try
            {
                musicPlayer.URL = shuffledPlaylist[currentTrackIndex];
                musicPlayer.settings.autoStart = true;
                musicPlayer.settings.volume = 50;
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
    }
}