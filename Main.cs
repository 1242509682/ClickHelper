using System;
using System.Drawing;
using System.Windows.Forms;
using ClickHelper.Macro;
using Newtonsoft.Json;

namespace ClickHelper;

/// <summary> 主窗口，UI与逻辑协调 </summary>
public class Main : Form
{
    // ---- UI控件 ----
    private NumericUpDown numInt;
    private Button btnStart, btnStop;
    private Button btnList;
    private NumericUpDown numLoop;
    private CheckBox chkSim, chkAutoMin;
    private Button btnSave, btnLoad;
    private ComboBox cboHot, cboHotAlt;
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

    // ---- 状态变化事件（供 MacForm 订阅） ----
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

        // ========== 修改点：添加 Application.Exit() ==========
        this.FormClosing += (s, e) =>
        {
            hk.Unregister();
            core.Dispose();
            macroRec.Dispose();
            mTimer?.Stop();
            tCheck?.Stop();
            tray!.Visible = false;
            tray.Dispose();
            Application.Exit();   // 确保进程完全退出
        };
        // =================================================

        this.Load += (s, e) =>
        {
            // 如果用户选择“不再显示”，则跳过关于对话框
            if (!cfg.SkipAbout)
            {
                using var about = new AboutForm(cfg);
                about.ShowDialog();
            }

            this.CenterToScreen();
        };

