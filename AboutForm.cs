using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using static ClickHelper.Program;

namespace ClickHelper;

public partial class AboutForm : Form
{
    private CheckBox chkNoShow;
    private CheckBox chkAutoOcr;
    private Button btnOk;
    private Panel titlePan;
    private AnimPanel animPan;
    private const string GhUrl = "https://github.com/1242509682/ClickHelper";
    private const int BottomSpacing = 200;
    private readonly string[] FeatArr = {
        
        "✦功能改进✦",
        "• 新增动态进度条，实时显示执行进度",
        "• 文字识别参数可配置化，支持自定义阈值、批大小等",
        "• 截图/预览OCR前弹窗调整参数，识别更灵活",
        "• 新增红色瞄准标志，点击时直观显示位置(不含宏播放)",
        "• 修复定时功能坐标列表模式失效问题",
        "• 图像匹配失败时可回退到坐标点击",
        "• 重构热键UI,支持修改组合键",
    };

    internal AboutForm()
    {
        Init();
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.None;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.ShowIcon = false;
        this.Padding = new Padding(15);
        this.BackColor = Color.White;
        this.Region = MakeRound(this.ClientRectangle, 16);
        chkNoShow.Checked = cfg.SkipAbout;
        this.KeyPreview = true;
        this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) this.Close(); };
    }

    private void Init()
    {
        this.Text = "";
        this.AutoSize = true;
        this.AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var table = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 9,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 10, 20, 15),
            BackColor = Color.Transparent
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        for (int i = 0; i < 9; i++)
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        int row = 0;

        // ---- 标题栏（跨两列） ----
        titlePan = new Panel
        {
            Height = 30,
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };
        var lblTitleBar = new Label
        {
            Text = "关于点击助手",
            Font = new Font("微软雅黑", 12, FontStyle.Bold),
            AutoSize = true,
            ForeColor = Color.FromArgb(0, 80, 180),
            Margin = new Padding(4, 4, 0, 0)
        };
        titlePan.Controls.Add(lblTitleBar);
        titlePan.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                WinApi.ReleaseCapture();
                WinApi.SendMessage(this.Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
            }
        };
        table.Controls.Add(titlePan, 0, row);
        table.SetColumnSpan(titlePan, 2);
        row++;

        // ---- 版本 ----
        var lblVersion = new LinkLabel
        {
            Text = $"版本 {Program.ver}",
            Font = new Font("微软雅黑", 9),
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            LinkArea = new LinkArea(0, $"版本 {Program.ver}".Length),
            LinkBehavior = LinkBehavior.HoverUnderline,
            Margin = new Padding(0, 5, 0, 5),
            LinkColor = Color.FromArgb(0, 120, 200)
        };
        lblVersion.LinkClicked += (s, e) => OpenGit();
        table.Controls.Add(lblVersion, 0, row);
        table.SetColumnSpan(lblVersion, 2);
        row++;

        // ---- 作者 ----
        var lblAuthor = new Label
        {
            Text = "羽学 QQ:1242509682",
            Font = new Font("微软雅黑", 9),
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 0, 10),
            ForeColor = Color.DimGray
        };
        table.Controls.Add(lblAuthor, 0, row);
        table.SetColumnSpan(lblAuthor, 2);
        row++;

        // ---- 分隔线1 ----
        table.Controls.Add(MakeLine(Color.FromArgb(255, 180, 0)), 0, row);
        table.SetColumnSpan(table.GetControlFromPosition(0, row), 2);
        row++;

        // ---- 功能标题 ----
        var lblFeatTitle = new Label
        {
            Text = "✨ 改进功能：",
            Font = new Font("微软雅黑", 10, FontStyle.Bold),
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 0, 4),
            ForeColor = Color.FromArgb(200, 120, 0)
        };
        table.Controls.Add(lblFeatTitle, 0, row);
        table.SetColumnSpan(lblFeatTitle, 2);
        row++;

        // ---- 功能列表 + 动画 ----
        var lblList = new Label
        {
            Text = string.Join("\n", FeatArr),
            Font = new Font("微软雅黑", 8f, FontStyle.Bold),
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 0, 8),
            ForeColor = Color.Teal
        };
        table.Controls.Add(lblList, 0, row);

        animPan = new AnimPanel
        {
            Size = new Size(50, 50),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(15, 0, 0, 0)
        };
        table.Controls.Add(animPan, 1, row);
        row++;

        // ---- 分隔线2 ----
        table.Controls.Add(MakeLine(Color.FromArgb(0, 180, 255)), 0, row);
        table.SetColumnSpan(table.GetControlFromPosition(0, row), 2);
        row++;

        // ---- 底部（跨两列） ----
        var botPan = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 10, 0, 0)
        };

        chkNoShow = new CheckBox
        {
            Text = "不显示本窗口",
            AutoSize = true,
            Font = new Font("微软雅黑", 9),
            Margin = new Padding(10, 0, BottomSpacing, 0),
            ForeColor = Color.DimGray
        };

        btnOk = new Button
        {
            Text = "确定",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            Font = new Font("微软雅黑", 9),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(0, 180, 255) },
            BackColor = Color.FromArgb(240, 248, 255),
            ForeColor = Color.FromArgb(0, 80, 180),
            Margin = new Padding(0, 0, 15, 0)
        };
        btnOk.Paint += (s, e) => DrawRoundBtn(s as Button, e.Graphics);

        // ★ 调整顺序：从右到左依次是 确定 → 自动加载OCR → 启动不显示本窗口
        botPan.Controls.Add(btnOk);
        botPan.Controls.Add(chkAutoOcr);
        botPan.Controls.Add(chkNoShow);

        table.Controls.Add(botPan, 0, row);
        table.SetColumnSpan(botPan, 2);
        row++;

        this.Controls.Add(table);

        this.FormClosing += (s, e) =>
        {
            cfg.SkipAbout = chkNoShow.Checked;
            cfg.Save();
        };
    }

    // ---- 辅助绘制方法（未改动） ----
    private Region MakeRound(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
        path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
        path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
        path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
        path.CloseFigure();
        return new Region(path);
    }

    private Control MakeLine(Color col)
    {
        return new Panel
        {
            Height = 2,
            BackColor = col,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(0, 4, 0, 4),
            MinimumSize = new Size(200, 2)
        };
    }

    private void DrawRoundBtn(Button btn, Graphics g)
    {
        if (btn == null) return;
        var rect = btn.ClientRectangle;
        rect.Inflate(-1, -1);
        using (var path = new GraphicsPath())
        {
            int r = 6;
            path.AddArc(rect.X, rect.Y, r, r, 180, 90);
            path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
            path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
            path.CloseFigure();
            btn.Region = new Region(path);
        }
    }

    private void OpenGit()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = GhUrl, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开链接：{ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            Logger.Log(ex, "OpenGit");
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        base.OnPaintBackground(e);
        using (var brush = new LinearGradientBrush(this.ClientRectangle,
            Color.FromArgb(240, 248, 255), Color.FromArgb(255, 248, 240),
            LinearGradientMode.ForwardDiagonal))
        {
            e.Graphics.FillRectangle(brush, this.ClientRectangle);
        }
    }
}