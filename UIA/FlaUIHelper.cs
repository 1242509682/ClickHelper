using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace ClickHelper;

/// <summary>
/// UIA 辅助类：基于 FlaUI 实现控件操作，支持后台点击、后台输入
/// 优先使用 UIA 模式（不移动鼠标、不激活窗口），失败后回退键盘模拟
/// </summary>
public static class FlaUIHelper
{
    private static readonly UIA3Automation auto;          // UIA 自动化对象
    private static readonly ConditionFactory cf;          // 条件工厂，用于构建查找条件

    static FlaUIHelper()
    {
        try
        {
            auto = new UIA3Automation();
            cf = auto?.ConditionFactory;
        }
        catch
        {
            auto = null;
            cf = null;
        }
    }

    #region 点击功能
    /// <summary> 执行点击（支持进程过滤，优先使用 UIA 模式）</summary>
    /// <param name="x">屏幕 X 坐标</param>
    /// <param name="y">屏幕 Y 坐标</param>
    /// <param name="btn">0=左键,1=中键,2=右键</param>
    /// <param name="proc">进程名（过滤用，留空则不过滤）</param>
    /// <returns>是否成功</returns>
    public static bool ClickAt(int x, int y, int btn, string proc = "")
    {
        if (auto == null) return false;

        try
        {
            // 获取指定坐标处的 UI 元素
            var elem = auto.FromPoint(new Point(x, y));
            if (elem == null) return false;

            // 进程过滤：若指定了进程名，检查元素所属进程是否匹配
            if (!string.IsNullOrEmpty(proc))
            {
                var pid = elem.Properties.ProcessId.Value;
                try
                {
                    using var p = Process.GetProcessById(pid);
                    if (p.ProcessName != proc)
                        return false;
                }
                catch { return false; }
            }

            // 左键优先尝试调用模式（不移动鼠标）
            if (btn == 0)
            {
                if (TryInvoke(elem)) return true;
            }

            // 回退：移动鼠标点击
            return ClickRestore(elem, btn);
        }
        catch (Exception ex)
        {
            Logger.Log(ex, "FlaUIHelper.ClickAt");
            return false;
        }
    }

    /// <summary> 尝试调用 Invoke/Toggle/SelectionItem 模式（不移动鼠标）</summary>
    private static bool TryInvoke(AutomationElement elem)
    {
        var invoke = elem.Patterns.Invoke;
        if (invoke?.Pattern != null) { invoke.Pattern.Invoke(); return true; }

        var toggle = elem.Patterns.Toggle;
        if (toggle?.Pattern != null) { toggle.Pattern.Toggle(); return true; }

        var select = elem.Patterns.SelectionItem;
        if (select?.Pattern != null) { select.Pattern.Select(); return true; }

        return false;
    }

    /// <summary> 瞬移鼠标点击后恢复原位置</summary>
    private static bool ClickRestore(AutomationElement elem, int btn)
    {
        if (!elem.TryGetClickablePoint(out var pt)) return false;

        var oldPos = Mouse.Position;
        try
        {
            Mouse.Position = pt;
            var button = btn switch
            {
                0 => MouseButton.Left,
                1 => MouseButton.Middle,
                2 => MouseButton.Right,
                _ => MouseButton.Left
            };
            Mouse.Click(button);
            return true;
        }
        finally
        {
            Mouse.Position = oldPos;
        }
    }
    #endregion

    #region 窗口操作
    /// <summary> 根据进程名获取主窗口（支持超时）</summary>
    public static Window? GetMainWindow(string proc, int timeoutMs = 2000)
    {
        if (auto == null) return null;
        try
        {
            var procs = Process.GetProcessesByName(proc);
            if (procs.Length == 0) return null;

            var app = FlaUI.Core.Application.Attach(procs[0]);
            return app.GetMainWindow(auto, TimeSpan.FromMilliseconds(timeoutMs));
        }
        catch (Exception ex)
        {
            Logger.Log(ex, "FlaUIHelper.GetMainWindow");
            return null;
        }
    }

