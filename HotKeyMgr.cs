using System;
using System.Windows.Forms;
using static ClickHelper.WinApi;

namespace ClickHelper;

internal class HotKeyMgr
{
    private IntPtr hwnd;
    private Action onF6;
    private Action onAltL;
    private int hotKeyF6;
    private int hotKeyAltL;

    public HotKeyMgr(IntPtr windowHandle, Action f6Action, Action altLAction, int f6Vk, int altLVk)
    {
        hwnd = windowHandle;
        onF6 = f6Action;
        onAltL = altLAction;
        hotKeyF6 = f6Vk;
        hotKeyAltL = altLVk;
        Register();
    }

    public void Register()
    {
        Unregister();
        RegisterHotKey(hwnd, HOTKEY_F6, MOD_NOREPEAT, (uint)hotKeyF6);
        RegisterHotKey(hwnd, HOTKEY_ALT_L, MOD_ALT, (uint)hotKeyAltL);
    }

    public void Unregister()
    {
        UnregisterHotKey(hwnd, HOTKEY_F6);
        UnregisterHotKey(hwnd, HOTKEY_ALT_L);
    }

    public void UpdateKeys(int newF6, int newAltL)
    {
        hotKeyF6 = newF6;
        hotKeyAltL = newAltL;
        Register();
    }

    public bool ProcessHotKey(ref Message m)
    {
        if (m.Msg == 0x0312) // WM_HOTKEY
        {
            int id = m.WParam.ToInt32();
            if (id == HOTKEY_F6)
            {
                onF6?.Invoke();
                return true;
            }
            else if (id == HOTKEY_ALT_L)
            {
                onAltL?.Invoke();
                return true;
            }
        }
        return false;
    }
}