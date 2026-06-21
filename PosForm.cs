using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClickHelper;

/// <summary> 位置列表编辑窗口（双栏布局）</summary>
public partial class PosForm : Form
{
    private TableLayoutPanel main;
    private ListBox lst;
    private Button btnUp, btnDown, btnEdit, btnCopy, btnDel, btnClr;
    private Config cfg;
    private Core core;
    private Action onChanged;
    private Timer refTimer;
    private int lastCnt;
    private string lastHash;
    private int dragIdx = -1;

    internal PosForm(Config config, Core clickCore, Action changedCallback)
    {
        cfg = config;
        core = clickCore;
        onChanged = changedCallback;

        this.Text = "坐标管理器";
        this.Size = new Size(540, 420);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.KeyPreview = true;
        this.MinimumSize = new Size(480, 350);
        this.BackColor = Color.FromArgb(240, 244, 248); // 统一背景
        this.KeyDown += OnFormKeyDown;

         main = new TableLayoutPanel
         {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(10),
            BackColor = Color.Transparent
         };

        main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380)); // 初始宽度，稍后动态调整
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // 按钮面板固定宽度

        var leftPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblTitle = new Label
        {
            Text = "📋 坐标表",
            Font = new Font("微软雅黑", 10F, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4),
            ForeColor = Color.FromArgb(40, 60, 90)
        };
        leftPanel.Controls.Add(lblTitle, 0, 0);

        lst = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("微软雅黑", 9F),
            AllowDrop = true,
            IntegralHeight = false,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        lst.DoubleClick += (s, e) => EditPos();
        lst.MouseDown += OnMouseDown;
        lst.DragEnter += (s, e) => e.Effect = DragDropEffects.Move;
        lst.DragDrop += OnDragDrop;
        leftPanel.Controls.Add(lst, 0, 1);

        main.Controls.Add(leftPanel, 0, 0);

        var rightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8, 0, 0, 0),
            BackColor = Color.Transparent
        };
        rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblOps = new Label
        {
            Text = "⚙ 操作",
            Font = new Font("微软雅黑", 10F, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4),
            ForeColor = Color.FromArgb(40, 60, 90)
        };
        rightPanel.Controls.Add(lblOps, 0, 0);

        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = false,
            Padding = new Padding(0, 10, 0, 0),
            BackColor = Color.Transparent
        };
        btnPanel.Layout += (s, e) =>
        {
            int totalHeight = 0;
            foreach (Control c in btnPanel.Controls)
                totalHeight += c.Height + c.Margin.Top + c.Margin.Bottom;
            if (btnPanel.Controls.Count > 0)
                totalHeight -= btnPanel.Controls[0].Margin.Top + btnPanel.Controls[btnPanel.Controls.Count - 1].Margin.Bottom;
            int pad = (btnPanel.Height - totalHeight) / 2;
            if (pad < 0) pad = 0;
            btnPanel.Padding = new Padding(0, pad, 0, pad);
        };

        Font btnFont = new Font("微软雅黑", 9F);
        int btnWidth = 85;
        int btnHeight = 30;

        // 按钮统一扁平风格，不同功能配不同颜色
        btnUp = new Button
        {
            Text = "▲上移",
            Width = btnWidth,
            Height = btnHeight,
            Font = btnFont,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 2, 0, 2),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(0, 180, 255) },
            BackColor = Color.FromArgb(225, 240, 255),
            ForeColor = Color.FromArgb(0, 80, 180)
        };
        btnDown = new Button
        {
            Text = "▼下移",
            Width = btnWidth,
            Height = btnHeight,
            Font = btnFont,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 2, 0, 2),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(0, 180, 255) },
            BackColor = Color.FromArgb(225, 240, 255),
            ForeColor = Color.FromArgb(0, 80, 180)
        };
        btnEdit = new Button
        {
            Text = "✎编辑",
            Width = btnWidth,
            Height = btnHeight,
            Font = btnFont,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 2, 0, 2),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(255, 180, 0) },
            BackColor = Color.FromArgb(255, 245, 225),
            ForeColor = Color.FromArgb(180, 100, 0)
        };
        btnCopy = new Button
        {
            Text = "📋复制",
            Width = btnWidth,
            Height = btnHeight,
            Font = btnFont,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 2, 0, 2),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(100, 200, 200) },
            BackColor = Color.FromArgb(225, 245, 245),
            ForeColor = Color.FromArgb(0, 100, 120)
        };
        btnDel = new Button
        {
            Text = "✖删除",
            Width = btnWidth,
            Height = btnHeight,
            Font = btnFont,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 2, 0, 2),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(255, 150, 100) },
            BackColor = Color.FromArgb(255, 235, 225),
            ForeColor = Color.FromArgb(180, 60, 0)
        };
        btnClr = new Button
        {
            Text = "🗑清空",
            Width = btnWidth,
            Height = btnHeight,
            Font = btnFont,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 2, 0, 2),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(180, 180, 180) },
            BackColor = Color.FromArgb(240, 240, 240),
            ForeColor = Color.FromArgb(80, 80, 80)
        };

        btnPanel.Controls.AddRange(new Control[] { btnUp, btnDown, btnEdit, btnCopy, btnDel, btnClr });
        rightPanel.Controls.Add(btnPanel, 0, 1);

        main.Controls.Add(rightPanel, 1, 0);
        this.Controls.Add(main);

        // ---- 事件绑定（保持不变） ----
        btnUp.Click += (s, e) => MovePos(-1);
        btnDown.Click += (s, e) => MovePos(1);
        btnEdit.Click += (s, e) => EditPos();
        btnCopy.Click += (s, e) => CopyPos();
        btnDel.Click += (s, e) => DelPos();
        btnClr.Click += (s, e) => ClearList();

        refTimer = new Timer { Interval = 500 };
        refTimer.Tick += (s, e) => CheckRefresh();
        refTimer.Start();

        LoadList();
        this.FormClosing += (s, e) => { refTimer.Stop(); refTimer.Dispose(); };
        this.Resize += (s, e) => btnPanel.PerformLayout();
    }

    // ---- 以下所有方法保持不变（只是样式已改） ----
    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            this.Close();
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.C)
        {
            if (lst.Focused)
            {
                CopyPos();
                e.Handled = true;
            }
        }
        else if (e.KeyCode == Keys.Delete)
        {
            if (lst.Focused)
            {
                DelPos();
                e.Handled = true;
            }
        }
    }

    private void OnMouseDown(object sender, MouseEventArgs e)
    {
        dragIdx = lst.IndexFromPoint(e.Location);
        if (dragIdx >= 0)
            lst.DoDragDrop(dragIdx, DragDropEffects.Move);
    }

    private void OnDragDrop(object sender, DragEventArgs e)
    {
        int src = (int)e.Data.GetData(typeof(int));
        if (src < 0 || src >= cfg.PosList.Count) return;
        Point pt = lst.PointToClient(new Point(e.X, e.Y));
        int tgt = lst.IndexFromPoint(pt);
        if (tgt < 0) tgt = cfg.PosList.Count - 1;
        if (tgt >= cfg.PosList.Count) tgt = cfg.PosList.Count - 1;
        if (src == tgt) return;
        core.Swap(src, tgt);
        LoadList();
        lst.SelectedIndex = tgt;
        dragIdx = -1;
    }

    private void LoadList()
    {
        lst.Items.Clear();
        int idx = 1;
        foreach (var p in cfg.PosList)
        {
            string desc = p.Desc ?? "位置";
            string action;
            if (p.UseTxt)
                action = $"文字:{p.TxtMatch}";
            else if (p.ActType == 0)
                action = p.ActKey == 0 ? "左键" : p.ActKey == 1 ? "中键" : "右键";
            else if (p.ActType == 1)
                action = GetKeyName(p.ActKey, p.ModKey);
            else if (p.ActType == 2)
                action = $"文本:\"{p.TextContent}\"";
            else if (p.ActType == 3)
                action = $"组合键:{p.ComboKeys}";
            else
                action = "未知";

            string mode = p.OpMode == 0 ? "单击" : p.OpMode == 1 ? "按下" : "弹起";
            string delayStr = p.WaitMs > 0 ? $" [延迟{p.WaitMs}ms]" : string.Empty;
            string uiaTag = p.UseUIA ? "[UIA]" : "";
            string pos = p.UseImage ? " | 图像匹配" : $" | 坐标:{p.X},{p.Y}";
            string display = $"[{idx}] {desc}{delayStr} {uiaTag} [{action} {mode}] {pos}";
            lst.Items.Add(display);
            idx++;
        }
        lastCnt = cfg.PosList.Count;
        lastHash = GetHash();
        onChanged?.Invoke();
        AdjustWidth();
    }

    private void AdjustWidth()
    {
        if (lst.Items.Count == 0) return;

        int maxTextWidth = 0;
        using (var g = this.CreateGraphics())
        using (var font = new Font("微软雅黑", 9F))
        {
            foreach (var item in lst.Items)
            {
                string text = item.ToString();
                var sz = g.MeasureString(text, font);
                int w = (int)Math.Ceiling(sz.Width) + 10; // 加一些内边距
                if (w > maxTextWidth) maxTextWidth = w;
            }
        }

        // 限制最大宽度，避免超出屏幕
        int maxAllowed = Screen.PrimaryScreen.WorkingArea.Width - 100;
        if (maxTextWidth > maxAllowed) maxTextWidth = maxAllowed;

        // 列1宽度 = 文本最大宽度 + 额外内边距（列表左右边距）
        int col1Width = maxTextWidth + 20;
        // 列2固定宽度（按钮面板宽度 + 右边距）
        int col2Width = 120; // 与构造函数中的第二列宽度一致

        // 总宽度 = 列1 + 列2 + 主容器左右内边距 (10*2) + 窗体边框（大约 4*2）
        int totalWidth = col1Width + col2Width + 20 + 8;
        if (totalWidth < 540) totalWidth = 540; // 保持最小宽度

        // 更新列宽
        main.ColumnStyles[0].Width = col1Width;
        main.ColumnStyles[0].SizeType = SizeType.Absolute;
        main.ColumnStyles[1].Width = col2Width;
        main.ColumnStyles[1].SizeType = SizeType.Absolute;

        // 调整窗体宽度
        this.Width = totalWidth;
    }

    private string GetKeyName(int vk, int mod)
    {
        string modStr = mod switch { 1 => "Ctrl+", 2 => "Shift+", 4 => "Alt+", _ => "" };
        return modStr + ((Keys)vk).ToString();
    }

    private string GetHash()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var p in cfg.PosList)
            sb.Append($"{p.X},{p.Y},{p.Desc},{p.ActType},{p.ActKey},{p.ModKey},{p.OpMode},{p.WaitMs};");
        return sb.ToString();
    }

    private void CheckRefresh()
    {
        if (cfg.PosList.Count != lastCnt || GetHash() != lastHash)
            LoadList();
    }

    private void ClearList()
    {
        if (cfg.PosList.Count == 0) return;
        if (MessageBox.Show("确定清空所有位置？", "确认", MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            cfg.PosList.Clear();
            cfg.Save();
            LoadList();
        }
    }

    private void DelPos()
    {
        int idx = lst.SelectedIndex;
        if (idx >= 0) { core.Del(idx); LoadList(); }
    }

    private void MovePos(int dir)
    {
        int idx = lst.SelectedIndex;
        int tgt = idx + dir;
        if (idx >= 0 && tgt >= 0 && tgt < cfg.PosList.Count)
        {
            core.Swap(idx, tgt);
            LoadList();
            lst.SelectedIndex = tgt;
        }
    }

    private void CopyPos()
    {
        int idx = lst.SelectedIndex;
        if (idx < 0)
        {
            MessageBox.Show("请先选中要复制的项目", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var src = cfg.PosList[idx];
        var copy = new Config.PosData
        {
            X = src.X,
            Y = src.Y,
            Desc = src.Desc != null ? (string)src.Desc.Clone() : null,
            ActType = src.ActType,
            ActKey = src.ActKey,
            ModKey = src.ModKey,
            OpMode = src.OpMode,
            WaitMs = src.WaitMs,
            UseImage = src.UseImage,
            ImageTemp = src.ImageTemp,
            Threshold = src.Threshold,
            UseTxt = src.UseTxt,
            TxtMatch = src.TxtMatch,
            TxtMode = src.TxtMode,
            TxtThresh = src.TxtThresh,
            UseUIA = src.UseUIA,
            UIAProc = src.UIAProc,
            TextContent = src.TextContent,
            ComboKeys = src.ComboKeys,
        };
        int insertIdx = idx + 1;
        cfg.PosList.Insert(insertIdx, copy);
        cfg.Save();
        LoadList();
        lst.SelectedIndex = insertIdx;
    }

    private void EditPos()
    {
        int idx = lst.SelectedIndex;
        if (idx < 0) return;
        var pos = cfg.PosList[idx];
        using var dlg = new PosEdit(pos);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            pos.X = dlg.NewX;
            pos.Y = dlg.NewY;
            pos.Desc = dlg.NewDesc;
            pos.ActType = dlg.NewActType;
            pos.ActKey = dlg.NewActKey;
            pos.ModKey = dlg.NewModKey;
            pos.OpMode = dlg.NewOpMode;
            pos.WaitMs = dlg.NewWaitMs;
            pos.UseImage = dlg.NewUseImageMatch;
            pos.ImageTemp = dlg.NewImageTemplate;
            pos.Threshold = dlg.NewThreshold;
            // ---- 新增文字匹配字段 ----
            pos.UseTxt = dlg.NewUseTxt;
            pos.TxtMatch = dlg.NewTxtMatch;
            pos.TxtMode = dlg.NewTxtMode;
            pos.TxtThresh = dlg.NewTxtThresh;
            // UIA
            pos.UseUIA = dlg.NewUseUIA;
            pos.UIAProc = dlg.NewUIAProc;
            pos.TextContent = dlg.NewTextContent;
            pos.ComboKeys = dlg.NewComboKeys;
            cfg.Save();
            LoadList();
        }
    }
}