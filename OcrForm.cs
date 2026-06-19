using System.Drawing;
using System.Windows.Forms;

namespace ClickHelper;

public class OcrForm : Form
{
    public string FinalText { get; private set; } = "";

    public OcrForm(string text)
    {
        this.Text = "OCR 文字识别";
        this.Size = new Size(400, 300);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.TopMost = true;
        this.MaximizeBox = false;
        this.MinimizeBox = false;

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

        this.Controls.Add(lay);
        this.AcceptButton = btnOk;

        // 按 ESC 键取消
        this.KeyPreview = true;
        this.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                e.Handled = true;
            }
        };

        // 点击复制按钮时保存文本
        btnOk.Click += (s, e) => FinalText = txt.Text.Trim();
    }
}