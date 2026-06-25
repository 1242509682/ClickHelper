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
using OpenCvSharp;
using RapidOCRSharpOnnx;
using RapidOCRSharpOnnx.Configurations;
using RapidOCRSharpOnnx.Providers;
using RapidOCRSharpOnnx.Utils;

namespace ClickHelper;

public static class OcrHelper
{
    public static event Action<string, Exception>? OnError;

    private static readonly object Lock = new();
    private static string? modelPath;
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

    /// <summary>
    /// 初始化：验证模型文件是否存在，保存模型路径供后续创建引擎使用
    /// </summary>
    public static bool Init(string? modelpath = null, bool showAsk = true)
    {
        lock (Lock)
        {
            try
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
                modelPath = path;
                return true;
            }
            catch (Exception ex)
            {
                if (showAsk)
                    MessageBox.Show($"OCR引擎初始化失败：{ex.Message}\n请确保 inference 文件夹包含完整模型文件。",
                                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.Log(ex, "Init");
                OnError?.Invoke("Init", ex);
                modelPath = null;
                return false;
            }
        }
    }

    // 检查模型文件
    private static bool ModelExists(string path)
    {
        return File.Exists(Path.Combine(path, "PP-OCRv6_small_det.onnx")) &&
               File.Exists(Path.Combine(path, "PP-OCRv6_small_rec.onnx"));
    }

    public static bool IsModelReady()
    {
        if (string.IsNullOrEmpty(modelPath)) return false;
        return ModelExists(modelPath);
    }

