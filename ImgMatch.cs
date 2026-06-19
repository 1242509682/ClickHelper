using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using ImageFinderNS;

namespace ClickHelper;

public static class ImgMatch
{
    public static byte[] Bmp2Bytes(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    public static Bitmap Bytes2Bmp(byte[] data)
    {
        using var ms = new MemoryStream(data);
        return new Bitmap(ms);
    }

    public static Point? FindPoint(byte[] tempData, float thresh = 0.8f)
    {
        try
        {
            Cursor.Hide();

            // 1. 截取主屏幕（避免超过 ImageFinder 最大尺寸 2560x2560）
            using var scrBmp = CaptureScr();
            // 2. 模板图像
            using var tplBmp = Bytes2Bmp(tempData);

            if (tplBmp.Width > scrBmp.Width || tplBmp.Height > scrBmp.Height)
                return null;

            // 3. 设置源图（必须）
            ImageFinder.SetSource(scrBmp);

            // 4. 执行匹配（返回 List<Match>，默认按相似度降序排列）
            var matches = ImageFinder.Find(tplBmp, thresh);

            if (matches != null && matches.Count > 0)
            {
                // 取第一个（最佳匹配）
                var match = matches[0];
                var loc = match.Zone.Location;          // 左上角坐标
                int cx = loc.X + tplBmp.Width / 2;
                int cy = loc.Y + tplBmp.Height / 2;
                return new Point(cx, cy);
            }
            return null;
        }
        finally
        {
            Cursor.Show();
        }
    }

    private static Bitmap CaptureScr()
    {
        // 只截主屏幕，通常不超过 2560x2560，避免超限
        var bounds = Screen.PrimaryScreen.Bounds;
        var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
        return bmp;
    }
}