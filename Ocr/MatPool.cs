using System;
using System.Collections.Concurrent;
using System.Threading;
using OpenCvSharp;

namespace ClickHelper;

/// <summary>
/// 线程安全的 Mat 对象池，用于复用 Mat 实例，避免频繁分配非托管内存。
/// 在 OCR、图像匹配等高频场景下可显著降低内存峰值和 GC 压力。
/// </summary>
public class MatPool : IDisposable
{
    // 线程安全的队列，存储可复用的 Mat 对象
    private readonly ConcurrentQueue<Mat> pool = new();

    // 池的最大容量，防止无限增长
    private readonly int maxCap;

    // 当前正在租用中的 Mat 数量（仅用于统计，非强制）
    private int rented;

    /// <summary>
    /// 初始化 MatPool 实例
    /// </summary>
    /// <param name="capacity">池中最多缓存的 Mat 数量，建议设为 5~10</param>
    public MatPool(int capacity = 10)
    {
        maxCap = capacity;
    }

    /// <summary>
    /// 从池中租用一个指定尺寸和类型的 Mat。
    /// 如果池中有匹配的可用 Mat，则直接返回；否则创建新的 Mat。
    /// 租用者用完必须调用 <see cref="Return"/> 归还，否则将造成泄漏。
    /// </summary>
    /// <param name="size">所需的图像尺寸 (Width, Height)</param>
    /// <param name="type">Mat 的数据类型（如 MatType.CV_8UC3）</param>
    /// <returns>可用的 Mat 实例，调用方负责最终归还</returns>
    public Mat Rent(Size size, MatType type)
    {
        // 循环尝试从队列中取出一个 Mat
        while (pool.TryDequeue(out var mat))
        {
            // 检查尺寸和类型是否匹配，不匹配则废弃并释放
            if (mat.Size() != size || mat.Type() != type)
            {
                mat.Dispose();  // 立即释放不匹配的 Mat
                continue;
            }
            // 匹配成功，增加租用计数，返回该 Mat
            Interlocked.Increment(ref rented);
            return mat;
        }

        // 池中无可用 Mat，创建新的
        var newMat = new Mat(size, type);
        Interlocked.Increment(ref rented);
        return newMat;
    }

    /// <summary>
    /// 将使用完毕的 Mat 归还到池中，以便后续复用。
    /// 如果池已满，则直接释放该 Mat。
    /// </summary>
    /// <param name="mat">要归还的 Mat，不能为 null</param>
    public void Return(Mat mat)
    {
        if (mat == null) return;

        // 如果池已达到最大容量，直接释放，不再缓存
        if (pool.Count >= maxCap)
        {
            mat.Dispose();
            Interlocked.Decrement(ref rented);
            return;
        }

        // 清空 Mat 数据，避免旧数据残留（对下次使用更安全）
        mat.SetTo(Scalar.All(0));

        // 将 Mat 放回队列，供后续租用
        pool.Enqueue(mat);
        Interlocked.Decrement(ref rented);
    }

    /// <summary>
    /// 释放池中所有缓存的 Mat，通常在程序退出时调用。
    /// </summary>
    public void Dispose()
    {
        // 取出并释放所有残留的 Mat
        while (pool.TryDequeue(out var mat))
            mat.Dispose();
    }
}