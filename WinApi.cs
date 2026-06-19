using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ClickHelper;

/// <summary> Windows API 封装 </summary>
internal class WinApi
{
    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(int x, int y);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern IntPtr FindWindow(string? cls, string? win);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();

    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    // ---- 窗口消息常量（鼠标/键盘） ----
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

    // ---- 热键 ID ----
    public const int HOTKEY_F6 = 1;
    public const int HOTKEY_ALT_L = 2;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    // ---- 鼠标事件标志 ----
    private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
    private const uint MOUSEEVENTF_LEFTUP = 0x04;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x08;
    private const uint MOUSEEVENTF_RIGHTUP = 0x10;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x20;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x40;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;

    // ---- 鼠标模拟（供全局使用） ----
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

    // ---- 键盘模拟（KeyDown/KeyUp 已存在，但增加重载以兼容 Keys） ----
    private const uint KEYEVENTF_KEYDOWN = 0x0000;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public static void KeyDown(Keys key) => keybd_event((byte)key, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
    public static void KeyUp(Keys key) => keybd_event((byte)key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    public static void KeyPress(Keys key) { KeyDown(key); KeyUp(key); }

    // ---- 钩子相关 ----
    public const int WH_MOUSE_LL = 14;
    public const int WH_KEYBOARD_LL = 13;

    [DllImport("user32.dll")]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    public delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // ---- 工具方法：生成常用的热键列表（供下拉框使用） ----
    public static object[] GetCommonKeys()
    {
        return new object[] {
            Keys.F1, Keys.F2, Keys.F3, Keys.F4, Keys.F5, Keys.F6, Keys.F7, Keys.F8,
            Keys.F9, Keys.F10, Keys.F11, Keys.F12,
            Keys.D0, Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9,
            Keys.A, Keys.B, Keys.C, Keys.D, Keys.E, Keys.F, Keys.G, Keys.H, Keys.I, Keys.J,
            Keys.K, Keys.L, Keys.M, Keys.N, Keys.O, Keys.P, Keys.Q, Keys.R, Keys.S, Keys.T,
            Keys.U, Keys.V, Keys.W, Keys.X, Keys.Y, Keys.Z
        };
    }

    public static IntPtr MakeLParam(int x, int y) => (IntPtr)((y << 16) | (x & 0xFFFF));
}