using System;
using System.Threading;
using System.Windows.Forms;

namespace ClickHelper;

/// <summary> 程序入口，版本号 </summary>
internal static class Program
{
    public static string ver => "v1.0.2";

    [STAThread]
    static void Main()
    {
        // 确保程序只启动一个实例
        bool createdNew;
        using Mutex mutex = new Mutex(true, "ClickHelper_SingleInstance_Mutex", out createdNew);

        if (!createdNew)
        {
            MessageBox.Show("程序已在运行中，请勿重复启动。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        WinApi.SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new Main());
    }
}