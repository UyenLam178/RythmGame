// FormMenu.cs
using System;
using System.Drawing;
using System.Windows.Forms;

namespace RhythmGame
{
    public class MainMenuForm : Form
    {
        public MainMenuForm()
        {
            // Thiết lập Form
            Text = "🎵 R H Y T H M   G A M E";
            Width = 500;
            Height = 500; // <-- TĂNG CHIỀU CAO
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            // BackgroundImage = Image.FromFile(@"F:\HK3\LTW\BunDau\BunDau\Image\BGmenu.png");
            // BackgroundImageLayout = ImageLayout.Stretch;

            try
            {
                string bgPath = Path.Combine(Application.StartupPath, "Image", "BGmenu.png");
                if (File.Exists(bgPath))
                    BackgroundImage = Image.FromFile(bgPath);
                else
                    throw new FileNotFoundException($"Không tìm thấy ảnh nền: {bgPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ứng dụng gặp lỗi khi tải ảnh nền:\n{ex.Message}", "Lỗi ảnh nền", MessageBoxButtons.OK, MessageBoxIcon.Error);
                BackgroundImage = new Bitmap(Width, Height);
                using (Graphics g = Graphics.FromImage(BackgroundImage))
                    g.Clear(Color.Black);
            }

            BackgroundImageLayout = ImageLayout.Stretch;

            // Tiêu đề Game
            Label lblTitle = new Label
            {
                Text = "RHYTHM GAME",
                Font = new Font("Segoe UI", 32, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 52, 60, 69),
                AutoSize = true,
                Location = new Point((Width - TextRenderer.MeasureText("RHYTHM GAME", new Font("Segoe UI", 32, FontStyle.Bold)).Width) / 2, 40)
            };
            Controls.Add(lblTitle);

            int buttonWidth = 250;
            int buttonHeight = 60;
            // Căn giữa -8 để bù trừ cho padding của Form
            int buttonLeft = (Width - buttonWidth) / 2 - 8;
            int topPosition = 150;
            int spacing = 80;

            // Nút "Chọn Level"
            Button btnPlay = new Button
            {
                Text = "🎮 BẮT ĐẦU",
                Top = topPosition,
                Left = buttonLeft,
                Width = buttonWidth,
                Height = buttonHeight,
                BackColor = Color.FromArgb(70, 130, 180), // Xanh dương đậm
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat, // Bật FlatStyle để tùy chỉnh màu
            };
            btnPlay.FlatAppearance.BorderSize = 0; // Bỏ viền mặc định
            btnPlay.Click += (s, e) =>
            {
                new FormPlay().Show();
            };

            // Nút "Tạo Level"
            topPosition += spacing;
            Button btnCreate = new Button
            {
                Text = "🛠️ TẠO LEVEL",
                Top = topPosition,
                Left = buttonLeft,
                Width = buttonWidth,
                Height = buttonHeight,
                BackColor = Color.FromArgb(100, 180, 100), // Xanh lá cây
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
            };
            btnCreate.FlatAppearance.BorderSize = 0;
            btnCreate.Click += (s, e) =>
            {
                new FormCreateLevel().Show();
            };

            // *** NÚT MỚI: CÀI ĐẶT ***
            topPosition += spacing;
            Button btnSettings = new Button
            {
                Text = "⚙️ CÀI ĐẶT",
                Top = topPosition,
                Left = buttonLeft,
                Width = buttonWidth,
                Height = buttonHeight,
                BackColor = Color.FromArgb(120, 120, 120), // Xám
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
            };
            btnSettings.FlatAppearance.BorderSize = 0;
            btnSettings.Click += (s, e) =>
            {
                // Mở FormSettings và đợi nó đóng lại
                using (FormSettings settingsForm = new FormSettings())
                {
                    settingsForm.ShowDialog(this);
                }
            };
            // *** KẾT THÚC NÚT MỚI ***


            // Hiệu ứng Hover cho nút
            AttachHoverEffect(btnPlay, Color.FromArgb(90, 150, 200));
            AttachHoverEffect(btnCreate, Color.FromArgb(120, 200, 120));
            AttachHoverEffect(btnSettings, Color.FromArgb(150, 150, 150)); // <-- THÊM HIỆU ỨNG


            Controls.Add(btnPlay);
            Controls.Add(btnCreate);
            Controls.Add(btnSettings); // <-- THÊM NÚT VÀO FORM
        }

        // Phương thức đính kèm hiệu ứng Hover
        private void AttachHoverEffect(Button btn, Color hoverColor)
        {
            Color defaultColor = btn.BackColor;

            btn.MouseEnter += (s, e) =>
            {
                btn.BackColor = hoverColor;
                btn.Cursor = Cursors.Hand;
            };

            btn.MouseLeave += (s, e) =>
            {
                btn.BackColor = defaultColor;
                btn.Cursor = Cursors.Default;
            };
        }
    }
}


