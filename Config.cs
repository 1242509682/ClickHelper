using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace ClickHelper;

/// <summary> 配置数据 </summary>
internal class Config
{
    [JsonProperty("跳过关于", Order = -10)] public bool SkipAbout { get; set; } = false;
    [JsonProperty("点击间隔", Order = -9)] public int IntervalMs { get; set; } = 200;
    [JsonProperty("循环次数", Order = -8)] public int PosLoopCount { get; set; } = -1;
    [JsonProperty("启动热键", Order = -7)] public int ClickHotKey { get; set; } = (int)Keys.F9;
    [JsonProperty("同时执行", Order = -6)] public bool SimulExec { get; set; } = false;
    [JsonProperty("记录热键", Order = -5)] public int HotKeyAltL { get; set; } = (int)Keys.S;

    [JsonProperty("定时开关", Order = 5)] public bool TimerEnabled { get; set; } = false;
    [JsonProperty("定时模式", Order = 6)] public int TimerMode { get; set; } = 0;   // 0=日期模式, 1=计时模式
    [JsonProperty("定时开始", Order = 7)] public DateTime TimerStart { get; set; } = DateTime.Now;
    [JsonProperty("定时结束", Order = 8)] public DateTime TimerEnd { get; set; } = DateTime.MinValue;
    [JsonProperty("定时时长", Order = 9)] public int TimerDuration { get; set; } = 60;  // 仅计时模式有效
    [JsonProperty("定时目标", Order = 10)] public int TimeType { get; set; } = 0;   // 0=位置列表, 1=宏播放
    [JsonProperty("定时宏", Order = 11)] public string MacroName { get; set; } = "";
    [JsonProperty("定时热键", Order = 12)] public int TimerHotKey { get; set; } = (int)Keys.F8;

    [JsonProperty("宏录制热键", Order = 20)] public int MacRecHotKey { get; set; } = (int)Keys.F6;
    [JsonProperty("宏播放热键", Order = 21)] public int MacPlayHotKey { get; set; } = (int)Keys.F7;
    [JsonProperty("截图热键", Order = 22)] public int SnapHotKey { get; set; } = (int)Keys.A;   // 新增

    [JsonProperty("位置列表", Order = 23)] public List<PosData> PosList { get; set; } = new();

    /// <summary> 单个位置/操作数据 </summary>
    public class PosData
    {
        [JsonProperty("X")] public int X { get; set; }
        [JsonProperty("Y")] public int Y { get; set; }
        [JsonProperty("描述")] public string? Desc { get; set; }
        [JsonProperty("操作类型")] public int ActType { get; set; } = 0;   // 0鼠标 1键盘
        [JsonProperty("操作键")] public int ActKey { get; set; } = 0;
        [JsonProperty("修饰键")] public int ModKey { get; set; } = 0;
        [JsonProperty("操作模式")] public int OpMode { get; set; } = 0;    // 0单击 1按下 2弹起
        [JsonProperty("等待毫秒")] public int WaitMs { get; set; } = 0;
        [JsonProperty("使用图像匹配")] public bool UseImage { get; set; } = false;
        [JsonProperty("图像模板")] public byte[]? ImageTemp { get; set; }
        [JsonProperty("匹配阈值")] public float Threshold { get; set; } = 0.8f;
    }

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
        catch { return new Config(); }
    }
}