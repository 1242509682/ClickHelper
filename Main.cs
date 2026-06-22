using System;
using System.Drawing;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace ClickHelper;

/// <info> 主窗口，UI与逻辑协调 </info>
public class Main : Form
{
    // 通用字段（跨模块）
    private Config cfg;
    private Core core;
    private HotKey hk;
    private Timer mTimer, tCheck;
    private NotifyIcon tray;
    private ContextMenuStrip tMenu;
    private bool manual;               // 是否手动启动
    private bool isSnapping;           // 截图互斥
    private bool aboutShow;            // 关于弹窗状态
    private bool togTimer;             // 定时热键防重入
    private DateTime timStart;         // 计时模式起始

    // 宏相关字段
    private MacRec macroRec;
    private MacPlay macroPlay;
    private MacForm? macForm;
    private bool macManual = false;
    private int recHotId = 200;
    private int playHotId = 201;
    private int snapHotId = 202;
    private int timHotId = 203;

    // 状态事件
    public event Action<string>? StatusChanged;
    public bool IsRecording = false;
    public bool IsPlaying = false;

    #region 构造与窗体初始化
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

        InitTitleBar();    // 自定义标题栏
        InitContent();     // 主内容区（控件）
        InitStatusBar();   // 状态栏
        InitTray();        // 托盘
        LoadCfg();         // 加载配置到控件

        this.Icon = LoadIcon("ClickHelper.Icon.YX.ico");
        this.FormClosing += OnFormClosing;
        this.Load += OnFormLoad;
        this.Resize += OnFormResize;
        this.KeyPreview = true;

        // 订阅 OCR 错误
        OcrHelper.OnError += (method, ex) =>
        {
            if (this.InvokeRequired)
                this.Invoke(() => OcrError(method, ex));
            else
                OcrError(method, ex);
        };

        // 定时检查
        tCheck = new Timer { Interval = 1000 };
        tCheck.Tick += TTick;
        if (cfg.TimerEnabled) tCheck.Start();

        RegMacKeys();
        RegSnapKey();
        RegTimerKey();

