using System;
using System.Drawing;
using System.Windows.Forms;
using ClickHelper.Macro;
using Newtonsoft.Json;

namespace ClickHelper;

/// <info> 主窗口，UI与逻辑协调 </info>
public class Main : Form
{
    // ---- UI控件 ----
    private NumericUpDown numInt;
    private Button btnStart, btnStop;
    private Button btnList;
    private NumericUpDown numLoop;
    private CheckBox chkSim, chkAutoMin;
    private Button btnSave, btnLoad;
    private ComboBox cboHot, cboHotAlt, cboSnap;
    private Button btnTimer;
    private StatusStrip stBar;
    private ToolStripStatusLabel stStat, stCoord, stOcr;   // 新增 stOcr

    private Config cfg;
    private Core core;
    private HotKey hk;
    private Timer mTimer;
    private Timer tCheck;

    private NotifyIcon tray;
    private ContextMenuStrip tMenu;
    private bool manual;

    // ---- 宏相关 ----
    private MacRec macroRec;
    private MacPlay macroPlay;
    public bool IsRecording = false;
    public bool IsPlaying = false;
    private int macroRecHotId = 200;
    private int macroPlayHotId = 201;
    private int snapHotId = 202;
    private bool macManual = false;
    private MacForm? macForm;   // 持有宏管理器引用

    // ---- 截图互斥 ----
    private bool isSnapping;

    // ---- 关于弹窗 ----
    private bool isAboutShowing = false;
    // ---- 启用定时弹窗 ----
    private bool isTogTimer = false;

    // ---- 定时热键 ----
    private int timerHotId = 203;
    private DateTime timStart;   // 用于计时模式

    // ---- 状态变化事件 ----
    public event Action<string>? StatusChanged;

    public Main()
    {
        if (!System.IO.Directory.Exists(Config.ScriptDir))
            System.IO.Directory.CreateDirectory(Config.ScriptDir);

        cfg = Config.Load();
        core = new Core(cfg);
        hk = new HotKey(this.Handle, OnHotKey, GetPos, cfg.ClickHotKey, cfg.HotKeyAltL);

        macroRec = new MacRec();
        macroPlay = new MacPlay();
        macroRec.SetExcluded(cfg.ClickHotKey, cfg.HotKeyAltL, cfg.MacRecHotKey, cfg.MacPlayHotKey);

        InitUI();
        LoadCfg();
        InitTray();

        this.Icon = LoadIcon("ClickHelper.Icon.YX.ico");

        this.FormClosing += (s, e) =>
        {
            hk.Unregister();
            core.Dispose();
            macroRec.Dispose();
            mTimer?.Stop();
            mTimer?.Dispose();
            tCheck?.Stop();
            tCheck?.Dispose();
            UnTimerKey();
            tray!.Visible = false;
            tray.Dispose();
            macroRec.Dispose();
            macroPlay.Dispose();
            OcrHelper.Dispose(); // 卸载ocr引擎
            Application.Exit();
        };

        this.Load += (s, e) =>
        {
            isAboutShowing = true;
            if (!cfg.SkipAbout)
            {
                using var about = new AboutForm(cfg);
                about.ShowDialog();
            }
            isAboutShowing = false;
            this.CenterToScreen();
            UpdateOcrStatus();
            SetStat("空闲");
        };

        this.Resize += (s, e) =>
        {
            if (this.WindowState == FormWindowState.Minimized && chkAutoMin.Checked)
            {
                this.Hide();
            }
        };

        // 订阅 OCR 错误事件
        OcrHelper.OnError += (method, ex) =>
        {
            // 避免在非UI线程直接操作控件
            if (this.InvokeRequired)
            {
                this.Invoke(() => OcrError(method, ex));
            }
            else
            {
                OcrError(method, ex);
            }
        };

        tCheck = new Timer { Interval = 1000 };
        tCheck.Tick += TTick;
        if (cfg.TimerEnabled) tCheck.Start();

        RegMacKeys();
        RegSnapKey();
        RegTimerKey();
    }

