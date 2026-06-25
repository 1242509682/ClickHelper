using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using static ClickHelper.WinApi;

namespace ClickHelper;

/// <summary>
/// 带录制按钮的组合键显示控件（只读标签 + 修改按钮）
/// </summary>
public class HotKeyBox : UserControl
{
    private Label lbl;          // 显示热键的标签（只读）
    private Button btn;         // 修改按钮

    /// <summary>
    /// 热键字符串变化事件
    /// </summary>
    public event EventHandler<string>? HotKeyChanged;

    /// <summary>
    /// 获取或设置当前热键字符串（如 "F9"、"Ctrl+S"）
    /// </summary>
    [DefaultValue("")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public string HotKey
    {
        get => lbl.Text;
        set => lbl.Text = value;
    }

    public HotKeyBox()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        // ---- 显示标签 ----
        lbl = new Label
        {
            Dock = DockStyle.Left,
            Width = 80,
            Height = 20,
            Font = new Font("微软雅黑", 8F),
            BackColor = SystemColors.Control,      // 浅灰背景表示只读
            ForeColor = Color.FromArgb(30, 60, 90),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(2, 0, 2, 0),
            AutoSize = false,
            Margin = new Padding(0, 0, 4, 0)
        };
        // 可选的：鼠标悬停提示
        lbl.Text = "点击“改”按钮修改";

        // ---- 修改按钮 ----
        btn = new Button
        {
            Text = "改",
            Width = 24,
            Height = 20,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(200, 150, 255) },
            BackColor = Color.FromArgb(240, 230, 255),
            ForeColor = Color.FromArgb(100, 50, 160),
            Font = new Font("微软雅黑", 7F),
            Dock = DockStyle.Left,
            Margin = new Padding(0, 0, 0, 0),
            TabIndex = 1
        };
        btn.Click += Btn_Click;

        // ---- 控件容器 ----
        this.Controls.Add(lbl);
        this.Controls.Add(btn);

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
            // 更新显示并触发事件
            if (lbl.Text != formatted)
            {
                lbl.Text = formatted;
                HotKeyChanged?.Invoke(this, formatted);
            }
        }
    }

    /// <summary>
    /// 重写 Text 属性以映射到 Label 的 Text
    /// </summary>
    public override string Text
    {
        get => lbl.Text;
        set => lbl.Text = value;
    }
}