    /// <summary> 查找窗口中的文本输入控件（支持 Edit 和 Document 类型）</summary>
    public static AutomationElement? FindEdit(Window win, string? aid = null, string? name = null, string? cls = null)
    {
        if (win == null || cf == null) return null;

        // 文本控件类型：Edit（标准输入框）和 Document（记事本、浏览器富文本）
        var textTypes = new OrCondition(
            cf.ByControlType(ControlType.Edit),
            cf.ByControlType(ControlType.Document)
        );

        // 构建额外条件
        var extras = new List<ConditionBase>();
        if (!string.IsNullOrEmpty(aid)) extras.Add(cf.ByAutomationId(aid));
        if (!string.IsNullOrEmpty(name)) extras.Add(cf.ByName(name));
        if (!string.IsNullOrEmpty(cls)) extras.Add(cf.ByClassName(cls));

        // 无额外条件时，直接查找文本控件
        if (extras.Count == 0)
            return win.FindFirstDescendant(textTypes);

        // 有额外条件时，组合为 And 条件
        var allConds = new List<ConditionBase> { textTypes };
        allConds.AddRange(extras);
        return win.FindFirstDescendant(new AndCondition(allConds.ToArray()));
    }

    #endregion

    #region 文本输入
    /// <summary> 向目标窗口输入文本（优先 UIA，失败则键盘模拟）</summary>
    public static bool SetTextToWin(string proc, string text, string? aid = null, string? name = null, string? cls = null)
    {
        if (string.IsNullOrEmpty(proc)) return false;

        // 1. 获取主窗口
        var win = GetMainWindow(proc);
        if (win == null) return false;

        // 2. 查找编辑框
        var elem = FindEdit(win, aid, name, cls);
        if (elem != null)
        {
            // 3. UIA 直接设置文本（不移动鼠标）
            if (SetTextDirect(elem, text))
                return true;
        }

        // 4. 回退：键盘模拟
        return SetTextByKey(proc, text);
    }

    /// <summary> 通过 UIA ValuePattern 直接设置文本（不移动鼠标）</summary>
    public static bool SetTextDirect(AutomationElement elem, string text)
    {
        if (elem == null) return false;

        try
        {
            // 优先 ValuePattern（支持大多数输入框）
            var val = elem.Patterns.Value;
            if (val?.Pattern != null)
            {
                val.Pattern.SetValue(text);
                return true;
            }

            // 尝试 TextBox（FlaUI 封装）
            var box = elem.AsTextBox();
            if (box != null)
            {
                box.Text = text;
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Logger.Log(ex, "FlaUIHelper.SetTextDirect");
            return false;
        }
    }

    /// <summary> 通过激活窗口 + 键盘模拟输入（回退方案）</summary>
    public static bool SetTextByKey(string proc, string text)
    {
        try
        {
            // 获取窗口句柄
            var hwnd = WinApi.FindWindow(null, proc);
            if (hwnd == nint.Zero)
            {
                var procs = Process.GetProcessesByName(proc);
                if (procs.Length == 0) return false;
                hwnd = procs[0].MainWindowHandle;
                if (hwnd == nint.Zero) return false;
            }

            // 激活窗口并等待焦点
            WinApi.SetForegroundWindow(hwnd);
            WaitFocus(hwnd);

            // 清空内容（Ctrl+A + Delete）
            TypeSimult(Keys.Control, Keys.A);
            TypeKey(Keys.Delete);

            // 输入文本
            TypeText(text);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Log(ex, "FlaUIHelper.SetTextByKey");
            return false;
        }
    }

    /// <summary> 等待窗口获得焦点（最大 500ms）</summary>
    private static void WaitFocus(nint hwnd, int maxMs = 500)
    {
        int elapsed = 0, step = 50;
        while (elapsed < maxMs)
        {
            if (WinApi.GetForegroundWindow() == hwnd) break;
            System.Threading.Thread.Sleep(step);
            elapsed += step;
        }
    }
    #endregion

    #region 键盘模拟
    /// <summary> 模拟键盘输入文本（全局）</summary>
    public static void TypeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try { Keyboard.Type(text); }
        catch (Exception ex) { Logger.Log(ex, "FlaUIHelper.TypeText"); }
    }

    /// <summary> 按下指定键（不释放）</summary>
    public static void PressKey(Keys key)
    {
        try { Keyboard.Press((VirtualKeyShort)key); }
        catch (Exception ex) { Logger.Log(ex, "FlaUIHelper.PressKey"); }
    }

