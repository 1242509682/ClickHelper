using System;
using System.Collections.Generic;
using System.Windows.Forms;
using static ClickHelper.WinApi;

namespace ClickHelper;

/// <summary>
/// 统一管理所有全局热键，支持字符串组合键注册
/// </summary>
public static class HotKeyManager
{
    private static nint _hwnd;
    private static Dictionary<int, (uint mod, Keys key, Action action)> _hotKeys = new();

    /// <summary>
    /// 初始化管理器，必须传入主窗口句柄
    /// </summary>
    public static void Initialize(nint windowHandle)
    {
        _hwnd = windowHandle;
    }

    /// <summary>
    /// 注册一个热键，id 必须唯一（建议使用常量或枚举）
    /// </summary>
    public static bool Register(int id, string hotKeyString, Action action)
    {
        if (_hwnd == nint.Zero) return false;
        Unregister(id); // 先注销旧的

        var (mod, key) = ParseHotKey(hotKeyString);
        if (key == Keys.None) return false;

        bool ok = RegisterHotKey(_hwnd, id, mod, (uint)key);
        if (ok)
        {
            _hotKeys[id] = (mod, key, action);
        }
        return ok;
    }

    /// <summary>
    /// 注销指定 id 的热键
    /// </summary>
    public static void Unregister(int id)
    {
        if (_hotKeys.Remove(id))
            WinApi.UnregisterHotKey(_hwnd, id);
    }

    /// <summary>
    /// 注销所有热键
    /// </summary>
    public static void UnregisterAll()
    {
        foreach (var id in _hotKeys.Keys)
            WinApi.UnregisterHotKey(_hwnd, id);
        _hotKeys.Clear();
    }

    /// <summary>
    /// 处理 WM_HOTKEY 消息，返回是否已处理
    /// </summary>
    public static bool ProcessHotKey(ref Message m)
    {
        if (m.Msg != 0x0312) return false;
        int id = m.WParam.ToInt32();
        if (_hotKeys.TryGetValue(id, out var entry))
        {
            entry.action?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>
    /// 更新已注册热键的字符串（自动重新注册）
    /// </summary>
    public static void Update(int id, string newHotKeyString)
    {
        if (_hotKeys.TryGetValue(id, out var entry))
        {
            // 重新注册，保留原有 Action
            Register(id, newHotKeyString, entry.action);
        }
    }

    /// <summary>
    /// 获取指定 id 的热键字符串（用于显示）
    /// </summary>
    public static string GetHotKeyString(int id)
    {
        if (_hotKeys.TryGetValue(id, out var entry))
            return FormatHotKey(entry.mod, entry.key);
        return "";
    }
}