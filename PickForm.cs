using System.Drawing;
using System.Windows.Forms;

namespace ClickHelper;

/// <summary> 鼠标拾取坐标窗体（全屏半透明，点击获取坐标） </summary>
public class PickForm : Form
{
    public Point PickPoint { get; private set; }

    public PickForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        TopMost = true;
        BackColor = Color.Black;
        Opacity = 0.15; // 轻微透明便于观察背景
        KeyPreview = true;

        // ESC 取消
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        };

        // 左键点击拾取坐标
        MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                PickPoint = e.Location; // 全屏窗体坐标即屏幕坐标
                // 调用动画
                 new AnimForm(PickPoint).Show();
                DialogResult = DialogResult.OK;
                Close();
            }
        };

        // ---- 提示文字（放置在屏幕底部） ----
        var lbl = new Label
        {
            Text = "点击任意位置获取坐标，按 ESC 取消",
            ForeColor = Color.White,
            BackColor = Color.FromArgb(60, 0, 0, 0),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("微软雅黑", 14F, FontStyle.Bold),
            Size = new Size(500, 60)        // 略宽以容纳文字
        };

        // 计算底部居中位置（考虑任务栏高度，通常约40px，这里留出更多空间）
        int screenW = Screen.PrimaryScreen.Bounds.Width;
        int screenH = Screen.PrimaryScreen.Bounds.Height;
        lbl.Location = new Point((screenW - lbl.Width) / 2, screenH - lbl.Height - 30);

        this.Controls.Add(lbl);
    }
}