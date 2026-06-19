using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PaddleOCRSharp;

namespace ClickHelper;

public class OcrHelper
{
    public static PaddleOCREngine? Engine;
    private static readonly object Lock = new();

    // 模型下载地址
    public static string DownloadUrl = "https://share.weiyun.com/pnRTFWq1";
    private const int TARWID = 800;
    private const int MINDIM = 200;

    public static bool Init(string? modelpath = null)
    {
        if (Engine != null) return true;

        lock (Lock)
        {
            if (Engine != null) return true;

            string path = modelpath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "inference");

            if (!ModelExists(path))
            {
                using var ask = new AskForm();
                ask.ShowDialog();
                return false;
            }

            try
            {
                var config = new OCRModelConfig();
                var param = new OCRParameter
                {
                    use_gpu = false,
                    det_db_thresh = 0.2f,
                    det_db_box_thresh = 0.4f,
                    det_db_unclip_ratio = 1.8f,
                    rec_batch_num = 6,
                    rec_img_h = 48,
                    rec_img_w = 320,
                    use_angle_cls = true,
                    cls_thresh = 0.9f
                };

                Engine = new PaddleOCREngine(config, param);

                var main = Application.OpenForms.OfType<Main>().FirstOrDefault();
                main?.UpdateOcrStatus();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"OCR引擎初始化失败：{ex.Message}\n请确保 inference 文件夹包含完整模型文件。",
                                "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Engine = null;
                return false;
            }
        }
    }

    /// <summary>检查模型文件是否存在</summary>
    private static bool ModelExists(string path)
    {
        // 检测标志文件，例如检测检测模型目录是否存在
        string detDir = Path.Combine(path, "PP-OCRv6_small_det_infer");
        string recDir = Path.Combine(path, "PP-OCRv6_small_rec_infer");
        return Directory.Exists(detDir) && Directory.Exists(recDir);
    }

    /// <summary>识别指定矩形区域内的文字，返回文本内容</summary>
    public static string? RecogRect(Rectangle rect)
    {
        if (Engine == null || rect.Width <= 0 || rect.Height <= 0) return null;
        using var bmp = CapRect(rect);
        var (proc, scale) = PrepImg(bmp);
        using var _ = proc != bmp ? proc : null;

        try
        {
            var result = Engine.DetectText(proc);
            if (result?.TextBlocks != null && result.TextBlocks.Count > 0)
                return string.Join("", result.TextBlocks.Select(t => t.Text?.Trim() ?? ""));
        }
        catch (Exception ex) when (ex.Message.Contains("box sizes <100"))
        {
            if (scale != 1.0)
            {
                using var raw = CapRect(rect);
                var result2 = Engine.DetectText(raw);
                if (result2?.TextBlocks != null && result2.TextBlocks.Count > 0)
                    return string.Join("", result2.TextBlocks.Select(t => t.Text?.Trim() ?? ""));
            }
        }
        return null;
    }

    // ---------- 私有辅助 ----------
    private static Bitmap CapRect(Rectangle rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
            throw new ArgumentException($"矩形尺寸无效: {rect.Width}x{rect.Height}", nameof(rect));
        var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(rect.X, rect.Y, 0, 0, rect.Size);
        return bmp;
    }

    private static (Bitmap processed, double scale) PrepImg(Bitmap src)
    {
        int w = src.Width, h = src.Height;
        double scale = 1.0;
        int minDim = Math.Min(w, h);
        if (minDim < MINDIM)
            scale = (double)MINDIM / minDim;
        else if (w > TARWID)
            scale = (double)TARWID / w;

        if (Math.Abs(scale - 1.0) > 0.001)
        {
            // 需要缩放时才创建新图
            int newW = (int)(w * scale);
            int newH = (int)(h * scale);
            var scaled = new Bitmap(newW, newH, PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(scaled);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 0, 0, newW, newH);
            return (scaled, scale);
        }
        // 无需缩放，直接返回原图（避免复制）
        return (src, 1.0);
    }


    public static void Dispose()
    {
        lock (Lock)
        {
            Engine?.Dispose();
            Engine = null;
        }
    }
}