using System;
using System.IO;

namespace ClickHelper;

public static class Check
{
    public static bool IsReady()
    {
        string root = AppDomain.CurrentDomain.BaseDirectory;
        string dll1 = Path.Combine(root, "OpenCvSharp.dll");
        string dll2 = Path.Combine(root, "OpenCvSharpExtern.dll");
        string dll3 = Path.Combine(root, "runtimes", "win-x64", "native", "OpenCvSharpExtern.dll");
        return File.Exists(dll1) && (File.Exists(dll2) || File.Exists(dll3));
    }
}