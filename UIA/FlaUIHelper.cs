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

// 底层依旧采用的是 Interop.UIAutomationClient
public static class FlaUIHelper
{
    private static readonly UIA3Automation auto;

    static FlaUIHelper()
    {
        try { auto = new UIA3Automation(); }
        catch { auto = null; }
    }

    /// <summary> 执行点击（支持进程过滤）</summary>
    public static bool ClickAt(int x, int y, int btn, string proc = "")
    {
        if (auto == null) return false;

        try
        {
            var elem = auto.FromPoint(new Point(x, y));
            if (elem == null) return false;

            // 进程过滤
            if (!string.IsNullOrEmpty(proc))
            {
                int pid = elem.Properties.ProcessId.Value;
                try
                {
                    var p = Process.GetProcessById(pid);
                    if (p.ProcessName != proc)
                        return false;
                }
                catch { return false; }
            }

            // 左键：优先尝试 Pattern（不移动鼠标）
            if (btn == 0)
            {
                var invoke = elem.Patterns.Invoke;
                if (invoke != null && invoke.Pattern != null)
                {
                    invoke.Pattern.Invoke();
                    return true;
                }
                var toggle = elem.Patterns.Toggle;
                if (toggle != null && toggle.Pattern != null)
                {
                    toggle.Pattern.Toggle();
                    return true;
                }
                var select = elem.Patterns.SelectionItem;
                if (select != null && select.Pattern != null)
                {
                    select.Pattern.Select();
                    return true;
                }
            }

            // 所有按键（包括左键 Pattern 失败后）：瞬移 + 点击 + 恢复
            return ClickRestore(elem, btn);
        }
        catch { return false; }
    }

    private static bool ClickRestore(AutomationElement elem, int btn)
    {
        // 获取可点击点
        if (!elem.TryGetClickablePoint(out var pt))
            return false;

        // 保存原光标位置
        var oldPos = Mouse.Position;
        try
        {
            // 瞬移
            Mouse.Position = pt;
            MouseButton button = btn switch
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
            // 恢复光标位置（即使点击异常也恢复）
            Mouse.Position = oldPos;
        }
    }

    /// <summary> 根据进程名获取主窗口 </summary>
    public static Window? GetMainWindow(string proc)
    {
        if (auto == null) return null;
        try
        {
            var procs = Process.GetProcessesByName(proc);
            if (procs.Length == 0) return null;
            var app = FlaUI.Core.Application.Attach(procs[0]);
            return app.GetMainWindow(auto);
        }
        catch { return null; }
    }

