using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ClickHelper;

// 图片查看器
public class PrevForm : Form
{
    private readonly PictureBox box;
    private readonly Panel panel;
    private readonly Size origSize;
    private float zoom = 1.0f;

    public PrevForm(byte[] image)
    {
        Text = "原尺寸预览 (滚轮缩放 | 右键菜单)";
        Size = new Size(600, 500);
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;
        BackColor = Color.FromArgb(240, 244, 248);
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        Click += (s, e) => Close();

        panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.White
        };
        Controls.Add(panel);

        Bitmap bmp = ImgMatch.Bytes2Bmp(image);
        origSize = bmp.Size;

        box = new PictureBox
        {
            Image = bmp,
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = Point.Empty,
            Size = origSize,
            BackColor = Color.White
        };
        panel.Controls.Add(box);

        // 滚轮
        MouseWheel += (s, e) => Zoom(e.Delta);
        box.MouseWheel += (s, e) => Zoom(e.Delta);

        // 双击重置
        box.DoubleClick += (s, e) => ResetZoom();

        // 右键菜单
        var menu = new ContextMenuStrip();
        var itemB64 = new ToolStripMenuItem("复制为 Base64");
        itemB64.Click += CopyB64;
        var itemOcr = new ToolStripMenuItem("识别文字");
        itemOcr.Click += DoOcr;
        var itemClip = new ToolStripMenuItem("复制剪贴板");
        itemClip.Click += CopyClip;
        var itemSave = new ToolStripMenuItem("另存为文件");
        itemSave.Click += SaveFile;

        menu.Items.Add(itemB64);
        menu.Items.Add(itemOcr);
        menu.Items.Add(itemClip);
        menu.Items.Add(itemSave);
        box.ContextMenuStrip = menu;

        Focus();
        FormClosing += (s, e) => box.Image?.Dispose();
    }

    // ----- 缩放 -----
    private void Zoom(int delta)
    {
        float step = delta > 0 ? 0.1f : -0.1f;
        zoom = Math.Clamp(zoom + step, 0.1f, 10.0f);
        UpdateSize();
    }

    private void ResetZoom()
    {
        zoom = 1.0f;
        UpdateSize();
    }

    private void UpdateSize()
    {
        int newW = (int)(origSize.Width * zoom);
        int newH = (int)(origSize.Height * zoom);
        box.Size = new Size(newW, newH);
        box.Location = new Point(
            Math.Max(0, (panel.Width - newW) / 2),
            Math.Max(0, (panel.Height - newH) / 2)
        );
    }

    // ----- 菜单事件 -----
    private void SaveFile(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog
        {
            // 增加 ICO 选项
            Filter = "PNG 图片|*.png|JPEG 图片|*.jpg|BMP 图片|*.bmp|GIF 图片|*.gif|TIFF 图片|*.tif|ICO 图标|*.ico|所有文件|*.*",
            DefaultExt = "png",
            FileName = "preview.png"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var img = box.Image;
                if (img == null) return;

                string ext = Path.GetExtension(dlg.FileName).ToLower();
                if (ext == ".ico")
                {
                    // 将 Bitmap 转换为 Icon 并保存
                    using (var icon = Icon.FromHandle(((Bitmap)img).GetHicon()))
                    using (var fs = new FileStream(dlg.FileName, FileMode.Create))
                    {
                        icon.Save(fs);
                    }
                    Clipboard.SetText(dlg.FileName);
                    MessageBox.Show($"ICO 文件已保存至：{dlg.FileName}\n路径已复制到剪贴板。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                ImageFormat fmt = ext switch
                {
                    ".jpg" or ".jpeg" => ImageFormat.Jpeg,
                    ".bmp" => ImageFormat.Bmp,
                    ".gif" => ImageFormat.Gif,
                    ".tif" or ".tiff" => ImageFormat.Tiff,
                    _ => ImageFormat.Png
                };
                img.Save(dlg.FileName, fmt);
                Clipboard.SetText(dlg.FileName);
                MessageBox.Show($"文件已保存至：{dlg.FileName}\n路径已复制到剪贴板。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void CopyB64(object? sender, EventArgs e)
    {
        var img = box.Image;
        if (img == null) return;
        try
        {
            using var ms = new MemoryStream();
            img.Save(ms, ImageFormat.Png);
            byte[] bytes = ms.ToArray();
            string b64 = Convert.ToBase64String(bytes);
            Clipboard.SetText(b64);
            MessageBox.Show("图片已转换为 Base64 字符串并复制到剪贴板。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"转换失败：{ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DoOcr(object? sender, EventArgs e)
    {
        var img = box.Image as Bitmap;
        if (img == null) return;

        bool ocrReady = OcrHelper.IsModelReady();
        if (!ocrReady)
        {
            var ans = MessageBox.Show("OCR 引擎尚未加载，是否立即初始化？", "提示",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ans == DialogResult.Yes)
            {
                bool ok = OcrHelper.Init(showAsk: true);
                if (!ok)
                {
                    MessageBox.Show("OCR 引擎初始化失败，请检查模型文件。", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            else
            {
                return;
            }
        }

        // ★ 改用 RecogImageBlocks 获取带换行的结果
        var blocks = OcrHelper.RecogImageBlocks(img);
        if (blocks == null || blocks.Count == 0)
        {
            MessageBox.Show("未识别到任何文字。", "OCR 结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string text = string.Join(Environment.NewLine, blocks.Select(b => b.Text.Trim()));
        using var ocrForm = new OcrForm(text);
        ocrForm.TopMost = true;
        if (ocrForm.ShowDialog() == DialogResult.OK)
        {
            if (!string.IsNullOrEmpty(ocrForm.FinalText))
                Clipboard.SetText(ocrForm.FinalText);
        }
    }

    private void CopyClip(object? sender, EventArgs e)
    {
        var img = box.Image;
        if (img == null) return;
        try
        {
            Clipboard.SetImage(img);
            MessageBox.Show("图片已复制到剪贴板。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"复制失败：{ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        Zoom(e.Delta);
        base.OnMouseWheel(e);
    }
}