using System;
using System.Windows.Forms;

namespace RhythmGame
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainMenuForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ứng dụng gặp lỗi: {ex.Message}", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}