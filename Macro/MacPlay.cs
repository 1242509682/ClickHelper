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
    private bool playing;

    public bool IsPlay => playing;

    public void Play(MacData data, double speed = 1.0, Action? done = null)
    {
        if (playing) return;
        cts = new CancellationTokenSource();
        var tok = cts.Token;
        playing = true;

        Task.Run(() =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                int idx = 0;
                while (idx < data.Items.Count && !tok.IsCancellationRequested)
                {
                    var it = data.Items[idx];
                    int target = (int)(it.Time / speed);
                    int delay = target - (int)sw.ElapsedMilliseconds;
                    if (delay > 0) Task.Delay(delay, tok).Wait(tok);
                    if (tok.IsCancellationRequested) break;
                    Exec(it);
                    idx++;
                }
                playing = false;
                done?.Invoke();
            }
            catch (OperationCanceledException) { playing = false; }
            catch { playing = false; throw; }
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