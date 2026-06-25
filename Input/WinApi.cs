using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ClickHelper;

internal class WinApi
{
    [DllImport("user32.dll")]
    public static extern nint WindowFromPoint(int x, int y);

    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(nint hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern nint FindWindow(string? cls, string? win);

    [DllImport("user32.dll")]
    public static extern nint SendMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(nint hWnd, int id);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, nuint dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();

    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(nint hWnd);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_RBUTTONDOWN = 0x0204;
    public const uint WM_RBUTTONUP = 0x0205;
    public const uint WM_MBUTTONDOWN = 0x0207;
    public const uint WM_MBUTTONUP = 0x0208;
    public const uint WM_MOUSEMOVE = 0x0200;
    public const uint WM_MOUSEWHEEL = 0x020A;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
    private const uint MOUSEEVENTF_LEFTUP = 0x04;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x08;
    private const uint MOUSEEVENTF_RIGHTUP = 0x10;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x20;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x40;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;

    public static void LeftDown() => mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
    public static void LeftUp() => mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
    public static void RightDown() => mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
    public static void RightUp() => mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
    public static void MiddleDown() => mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, 0);
    public static void MiddleUp() => mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, 0);
    public static void Wheel(int delta) => mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)delta, 0);
    public static void LeftClick() { LeftDown(); LeftUp(); }
    public static void RightClick() { RightDown(); RightUp(); }
    public static void MiddleClick() { MiddleDown(); MiddleUp(); }

    public const int WH_MOUSE_LL = 14;
    public const int WH_KEYBOARD_LL = 13;

    [DllImport("user32.dll")]
    public static extern nint SetWindowsHookEx(int idHook, LowLevelProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    public static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    public static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll")]
    public static extern nint GetModuleHandle(string lpModuleName);

    public delegate nint LowLevelProc(int nCode, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public nint dwExtraInfo;
    }

    public delegate bool EnumWinProc(nint hWnd, nint lParam);
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWinProc lpEnumFunc, nint lParam);
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(nint hWnd);
    [DllImport("user32.dll")]
    public static extern int GetWindowText(nint hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint procId);

    [DllImport("user32.dll")]
    public static extern bool ScreenToClient(nint hWnd, ref POINT pt);

    // ---- 热键解析与格式化（新增） ----
    public static (uint modifiers, Keys key) ParseHotKey(string str)
    {
        uint mods = 0;
        Keys key = Keys.None;
        if (string.IsNullOrWhiteSpace(str)) return (mods, key);

        var parts = str.Split('+', StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            string trimmed = p.Trim();
            if (trimmed.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
                mods |= MOD_CONTROL;
            else if (trimmed.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                mods |= MOD_SHIFT;
            else if (trimmed.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                mods |= MOD_ALT;
            else if (trimmed.Equals("Win", StringComparison.OrdinalIgnoreCase))
                mods |= MOD_WIN;
            else if (Enum.TryParse<Keys>(trimmed, true, out var k))
                key = k;
        }
        return (mods, key);
    }

    public static string FormatHotKey(uint modifiers, Keys key)
    {
        var parts = new List<string>();
        if ((modifiers & MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & MOD_SHIFT) != 0) parts.Add("Shift");
        if ((modifiers & MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & MOD_WIN) != 0) parts.Add("Win");
        if (key != Keys.None) parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    /// <summary>
    /// 将组合键字符串解析为 Keys 列表（用于执行组合键）
    /// 例如 "Ctrl+C" -> [ControlKey, C]
    /// </summary>
    public static List<Keys> ParseHotKeyToKeys(string str)
    {
        var result = new List<Keys>();
        if (string.IsNullOrWhiteSpace(str)) return result;

        var (modifiers, key) = ParseHotKey(str);
        if (key == Keys.None) return result;

        // 按顺序添加修饰键（Ctrl、Shift、Alt）
        if ((modifiers & MOD_CONTROL) != 0) result.Add(Keys.ControlKey);
        if ((modifiers & MOD_SHIFT) != 0) result.Add(Keys.ShiftKey);
        if ((modifiers & MOD_ALT) != 0) result.Add(Keys.Menu);
        // 主键放在最后
        result.Add(key);
        return result;
    }

    public static nint MakeLParam(int x, int y) => (nint)((y << 16) | (x & 0xFFFF));

    public static List<nint> GetHandles(IEnumerable<string> ids)
    {
        var set = new HashSet<nint>();
        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id)) continue;
            foreach (var p in Process.GetProcessesByName(id))
                if (p.MainWindowHandle != nint.Zero)
                    set.Add(p.MainWindowHandle);
            var hwnd = FindWindow(null, id);
            if (hwnd != nint.Zero) set.Add(hwnd);
        }
        return set.ToList();
    }

    public static void SendClick(nint hwnd, int sx, int sy, int btn, int mode)
    {
        var pt = new POINT { X = sx, Y = sy };
        ScreenToClient(hwnd, ref pt);
        uint down, up;
        switch (btn)
        {
            case 0: down = WM_LBUTTONDOWN; up = WM_LBUTTONUP; break;
            case 1: down = WM_MBUTTONDOWN; up = WM_MBUTTONUP; break;
            case 2: down = WM_RBUTTONDOWN; up = WM_RBUTTONUP; break;
            default: return;
        }
        nint lParam = (nint)((pt.Y << 16) | (pt.X & 0xFFFF));
        if (mode == 0 || mode == 1) PostMessage(hwnd, down, nint.Zero, lParam);
        if (mode == 0 || mode == 2) PostMessage(hwnd, up, nint.Zero, lParam);
    }
}