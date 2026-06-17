using System.Collections.Generic;

namespace ClickHelper.Macro;

/// <summary> 宏转位置列表 </summary>
public static class MacConv
{
    internal static List<Config.PosData> Convert(MacData data)
    {
        var list = new List<Config.PosData>();
        int prev = 0;
        foreach (var it in data.Items)
        {
            int wait = it.Time - prev;
            if (wait < 0) wait = 0;
            if (it.Act == MacAction.LDown || it.Act == MacAction.LUp ||
                it.Act == MacAction.RDown || it.Act == MacAction.RUp ||
                it.Act == MacAction.MDown || it.Act == MacAction.MUp)
            {
                int btn = 0;
                if (it.Act == MacAction.RDown || it.Act == MacAction.RUp) btn = 2;
                else if (it.Act == MacAction.MDown || it.Act == MacAction.MUp) btn = 1;
                int mode = it.Act == MacAction.LDown || it.Act == MacAction.RDown || it.Act == MacAction.MDown ? 1 : 2;
                list.Add(new Config.PosData
                {
                    X = it.X,
                    Y = it.Y,
                    ActType = 0,
                    ActKey = btn,
                    OpMode = mode,
                    WaitMs = wait
                });
                prev = it.Time;
            }
            else if (it.Act == MacAction.KDown || it.Act == MacAction.KUp)
            {
                int mode = it.Act == MacAction.KDown ? 1 : 2;
                list.Add(new Config.PosData
                {
                    X = 0,
                    Y = 0,
                    ActType = 1,
                    ActKey = it.Key,
                    ModKey = 0,
                    OpMode = mode,
                    WaitMs = wait
                });
                prev = it.Time;
            }
            // 忽略移动和滚轮（可扩展）
        }
        return list;
    }
}