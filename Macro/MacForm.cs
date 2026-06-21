using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ClickHelper.Macro;

public class MacForm : Form
{
    // UI 控件
    private ListBox lstFiles;
    private ListBox lstItems;
    private Button btnPlay, btnStopPlay, btnRec, btnStopRec, btnConv, btnEdit, btnDel, btnClear;
    private ComboBox cboRecHot, cboPlayHot;
    private Label lblStatus;
    private ToolTip tip = new ToolTip();

    private Config cfg;
    private Action changed;
    private Main mainForm;
    private string curMacro;
    private int dragIdx = -1;
    private FileSystemWatcher watcher;
    private Dictionary<string, string> fileMap;

    internal MacForm(Config config, Action onChanged, Main mainFormRef)
    {
        cfg = config;
        changed = onChanged;
        mainForm = mainFormRef;
        fileMap = new Dictionary<string, string>();

        Text = "宏管理";
        Size = new Size(740, 480);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        KeyPreview = true;
        BackColor = Color.FromArgb(240, 244, 248);  // 统一背景
        KeyDown += MacForm_KeyDown;

        // ---- 主布局 ----
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(8)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // ---- 左侧面板（两行列表） ----
        var leftPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(0, 0, 8, 0),
            BackColor = Color.Transparent
        };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 70));

        // 顶部操作按钮（新建/保存/加载/删除/清空列表）
        var top = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        var btnNew = new Button
        {
            Text = "新建",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(0, 180, 255) },
            BackColor = Color.FromArgb(225, 240, 255),
            ForeColor = Color.FromArgb(0, 80, 180),
            Font = new Font("微软雅黑", 9F)
        };
        btnNew.Click += (s, e) => NewMac();
        tip.SetToolTip(btnNew, "创建新宏文件");
        top.Controls.Add(btnNew);

        var btnLoad = new Button
        {
            Text = "加载",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(100, 200, 200) },
            BackColor = Color.FromArgb(225, 245, 245),
            ForeColor = Color.FromArgb(0, 100, 120),
            Font = new Font("微软雅黑", 9F)
        };
        btnLoad.Click += (s, e) => LoadMac();
        tip.SetToolTip(btnLoad, "从外部加载宏文件");
        top.Controls.Add(btnLoad);

        var btnSave = new Button
        {
            Text = "保存",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(100, 200, 100) },
            BackColor = Color.FromArgb(230, 245, 230),
            ForeColor = Color.FromArgb(0, 120, 0),
            Font = new Font("微软雅黑", 9F)
        };
        btnSave.Click += (s, e) => SaveMac();
        tip.SetToolTip(btnSave, "保存当前宏");
        top.Controls.Add(btnSave);

        var btnDelMac = new Button
        {
            Text = "删除",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(255, 150, 100) },
            BackColor = Color.FromArgb(255, 235, 225),
            ForeColor = Color.FromArgb(180, 60, 0),
            Font = new Font("微软雅黑", 9F)
        };
        btnDelMac.Click += (s, e) => DelMac();
        tip.SetToolTip(btnDelMac, "删除选中的宏文件");
        top.Controls.Add(btnDelMac);

        var btnClrAll = new Button
        {
            Text = "清空列表",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(180, 180, 180) },
            BackColor = Color.FromArgb(240, 240, 240),
            ForeColor = Color.FromArgb(80, 80, 80),
            Font = new Font("微软雅黑", 9F)
        };
        btnClrAll.Click += (s, e) => ClearAll();
        tip.SetToolTip(btnClrAll, "删除所有宏文件");
        top.Controls.Add(btnClrAll);

        leftPanel.Controls.Add(top, 0, 0);

        // 宏文件列表（上半）
        lstFiles = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("微软雅黑", 9F),
            IntegralHeight = false,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        lstFiles.SelectedIndexChanged += (s, e) => LoadItems();
        leftPanel.Controls.Add(lstFiles, 0, 1);

        // 宏指令列表（下半）
        lstItems = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("微软雅黑", 9F),
            IntegralHeight = false,
            AllowDrop = true,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        lstItems.DoubleClick += (s, e) => Edit();
        lstItems.MouseDown += OnLstMouseDown;
        lstItems.DragEnter += (s, e) => e.Effect = DragDropEffects.Move;
        lstItems.DragDrop += OnLstDragDrop;
        leftPanel.Controls.Add(lstItems, 0, 2);

        mainLayout.Controls.Add(leftPanel, 0, 0);

        // ---- 右侧面板 ----
        var rightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(8, 0, 0, 0),
            BackColor = Color.Transparent
        };
        for (int i = 0; i < 8; i++)
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5f));
        rightPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rightPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // ---- 行0：状态标签 ----
        lblStatus = new Label
        {
            Text = "就绪",
            AutoSize = true,
            Font = new Font("微软雅黑", 9F, FontStyle.Bold),
            ForeColor = Color.Green,
            Anchor = AnchorStyles.None
        };
        rightPanel.Controls.Add(lblStatus, 0, 0);
        rightPanel.SetColumnSpan(lblStatus, 2);

        // ---- 行1：录制 / 停录 ----
        btnRec = MakeButton("录制", Color.FromArgb(0, 180, 255), Color.FromArgb(225, 240, 255), Color.FromArgb(0, 80, 180));
        btnStopRec = MakeButton("停录", Color.FromArgb(255, 100, 100), Color.FromArgb(255, 230, 230), Color.FromArgb(180, 0, 0));
        btnStopRec.Enabled = false;
        tip.SetToolTip(btnRec, "开始录制宏（热键可切换）");
        tip.SetToolTip(btnStopRec, "停止录制");
        var flow1 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        flow1.Controls.Add(btnRec);
        flow1.Controls.Add(btnStopRec);
        flow1.Layout += (s, e) => CenterControls(flow1);
        rightPanel.Controls.Add(flow1, 0, 1);
        rightPanel.SetColumnSpan(flow1, 2);

        // ---- 行2：播放 / 停播 ----
        btnPlay = MakeButton("播放", Color.FromArgb(255, 180, 0), Color.FromArgb(255, 245, 225), Color.FromArgb(180, 100, 0));
        btnStopPlay = MakeButton("停播", Color.FromArgb(255, 150, 100), Color.FromArgb(255, 235, 225), Color.FromArgb(180, 60, 0));
        btnStopPlay.Enabled = false;
        tip.SetToolTip(btnPlay, "播放当前宏（热键可切换）");
        tip.SetToolTip(btnStopPlay, "停止播放");
        var flow2 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        flow2.Controls.Add(btnPlay);
        flow2.Controls.Add(btnStopPlay);
        flow2.Layout += (s, e) => CenterControls(flow2);
        rightPanel.Controls.Add(flow2, 0, 2);
        rightPanel.SetColumnSpan(flow2, 2);

        // ---- 行3：转换 / 编辑 ----
        btnConv = MakeButton("转换", Color.FromArgb(200, 150, 255), Color.FromArgb(240, 230, 255), Color.FromArgb(100, 50, 160));
        btnEdit = MakeButton("编辑", Color.FromArgb(100, 200, 200), Color.FromArgb(225, 245, 245), Color.FromArgb(0, 100, 120));
        tip.SetToolTip(btnConv, "将宏转换为坐标管理表");
        tip.SetToolTip(btnEdit, "编辑选中的宏指令");
        var flow3 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        flow3.Controls.Add(btnConv);
        flow3.Controls.Add(btnEdit);
        flow3.Layout += (s, e) => CenterControls(flow3);
        rightPanel.Controls.Add(flow3, 0, 3);
        rightPanel.SetColumnSpan(flow3, 2);

        // ---- 行4：删除 / 清空 ----
        btnDel = MakeButton("删行", Color.FromArgb(180, 180, 180), Color.FromArgb(240, 240, 240), Color.FromArgb(80, 80, 80));
        btnClear = MakeButton("清空", Color.FromArgb(180, 180, 180), Color.FromArgb(240, 240, 240), Color.FromArgb(80, 80, 80));
        tip.SetToolTip(btnDel, "删除选中的宏指令");
        tip.SetToolTip(btnClear, "清空当前宏的所有指令");
        var flow4 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        flow4.Controls.Add(btnDel);
        flow4.Controls.Add(btnClear);
        flow4.Layout += (s, e) => CenterControls(flow4);
        rightPanel.Controls.Add(flow4, 0, 4);
        rightPanel.SetColumnSpan(flow4, 2);

        // ---- 行5：热键设置标题 ----
        var lblHot = new Label
        {
            Text = "热键设置:",
            AutoSize = true,
            Font = new Font("微软雅黑", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(40, 60, 90),
            Anchor = AnchorStyles.None
        };
        rightPanel.Controls.Add(lblHot, 0, 5);
        rightPanel.SetColumnSpan(lblHot, 2);

        // ---- 行6：录制切换标签 + 下拉 ----
        rightPanel.Controls.Add(new Label
        {
            Text = "录制:",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(40, 60, 90),
            Font = new Font("微软雅黑", 9F)
        }, 0, 6);
        cboRecHot = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 80,
            Anchor = AnchorStyles.Left,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90),
            Font = new Font("微软雅黑", 9F)
        };
        rightPanel.Controls.Add(cboRecHot, 1, 6);
        cboRecHot.Anchor = AnchorStyles.Left;

        // ---- 行7：播放切换标签 + 下拉 ----
        rightPanel.Controls.Add(new Label
        {
            Text = "播放:",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(40, 60, 90),
            Font = new Font("微软雅黑", 9F)
        }, 0, 7);
        cboPlayHot = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 80,
            Anchor = AnchorStyles.Left,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90),
            Font = new Font("微软雅黑", 9F)
        };
        rightPanel.Controls.Add(cboPlayHot, 1, 7);
        cboPlayHot.Anchor = AnchorStyles.Left;

        mainLayout.Controls.Add(rightPanel, 1, 0);

        Controls.Add(mainLayout);

        // ---- 事件绑定（保持不变） ----
        btnRec.Click += (s, e) => { mainForm.ToglRecord(); UpdateButtons(); };
        btnStopRec.Click += (s, e) => { mainForm.ToglRecord(); UpdateButtons(); };
        btnPlay.Click += (s, e) => { mainForm.SwitchPlay(); UpdateButtons(); };
        btnStopPlay.Click += (s, e) => { mainForm.SwitchPlay(); UpdateButtons(); };
        btnConv.Click += (s, e) => ConvMac();
        btnEdit.Click += (s, e) => Edit();
        btnDel.Click += (s, e) => DelItem();
        btnClear.Click += (s, e) => ClearMac();

        cboRecHot.SelectedIndexChanged += OnRecHotChanged;
        cboPlayHot.SelectedIndexChanged += OnPlayHotChanged;

        // 初始化热键下拉
        var keys = WinApi.GetCommonKeys();
        cboRecHot.Items.AddRange(keys);
        cboPlayHot.Items.AddRange(keys);
        LoadHotFromCfg();

        // 初始化文件列表
        RefreshFiles();

        // 设置文件监视器
        SetupWatcher();

        // 订阅主窗口状态变化
        mainForm.StatusChanged += OnMainStatusChanged;
        UpdateButtons();
        FormClosing += (s, e) =>
        {
            mainForm.StatusChanged -= OnMainStatusChanged;
            watcher?.Dispose();
        };
    }

    // ---- 辅助方法：创建统一风格的按钮 ----
    private Button MakeButton(string text, Color border, Color back, Color fore)
    {
        return new Button
        {
            Text = text,
            Width = 70,
            Height = 28,
            TextAlign = ContentAlignment.MiddleCenter,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = border },
            BackColor = back,
            ForeColor = fore,
            Font = new Font("微软雅黑", 9F)
        };
    }

    private void CenterControls(FlowLayoutPanel panel)
    {
        if (panel.Controls.Count == 0) return;
        int totalWidth = 0;
        foreach (Control c in panel.Controls)
            totalWidth += c.Width + c.Margin.Left + c.Margin.Right;
        int pad = (panel.Width - totalWidth) / 2;
        if (pad < 0) pad = 0;
        panel.Padding = new Padding(pad, 0, pad, 0);
    }

    // ---- 文件系统监视 ----
    private void SetupWatcher()
    {
        string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Macros");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        watcher = new FileSystemWatcher(dir, "*.txt");
        watcher.Changed += (s, e) => RefreshFiles();
        watcher.Created += (s, e) => RefreshFiles();
        watcher.Deleted += (s, e) => RefreshFiles();
        watcher.Renamed += (s, e) => RefreshFiles();
        watcher.SynchronizingObject = this;
        watcher.EnableRaisingEvents = true;
    }

    private void RefreshFiles()
    {
        if (this.IsDisposed) return;
        string oldDisplay = lstFiles.SelectedItem?.ToString();
        string oldName = (oldDisplay != null && fileMap.ContainsKey(oldDisplay)) ? fileMap[oldDisplay] : null;
        lstFiles.Items.Clear();
        fileMap.Clear();
        var names = MacIO.GetMacroNames();
        int idx = 1;
        foreach (var n in names)
        {
            string display = $"{idx}. {n}";
            lstFiles.Items.Add(display);
            fileMap[display] = n;
            idx++;
        }
        if (lstFiles.Items.Count > 0)
        {
            if (oldName != null)
            {
                string newDisplay = null;
                foreach (var kv in fileMap)
                {
                    if (kv.Value == oldName)
                    {
                        newDisplay = kv.Key;
                        break;
                    }
                }
                if (newDisplay != null)
                    lstFiles.SelectedItem = newDisplay;
                else
                    lstFiles.SelectedIndex = 0;
            }
            else
                lstFiles.SelectedIndex = 0;
        }
        else
        {
            curMacro = null;
            lstItems.Items.Clear();
        }
    }

    private void ClearAll()
    {
        if (lstFiles.Items.Count == 0)
        {
            MessageBox.Show("没有宏文件可清空", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show("确定要删除所有宏文件吗？此操作不可撤销！", "确认清空", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            var names = MacIO.GetMacroNames();
            foreach (var name in names)
                MacIO.Delete(name);
            RefreshFiles();
            changed?.Invoke();
        }
    }

    // ---- 快捷键 ----
    private void MacForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (lstFiles.Focused)
        {
            if (e.KeyCode == Keys.Delete)
            {
                DelMac();
                e.Handled = true;
            }
            return;
        }

        if (lstItems.Focused)
        {
            if (e.KeyCode == Keys.Delete)
            {
                DelItem();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.C)
            {
                CopyItem();
                e.Handled = true;
            }
        }
    }

    private void CopyItem()
    {
        if (curMacro == null) return;
        int idx = lstItems.SelectedIndex;
        if (idx < 0) return;

        var data = MacIO.Load(curMacro);
        if (data == null || idx >= data.Items.Count) return;

        var src = data.Items[idx];
        var copy = new MacItem
        {
            Act = src.Act,
            X = src.X,
            Y = src.Y,
            Key = src.Key,
            Delta = src.Delta,
            Time = src.Time
        };

        int insertIdx = idx + 1;
        data.Items.Insert(insertIdx, copy);
        MacIO.Save(data);

        LoadItems();
        lstItems.SelectedIndex = insertIdx;
        changed?.Invoke();
    }

    // ---- 热键下拉 ----
    private void LoadHotFromCfg()
    {
        cboRecHot.SelectedIndexChanged -= OnRecHotChanged;
        cboPlayHot.SelectedIndexChanged -= OnPlayHotChanged;

        if (cboRecHot.Items.Contains((Keys)cfg.MacRecHotKey))
            cboRecHot.SelectedItem = (Keys)cfg.MacRecHotKey;
        else if (cboRecHot.Items.Count > 0)
            cboRecHot.SelectedIndex = 0;

        if (cboPlayHot.Items.Contains((Keys)cfg.MacPlayHotKey))
            cboPlayHot.SelectedItem = (Keys)cfg.MacPlayHotKey;
        else if (cboPlayHot.Items.Count > 0)
            cboPlayHot.SelectedIndex = 0;

        cboRecHot.SelectedIndexChanged += OnRecHotChanged;
        cboPlayHot.SelectedIndexChanged += OnPlayHotChanged;
    }

    private void OnRecHotChanged(object? sender, EventArgs e)
    {
        if (cboRecHot.SelectedItem == null || cboPlayHot.SelectedItem == null)
            return;
        int newRec = (int)(Keys)cboRecHot.SelectedItem;
        int newPlay = (int)(Keys)cboPlayHot.SelectedItem;
        mainForm.UpMacKeys(newRec, newPlay);
    }

    private void OnPlayHotChanged(object? sender, EventArgs e)
    {
        if (cboRecHot.SelectedItem == null || cboPlayHot.SelectedItem == null)
            return;
        int newRec = (int)(Keys)cboRecHot.SelectedItem;
        int newPlay = (int)(Keys)cboPlayHot.SelectedItem;
        mainForm.UpMacKeys(newRec, newPlay);
    }

    // ---- 主窗口状态变化 ----
    private void OnMainStatusChanged(string status)
    {
        if (IsDisposed) return;
        if (lblStatus != null && !lblStatus.IsDisposed)
        {
            lblStatus.Text = status;
            if (status.Contains("录制")) lblStatus.ForeColor = Color.Red;
            else if (status.Contains("播放")) lblStatus.ForeColor = Color.Blue;
            else if (status.Contains("空闲")) lblStatus.ForeColor = Color.Green;
            else lblStatus.ForeColor = Color.Black;
        }
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        if (IsDisposed) return;
        bool isRec = mainForm?.IsRecording ?? false;
        bool isPlay = mainForm?.IsPlaying ?? false;
        btnRec.Enabled = !isRec;
        btnStopRec.Enabled = isRec;
        btnPlay.Enabled = !isPlay;
        btnStopPlay.Enabled = isPlay;
    }

    // ---- 加载宏指令列表 ----
    private void LoadItems()
    {
        if (lstFiles.SelectedItem == null)
        {
            lstItems.Items.Clear();
            curMacro = null;
            return;
        }
        string display = lstFiles.SelectedItem.ToString();
        if (!fileMap.TryGetValue(display, out string realName))
        {
            lstItems.Items.Clear();
            curMacro = null;
            return;
        }
        curMacro = realName;
        var data = MacIO.Load(curMacro);
        if (data == null)
        {
            lstItems.Items.Clear();
            return;
        }
        lstItems.Items.Clear();
        int i = 1;
        foreach (var it in data.Items)
        {
            string actName = GetActionName(it.Act);
            string extra = "";
            if (it.Act == MacAction.Move) extra = $" ({it.X},{it.Y})";
            else if (it.Act == MacAction.Wheel) extra = $" Δ={it.Delta}";
            else if (it.Act == MacAction.KDown || it.Act == MacAction.KUp) extra = $" {(Keys)it.Key}";
            else extra = $" ({it.X},{it.Y})";
            string s = $"{i,3} {it.Time,6}ms : {actName}{extra}";
            lstItems.Items.Add(s);
            i++;
        }
    }

    private string GetActionName(MacAction act)
    {
        return act switch
        {
            MacAction.Move => "移动",
            MacAction.LDown => "左键按下",
            MacAction.LUp => "左键弹起",
            MacAction.RDown => "右键按下",
            MacAction.RUp => "右键弹起",
            MacAction.MDown => "中键按下",
            MacAction.MUp => "中键弹起",
            MacAction.Wheel => "滚轮",
            MacAction.KDown => "按键按下",
            MacAction.KUp => "按键弹起",
            _ => act.ToString()
        };
    }

    // ---- 拖拽排序 ----
    private void OnLstMouseDown(object sender, MouseEventArgs e)
    {
        dragIdx = lstItems.IndexFromPoint(e.Location);
        if (dragIdx >= 0)
            lstItems.DoDragDrop(dragIdx, DragDropEffects.Move);
    }

    private void OnLstDragDrop(object sender, DragEventArgs e)
    {
        int src = (int)e.Data.GetData(typeof(int));
        if (src < 0) return;
        if (curMacro == null) return;
        var data = MacIO.Load(curMacro);
        if (data == null) return;
        if (src >= data.Items.Count) return;
        Point pt = lstItems.PointToClient(new Point(e.X, e.Y));
        int tgt = lstItems.IndexFromPoint(pt);
        if (tgt < 0) tgt = data.Items.Count - 1;
        if (tgt >= data.Items.Count) tgt = data.Items.Count - 1;
        if (src == tgt) return;
        var tmp = data.Items[src];
        data.Items.RemoveAt(src);
        data.Items.Insert(tgt, tmp);
        MacIO.Save(data);
        LoadItems();
        lstItems.SelectedIndex = tgt;
        dragIdx = -1;
    }

    // ---- 编辑 ----
    private void Edit()
    {
        if (curMacro == null) return;
        int idx = lstItems.SelectedIndex;
        if (idx < 0) return;
        var data = MacIO.Load(curMacro);
        if (data == null) return;
        if (idx >= data.Items.Count) return;

        var item = data.Items[idx];
        using var dlg = new MacEdit(item);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            item.Act = dlg.NewAct;
            item.Time = dlg.NewTime;
            item.X = dlg.NewX;
            item.Y = dlg.NewY;
            item.Key = dlg.NewKey;
            item.Delta = dlg.NewDelta;
            MacIO.Save(data);
            LoadItems();
            changed?.Invoke();
        }
    }

    // ---- 其他操作 ----
    private void NewMac()
    {
        string name = $"宏_{DateTime.Now:yyyyMMdd_HHmmss}";
        var data = new MacData { Name = name };
        MacIO.Save(data);
        RefreshFiles();
        if (lstFiles.Items.Contains(name))
            lstFiles.SelectedItem = name;
        changed?.Invoke();
    }

    private void SaveMac()
    {
        if (curMacro == null) { MessageBox.Show("请先选择或新建一个宏"); return; }
        var data = MacIO.Load(curMacro);
        if (data != null)
        {
            MacIO.Save(data);
            MessageBox.Show("已保存");
        }
    }

    private void LoadMac()
    {
        using var ofd = new OpenFileDialog();
        ofd.Title = "加载宏文件";
        ofd.Filter = "宏文件|*.macro|文本文件|*.txt|所有文件|*.*";
        ofd.InitialDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Macros");
        if (ofd.ShowDialog() == DialogResult.OK)
        {
            string fileName = Path.GetFileNameWithoutExtension(ofd.FileName);
            string dest = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Macros", fileName + ".txt");
            if (!File.Exists(dest))
                File.Copy(ofd.FileName, dest);
            RefreshFiles();
            if (lstFiles.Items.Contains(fileName))
                lstFiles.SelectedItem = fileName;
            changed?.Invoke();
        }
    }

    private void DelMac()
    {
        if (curMacro == null) return;
        if (MessageBox.Show($"确定删除宏“{curMacro}”？", "确认", MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            MacIO.Delete(curMacro);
            RefreshFiles();
            changed?.Invoke();
        }
    }

    private void ConvMac()
    {
        if (curMacro == null) return;
        var data = MacIO.Load(curMacro);
        if (data == null) return;
        var list = MacConv.Convert(data);
        if (list.Count == 0) { MessageBox.Show("无可转换事件"); return; }
        cfg.PosList.AddRange(list);
        cfg.Save();
        changed?.Invoke();
        MessageBox.Show($"已转换 {list.Count} 个宏到《坐标管理表》");
    }

    private void DelItem()
    {
        if (curMacro == null) return;
        int idx = lstItems.SelectedIndex;
        if (idx < 0) return;
        var data = MacIO.Load(curMacro);
        if (data == null) return;
        data.Items.RemoveAt(idx);
        MacIO.Save(data);
        LoadItems();
        changed?.Invoke();
    }

    private void ClearMac()
    {
        if (curMacro == null) return;
        if (MessageBox.Show("清空当前宏的所有指令？", "确认", MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            var data = MacIO.Load(curMacro);
            if (data != null)
            {
                data.Items.Clear();
                MacIO.Save(data);
                LoadItems();
                changed?.Invoke();
            }
        }
    }

    public void SelectMacroByName(string name)
    {
        foreach (var item in lstFiles.Items)
        {
            string display = item.ToString();
            if (fileMap.TryGetValue(display, out string realName) && realName == name)
            {
                lstFiles.SelectedItem = display;
                LoadItems();
                return;
            }
        }
        if (lstFiles.Items.Count > 0)
            lstFiles.SelectedIndex = 0;
    }

    // ---- 内部编辑对话框 ----
    private class MacEdit : Form
    {
        public MacAction NewAct { get; private set; }
        public int NewTime { get; private set; }
        public int NewX { get; private set; }
        public int NewY { get; private set; }
        public int NewKey { get; private set; }
        public int NewDelta { get; private set; }

        private ComboBox cboAct;
        private NumericUpDown numTime, numX, numY, numKey, numDelta;
        private Label lblX, lblY, lblKey, lblDelta;

        public MacEdit(MacItem item)
        {
            Text = "编辑宏指令";
            Size = new Size(380, 250);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(240, 244, 248);

            var lay = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                Padding = new Padding(10),
                BackColor = Color.Transparent
            };
            lay.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            lay.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            lay.Controls.Add(new Label { Text = "动作:", AutoSize = true, Font = new Font("微软雅黑", 9F), ForeColor = Color.FromArgb(40, 60, 90) }, 0, 0);
            cboAct = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 120,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 60, 90),
                Font = new Font("微软雅黑", 9F)
            };
            cboAct.Items.AddRange(new object[] { "移动", "左键按下", "左键弹起", "右键按下", "右键弹起", "中键按下", "中键弹起", "滚轮", "按键按下", "按键弹起" });
            cboAct.SelectedIndex = (int)item.Act;
            lay.Controls.Add(cboAct, 1, 0);

            lay.Controls.Add(new Label { Text = "时间(ms):", AutoSize = true, Font = new Font("微软雅黑", 9F), ForeColor = Color.FromArgb(40, 60, 90) }, 0, 1);
            numTime = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 3600000,
                Value = item.Time,
                Width = 100,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 60, 90),
                Font = new Font("微软雅黑", 9F)
            };
            lay.Controls.Add(numTime, 1, 1);

            lblX = new Label { Text = "X:", AutoSize = true, Font = new Font("微软雅黑", 9F), ForeColor = Color.FromArgb(40, 60, 90) };
            lay.Controls.Add(lblX, 0, 2);
            numX = new NumericUpDown
            {
                Minimum = -99999,
                Maximum = 99999,
                Value = item.X,
                Width = 100,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 60, 90),
                Font = new Font("微软雅黑", 9F)
            };
            lay.Controls.Add(numX, 1, 2);

            lblY = new Label { Text = "Y:", AutoSize = true, Font = new Font("微软雅黑", 9F), ForeColor = Color.FromArgb(40, 60, 90) };
            lay.Controls.Add(lblY, 0, 3);
            numY = new NumericUpDown
            {
                Minimum = -99999,
                Maximum = 99999,
                Value = item.Y,
                Width = 100,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 60, 90),
                Font = new Font("微软雅黑", 9F)
            };
            lay.Controls.Add(numY, 1, 3);

            lblKey = new Label { Text = "键码:", AutoSize = true, Font = new Font("微软雅黑", 9F), ForeColor = Color.FromArgb(40, 60, 90) };
            lay.Controls.Add(lblKey, 0, 4);
            numKey = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 255,
                Value = item.Key,
                Width = 100,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 60, 90),
                Font = new Font("微软雅黑", 9F)
            };
            lay.Controls.Add(numKey, 1, 4);

            lblDelta = new Label { Text = "增量:", AutoSize = true, Font = new Font("微软雅黑", 9F), ForeColor = Color.FromArgb(40, 60, 90) };
            lay.Controls.Add(lblDelta, 0, 5);
            numDelta = new NumericUpDown
            {
                Minimum = -1000,
                Maximum = 1000,
                Value = item.Delta,
                Width = 100,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 60, 90),
                Font = new Font("微软雅黑", 9F)
            };
            lay.Controls.Add(numDelta, 1, 5);

            var flowOk = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Bottom };
            var btnOk = new Button
            {
                Text = "确定",
                DialogResult = DialogResult.OK,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(0, 180, 255) },
                BackColor = Color.FromArgb(225, 240, 255),
                ForeColor = Color.FromArgb(0, 80, 180),
                Font = new Font("微软雅黑", 9F)
            };
            var btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(180, 180, 180) },
                BackColor = Color.FromArgb(240, 240, 240),
                ForeColor = Color.FromArgb(80, 80, 80),
                Font = new Font("微软雅黑", 9F)
            };
            flowOk.Controls.Add(btnOk);
            flowOk.Controls.Add(btnCancel);
            lay.Controls.Add(flowOk, 0, 6);
            lay.SetColumnSpan(flowOk, 2);

            Controls.Add(lay);
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            UpdateVis();
            cboAct.SelectedIndexChanged += (s, e) => UpdateVis();

            btnOk.Click += (s, e) =>
            {
                NewAct = (MacAction)cboAct.SelectedIndex;
                NewTime = (int)numTime.Value;
                NewX = (int)numX.Value;
                NewY = (int)numY.Value;
                NewKey = (int)numKey.Value;
                NewDelta = (int)numDelta.Value;
            };
        }

        private void UpdateVis()
        {
            int idx = cboAct.SelectedIndex;
            bool isMove = idx == 0;
            bool isClick = idx >= 1 && idx <= 6;
            bool isWheel = idx == 7;
            bool isKey = idx == 8 || idx == 9;

            lblX.Visible = numX.Visible = isMove || isClick;
            lblY.Visible = numY.Visible = isMove || isClick;
            lblKey.Visible = numKey.Visible = isKey;
            lblDelta.Visible = numDelta.Visible = isWheel;
            PerformLayout();
        }
    }
}