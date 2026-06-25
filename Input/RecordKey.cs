using System.Drawing;
using System.Windows.Forms;
using static ClickHelper.WinApi;

namespace ClickHelper;

public class RecordKey : Form
{
    public uint RecordedModifiers { get; private set; }
    public Keys RecordedKey { get; private set; }
    private bool recording = false;
    private Label lbl;

    public RecordKey()
    {
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        TopMost = true;
        BackColor = Color.Black;
        Opacity = 0.3;
        KeyPreview = true;

        lbl = new Label
        {
            Text = "请按下组合键,如:F9 或 Alt+S ..\n取消修改:按 ESC 键",
            ForeColor = Color.White,
            BackColor = Color.FromArgb(128, 0, 0, 0),  // 原为128
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("微软雅黑", 16F, FontStyle.Bold),
            Size = new Size(600, 100),
            Location = new Point((Screen.PrimaryScreen.Bounds.Width - 600) / 2,
                                 (Screen.PrimaryScreen.Bounds.Height - 100) / 2)
        };
        Controls.Add(lbl);

        this.Shown += (s, e) => recording = true;
        this.KeyDown += (s, e) =>
        {
            if (!recording) return;
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            uint mods = 0;
            if (e.Control) mods |= MOD_CONTROL;
            if (e.Shift) mods |= MOD_SHIFT;
            if (e.Alt) mods |= MOD_ALT;
            Keys key = e.KeyCode;
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu)
                return;

            RecordedModifiers = mods;
            RecordedKey = key;
            recording = false;
            DialogResult = DialogResult.OK;
            Close();
        };
    }
}