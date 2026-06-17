using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClickHelper;

/// <summary> 位置列表编辑窗口（双栏布局）</summary>
public class PosForm : Form
{
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

        this.Text = "位置列表编辑";
        this.Size = new Size(520, 400);
        this.StartPosition = FormStartPosition.CenterParent;
        this.MinimumSize = new Size(480, 350);

        this.KeyPreview = true;
        this.KeyDown += OnFormKeyDown;

        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(10)
        };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        var leftPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0)
        };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblTitle = new Label
        {
            Text = "📋 位置列表",
            Font = new Font("微软雅黑", 10F, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };
        leftPanel.Controls.Add(lblTitle, 0, 0);

        lst = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("微软雅黑", 9F),
            AllowDrop = true,
            IntegralHeight = false
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
            Padding = new Padding(8, 0, 0, 0)
        };
        rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblOps = new Label
        {
            Text = "⚙ 操作",
            Font = new Font("微软雅黑", 10F, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };
        rightPanel.Controls.Add(lblOps, 0, 0);

        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = false,
            Padding = new Padding(0, 10, 0, 0)
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
        int btnWidth = 80;
        int btnHeight = 30;
        btnUp = new Button { Text = "▲上移", Width = btnWidth, Height = btnHeight, Font = btnFont, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 2, 0, 2) };
        btnDown = new Button { Text = "▼下移", Width = btnWidth, Height = btnHeight, Font = btnFont, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 2, 0, 2) };
        btnEdit = new Button { Text = "✎编辑", Width = btnWidth, Height = btnHeight, Font = btnFont, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 2, 0, 2) };
        btnCopy = new Button { Text = "📋复制", Width = btnWidth, Height = btnHeight, Font = btnFont, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 2, 0, 2) };
        btnDel = new Button { Text = "✖删除", Width = btnWidth, Height = btnHeight, Font = btnFont, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 2, 0, 2) };
        btnClr = new Button { Text = "🗑清空", Width = btnWidth, Height = btnHeight, Font = btnFont, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 2, 0, 2) };

        btnPanel.Controls.AddRange(new Control[] { btnUp, btnDown, btnEdit, btnCopy, btnDel, btnClr });
        rightPanel.Controls.Add(btnPanel, 0, 1);

        main.Controls.Add(rightPanel, 1, 0);
        this.Controls.Add(main);

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
            if (p.ActType == 0)
                action = p.ActKey == 0 ? "左键" : p.ActKey == 1 ? "中键" : "右键";
            else
                action = GetKeyName(p.ActKey, p.ModKey);
            string mode = p.OpMode == 0 ? "单击" : p.OpMode == 1 ? "按下" : "弹起";
            string delayStr = p.WaitMs > 0 ? $" [延迟{p.WaitMs}ms]" : "";
            string display = $"[{idx}] {desc}{delayStr} [{action} {mode}] ({p.X},{p.Y})";
            lst.Items.Add(display);
            idx++;
        }
        lastCnt = cfg.PosList.Count;
        lastHash = GetHash();
        onChanged?.Invoke();
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
            WaitMs = src.WaitMs
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
            cfg.Save();
            LoadList();
        }
    }

    // ---- 内部编辑对话框 ----
    private class PosEdit : Form
    {
        public int NewX, NewY, NewActType, NewActKey, NewModKey, NewOpMode, NewWaitMs;
        public string NewDesc = "";

        private NumericUpDown numX, numY, numWait;
        private TextBox txtDesc;
        private ComboBox cboAct, cboMouse, cboKey, cboMod, cboMode;
        private Button btnRec;

        public PosEdit(Config.PosData pos)
        {
            this.Text = "编辑位置";
            this.Size = new Size(350, 390);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            var lay = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 8,
                Padding = new Padding(8)
            };
            lay.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            lay.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            lay.Controls.Add(new Label { Text = "名称:", AutoSize = true }, 0, 0);
            txtDesc = new TextBox { Text = pos.Desc ?? "", Dock = DockStyle.Fill };
            lay.Controls.Add(txtDesc, 1, 0);

            lay.Controls.Add(new Label { Text = "X:", AutoSize = true }, 0, 1);
            numX = new NumericUpDown { Minimum = -99999, Maximum = 99999, Value = pos.X, Width = 100 };
            lay.Controls.Add(numX, 1, 1);

            lay.Controls.Add(new Label { Text = "Y:", AutoSize = true }, 0, 2);
            numY = new NumericUpDown { Minimum = -99999, Maximum = 99999, Value = pos.Y, Width = 100 };
            lay.Controls.Add(numY, 1, 2);

            lay.Controls.Add(new Label { Text = "类型:", AutoSize = true }, 0, 3);
            cboAct = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
            cboAct.Items.AddRange(new object[] { "鼠标点击", "键盘按键" });
            cboAct.SelectedIndex = pos.ActType;
            lay.Controls.Add(cboAct, 1, 3);

            lay.Controls.Add(new Label { Text = "按键:", AutoSize = true }, 0, 4);
            var pan = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
            cboMouse = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
            cboMouse.Items.AddRange(new object[] { "左键", "中键", "右键" });
            cboMouse.SelectedIndex = pos.ActType == 0 ? pos.ActKey : 0;
            cboKey = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
            var keys = WinApi.GetCommonKeys();  // 使用公共键列表
            cboKey.Items.AddRange(keys);
            if (pos.ActType == 1 && cboKey.Items.Contains((Keys)pos.ActKey))
                cboKey.SelectedItem = (Keys)pos.ActKey;
            else
                cboKey.SelectedIndex = 0;
            btnRec = new Button { Text = "录制", AutoSize = true };
            pan.Controls.Add(cboMouse);
            pan.Controls.Add(cboKey);
            pan.Controls.Add(btnRec);
            lay.Controls.Add(pan, 1, 4);

            lay.Controls.Add(new Label { Text = "修饰键:", AutoSize = true }, 0, 5);
            cboMod = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
            cboMod.Items.AddRange(new object[] { "无", "Ctrl", "Shift", "Alt" });
            cboMod.SelectedIndex = pos.ActType == 1 ? (pos.ModKey == 1 ? 1 : pos.ModKey == 2 ? 2 : pos.ModKey == 4 ? 3 : 0) : 0;
            lay.Controls.Add(cboMod, 1, 5);

            lay.Controls.Add(new Label { Text = "模式:", AutoSize = true }, 0, 6);
            cboMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
            cboMode.Items.AddRange(new object[] { "单击", "按下", "弹起" });
            cboMode.SelectedIndex = pos.OpMode;
            lay.Controls.Add(cboMode, 1, 6);

            lay.Controls.Add(new Label { Text = "延迟:", AutoSize = true }, 0, 7);
            numWait = new NumericUpDown { Minimum = 0, Maximum = 3600000, Value = pos.WaitMs, Width = 80 };
            lay.Controls.Add(numWait, 1, 7);

            var flowOk = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Bottom };
            var btnOk = new Button { Text = "确定", DialogResult = DialogResult.OK, AutoSize = true };
            var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
            flowOk.Controls.Add(btnOk);
            flowOk.Controls.Add(btnCancel);
            lay.Controls.Add(flowOk, 0, 8);
            lay.SetColumnSpan(flowOk, 2);

            this.Controls.Add(lay);
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

            void UpdateVis()
            {
                bool isKey = cboAct.SelectedIndex == 1;
                cboMouse.Visible = !isKey;
                cboKey.Visible = isKey;
                btnRec.Visible = isKey;
                cboMod.Enabled = isKey;
                cboMode.Visible = true;
                numWait.Visible = true;
            }
            cboAct.SelectedIndexChanged += (s, e) => UpdateVis();
            UpdateVis();

            btnRec.Click += (s, e) =>
            {
                using var rec = new RecordKey();
                if (rec.ShowDialog() == DialogResult.OK)
                {
                    if (cboKey.Items.Contains((Keys)rec.RecordedKey))
                        cboKey.SelectedItem = (Keys)rec.RecordedKey;
                }
            };

            btnOk.Click += (s, e) =>
            {
                NewX = (int)numX.Value;
                NewY = (int)numY.Value;
                NewDesc = txtDesc.Text.Trim();
                NewActType = cboAct.SelectedIndex;
                if (NewActType == 0)
                {
                    NewActKey = cboMouse.SelectedIndex;
                    NewModKey = 0;
                }
                else
                {
                    NewActKey = (int)((Keys)cboKey.SelectedItem);
                    NewModKey = cboMod.SelectedIndex switch { 1 => 1, 2 => 2, 3 => 4, _ => 0 };
                }
                NewOpMode = cboMode.SelectedIndex;
                NewWaitMs = (int)numWait.Value;
            };
        }

        private class RecordKey : Form
        {
            public Keys RecordedKey { get; private set; } = Keys.None;
            private bool rec = false;
            private Label lbl;

            public RecordKey()
            {
                this.Text = "录制按键";
                this.Size = new Size(250, 100);
                this.StartPosition = FormStartPosition.CenterParent;
                this.KeyPreview = true;
                lbl = new Label { Text = "按下任意键...", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
                this.Controls.Add(lbl);
                this.Shown += (s, e) => { rec = true; };
                this.KeyDown += (s, e) =>
                {
                    if (rec && e.KeyCode != Keys.ControlKey && e.KeyCode != Keys.ShiftKey && e.KeyCode != Keys.Menu)
                    {
                        RecordedKey = e.KeyCode;
                        rec = false;
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                    }
                };
            }
        }
    }
}