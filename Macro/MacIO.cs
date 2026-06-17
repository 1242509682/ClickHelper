using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ClickHelper.Macro;

/// <summary> 宏文件读写（TXT格式） </summary>
public static class MacIO
{
    private static string _dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Macros");

    static MacIO()
    {
        if (!Directory.Exists(_dir)) Directory.CreateDirectory(_dir);
    }

    public static string[] GetMacroNames()
    {
        var files = Directory.GetFiles(_dir, "*.txt");
        var names = new string[files.Length];
        for (int i = 0; i < files.Length; i++)
            names[i] = Path.GetFileNameWithoutExtension(files[i]);
        return names;
    }

    public static MacData Load(string name)
    {
        string path = Path.Combine(_dir, name + ".txt");
        if (!File.Exists(path)) return null;

        var lines = File.ReadAllLines(path, Encoding.UTF8);
        var data = new MacData { Name = name };
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            if (!int.TryParse(parts[0], out int time)) continue;
            var actStr = parts[1];
            var item = new MacItem { Time = time };
            switch (actStr)
            {
                case "Move":
                    if (parts.Length < 4) continue;
                    if (int.TryParse(parts[2], out int x) && int.TryParse(parts[3], out int y))
                    { item.Act = MacAction.Move; item.X = x; item.Y = y; }
                    break;
                case "LDown": item.Act = MacAction.LDown; TryParseXY(parts, item); break;
                case "LUp": item.Act = MacAction.LUp; TryParseXY(parts, item); break;
                case "RDown": item.Act = MacAction.RDown; TryParseXY(parts, item); break;
                case "RUp": item.Act = MacAction.RUp; TryParseXY(parts, item); break;
                case "MDown": item.Act = MacAction.MDown; TryParseXY(parts, item); break;
                case "MUp": item.Act = MacAction.MUp; TryParseXY(parts, item); break;
                case "Wheel":
                    if (parts.Length >= 3 && int.TryParse(parts[2], out int delta))
                    { item.Act = MacAction.Wheel; item.Delta = delta; }
                    break;
                case "KDown":
                    if (parts.Length >= 3 && int.TryParse(parts[2], out int kd))
                    { item.Act = MacAction.KDown; item.Key = kd; }
                    break;
                case "KUp":
                    if (parts.Length >= 3 && int.TryParse(parts[2], out int ku))
                    { item.Act = MacAction.KUp; item.Key = ku; }
                    break;
                default: continue;
            }
            data.Items.Add(item);
        }
        return data;
    }

    private static void TryParseXY(string[] parts, MacItem item)
    {
        if (parts.Length >= 4 && int.TryParse(parts[2], out int x) && int.TryParse(parts[3], out int y))
        { item.X = x; item.Y = y; }
    }

    public static void Save(MacData data)
    {
        if (string.IsNullOrEmpty(data.Name)) data.Name = "未命名";
        string path = Path.Combine(_dir, data.Name + ".txt");
        var lines = new List<string>();
        foreach (var it in data.Items)
        {
            string line = $"{it.Time} ";
            switch (it.Act)
            {
                case MacAction.Move: line += $"Move {it.X} {it.Y}"; break;
                case MacAction.LDown: line += $"LDown {it.X} {it.Y}"; break;
                case MacAction.LUp: line += $"LUp {it.X} {it.Y}"; break;
                case MacAction.RDown: line += $"RDown {it.X} {it.Y}"; break;
                case MacAction.RUp: line += $"RUp {it.X} {it.Y}"; break;
                case MacAction.MDown: line += $"MDown {it.X} {it.Y}"; break;
                case MacAction.MUp: line += $"MUp {it.X} {it.Y}"; break;
                case MacAction.Wheel: line += $"Wheel {it.Delta}"; break;
                case MacAction.KDown: line += $"KDown {it.Key}"; break;
                case MacAction.KUp: line += $"KUp {it.Key}"; break;
            }
            lines.Add(line);
        }
        File.WriteAllLines(path, lines, Encoding.UTF8);
    }

    public static void Delete(string name)
    {
        string path = Path.Combine(_dir, name + ".txt");
        if (File.Exists(path)) File.Delete(path);
    }
}