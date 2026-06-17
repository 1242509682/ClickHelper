using System;
using System.Windows.Forms;
using static ClickHelper.WinApi;

namespace ClickHelper;

internal class ClickCore
{
    private Timer timer;
    private Config cfg;
    private int idx;      // 对于顺序执行：已执行位置个数；对于同时执行：已执行轮数
    private bool isRun;
    private object lockObj = new();

    public ClickCore(Config config)
    {
        cfg = config;
        timer = new Timer { Interval = cfg.IntervalMs };
        timer.Tick += OnTick;
        idx = 0;
        isRun = false;
    }

    public void Start()
    {
        lock (lockObj)
        {
            if (isRun) return;
            timer.Interval = Math.Max(1, cfg.IntervalMs);
            timer.Start();
            isRun = true;
        }
    }

    public void Stop()
    {
        lock (lockObj)
        {
            if (!isRun) return;
            timer.Stop();
            isRun = false;
            idx = 0;
        }
    }
    public bool IsRunning => isRun;

    public void SetInterval(int ms)
    {
        timer.Interval = Math.Max(1, ms);
        cfg.IntervalMs = ms;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (cfg.PosLoopCount == 0 || cfg.PosList.Count == 0)
            return;

        if (cfg.SimulExec)
        {
            // 同时执行模式：每轮执行所有位置一次，idx 为已执行轮数
            if (cfg.PosLoopCount > 0 && idx >= cfg.PosLoopCount)
            {
                Stop();
                return;
            }

            // 一次性执行全部位置
            foreach (var pos in cfg.PosList)
                ExecuteAction(pos);

            idx++; // 完成一轮
        }
        else
        {
            // 顺序执行模式：每次执行一个位置，idx 为已执行位置个数
            int totalCount = cfg.PosList.Count;
            if (cfg.PosLoopCount > 0 && idx >= cfg.PosLoopCount * totalCount)
            {
                Stop();
                return;
            }

            var pos = cfg.PosList[idx % totalCount];
            ExecuteAction(pos);
            idx++;
        }
    }

    private void ExecuteAction(Config.PosData pos)
    {
        if (pos.ActType == 0) // 鼠标
            DoMouseClick(pos.X, pos.Y, pos.ActKey, pos.OpMode);
        else if (pos.ActType == 1) // 键盘
            DoKeyPress(pos.ActKey, pos.ModKey, pos.OpMode);
    }

    private void DoMouseClick(int x, int y, int btn, int mode)
    {
        Cursor.Position = new System.Drawing.Point(x, y);
        if (btn == 0) // 左键
        {
            if (mode == 0 || mode == 1) LeftDown();
            if (mode == 0 || mode == 2) LeftUp();
        }
        else if (btn == 1) // 中键
        {
            if (mode == 0 || mode == 1) MiddleDown();
            if (mode == 0 || mode == 2) MiddleUp();
        }
        else if (btn == 2) // 右键
        {
            if (mode == 0 || mode == 1) RightDown();
            if (mode == 0 || mode == 2) RightUp();
        }
    }

    // 辅助鼠标事件（直接调用 WinApi 的 mouse_event）
    private void LeftDown() { mouse_event(0x02, 0, 0, 0, 0); }
    private void LeftUp() { mouse_event(0x04, 0, 0, 0, 0); }
    private void MiddleDown() { mouse_event(0x20, 0, 0, 0, 0); }
    private void MiddleUp() { mouse_event(0x40, 0, 0, 0, 0); }
    private void RightDown() { mouse_event(0x08, 0, 0, 0, 0); }
    private void RightUp() { mouse_event(0x10, 0, 0, 0, 0); }

    private void DoKeyPress(int vk, int mod, int mode)
    {
        if (mod != 0)
        {
            Keys modKey = 0;
            if ((mod & 1) != 0) modKey = Keys.Control;
            else if ((mod & 2) != 0) modKey = Keys.Shift;
            else if ((mod & 4) != 0) modKey = Keys.Alt;
            if (modKey != 0 && (mode == 0 || mode == 1)) KeyDown(modKey);
        }

        if (mode == 0 || mode == 1) KeyDown((Keys)vk);
        if (mode == 0 || mode == 2) KeyUp((Keys)vk);

        if (mod != 0 && (mode == 0 || mode == 2))
        {
            Keys modKey = 0;
            if ((mod & 1) != 0) modKey = Keys.Control;
            else if ((mod & 2) != 0) modKey = Keys.Shift;
            else if ((mod & 4) != 0) modKey = Keys.Alt;
            if (modKey != 0) KeyUp(modKey);
        }
    }

    // 位置管理
    public void AddPos(int x, int y, string? desc = null, int actType = 0, int actKey = 0, int modKey = 0, int opMode = 0)
    {
        cfg.PosList.Add(new Config.PosData
        {
            X = x,
            Y = y,
            Desc = desc,
            ActType = actType,
            ActKey = actKey,
            ModKey = modKey,
            OpMode = opMode
        });
        cfg.Save();
    }

    public void DelPos(int index)
    {
        if (index >= 0 && index < cfg.PosList.Count)
        {
            cfg.PosList.RemoveAt(index);
            cfg.Save();
        }
    }

    public void SwapPos(int i, int j)
    {
        if (i >= 0 && i < cfg.PosList.Count && j >= 0 && j < cfg.PosList.Count)
        {
            var tmp = cfg.PosList[i];
            cfg.PosList[i] = cfg.PosList[j];
            cfg.PosList[j] = tmp;
            cfg.Save();
        }
    }

    public void Dispose()
    {
        Stop();
        timer.Tick -= OnTick;
        timer.Dispose();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
}