using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using OpenCvSharp;

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

    public static System.Drawing.Point? FindTemp(byte[] tempData, float thresh = 0.8f)
    {
        try
        {
            // 隐藏鼠标指针，避免鼠标图标干扰模板匹配
            Cursor.Hide();

            var screen = CaptureScr();
            byte[] scrBytes;
            using (var ms = new MemoryStream()) { screen.Save(ms, ImageFormat.Png); scrBytes = ms.ToArray(); }
            using var tempMat = Mat.FromImageData(tempData, ImreadModes.Color);
            using var scrMat = Mat.FromImageData(scrBytes, ImreadModes.Color);
            if (tempMat.Width > scrMat.Width || tempMat.Height > scrMat.Height) return null;
            using var result = new Mat();
            Cv2.MatchTemplate(scrMat, tempMat, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);
            if (maxVal >= thresh)
            {
                int cx = maxLoc.X + tempMat.Width / 2;
                int cy = maxLoc.Y + tempMat.Height / 2;
                return new System.Drawing.Point(cx, cy);
            }
            return null;
        }
        finally
        {
            // 确保鼠标指针恢复显示
            Cursor.Show();
        }
    }

    private static Bitmap CaptureScr()
    {
        var bounds = SystemInformation.VirtualScreen;
        var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
        return bmp;
    }
}