    /// <summary>
    /// 创建临时 OCR 引擎实例（每次调用都会加载模型，用完必须 Dispose）
    /// </summary>
    private static RapidOCRSharp CreateEngine(Config.OcrOpt? opt = null)
    {
        if (string.IsNullOrEmpty(modelPath))
            throw new InvalidOperationException("OCR 未初始化，请先调用 Init()");

        try
        {
            string detPath = Path.Combine(modelPath, "PP-OCRv6_small_det.onnx");
            string recPath = Path.Combine(modelPath, "PP-OCRv6_small_rec.onnx");

            var ocrCfg = new OcrConfig(detPath, recPath, LangRec.CH, OCRVersion.PPOCRV6);

            // 应用自定义参数（存在则用，否则默认）
            if (opt != null)
            {
                ocrCfg.DetectorConfig.Thresh = opt.DetThr;
                ocrCfg.DetectorConfig.BoxThresh = opt.BoxThr;
                ocrCfg.DetectorConfig.UnclipRatio = opt.Unclip;
                ocrCfg.RecognizerConfig.RecBatchNum = opt.Batch;
                ocrCfg.RecognizerConfig.TextScore = opt.RecThr;
                ocrCfg.BatchPoolSize = opt.BatchPoolSize;
                ocrCfg.DetectorConfig.LimitSideLen = opt.LimitSideLen;
                ocrCfg.DetectorConfig.ScoreMode = (ScoreMode)opt.ScoreMode;
                ocrCfg.DetectorConfig.UseDilation = opt.UseDilation;
            }
            else
            {
                // 默认值（与之前一致）
                ocrCfg.DetectorConfig.Thresh = 0.2f;
                ocrCfg.DetectorConfig.BoxThresh = 0.4f;
                ocrCfg.DetectorConfig.UnclipRatio = 1.8f;
                ocrCfg.RecognizerConfig.RecBatchNum = 6;
                ocrCfg.RecognizerConfig.TextScore = 0.5f;
                ocrCfg.BatchPoolSize = 1;
                ocrCfg.DetectorConfig.LimitSideLen = 960;
                ocrCfg.DetectorConfig.ScoreMode = ScoreMode.FAST;
                ocrCfg.DetectorConfig.UseDilation = false;
            }

            // 选择执行提供者
            ExecutionProvider provider = new ExecutionProviderCPU(ocrCfg);
            return new RapidOCRSharp(provider);
        }
        catch (FileNotFoundException ex)
        {
            Logger.Log(ex, "CreateEngine");
            throw; // 仍然抛出，让上层处理
        }
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
    /// 从缓存读取文本块列表
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
        catch { return null; }
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
            finally { sem.Release(); }
        }
        catch { /* 忽略 */ }
    }

    /// <summary> 获取指定矩形区域内的所有文本块（含坐标），支持缓存 </summary>
    internal static List<TextBlock>? GetText(Rectangle rect, bool useCache = true, Config.OcrOpt? opt = null)
    {
        // 如果有自定义参数，强制不使用缓存
        if (opt != null) useCache = false;

        if (rect.Width <= 0 || rect.Height <= 0) return null;

        using var bmp = CapRect(rect);
        byte[] imageBytes;
        using (var ms = new MemoryStream())
        {
            bmp.Save(ms, ImageFormat.Png);
            imageBytes = ms.ToArray();
        }
        string hash = ComputeHash(imageBytes);

        // 1. 尝试从缓存加载
        if (useCache)
        {
            var cached = LoadFromCache(hash);
            if (cached != null) return cached;
        }

        // 2. 缓存未命中，执行 OCR
        var (proc, scale) = PrepImg(bmp);
        using var _ = proc != bmp ? proc : null;

        // ★ 创建临时引擎
        using var engine = CreateEngine(opt);
        Mat mat = ImgMatch.BitmapToMatPooled(proc);
        try
        {
            var result = engine.RecognizeText(mat);

            if (result?.DetResult?.Data?.DetItems == null ||
                result.RecResult?.Data == null ||
                result.DetResult.Data.DetItems.Length != result.RecResult.Data.Length)
                return null;

            var detItems = result.DetResult.Data.DetItems;
            var recItems = result.RecResult.Data;

            var list = new List<TextBlock>();
            for (int i = 0; i < recItems.Length; i++)
            {
                var points = detItems[i].Box.Select(p => new OCRPoint((int)p.X, (int)p.Y)).ToList();
                list.Add(new TextBlock
                {
                    Text = recItems[i].Label ?? "",
                    Score = recItems[i].Score,
                    BoxPoints = points
                });
            }

            // 缩放坐标回原图
            if (Math.Abs(scale - 1.0) > 0.001)
            {
                foreach (var block in list)
                    for (int j = 0; j < block.BoxPoints.Count; j++)
                    {
                        block.BoxPoints[j].X = (int)(block.BoxPoints[j].X / scale);
                        block.BoxPoints[j].Y = (int)(block.BoxPoints[j].Y / scale);
                    }
            }

            // 异步写入缓存
            Task.Run(() => SaveToCache(hash, list));
            return list;
        }
        catch (Exception ex) when (ex.Message.Contains("box sizes <100"))
        {
            // 回退原图
            using Mat mat2 = ImgMatch.BitmapToMatPooled(proc);
            try
            {
                var result2 = engine.RecognizeText(mat2);
                if (result2?.DetResult?.Data?.DetItems != null &&
                    result2.RecResult?.Data != null &&
                    result2.DetResult.Data.DetItems.Length == result2.RecResult.Data.Length)
                {
                    var detItems2 = result2.DetResult.Data.DetItems;
                    var recItems2 = result2.RecResult.Data;
                    var list2 = new List<TextBlock>();
                    for (int i = 0; i < recItems2.Length; i++)
                    {
                        var points = detItems2[i].Box.Select(p => new OCRPoint((int)p.X, (int)p.Y)).ToList();
                        list2.Add(new TextBlock
                        {
                            Text = recItems2[i].Label ?? "",
                            Score = recItems2[i].Score,
                            BoxPoints = points
                        });
                    }
                    Task.Run(() => SaveToCache(hash, list2));
                    return list2;
                }
            }
            catch (Exception innerEx) { Logger.Log(innerEx, "GetText (fallback)"); }
            finally { ImgMatch.ReturnMat(mat2); }
        }
        catch (Exception ex)
        {
            Logger.Log(ex, "GetText");
            OnError?.Invoke("GetText", ex);
            return null;
        }
        finally
        {
            ImgMatch.ReturnMat(mat);
        }
        return null;
    }

    /// <summary> 识别指定矩形区域内的文字，返回拼接文本（也支持缓存） </summary>
    internal static string? RecogRect(Rectangle rect, bool useCache = true, Config.OcrOpt? opt = null)
    {
        var blocks = GetText(rect, useCache, opt);
        return blocks == null ? null : string.Join("", blocks.Select(t => t.Text?.Trim() ?? ""));
    }

    /// <summary> 识别图像，返回文本块列表（含坐标），每行独立 </summary>
    internal static List<TextBlock>? RecogImageBlocks(Bitmap image, Config.OcrOpt? opt = null)
    {
        if (image == null) return null;
        using var engine = CreateEngine(opt);
        try
        {
            using Mat mat = ImgMatch.BitmapToMatPooled(image);
            var result = engine.RecognizeText(mat);
            if (result?.DetResult?.Data?.DetItems == null ||
                result.RecResult?.Data == null ||
                result.DetResult.Data.DetItems.Length != result.RecResult.Data.Length)
                return null;

            var detItems = result.DetResult.Data.DetItems;
            var recItems = result.RecResult.Data;
            var list = new List<TextBlock>();
            for (int i = 0; i < recItems.Length; i++)
            {
                var points = detItems[i].Box.Select(p => new OCRPoint((int)p.X, (int)p.Y)).ToList();
                list.Add(new TextBlock
                {
                    Text = recItems[i].Label ?? "",
                    Score = recItems[i].Score,
                    BoxPoints = points
                });
            }
            return list;
        }
        catch (Exception ex)
        {
            Logger.Log(ex, "RecogImageBlocks");
            OnError?.Invoke("RecogImageBlocks", ex);
            return null;
        }
    }

    /// <summary> 直接识别图像对象，返回拼接后的文字（不缓存） </summary>
    internal static string? RecogImage(Bitmap image, Config.OcrOpt? opt = null)
    {
        if (image == null) return null;

        using var engine = CreateEngine(opt);
        Mat mat = ImgMatch.BitmapToMatPooled(image);
        try
        {
            var result = engine.RecognizeText(mat);
            return result?.TextBlocks;
        }
        catch (Exception ex)
        {
            Logger.Log(ex, "RecogImage");
            OnError?.Invoke("RecogImage", ex);
            return null;
        }
        finally
        {
            ImgMatch.ReturnMat(mat);
        }
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

    public static void ClearCache()
    {
        try
        {
            if (Directory.Exists(CacheDir))
            {
                foreach (var file in Directory.GetFiles(CacheDir, "*.json"))
                    File.Delete(file);
            }

            // 释放 MatPool 中残留的 Mat
            ImgMatch.Dispose();

            // ★ 停止时强制回收内存（释放未使用的托管对象）
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        catch { }
    }
}

public class OCRPoint
{
    public int X { get; set; }
    public int Y { get; set; }
    public OCRPoint() { }
    public OCRPoint(int x, int y) { X = x; Y = y; }
    public override string ToString() => $"({X},{Y})";
}

public class TextBlock
{
    public List<OCRPoint> BoxPoints { get; set; } = new();
    public string Text { get; set; } = "";
    public float Score { get; set; }
    public float cls_score { get; set; }
    public int cls_label { get; set; }
    public override string ToString()
    {
        if (BoxPoints == null || BoxPoints.Count == 0) return "";
        return $"{Text},Score:{Score},[{string.Join(",", BoxPoints)}]";
    }
}