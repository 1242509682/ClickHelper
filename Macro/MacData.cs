using System.Collections.Generic;

namespace ClickHelper.Macro;

/// <summary> 宏动作类型 </summary>
public enum MacAction
{
    Move, LDown, LUp, RDown, RUp, MDown, MUp, Wheel, KDown, KUp
}

/// <summary> 单条宏指令 </summary>
public class MacItem
{
    public MacAction Act { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Key { get; set; }      // 虚拟键码
    public int Delta { get; set; }    // 滚轮增量
    public int Time { get; set; }     // 相对起始毫秒
}

/// <summary> 宏数据容器 </summary>
public class MacData
{
    public string Name { get; set; } = "新宏";
    public List<MacItem> Items { get; set; } = new();
    public int Total => Items.Count > 0 ? Items[^1].Time : 0;
}