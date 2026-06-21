using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Point = System.Drawing.Point;
using Timer = System.Windows.Forms.Timer;

namespace ClickHelper;

/// <summary> 点击核心 </summary>
internal class Core
{
    private Timer tmr;
    private Timer dTmr;
    private Config cfg;
    private int idx;
    private bool run;
    private bool delay;
    private object lck = new();
    public int CurIdx => idx;
    public bool Running => run;
    private Task? simulTask;
    private CancellationTokenSource? simulCts;
    private bool isBatchRunning = false;
    public event Action? Stopped;
    private static bool cvMissingShown = false;

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

            cvMissingShown = false;   // 每次启动重置，确保下次启动仍会检测

            if (cfg.SimulExec)
            {
                if (simulTask != null && !simulTask.IsCompleted) return;
                run = true;
                isBatchRunning = false;
                idx = 0;
                simulCts = new CancellationTokenSource();
                var token = simulCts.Token;

                simulTask = Task.Run(() =>
                {
                    try
                    {
                        int loopIdx = 0;
                        while (!token.IsCancellationRequested)
                        {
                            if (cfg.PosLoopCount > 0 && loopIdx >= cfg.PosLoopCount)
                                break;
                            var items = cfg.PosList.ToList();
                            foreach (var pos in items)
                            {
                                if (token.IsCancellationRequested) break;
                                Exec(pos);
                            }
                            loopIdx++;
                            if (cfg.IntervalMs > 0 && !token.IsCancellationRequested)
                            {
                                Task.Delay(cfg.IntervalMs, token).Wait(token);
                            }
                        }
                    }
                    catch (OperationCanceledException) { }
                    finally
                    {
                        lock (lck) { run = false; isBatchRunning = false; }
                        Stopped?.Invoke();
                    }
                }, token);
                return;
            }

            if (delay) { dTmr.Stop(); delay = false; }
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
            if (cfg.SimulExec)
            {
                simulCts?.Cancel();
                return;
            }
            if (delay) { dTmr.Stop(); delay = false; }
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