    /// <summary> 向指定窗口输入文本（不移动鼠标）</summary>
    public static bool SetText(Window win, string text)
    {
        try
        {
            var edit = win.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit));
            if (edit == null) return false;
            var box = edit.AsTextBox();
            box.Text = text;
            return true;
        }
        catch { return false; }
    }

    /// <summary> 模拟键盘输入文本（全局）</summary>
    public static void TypeText(string text) => Keyboard.Type(text);

    /// <summary> 模拟按下单个键（不释放）</summary>
    public static void PressKey(Keys key) => Keyboard.Press((VirtualKeyShort)key);

    /// <summary> 模拟释放单个键</summary>
    public static void ReleaseKey(Keys key) => Keyboard.Release((VirtualKeyShort)key);

    /// <summary> 模拟按下并释放单个键</summary>
    public static void TypeKey(Keys key) => Keyboard.Type((VirtualKeyShort)key);

    /// <summary> 模拟同时按下多个键（然后依次释放）</summary>
    public static void TypeSimultaneously(params Keys[] keys)
    {
        if (keys == null || keys.Length == 0) return;
        var vks = Array.ConvertAll(keys, k => (VirtualKeyShort)k);
        Keyboard.TypeSimultaneously(vks);
    }

    /// <summary> 模拟依次按下并释放多个键</summary>
    public static void TypeSequentially(params Keys[] keys)
    {
        if (keys == null || keys.Length == 0) return;
        var vks = Array.ConvertAll(keys, k => (VirtualKeyShort)k);
        Keyboard.Type(vks);
    }

    // 保留原来的 PressKeys 方法，但改用组合键实现
    public static void PressKeys(Keys key1, Keys key2) => TypeSimultaneously(key1, key2);
    // 也可以重载更多参数
    public static void PressKeys(params Keys[] keys) => TypeSimultaneously(keys);

    // 组合键解析辅助方法
    public static List<Keys> ComboKeys(string combo)
    {
        var result = new List<Keys>();
        var parts = combo.Split('+');
        foreach (var p in parts)
        {
            if (Enum.TryParse<Keys>(p.Trim(), true, out var key))
                result.Add(key);
        }
        return result;
    }

    /// <summary> 通过窗口句柄点击（后台，不移动鼠标）</summary>
    public static bool ClickHwnd(nint hwnd, int x, int y, int btn)
    {
        if (auto == null) return false;
        try
        {
            var elem = auto.FromHandle(hwnd);
            if (elem == null) return false;
            var inv = elem.Patterns.Invoke;
            if (inv?.Pattern != null) { inv.Pattern.Invoke(); return true; }
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

    /// 按属性点击（单进程，用于未使用多窗口列表的旧逻辑，但保留）
    public static bool ClickProp(string proc, string? aid = null, string? name = null, string? cls = null)
    {
        if (auto == null) return false;
        try
        {
            var procs = Process.GetProcessesByName(proc);
            if (procs.Length == 0) return false;
            var app = FlaUI.Core.Application.Attach(procs[0]);
            var win = app.GetMainWindow(auto);
            if (win == null) return false;

            var cf = auto.ConditionFactory;
            var conds = new List<ConditionBase>();
            if (!string.IsNullOrEmpty(aid)) conds.Add(cf.ByAutomationId(aid));
            if (!string.IsNullOrEmpty(name)) conds.Add(cf.ByName(name));
            if (!string.IsNullOrEmpty(cls)) conds.Add(cf.ByClassName(cls));
            if (conds.Count == 0) return false;

            // ✅ 使用 AndCondition 组合条件
            var finalCond = new AndCondition(conds.ToArray());
            var elem = win.FindFirstDescendant(finalCond);
            if (elem == null) return false;

            var inv = elem.Patterns.Invoke;
            if (inv?.Pattern != null) { inv.Pattern.Invoke(); return true; }
            return ClickRestore(elem, 0);
        }
        catch { return false; }
    }

    // 根据坐标获取 AutomationElement
    public static AutomationElement GetElemPt(int x, int y)
    {
        if (auto == null) return null;
        try { return auto.FromPoint(new Point(x, y)); }
        catch { return null; }
    }

    // 通过进程名 + 属性查找编辑框并设置文本
    public static bool SetTxtProp(string proc, string aid, string name, string cls, string text)
    {
        if (auto == null) return false;
        try
        {
            var win = GetMainWindow(proc);
            if (win == null) return false;
            var cf = auto.ConditionFactory;
            var conds = new List<ConditionBase>();
            if (!string.IsNullOrEmpty(aid)) conds.Add(cf.ByAutomationId(aid));
            if (!string.IsNullOrEmpty(name)) conds.Add(cf.ByName(name));
            if (!string.IsNullOrEmpty(cls)) conds.Add(cf.ByClassName(cls));
            if (conds.Count == 0) return false;
            var elem = win.FindFirstDescendant(new AndCondition(conds.ToArray()));
            if (elem == null) return false;
            if (elem.ControlType != ControlType.Edit) return false;
            elem.AsTextBox().Text = text;
            return true;
        }
        catch { return false; }
    }

    // 根据 AutomationElement 直接设置文本（支持 ValuePattern 或 TextBox）
    public static bool SetTxtElem(AutomationElement elem, string text)
    {
        try
        {
            if (elem == null) return false;
            var val = elem.Patterns.Value;
            if (val?.Pattern != null) { val.Pattern.SetValue(text); return true; }
            var box = elem.AsTextBox();
            if (box != null) { box.Text = text; return true; }
            return false;
        }
        catch { return false; }
    }
}