        // 鼠标位置更新定时器
        mTimer = new Timer { Interval = 100 };
        mTimer.Tick += (s, e) => UpInfo();
        mTimer.Start();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
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
        macroPlay.Dispose();
        OcrHelper.Dispose(); // 卸载OCR引擎
        Application.Exit();
    }

    private void OnFormLoad(object? sender, EventArgs e)
    {
        aboutShow = true;
        if (!cfg.SkipAbout)
        {
            using var about = new AboutForm(cfg);
            about.ShowDialog();
        }
        aboutShow = false;
        this.CenterToScreen();
        UpdateOcrStatus();
        SetStat("空闲");
    }

    private void OnFormResize(object? sender, EventArgs e)
    {
        if (this.WindowState == FormWindowState.Minimized &&
            chkAutoMin.Checked)
            this.Hide();
    }
    #endregion

    #region 自定义标题栏
    private Panel titleBar;
    private Label lblTitle;
    private Button btnAbout, btnMin, btnClose;

    private void InitTitleBar()
    {
        this.FormBorderStyle = FormBorderStyle.None;
        this.Size = new Size(360, 500);
        this.MinimumSize = new Size(360, 500);
        this.MaximizeBox = false;
        this.BackColor = Color.FromArgb(240, 244, 248);

        titleBar = new Panel
        {
            Height = 32,
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(0, 80, 180),
            Margin = new Padding(0)
        };

        lblTitle = new Label
        {
            Text = $"点击助手 {Program.ver}",
            Font = new Font("微软雅黑", 10F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(10, 6)
        };
        titleBar.Controls.Add(lblTitle);

        btnAbout = new Button
        {
            Text = "?",
            Font = new Font("微软雅黑", 9F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Size = new Size(28, 28),
            Location = new Point(this.Width - 90, 2)
        };
        btnAbout.Click += (s, e) =>
        {
            using var about = new AboutForm(cfg);
            about.ShowDialog(this);
        };
        btnAbout.MouseEnter += (s, e) => btnAbout.BackColor = Color.FromArgb(60, 120, 200);
        btnAbout.MouseLeave += (s, e) => btnAbout.BackColor = Color.Transparent;

        btnMin = new Button
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

        btnClose = new Button
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

        titleBar.Controls.Add(btnAbout);
        titleBar.Controls.Add(btnMin);
        titleBar.Controls.Add(btnClose);

        titleBar.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                WinApi.ReleaseCapture();
                WinApi.SendMessage(this.Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
            }
        };
    }
    #endregion

    #region 主内容区 (所有操作控件)
    private NumericUpDown numInt, numLoop;
    private Button btnStart, btnStop, btnList, btnSave, btnLoad, btnTimer;
    private CheckBox chkSim, chkAutoMin;
    private ComboBox cboHot, cboHotAlt, cboSnap;

    private void InitContent()
    {
        var mainContainer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        mainContainer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // 标题栏放入第一行
        mainContainer.Controls.Add(titleBar, 0, 0);

        // 内容面板
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

        // 间隔 + 启动/停止
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

        // 自动最小化
        chkAutoMin = new CheckBox
        {
            Text = "自动最小化",
            AutoSize = true,
            Font = font,
            ForeColor = Color.FromArgb(40, 60, 90)
        };
        main.Controls.Add(new Label { Text = "", AutoSize = true }, 0, row);
        main.Controls.Add(chkAutoMin, 1, row++);

        // 循环 + 批量执行
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

        // 热键
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

        // 记录
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

        // 截图
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

        // 坐标管理器
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

        // 宏管理器
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

        // 定时管理
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

        // 配置管理
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

        mainContainer.Controls.Add(main, 0, 1);
        this.Controls.Add(mainContainer);

        // ---- 事件绑定（该模块内） ----
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

        // 初始化下拉框数据
        var keys = WinApi.GetCommonKeys();
        cboHot.Items.AddRange(keys);
        cboHotAlt.Items.AddRange(keys);
        cboSnap.Items.AddRange(keys);
        cboHotAlt.Items.Add(Keys.LButton);
        cboHotAlt.Items.Add(Keys.RButton);
        cboHotAlt.Items.Add(Keys.MButton);
    }
    #endregion

    #region 状态栏
    private StatusStrip stBar;
    private ToolStripStatusLabel stStat, stCoord, stOcr;

    private void InitStatusBar()
    {
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

    // 更新状态栏文字
    public void SetStat(string text)
    {
        if (stStat != null && !stStat.IsDisposed)
        {
            stStat.Text = text;
            StatusChanged?.Invoke(text);
        }
    }

    // 更新OCR状态指示
    public void UpdateOcrStatus()
    {
        if (stOcr != null && !stOcr.IsDisposed)
        {
            bool loaded = OcrHelper.Engine != null;
            stOcr.Text = loaded ? "ocr已加载" : "ocr未加载";
            stOcr.ForeColor = loaded ? Color.Green : Color.Red;
        }
    }

    private void UpInfo()
    {
        if (stCoord == null || stCoord.IsDisposed) return;
        stCoord.Text = $"X:{Control.MousePosition.X} Y:{Control.MousePosition.Y}";
    }
    #endregion

    #region 托盘
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
            if (aboutShow) return;
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
    #endregion

    #region 配置加载/保存
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

    #region 热键注册
    private void RegMacKeys()
    {
        UnRegMacKeys();
        WinApi.RegisterHotKey(this.Handle, recHotId, 0, (uint)cfg.MacRecHotKey);
        WinApi.RegisterHotKey(this.Handle, playHotId, 0, (uint)cfg.MacPlayHotKey);
    }

    private void UnRegMacKeys()
    {
        WinApi.RegisterHotKey(this.Handle, recHotId);
        WinApi.RegisterHotKey(this.Handle, playHotId);
    }

    private void RegSnapKey()
    {
        WinApi.RegisterHotKey(this.Handle, snapHotId);
        WinApi.RegisterHotKey(this.Handle, snapHotId, WinApi.MOD_ALT, (uint)cfg.SnapHotKey);
    }

    private void RegTimerKey()
    {
        UnTimerKey();
        WinApi.RegisterHotKey(this.Handle, timHotId, 0, (uint)cfg.TimerHotKey);
    }

    private void UnTimerKey()
    {
        WinApi.RegisterHotKey(this.Handle, timHotId);
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
    #endregion

    #region 宏录制 / 播放
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
            OpenMacForm(mac.Name);
        }
        else
        {
            MessageBox.Show("未录制到操作");
        }
        IsRecording = false;
        SetStat("空闲");
    }

    private void OpenMacForm(string? selectMacro = null)
    {
        if (macForm != null && !macForm.IsDisposed)
        {
            macForm.Activate();
            if (!string.IsNullOrEmpty(selectMacro))
                macForm.SelectMacroByName(selectMacro);
            return;
        }
        macForm = new MacForm(cfg, LoadCfg, this);
        macForm.FormClosed += (s, e) => macForm = null;
        if (!string.IsNullOrEmpty(selectMacro))
            macForm.SelectMacroByName(selectMacro);
        macForm.ShowDialog(this);
    }

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

    #region 点击控制
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
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private void UpBtns()
    {
        bool run = core.Running;
        btnStart.Enabled = !run;
        btnStop.Enabled = run;
    }

    // 热键回调（启动/停止切换）
    private void OnHotKey()
    {
        if (core.Running)
            StopC();
        else
            StartC();
    }

    // OCR 错误处理
    private void OcrError(string method, Exception ex)
    {
        if (core.Running)
        {
            core.Stop();
            UpBtns();
            SetStat($"OCR错误已停止: {ex.Message}");
            MessageBox.Show($"OCR识别出错，已停止点击。\n方法：{method}\n错误：{ex.Message}",
                            "OCR错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        else
        {
            SetStat($"OCR错误: {ex.Message}");
        }
    }
    #endregion

    #region 坐标拾取 & 截图
    private void GetPos()
    {
        if (core.Running || IsRecording || IsPlaying || isSnapping) return;
        var pt = Control.MousePosition;
        core.Add(pt.X, pt.Y, $"P{cfg.PosList.Count + 1}", 0, 0, 0, 0);
        new AnimForm(pt).Show();
    }

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
                        data.UseImage = NewEdit.NewUseImg;        // 原 NewUseImageMatch
                        data.ImageTemp = NewEdit.NewImgTmp;       // 原 NewImageTemplate
                        data.Threshold = NewEdit.NewThresh;       // 原 NewThreshold

                        // ---- 文字匹配 ----
                        data.UseTxt = NewEdit.NewUseTxt;
                        data.TxtMatch = NewEdit.NewTxtMatch;
                        data.TxtMode = NewEdit.NewTxtMode;
                        data.TxtThresh = NewEdit.NewTxtThr;       // 原 NewTxtThresh

                        // ---- UIA ----
                        data.UseUIA = NewEdit.NewUseUIA;
                        data.Targets = NewEdit.NewTargets;
                        data.AutoId = NewEdit.NewAutoId;
                        data.UName = NewEdit.NewUName;
                        data.ClassN = NewEdit.NewClassN;
                        data.TextContent = NewEdit.NewTxtVal;     // 原 NewTextContent
                        data.ComboKeys = NewEdit.NewCombo;        // 原 NewComboKeys

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

    #region 定时任务
    private void TTick(object? sender, EventArgs e)
    {
        if (!cfg.TimerEnabled) return;

        if (cfg.TimerMode == 1)
        {
            double elapsed = (DateTime.Now - timStart).TotalSeconds;
            if (elapsed >= cfg.TimerDuration)
            {
                StopTim();
                return;
            }
        }

        bool inRange = false;
        if (cfg.TimerMode == 0)
        {
            DateTime now = DateTime.Now;
            inRange = now >= cfg.TimerStart && (cfg.TimerEnd == DateTime.MinValue || now < cfg.TimerEnd);
        }

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
                    var names = MacIO.GetMacroNames();
                    if (names.Length == 0) return;
                    Array.Sort(names);
                    macroName = names[^1];
                }
                var data = MacIO.Load(macroName);
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

    private void TogTimer()
    {
        if (togTimer) return;
        togTimer = true;
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
            togTimer = false;
        }
    }

    private string GetTimInfo()
    {
        string modeStr = cfg.TimerMode == 0 ? "日期模式" : "计时模式";
        string tgtInfo = "", detInfo = "", durInfo = "";

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
                var names = MacIO.GetMacroNames();
                if (names.Length == 0)
                    return "无宏可播放";
                Array.Sort(names);
                macName = names[^1];
            }
            var data = MacIO.Load(macName);
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
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
    #endregion

    #region 辅助 & 窗口过程
    private void OpenPos()
    {
        using var dlg = new PosForm(cfg, core, LoadCfg);
        dlg.ShowDialog(this);
    }

    private Icon LoadIcon(string name)
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        using var stm = asm.GetManifestResourceStream(name);
        if (stm != null)
            return new Icon(stm);
        return SystemIcons.Application;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312) // WM_HOTKEY
        {
            int id = m.WParam.ToInt32();
            if (id == recHotId)
            {
                ToglRecord();
                return;
            }
            else if (id == playHotId)
            {
                SwitchPlay();
                return;
            }
            else if (id == snapHotId)
            {
                AddSnap();
                return;
            }
            else if (id == timHotId)
            {
                TogTimer();
                return;
            }
        }
        if (hk != null && hk.ProcessHotKey(ref m))
            return;
        base.WndProc(ref m);
    }
    #endregion
}