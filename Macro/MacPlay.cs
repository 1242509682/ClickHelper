using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using static ClickHelper.WinApi;

namespace ClickHelper.Macro;

/// <summary> 宏回放引擎 </summary>
public class MacPlay
{
    private CancellationTokenSource cts;

    /// <summary> 是否正在播放 </summary>
    public bool Playing { get; private set; }

    /// <param name="data">宏数据</param>
    /// <param name="loopCount">循环次数：-1无限，0不播放，>0指定次数</param>
    /// <param name="speed">播放速度倍率</param>
    /// <param name="done">播放结束回调（含取消或正常结束）</param>
    public void Play(MacData data, int loopCount = 1, double speed = 1.0, Action? done = null)
    {
        if (Playing) return;
        if (loopCount == 0)
        {
            done?.Invoke();
            return;
        }
        cts = new CancellationTokenSource();
        var tok = cts.Token;
        Playing = true;

        Task.Run(() =>
        {
            try
            {
                int totalCount = 0;
                var sw = Stopwatch.StartNew();
                while (true)
                {
                    sw.Restart();
                    if (loopCount > 0 && totalCount >= loopCount)
                        break;
                    if (tok.IsCancellationRequested)
                        break;

                    int idx = 0;
                    while (idx < data.Items.Count && !tok.IsCancellationRequested)
                    {
                        var it = data.Items[idx];
                        int target = (int)(it.Time / speed);
                        int delay = target - (int)sw.ElapsedMilliseconds;
                        if (delay > 0)
                            Task.Delay(delay, tok).Wait(tok);
                        if (tok.IsCancellationRequested)
                            break;
                        Exec(it);
                        idx++;
                    }
                    totalCount++;

                    if (loopCount == -1)
                        continue;
                }
                Playing = false;
                done?.Invoke();
            }
            catch (OperationCanceledException)
            {
                Playing = false;
            }
            catch
            {
                Playing = false;
                throw;
            }
        }, tok);
    }

    public void Stop() => cts?.Cancel();

    private void Exec(MacItem it)
    {
        switch (it.Act)
        {
            case MacAction.Move: SetCursorPos(it.X, it.Y); break;
            case MacAction.LDown: LeftDown(); break;
            case MacAction.LUp: LeftUp(); break;
            case MacAction.RDown: RightDown(); break;
            case MacAction.RUp: RightUp(); break;
            case MacAction.MDown: MiddleDown(); break;
            case MacAction.MUp: MiddleUp(); break;
            case MacAction.Wheel: Wheel(it.Delta); break;
            case MacAction.KDown: KeyDown((System.Windows.Forms.Keys)it.Key); break;
            case MacAction.KUp: KeyUp((System.Windows.Forms.Keys)it.Key); break;
        }
    }
}