    private void InitUI()
    {
        // 移除系统标题栏，启用自定义
        this.FormBorderStyle = FormBorderStyle.None;
        this.Size = new Size(360, 500);
        this.MinimumSize = new Size(360, 500);
        this.MaximizeBox = false;
        this.BackColor = Color.FromArgb(240, 244, 248);

        // ---- 顶层容器（垂直排列） ----
        var mainContainer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        mainContainer.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 标题栏行
        mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 内容行

        // ---- 自定义标题栏 ----
        var titleBar = new Panel
        {
            Height = 32,
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(0, 80, 180), // 深蓝背景
            Margin = new Padding(0)
        };
        // 标题文字
        var lblTitle = new Label
        {
            Text = $"点击助手 {Program.ver}",
            Font = new Font("微软雅黑", 10F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(10, 6)
        };
        titleBar.Controls.Add(lblTitle);

        // ---- 关于按钮 ----
        var btnAbout = new Button
        {
            Text = "?",
            Font = new Font("微软雅黑", 9F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Size = new Size(28, 28),
            Location = new Point(this.Width - 90, 2)  // 在最小化按钮左侧
        };
        btnAbout.Click += (s, e) =>
        {
            using var about = new AboutForm(cfg);
            about.ShowDialog(this);
        };
        btnAbout.MouseEnter += (s, e) => btnAbout.BackColor = Color.FromArgb(60, 120, 200);
        btnAbout.MouseLeave += (s, e) => btnAbout.BackColor = Color.Transparent;
        titleBar.Controls.Add(btnAbout);

        // 最小化按钮
        var btnMin = new Button
        {
            Text = "─",
            Font = new Font("微软雅黑", 9F),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Size = new Size(28, 28),
            Location = new Point(this.Width - 60, 2)
        };
        btnMin.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
        btnMin.MouseEnter += (s, e) => btnMin.BackColor = Color.FromArgb(60, 120, 200);
        btnMin.MouseLeave += (s, e) => btnMin.BackColor = Color.Transparent;

        // 关闭按钮
        var btnClose = new Button
        {
            Text = "✕",
            Font = new Font("微软雅黑", 9F),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Size = new Size(28, 28),
            Location = new Point(this.Width - 30, 2)
        };
        btnClose.Click += (s, e) => this.Close();
        btnClose.MouseEnter += (s, e) => { btnClose.BackColor = Color.Red; btnClose.ForeColor = Color.White; };
        btnClose.MouseLeave += (s, e) => { btnClose.BackColor = Color.Transparent; btnClose.ForeColor = Color.White; };

        titleBar.Controls.Add(btnMin);
        titleBar.Controls.Add(btnClose);
        // 拖拽移动
        titleBar.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                WinApi.ReleaseCapture();
                WinApi.SendMessage(this.Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
            }
        };

        mainContainer.Controls.Add(titleBar, 0, 0);

        // ---- 内容区（原有布局，直接移入） ----
        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 10,
            Padding = new Padding(12, 8, 12, 8),
            BackColor = Color.Transparent
        };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < main.RowCount; i++)
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Font font = new Font("微软雅黑", 8.5F);
        Font lblFont = new Font("微软雅黑", 8.5F, FontStyle.Bold);
        ToolTip tip = new ToolTip();
        int row = 0;

        // ---- 间隔 + 启动/停止 ----
        var flowCtrl = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        numInt = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 1800000,
            Value = 50,
            Width = 65,
            Font = font,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        tip.SetToolTip(numInt, "单位毫秒,1~1800000");
        flowCtrl.Controls.Add(numInt);

