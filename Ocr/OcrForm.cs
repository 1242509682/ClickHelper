using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClickHelper;

/// <summary>
/// OCR识别结果展示窗体，支持自动调整高度
/// </summary>
public class OcrForm : Form
{
    public string FinalText { get; private set; } = "";

    public OcrForm(string text)
    {
        Text = "OCR 文字识别";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        TopMost = true;
        MaximizeBox = false;
        MinimizeBox = false;

        // ---- 布局容器 ----
        var lay = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10)
        };
        lay.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        lay.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // ---- 文本框 ----
        var txt = new TextBox
        {
            Text = text,
            Multiline = true,
            Dock = DockStyle.Fill,
            Font = new Font("微软雅黑", 8f),
            WordWrap = true,
            ScrollBars = ScrollBars.None
        };
        lay.Controls.Add(txt, 0, 0);

        // ---- 按钮 ----
        var flow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill
        };
        var btnOk = new Button
        {
            Text = "复制",
            DialogResult = DialogResult.OK,
            AutoSize = true
        };
        flow.Controls.Add(btnOk);
        lay.Controls.Add(flow, 0, 1);

        Controls.Add(lay);
        AcceptButton = btnOk;

        // ---- 自动调整窗口高度 ----
        AutoFit(txt);

        // ---- 键盘事件 ----
        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                e.Handled = true;
            }
        };

        // ---- 复制按钮 ----
        btnOk.Click += (s, e) =>
        {
            FinalText = txt.Text.Trim();
            if (!string.IsNullOrEmpty(FinalText))
                Clipboard.SetText(FinalText);
            DialogResult = DialogResult.OK;
        };
    }

    /// <summary>
    /// 根据文本内容调整窗口高度（宽度固定400px）
    /// </summary>
    private void AutoFit(TextBox txt)
    {
        int wid = 400 - 20;          // 文本框可用宽度（减去左右padding）
        Size prop = new Size(wid, 0);
        Size need = txt.GetPreferredSize(prop);  // 计算所需尺寸

        int maxH = (int)(Screen.PrimaryScreen!.WorkingArea.Height * 0.7); // 最大高度
        int finH = Math.Min(need.Height, maxH);  // 最终文本框高度

        // 窗口总高度 = 文本框 + 按钮区(50) + padding(20)
        int total = finH + 50 + 20;
        this.ClientSize = new Size(400, total);

        // 超出最大高度时显示滚动条
        txt.ScrollBars = need.Height > maxH ? ScrollBars.Vertical : ScrollBars.None;
    }
}