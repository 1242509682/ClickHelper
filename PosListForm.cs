using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClickHelper;

public class PosListForm : Form
{
    private ListBox lstPos;
    private Button btnClear;
    private Button btnDel;
    private Button btnUp;
    private Button btnDown;
    private Button btnEdit;
    private Config cfg;
    private ClickCore core;
    private Action onChanged;
    private Timer refreshTimer;
    private int lastCount;
    private string lastHash;

    internal PosListForm(Config config, ClickCore clickCore, Action changedCallback)
    {
        cfg = config;
        core = clickCore;
        onChanged = changedCallback;

        this.Text = "位置列表编辑";
        this.Size = new Size(460, 350);
        this.StartPosition = FormStartPosition.CenterParent;
        this.MinimumSize = new Size(400, 300);

        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8)
        };
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        lstPos = new ListBox { Dock = DockStyle.Fill, Font = new Font("微软雅黑", 9F) };
        lstPos.DoubleClick += (s, e) => EditPos();
        main.Controls.Add(lstPos, 0, 0);

        var flowBtns = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        btnClear = new Button { Text = "清空", AutoSize = true, Font = new Font("微软雅黑", 9F) };
        btnDel = new Button { Text = "删除", AutoSize = true, Font = new Font("微软雅黑", 9F) };
        btnEdit = new Button { Text = "编辑", AutoSize = true, Font = new Font("微软雅黑", 9F) };
        btnUp = new Button { Text = "上移", AutoSize = true, Font = new Font("微软雅黑", 9F) };
        btnDown = new Button { Text = "下移", AutoSize = true, Font = new Font("微软雅黑", 9F) };
        flowBtns.Controls.AddRange(new Control[] { btnClear, btnDel, btnEdit, btnUp, btnDown });
        main.Controls.Add(flowBtns, 0, 1);

        this.Controls.Add(main);

        btnClear.Click += (s, e) => ClearList();
        btnDel.Click += (s, e) => DelPos();
        btnEdit.Click += (s, e) => EditPos();
        btnUp.Click += (s, e) => MovePos(-1);
        btnDown.Click += (s, e) => MovePos(1);

        refreshTimer = new Timer();
        refreshTimer.Interval = 500;
        refreshTimer.Tick += (s, e) => CheckAndRefresh();
        refreshTimer.Start();

        LoadList();
        this.FormClosing += (s, e) => refreshTimer.Stop();
    }

    private void LoadList()
    {
        lstPos.Items.Clear();
        foreach (var p in cfg.PosList)
        {
            string desc = p.Desc ?? "位置";
            string action = p.ActType == 0 ? (p.ActKey == 0 ? "左键" : p.ActKey == 1 ? "中键" : "右键") : GetKeyName(p.ActKey, p.ModKey);
            string mode = p.OpMode == 0 ? "单击" : p.OpMode == 1 ? "按下" : "弹起";
            lstPos.Items.Add($"{desc} [{action} {mode}] ({p.X},{p.Y})");
        }
        lastCount = cfg.PosList.Count;
        lastHash = GetHash();
        onChanged?.Invoke();
    }

    private string GetKeyName(int vk, int mod)
    {
        string modStr = mod switch
        {
            1 => "Ctrl+",
            2 => "Shift+",
            4 => "Alt+",
            _ => ""
        };
        return modStr + ((Keys)vk).ToString();
    }

    private string GetHash()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var p in cfg.PosList)
            sb.Append($"{p.X},{p.Y},{p.Desc},{p.ActType},{p.ActKey},{p.ModKey},{p.OpMode};");
        return sb.ToString();
    }

    private void CheckAndRefresh()
    {
        if (cfg.PosList.Count != lastCount || GetHash() != lastHash)
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
        int idx = lstPos.SelectedIndex;
        if (idx >= 0) { core.DelPos(idx); LoadList(); }
    }

    private void MovePos(int dir)
    {
        int idx = lstPos.SelectedIndex;
        int target = idx + dir;
        if (idx >= 0 && target >= 0 && target < cfg.PosList.Count)
        {
            core.SwapPos(idx, target);
            LoadList();
            lstPos.SelectedIndex = target;
        }
    }

    private void EditPos()
    {
        int idx = lstPos.SelectedIndex;
        if (idx < 0) return;
        var pos = cfg.PosList[idx];
        using var dlg = new PosEditDialog(pos);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            pos.X = dlg.NewX;
            pos.Y = dlg.NewY;
            pos.Desc = dlg.NewDesc;
            pos.ActType = dlg.NewActType;
            pos.ActKey = dlg.NewActKey;
            pos.ModKey = dlg.NewModKey;
            pos.OpMode = dlg.NewOpMode;
            cfg.Save();
            LoadList();
        }
    }

    // 内部编辑对话框
    private class PosEditDialog : Form
    {
        public int NewX { get; private set; }
        public int NewY { get; private set; }
        public string NewDesc { get; private set; }
        public int NewActType { get; private set; }
        public int NewActKey { get; private set; }
        public int NewModKey { get; private set; }
        public int NewOpMode { get; private set; }

        private NumericUpDown numX, numY;
        private TextBox txtDesc;
        private ComboBox cboActType;
        private ComboBox cboMouseKey;
        private ComboBox cboKeyKey;
        private ComboBox cboModKey;
        private ComboBox cboOpMode;
        private Button btnRec;

        public PosEditDialog(Config.PosData pos)
        {
            this.Text = "编辑位置";
            this.Size = new Size(350, 360);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                Padding = new Padding(8)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // 描述
            layout.Controls.Add(new Label { Text = "描述:", AutoSize = true }, 0, 0);
            txtDesc = new TextBox { Text = pos.Desc ?? "", Dock = DockStyle.Fill };
            layout.Controls.Add(txtDesc, 1, 0);

            // X
            layout.Controls.Add(new Label { Text = "X:", AutoSize = true }, 0, 1);
            numX = new NumericUpDown { Minimum = -99999, Maximum = 99999, Value = pos.X, Width = 100 };
            layout.Controls.Add(numX, 1, 1);

            // Y
            layout.Controls.Add(new Label { Text = "Y:", AutoSize = true }, 0, 2);
            numY = new NumericUpDown { Minimum = -99999, Maximum = 99999, Value = pos.Y, Width = 100 };
            layout.Controls.Add(numY, 1, 2);

            // 操作类型
            layout.Controls.Add(new Label { Text = "操作类型:", AutoSize = true }, 0, 3);
            cboActType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
            cboActType.Items.AddRange(new object[] { "鼠标点击", "键盘按键" });
            cboActType.SelectedIndex = pos.ActType;
            layout.Controls.Add(cboActType, 1, 3);

            // 按键
            layout.Controls.Add(new Label { Text = "按键:", AutoSize = true }, 0, 4);
            var panelBtn = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
            cboMouseKey = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
            cboMouseKey.Items.AddRange(new object[] { "左键", "中键", "右键" });
            cboMouseKey.SelectedIndex = pos.ActType == 0 ? pos.ActKey : 0;

            cboKeyKey = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
            var keys = new object[] {
                Keys.A, Keys.B, Keys.C, Keys.D, Keys.E, Keys.F, Keys.G, Keys.H, Keys.I, Keys.J,
                Keys.K, Keys.L, Keys.M, Keys.N, Keys.O, Keys.P, Keys.Q, Keys.R, Keys.S, Keys.T,
                Keys.U, Keys.V, Keys.W, Keys.X, Keys.Y, Keys.Z,
                Keys.D0, Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9,
                Keys.F1, Keys.F2, Keys.F3, Keys.F4, Keys.F5, Keys.F6, Keys.F7, Keys.F8, Keys.F9, Keys.F10, Keys.F11, Keys.F12
            };
            cboKeyKey.Items.AddRange(keys);
            if (pos.ActType == 1 && cboKeyKey.Items.Contains((Keys)pos.ActKey))
                cboKeyKey.SelectedItem = (Keys)pos.ActKey;
            else
                cboKeyKey.SelectedIndex = 0;

            btnRec = new Button { Text = "录制", AutoSize = true };

            panelBtn.Controls.Add(cboMouseKey);
            panelBtn.Controls.Add(cboKeyKey);
            panelBtn.Controls.Add(btnRec);
            layout.Controls.Add(panelBtn, 1, 4);

            // 修饰键
            layout.Controls.Add(new Label { Text = "修饰键:", AutoSize = true }, 0, 5);
            cboModKey = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
            cboModKey.Items.AddRange(new object[] { "无", "Ctrl", "Shift", "Alt" });
            cboModKey.SelectedIndex = pos.ActType == 1 ? (pos.ModKey == 1 ? 1 : pos.ModKey == 2 ? 2 : pos.ModKey == 4 ? 3 : 0) : 0;
            layout.Controls.Add(cboModKey, 1, 5);

            // 操作模式（新增）
            layout.Controls.Add(new Label { Text = "操作模式:", AutoSize = true }, 0, 6);
            cboOpMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
            cboOpMode.Items.AddRange(new object[] { "单击", "按下", "弹起" });
            cboOpMode.SelectedIndex = pos.OpMode;
            layout.Controls.Add(cboOpMode, 1, 6);

            // 确定/取消
            var flowOk = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Bottom };
            var btnOk = new Button { Text = "确定", DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel };
            flowOk.Controls.Add(btnOk);
            flowOk.Controls.Add(btnCancel);
            layout.Controls.Add(flowOk, 0, 7);
            layout.SetColumnSpan(flowOk, 2);

            this.Controls.Add(layout);
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

            // 更新可见性（操作类型切换）
            void UpdateVisibility()
            {
                bool isKey = cboActType.SelectedIndex == 1;
                cboMouseKey.Visible = !isKey;
                cboKeyKey.Visible = isKey;
                btnRec.Visible = isKey;
                cboModKey.Enabled = isKey;
            }
            cboActType.SelectedIndexChanged += (s, e) => UpdateVisibility();
            UpdateVisibility();

            // 录制键盘
            btnRec.Click += (s, e) =>
            {
                using var recDlg = new RecordKeyDialog();
                if (recDlg.ShowDialog() == DialogResult.OK)
                {
                    if (cboKeyKey.Items.Contains((Keys)recDlg.RecordedKey))
                        cboKeyKey.SelectedItem = (Keys)recDlg.RecordedKey;
                }
            };

            // 确定时取值
            btnOk.Click += (s, e) =>
            {
                NewX = (int)numX.Value;
                NewY = (int)numY.Value;
                NewDesc = txtDesc.Text.Trim();
                NewActType = cboActType.SelectedIndex;
                if (NewActType == 0)
                {
                    NewActKey = cboMouseKey.SelectedIndex;
                    NewModKey = 0;
                }
                else
                {
                    NewActKey = (int)((Keys)cboKeyKey.SelectedItem);
                    NewModKey = cboModKey.SelectedIndex switch
                    {
                        1 => 1,
                        2 => 2,
                        3 => 4,
                        _ => 0
                    };
                }
                NewOpMode = cboOpMode.SelectedIndex;
            };
        }

        // 内部录制对话框
        private class RecordKeyDialog : Form
        {
            public Keys RecordedKey { get; private set; } = Keys.None;
            private bool recording = false;
            private Label lbl;

            public RecordKeyDialog()
            {
                this.Text = "录制按键";
                this.Size = new Size(250, 100);
                this.StartPosition = FormStartPosition.CenterParent;
                this.KeyPreview = true;
                lbl = new Label { Text = "按下任意键...", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
                this.Controls.Add(lbl);
                this.Shown += (s, e) => { recording = true; };
                this.KeyDown += (s, e) =>
                {
                    if (recording && e.KeyCode != Keys.ControlKey && e.KeyCode != Keys.ShiftKey && e.KeyCode != Keys.Menu)
                    {
                        RecordedKey = e.KeyCode;
                        recording = false;
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