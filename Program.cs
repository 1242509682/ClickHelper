using System;
using System.Windows.Forms;

namespace ClickHelper;

internal static class Program
{
    // °æ±¾ºÅ
    public static string version => "v1.0.0";

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}