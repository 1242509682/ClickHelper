using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using static ClickHelper.Config;
using static ClickHelper.Program;

namespace ClickHelper;

public class Main : Form
{
    // ---- 常量热键ID ----
    private const int HOTKEY_CLICK = 1;
    private const int HOTKEY_RECORD = 2;
    private const int HOTKEY_MAC_REC = 200;
    private const int HOTKEY_MAC_PLAY = 201;
    private const int HOTKEY_SNAP = 202;
    private const int HOTKEY_TIMER = 203;

    // ---- 通用字段 ----
    private Timer mTimer, tCheck;
    private NotifyIcon tray;
    private ContextMenuStrip tMenu;
    private bool manual;
    private bool isSnapping;
    private bool aboutShow;
    private bool togTimer;
    private DateTime timStart;

    // ---- 宏相关 ----
    private MacRec macroRec;
    private MacPlay macroPlay;
    private MacForm? macForm;
    private bool macManual = false;

    // ---- 状态 ----
    public event Action<string>? StatusChanged;
    public bool IsRecording = false;
    public bool IsPlaying = false;

    // ---- 控件 ----
    private CheckBox chkAim;
    private NumericUpDown numInt, numLoop;
    private Button btnStart, btnStop, btnList, btnSave, btnLoad, btnTimer;
    private CheckBox chkSim, chkAutoMin;
    private HotKeyBox hkClick, hkRecord, hkSnap;  // 新增

    // ---- 标题栏 ----
    private Panel titleBar;
    private Label lblTitle;
    private Button btnAbout, btnMin, btnClose;

    // ---- 进度条 ----
    private ProgressBar progBar;

    // ---- 状态栏 ----
    private StatusStrip stBar;
    private ToolStripStatusLabel stStat, stCoord;

    // ---- 瞄准窗体 ----
    private AimForm? aimForm;

    public Main()
    {
        if (!System.IO.Directory.Exists(Config.ScriptDir))
            System.IO.Directory.CreateDirectory(Config.ScriptDir);

        // 初始化热键管理器
        HotKeyManager.Initialize(this.Handle);
        RegisterAllHotKeys();

        macroRec = new MacRec();
        macroPlay = new MacPlay();
        UpdateMacExcluded();

        InitTitleBar();
        InitContent();
        SetProg();
        InitStatusBar();
        InitTray();
        LoadCfg();

        this.Icon = LoadIcon("ClickHelper.Icon.YX.ico");
        this.FormClosing += OnFormClosing;
        this.Load += OnFormLoad;
        this.Resize += OnFormResize;
        this.KeyPreview = true;

        OcrHelper.OnError += (method, ex) =>
        {
            if (this.InvokeRequired)
                this.Invoke(() => OcrError(method, ex));
            else
                OcrError(method, ex);
        };

        tCheck = new Timer { Interval = 1000 };
        tCheck.Tick += TTick;
        if (cfg.TimerEnabled) tCheck.Start();

        mTimer = new Timer { Interval = 100 };
        mTimer.Tick += (s, e) => UpInfo();
        mTimer.Start();
    }

    private void RegisterAllHotKeys()
    {
        HotKeyManager.Register(HOTKEY_CLICK, cfg.ClickHotKey, OnHotKey);
        HotKeyManager.Register(HOTKEY_RECORD, cfg.RecordHotKey, GetPos);
        HotKeyManager.Register(HOTKEY_MAC_REC, cfg.MacRecHotKey, ToglRecord);
        HotKeyManager.Register(HOTKEY_MAC_PLAY, cfg.MacPlayHotKey, SwitchPlay);
        HotKeyManager.Register(HOTKEY_SNAP, cfg.SnapHotKey, AddSnap);
        HotKeyManager.Register(HOTKEY_TIMER, cfg.TimerHotKey, TogTimer);
    }

