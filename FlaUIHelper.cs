using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using FlaUI.Core.AutomationElements;
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

    /// <summary>
    /// 执行点击（支持进程过滤）
    /// </summary>
    /// <param name="x">屏幕 X</param>
    /// <param name="y">屏幕 Y</param>
    /// <param name="btn">0=左,1=中,2=右</param>
    /// <param name="proc">目标进程名（可选，为空则不限制）</param>
    /// <returns>是否成功</returns>
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
}