using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ClickHelper;

public class AskForm : Form
{
    public AskForm()
    {
        Text = "依赖与语言模型下载";
        Size = new Size(480, 210);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ControlBox = false;
        TopMost = true;

        var lay = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(15)
        };
        lay.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        lay.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        lay.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        lay.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        lay.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // 第一行：提示文字
        var lbl = new Label
        {
            Text = "检测到缺少OCR核心文件!\n请下载后解压到本程序根目录",
            AutoSize = true,
            Font = new Font("微软雅黑", 9F)
        };
        lay.Controls.Add(lbl, 0, 0);

        // 第二行：可点击链接（显示模型名称）
        var link = new LinkLabel
        {
            Text = "PaddleOCR v6 依赖+语言模型",
            AutoSize = true,
            Font = new Font("微软雅黑", 9F, FontStyle.Underline),
            LinkColor = Color.Blue
        };
        link.LinkClicked += (s, e) =>
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = OcrHelper.DownloadUrl,
                UseShellExecute = true
            });
        };
        lay.Controls.Add(link, 0, 1);

        // 第三行：压缩包名称
        var zipName = new Label
        {
            Text = "压缩包：PaddleOCR v6 依赖+语言模型.zip",
            AutoSize = true,
            Font = new Font("微软雅黑", 8F),
            ForeColor = Color.DimGray
        };
        lay.Controls.Add(zipName, 0, 2);

        // 第四行：大小信息
        var sizeInfo = new Label
        {
            Text = "压缩包 123 MB | 解压 350 MB | 需 480 MB 空间",
            AutoSize = true,
            Font = new Font("微软雅黑", 8F),
            ForeColor = Color.Gray
        };
        lay.Controls.Add(sizeInfo, 0, 3);

        // 第五行：按钮（从左到右排列）
        var flow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = false
        };

        var btnDownload = new Button { Text = "下载", AutoSize = true };
        var btnFolder = new Button { Text = "打开根目录", AutoSize = true };
        var btnCancel = new Button { Text = "取消", AutoSize = true };

        flow.Controls.Add(btnDownload);
        flow.Controls.Add(btnFolder);
        flow.Controls.Add(btnCancel);

        lay.Controls.Add(flow, 0, 4);

        Controls.Add(lay);
        AcceptButton = btnDownload;
        CancelButton = btnCancel;

        // 下载：打开浏览器并关闭窗口
        btnDownload.Click += (s, e) =>
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = OcrHelper.DownloadUrl,
                UseShellExecute = true
            });
            DialogResult = DialogResult.Cancel;
            Close();
        };

        // 打开根目录
        btnFolder.Click += (s, e) =>
        {
            string root = Application.StartupPath;
            if (System.IO.Directory.Exists(root))
            {
                Process.Start("explorer.exe", root);
            }
        };

        // 取消
        btnCancel.Click += (s, e) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
    }
}