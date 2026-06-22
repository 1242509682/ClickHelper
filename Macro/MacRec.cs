using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static ClickHelper.WinApi;

namespace ClickHelper;

/// <summary> 宏录制器（全局钩子） </summary>
public class MacRec : IDisposable
{
    private nint mHook = nint.Zero;
    private nint kHook = nint.Zero;
    private LowLevelProc mProc;
    private LowLevelProc kProc;
    private Stopwatch watch;
    private List<MacItem> items;
    private bool rec;
    private HashSet<int> excluded = new HashSet<int>();

    public event Action<MacItem>? OnRecord;

    public void SetExcluded(params int[] keys)
    {
        excluded = new HashSet<int>(keys);
    }

    public void StartRec()
    {
        if (rec) return;
        items = new List<MacItem>();
        watch = Stopwatch.StartNew();
        rec = true;

        mProc = MouseProc;
        kProc = KeyProc;

        using (var p = Process.GetCurrentProcess())
        using (var m = p.MainModule)
        {
            nint hMod = GetModuleHandle(m.ModuleName);
            mHook = SetWindowsHookEx(WH_MOUSE_LL, mProc, hMod, 0);
            kHook = SetWindowsHookEx(WH_KEYBOARD_LL, kProc, hMod, 0);
        }
    }

    public void StopRec()
    {
        if (!rec) return;
        rec = false;
        watch.Stop();
        if (mHook != nint.Zero) { UnhookWindowsHookEx(mHook); mHook = nint.Zero; }
        if (kHook != nint.Zero) { UnhookWindowsHookEx(kHook); kHook = nint.Zero; }
    }

    public List<MacItem> GetItems() => new List<MacItem>(items);

    private nint MouseProc(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && rec)
        {
            var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            int t = (int)watch.ElapsedMilliseconds;
            int x = info.pt.X, y = info.pt.Y;
            uint data = info.mouseData;

            switch ((uint)wParam)
            {
                case WM_MOUSEMOVE:
                    if (items.Count > 0 && items[^1].Act == MacAction.Move &&
                        Math.Abs(items[^1].X - x) < 5 && Math.Abs(items[^1].Y - y) < 5)
                        break;
                    Add(MacAction.Move, x, y, t);
                    break;
                case WM_LBUTTONDOWN: Add(MacAction.LDown, x, y, t); break;
                case WM_LBUTTONUP: Add(MacAction.LUp, x, y, t); break;
                case WM_RBUTTONDOWN: Add(MacAction.RDown, x, y, t); break;
                case WM_RBUTTONUP: Add(MacAction.RUp, x, y, t); break;
                case WM_MBUTTONDOWN: Add(MacAction.MDown, x, y, t); break;
                case WM_MBUTTONUP: Add(MacAction.MUp, x, y, t); break;
                case WM_MOUSEWHEEL:
                    int delta = (short)(data >> 16 & 0xFFFF);
                    Add(MacAction.Wheel, x, y, t, delta: delta);
                    break;
            }
        }
        return CallNextHookEx(mHook, code, wParam, lParam);
    }

    private nint KeyProc(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && rec)
        {
            var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int t = (int)watch.ElapsedMilliseconds;
            Keys key = (Keys)info.vkCode;
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu)
                return CallNextHookEx(kHook, code, wParam, lParam);

            if (excluded.Contains((int)key))
                return CallNextHookEx(kHook, code, wParam, lParam);

            if ((uint)wParam == WM_KEYDOWN) Add(MacAction.KDown, keyCode: (int)key, time: t);
            else if ((uint)wParam == WM_KEYUP) Add(MacAction.KUp, keyCode: (int)key, time: t);
        }
        return CallNextHookEx(kHook, code, wParam, lParam);
    }

    private void Add(MacAction act, int x = 0, int y = 0, int time = 0, int keyCode = 0, int delta = 0)
    {
        var item = new MacItem
        {
            Act = act,
            X = x,
            Y = y,
            Key = keyCode,
            Delta = delta,
            Time = time
        };
        items.Add(item);
        OnRecord?.Invoke(item);
    }

    public void Dispose()
    {
        StopRec();
        mProc = null;
        kProc = null;
    }
}