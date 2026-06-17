using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace ClickHelper;

internal class Config
{
    #region 配置项
    [JsonProperty("点击间隔", Order = 0)]
    public int IntervalMs { get; set; } = 200;
    [JsonProperty("循环次数", Order = 1)]
    public int PosLoopCount { get; set; } = -1;   // -1无限，正整数次数
    [JsonProperty("启动热键", Order = 2)]
    public int HotKeyF6 { get; set; } = (int)Keys.F9;
    [JsonProperty("同时执行", Order = 3)]
    public bool SimulExec { get; set; } = false;
    [JsonProperty("记录热键", Order = 4)]
    public int HotKeyAltL { get; set; } = (int)Keys.A;
    [JsonProperty("位置列表", Order = 5)]
    public List<PosData> PosList { get; set; } = new();

    // ---------- 定时执行 ----------
    [JsonProperty("定时启用", Order = 6)]
    public bool TimerEnabled { get; set; } = false;
    [JsonProperty("定时开始", Order = 7)]
    public DateTime TimerStart { get; set; } = DateTime.Now;
    [JsonProperty("定时结束", Order = 8)]
    public DateTime TimerEnd { get; set; } = DateTime.MinValue;  // MinValue 表示无结束
    #endregion

    public class PosData
    {
        [JsonProperty("X")] public int X { get; set; }
        [JsonProperty("Y")] public int Y { get; set; }
        [JsonProperty("描述")] public string? Desc { get; set; }
        [JsonProperty("操作类型")] public int ActType { get; set; } = 0;
        [JsonProperty("操作键")] public int ActKey { get; set; } = 0;
        [JsonProperty("修饰键")] public int ModKey { get; set; } = 0;
        [JsonProperty("操作模式")] public int OpMode { get; set; } = 0;
    }

    #region 读写文件
    public static readonly string Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config.json");
    public static readonly string ScriptDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backup");

    public void Save()
    {
        var dir = System.IO.Path.GetDirectoryName(Path)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        string json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(Path, json);
    }

    public static Config Load()
    {
        if (!File.Exists(Path))
        {
            var cfg = new Config();
            cfg.Save();
            return cfg;
        }

        try
        {
            string json = File.ReadAllText(Path);
            return JsonConvert.DeserializeObject<Config>(json)!;
        }
        catch
        {
            return new Config();
        }
    }
    #endregion
}