    /// <summary> 释放指定键</summary>
    public static void ReleaseKey(Keys key)
    {
        try { Keyboard.Release((VirtualKeyShort)key); }
        catch (Exception ex) { Logger.Log(ex, "FlaUIHelper.ReleaseKey"); }
    }

    /// <summary> 按下并释放单个键</summary>
    public static void TypeKey(Keys key)
    {
        try { Keyboard.Type((VirtualKeyShort)key); }
        catch (Exception ex) { Logger.Log(ex, "FlaUIHelper.TypeKey"); }
    }

    /// <summary> 同时按下多个键（用于组合键）</summary>
    public static void TypeSimult(params Keys[] keys)
    {
        if (keys == null || keys.Length == 0) return;
        try
        {
            var vks = Array.ConvertAll(keys, k => (VirtualKeyShort)k);
            Keyboard.TypeSimultaneously(vks);
        }
        catch (Exception ex) { Logger.Log(ex, "FlaUIHelper.TypeSimult"); }
    }

    /// <summary> 依次按下多个键</summary>
    public static void TypeSeq(params Keys[] keys)
    {
        if (keys == null || keys.Length == 0) return;
        try
        {
            var vks = Array.ConvertAll(keys, k => (VirtualKeyShort)k);
            Keyboard.Type(vks);
        }
        catch (Exception ex) { Logger.Log(ex, "FlaUIHelper.TypeSeq"); }
    }

    /// <summary> 执行组合键字符串（如 "Ctrl+C"）</summary>
    public static void TypeCombo(string combo)
    {
        if (string.IsNullOrEmpty(combo)) return;
        var keys = WinApi.ParseHotKeyToKeys(combo);
        if (keys.Count > 0)
            TypeSimult(keys.ToArray());
    }
    #endregion

    #region UIA 辅助
    /// <summary> 通过窗口句柄点击（后台）</summary>
    public static bool ClickHwnd(nint hwnd, int x, int y, int btn)
    {
        if (auto == null) return false;
        try
        {
            var elem = auto.FromHandle(hwnd);
            if (elem == null) return false;

            if (btn == 0 && TryInvoke(elem)) return true;

            if (elem.TryGetClickablePoint(out var pt))
            {
                var oldPos = Mouse.Position;
                Mouse.Position = pt;
                var mbtn = btn == 0 ? MouseButton.Left : btn == 1 ? MouseButton.Middle : MouseButton.Right;
                Mouse.Click(mbtn);
                Mouse.Position = oldPos;
                return true;
            }
            return false;
        }
        catch { return false; }
    }

    /// <summary> 通过属性点击（按进程名 + 属性）</summary>
    public static bool ClickProp(string proc, string? aid = null, string? name = null, string? cls = null)
    {
        if (auto == null) return false;
        try
        {
            var win = GetMainWindow(proc);
            if (win == null) return false;

            var conds = new List<ConditionBase>();
            if (!string.IsNullOrEmpty(aid)) conds.Add(cf.ByAutomationId(aid));
            if (!string.IsNullOrEmpty(name)) conds.Add(cf.ByName(name));
            if (!string.IsNullOrEmpty(cls)) conds.Add(cf.ByClassName(cls));
            if (conds.Count == 0) return false;

            var elem = win.FindFirstDescendant(new AndCondition(conds.ToArray()));
            if (elem == null) return false;

            if (TryInvoke(elem)) return true;
            return ClickRestore(elem, 0);
        }
        catch { return false; }
    }

    /// <summary> 根据坐标获取 UI 元素</summary>
    public static AutomationElement GetElemPt(int x, int y)
    {
        if (auto == null) return null;
        try { return auto.FromPoint(new Point(x, y)); }
        catch { return null; }
    }

    /// <summary> 通过进程名+属性设置文本（兼容旧接口）</summary>
    public static bool SetTxtProp(string proc, string aid, string name, string cls, string text)
    {
        var win = GetMainWindow(proc);
        if (win == null) return false;

        var elem = FindEdit(win, aid, name, cls);
        if (elem == null) return false;

        return SetTextDirect(elem, text);
    }
    #endregion
}