using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using static ClickHelper.WinApi;

namespace ClickHelper;

/// <summary>
/// 带录制按钮的组合键输入框（紧凑美观）
/// </summary>
public class HotKeyBox : UserControl
{
    private TextBox txt;
    private Button btn;

    public event EventHandler<string>? HotKeyChanged;

    [DefaultValue("")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public string HotKey
    {
        get => txt.Text;
        set => txt.Text = value;
    }

    public HotKeyBox()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        // ---- 输入框 ----
        txt = new TextBox
        {
            Dock = DockStyle.Left,
            Width = 80,                     // 足够显示组合键
            Height = 20,                     // 足够显示组合键
            Font = new Font("微软雅黑", 8F),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90),
            TextAlign = HorizontalAlignment.Left,
            Margin = new Padding(0, 0, 4, 0) // 右侧留出间距
        };
        txt.TextChanged += (s, e) => HotKeyChanged?.Invoke(this, txt.Text);

        // ---- 录制按钮 ----
        btn = new Button
        {
            Text = "改",
            Width = 20,
            Height = 20,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(200, 150, 255) },
            BackColor = Color.FromArgb(240, 230, 255),
            ForeColor = Color.FromArgb(100, 50, 160),
            Font = new Font("微软雅黑", 7F),
            Dock = DockStyle.Left,
            Margin = new Padding(0, 0, 0, 0)
        };
        btn.Click += Btn_Click;

        // ---- 添加控件 ----
        this.Controls.Add(txt);
        this.Controls.Add(btn);

        // ---- 控件自身设置 ----
        this.AutoSize = true;
        this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        this.Dock = DockStyle.None;
        this.MinimumSize = new Size(110, 26);
        this.MaximumSize = new Size(200, 28);
        this.Padding = new Padding(0, 0, 0, 0);

        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private void Btn_Click(object sender, EventArgs e)
    {
        using var recorder = new RecordKey();
        if (recorder.ShowDialog() == DialogResult.OK)
        {
            string formatted = FormatHotKey(recorder.RecordedModifiers, recorder.RecordedKey);
            this.HotKey = formatted;
        }
    }
}