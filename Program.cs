using System;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;

namespace ClickHelper;

internal static class Program
{
    public static string ver => "v1.0.6";

    internal static Config cfg = new Config();
    internal static Core? core;

    // 静态互斥体，便于释放
    private static Mutex? mutex;

    [STAThread]
    static void Main()
    {
        bool flag;
        // 使用 Global\ 前缀使互斥体跨会话生效
        mutex = new Mutex(true, @"Global\ClickHelper_SingleInstance_Mutex", out flag);

        if (!flag)
        {
            MessageBox.Show("点击助手正在运行，请勿重复启动。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        cfg = Config.Load();
        core = new Core();

        WinApi.SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new Main());
    }

    public static void RestartApp()
    {
        // ★ 释放互斥体，允许新进程获取
        mutex?.ReleaseMutex();
        mutex?.Dispose();
        mutex = null;

        Process.Start(new ProcessStartInfo
        {
            FileName = Application.ExecutablePath,
            UseShellExecute = true
        });

        Application.Exit();
    }
}