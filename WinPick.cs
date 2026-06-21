using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace ClickHelper
{
    public class WinPick : Form
    {
        private ListBox lst;
        private Button btnOk, btnCancel, btnUnbind;  // ★ 新增 btnUnbind
        public string SelProc { get; private set; } = "";

        public WinPick()
        {
            this.Text = "选择目标窗口";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(240, 244, 248);
            this.KeyPreview = true;
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) this.Close(); };

            var pan = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(12),
                BackColor = Color.Transparent
            };
            pan.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pan.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            pan.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblTitle = new Label
            {
                Text = "请选择要后台点击的目标窗口（双击确认）",
                Font = new Font("微软雅黑", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 60, 90),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 6)
            };
            pan.Controls.Add(lblTitle, 0, 0);

            lst = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("微软雅黑", 9F),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 60, 90),
                IntegralHeight = false,
                DrawMode = DrawMode.OwnerDrawVariable
            };
            lst.DrawItem += Lst_DrawItem;
            lst.MeasureItem += Lst_MeasureItem;
            lst.DoubleClick += (s, e) => { if (lst.SelectedItem != null) btnOk.PerformClick(); };
            pan.Controls.Add(lst, 0, 1);

            var flow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 0)
            };

            // ---- 确定按钮 ----
            btnOk = new Button
            {
                Text = "确定",
                DialogResult = DialogResult.OK,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(0, 180, 255) },
                BackColor = Color.FromArgb(225, 240, 255),
                ForeColor = Color.FromArgb(0, 80, 180),
                Font = new Font("微软雅黑", 9F),
                Padding = new Padding(12, 4, 12, 4)
            };

            // ★ 新增：解除绑定按钮
            btnUnbind = new Button
            {
                Text = "解除绑定",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(255, 150, 100) },
                BackColor = Color.FromArgb(255, 235, 225),
                ForeColor = Color.FromArgb(180, 60, 0),
                Font = new Font("微软雅黑", 9F),
                Padding = new Padding(12, 4, 12, 4)
            };
            btnUnbind.Click += (s, e) =>
            {
                SelProc = "";   // 返回空字符串表示解除绑定
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            // ---- 取消按钮 ----
            btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(180, 180, 180) },
                BackColor = Color.FromArgb(240, 240, 240),
                ForeColor = Color.FromArgb(80, 80, 80),
                Font = new Font("微软雅黑", 9F),
                Padding = new Padding(12, 4, 12, 4)
            };

            // 注意：FlowDirection=RightToLeft，添加顺序决定显示顺序
            // 我们想要从左到右显示：确定 | 解除绑定 | 取消
            // 因此按 取消 → 解除绑定 → 确定 的顺序添加（因为会被反转）
            flow.Controls.Add(btnCancel);
            flow.Controls.Add(btnUnbind);
            flow.Controls.Add(btnOk);

            pan.Controls.Add(flow, 0, 2);

            this.Controls.Add(pan);
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

            LoadWins();

            // 确定按钮点击：从列表中获取选中的进程名
            btnOk.Click += (s, e) =>
            {
                if (lst.SelectedItem is WinInfo wi)
                    SelProc = wi.Proc;
                else
                    SelProc = "";
            };
        }

        // ---- 自定义绘制 ----
        private void Lst_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            e.DrawBackground();
            var wi = lst.Items[e.Index] as WinInfo;
            if (wi == null) return;

            using (var fontBold = new Font("微软雅黑", 9F, FontStyle.Bold))
            using (var fontNormal = new Font("微软雅黑", 9F))
            using (var brush = new SolidBrush(e.ForeColor))
            {
                e.Graphics.DrawString(wi.Proc, fontBold, brush, e.Bounds.X, e.Bounds.Y);
                var sz = e.Graphics.MeasureString(wi.Proc, fontBold);
                float x = e.Bounds.X + sz.Width + 4;
                e.Graphics.DrawString($"({wi.Title})", fontNormal, brush, x, e.Bounds.Y);
            }
            e.DrawFocusRectangle();
        }

        private void Lst_MeasureItem(object sender, MeasureItemEventArgs e)
        {
            e.ItemHeight = 26;
        }

        private void LoadWins()
        {
            var wins = GetTopWins();
            lst.Items.Clear();
            if (wins.Count == 0)
            {
                lst.Items.Add("没有找到可用的窗口（可能都无标题）");
                btnOk.Enabled = false;
                return;
            }
            foreach (var w in wins)
                lst.Items.Add(w);
            lst.SelectedIndex = 0;
            btnOk.Enabled = true;

            using (var g = this.CreateGraphics())
            using (var font = new Font("微软雅黑", 9F))
            using (var fontBold = new Font("微软雅黑", 9F, FontStyle.Bold))
            {
                int maxWidth = 0;
                foreach (WinInfo wi in wins)
                {
                    var sz1 = g.MeasureString(wi.Proc, fontBold);
                    var sz2 = g.MeasureString($"({wi.Title})", font);
                    int total = (int)(sz1.Width + sz2.Width + 8);
                    if (total > maxWidth) maxWidth = total;
                }
                int newWidth = maxWidth + 60;
                if (newWidth < 400) newWidth = 400;
                if (newWidth > 800) newWidth = 800;
                this.Width = newWidth;
                this.Height = 500;
            }
        }

        private List<WinInfo> GetTopWins()
        {
            var list = new List<WinInfo>();
            WinApi.EnumWindows((hWnd, lParam) =>
            {
                if (WinApi.IsWindowVisible(hWnd))
                {
                    var title = GetWindowTitle(hWnd);
                    var proc = GetProcessName(hWnd);
                    if (!string.IsNullOrEmpty(proc) && !string.IsNullOrEmpty(title) && title != "0")
                    {
                        list.Add(new WinInfo { Handle = hWnd, Title = title, Proc = proc });
                    }
                }
                return true;
            }, IntPtr.Zero);
            return list;
        }

        private string GetWindowTitle(IntPtr hWnd)
        {
            var sb = new StringBuilder(256);
            WinApi.GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString().Trim();
        }

        private string GetProcessName(IntPtr hWnd)
        {
            WinApi.GetWindowThreadProcessId(hWnd, out uint pid);
            try
            {
                var p = System.Diagnostics.Process.GetProcessById((int)pid);
                return p.ProcessName;
            }
            catch { return ""; }
        }
    }

    public class WinInfo
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; } = "";
        public string Proc { get; set; } = "";
        public override string ToString() => $"{Title} ({Proc})";
    }
}