        btnStart = new Button
        {
            Text = "启动",
            Width = 55,
            AutoSize = true,
            Font = font,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(0, 180, 255) },
            BackColor = Color.FromArgb(225, 240, 255),
            ForeColor = Color.FromArgb(0, 80, 180)
        };
        btnStop = new Button
        {
            Text = "停止",
            Width = 55,
            AutoSize = true,
            Enabled = false,
            Font = font,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(255, 150, 100) },
            BackColor = Color.FromArgb(255, 235, 225),
            ForeColor = Color.FromArgb(180, 60, 0)
        };
        flowCtrl.Controls.Add(btnStart);
        flowCtrl.Controls.Add(btnStop);
        main.Controls.Add(new Label { Text = "间隔", AutoSize = true, Font = lblFont, ForeColor = Color.FromArgb(40, 60, 90), Margin = new Padding(0, 6, 0, 0) }, 0, row);
        main.Controls.Add(flowCtrl, 1, row++);

        // ---- 自动最小化 ----
        chkAutoMin = new CheckBox
        {
            Text = "自动最小化",
            AutoSize = true,
            Font = font,
            ForeColor = Color.FromArgb(40, 60, 90)
        };
        main.Controls.Add(new Label { Text = "", AutoSize = true }, 0, row);
        main.Controls.Add(chkAutoMin, 1, row++);

        // ---- 循环 + 同时执行 ----
        var flowLoop = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        numLoop = new NumericUpDown
        {
            Minimum = -1,
            Maximum = 99999,
            Value = -1,
            Width = 60,
            Font = font,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        tip.SetToolTip(numLoop, "-1无限，0不循环");
        flowLoop.Controls.Add(numLoop);
        flowLoop.Controls.Add(new Label { Text = "  ", AutoSize = true });
        chkSim = new CheckBox
        {
            Text = "批量执行",
            AutoSize = true,
            Font = font,
            ForeColor = Color.FromArgb(40, 60, 90)
        };
        flowLoop.Controls.Add(chkSim);
        main.Controls.Add(new Label { Text = "循环", AutoSize = true, Font = lblFont, ForeColor = Color.FromArgb(40, 60, 90), Margin = new Padding(0, 6, 0, 0) }, 0, row);
        main.Controls.Add(flowLoop, 1, row++);

        // ---- 热键 ----
        main.Controls.Add(new Label { Text = "热键", AutoSize = true, Font = lblFont, ForeColor = Color.FromArgb(40, 60, 90), Margin = new Padding(0, 6, 0, 0) }, 0, row);
        cboHot = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 70,
            Font = font,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        main.Controls.Add(cboHot, 1, row++);

        // ---- 记录 ----
        main.Controls.Add(new Label { Text = "记录", AutoSize = true, Font = lblFont, ForeColor = Color.FromArgb(40, 60, 90), Margin = new Padding(0, 6, 0, 0) }, 0, row);
        var flowRec = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        cboHotAlt = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 70,
            Font = font,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        flowRec.Controls.Add(cboHotAlt);
        flowRec.Controls.Add(new Label { Text = "+ Alt", AutoSize = true, Font = font, ForeColor = Color.DimGray });
        main.Controls.Add(flowRec, 1, row++);

        // ---- 截图 ----
        main.Controls.Add(new Label { Text = "截图", AutoSize = true, Font = lblFont, ForeColor = Color.FromArgb(40, 60, 90), Margin = new Padding(0, 6, 0, 0) }, 0, row);
        var flowSnap = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        cboSnap = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 70,
            Font = font,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        flowSnap.Controls.Add(cboSnap);
        flowSnap.Controls.Add(new Label { Text = "+ Alt", AutoSize = true, Font = font, ForeColor = Color.DimGray });
        main.Controls.Add(flowSnap, 1, row++);

        // ---- 坐标管理器 ----
        btnList = new Button
        {
            Text = "坐标管理",
            AutoSize = true,
            Font = font,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(0, 180, 255) },
            BackColor = Color.FromArgb(225, 240, 255),
            ForeColor = Color.FromArgb(0, 80, 180)
        };
        main.Controls.Add(new Label { Text = "位置", AutoSize = true, Font = lblFont, ForeColor = Color.FromArgb(40, 60, 90), Margin = new Padding(0, 6, 0, 0) }, 0, row);
        main.Controls.Add(btnList, 1, row++);

        // ---- 宏管理器 ----
        main.Controls.Add(new Label { Text = "录制", AutoSize = true, Font = lblFont, ForeColor = Color.FromArgb(40, 60, 90), Margin = new Padding(0, 6, 0, 0) }, 0, row);
        var flowMac = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        var btnMacL = new Button
        {
            Text = "宏管理器",
            AutoSize = true,
            Font = font,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(255, 180, 0) },
            BackColor = Color.FromArgb(255, 245, 225),
            ForeColor = Color.FromArgb(180, 100, 0)
        };
        flowMac.Controls.Add(btnMacL);
        main.Controls.Add(flowMac, 1, row++);

        // ---- 定时管理 ----
        main.Controls.Add(new Label { Text = "定时", AutoSize = true, Font = lblFont, ForeColor = Color.FromArgb(40, 60, 90), Margin = new Padding(0, 6, 0, 0) }, 0, row);
        var flowTimer = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        btnTimer = new Button
        {
            Text = "定时管理",
            AutoSize = true,
            Font = font,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(200, 150, 255) },
            BackColor = Color.FromArgb(240, 230, 255),
            ForeColor = Color.FromArgb(100, 50, 160)
        };
        flowTimer.Controls.Add(btnTimer);
        main.Controls.Add(flowTimer, 1, row++);

        // ---- 配置管理 ----
        main.Controls.Add(new Label { Text = "配置", AutoSize = true, Font = lblFont, ForeColor = Color.FromArgb(40, 60, 90), Margin = new Padding(0, 6, 0, 0) }, 0, row);
        var scrPan = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        btnSave = new Button
        {
            Text = "保存配置",
            AutoSize = true,
            Font = font,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(100, 200, 100) },
            BackColor = Color.FromArgb(230, 245, 230),
            ForeColor = Color.FromArgb(0, 120, 0)
        };
        btnLoad = new Button
        {
            Text = "加载配置",
            AutoSize = true,
            Font = font,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(100, 200, 200) },
            BackColor = Color.FromArgb(225, 245, 245),
            ForeColor = Color.FromArgb(0, 100, 120)
        };
        scrPan.Controls.Add(btnSave);
        scrPan.Controls.Add(btnLoad);
        main.Controls.Add(scrPan, 1, row++);

        // 将内容放入容器
        mainContainer.Controls.Add(main, 0, 1);
        this.Controls.Add(mainContainer);

        // ---- 状态栏（原样） ----
        mTimer = new Timer { Interval = 100 };
        mTimer.Tick += (s, e) => UpInfo();
        mTimer.Start();

        // 热键下拉
        var keys = WinApi.GetCommonKeys();
        cboHot.Items.AddRange(keys);
        cboHotAlt.Items.AddRange(keys);
        cboSnap.Items.AddRange(keys);
        cboHotAlt.Items.Add(Keys.LButton);
        cboHotAlt.Items.Add(Keys.RButton);
        cboHotAlt.Items.Add(Keys.MButton);

        // ---- 事件绑定（保持不变） ----
        btnStart.Click += (s, e) => StartC();
        btnStop.Click += (s, e) => StopC();
        numInt.ValueChanged += (s, e) => { cfg.IntervalMs = (int)numInt.Value; cfg.Save(); core.SetInt(cfg.IntervalMs); };
        numLoop.ValueChanged += (s, e) => { cfg.PosLoopCount = (int)numLoop.Value; cfg.Save(); };
        chkSim.CheckedChanged += (s, e) => { cfg.SimulExec = chkSim.Checked; cfg.Save(); };
        btnList.Click += (s, e) => OpenPos();
        btnSave.Click += (s, e) => SaveSc();
        btnLoad.Click += (s, e) => LoadSc();
        cboHot.SelectedIndexChanged += (s, e) => { if (cboHot.SelectedItem is Keys k) { cfg.ClickHotKey = (int)k; cfg.Save(); hk.UpdateKeys(cfg.ClickHotKey, cfg.HotKeyAltL); UpMacExcl(); } };
        cboHotAlt.SelectedIndexChanged += (s, e) => { if (cboHotAlt.SelectedItem is Keys k) { cfg.HotKeyAltL = (int)k; cfg.Save(); hk.UpdateKeys(cfg.ClickHotKey, cfg.HotKeyAltL); UpMacExcl(); } };
        cboSnap.SelectedIndexChanged += (s, e) => { if (cboSnap.SelectedItem is Keys k) { cfg.SnapHotKey = (int)k; cfg.Save(); RegSnapKey(); } };
        btnTimer.Click += (s, e) =>
        {
            using var dlg = new TimerForm(cfg);
            dlg.ShowDialog(this);
            RegTimerKey();
        };
        btnMacL.Click += (s, e) => OpenMacForm();

        // 状态栏
        stBar = new StatusStrip
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            BackColor = Color.FromArgb(230, 235, 240)
        };
        stOcr = new ToolStripStatusLabel(OcrHelper.Engine != null ? "ocr已加载" : "ocr未加载")
        {
            Width = 60,
            Spring = false,
            AutoSize = true,
            ForeColor = OcrHelper.Engine != null ? Color.Green : Color.Red,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        stBar.Items.Add(stOcr);
        stStat = new ToolStripStatusLabel("空闲")
        {
            Spring = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Width = 60,
            AutoSize = true,
            ForeColor = Color.FromArgb(40, 60, 90)
        };
        stBar.Items.Add(stStat);
        stCoord = new ToolStripStatusLabel("X:0 Y:0")
        {
            Spring = false,
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = true,
            ForeColor = Color.DimGray
        };
        stBar.Items.Add(stCoord);
        this.Controls.Add(stBar);
    }

    private void InitTray()
    {
        tray = new NotifyIcon
        {
            Icon = LoadIcon("ClickHelper.Icon.tray.ico"),
            Text = "点击助手 (双击恢复)",
            Visible = true
        };

        tray.DoubleClick += (s, e) =>
        {
            if (isAboutShowing) return;
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();
        };

        tMenu = new ContextMenuStrip();
        tMenu.Items.Add("显示主窗口", null, (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; this.Activate(); });
        tMenu.Items.Add("-");
        tMenu.Items.Add("退出程序", null, (s, e) => { tray.Visible = false; Application.Exit(); });
        tray.ContextMenuStrip = tMenu;
    }

    private void LoadCfg()
    {
        numInt.Value = cfg.IntervalMs;
        numLoop.Value = cfg.PosLoopCount;
        chkSim.Checked = cfg.SimulExec;

        if (cboHot.Items.Contains((Keys)cfg.ClickHotKey))
            cboHot.SelectedItem = (Keys)cfg.ClickHotKey;
        else
            cboHot.SelectedItem = Keys.F6;

        if (cboHotAlt.Items.Contains((Keys)cfg.HotKeyAltL))
            cboHotAlt.SelectedItem = (Keys)cfg.HotKeyAltL;
        else
            cboHotAlt.SelectedItem = Keys.LButton;

        if (cboSnap.Items.Contains((Keys)cfg.SnapHotKey))
            cboSnap.SelectedItem = (Keys)cfg.SnapHotKey;
        else
            cboSnap.SelectedItem = Keys.S;

        UpMacExcl();
    }

    // ---- 热键注册/注销 ----
    private void RegMacKeys()
    {
        UnRegMacKeys();
        WinApi.RegisterHotKey(this.Handle, macroRecHotId, 0, (uint)cfg.MacRecHotKey);
        WinApi.RegisterHotKey(this.Handle, macroPlayHotId, 0, (uint)cfg.MacPlayHotKey);
    }

    private void UnRegMacKeys()
    {
        WinApi.RegisterHotKey(this.Handle, macroRecHotId);
        WinApi.RegisterHotKey(this.Handle, macroPlayHotId);
    }

    private void RegSnapKey()
    {
        WinApi.RegisterHotKey(this.Handle, snapHotId);
        WinApi.RegisterHotKey(this.Handle, snapHotId, WinApi.MOD_ALT, (uint)cfg.SnapHotKey);
    }

    private void RegTimerKey()
    {
        UnTimerKey();
        WinApi.RegisterHotKey(this.Handle, timerHotId, 0, (uint)cfg.TimerHotKey);
    }

    private void UnTimerKey()
    {
        WinApi.RegisterHotKey(this.Handle, timerHotId);
    }

    public void UpMacKeys(int newRec, int newPlay)
    {
        cfg.MacRecHotKey = newRec;
        cfg.MacPlayHotKey = newPlay;
        cfg.Save();
        RegMacKeys();
        UpMacExcl();
    }

    private void UpMacExcl()
    {
        if (macroRec != null)
            macroRec.SetExcluded(cfg.ClickHotKey, cfg.HotKeyAltL, cfg.MacRecHotKey, cfg.MacPlayHotKey);
    }

    // ---- 宏控制 ----
    public void ToglRecord()
    {
        if (IsPlaying || isSnapping) return;

        if (IsRecording)
            StopRecord();
        else
            StartRecord();
    }

    private void StartRecord()
    {
        if (IsRecording || core.Running || IsPlaying || isSnapping) return;

        macroRec.SetExcluded(cfg.ClickHotKey, cfg.HotKeyAltL, cfg.MacRecHotKey, cfg.MacPlayHotKey);
        macroRec.StartRec();
        IsRecording = true;
        SetStat("录制中...");
        if (chkAutoMin.Checked)
            this.WindowState = FormWindowState.Minimized;
    }

    private void StopRecord()
    {
        if (!IsRecording) return;
        macroRec.StopRec();
        var items = macroRec.GetItems();
        if (items.Count > 0)
        {
            var mac = new MacData { Name = $"宏_{DateTime.Now:yyyyMMdd_HHmmss}" };
            mac.Items.AddRange(items);
            MacIO.Save(mac);
            // 复用或创建宏管理器，并选中新宏
            OpenMacForm(mac.Name);
        }
        else
        {
            MessageBox.Show("未录制到操作");
        }
        IsRecording = false;
        SetStat("空闲");
    }

    // 打开宏管理器的统一方法
    private void OpenMacForm(string? selectMacro = null)
    {
        // 如果窗体已存在且未销毁，激活它并刷新
        if (macForm != null && !macForm.IsDisposed)
        {
            macForm.Activate();
            if (!string.IsNullOrEmpty(selectMacro))
                macForm.SelectMacroByName(selectMacro);
            return;
        }

        // 否则创建新窗体
        macForm = new MacForm(cfg, LoadCfg, this);
        macForm.FormClosed += (s, e) => macForm = null;
        if (!string.IsNullOrEmpty(selectMacro))
            macForm.SelectMacroByName(selectMacro);
        macForm.ShowDialog(this);
    }

    #region 宏播放状态管理
    public void SwitchPlay()
    {
        if (IsRecording || isSnapping) return;

        if (IsPlaying)
            StopPlay();
        else
            StartPlay();
    }

    private void StartPlay()
    {
        if (core.Running || IsRecording || isSnapping) return;

        if (cfg.PosLoopCount == 0)
        {
            MessageBox.Show("循环次数为0，不播放宏", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var names = MacIO.GetMacroNames();
        if (names.Length == 0) { MessageBox.Show("无宏可播放"); return; }
        Array.Sort(names);
        var last = names[^1];
        var data = MacIO.Load(last);
        if (data == null || data.Items.Count == 0) { MessageBox.Show("宏为空"); return; }
        if (IsPlaying) return;

        macManual = true;
        IsPlaying = true;
        SetStat("播放中...");
        if (chkAutoMin.Checked)
            this.WindowState = FormWindowState.Minimized;

        macroPlay.Play(data, loopCount: cfg.PosLoopCount, speed: 1.0, done: () =>
        {
            this.Invoke((Action)(() =>
            {
                IsPlaying = false;
                SetStat(cfg.PosLoopCount == -1 ? "已停止" : "播放完成");
            }));
        });
    }

    private void StopPlay()
    {
        if (!IsPlaying) return;
        macroPlay.Stop();
        IsPlaying = false;
        macManual = true;
        SetStat("已停止");
    }
    #endregion

    // ---- 点击控制 ----
    private void StartC()
    {
        if (IsRecording || IsPlaying || isSnapping) return;

        if (cfg.PosLoopCount == 0)
        {
            MessageBox.Show("循环次数为0，不会执行");
            return;
        }
        if (cfg.PosList.Count == 0)
        {
            MessageBox.Show("坐标管理列表为空");
            return;
        }
        manual = true;
        core.Start();
        UpBtns();
        SetStat("运行中");
        if (chkAutoMin.Checked)
            this.WindowState = FormWindowState.Minimized;
    }

    private void StopC()
    {
        manual = true;
        core.Stop();
        UpBtns();
        SetStat("空闲");

        // ★ 停止时强制回收内存（释放未使用的托管对象）
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect(); // 回收可能提升到下一代的对象
    }

    private void OcrError(string method, Exception ex)
    {
        if (core.Running)
        {
            core.Stop();
            UpBtns();
            SetStat($"OCR错误已停止: {ex.Message}");

            // 显示错误提示
            MessageBox.Show($"OCR识别出错，已停止点击。\n方法：{method}\n错误：{ex.Message}",
                            "OCR错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        else
        {
            SetStat($"OCR错误: {ex.Message}");
        }
    }

    #region 获取鼠标坐标
    private void GetPos()
    {
        if (core.Running || IsRecording || IsPlaying || isSnapping) return;

        var pt = Control.MousePosition;
        core.Add(pt.X, pt.Y, $"P{cfg.PosList.Count + 1}", 0, 0, 0, 0);

        // 显示水波动画（仅记录坐标时）
        new AnimForm(pt).Show();
    }
    #endregion

    #region 添加截图
    private void AddSnap()
    {
        if (core.Running || IsRecording || IsPlaying || isSnapping) return;

        isSnapping = true;
        try
        {
            bool was = false;
            if (chkAutoMin.Checked)
            {
                was = true;
                this.WindowState = FormWindowState.Minimized;
                this.Hide();
            }

            using var snap = new SnapForm();
            if (snap.ShowDialog() == DialogResult.OK && snap.GetImage != null)
            {
                byte[] bytes = ImgMatch.Bmp2Bytes(snap.GetImage);

                Config.PosData data = new()
                {
                    X = 0,
                    Y = 0,
                    Desc = $"截图_{DateTime.Now:HHmmss}",
                    ActType = 0,
                    ActKey = 0,
                    OpMode = 0,
                    WaitMs = 0,
                    UseImage = true,
                    ImageTemp = bytes,
                };

                this.BeginInvoke(() =>
                {
                    using var NewEdit = new PosEdit(data);
                    if (NewEdit.ShowDialog() == DialogResult.OK)
                    {
                        data.X = NewEdit.NewX;
                        data.Y = NewEdit.NewY;
                        data.Desc = NewEdit.NewDesc;
                        data.ActType = NewEdit.NewActType;
                        data.ActKey = NewEdit.NewActKey;
                        data.ModKey = NewEdit.NewModKey;
                        data.OpMode = NewEdit.NewOpMode;
                        data.WaitMs = NewEdit.NewWaitMs;

                        // ---- 图像匹配 ----
                        data.UseImage = NewEdit.NewUseImageMatch;
                        data.ImageTemp = NewEdit.NewImageTemplate;
                        data.Threshold = NewEdit.NewThreshold;

                        // ★★★ 新增：文字匹配属性 ★★★
                        data.UseTxt = NewEdit.NewUseTxt;
                        data.TxtMatch = NewEdit.NewTxtMatch;
                        data.TxtMode = NewEdit.NewTxtMode;
                        data.TxtThresh = NewEdit.NewTxtThresh;

                        // ★★★ 新增：UIA 属性 ★★★
                        data.UseUIA = NewEdit.NewUseUIA;
                        data.UIAProc = NewEdit.NewUIAProc;

                        // ★★★ 新增：文本输入 / 组合键内容 ★★★
                        data.TextContent = NewEdit.NewTextContent;
                        data.ComboKeys = NewEdit.NewComboKeys;
                        cfg.PosList.Add(data);
                        cfg.Save();
                    }
                });
            }

            if (was)
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.Activate();
            }
        }
        finally
        {
            isSnapping = false;
        }
    }
    #endregion

    #region 脚本保存加载（配置文件额外备份）
    private void SaveSc()
    {
        using var sfd = new SaveFileDialog();
        sfd.Title = "保存配置";
        sfd.Filter = "JSON文件|*.json";
        sfd.InitialDirectory = Config.ScriptDir;
        if (sfd.ShowDialog() == DialogResult.OK)
        {
            cfg.Save();
            System.IO.File.Copy(Config.Path, sfd.FileName, true);
            MessageBox.Show("配置已保存");
        }
    }

    private void LoadSc()
    {
        using var ofd = new OpenFileDialog();
        ofd.Title = "加载配置";
        ofd.Filter = "JSON文件|*.json";
        ofd.InitialDirectory = Config.ScriptDir;
        if (ofd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                string json = System.IO.File.ReadAllText(ofd.FileName);
                var newCfg = JsonConvert.DeserializeObject<Config>(json)!;
                cfg.IntervalMs = newCfg.IntervalMs;
                cfg.PosLoopCount = newCfg.PosLoopCount;
                cfg.PosList = newCfg.PosList;
                cfg.SimulExec = newCfg.SimulExec;
                cfg.ClickHotKey = newCfg.ClickHotKey;
                cfg.HotKeyAltL = newCfg.HotKeyAltL;
                cfg.MacRecHotKey = newCfg.MacRecHotKey;
                cfg.MacPlayHotKey = newCfg.MacPlayHotKey;
                cfg.SnapHotKey = newCfg.SnapHotKey;
                cfg.TimerEnabled = newCfg.TimerEnabled;
                cfg.TimerMode = newCfg.TimerMode;
                cfg.TimerStart = newCfg.TimerStart;
                cfg.TimerEnd = newCfg.TimerEnd;
                cfg.TimerDuration = newCfg.TimerDuration;
                cfg.TimeType = newCfg.TimeType;
                cfg.MacroName = newCfg.MacroName;
                cfg.TimerHotKey = newCfg.TimerHotKey;
                cfg.Save();
                LoadCfg();
                core = new Core(cfg);
                hk.UpdateKeys(cfg.ClickHotKey, cfg.HotKeyAltL);
                RegMacKeys();
                RegSnapKey();
                RegTimerKey();
                UpMacExcl();
                if (cfg.TimerEnabled) tCheck.Start(); else tCheck.Stop();
                MessageBox.Show("配置加载成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show("加载失败: " + ex.Message);
                Logger.Log(ex, "LoadSc");
            }
        }
    }
    #endregion

    // ---- 热键回调 ----
    private void OnHotKey()
    {
        if (core.Running)
            StopC();
        else
            StartC();
    }

    // ---- 定时任务 ----
    private void TTick(object? sender, EventArgs e)
    {
        if (!cfg.TimerEnabled) return;

        // 计时模式超时检查
        if (cfg.TimerMode == 1)
        {
            double elapsed = (DateTime.Now - timStart).TotalSeconds;
            if (elapsed >= cfg.TimerDuration)
            {
                StopTim();
                return;
            }
        }

        // 日期模式区间检查
        bool inRange = false;
        if (cfg.TimerMode == 0)
        {
            DateTime now = DateTime.Now;
            inRange = now >= cfg.TimerStart && (cfg.TimerEnd == DateTime.MinValue || now < cfg.TimerEnd);
        }

        // 日期模式且不在区间内：停止定时任务并更新状态
        if (cfg.TimerMode == 0 && !inRange)
        {
            if (cfg.TimeType == 0)
            {
                if (core.Running && !manual)
                {
                    core.Stop();
                    UpBtns();
                    SetStat("空闲");
                }
                else if (!core.Running && !manual)
                {
                    SetStat("等待定时...");
                }
                else if (core.Running && manual)
                {
                    SetStat("手动运行中");
                }
            }
            else
            {
                if (IsPlaying && !macManual)
                {
                    StopPlay();
                    SetStat("定时结束");
                }
                else if (!IsPlaying && !macManual)
                {
                    SetStat("等待定时...");
                }
                else if (IsPlaying && macManual)
                {
                    SetStat("手动播放中");
                }
            }
            return;
        }

        // 在有效区间内（日期模式在范围内，或计时模式始终有效）
        if (cfg.TimeType == 0)
        {
            if (!core.Running && !manual)
            {
                if (isSnapping || IsRecording || IsPlaying) return;
                if (cfg.PosLoopCount == 0 || cfg.PosList.Count == 0)
                {
                    SetStat("坐标管理表为空或循环0，跳过");
                    return;
                }
                core.Start();
                manual = false;
                UpBtns();
                SetStat("定时运行中");
                if (chkAutoMin.Checked) this.WindowState = FormWindowState.Minimized;
            }
            else if (core.Running && manual)
            {
                SetStat("手动运行中");
            }
        }
        else
        {
            if (!IsPlaying && !core.Running && !isSnapping && !IsRecording)
            {
                if (cfg.PosLoopCount == 0)
                {
                    SetStat("循环0，不播放宏");
                    return;
                }
                string macroName = cfg.MacroName;
                if (string.IsNullOrEmpty(macroName))
                {
                    var names = Macro.MacIO.GetMacroNames();
                    if (names.Length == 0) return;
                    Array.Sort(names);
                    macroName = names[^1];
                }
                var data = Macro.MacIO.Load(macroName);
                if (data == null || data.Items.Count == 0) return;

                macManual = false;
                IsPlaying = true;
                SetStat("定时播放中...");
                if (chkAutoMin.Checked) this.WindowState = FormWindowState.Minimized;

                macroPlay.Play(data, loopCount: cfg.PosLoopCount, speed: 1.0, done: () =>
                {
                    this.Invoke(() =>
                    {
                        IsPlaying = false;
                        SetStat(cfg.PosLoopCount == -1 ? "已停止" : "播放完成");
                    });
                });
            }
            else if (IsPlaying && macManual)
            {
                SetStat("手动播放中");
            }
        }
    }

    // ---- 定时热键切换 ----
    private void TogTimer()
    {
        if (isTogTimer) return;
        isTogTimer = true;
        try
        {
            if (cfg.TimerEnabled)
            {
                StopTim();
            }
            else
            {
                string info = GetTimInfo();
                if (info.StartsWith("无") || info.StartsWith("宏为空"))
                {
                    MessageBox.Show(info, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string hotKey = ((Keys)cfg.TimerHotKey).ToString();
                string msg = $"{info}\n\n按 {hotKey} 可停止定时。";
                if (MessageBox.Show(msg, "启用定时", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    cfg.TimerEnabled = true;
                    cfg.Save();
                    timStart = DateTime.Now;
                    tCheck.Start();
                    SetStat("定时已启用");
                }
            }
        }
        finally
        {
            isTogTimer = false;
        }
    }

    // 获取定时任务详情
    private string GetTimInfo()
    {
        string modeStr = cfg.TimerMode == 0 ? "日期模式" : "计时模式";
        string tgtInfo = "";
        string detInfo = "";
        string durInfo = "";

        if (cfg.TimeType == 0)
        {
            tgtInfo = "执行目标：坐标表";
            int cnt = cfg.PosList.Count;
            int wait = 0;
            foreach (var p in cfg.PosList)
                wait += p.WaitMs;
            detInfo = $"指令数：{cnt} 条";
            if (wait > 0)
                detInfo += $"\n总延迟：{wait} ms";
        }
        else
        {
            tgtInfo = "执行目标：宏播放";
            string macName = cfg.MacroName;
            if (string.IsNullOrEmpty(macName))
            {
                var names = Macro.MacIO.GetMacroNames();
                if (names.Length == 0)
                    return "无宏可播放";
                Array.Sort(names);
                macName = names[^1];
            }
            var data = Macro.MacIO.Load(macName);
            if (data == null || data.Items.Count == 0)
                return "宏为空或不存在";

            detInfo = $"指令数：{data.Items.Count} 条\n总时长：{data.Total} ms";
        }

        if (cfg.TimerMode == 1)
        {
            int d = cfg.TimerDuration;
            int h = d / 3600;
            int m = (d % 3600) / 60;
            int s = d % 60;
            durInfo = $"运行时长：{h}时{m}分{s}秒";
        }

        return $"定时模式：{modeStr}\n{tgtInfo}\n{detInfo}\n{durInfo}".TrimEnd('\n');
    }

    // ---- 立即停止定时任务 ----
    private void StopTim()
    {
        if (!cfg.TimerEnabled) return;

        cfg.TimerEnabled = false;
        cfg.Save();
        tCheck.Stop();

        if (core.Running && !manual)
        {
            core.Stop();
            UpBtns();
        }

        if (IsPlaying && !macManual)
        {
            StopPlay();
        }

        SetStat("定时已禁用");

        // ★ 停止时强制回收内存（释放未使用的托管对象）
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect(); // 回收可能提升到下一代的对象
    }

    // ---- 其他辅助 ----
    private void OpenPos()
    {
        using var dlg = new PosForm(cfg, core, LoadCfg);
        dlg.ShowDialog(this);
    }

    #region 更新主窗口状态栏
    public void SetStat(string text)
    {
        if (stStat != null && !stStat.IsDisposed)
        {
            stStat.Text = text;
            StatusChanged?.Invoke(text);
        }
    }

    public void UpdateOcrStatus()
    {
        if (stOcr != null && !stOcr.IsDisposed)
        {
            bool loaded = OcrHelper.Engine != null;
            stOcr.Text = loaded ? "ocr已加载" : "ocr未加载";
            stOcr.ForeColor = loaded ? Color.Green : Color.Red;
        }
    }
    #endregion

    private void UpInfo()
    {
        if (stCoord == null || stCoord.IsDisposed) return;
        stCoord.Text = $"X:{Control.MousePosition.X} Y:{Control.MousePosition.Y}";
    }

    private void UpBtns()
    {
        bool run = core.Running;
        btnStart.Enabled = !run;
        btnStop.Enabled = run;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312) // WM_HOTKEY
        {
            int id = m.WParam.ToInt32();
            if (id == macroRecHotId)
            {
                ToglRecord();
                return;
            }
            else if (id == macroPlayHotId)
            {
                SwitchPlay();
                return;
            }
            else if (id == snapHotId)
            {
                AddSnap();
                return;
            }
            else if (id == timerHotId)
            {
                TogTimer();
                return;
            }
        }
        if (hk != null && hk.ProcessHotKey(ref m))
            return;
        base.WndProc(ref m);
    }

    private Icon LoadIcon(string name)
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        using var stm = asm.GetManifestResourceStream(name);
        if (stm != null)
            return new Icon(stm);
        return SystemIcons.Application;
    }
}