    private void Next()
    {
        if (cfg.PosLoopCount == 0 || cfg.PosList.Count == 0) return;

        if (cfg.SimulExec)
        {
            if (cfg.PosLoopCount > 0 && idx >= cfg.PosLoopCount) { Stop(); return; }
            foreach (var pos in cfg.PosList) Exec(pos);
            idx++;
        }
        else
        {
            int total = cfg.PosList.Count;
            if (cfg.PosLoopCount > 0 && idx >= cfg.PosLoopCount * total) { Stop(); return; }
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

    private void Exec(Config.PosData pos)
    {
        // ---- 文字匹配 + 图像匹配 ----
        if (pos.ImageTemp != null && pos.ImageTemp.Length > 0)
        {
            if (!CvCheck.IsReady())
            {
                if (!cvMissingShown)
                {
                    cvMissingShown = true;
                    using var dlg = new MissingCvForm();
                    dlg.ShowDialog();
                }
                return; // 跳过此坐标项
            }

            if (!pos.UseImage) return;

                var rect = ImgMatch.FindPoint(pos.ImageTemp, pos.Threshold);
            if (!rect.HasValue) return;

            if (pos.UseTxt && !string.IsNullOrEmpty(pos.TxtMatch))
            {
                using var tempBmp = ImgMatch.Bytes2Bmp(pos.ImageTemp);
                if (tempBmp == null)
                    return; // 模板无效则放弃

                int w = tempBmp.Width, h = tempBmp.Height;

                var rect2 = new Rectangle(rect.Value.X - w / 2, rect.Value.Y - h / 2, w, h);
                var blocks = OcrHelper.GetText(rect2);
                if (blocks != null)
                {
                    foreach (var block in blocks)
                    {
                        bool match = pos.TxtMode == 0 ? block.Text.Contains(pos.TxtMatch) : block.Text.Equals(pos.TxtMatch);
                        if (match && block.Score >= pos.TxtThresh)
                        {
                            var pts = block.BoxPoints;
                            // 获取文字坐标 + 偏移
                            int cx = (int)pts.Average(p => p.X) + rect2.X;
                            int cy = (int)pts.Average(p => p.Y) + rect2.Y;
                            MClick(cx, cy, pos.ActKey, pos.OpMode, pos.UseUIA, pos.UIAProc);
                            return;
                        }
                    }
                }
            }
            else
            {
                // ---- 图像匹配 ----
                MClick(rect.Value.X, rect.Value.Y, pos.ActKey, pos.OpMode, pos.UseUIA, pos.UIAProc);
            }
            return;
        }

        // ---- 普通坐标操作 / 键盘 / 文本 / 组合键 ----
        switch (pos.ActType)
        {
            case 0: // 鼠标点击
                int clickX = pos.X;
                int clickY = pos.Y;
                // 若坐标为 (0,0)，则使用当前鼠标位置
                if (clickX == 0 && clickY == 0)
                {
                    clickX = Cursor.Position.X;
                    clickY = Cursor.Position.Y;
                }
                MClick(clickX, clickY, pos.ActKey, pos.OpMode, pos.UseUIA, pos.UIAProc);
                break;

            case 1: // 键盘按键
                KPress(pos.ActKey, pos.ModKey, pos.OpMode);
                break;

            case 2: // 文本输入
                if (!string.IsNullOrEmpty(pos.TextContent))
                    FlaUIHelper.TypeText(pos.TextContent);
                break;

            case 3: // 组合键
                if (!string.IsNullOrEmpty(pos.ComboKeys))
                {
                    var keys = FlaUIHelper.ComboKeys(pos.ComboKeys);
                    if (keys.Count > 0)
                        FlaUIHelper.TypeSimultaneously(keys.ToArray());
                }
                break;
            default:
                break;
        }
    }

    // ---- 鼠标操作（集成 UIA） ----
    private void MClick(int x, int y, int btn, int mode, bool useUIA, string uiaProc)
    {
        bool clicked = false;

        // 1. 尝试 UIA（仅单击模式）
        if (useUIA && mode == 0)
        {
            bool flag = btn switch
            {
                0 => FlaUIHelper.ClickAt(x, y, 0, uiaProc),
                1 => FlaUIHelper.ClickAt(x, y, 1, uiaProc),
                2 => FlaUIHelper.ClickAt(x, y, 2, uiaProc),
                _ => false
            };
            if (flag) return; // 成功，不移动鼠标
        }

        // 2. 若 UIA 未成功，回退到传统模拟
        if (!clicked)
        {
            Cursor.Position = new Point(x, y);
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
    }

    private void KPress(int vk, int mod, int mode)
    {
        // 处理修饰键
        if (mod != 0)
        {
            Keys mk = 0;
            if ((mod & 1) != 0) mk = Keys.Control;
            else if ((mod & 2) != 0) mk = Keys.Shift;
            else if ((mod & 4) != 0) mk = Keys.Alt;
            if (mk != 0 && (mode == 0 || mode == 1))
                FlaUIHelper.PressKey(mk);  // 使用 FlaUIHelper
        }

        // 主键
        Keys key = (Keys)vk;
        if (mode == 0 || mode == 1)
            FlaUIHelper.PressKey(key);
        if (mode == 0 || mode == 2)
            FlaUIHelper.ReleaseKey(key);

        // 释放修饰键
        if (mod != 0 && (mode == 0 || mode == 2))
        {
            Keys mk = 0;
            if ((mod & 1) != 0) mk = Keys.Control;
            else if ((mod & 2) != 0) mk = Keys.Shift;
            else if ((mod & 4) != 0) mk = Keys.Alt;
            if (mk != 0)
                FlaUIHelper.ReleaseKey(mk);
        }
    }

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
        simulCts?.Cancel();
        simulCts?.Dispose();
        tmr.Tick -= Tick;
        tmr.Dispose();
        dTmr.Tick -= DTick;
        dTmr.Dispose();
    }
}