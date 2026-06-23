using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace ClickHelper;

public static class Logger
{
    private static readonly string LogDir = Path.Combine(Application.StartupPath, "Log");
    private static readonly object Lock = new();

    // ★ 调试开关：发布时设为 false，开发时设为 true
    public static bool DebugMode { get; set; } = true;

    /// <summary>
    /// 记录异常到日志文件
    /// </summary>
    public static void Log(Exception ex, string? Msg = null)
    {
        lock (Lock)
        {
            if (!Directory.Exists(LogDir))
                Directory.CreateDirectory(LogDir);
            string datePart = DateTime.Now.ToString("yyyyMMdd");
            string logFile = Path.Combine(LogDir, $"{datePart}.txt");
            string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string msg = Msg ?? ex.Message;
            string entry = $"{time} - {msg}\n{ex.StackTrace}\n";
            File.AppendAllText(logFile, entry + Environment.NewLine);
        }
    }

    /// <summary>
    /// 记录调试信息（仅当 DebugMode = true 时生效）
    /// </summary>
    public static void Debug(string tag, string message)
    {
        if (!DebugMode) return;
        lock (Lock)
        {
            if (!Directory.Exists(LogDir))
                Directory.CreateDirectory(LogDir);
            string logFile = Path.Combine(LogDir, $"{DateTime.Now:yyyyMMdd}_debug.txt");
            string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string entry = $"{time} [{tag}] {message}";
            File.AppendAllText(logFile, entry + Environment.NewLine);
        }
    }

    /// <summary>
    /// 记录方法进入（自动记录调用者信息）
    /// </summary>
    public static void Enter([System.Runtime.CompilerServices.CallerMemberName] string? methodName = null)
    {
        if (!DebugMode) return;
        Debug("ENTER", $"→ {methodName}");
    }

    /// <summary>
    /// 记录方法退出（自动记录调用者信息）
    /// </summary>
    public static void Exit([System.Runtime.CompilerServices.CallerMemberName] string? methodName = null)
    {
        if (!DebugMode) return;
        Debug("EXIT", $"← {methodName}");
    }

    /// <summary>
    /// 记录关键变量的值
    /// </summary>
    public static void Var(string name, object? value)
    {
        if (!DebugMode) return;
        Debug("VAR", $"{name} = {value ?? "null"}");
    }

    /// <summary>
    /// 记录操作成功
    /// </summary>
    public static void Success(string message)
    {
        if (!DebugMode) return;
        Debug("✅", message);
    }

    /// <summary>
    /// 记录操作失败
    /// </summary>
    public static void Fail(string message)
    {
        if (!DebugMode) return;
        Debug("❌", message);
    }

    /// <summary>
    /// 记录警告
    /// </summary>
    public static void Warn(string message)
    {
        if (!DebugMode) return;
        Debug("⚠️", message);
    }

    /// <summary>
    /// 记录详细信息
    /// </summary>
    public static void Info(string message)
    {
        if (!DebugMode) return;
        Debug("ℹ️", message);
    }
}