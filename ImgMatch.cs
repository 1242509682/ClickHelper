using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using OpenCvSharp;

namespace ClickHelper;

public static class ImgMatch
{
    /// <summary>
    /// 基于 OpenCvSharp 的模板匹配（无尺寸限制，高精度）
    /// </summary>
    /// <param name="tempData">模板图像字节数组（PNG/JPEG 等）</param>
    /// <param name="thresh">相似度阈值 0~1</param>
    /// <returns>匹配中心点（屏幕绝对坐标），未找到返回 null</returns>
    public static System.Drawing.Point? FindPoint(byte[] tempData, float thresh = 0.8f)
    {
        try
        {
            Cursor.Hide();

            using var temp = Cv2.ImDecode(tempData, ImreadModes.Color);
            if (temp == null || temp.Empty())
                return null;

            using var scrBmp = CaptureScreen();
            using var srcMat = BitmapToMat(scrBmp);

            if (temp.Width > srcMat.Width || temp.Height > srcMat.Height)
                return null;

            using var result = new Mat();

            // 输入参数显式转换为 InputArray，输出直接传递 result（Mat）
            using (InputArray srcIa = srcMat)
            using (InputArray tempIa = temp)
            {
                Cv2.MatchTemplate(srcIa, tempIa, result, TemplateMatchModes.CCoeffNormed);
            }

            // 查找最佳匹配位置
            using (InputArray resultIa = result)
            {
                Cv2.MinMaxLoc(resultIa, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);
                if (maxVal < thresh)
                    return null;

                int cx = maxLoc.X + temp.Width / 2;
                int cy = maxLoc.Y + temp.Height / 2;
                return new System.Drawing.Point(cx, cy);
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex, "ImgMatch.FindPoint (OpenCV)");
            return null;
        }
        finally
        {
            Cursor.Show();
        }
    }

    /// <summary>
    /// 使用 GDI+ 截取主屏幕
    /// </summary>
    private static Bitmap CaptureScreen()
    {
        var bounds = SystemInformation.VirtualScreen;
        var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
        return bmp;
    }

    /// <summary>
    /// 将 System.Drawing.Bitmap 转换为 OpenCvSharp.Mat（自动处理 RGB→BGR）
    /// </summary>
    public static Mat BitmapToMat(Bitmap bmp)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, bmp.PixelFormat);
        try
        {
            int bytesPerPixel = Image.GetPixelFormatSize(bmp.PixelFormat) / 8;
            MatType type = bytesPerPixel switch
            {
                3 => MatType.CV_8UC3,
                4 => MatType.CV_8UC4,
                _ => throw new NotSupportedException($"不支持的像素格式: {bmp.PixelFormat}")
            };
            // 使用 Mat.FromPixelData（避免已弃用的构造函数）
            var mat = Mat.FromPixelData(bmp.Height, bmp.Width, type, data.Scan0, data.Stride);
            // 转换颜色通道：System.Drawing 使用 RGB，OpenCV 使用 BGR
            if (bytesPerPixel == 3 || bytesPerPixel == 4)
                Cv2.CvtColor(mat, mat, ColorConversionCodes.RGB2BGR);
            return mat;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    // 保留原有方法 用于减少调用Mat类隐式转换 InputArray 可能造成内存堆积
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
}