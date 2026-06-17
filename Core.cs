using System;
using System.Windows.Forms;
using static ClickHelper.WinApi;

namespace ClickHelper;

/// <summary> 点击核心 </summary>
internal class Core
{
    private Timer tmr;          // 主定时器
    private Timer dTmr;         // 延迟定时器
    private Config cfg;
    private int idx;            // 当前索引（顺序模式）或轮次（同时模式）
    private bool run;
    private bool delay;
    private object lck = new();
    public int CurIdx => idx;
    public bool Running => run;

    public Core(Config config)
    {
        cfg = config;
        tmr = new Timer { Interval = cfg.IntervalMs };
        tmr.Tick += Tick;
        dTmr = new Timer();
        dTmr.Tick += DTick;
        idx = 0;
        run = false;
        delay = false;
    }

    public void Start()
    {
        lock (lck)
        {
            if (run) return;
            if (delay)
            {
                dTmr.Stop();
                delay = false;
            }
            tmr.Interval = Math.Max(1, cfg.IntervalMs);
            tmr.Start();
            run = true;
        }
    }

    public void Stop()
    {
        lock (lck)
        {
            if (!run && !delay) return;
            if (delay)
            {
                dTmr.Stop();
                delay = false;
            }
            tmr.Stop();
            run = false;
            idx = 0;
        }
    }

    public void SetInt(int ms)
    {
        tmr.Interval = Math.Max(1, ms);
        cfg.IntervalMs = ms;
    }

    private void Tick(object? sender, EventArgs e) => Next();

    private void Next()
    {
        if (cfg.PosLoopCount == 0 || cfg.PosList.Count == 0)
            return;

        if (cfg.SimulExec)
        {
            // 同时执行模式：一轮内执行所有位置，忽略延迟
            if (cfg.PosLoopCount > 0 && idx >= cfg.PosLoopCount)
            {
                Stop();
                return;
            }
            foreach (var pos in cfg.PosList)
                Exec(pos);
            idx++;
        }
        else
        {
            // 顺序执行模式
            int total = cfg.PosList.Count;
            if (cfg.PosLoopCount > 0 && idx >= cfg.PosLoopCount * total)
            {
                Stop();
                return;
            }
            var pos = cfg.PosList[idx % total];

            if (pos.WaitMs > 0)
            {
                tmr.Stop();
                delay = true;
                dTmr.Interval = pos.WaitMs;
                dTmr.Start();
                return;
            }

            Exec(pos);
            idx++;
        }
    }

    private void DTick(object? sender, EventArgs e)
    {
        dTmr.Stop();
        delay = false;

        int total = cfg.PosList.Count;
        var pos = cfg.PosList[idx % total];
        Exec(pos);
        idx++;

        tmr.Start();
        Next();
    }

    private void Exec(Config.PosData pos)
    {
        if (pos.ActType == 0)
            MClick(pos.X, pos.Y, pos.ActKey, pos.OpMode);
        else if (pos.ActType == 1)
            KPress(pos.ActKey, pos.ModKey, pos.OpMode);
    }

    // ---- 鼠标操作（统一调用 WinApi） ----
    private void MClick(int x, int y, int btn, int mode)
    {
        Cursor.Position = new System.Drawing.Point(x, y);
        if (btn == 0)
        {
            if (mode == 0 || mode == 1) WinApi.LeftDown();
            if (mode == 0 || mode == 2) WinApi.LeftUp();
        }
        else if (btn == 1)
        {
            if (mode == 0 || mode == 1) WinApi.MiddleDown();
            if (mode == 0 || mode == 2) WinApi.MiddleUp();
        }
        else if (btn == 2)
        {
            if (mode == 0 || mode == 1) WinApi.RightDown();
            if (mode == 0 || mode == 2) WinApi.RightUp();
        }
    }

    // ---- 键盘操作（统一调用 WinApi） ----
    private void KPress(int vk, int mod, int mode)
    {
        if (mod != 0)
        {
            Keys mk = 0;
            if ((mod & 1) != 0) mk = Keys.Control;
            else if ((mod & 2) != 0) mk = Keys.Shift;
            else if ((mod & 4) != 0) mk = Keys.Alt;
            if (mk != 0 && (mode == 0 || mode == 1)) WinApi.KeyDown(mk);
        }

        if (mode == 0 || mode == 1) WinApi.KeyDown((Keys)vk);
        if (mode == 0 || mode == 2) WinApi.KeyUp((Keys)vk);

        if (mod != 0 && (mode == 0 || mode == 2))
        {
            Keys mk = 0;
            if ((mod & 1) != 0) mk = Keys.Control;
            else if ((mod & 2) != 0) mk = Keys.Shift;
            else if ((mod & 4) != 0) mk = Keys.Alt;
            if (mk != 0) WinApi.KeyUp(mk);
        }
    }

    // ---- 列表编辑 ----
    public void Add(int x, int y, string? desc = null, int actType = 0, int actKey = 0, int modKey = 0, int opMode = 0, int waitMs = 0)
    {
        cfg.PosList.Add(new Config.PosData
        {
            X = x,
            Y = y,
            Desc = desc,
            ActType = actType,
            ActKey = actKey,
            ModKey = modKey,
            OpMode = opMode,
            WaitMs = waitMs
        });
        cfg.Save();
    }

    public void Del(int index)
    {
        if (index >= 0 && index < cfg.PosList.Count)
        {
            cfg.PosList.RemoveAt(index);
            cfg.Save();
        }
    }

    public void Swap(int i, int j)
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
        tmr.Tick -= Tick;
        tmr.Dispose();
        dTmr.Tick -= DTick;
        dTmr.Dispose();
    }
}