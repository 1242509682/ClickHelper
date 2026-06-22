using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClickHelper;

/// <summary> 全屏截图窗体（仿QQ截图风格，支持调整选区） </summary>
public class SnapForm : Form
{
    #region 字段与属性
    public Bitmap? GetImage { get; private set; }

    private Bitmap scrBmp;
    private Point mPos;
    private Point sPoint;
    private Rectangle selRect;
    private bool seling;
    private int magSize = 60;
    private int zoom = 2;
    private int rsMode;
    private Point rsStart;
    private Rectangle rsOrig;
    private bool isResizing;
    #endregion

    #region 构造与初始化
    public SnapForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Color.Black;
        ShowInTaskbar = false;
        KeyPreview = true;

        scrBmp = CapScreen();

        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        KeyDown += OnKeyDown;
        Paint += OnPaint;
    }
    #endregion

    #region 截图与屏幕捕获
    private Bitmap CapScreen()
    {
        var bounds = SystemInformation.VirtualScreen;
        var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp))
            g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
        return bmp;
    }

    private void Captured()
    {
        if (selRect.Width > 5 && selRect.Height > 5)
        {
            using var bmp = new Bitmap(selRect.Width, selRect.Height);
            using (var g = Graphics.FromImage(bmp))
                g.CopyFromScreen(selRect.X, selRect.Y, 0, 0, selRect.Size);
            GetImage = new Bitmap(bmp);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
    #endregion

    #region 鼠标事件处理
    private void OnMouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (!selRect.IsEmpty && !seling && !isResizing)
            {
                int mode = GetResizeMode(e.Location);
                if (mode != 0)
                {
                    rsMode = mode;
                    rsStart = e.Location;
                    rsOrig = selRect;
                    isResizing = true;
                    seling = false;
                    return;
                }
            }

            if (isResizing) isResizing = false;
            sPoint = e.Location;
            seling = true;
            selRect = new Rectangle(e.Location, new Size(0, 0));
            Invalidate();
        }
        else if (e.Button == MouseButtons.Right)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        mPos = e.Location;

        if (isResizing)
        {
            int dx = e.X - rsStart.X;
            int dy = e.Y - rsStart.Y;
            int newX = rsOrig.X, newY = rsOrig.Y, newW = rsOrig.Width, newH = rsOrig.Height;
            switch (rsMode)
            {
                case 1: newY = Math.Max(0, rsOrig.Y + dy); newH = rsOrig.Height - dy; break;
                case 2: newH = Math.Max(1, rsOrig.Height + dy); break;
                case 3: newX = Math.Max(0, rsOrig.X + dx); newW = rsOrig.Width - dx; break;
                case 4: newW = Math.Max(1, rsOrig.Width + dx); break;
                case 5:
                    newX = Math.Max(0, rsOrig.X + dx); newY = Math.Max(0, rsOrig.Y + dy);
                    newW = rsOrig.Width - dx; newH = rsOrig.Height - dy; break;
                case 6:
                    newY = Math.Max(0, rsOrig.Y + dy); newW = Math.Max(1, rsOrig.Width + dx);
                    newH = rsOrig.Height - dy; break;
                case 7:
                    newX = Math.Max(0, rsOrig.X + dx); newH = Math.Max(1, rsOrig.Height + dy);
                    newW = rsOrig.Width - dx; break;
                case 8: newW = Math.Max(1, rsOrig.Width + dx); newH = Math.Max(1, rsOrig.Height + dy); break;
            }
            if (newX + newW > scrBmp.Width) newW = scrBmp.Width - newX;
            if (newY + newH > scrBmp.Height) newH = scrBmp.Height - newY;
            selRect = new Rectangle(newX, newY, newW, newH);
            Invalidate();
            return;
        }

        if (seling)
        {
            int x = Math.Min(e.X, sPoint.X);
            int y = Math.Min(e.Y, sPoint.Y);
            int w = Math.Abs(e.X - sPoint.X);
            int h = Math.Abs(e.Y - sPoint.Y);
            selRect = new Rectangle(x, y, w, h);
            Invalidate();
        }
        else
        {
            Invalidate();
        }
    }

    private void OnMouseUp(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (isResizing)
            {
                isResizing = false;
                rsMode = 0;
                Invalidate();
                return;
            }
            if (seling)
            {
                seling = false;
                if (selRect.Width < 5 || selRect.Height < 5)
                    selRect = Rectangle.Empty;
                else
                {
                    if (selRect.X < 0) selRect.X = 0;
                    if (selRect.Y < 0) selRect.Y = 0;
                    if (selRect.Right > scrBmp.Width) selRect.Width = scrBmp.Width - selRect.X;
                    if (selRect.Bottom > scrBmp.Height) selRect.Height = scrBmp.Height - selRect.Y;
                }
                Invalidate();
            }
        }
    }

    private int GetResizeMode(Point p)
    {
        if (selRect.IsEmpty || !selRect.Contains(p)) return 0;
        const int edge = 8;
        int left = selRect.X, right = selRect.Right;
        int top = selRect.Y, bottom = selRect.Bottom;
        bool nearLeft = Math.Abs(p.X - left) <= edge;
        bool nearRight = Math.Abs(p.X - right) <= edge;
        bool nearTop = Math.Abs(p.Y - top) <= edge;
        bool nearBottom = Math.Abs(p.Y - bottom) <= edge;

        if (nearLeft && nearTop) return 5;
        if (nearRight && nearTop) return 6;
        if (nearLeft && nearBottom) return 7;
        if (nearRight && nearBottom) return 8;
        if (nearLeft) return 3;
        if (nearRight) return 4;
        if (nearTop) return 1;
        if (nearBottom) return 2;
        return 0;
    }
    #endregion

    #region 键盘事件处理
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
        else if (e.KeyCode == Keys.Enter)
        {
            Captured();
        }
        else if (e.KeyCode == Keys.C && !seling && !isResizing)
        {
            CopyColor();
        }
        else if (e.KeyCode == Keys.B && !seling && !isResizing && selRect.Width > 5 && selRect.Height > 5)
        {
            DoOcr();
            e.Handled = true;
        }
    }
    #endregion

    #region 绘制与UI渲染
    private void OnPaint(object? sender, PaintEventArgs e)
    {
        e.Graphics.DrawImage(scrBmp, 0, 0, scrBmp.Width, scrBmp.Height);

        if (selRect.Width > 0 && selRect.Height > 0)
        {
            using (var brush = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
            {
                Region clip = new Region(new Rectangle(0, 0, Width, Height));
                clip.Exclude(selRect);
                e.Graphics.Clip = clip;
                e.Graphics.FillRectangle(brush, ClientRectangle);
                e.Graphics.ResetClip();
            }

            using (var pen = new Pen(Color.DodgerBlue, 2))
                e.Graphics.DrawRectangle(pen, selRect);

            DrawSizeInfo(e.Graphics);
            DrawBottomHint(e.Graphics);
        }
        else
        {
            using (var brush = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                e.Graphics.FillRectangle(brush, ClientRectangle);
            string hint = "左键拖拽选择区域  |  右键/ESC退出";
            using (var font = new Font("Segoe UI", 12, FontStyle.Regular))
            using (var fg = new SolidBrush(Color.White))
            {
                var sz = e.Graphics.MeasureString(hint, font);
                int x = (Width - (int)sz.Width) / 2;
                int y = (Height - (int)sz.Height) / 2;
                e.Graphics.DrawString(hint, font, fg, x, y);
            }
        }

        DrawMag(e.Graphics);
        DrawInfo(e.Graphics);
    }

    private void DrawSizeInfo(Graphics g)
    {
        string sizeInfo = $"{selRect.Width} × {selRect.Height}";
        using var font = new Font("Segoe UI", 11, FontStyle.Bold);
        var sz = g.MeasureString(sizeInfo, font);
        int textWidth = (int)sz.Width + 8;
        int textHeight = (int)sz.Height + 8;

        int x = selRect.X + (selRect.Width - textWidth) / 2;
        int y = selRect.Bottom + 10;

        if (y + textHeight > Height)
        {
            y = selRect.Top - textHeight - 10;
            if (y < 0)
            {
                y = selRect.Top + (selRect.Height - textHeight) / 2;
                x = selRect.Right + 10;
                if (x + textWidth > Width)
                {
                    x = selRect.Left - textWidth - 10;
                    if (x < 0) x = 10;
                }
            }
        }

        using (var bg = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
        using (var fg = new SolidBrush(Color.White))
        {
            var rect = new Rectangle(x, y, textWidth, textHeight);
            g.FillRectangle(bg, rect);
            g.DrawString(sizeInfo, font, fg, x + 4, y + 4);
        }
    }

    private void DrawBottomHint(Graphics g)
    {
        var ocrYes = OcrHelper.Engine != null ? "   按 B 识别文字   " : " 按 B 识别文字 (需加载) ";
        string tipText = $"按 Enter 截图   |{ocrYes}|   按 C 复制色号   |   右键/ESC 取消";
        using var font = new Font("Segoe UI", 10, FontStyle.Regular);
        var sz = g.MeasureString(tipText, font);
        int textWidth = (int)sz.Width + 8;
        int textHeight = (int)sz.Height + 8;
        int tipX = (Width - textWidth) / 2;
        int tipY = Height - textHeight - 30;

        Rectangle tipRect = new Rectangle(tipX, tipY, textWidth, textHeight);
        if (selRect.IntersectsWith(tipRect))
            return;

        using (var bg = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
        using (var fg = new SolidBrush(Color.White))
        {
            g.FillRectangle(bg, tipRect);
            g.DrawString(tipText, font, fg, tipX + 4, tipY + 4);
        }
    }

    private void DrawMag(Graphics g)
    {
        int cx = mPos.X, cy = mPos.Y;
        int half = magSize / 2;
        int offX = 20;
        int offY = 20;

        int srcX = cx - half / zoom;
        int srcY = cy - half / zoom;
        int srcW = magSize / zoom;
        int srcH = magSize / zoom;
        srcX = Math.Clamp(srcX, 0, scrBmp.Width - srcW);
        srcY = Math.Clamp(srcY, 0, scrBmp.Height - srcH);
        Rectangle srcRect = new Rectangle(srcX, srcY, srcW, srcH);

        int destX = cx + offX;
        int destY = cy + offY;
        var destRect = new Rectangle(destX, destY, magSize, magSize);

        if (destRect.Right > Width) destRect.X = Width - destRect.Width - 2;
        if (destRect.Bottom > Height) destRect.Y = Height - destRect.Height - 2;
        if (destRect.X < 0) destRect.X = 2;
        if (destRect.Y < 0) destRect.Y = 2;

        using (var path = new System.Drawing.Drawing2D.GraphicsPath())
        {
            path.AddEllipse(destRect);
            g.SetClip(path);
            g.DrawImage(scrBmp, destRect, srcRect, GraphicsUnit.Pixel);
            g.ResetClip();
        }

        using (var pen = new Pen(Color.LightGray, 2))
            g.DrawEllipse(pen, destRect);

        int centerX = destRect.X + destRect.Width / 2;
        int centerY = destRect.Y + destRect.Height / 2;
        using (var pen = new Pen(Color.White, 1))
        {
            g.DrawLine(pen, centerX - 10, centerY, centerX + 10, centerY);
            g.DrawLine(pen, centerX, centerY - 10, centerX, centerY + 10);
        }
    }

    private void DrawInfo(Graphics g)
    {
        Color pixel;
        if (mPos.X >= 0 && mPos.X < scrBmp.Width &&
            mPos.Y >= 0 && mPos.Y < scrBmp.Height)
            pixel = scrBmp.GetPixel(mPos.X, mPos.Y);
        else
            pixel = Color.Black;

        string text = $"({mPos.X},{mPos.Y})";

        using (var font = new Font("Consolas", 10, FontStyle.Bold))
        {
            var sz = g.MeasureString(text, font);
            int w = (int)sz.Width + 24;
            int h = (int)sz.Height + 6;

            int x = mPos.X - w - 10;
            int y = mPos.Y + 20;
            if (x < 0) x = mPos.X + 10;
            if (y + h > Height) y = mPos.Y - h - 10;
            if (x + w > Width) x = Width - w - 10;
            if (y < 0) y = 10;

            using (var bg = new SolidBrush(Color.FromArgb(200, 40, 40, 40)))
            {
                var rect = new Rectangle(x, y, w, h);
                g.FillRectangle(bg, rect);
            }

            using (var cb = new SolidBrush(pixel))
            {
                var cr = new Rectangle(x + 4, y + 4, 16, 16);
                g.FillRectangle(cb, cr);
                g.DrawRectangle(Pens.White, cr);
            }

            using (var fg = new SolidBrush(Color.White))
            {
                g.DrawString(text, font, fg, x + 24, y + 4);
            }
        }
    }
    #endregion

    #region 辅助功能
    private void DoOcr()
    {
        if (OcrHelper.Engine == null)
        {
            Task.Run(() =>
            {
                var ok = OcrHelper.Init();
                Invoke(() =>
                {
                    if (!ok)
                    {
                        MessageBox.Show("OCR 未找到语言模型与依赖库请点击下载", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        DialogResult = DialogResult.None;
                        Close();
                        return;
                    }
                    MessageBox.Show("OCR 文字识别引擎已加载完成..", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            });
        }

        string? text = OcrHelper.RecogRect(selRect);
        if (string.IsNullOrEmpty(text))
        {
            MessageBox.Show("未识别到文字。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var edit = new OcrForm(text);
        edit.TopMost = true;
        if (edit.ShowDialog() == DialogResult.OK)
        {
            if (!string.IsNullOrEmpty(edit.FinalText))
                Clipboard.SetText(edit.FinalText);
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private void CopyColor()
    {
        if (mPos.X >= 0 && mPos.X < scrBmp.Width &&
            mPos.Y >= 0 && mPos.Y < scrBmp.Height)
        {
            Color pixel = scrBmp.GetPixel(mPos.X, mPos.Y);
            string hex = $"#{pixel.R:X2}{pixel.G:X2}{pixel.B:X2}";
            Clipboard.SetText(hex);
            var tip = new ToolTip();
            tip.Show($"已复制 {hex}", this, mPos.X + 20, mPos.Y + 20, 1000);
        }
    }
    #endregion

    #region 资源释放
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            scrBmp?.Dispose();
        }
        base.Dispose(disposing);
    }
    #endregion
}