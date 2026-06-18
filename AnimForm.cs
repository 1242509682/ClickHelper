using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClickHelper;

/// <summary>
/// 多米诺骨牌效应动画：圆环始终有一个缺口，缺口顺时针旋转，
/// 缺口大小逐渐变化（出现时缺口缩小至消失，消失时缺口扩大）。
/// 颜色：蓝→橙（出现），橙→蓝（消失），形成完整循环。
/// </summary>
internal class AnimForm : Form
{
    // ---- 动画状态 ----
    private Point pos;
    private Timer tm;
    private float rad;
    private int step;
    private int phase;
    private int alpha;
    private float rotAng;
    private float gap;
    private float colProg;
    private const int MaxS = 60;
    private float maxR;
    private float pw;
    private float ds;
    private float dpiS;
    private const float StartAng = -90f;

    public AnimForm(Point p)
    {
        Color bg = GetColor(p);

        using (Graphics g = this.CreateGraphics())
        {
            dpiS = g.DpiX / 96f;
        }
        if (dpiS < 1f) dpiS = 1f;

        maxR = 16f * dpiS;
        pw = 1.5f * dpiS;
        ds = 3.0f * dpiS;

        float mar = ds * 2.0f;
        float sz = (maxR + mar) * 2f;
        int fw = (int)Math.Ceiling(sz);
        int fh = fw;
        int fx = p.X - fw / 2;
        int fy = p.Y - fh / 2;

        this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        this.SetStyle(ControlStyles.UserPaint, true);
        this.SetStyle(ControlStyles.Opaque, false);
        this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        this.DoubleBuffered = true;

        this.FormBorderStyle = FormBorderStyle.None;
        this.ShowInTaskbar = false;
        this.TopMost = true;
        this.BackColor = bg;
        this.TransparencyKey = bg;
        this.SetBounds(fx, fy, fw, fh);

        pos = new Point(p.X - fx, p.Y - fy);

        rad = 0;
        step = 0;
        phase = 0;
        alpha = 255;
        rotAng = 0;
        gap = 360;
        colProg = 0;

        tm = new Timer { Interval = 10 };
        tm.Tick += (s, e) =>
        {
            step++;
            if (step > MaxS)
            {
                if (phase == 0)
                {
                    phase = 1;
                    step = 0;
                    rad = maxR;
                    gap = 0;
                    rotAng = 360;
                    alpha = 255;
                    colProg = 1f;
                    this.Invalidate();
                    return;
                }
                else
                {
                    tm.Stop();
                    this.Close();
                    return;
                }
            }

            float prog = step / (float)MaxS;
            float ease = 1 - (float)Math.Pow(1 - prog, 2);

            rad = maxR;

            if (phase == 0)
            {
                gap = 360f * (1 - ease);
                rotAng = 360f * ease;
                alpha = 255;
                colProg = ease;
            }
            else
            {
                gap = 360f * ease;
                rotAng = 360f + 360f * ease;
                alpha = (int)(255 * (1 - ease));
                if (alpha < 0) alpha = 0;
                colProg = 1f - ease;
            }

            this.Invalidate();
        };

        this.Paint += OnPaint;
        this.Shown += (s, e) => tm.Start();
    }

    private static Color GetColor(Point pos)
    {
        try
        {
            using (var bmp = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(pos.X, pos.Y, 0, 0, new Size(1, 1));
                return bmp.GetPixel(0, 0);
            }
        }
        catch { return Color.Gray; }
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x00080000;
            return cp;
        }
    }

    private void OnPaint(object sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        if (rad < 1 || alpha < 1) return;

        int r = (int)Math.Round(rad);
        float sweep = 360f - gap;
        if (sweep < 0.5f) return;

        float start = StartAng + rotAng;

        // 颜色插值：蓝 (0,180,255) ↔ 橙 (255,180,0)
        Color sCol = Color.FromArgb(0, 180, 255);
        Color eCol = Color.FromArgb(255, 180, 0);
        float c = colProg;
        int rc = (int)(sCol.R + (eCol.R - sCol.R) * c);
        int gc = (int)(sCol.G + (eCol.G - sCol.G) * c);
        int bc = (int)(sCol.B + (eCol.B - sCol.B) * c);

        Color aCol = Color.FromArgb(alpha, rc, gc, bc);
        Color dCol = Color.FromArgb(alpha, Math.Min(255, rc + 30), Math.Min(255, gc + 20), Math.Min(255, bc + 20));
        Color gCol = Color.FromArgb((int)(alpha * 0.25f), rc / 2, gc / 2, bc / 2);

        using (Pen pen = new Pen(aCol, pw))
        {
            pen.Alignment = PenAlignment.Center;
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            g.DrawArc(pen, pos.X - r, pos.Y - r, r * 2, r * 2, start, sweep);
        }

        float endA = start + sweep;
        float radA = endA * (float)Math.PI / 180f;
        float hx = pos.X + r * (float)Math.Cos(radA);
        float hy = pos.Y + r * (float)Math.Sin(radA);

        using (SolidBrush br = new SolidBrush(dCol))
        {
            float sz = ds;
            g.FillEllipse(br, hx - sz / 2, hy - sz / 2, sz, sz);
        }
        using (SolidBrush glow = new SolidBrush(gCol))
        {
            float gsz = ds * 1.8f;
            g.FillEllipse(glow, hx - gsz / 2, hy - gsz / 2, gsz, gsz);
        }
    }

    protected override void Dispose(bool d)
    {
        if (d) tm?.Dispose();
        base.Dispose(d);
    }
}