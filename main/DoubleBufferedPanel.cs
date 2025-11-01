//DoubleBufferedPanel.cs
using System.Windows.Forms;
using System.Drawing;

// A simple panel with double buffering enabled to reduce flicker
public class DoubleBufferedPanel : Panel
{
    public DoubleBufferedPanel()
    {
        this.DoubleBuffered = true;
        this.ResizeRedraw = true;
    }
}