        this.Resize += (s, e) =>
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
                // 托盘常驻，不再切换可见性
            }
        };

        tCheck = new Timer { Interval = 1000 };
        tCheck.Tick += TTick;
        if (cfg.TimerEnabled) tCheck.Start();

        RegisterMacroHotKeys();
    }

    private void InitUI()
    {
        this.Text = $"点击助手 {Program.ver}";
        this.Size = new Size(340, 430);
        this.MinimumSize = new Size(340, 430);
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;

        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 9,
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

        // 行7：配置 + 定时
        main.Controls.Add(new Label { Text = "配置", AutoSize = true, Font = font, Margin = new Padding(0, 4, 0, 0) }, 0, row);
        var scrPan = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        btnSave = new Button { Text = "保存配置", AutoSize = true, Font = font };
        btnLoad = new Button { Text = "加载配置", AutoSize = true, Font = font };
        scrPan.Controls.Add(btnSave);
        scrPan.Controls.Add(btnLoad);
        main.Controls.Add(scrPan, 1, row++);

        // 行8：定时设置
        main.Controls.Add(new Label { Text = "定时", AutoSize = true, Font = font, Margin = new Padding(0, 4, 0, 0) }, 0, row);
        var flowTimer = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        btnTimer = new Button { Text = "定时管理", AutoSize = true, Font = font };
        flowTimer.Controls.Add(btnTimer);
        main.Controls.Add(flowTimer, 1, row++);

        this.Controls.Add(main);

        mTimer = new Timer { Interval = 100 };
        mTimer.Tick += (s, e) => UpInfo();
        mTimer.Start();

        // 热键下拉（使用公共键列表）
        var keys = WinApi.GetCommonKeys();
        cboHot.Items.AddRange(keys);
        cboHotAlt.Items.AddRange(keys);
        // 额外添加鼠标键（仅记录热键可用）
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
        cboHot.SelectedIndexChanged += (s, e) => { if (cboHot.SelectedItem is Keys k) { cfg.ClickHotKey = (int)k; cfg.Save(); hk.UpdateKeys(cfg.ClickHotKey, cfg.HotKeyAltL); UpdateMacroExclusions(); } };
        cboHotAlt.SelectedIndexChanged += (s, e) => { if (cboHotAlt.SelectedItem is Keys k) { cfg.HotKeyAltL = (int)k; cfg.Save(); hk.UpdateKeys(cfg.ClickHotKey, cfg.HotKeyAltL); UpdateMacroExclusions(); } };
        btnTimer.Click += (s, e) =>
        {
            using var dlg = new TimerForm(cfg);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                if (cfg.TimerEnabled) tCheck.Start(); else tCheck.Stop();
            }
        };

        btnMacL.Click += (s, e) =>
        {
            using var form = new MacForm(cfg, LoadCfg, this);
            form.ShowDialog();
        };

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
            Visible = true          // 始终显示
        };
        tray.DoubleClick += (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; this.Activate(); };
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

        UpdateMacroExclusions();
    }

    private void RegisterMacroHotKeys()
    {
        UnregisterMacroHotKeys();
        WinApi.RegisterHotKey(this.Handle, macroRecHotId, 0, (uint)cfg.MacRecHotKey);
        WinApi.RegisterHotKey(this.Handle, macroPlayHotId, 0, (uint)cfg.MacPlayHotKey);
    }

    private void UnregisterMacroHotKeys()
    {
        WinApi.UnregisterHotKey(this.Handle, macroRecHotId);
        WinApi.UnregisterHotKey(this.Handle, macroPlayHotId);
    }

    public void UpdateMacroHotKeys(int newRec, int newPlay)
    {
        cfg.MacRecHotKey = newRec;
        cfg.MacPlayHotKey = newPlay;
        cfg.Save();
        RegisterMacroHotKeys();
        UpdateMacroExclusions();
    }

    private void UpdateMacroExclusions()
    {
        if (macroRec != null)
            macroRec.SetExcluded(cfg.ClickHotKey, cfg.HotKeyAltL, cfg.MacRecHotKey, cfg.MacPlayHotKey);
    }

    // ---- 宏控制 ----
    public void ToggleRecording()
    {
        if (IsRecording)
            StopRecording();
        else
            StartRecording();
    }

    private void StartRecording()
    {
        if (IsRecording) return;
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
            MessageBox.Show($"录制完成，共 {items.Count} 条指令");
        }
        else
            MessageBox.Show("未录制到操作");
        IsRecording = false;
        SetStat("空闲");
    }

    public void TogglePlay()
    {
        if (IsPlaying)
            StopPlay();
        else
            StartPlay();
    }

    private void StartPlay()
    {
        var names = MacIO.GetMacroNames();
        if (names.Length == 0) { MessageBox.Show("无宏可播放"); return; }
        Array.Sort(names);
        var last = names[^1];
        var data = MacIO.Load(last);
        if (data == null || data.Items.Count == 0) { MessageBox.Show("宏为空"); return; }
        if (IsPlaying) return;
        IsPlaying = true;
        SetStat("播放中...");
        if (chkAutoMin.Checked)
            this.WindowState = FormWindowState.Minimized;
        macroPlay.Play(data, speed: 1.0, done: () =>
        {
            this.Invoke((Action)(() =>
            {
                IsPlaying = false;
                SetStat("播放完成");
            }));
        });
    }

    private void StopPlay()
    {
        if (!IsPlaying) return;
        macroPlay.Stop();
        IsPlaying = false;
        SetStat("已停止");
    }

    // ---- 原有方法 ----
    private void StartC()
    {
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
        manual = false;
        core.Stop();
        UpBtns();
        SetStat("空闲");
    }

    private void AddCur()
    {
        var pt = Control.MousePosition;
        core.Add(pt.X, pt.Y, $"P{cfg.PosList.Count + 1}", 0, 0, 0, 0);
        System.Media.SystemSounds.Beep.Play();  // 蜂鸣提示
        SetStat($"已记录 ({pt.X},{pt.Y})");
        // 1秒后恢复状态（若未改变）
        System.Threading.Tasks.Task.Delay(1000).ContinueWith(_ =>
        {
            if (stStat != null && !stStat.IsDisposed && stStat.Text == $"已记录 ({pt.X},{pt.Y})")
                stStat.Text = "空闲";
        });
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
                // 手动复制所有字段，避免引用混乱
                cfg.IntervalMs = newCfg.IntervalMs;
                cfg.PosLoopCount = newCfg.PosLoopCount;
                cfg.PosList = newCfg.PosList;
                cfg.SimulExec = newCfg.SimulExec;
                cfg.ClickHotKey = newCfg.ClickHotKey;
                cfg.HotKeyAltL = newCfg.HotKeyAltL;
                cfg.MacRecHotKey = newCfg.MacRecHotKey;
                cfg.MacPlayHotKey = newCfg.MacPlayHotKey;
                cfg.TimerEnabled = newCfg.TimerEnabled;
                cfg.TimerStart = newCfg.TimerStart;
                cfg.TimerEnd = newCfg.TimerEnd;
                cfg.Save();
                LoadCfg();
                core = new Core(cfg);
                hk.UpdateKeys(cfg.ClickHotKey, cfg.HotKeyAltL);
                RegisterMacroHotKeys();
                UpdateMacroExclusions();
                if (cfg.TimerEnabled) tCheck.Start(); else tCheck.Stop();
                MessageBox.Show("配置加载成功");
            }
            catch (Exception ex) { MessageBox.Show("加载失败: " + ex.Message); }
        }
    }

    private void OnHotKey()
    {
        if (core.Running)
            StopC();
        else
            StartC();
    }

    private void TTick(object? sender, EventArgs e)
    {
        if (!cfg.TimerEnabled) return;
        DateTime now = DateTime.Now;
        bool inRange = now >= cfg.TimerStart && (cfg.TimerEnd == DateTime.MinValue || now < cfg.TimerEnd);

        if (inRange)
        {
            if (!core.Running && !manual)
            {
                if (cfg.PosLoopCount == 0 || cfg.PosList.Count == 0)
                {
                    cfg.TimerEnabled = false;
                    cfg.Save();
                    tCheck.Stop();
                    MessageBox.Show("循环为0或列表为空，已禁用定时");
                    return;
                }
                core.Start();
                UpBtns();
                SetStat("定时运行中");
                if (chkAutoMin.Checked)
                    this.WindowState = FormWindowState.Minimized;
            }
            else if (core.Running && manual)
            {
                SetStat("手动运行中");
            }
        }
        else
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
    }

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
            StatusChanged?.Invoke(text); // 通知订阅者（如 MacForm）
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
                ToggleRecording();
                return;
            }
            else if (id == macroPlayHotId)
            {
                TogglePlay();
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