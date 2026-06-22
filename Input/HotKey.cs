using System;
using System.Windows.Forms;
using static ClickHelper.WinApi;

namespace ClickHelper;

/// <summary> 注册全局热键，处理启动/记录 </summary>
internal class HotKey
{
    private nint hwnd;
    private Action actStart;
    private Action actRecord;
    private int hkF6;
    private int hkAlt;

    public HotKey(nint windowHandle, Action f6Action, Action altLAction, int f6Vk, int altLVk)
    {
        hwnd = windowHandle;
        actStart = f6Action;
        actRecord = altLAction;
        hkF6 = f6Vk;
        hkAlt = altLVk;
        Register();
    }

    public void Register()
    {
        Unregister();
        RegisterHotKey(hwnd, HOTKEY_F6, MOD_NOREPEAT, (uint)hkF6);
        RegisterHotKey(hwnd, HOTKEY_ALT_L, MOD_ALT, (uint)hkAlt);
    }

    public void Unregister()
    {
        RegisterHotKey(hwnd, HOTKEY_F6);
        RegisterHotKey(hwnd, HOTKEY_ALT_L);
    }

    public void UpdateKeys(int newF6, int newAlt)
    {
        hkF6 = newF6;
        hkAlt = newAlt;
        Register();
    }

    public bool ProcessHotKey(ref Message m)
    {
        if (m.Msg == 0x0312) // WM_HOTKEY
        {
            int id = m.WParam.ToInt32();
            if (id == HOTKEY_F6)
            {
                actStart?.Invoke();
                return true;
            }
            else if (id == HOTKEY_ALT_L)
            {
                actRecord?.Invoke();
                return true;
            }
        }
        return false;
    }
}