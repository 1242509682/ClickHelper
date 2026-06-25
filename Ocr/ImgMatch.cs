using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using OpenCvSharp;

namespace ClickHelper;

/// <summary>
/// 图像匹配工具类，封装了基于 OpenCvSharp 的模板匹配和图像格式转换功能。
/// 本类利用 <see cref="MatPool"/> 对象池复用 Mat 对象，显著降低非托管内存分配，
/// 配合 <see cref="FindPoint"/> 方法在截屏匹配场景下实现高性能、低内存的稳定运行。
/// </summary>
public static class ImgMatch
{
    // ─── MatPool 实例（用于复用屏幕截图的 Mat） ──────────────────────────────
    // 由于 FindPoint 每次调用都会截取全屏（通常 1920x1080 左右），
    // 如果频繁调用会重复分配大块非托管内存，池化后可复用。
    // 容量设为 2 足够：同一时刻最多只有一张截图 Mat 被使用。
    private static readonly MatPool matPool = new MatPool(capacity: 2);

    /// <summary>
    /// 基于 OpenCvSharp 的模板匹配（无尺寸限制，高精度）。
    /// 在屏幕上搜索与给定模板图像最匹配的位置，返回匹配中心点的屏幕坐标。
    /// </summary>
    /// <param name="tempData">模板图像的字节数组（PNG/JPEG 等格式）</param>
    /// <param name="thresh">相似度阈值（0~1），只有匹配度超过此值才认为有效</param>
    /// <returns>匹配中心点的屏幕坐标；若未找到或出错则返回 null</returns>
    public static System.Drawing.Point? FindPoint(byte[] tempData, float thresh = 0.8f)
    {
        try
        {
            // 隐藏鼠标指针，避免干扰截图
            Cursor.Hide();

            // ─── 1. 解码模板图像 ──────────────────────────────────────────────
            // 从字节数组加载为 OpenCV Mat（自动识别格式，返回 BGR 三通道彩色图）
            using var temp = Cv2.ImDecode(tempData, ImreadModes.Color);
            if (temp == null || temp.Empty())
                return null;

            // ─── 2. 截取当前屏幕 ──────────────────────────────────────────────
            // CaptureScreen() 返回 System.Drawing.Bitmap（GDI+ 对象）
            using var scrBmp = CaptureScreen();

            // ─── 3. 从池中租用一个与截图尺寸/类型匹配的 Mat ─────────────────
            // 租用前需确保池中 Mat 的尺寸和类型与当前截图一致，否则 Rent 会丢弃不匹配的并创建新 Mat。
            var size = new OpenCvSharp.Size(scrBmp.Width, scrBmp.Height);
            var type = MatType.CV_8UC3; // 截图是 24 位 RGB，对应 CV_8UC3
            Mat srcMat = matPool.Rent(size, type);
            try
            {
                // ─── 4. 将 Bitmap 数据填充到池中的 Mat（含 RGB→BGR 转换） ──
                // 这一步替换了原来的 BitmapToMat，避免了额外的内存分配和 CvtColor 复制。
                FillMatFromBitmap(scrBmp, srcMat);

                // ─── 5. 检查模板是否大于截图 ────────────────────────────────
                if (temp.Width > srcMat.Width || temp.Height > srcMat.Height)
                    return null;

                // ─── 6. 执行模板匹配 ──────────────────────────────────────────
                using var result = new Mat(); // 存放匹配结果（浮点矩阵）
                Cv2.MatchTemplate(srcMat, temp, result, TemplateMatchModes.CCoeffNormed);

                // ─── 7. 找到最佳匹配位置 ──────────────────────────────────────
                Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);
                if (maxVal < thresh)
                    return null;

                // 计算匹配框的中心点（屏幕绝对坐标）
                int cx = maxLoc.X + temp.Width / 2;
                int cy = maxLoc.Y + temp.Height / 2;
                return new System.Drawing.Point(cx, cy);
            }
            finally
            {
                // ─── 8. 无论是否发生异常，都将 Mat 归还到池中 ──────────────
                // 这是保证池化生效的关键，必须执行！
                matPool.Return(srcMat);
            }
        }
        catch (Exception ex)
        {
            // 记录异常日志（不影响主流程）
            Logger.Log(ex, "ImgMatch.FindPoint (OpenCV)");
            return null;
        }
        finally
        {
            // 恢复鼠标指针
            Cursor.Show();
        }
    }

    /// <summary>
    /// 使用 GDI+ 截取虚拟屏幕（所有显示器合并区域），返回 Bitmap 对象。
    /// 调用者负责在合适时机释放该 Bitmap（通常用 using）。
    /// </summary>
    private static Bitmap CaptureScreen()
    {
        // 获取虚拟屏幕边界（支持多显示器）
        var bounds = SystemInformation.VirtualScreen;
        // 创建 24 位 RGB 位图
        var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        // 从屏幕复制像素数据
        g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
        return bmp; // 返回给调用者，调用者负责释放
    }

    /// <summary>
    /// 将 System.Drawing.Bitmap 的像素数据复制到目标 Mat 中，并完成 RGB→BGR 颜色通道转换。
    /// 目标 Mat 必须已经分配好连续内存，且类型为 CV_8UC3（三通道字节）。
    /// 本方法通过指针操作实现高效复制，避免额外的中间对象。
    /// </summary>
    /// <param name="bmp">源 Bitmap（必须为 24 位或 32 位 RGB）</param>
    /// <param name="dst">目标 Mat（预先通过 Rent 从池中获取）</param>
    private static unsafe void FillMatFromBitmap(Bitmap bmp, Mat dst)
    {
        // 锁定 Bitmap 的像素数据（获得内存指针）
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, bmp.PixelFormat);
        try
        {
            int bytesPerPixel = Image.GetPixelFormatSize(bmp.PixelFormat) / 8;
            if (bytesPerPixel != 3 && bytesPerPixel != 4)
                throw new NotSupportedException($"不支持的像素格式: {bmp.PixelFormat}");

            // 获取源数据和目标数据的指针
            byte* srcPtr = (byte*)data.Scan0;
            byte* dstPtr = dst.DataPointer;
            int srcStride = data.Stride;          // 每行字节数（可能包含填充）
            int dstStep = (int)dst.Step();        // 每行实际字节数（CV_8UC3 每行 3*width）

            // 逐行复制，并交换 R 和 B 通道（RGB→BGR）
            for (int y = 0; y < bmp.Height; y++)
            {
                byte* srcRow = srcPtr + y * srcStride;
                byte* dstRow = dstPtr + y * dstStep;
                for (int x = 0; x < bmp.Width; x++)
                {
                    int srcIdx = x * bytesPerPixel;
                    int dstIdx = x * 3; // 目标每像素3字节

                    // 原格式为 RGB（或 RGBA），OpenCV 需要 BGR，所以交换第0和第2通道
                    dstRow[dstIdx] = srcRow[srcIdx + 2]; // B
                    dstRow[dstIdx + 1] = srcRow[srcIdx + 1]; // G
                    dstRow[dstIdx + 2] = srcRow[srcIdx];     // R
                    // 如果有 Alpha 通道（bytesPerPixel==4），忽略之
                }
            }
        }
        finally
        {
            // 解锁 Bitmap 像素数据
            bmp.UnlockBits(data);
        }
    }

    /// <summary>
    /// 从池中租用一个 Mat 并填充 Bitmap 数据（含 RGB→BGR 转换）。
    /// 调用者必须在使用完毕后调用 <see cref="ReturnMat"/> 归还。
    /// </summary>
    public static Mat BitmapToMatPooled(Bitmap bmp)
    {
        var size = new OpenCvSharp.Size(bmp.Width, bmp.Height);
        var type = MatType.CV_8UC3; // 截图/图像通常为 24 位 RGB
        Mat dst = matPool.Rent(size, type);
        FillMatFromBitmap(bmp, dst);
        return dst;
    }

    /// <summary>
    /// 归还池中租用的 Mat。
    /// </summary>
    public static void ReturnMat(Mat mat)
    {
        if (mat == null) return;
        matPool.Return(mat);
    }

    /// <summary>
    /// 将 Bitmap 对象编码为 PNG 字节数组。
    /// </summary>
    /// <param name="bmp">源 Bitmap</param>
    /// <returns>PNG 格式的字节数组</returns>
    public static byte[] Bmp2Bytes(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    /// <summary>
    /// 从字节数组解码为 Bitmap 对象。
    /// </summary>
    /// <param name="data">图像数据字节数组（如 PNG/JPEG 等）</param>
    /// <returns>Bitmap 对象，调用者负责释放</returns>
    public static Bitmap Bytes2Bmp(byte[] data)
    {
        using var ms = new MemoryStream(data);
        return new Bitmap(ms);
    }

    public static void Dispose() => matPool.Dispose();
}