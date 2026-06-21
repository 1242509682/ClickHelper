using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClickHelper
{
    public partial class AboutForm
    {
        // ---- 内部动画控件（与之前相同，无改动） ----
        private class AnimPanel : Panel
        {
            private Timer animTim;
            private float rotAng;
            private float gap;
            private int step;
            private int phase;
            private const int MaxS = 60;

            public AnimPanel()
            {
                this.DoubleBuffered = true;
                this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
                this.SetStyle(ControlStyles.UserPaint, true);
                this.SetStyle(ControlStyles.Opaque, false);
                this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);

                rotAng = 0;
                gap = 360;
                step = 0;
                phase = 0;

                animTim = new Timer { Interval = 20 };
                animTim.Tick += (s, e) => TickAnim();
                this.Paint += (s, e) => OnDraw(e.Graphics);
                this.Disposed += (s, e) => animTim?.Stop();
                animTim.Start();
            }

            private void TickAnim()
            {
                step++;
                if (step > MaxS)
                {
                    phase = 1 - phase;
                    step = 0;
                    if (phase == 0)
                    {
                        gap = 360;
                        rotAng = 0;
                    }
                    else
                    {
                        gap = 0;
                        rotAng = 360;
                    }
                    this.Invalidate();
                    return;
                }

                float prog = step / (float)MaxS;
                float ease = 1 - (float)Math.Pow(1 - prog, 2);

                if (phase == 0)
                {
                    gap = 360f * (1 - ease);
                    rotAng = 360f * ease;
                }
                else
                {
                    gap = 360f * ease;
                    rotAng = 360f + 360f * ease;
                }
                this.Invalidate();
            }

            private void OnDraw(Graphics g)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                int size = Math.Min(this.Width, this.Height);
                if (size < 6) return;
                int r = size / 2 - 2;
                float sweep = 360f - gap;
                if (sweep < 0.5f) return;

                float start = -90f + rotAng;

                float c = phase == 0 ? 1 - (float)step / MaxS : (float)step / MaxS;
                Color col1 = Color.FromArgb(0, 180, 255);
                Color col2 = Color.FromArgb(255, 180, 0);
                int rc = (int)(col1.R + (col2.R - col1.R) * c);
                int gc = (int)(col1.G + (col2.G - col1.G) * c);
                int bc = (int)(col1.B + (col2.B - col1.B) * c);

                using (var pen = new Pen(Color.FromArgb(200, rc, gc, bc), 2.5f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawArc(pen, 2, 2, size - 4, size - 4, start, sweep);
                }
            }
        }
    }
}