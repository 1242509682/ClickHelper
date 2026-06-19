using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClickHelper
{
    public partial class AboutForm : Form
    {
        private Config cfg;
        private CheckBox chkNoShow;
        private Button btnOk;
        private Panel titlePan;
        private AnimPanel animPan;

        private const string GhUrl = "https://github.com/1242509682/ClickHelper";
        private readonly string[] FeatArr = {
            
            "• 美化了部分UI排版",
            "• 加入了识别文字：",
            "  本功能需要使用截图后按B键",
            "  缺失依赖则自动弹窗提示下载",
            "  仅在首次加载Ocr引擎才会很慢",
        };

        internal AboutForm(Config config)
        {
            cfg = config;
            Init();
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.None;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.Padding = new Padding(15);
            this.BackColor = Color.White;
            this.Region = MakeRound(this.ClientRectangle, 16);
            this.KeyPreview = true;
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) this.Close(); };
        }

        private void Init()
        {
            this.Text = "";
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            // 两列布局：左列内容，右列动画
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

            // ---- 版本（跨两列） ----
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

            // ---- 作者（跨两列） ----
            var lblAuthor = new Label
            {
                Text = "开发 羽学 QQ:1242509682",
                Font = new Font("微软雅黑", 9),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 0, 10),
                ForeColor = Color.DimGray
            };
            table.Controls.Add(lblAuthor, 0, row);
            table.SetColumnSpan(lblAuthor, 2);
            row++;

            // ---- 分隔线1（跨两列） ----
            table.Controls.Add(MakeLine(Color.FromArgb(255, 180, 0)), 0, row);
            table.SetColumnSpan(table.GetControlFromPosition(0, row), 2);
            row++;

            // ---- 功能标题（跨两列） ----
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

            // ---- 功能列表（第一列） + 动画（第二列） ----
            var lblList = new Label
            {
                Text = string.Join("\n", FeatArr),
                Font = new Font("微软雅黑", 9.5f),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 0, 8),
                ForeColor = Color.Black   // 改为黑色
            };
            table.Controls.Add(lblList, 0, row);

            // 动画放在第二列，垂直居中（使用Anchor）
            animPan = new AnimPanel
            {
                Size = new Size(50, 50),   // 稍大一点，更明显
                Anchor = AnchorStyles.Left,
                Margin = new Padding(15, 0, 0, 0)
            };
            table.Controls.Add(animPan, 1, row);
            row++;

            // ---- 分隔线2（跨两列） ----
            table.Controls.Add(MakeLine(Color.FromArgb(0, 180, 255)), 0, row);
            table.SetColumnSpan(table.GetControlFromPosition(0, row), 2);
            row++;

            // ---- 底部（跨两列） ----
            var botPan = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 10, 0, 0)
            };
            chkNoShow = new CheckBox
            {
                Text = "不再显示此窗口",
                AutoSize = true,
                Font = new Font("微软雅黑", 9),
                Margin = new Padding(10, 0, 0, 0),
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
                Margin = new Padding(0, 0, 0, 0)
            };
            btnOk.Paint += (s, e) => DrawRoundBtn(s as Button, e.Graphics);
            botPan.Controls.Add(btnOk);
            botPan.Controls.Add(chkNoShow);
            table.Controls.Add(botPan, 0, row);
            table.SetColumnSpan(botPan, 2);
            row++;

            this.Controls.Add(table);

            this.FormClosing += (s, e) =>
            {
                if (chkNoShow.Checked)
                {
                    cfg.SkipAbout = true;
                    cfg.Save();
                }
            };
        }

        // ---- 辅助绘制方法（保持不变） ----
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

        // ---- 内部动画控件（与之前相同，无改动） ----
        private class AnimPanel : Panel
        {
            private Timer animTim;
            private float rotAng;
            private float gap;
            private int step;
            private int phase;
            private const int MaxS = 60;

            public AnimPanel()
            {
                this.DoubleBuffered = true;
                this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
                this.SetStyle(ControlStyles.UserPaint, true);
                this.SetStyle(ControlStyles.Opaque, false);
                this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);

                rotAng = 0;
                gap = 360;
                step = 0;
                phase = 0;

                animTim = new Timer { Interval = 20 };
                animTim.Tick += (s, e) => TickAnim();
                this.Paint += (s, e) => OnDraw(e.Graphics);
                this.Disposed += (s, e) => animTim?.Stop();
                animTim.Start();
            }

            private void TickAnim()
            {
                step++;
                if (step > MaxS)
                {
                    phase = 1 - phase;
                    step = 0;
                    if (phase == 0)
                    {
                        gap = 360;
                        rotAng = 0;
                    }
                    else
                    {
                        gap = 0;
                        rotAng = 360;
                    }
                    this.Invalidate();
                    return;
                }

                float prog = step / (float)MaxS;
                float ease = 1 - (float)Math.Pow(1 - prog, 2);

                if (phase == 0)
                {
                    gap = 360f * (1 - ease);
                    rotAng = 360f * ease;
                }
                else
                {
                    gap = 360f * ease;
                    rotAng = 360f + 360f * ease;
                }
                this.Invalidate();
            }

            private void OnDraw(Graphics g)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                int size = Math.Min(this.Width, this.Height);
                if (size < 6) return;
                int r = size / 2 - 2;
                float sweep = 360f - gap;
                if (sweep < 0.5f) return;

                float start = -90f + rotAng;

                float c = phase == 0 ? 1 - (float)step / MaxS : (float)step / MaxS;
                Color col1 = Color.FromArgb(0, 180, 255);
                Color col2 = Color.FromArgb(255, 180, 0);
                int rc = (int)(col1.R + (col2.R - col1.R) * c);
                int gc = (int)(col1.G + (col2.G - col1.G) * c);
                int bc = (int)(col1.B + (col2.B - col1.B) * c);

                using (var pen = new Pen(Color.FromArgb(200, rc, gc, bc), 2.5f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawArc(pen, 2, 2, size - 4, size - 4, start, sweep);
                }
            }
        }
    }
}