    private void UpdateMacExcluded()
    {
        if (macroRec != null)
        {
            var (_, k1) = WinApi.ParseHotKey(cfg.ClickHotKey);
            var (_, k2) = WinApi.ParseHotKey(cfg.RecordHotKey);
            var (_, k3) = WinApi.ParseHotKey(cfg.MacRecHotKey);
            var (_, k4) = WinApi.ParseHotKey(cfg.MacPlayHotKey);
            macroRec.SetExcluded((int)k1, (int)k2, (int)k3, (int)k4);
        }
    }

    // ---- 标题栏 ----
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
        btnAbout.Click += (s, e) => { using var about = new AboutForm(); about.ShowDialog(this); };
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
                WinApi.SendMessage(this.Handle, 0xA1, (nint)2, nint.Zero);
            }
        };
    }

    // ---- 主内容 ----
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
        mainContainer.Controls.Add(titleBar, 0, 0);

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

        // 自动最小化 + 显示瞄准
        var flowCb = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            BackColor = Color.Transparent
        };
        chkAutoMin = new CheckBox { Text = "自动最小化", AutoSize = true, Font = font, ForeColor = Color.FromArgb(40, 60, 90) };
        flowCb.Controls.Add(chkAutoMin);
        chkAim = new CheckBox
        {
            Text = "显示瞄准",
            AutoSize = true,
            Font = font,
            ForeColor = Color.FromArgb(40, 60, 90),
            Checked = cfg.ShowAim
        };
        chkAim.CheckedChanged += (s, e) => { cfg.ShowAim = chkAim.Checked; cfg.Save(); };
        flowCb.Controls.Add(chkAim);
        main.Controls.Add(new Label { Text = "", AutoSize = true }, 0, row);
        main.Controls.Add(flowCb, 1, row++);
        if (core != null)
        {
            core.BefClick += (x, y) =>
            {
                if (cfg.ShowAim)
                {
                    if (this.InvokeRequired)
                        this.Invoke(() => ShowAim(x, y));
                    else
                        ShowAim(x, y);
                }
            };
        }

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
        chkSim = new CheckBox { Text = "批量执行", AutoSize = true, Font = font, ForeColor = Color.FromArgb(40, 60, 90) };
        flowLoop.Controls.Add(chkSim);
        main.Controls.Add(new Label { Text = "循环", AutoSize = true, Font = lblFont, ForeColor = Color.FromArgb(40, 60, 90), Margin = new Padding(0, 6, 0, 0) }, 0, row);
        main.Controls.Add(flowLoop, 1, row++);

        // 热键（启动）
        main.Controls.Add(new Label { Text = "热键", AutoSize = true, Font = lblFont, ForeColor = Color.FromArgb(40, 60, 90), Margin = new Padding(0, 6, 0, 0) }, 0, row);
        hkClick = new HotKeyBox { HotKey = cfg.ClickHotKey };
        hkClick.HotKeyChanged += (s, val) => { cfg.ClickHotKey = val; cfg.Save(); HotKeyManager.Update(HOTKEY_CLICK, val); UpdateMacExcluded(); };
        main.Controls.Add(hkClick, 1, row++);

        // 记录热键
        main.Controls.Add(new Label { Text = "记录", AutoSize = true, Font = lblFont, ForeColor = Color.FromArgb(40, 60, 90), Margin = new Padding(0, 6, 0, 0) }, 0, row);
        hkRecord = new HotKeyBox { HotKey = cfg.RecordHotKey };
        hkRecord.HotKeyChanged += (s, val) => { cfg.RecordHotKey = val; cfg.Save(); HotKeyManager.Update(HOTKEY_RECORD, val); UpdateMacExcluded(); };
        main.Controls.Add(hkRecord, 1, row++);

        // 截图热键
        main.Controls.Add(new Label { Text = "截图", AutoSize = true, Font = lblFont, ForeColor = Color.FromArgb(40, 60, 90), Margin = new Padding(0, 6, 0, 0) }, 0, row);
        hkSnap = new HotKeyBox { HotKey = cfg.SnapHotKey };
        hkSnap.HotKeyChanged += (s, val) => { cfg.SnapHotKey = val; cfg.Save(); HotKeyManager.Update(HOTKEY_SNAP, val); };
        main.Controls.Add(hkSnap, 1, row++);

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

        // ---- 事件绑定 ----
        btnStart.Click += (s, e) => StartC();
        btnStop.Click += (s, e) => StopC();
        numInt.ValueChanged += (s, e) => { cfg.IntervalMs = (int)numInt.Value; cfg.Save(); core.SetInt(cfg.IntervalMs); };
        numLoop.ValueChanged += (s, e) => { cfg.PosLoopCount = (int)numLoop.Value; cfg.Save(); };
        chkSim.CheckedChanged += (s, e) => { cfg.SimulExec = chkSim.Checked; cfg.Save(); };
        btnList.Click += (s, e) => OpenPos();
        btnSave.Click += (s, e) => SaveSc();
        btnLoad.Click += (s, e) => LoadSc();
        btnTimer.Click += (s, e) => { using var dlg = new TimerForm(); dlg.ShowDialog(this); HotKeyManager.Update(HOTKEY_TIMER, cfg.TimerHotKey); };
        btnMacL.Click += (s, e) => OpenMacForm();
    }

    // ---- 瞄准 ----
    private void ShowAim(int x, int y)
    {
        if (aimForm != null && !aimForm.IsDisposed)
            aimForm.Close();
        aimForm = new AimForm(x, y);
        aimForm.Show();
    }

    // ---- 进度条 ----
    private void SetProg()
    {
        progBar = new ProgressBar
        {
            Dock = DockStyle.Bottom,
            Height = 20,
            Style = ProgressBarStyle.Continuous,
            Minimum = 0,
            Maximum = 100,
            Value = 0
        };
        this.Controls.Add(progBar);
        if (core != null)
        {
            core.PrgChg += (idx, total) => { if (this.InvokeRequired) this.Invoke(() => UpdProg(idx, total)); else UpdProg(idx, total); };
            core.Stopped += () =>
            {
                if (this.InvokeRequired)
                    this.Invoke(() => { progBar.Value = 0; SetStat("已停止"); });
                else
                { progBar.Value = 0; SetStat("已停止"); }
            };
        }
        macroPlay.PrgChg += (idx, total) => { if (this.InvokeRequired) this.Invoke(() => UpdProg(idx, total)); else UpdProg(idx, total); };
    }

    private void UpdProg(int idx, int total)
    {
        if (total <= 0) { progBar.Value = 0; return; }
        int val = (int)((idx * 100.0) / total);
        if (val > 100) val = 100;
        progBar.Value = val;
    }

    // ---- 状态栏 ----
    private void InitStatusBar()
    {
        stBar = new StatusStrip
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            BackColor = Color.FromArgb(230, 235, 240)
        };
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

    public void SetStat(string text)
    {
        if (stStat != null && !stStat.IsDisposed)
        {
            stStat.Text = text;
            StatusChanged?.Invoke(text);
        }
    }

    private void UpInfo()
    {
        if (stCoord == null || stCoord.IsDisposed) return;
        stCoord.Text = $"X:{Control.MousePosition.X} Y:{Control.MousePosition.Y}";
    }

    // ---- 托盘 ----
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

    // ---- 加载配置 ----
    private void LoadCfg()
    {
        numInt.Value = cfg.IntervalMs;
        numLoop.Value = cfg.PosLoopCount;
        chkSim.Checked = cfg.SimulExec;
        hkClick.HotKey = cfg.ClickHotKey;
        hkRecord.HotKey = cfg.RecordHotKey;
        hkSnap.HotKey = cfg.SnapHotKey;
        UpdateMacExcluded();
    }

    // ---- 保存/加载配置 ----
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
                cfg.RecordHotKey = newCfg.RecordHotKey;
                cfg.MacRecHotKey = newCfg.MacRecHotKey;
                cfg.MacPlayHotKey = newCfg.MacPlayHotKey;
                cfg.SnapHotKey = newCfg.SnapHotKey;
                cfg.TimerHotKey = newCfg.TimerHotKey;
                cfg.TimerEnabled = newCfg.TimerEnabled;
                cfg.TimerMode = newCfg.TimerMode;
                cfg.TimerStart = newCfg.TimerStart;
                cfg.TimerEnd = newCfg.TimerEnd;
                cfg.TimerDuration = newCfg.TimerDuration;
                cfg.TimeType = newCfg.TimeType;
                cfg.MacroName = newCfg.MacroName;
                cfg.Save();
                LoadCfg();
                core = new Core();
                RegisterAllHotKeys();
                UpdateMacExcluded();
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

    // ---- 热键处理（宏录制/播放等） ----
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
        UpdateMacExcluded();
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
        macForm = new MacForm(LoadCfg, this);
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
                progBar.Value = 0;
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
        progBar.Value = 0;
        SetStat("已停止");
    }

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
        progBar.Value = 0;
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

    private void OnHotKey()
    {
        if (core.Running)
            StopC();
        else
            StartC();
    }

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

    // ---- 坐标拾取 & 截图 ----
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
                        data.UseImage = NewEdit.NewUseImg;
                        data.ImageTemp = NewEdit.NewImgTmp;
                        data.Threshold = NewEdit.NewThresh;
                        data.UseTxt = NewEdit.NewUseTxt;
                        data.TxtMatch = NewEdit.NewTxtMatch;
                        data.TxtMode = NewEdit.NewTxtMode;
                        data.TxtThresh = NewEdit.NewTxtThr;
                        data.OcrOptions = NewEdit.NewOcrOpt ?? new OcrOpt();
                        data.UseUIA = NewEdit.NewUseUIA;
                        data.Targets = NewEdit.NewTargets;
                        data.AutoId = NewEdit.NewAutoId;
                        data.UName = NewEdit.NewUName;
                        data.ClassN = NewEdit.NewClassN;
                        data.TextContent = NewEdit.NewTxtVal;
                        data.ComboKeys = NewEdit.NewCombo;
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

    // ---- 定时任务 ----
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
                manual = false;
                core.Start();
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

                Task.Run(() =>
                {
                    macroPlay.Play(data, loopCount: cfg.PosLoopCount, speed: 1.0, done: () =>
                    {
                        this.Invoke(() =>
                        {
                            IsPlaying = false;
                            SetStat(cfg.PosLoopCount == -1 ? "已停止" : "播放完成");
                        });
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
                string hotKey = cfg.TimerHotKey;
                string msg = $"{info}\n\n按 {hotKey} 可停止定时。";
                if (MessageBox.Show(msg, "启用定时", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    manual = false;
                    macManual = false;
                    if (core.Running) StopC();
                    if (IsPlaying) StopPlay();

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
            StopPlay();

        progBar.Value = 0;
        SetStat("定时已禁用");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    // ---- 辅助 ----
    private void OpenPos()
    {
        using var dlg = new PosForm(LoadCfg);
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

    // ---- 窗口过程 ----
    protected override void WndProc(ref Message m)
    {
        if (HotKeyManager.ProcessHotKey(ref m))
            return;
        base.WndProc(ref m);
    }

    // ---- 窗体事件 ----
    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        HotKeyManager.UnregisterAll();
        core.Dispose();
        macroRec.Dispose();
        mTimer?.Stop();
        mTimer?.Dispose();
        tCheck?.Stop();
        tCheck?.Dispose();
        tray!.Visible = false;
        tray.Dispose();
        macroPlay.Dispose();
        Application.Exit();
    }

    private void OnFormLoad(object? sender, EventArgs e)
    {
        aboutShow = true;
        if (!cfg.SkipAbout)
        {
            using var about = new AboutForm();
            about.ShowDialog();
        }
        aboutShow = false;
        this.CenterToScreen();
        SetStat("空闲");
    }

    private void OnFormResize(object? sender, EventArgs e)
    {
        if (this.WindowState == FormWindowState.Minimized && chkAutoMin.Checked)
            this.Hide();
    }
}