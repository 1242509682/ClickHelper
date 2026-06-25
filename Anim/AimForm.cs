using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClickHelper;

/// <summary>
/// 红色瞄准标志窗体，用于在点击位置显示一个临时瞄准图标。
/// 窗体在显示 300ms 后自动关闭，支持调试信息显示。
/// </summary>
internal class AimForm : Form
{
    // 定时器，用于控制窗体自动关闭
    private Timer closeTimer;

    /// <summary>
    /// 初始化瞄准窗体，并定位到指定屏幕坐标。
    /// </summary>
    /// <param name="x">点击位置的屏幕 X 坐标</param>
    /// <param name="y">点击位置的屏幕 Y 坐标</param>
    public AimForm(int x, int y)
    {
        // 禁用自动缩放，避免系统 DPI 缩放干扰窗体定位
        this.AutoScaleMode = AutoScaleMode.None;

        // 设置窗体基础样式：无边框、不显示在任务栏、始终置顶
        this.FormBorderStyle = FormBorderStyle.None;
        this.ShowInTaskbar = false;
        this.TopMost = true;

        // 窗体大小固定为 40×40 像素（内部绘制区域）
        this.Size = new Size(40, 40);

        // 定位窗体：由于系统 DPI 缩放（此处为 125%）导致 X 方向偏移需要额外补偿，
        // 经过实测，X 方向需减去 80 像素，Y 方向减去 20 像素才能让窗体中心对准点击点。
        // 若 DPI 改变，需相应调整这些值（或改用动态计算）。
        this.StartPosition = FormStartPosition.Manual;
        this.Location = new Point(x - 80, y - 20);

        // 设置透明背景：使用 Lime 色作为透明键，使窗体只显示绘制的图形
        this.BackColor = Color.Lime;
        this.TransparencyKey = Color.Lime;

        // 启用双缓冲减少闪烁
        this.DoubleBuffered = true;

        // 订阅 Paint 事件，绘制瞄准图形
        this.Paint += (s, e) => DrawAim(e.Graphics);

        // 添加调试标签，显示当前点击坐标（方便验证偏移量）
        var lbl = new Label
        {
            Text = $"{x},{y}",
            ForeColor = Color.White,
            BackColor = Color.FromArgb(128, Color.Black), // 半透明背景
            AutoSize = true,
            Font = new Font("Consolas", 8),
            Location = new Point(0, 0) // 位于窗体左上角
        };
        this.Controls.Add(lbl);

        // 初始化并启动自动关闭定时器（300ms 后关闭）
        closeTimer = new Timer { Interval = 300 };
        closeTimer.Tick += (s, e) =>
        {
            closeTimer.Stop();
            this.Close(); // 关闭窗体
        };
        closeTimer.Start();

        // 窗体显示时激活，确保获得焦点（可选）
        this.Shown += (s, e) => this.Activate();
    }

    /// <summary>
    /// 绘制红色瞄准标志：外圈圆形、十字线、中心实心点。
    /// </summary>
    /// <param name="g">绘图表面</param>
    private void DrawAim(Graphics g)
    {
        // 启用抗锯齿使图形更平滑
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // 计算窗体客户区的中心点（即瞄准标志的中心）
        int cx = this.ClientSize.Width / 2;   // 20
        int cy = this.ClientSize.Height / 2;  // 20

        // 定义图形参数（半径、线宽、十字线长度）
        int radius = 12;                // 外圈半径
        float penWidth = 1.5f;          // 线条宽度
        int lineLen = radius - 2;       // 十字线长度（比半径短，避免画出圈外）

        // 使用红色画笔绘制外圈和十字线
        using (var pen = new Pen(Color.Red, penWidth))
        {
            // 绘制外圈（以中心点为圆心）
            g.DrawEllipse(pen, cx - radius, cy - radius, radius * 2, radius * 2);

            // 绘制水平十字线（左右对称）
            g.DrawLine(pen, cx - lineLen, cy, cx + lineLen, cy);

            // 绘制垂直十字线（上下对称）
            g.DrawLine(pen, cx, cy - lineLen, cx, cy + lineLen);
        }

        // 绘制中心实心红色小点（强调瞄准点）
        using (var brush = new SolidBrush(Color.Red))
        {
            g.FillEllipse(brush, cx - 2, cy - 2, 4, 4);
        }
    }

    /// <summary>
    /// 窗体关闭时释放定时器资源，避免内存泄漏。
    /// </summary>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // 停止并释放定时器
        closeTimer?.Stop();
        closeTimer?.Dispose();
        // 调用基类方法，确保其他资源正常释放
        base.OnFormClosed(e);
    }
}