using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using PaddleOCRSharp;

namespace ClickHelper;

public class OcrHelper
{
    public static event Action<string, Exception>? OnError;

    public static PaddleOCREngine? Engine;
    private static readonly object Lock = new();

    public static string DownloadUrl = "https://share.weiyun.com/pnRTFWq1";
    private const int TARWID = 800;
    private const int MINDIM = 200;

    // ---- 缓存相关 ----
    private static readonly string CacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OcrCache");
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> CacheLocks = new();

    static OcrHelper()
    {
        if (!Directory.Exists(CacheDir))
            Directory.CreateDirectory(CacheDir);
    }

    public static bool Init(string? modelpath = null, bool showAsk = true)
    {
        if (Engine != null) return true;

        lock (Lock)
        {
            string path = modelpath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "inference");

            if (!ModelExists(path))
            {
                if (showAsk)
                {
                    using var ask = new AskForm();
                    ask.ShowDialog();
                }
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
                if (showAsk)
                    MessageBox.Show($"OCR引擎初始化失败：{ex.Message}\n请确保 inference 文件夹包含完整模型文件。",
                                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.Log(ex, "Init");
                OnError?.Invoke("Init", ex);
                Engine = null;
                return false;
            }
        }
    }

    private static bool ModelExists(string path)
    {
        string detDir = Path.Combine(path, "PP-OCRv6_small_det_infer");
        string recDir = Path.Combine(path, "PP-OCRv6_small_rec_infer");
        return Directory.Exists(detDir) && Directory.Exists(recDir);
    }

    /// <summary>
    /// 计算图像的 MD5 哈希（用于缓存键）
    /// </summary>
    private static string ComputeHash(byte[] data)
    {
        using var md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(data);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// 从缓存读取文本块列表，如果不存在或损坏则返回 null
    /// </summary>
    private static List<TextBlock>? LoadFromCache(string hash)
    {
        string cacheFile = Path.Combine(CacheDir, $"{hash}.json");
        if (!File.Exists(cacheFile)) return null;
        try
        {
            string json = File.ReadAllText(cacheFile);
            return JsonConvert.DeserializeObject<List<TextBlock>>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 将文本块列表写入缓存
    /// </summary>
    private static void SaveToCache(string hash, List<TextBlock> blocks)
    {
        try
        {
            var sem = CacheLocks.GetOrAdd(hash, _ => new SemaphoreSlim(1, 1));
            sem.Wait();
            try
            {
                string cacheFile = Path.Combine(CacheDir, $"{hash}.json");
                string json = JsonConvert.SerializeObject(blocks);
                File.WriteAllText(cacheFile, json);
            }
            finally
            {
                sem.Release();
            }
        }
        catch { /* 缓存写入失败不影响主流程 */ }
    }

    /// <summary> 获取指定矩形区域内的所有文本块（含坐标），支持缓存 </summary>
    public static List<TextBlock>? GetText(Rectangle rect, bool useCache = true)
    {
        if (Engine == null || rect.Width <= 0 || rect.Height <= 0) return null;

        // 1. 截取区域图像并转为字节数组用于计算哈希
        using var bmp = CapRect(rect);
        byte[] imageBytes;
        using (var ms = new MemoryStream())
        {
            bmp.Save(ms, ImageFormat.Png);
            imageBytes = ms.ToArray();
        }

        string hash = ComputeHash(imageBytes);

        // 2. 尝试从缓存加载
        if (useCache)
        {
            var cached = LoadFromCache(hash);
            if (cached != null)
                return cached;
        }

        // 3. 缓存未命中，执行 OCR
        var (proc, scale) = PrepImg(bmp);
        using var _ = proc != bmp ? proc : null;
        try
        {
            var result = Engine.DetectText(proc);
            if (result?.TextBlocks != null && result.TextBlocks.Count > 0)
            {
                // 若缩放则映射坐标回原图
                if (Math.Abs(scale - 1.0) > 0.001)
                {
                    foreach (var block in result.TextBlocks)
                    {
                        foreach (var pt in block.BoxPoints)
                        {
                            pt.X = (int)(pt.X / scale);
                            pt.Y = (int)(pt.Y / scale);
                        }
                    }
                }

                // 异步写入缓存（不等待）
                Task.Run(() => SaveToCache(hash, result.TextBlocks));
                return result.TextBlocks;
            }
        }
        catch (Exception ex) when (ex.Message.Contains("box sizes <100"))
        {
            // 缩放后异常，尝试原图
            try
            {
                using var raw = CapRect(rect);
                var result2 = Engine.DetectText(raw);
                if (result2?.TextBlocks != null && result2.TextBlocks.Count > 0)
                {
                    // 缓存原图结果
                    Task.Run(() => SaveToCache(hash, result2.TextBlocks));
                    return result2.TextBlocks;
                }
            }
            catch (Exception innerEx)
            {
                Logger.Log(innerEx, "GetText (fallback)");
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex, "GetText");
            OnError?.Invoke("GetText", ex);
            return null;
        }
        return null;
    }

    /// <summary> 识别指定矩形区域内的文字，返回拼接文本（也支持缓存） </summary>
    public static string? RecogRect(Rectangle rect, bool useCache = true)
    {
        var blocks = GetText(rect, useCache);
        if (blocks == null) return null;
        return string.Join("", blocks.Select(t => t.Text?.Trim() ?? ""));
    }

    /// <summary> 直接识别图像对象，返回拼接后的文字（不缓存） </summary>
    public static string? RecogImage(Bitmap image)
    {
        if (Engine == null || image == null) return null;
        try
        {
            var result = Engine.DetectText(image);
            if (result?.TextBlocks != null && result.TextBlocks.Count > 0)
                return string.Join("", result.TextBlocks.Select(t => t.Text?.Trim() ?? ""));
        }
        catch (Exception ex)
        {
            Logger.Log(ex, "RecogImage");
            OnError?.Invoke("RecogImage", ex);
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
            int newW = (int)(w * scale);
            int newH = (int)(h * scale);
            var scaled = new Bitmap(newW, newH, PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(scaled);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 0, 0, newW, newH);
            return (scaled, scale);
        }
        return (src, 1.0);
    }

    public static void Dispose()
    {
        lock (Lock)
        {
            ClearCache();
            Engine?.Dispose();
            Engine = null;
        }
    }

    /// <summary> 清理缓存文件 </summary>
    public static void ClearCache()
    {
        try
        {
            if (Directory.Exists(CacheDir))
            {
                foreach (var file in Directory.GetFiles(CacheDir, "*.json"))
                {
                    File.Delete(file);
                }
            }
        }
        catch { }
    }
}