using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace ClickHelper;

/// <summary> 配置数据 </summary>
internal class Config
{
    [JsonProperty("跳过关于", Order = -11)] 
    public bool SkipAbout { get; set; } = false;
    [JsonProperty("显示瞄准", Order = -10)]
    public bool ShowAim { get; set; } = true;
    [JsonProperty("点击间隔", Order = -9)] 
    public int IntervalMs { get; set; } = 500;
    [JsonProperty("循环次数", Order = -8)]
    public int PosLoopCount { get; set; } = -1;
    [JsonProperty("启动热键", Order = -7)]
    public string ClickHotKey { get; set; } = "F9";
    [JsonProperty("同时执行", Order = -6)]
    public bool SimulExec { get; set; } = false;
    [JsonProperty("记录热键", Order = -5)]
    public string RecordHotKey { get; set; } = "Alt+S";


    [JsonProperty("定时开关", Order = 5)] 
    public bool TimerEnabled { get; set; } = false;
    [JsonProperty("定时模式", Order = 6)] 
    public int TimerMode { get; set; } = 0;
    [JsonProperty("定时开始", Order = 7)]
    public DateTime TimerStart { get; set; } = DateTime.Now;
    [JsonProperty("定时结束", Order = 8)] 
    public DateTime TimerEnd { get; set; } = DateTime.MinValue;
    [JsonProperty("定时时长", Order = 9)] 
    public int TimerDuration { get; set; } = 60;
    [JsonProperty("定时目标", Order = 10)] 
    public int TimeType { get; set; } = 0;
    [JsonProperty("定时宏", Order = 11)] 
    public string MacroName { get; set; } = "";
    [JsonProperty("定时热键", Order = 12)]
    public string TimerHotKey { get; set; } = "F8";

    [JsonProperty("宏录制热键", Order = 20)]
    public string MacRecHotKey { get; set; } = "F6";
    [JsonProperty("宏播放热键", Order = 21)]
    public string MacPlayHotKey { get; set; } = "F7";
    [JsonProperty("截图热键", Order = 22)]
    public string SnapHotKey { get; set; } = "Alt+A";

    [JsonProperty("坐标管理表", Order = 23)] 
    public List<PosData> PosList { get; set; } = new();

    /// <summary> 单个位置/操作数据 </summary>
    public class PosData
    {
        [JsonProperty("X")] 
        public int X { get; set; }
        [JsonProperty("Y")] 
        public int Y { get; set; }
        [JsonProperty("描述")]
        public string? Desc { get; set; }
        [JsonProperty("操作类型")] 
        public int ActType { get; set; } = 0;
        [JsonProperty("操作键")] 
        public int ActKey { get; set; } = 0;
        [JsonProperty("修饰键")] 
        public int ModKey { get; set; } = 0;
        [JsonProperty("操作模式")] 
        public int OpMode { get; set; } = 0;
        [JsonProperty("等待毫秒")] 
        public int WaitMs { get; set; } = 0;

        [JsonProperty("使用图像匹配")]
        public bool UseImage { get; set; } = false;
        [JsonProperty("图像模板")] 
        public byte[]? ImageTemp { get; set; }
        [JsonProperty("匹配阈值")] 
        public float Threshold { get; set; } = 0.6f;

        [JsonProperty("使用文字")] 
        public bool UseTxt { get; set; } = false;
        [JsonProperty("匹配文字")] 
        public string? TxtMatch { get; set; }
        [JsonProperty("文字模式")] 
        public int TxtMode { get; set; } = 0;
        [JsonProperty("文字阈值")] 
        public float TxtThresh { get; set; } = 0.8f;
        [JsonProperty("识别文字选项")]
        public OcrOpt OcrOptions { get; set; } = new OcrOpt();

        // ----- 重构 UIA 多窗口与属性 -----
        [JsonProperty("启用UIA")]
        public bool UseUIA { get; set; } = false;
        [JsonProperty("目标窗口列表")]
        public List<string> Targets { get; set; } = new();
        [JsonProperty("UIA自动化ID")] 
        public string? AutoId { get; set; }
        [JsonProperty("UIA名称")] 
        public string? UName { get; set; }
        [JsonProperty("UIA类名")] 
        public string? ClassN { get; set; }

        // 文本输入 / 组合键
        [JsonProperty("文本内容")] 
        public string TextContent { get; set; } = "";
        [JsonProperty("组合键")] 
        public string ComboKeys { get; set; } = "Ctrl+C";
    }

    #region 识别文字选项属性
    public class OcrOpt
    {
        [JsonProperty("检测阈值")]
        public float DetThr { get; set; } = 0.2f;
        [JsonProperty("文本框阈值")]
        public float BoxThr { get; set; } = 0.4f;
        [JsonProperty("扩张比例")]
        public float Unclip { get; set; } = 1.8f;
        [JsonProperty("批大小")]
        public int Batch { get; set; } = 6;
        [JsonProperty("识别阈值")]
        public float RecThr { get; set; } = 0.5f;
        [JsonProperty("线程数")]
        public int BatchPoolSize { get; set; } = 1;
        [JsonProperty("缩放边长")]
        public int LimitSideLen { get; set; } = 960;
        [JsonProperty("得分模式")] // 0=FAST, 1=SLOW
        public int ScoreMode { get; set; } = 0;
        [JsonProperty("使用膨胀")]
        public bool UseDilation { get; set; } = false;
    }
    #endregion

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