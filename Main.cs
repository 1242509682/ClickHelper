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
    private ToolStripStatusLabel stStat, stCoord;

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
        hk = new HotKey(this.Handle, OnHotKey, AddCur, cfg.ClickHotKey, cfg.HotKeyAltL);

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
        };

        this.Resize += (s, e) =>
        {
            if (this.WindowState == FormWindowState.Minimized && chkAutoMin.Checked)
            {
                this.Hide();
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
        this.Text = $"点击助手 {Program.ver}";
        this.Size = new Size(340, 480);
        this.MinimumSize = new Size(340, 480);
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;

        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 10,
            Padding = new Padding(6)
        };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < main.RowCount; i++)
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Font font = new Font("微软雅黑", 8.5F);
        ToolTip tip = new();
        int row = 0;

        // 行0：间隔 + 启动/停止
        var flowCtrl = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        numInt = new NumericUpDown { Minimum = 1, Maximum = 1800000, Value = 50, Width = 60, Font = font };
        tip.SetToolTip(numInt, "单位毫秒,1~1800000");
        flowCtrl.Controls.Add(numInt);
        btnStart = new Button { Text = "启动", Width = 55, AutoSize = true, Font = font };
        btnStop = new Button { Text = "停止", Width = 55, AutoSize = true, Enabled = false, Font = font };
        flowCtrl.Controls.Add(btnStart);
        flowCtrl.Controls.Add(btnStop);
        main.Controls.Add(new Label { Text = "间隔", AutoSize = true, Font = font, Margin = new Padding(0, 4, 0, 0) }, 0, row);
        main.Controls.Add(flowCtrl, 1, row++);

        // 行1：自动最小化
        chkAutoMin = new CheckBox { Text = "自动最小化", AutoSize = true, Font = font };
        main.Controls.Add(new Label { Text = "", AutoSize = true }, 0, row);
        main.Controls.Add(chkAutoMin, 1, row++);

        // 行2：位置列表
        btnList = new Button { Text = "位置列表", AutoSize = true, Font = font };
        main.Controls.Add(new Label { Text = "位置", AutoSize = true, Font = font, Margin = new Padding(0, 4, 0, 0) }, 0, row);
        main.Controls.Add(btnList, 1, row++);

        // 行3：宏管理
        main.Controls.Add(new Label { Text = "录制", AutoSize = true, Font = font, Margin = new Padding(0, 4, 0, 0) }, 0, row);
        var flowMac = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        var btnMacL = new Button { Text = "宏管理器", AutoSize = true, Font = font };
        flowMac.Controls.Add(btnMacL);
        main.Controls.Add(flowMac, 1, row++);

        // 行4：循环 + 同时执行
        var flowLoop = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        numLoop = new NumericUpDown { Minimum = -1, Maximum = 99999, Value = -1, Width = 60, Font = font };
        tip.SetToolTip(numLoop, "-1无限，0不循环");
        flowLoop.Controls.Add(numLoop);
        flowLoop.Controls.Add(new Label { Text = "  ", AutoSize = true });
        chkSim = new CheckBox { Text = "批量执行", AutoSize = true, Font = font };
        flowLoop.Controls.Add(chkSim);
        main.Controls.Add(new Label { Text = "循环", AutoSize = true, Font = font, Margin = new Padding(0, 4, 0, 0) }, 0, row);
        main.Controls.Add(flowLoop, 1, row++);

        // 行5：启动热键
        main.Controls.Add(new Label { Text = "热键", AutoSize = true, Font = font, Margin = new Padding(0, 4, 0, 0) }, 0, row);
        cboHot = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70, Font = font };
        main.Controls.Add(cboHot, 1, row++);

        // 行6：记录热键
        main.Controls.Add(new Label { Text = "记录", AutoSize = true, Font = font, Margin = new Padding(0, 4, 0, 0) }, 0, row);
        var flowRec = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        cboHotAlt = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70, Font = font };
        flowRec.Controls.Add(cboHotAlt);
        flowRec.Controls.Add(new Label { Text = "+ Alt", AutoSize = true, Font = font });
        main.Controls.Add(flowRec, 1, row++);

        // 行7：截图热键
        main.Controls.Add(new Label { Text = "截图", AutoSize = true, Font = font, Margin = new Padding(0, 4, 0, 0) }, 0, row);
        var flowSnap = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        cboSnap = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70, Font = font };
        flowSnap.Controls.Add(cboSnap);
        flowSnap.Controls.Add(new Label { Text = "+ Alt", AutoSize = true, Font = font });
        main.Controls.Add(flowSnap, 1, row++);

        // 行8：配置 + 定时
        main.Controls.Add(new Label { Text = "配置", AutoSize = true, Font = font, Margin = new Padding(0, 4, 0, 0) }, 0, row);
        var scrPan = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        btnSave = new Button { Text = "保存配置", AutoSize = true, Font = font };
        btnLoad = new Button { Text = "加载配置", AutoSize = true, Font = font };
        scrPan.Controls.Add(btnSave);
        scrPan.Controls.Add(btnLoad);
        main.Controls.Add(scrPan, 1, row++);

        // 行9：定时设置
        main.Controls.Add(new Label { Text = "定时", AutoSize = true, Font = font, Margin = new Padding(0, 4, 0, 0) }, 0, row);
        var flowTimer = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        btnTimer = new Button { Text = "定时管理", AutoSize = true, Font = font };
        flowTimer.Controls.Add(btnTimer);
        main.Controls.Add(flowTimer, 1, row++);

        this.Controls.Add(main);

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

        // ---- 事件绑定 ----
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
        stBar = new StatusStrip { Dock = DockStyle.Bottom, AutoSize = true };
        stStat = new ToolStripStatusLabel("空闲") { Spring = false, TextAlign = ContentAlignment.MiddleLeft, Width = 60, AutoSize = true };
        stCoord = new ToolStripStatusLabel("X:0 Y:0") { Spring = false, TextAlign = ContentAlignment.MiddleRight, AutoSize = true };
        stBar.Items.Add(stStat);
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
        WinApi.UnregisterHotKey(this.Handle, macroRecHotId);
        WinApi.UnregisterHotKey(this.Handle, macroPlayHotId);
    }

    private void RegSnapKey()
    {
        UnRegSnapKey();
        WinApi.RegisterHotKey(this.Handle, snapHotId, WinApi.MOD_ALT, (uint)cfg.SnapHotKey);
    }

    private void UnRegSnapKey()
    {
        WinApi.UnregisterHotKey(this.Handle, snapHotId);
    }

    private void RegTimerKey()
    {
        UnTimerKey();
        WinApi.RegisterHotKey(this.Handle, timerHotId, 0, (uint)cfg.TimerHotKey);
    }

    private void UnTimerKey()
    {
        WinApi.UnregisterHotKey(this.Handle, timerHotId);
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
            StopRecording();
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

    private void StopRecording()
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

    public void TogglePlay()
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
            MessageBox.Show("位置列表为空");
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
    }

    private void AddCur()
    {
        if (core.Running || IsRecording || IsPlaying || isSnapping) return;

        var pt = Control.MousePosition;
        core.Add(pt.X, pt.Y, $"P{cfg.PosList.Count + 1}", 0, 0, 0, 0);

        // 显示水波动画（仅记录坐标时）
        new AnimForm(pt).Show();
        SetStat($"已记录 ({pt.X},{pt.Y})");
        System.Threading.Tasks.Task.Delay(1000).ContinueWith(_ =>
        {
            if (stStat != null && !stStat.IsDisposed && stStat.Text == $"已记录 ({pt.X},{pt.Y})")
                stStat.Text = "空闲";
        });
    }

    // ---- 截图添加 ----
    private void SnapAndAdd()
    {
        if (core.Running || IsRecording || IsPlaying || isSnapping) return;

        isSnapping = true;
        try
        {
            bool wasMinimized = false;
            if (chkAutoMin.Checked)
            {
                wasMinimized = true;
                this.WindowState = FormWindowState.Minimized;
                this.Hide();
            }

            using var snap = new SnapForm();
            if (snap.ShowDialog() == DialogResult.OK && snap.CapturedImage != null)
            {
                var bytes = ImgMatch.Bmp2Bytes(snap.CapturedImage);
                var data = new Config.PosData
                {
                    X = 0,
                    Y = 0,
                    Desc = $"截图_{DateTime.Now:HHmmss}",
                    ActType = 0,
                    ActKey = 0,
                    OpMode = 0,
                    WaitMs = 0,
                    UseImageMatch = true,
                    ImageTemplate = bytes,
                    Threshold = 0.8f
                };
                cfg.PosList.Add(data);
                cfg.Save();

                this.BeginInvoke(() =>
                {
                    using var NewEdit = new PosForm.PosEdit(data);
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
                        data.UseImageMatch = NewEdit.NewUseImageMatch;
                        data.ImageTemplate = NewEdit.NewImageTemplate;
                        data.Threshold = NewEdit.NewThreshold;
                    }
                });
            }

            if (wasMinimized)
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

    // ---- 配置保存/加载 ----
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
            catch (Exception ex) { MessageBox.Show("加载失败: " + ex.Message); }
        }
    }

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
                    SetStat("位置列表为空或循环0，跳过");
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

    // 获取定时任务详情
    private string GetTimInfo()
    {
        string modeStr = cfg.TimerMode == 0 ? "日期模式" : "计时模式";
        string tgtInfo = "";
        string detInfo = "";
        string durInfo = "";

        if (cfg.TimeType == 0)
        {
            tgtInfo = "执行目标：位置列表";
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
    }

    // ---- 其他辅助 ----
    private void OpenPos()
    {
        using var dlg = new PosForm(cfg, core, LoadCfg);
        dlg.ShowDialog(this);
    }

    private void SetStat(string text)
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
                TogglePlay();
                return;
            }
            else if (id == snapHotId)
            {
                SnapAndAdd();
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