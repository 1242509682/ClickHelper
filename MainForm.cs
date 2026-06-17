using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClickHelper;

public class MainForm : Form
{
    #region UI控件
    private NumericUpDown numInterval;
    private Button btnStart;
    private Button btnStop;
    private Button btnPosList;
    private NumericUpDown numLoopCount;
    private CheckBox chkSimul;
    private Button btnSave;
    private Button btnLoad;
    private ComboBox cboHotKeyF6;
    private ComboBox cboHotKeyAltL;
    private Label lblMousePos;
    private Button btnTimer;          // 定时设置按钮

    private Config cfg;
    private ClickCore core;
    private HotKeyMgr hk;
    private Timer mouseTimer;
    private Timer timerCheck;
    private NotifyIcon trayIcon;
    private ContextMenuStrip trayMenu;
    #endregion

    public MainForm()
    {
        if (!System.IO.Directory.Exists(Config.ScriptDir))
            System.IO.Directory.CreateDirectory(Config.ScriptDir);

        cfg = Config.Load();
        core = new ClickCore(cfg);
        hk = new HotKeyMgr(this.Handle, OnPressed, AddCurrentPos, cfg.HotKeyF6, cfg.HotKeyAltL);

        InitializeUI();
        LoadUIFromConfig();
        InitializeTray();

        this.Icon = LoadTitleIcon();

        this.FormClosing += (s, e) =>
        {
            hk.Unregister();
            core.Dispose();
            mouseTimer?.Stop();
            timerCheck?.Stop();
            trayIcon.Visible = false;
            trayIcon.Dispose();
        };

        this.Load += (s, e) => this.CenterToScreen();
        this.Resize += (s, e) =>
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
                trayIcon.Visible = true;
            }
        };

        // 定时检查
        timerCheck = new Timer { Interval = 1000 };
        timerCheck.Tick += TimerCheck_Tick;
        if (cfg.TimerEnabled) timerCheck.Start();
    }

    private void InitializeTray()
    {
        trayIcon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "点击助手 (双击恢复)",
            Visible = false
        };
        trayIcon.DoubleClick += (s, e) =>
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            trayIcon.Visible = false;
            this.Activate();
        };
        trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("显示主窗口", null, (s, e) =>
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            trayIcon.Visible = false;
            this.Activate();
        });
        trayMenu.Items.Add("-");
        trayMenu.Items.Add("退出程序", null, (s, e) =>
        {
            trayIcon.Visible = false;
            Application.Exit();
        });
        trayIcon.ContextMenuStrip = trayMenu;
    }

    private void InitializeUI()
    {
        this.Text = $"点击助手 {Program.version}  by羽学";
        this.Size = new Size(320, 320);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MinimumSize = new Size(320, 320);

        // 阻止窗口拉伸
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;

        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(6)
        };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < main.RowCount; i++)
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        int row = 0;
        Font font = new Font("微软雅黑", 8.5F);
        Font labelFont = font;
        ToolTip tip = new();

        // 行0：间隔 + 启动/停止
        var flowCtrl = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        numInterval = new NumericUpDown { Minimum = 1, Maximum = 1800000, Value = 50, Width = 60, Font = font };
        flowCtrl.Controls.Add(numInterval);
        tip.SetToolTip(numInterval, "单位毫秒,最短1ms,最大1800000(30分钟)");
        btnStart = new Button { Text = "启动", Width = 55, Font = font };
        btnStop = new Button { Text = "停止", Width = 55, Enabled = false, Font = font };
        flowCtrl.Controls.Add(btnStart);
        flowCtrl.Controls.Add(btnStop);
        main.Controls.Add(new Label { Text = "间隔", AutoSize = true, Font = labelFont, Margin = new Padding(0, 4, 0, 0) }, 0, row);
        main.Controls.Add(flowCtrl, 1, row++);

        // 行1：位置列表
        btnPosList = new Button { Text = "位置列表...", AutoSize = true, Font = font };
        tip.SetToolTip(btnPosList, "可编辑记录过的位置操作模式,如:鼠标左右键、按下弹起、键盘组合键等");
        main.Controls.Add(new Label { Text = "位置", AutoSize = true, Font = labelFont, Margin = new Padding(0, 4, 0, 0) }, 0, row);
        main.Controls.Add(btnPosList, 1, row++);

        // 行2：循环 + 同时执行
        var flowLoop = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        numLoopCount = new NumericUpDown { Minimum = -1, Maximum = 99999, Value = -1, Width = 60, Font = font };
        tip.SetToolTip(numLoopCount, "-1无限\n正整数次数\n0不循环");
        flowLoop.Controls.Add(numLoopCount);
        flowLoop.Controls.Add(new Label { Text = "  ", AutoSize = true });
        chkSimul = new CheckBox { Text = "同时执行", AutoSize = true, Font = font };
        tip.SetToolTip(chkSimul, "勾选后每轮同时执行所有位置");
        flowLoop.Controls.Add(chkSimul);
        main.Controls.Add(new Label { Text = "循环", AutoSize = true, Font = labelFont, Margin = new Padding(0, 4, 0, 0) }, 0, row);
        main.Controls.Add(flowLoop, 1, row++);

        // 行3：热键
        main.Controls.Add(new Label { Text = "热键", AutoSize = true, Font = labelFont, Margin = new Padding(0, 4, 0, 0) }, 0, row);
        cboHotKeyF6 = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70, Font = font };
        tip.SetToolTip(cboHotKeyF6, "按此键启动/停止");
        main.Controls.Add(cboHotKeyF6, 1, row++);

        // 行4：记录
        main.Controls.Add(new Label { Text = "记录", AutoSize = true, Font = labelFont, Margin = new Padding(0, 4, 0, 0) }, 0, row);
        var flowRecord = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        cboHotKeyAltL = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70, Font = font };
        tip.SetToolTip(cboHotKeyAltL, "Alt+此键记录当前鼠标位置到《位置列表》中");
        flowRecord.Controls.Add(cboHotKeyAltL);
        flowRecord.Controls.Add(new Label { Text = "+ Alt", AutoSize = true, Font = labelFont });
        main.Controls.Add(flowRecord, 1, row++);

        // 行5：脚本
        main.Controls.Add(new Label { Text = "脚本", AutoSize = true, Font = labelFont, Margin = new Padding(0, 4, 0, 0) }, 0, row);
        var scrPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
        };
        btnSave = new Button { Text = "保存脚本", AutoSize = true, Font = font };
        tip.SetToolTip(btnSave, "保存当前配置文件,含：热键、执行间隔、位置列表、定制设置等...");
        btnLoad = new Button { Text = "加载脚本", AutoSize = true, Font = font };
        tip.SetToolTip(btnLoad, "从指定路径加载配置文件");
        scrPanel.Controls.Add(btnSave);
        scrPanel.Controls.Add(btnLoad);
        main.Controls.Add(scrPanel, 1, row++);

        // 行6：定时设置按钮
        main.Controls.Add(new Label { Text = "定时", AutoSize = true, Font = labelFont, Margin = new Padding(0, 4, 0, 0) }, 0, row);
        var flowTimerBtn = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
        };
        btnTimer = new Button { Text = "定时设置...", AutoSize = true, Font = font };
        flowTimerBtn.Controls.Add(btnTimer);
        main.Controls.Add(flowTimerBtn, 1, row++);

        // 行7：鼠标位置
        main.Controls.Add(new Label { Text = "鼠标位置", AutoSize = true, Font = labelFont, Margin = new Padding(0, 4, 0, 0) }, 0, row);
        lblMousePos = new Label { Text = "X: 0, Y: 0", AutoSize = true, Font = font, Margin = new Padding(0, 4, 0, 0) };
        main.Controls.Add(lblMousePos, 1, row++);

        this.Controls.Add(main);

        mouseTimer = new Timer { Interval = 50 };
        mouseTimer.Tick += (s, e) =>
        {
            var pt = Control.MousePosition;
            lblMousePos.Text = $"X: {pt.X}, Y: {pt.Y}";
        };
        mouseTimer.Start();

        // 热键下拉列表（与之前相同）
        var hotKeys = new object[] {
            Keys.F1, Keys.F2, Keys.F3, Keys.F4, Keys.F5, Keys.F6, Keys.F7, Keys.F8,
            Keys.F9, Keys.F10, Keys.F11, Keys.F12,
            Keys.D0, Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9,
            Keys.A, Keys.B, Keys.C, Keys.D, Keys.E, Keys.F, Keys.G, Keys.H, Keys.I, Keys.J,
            Keys.K, Keys.L, Keys.M, Keys.N, Keys.O, Keys.P, Keys.Q, Keys.R, Keys.S, Keys.T,
            Keys.U, Keys.V, Keys.W, Keys.X, Keys.Y, Keys.Z
        };
        cboHotKeyF6.Items.AddRange(hotKeys);
        cboHotKeyAltL.Items.AddRange(hotKeys);
        if (!cboHotKeyAltL.Items.Contains(Keys.LButton))
            cboHotKeyAltL.Items.Add(Keys.LButton);
        cboHotKeyAltL.Items.Add(Keys.RButton);
        cboHotKeyAltL.Items.Add(Keys.MButton);

        // ---- 事件绑定 ----
        btnStart.Click += (s, e) => StartCore();
        btnStop.Click += (s, e) => StopCore();
        numInterval.ValueChanged += (s, e) => { cfg.IntervalMs = (int)numInterval.Value; cfg.Save(); core.SetInterval(cfg.IntervalMs); };
        numLoopCount.ValueChanged += (s, e) => { cfg.PosLoopCount = (int)numLoopCount.Value; cfg.Save(); };
        chkSimul.CheckedChanged += (s, e) => { cfg.SimulExec = chkSimul.Checked; cfg.Save(); };
        btnPosList.Click += (s, e) => OpenPosList();
        btnSave.Click += (s, e) => SaveScript();
        btnLoad.Click += (s, e) => LoadScript();

        cboHotKeyF6.SelectedIndexChanged += (s, e) =>
        {
            if (cboHotKeyF6.SelectedItem is Keys key)
            {
                cfg.HotKeyF6 = (int)key;
                cfg.Save();
                hk.UpdateKeys(cfg.HotKeyF6, cfg.HotKeyAltL);
            }
        };
        cboHotKeyAltL.SelectedIndexChanged += (s, e) =>
        {
            if (cboHotKeyAltL.SelectedItem is Keys key)
            {
                cfg.HotKeyAltL = (int)key;
                cfg.Save();
                hk.UpdateKeys(cfg.HotKeyF6, cfg.HotKeyAltL);
            }
        };

        // 定时设置按钮
        btnTimer.Click += (s, e) =>
        {
            using var dlg = new TimerConfigForm(cfg);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                // 配置已保存，更新定时器状态
                if (cfg.TimerEnabled)
                    timerCheck.Start();
                else
                    timerCheck.Stop();
            }
        };
    }

    // 定时检查
    private void TimerCheck_Tick(object? sender, EventArgs e)
    {
        if (!cfg.TimerEnabled)
            return;

        DateTime now = DateTime.Now;
        DateTime start = cfg.TimerStart;
        DateTime end = cfg.TimerEnd;

        bool inRange = now >= start;
        if (end != DateTime.MinValue)
            inRange = inRange && now < end;

        if (inRange)
        {
            if (!core.IsRunning)
            {
                if (cfg.PosLoopCount == 0)
                {
                    cfg.TimerEnabled = false;
                    cfg.Save();
                    timerCheck.Stop();
                    MessageBox.Show("循环次数为0，无法启动定时任务。已自动禁用定时功能。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (cfg.PosList.Count == 0)
                {
                    cfg.TimerEnabled = false;
                    cfg.Save();
                    timerCheck.Stop();
                    MessageBox.Show("位置列表为空，无法启动定时任务,已自动禁用定时功能。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                core.Start();
                UpdateButtons();
            }
        }
        else
        {
            if (core.IsRunning)
            {
                core.Stop();
                UpdateButtons();
            }
        }
    }


    private void OpenPosList()
    {
        using (var dlg = new PosListForm(cfg, core, LoadUIFromConfig))
            dlg.ShowDialog(this);
    }

    private void LoadUIFromConfig()
    {
        numInterval.Value = cfg.IntervalMs;
        numLoopCount.Value = cfg.PosLoopCount;
        chkSimul.Checked = cfg.SimulExec;

        if (cboHotKeyF6.Items.Contains((Keys)cfg.HotKeyF6))
            cboHotKeyF6.SelectedItem = (Keys)cfg.HotKeyF6;
        else
            cboHotKeyF6.SelectedItem = Keys.F6;

        if (cboHotKeyAltL.Items.Contains((Keys)cfg.HotKeyAltL))
            cboHotKeyAltL.SelectedItem = (Keys)cfg.HotKeyAltL;
        else
            cboHotKeyAltL.SelectedItem = Keys.LButton;
    }

    private void StartCore()
    {
        if (cfg.PosLoopCount == 0)
        {
            MessageBox.Show("循环次数为0，不会执行任何操作");
            return;
        }
        if (cfg.PosList.Count == 0)
        {
            MessageBox.Show("位置列表为空，请添加位置");
            return;
        }
        core.Start();
        UpdateButtons();
    }

    private void StopCore()
    {
        core.Stop();
        UpdateButtons();
    }

    private void AddCurrentPos()
    {
        var pt = Control.MousePosition;
        core.AddPos(pt.X, pt.Y, $"P{cfg.PosList.Count + 1}", 0, 0, 0, 0);
    }

    private void SaveScript()
    {
        using var sfd = new SaveFileDialog();
        sfd.Title = "保存脚本";
        sfd.Filter = "JSON文件|*.json";
        sfd.InitialDirectory = Config.ScriptDir;
        if (sfd.ShowDialog() == DialogResult.OK)
        {
            cfg.Save();
            System.IO.File.Copy(Config.Path, sfd.FileName, true);
            MessageBox.Show("脚本已保存");
        }
    }

    private void LoadScript()
    {
        using var ofd = new OpenFileDialog();
        ofd.Title = "加载脚本";
        ofd.Filter = "JSON文件|*.json";
        ofd.InitialDirectory = Config.ScriptDir;
        if (ofd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                string json = System.IO.File.ReadAllText(ofd.FileName);
                var newCfg = Newtonsoft.Json.JsonConvert.DeserializeObject<Config>(json)!;
                cfg.IntervalMs = newCfg.IntervalMs;
                cfg.PosLoopCount = newCfg.PosLoopCount;
                cfg.PosList = newCfg.PosList;
                cfg.SimulExec = newCfg.SimulExec;
                cfg.HotKeyF6 = newCfg.HotKeyF6;
                cfg.HotKeyAltL = newCfg.HotKeyAltL;
                cfg.Save();
                LoadUIFromConfig();
                core = new ClickCore(cfg);
                hk.UpdateKeys(cfg.HotKeyF6, cfg.HotKeyAltL);
                MessageBox.Show("脚本加载成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show("加载失败: " + ex.Message);
            }
        }
    }

    private void OnPressed()
    {
        if (core.IsRunning)
        {
            // 如果定时启用且当前在有效时间内，则认为是定时任务运行中，用户手动停止
            if (cfg.TimerEnabled && timerCheck.Enabled)
            {
                DateTime now = DateTime.Now;
                bool inRange = now >= cfg.TimerStart;
                if (cfg.TimerEnd != DateTime.MinValue)
                    inRange = inRange && now < cfg.TimerEnd;
                if (inRange)
                {
                    // 定时任务运行中，用户手动停止，关闭定时
                    cfg.TimerEnabled = false;
                    cfg.Save();
                    timerCheck.Stop();
                    core.Stop();
                    UpdateButtons();
                    MessageBox.Show("已停止定时任务并关闭定时功能。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            // 否则普通停止
            StopCore();
        }
        else
        {
            // 未运行，按下热键启动
            // 如果定时启用，则先关闭定时
            if (cfg.TimerEnabled && timerCheck.Enabled)
            {
                DateTime now = DateTime.Now;
                bool inRange = now >= cfg.TimerStart;
                if (cfg.TimerEnd != DateTime.MinValue)
                    inRange = inRange && now < cfg.TimerEnd;
                if (inRange)
                {
                    // 定时任务未启动（可能不在启动条件），用户手动启动，关闭定时
                    cfg.TimerEnabled = false;
                    cfg.Save();
                    timerCheck.Stop();
                    MessageBox.Show("定时功能已关闭，改为手动启动。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            // 执行手动启动
            StartCore();
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (hk != null && hk.ProcessHotKey(ref m))
            return;

        base.WndProc(ref m);
    }

    private void UpdateButtons()
    {
        bool isRunning = core.IsRunning;
        btnStart.Enabled = !isRunning;
        btnStop.Enabled = isRunning;
    }

    private Icon LoadTitleIcon()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("ClickHelper.图标.YX.ico");
        if (stream != null)
            return new Icon(stream);
        return SystemIcons.Application;
    }

    private Icon LoadTrayIcon()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("ClickHelper.图标.tray.ico");
        if (stream != null)
            return new Icon(stream);
        return SystemIcons.Application;
    }
}