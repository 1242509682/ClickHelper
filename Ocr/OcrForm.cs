using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClickHelper;

public class OcrForm : Form
{
    public string FinalText { get; private set; } = "";

    public OcrForm(string text)
    {
        Text = "OCR 文字识别";
        Size = new Size(400, 300);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        TopMost = true;
        MaximizeBox = false;
        MinimizeBox = false;

        var lay = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10)
        };
        lay.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        lay.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // 文本框
        var txt = new TextBox
        {
            Text = text,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font("微软雅黑", 8f)
        };
        lay.Controls.Add(txt, 0, 0);

        // 按钮
        var flow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill
        };
        var btnOk = new Button { Text = "复制", DialogResult = DialogResult.OK, AutoSize = true };
        flow.Controls.Add(btnOk);
        lay.Controls.Add(flow, 0, 1);

        Controls.Add(lay);
        AcceptButton = btnOk;

        // 按 ESC 键取消
        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();

                // ★ 停止时强制回收内存（释放未使用的托管对象）
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect(); // 回收可能提升到下一代的对象
                e.Handled = true;
            }
        };

        // 点击复制按钮时保存文本
        btnOk.Click += (s, e) =>
        {
            FinalText = txt.Text.Trim();
            if (!string.IsNullOrEmpty(FinalText))
            {
                Clipboard.SetText(FinalText);

                // ★ 停止时强制回收内存（释放未使用的托管对象）
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect(); // 回收可能提升到下一代的对象
            }

            DialogResult = DialogResult.OK;
        };
    }
}