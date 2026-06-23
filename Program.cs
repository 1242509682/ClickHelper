using System;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;

namespace ClickHelper;

/// <summary> 程序入口 </summary>
internal static class Program
{
    // 版本号
    private static bool Restart = false;
    public static string ver => "v1.0.5";

    // ★ 全局实例（统一管理，避免多处维护）
    internal static Config cfg = new Config();
    internal static Core? core;
    internal static HotKey? hk;

    [STAThread]
    static void Main()
    {
        if (!Restart)
        {
            // 确保程序只启动一个实例
            bool createdNew;
            using Mutex mutex = new Mutex(true, "ClickHelper_SingleInstance_Mutex", out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("程序已在运行中，请勿重复启动。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
        }

        // ★ 加载配置
        cfg = Config.Load();
        core = new Core();

        WinApi.SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new Main());
    }

    /// <summary>
    /// 重启程序（可用于加载新模型或刷新依赖）
    /// </summary>
    public static void RestartApp()
    {
        Restart = true;

        // 启动新进程
        Process.Start(new ProcessStartInfo
        {
            FileName = Application.ExecutablePath,
            UseShellExecute = true
        });

        // 关闭当前进程
        Application.Exit();
    }
}