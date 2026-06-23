using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FlaUI.Core.Conditions;
using static ClickHelper.Program;
using Point = System.Drawing.Point;
using Timer = System.Windows.Forms.Timer;

namespace ClickHelper;

/// <summary> 点击核心 </summary>
internal class Core
{
    #region 字段与属性
    private Timer tmr;
    private Timer dTmr;
    private int idx;
    private bool run;
    private bool delay;
    private object lck = new();
    private Task? simulTask;
    private CancellationTokenSource? simulCts;
    private bool isBatchRunning = false;
    private static bool cvMissingShown = false;

    public int CurIdx => idx;
    public bool Running => run;
    public event Action? Stopped;
    #endregion

    #region 构造与初始化
    public Core()
    {
        tmr = new Timer { Interval = cfg.IntervalMs };
        tmr.Tick += Tick;
        dTmr = new Timer();
        dTmr.Tick += DTick;
        idx = 0;
        run = false;
        delay = false;
    }
    #endregion

    #region 启动/停止控制
    public void Start()
    {
        // 缺失OpenCvSharp 不启动与图像识别有关的任务
        if (cfg.PosList.Any(p => p.UseImage && !Check.IsReady()))
        {
            using var dlg = new MissingCvForm();
            dlg.ShowDialog();
            var main = Application.OpenForms.OfType<Main>().FirstOrDefault();
            main?.SetStat("图像匹配不可用，请安装OpenCvSharp依赖");
            return; 
        }

        // 缺失 OCR 模型（文字识别依赖）不启用任务
        if (cfg.PosList.Any(p => p.UseTxt && !OcrHelper.Init()))
        {
            var main = Application.OpenForms.OfType<Main>().FirstOrDefault();
            main?.SetStat("OCR语言模型缺失，文字识别失败");
            return;
        }

        lock (lck)
        {
            if (run) return;

            // 每次启动重置，确保下次启动仍会检测
            cvMissingShown = false;  

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
    #endregion

    #region 定时器回调与步进
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
    #endregion

    #region 执行逻辑（Exec + 图像/文字匹配）
    private void Exec(Config.PosData pos)
    {
        // ---- 文字匹配 + 图像匹配 ----
        if (pos.ImageTemp != null && pos.ImageTemp.Length > 0)
        {
            if (!pos.UseImage) return;

            var rect = ImgMatch.FindPoint(pos.ImageTemp, pos.Threshold);
            if (!rect.HasValue) return;

            // 截图匹配成功后，若操作类型为文本输入，则走 UIA 输入路径
            if (pos.ActType == 2 && !string.IsNullOrEmpty(pos.TextContent))
            {
                var elem = FlaUIHelper.GetElemPt(rect.Value.X, rect.Value.Y);
                if (elem != null && FlaUIHelper.SetTextDirect(elem, pos.TextContent))
                    return;
                // 若元素不支持直接设置，则尝试向上查找可输入父级
                var parent = elem?.FindFirst(FlaUI.Core.Definitions.TreeScope.Ancestors, TrueCondition.Default);
                if (parent != null && FlaUIHelper.SetTextDirect(parent, pos.TextContent))
                    return;
                // 最后回退全局键盘输入
                FlaUIHelper.TypeText(pos.TextContent);
                return;
            }

            if (pos.UseTxt && !string.IsNullOrEmpty(pos.TxtMatch))
            {
                using var tempBmp = ImgMatch.Bytes2Bmp(pos.ImageTemp);
                if (tempBmp == null) return; // 模板无效则放弃

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
                            MClick(cx, cy, pos);
                            return;
                        }
                    }
                }
            }
            else
            {
                // ---- 图像匹配 ----
                MClick(rect.Value.X, rect.Value.Y, pos);
            }
            return;
        }

        // ---- 普通坐标 / 键盘 / 文本 / 组合键 ----
        switch (pos.ActType)
        {
            case 0: // 鼠标点击
                int clickX = pos.X == 0 && pos.Y == 0 ? Cursor.Position.X : pos.X;
                int clickY = pos.X == 0 && pos.Y == 0 ? Cursor.Position.Y : pos.Y;
                MClick(clickX, clickY, pos);
                break;

            case 1: // 键盘按键
                KPress(pos.ActKey, pos.ModKey, pos.OpMode);
                break;

            case 2: // 文本输入
                if (!string.IsNullOrEmpty(pos.TextContent))
                {
                    bool done = false;
                    if (pos.UseUIA && pos.Targets.Count > 0)
                    {
                        foreach (var t in pos.Targets)
                        {
                            // ★ 使用新命名：SetTextToWin（原 SetTextToWindow）
                            if (FlaUIHelper.SetTextToWin(t, pos.TextContent, pos.AutoId, pos.UName, pos.ClassN))
                            {
                                done = true;
                                break;
                            }
                        }
                    }
                    if (!done)
                    {
                        // 回退到全局键盘输入
                        FlaUIHelper.TypeText(pos.TextContent);
                    }
                }
                break;

            case 3: // 组合键
                if (!string.IsNullOrEmpty(pos.ComboKeys))
                {
                    var keys = FlaUIHelper.ParseKeys(pos.ComboKeys);
                    if (keys.Count > 0)
                        FlaUIHelper.TypeSimult(keys.ToArray());
                }
                break;
        }
    }
    #endregion

    #region 鼠标/键盘操作
    private void MClick(int x, int y, Config.PosData pos)
    {
        if (pos.Targets != null && pos.Targets.Count > 0)
        {
            var handles = WinApi.GetHandles(pos.Targets);
            foreach (var hwnd in handles)
            {
                if (pos.UseUIA && (!string.IsNullOrEmpty(pos.AutoId) || !string.IsNullOrEmpty(pos.UName) || !string.IsNullOrEmpty(pos.ClassN)))
                {
                    // 优先使用基于 hwnd 的 UIA 点击（更精确）
                    bool ok = FlaUIHelper.ClickHwnd(hwnd, x, y, pos.ActKey);
                    if (ok) continue;

                    // 回退：尝试按进程名/属性匹配（兼容旧逻辑）
                    try
                    {
                        WinApi.GetWindowThreadProcessId(hwnd, out uint pid);
                        if (pid != 0)
                        {
                            using var p = System.Diagnostics.Process.GetProcessById((int)pid);
                            ok = FlaUIHelper.ClickProp(p.ProcessName, pos.AutoId, pos.UName, pos.ClassN);
                            if (ok) continue;
                        }
                    }
                    catch { }
                }
                WinApi.SendClick(hwnd, x, y, pos.ActKey, pos.OpMode);
            }
            return;
        }

        Cursor.Position = new Point(x, y);
        int btn = pos.ActKey;
        int mode = pos.OpMode;
        if (btn == 0) { if (mode == 0 || mode == 1) WinApi.LeftDown(); if (mode == 0 || mode == 2) WinApi.LeftUp(); }
        else if (btn == 1) { if (mode == 0 || mode == 1) WinApi.MiddleDown(); if (mode == 0 || mode == 2) WinApi.MiddleUp(); }
        else if (btn == 2) { if (mode == 0 || mode == 1) WinApi.RightDown(); if (mode == 0 || mode == 2) WinApi.RightUp(); }
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
                FlaUIHelper.PressKey(mk);
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
    #endregion

    #region 坐标管理（增删改）
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
    #endregion

    #region 资源释放
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
    #endregion
}