using System;
using System.Drawing;
using System.Windows.Forms;
using static ClickHelper.Config;

namespace ClickHelper;

public class OcrOptForm : Form
{
    private NumericUpDown numDetThr, numBoxThr, numUnclip, numRecThr, numBatch, numPool, numLimit;
    private ComboBox cboScore;
    private CheckBox chkDilate;
    private OcrOpt result;
    internal OcrOpt GetResult() => result;
    internal OcrOptForm(OcrOpt opt)
    {
        Text = "识别参数配置";
        Size = new Size(320, 400);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        TopMost = true; // 窗口置顶

        result = new OcrOpt
        {
            DetThr = opt.DetThr,
            BoxThr = opt.BoxThr,
            Unclip = opt.Unclip,
            Batch = opt.Batch,
            RecThr = opt.RecThr,
            BatchPoolSize = opt.BatchPoolSize,
            LimitSideLen = opt.LimitSideLen,
            ScoreMode = opt.ScoreMode,
            UseDilation = opt.UseDilation
        };

        var lay = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 11,
            Padding = new Padding(10)
        };
        lay.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        lay.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;

        AddRow(lay, "检测阈值", 0.0m, 1.0m, 0.05m, result.DetThr, out numDetThr, row++);
        AddRow(lay, "框阈值", 0.0m, 1.0m, 0.05m, result.BoxThr, out numBoxThr, row++);
        AddRow(lay, "扩张比例", 0.5m, 5.0m, 0.1m, result.Unclip, out numUnclip, row++);
        AddRow(lay, "识别阈值", 0.0m, 1.0m, 0.05m, result.RecThr, out numRecThr, row++);
        AddRow(lay, "批大小", 1, 20, 1, result.Batch, out numBatch, row++);
        AddRow(lay, "线程数", 1, 8, 1, result.BatchPoolSize, out numPool, row++);
        AddRow(lay, "缩放边长", 320, 1920, 10, result.LimitSideLen, out numLimit, row++);

        // 得分模式下拉
        lay.Controls.Add(new Label { Text = "得分模式", AutoSize = true, Font = new Font("微软雅黑", 9F) }, 0, row);
        cboScore = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 120,
            Font = new Font("微软雅黑", 9F)
        };
        cboScore.Items.AddRange(new object[] { "FAST (快速)", "SLOW (精确)" });
        cboScore.SelectedIndex = result.ScoreMode;
        lay.Controls.Add(cboScore, 1, row);
        row++;

        // 膨胀复选框
        chkDilate = new CheckBox { Text = "使用膨胀", AutoSize = true, Font = new Font("微软雅黑", 9F), Checked = result.UseDilation };
        lay.Controls.Add(chkDilate, 0, row);
        lay.SetColumnSpan(chkDilate, 2);
        row++;

        // 确定/取消按钮
        var flowBtn = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };
        var btnOk = new Button { Text = "确定", DialogResult = DialogResult.OK, AutoSize = true };
        var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
        flowBtn.Controls.Add(btnOk);
        flowBtn.Controls.Add(btnCancel);
        lay.Controls.Add(flowBtn, 0, row);
        lay.SetColumnSpan(flowBtn, 2);
        row++;

        Controls.Add(lay);
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        btnOk.Click += (s, e) =>
        {
            result.DetThr = (float)numDetThr.Value;
            result.BoxThr = (float)numBoxThr.Value;
            result.Unclip = (float)numUnclip.Value;
            result.RecThr = (float)numRecThr.Value;
            result.Batch = (int)numBatch.Value;
            result.BatchPoolSize = (int)numPool.Value;
            result.LimitSideLen = (int)numLimit.Value;
            result.ScoreMode = cboScore.SelectedIndex;
            result.UseDilation = chkDilate.Checked;
        };
    }

    private void AddRow(TableLayoutPanel lay, string label, decimal min, decimal max, decimal inc, float val, out NumericUpDown num, int row)
    {
        lay.Controls.Add(new Label { Text = label, AutoSize = true, Font = new Font("微软雅黑", 9F) }, 0, row);
        num = new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Increment = inc,
            DecimalPlaces = 2,
            Value = (decimal)val,
            Width = 120,
            Font = new Font("微软雅黑", 9F)
        };
        lay.Controls.Add(num, 1, row);
    }
}