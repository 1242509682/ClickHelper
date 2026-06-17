using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClickHelper;

/// <summary> 定时执行设置对话框 </summary>
public class TimerForm : Form
{
    private Config cfg;
    private CheckBox chkEn;
    private DateTimePicker dtpStart, dtpEnd;
    private CheckBox chkNoEnd;

    internal TimerForm(Config config)
    {
        cfg = config;
        this.Text = "定时执行设置";
        this.Size = new Size(460, 220);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;

        var lay = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(10)
        };
        lay.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        lay.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        Font font = new Font("微软雅黑", 9F);
        int row = 0;

        chkEn = new CheckBox { Text = "启用定时", AutoSize = true, Font = font };
        lay.Controls.Add(chkEn, 0, row);
        lay.SetColumnSpan(chkEn, 2);
        row++;

        lay.Controls.Add(new Label { Text = "开始时间:", AutoSize = true, Font = font }, 0, row);
        var panStart = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        dtpStart = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm:ss", Width = 220, Font = font, Value = cfg.TimerStart };
        var btnNow = new Button { Text = "现在", AutoSize = true, Font = font };
        btnNow.Click += (s, e) => dtpStart.Value = DateTime.Now;
        panStart.Controls.Add(dtpStart);
        panStart.Controls.Add(btnNow);
        lay.Controls.Add(panStart, 1, row);
        row++;

        lay.Controls.Add(new Label { Text = "结束时间:", AutoSize = true, Font = font }, 0, row);
        var panEnd = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        DateTime endVal = cfg.TimerEnd == DateTime.MinValue ? DateTime.Now.AddHours(1) : cfg.TimerEnd;
        dtpEnd = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm:ss", Width = 220, Font = font, Value = endVal };
        chkNoEnd = new CheckBox { Text = "无结束", AutoSize = true, Font = font };
        chkNoEnd.Checked = cfg.TimerEnd == DateTime.MinValue;
        panEnd.Controls.Add(dtpEnd);
        panEnd.Controls.Add(chkNoEnd);
        lay.Controls.Add(panEnd, 1, row);
        row++;

        var flowOk = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Dock = DockStyle.Bottom, Margin = new Padding(0, 10, 0, 0) };
        var btnOk = new Button { Text = "确定", DialogResult = DialogResult.OK, Font = font, AutoSize = true };
        var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Font = font, AutoSize = true };
        flowOk.Controls.Add(btnOk);
        flowOk.Controls.Add(btnCancel);
        lay.Controls.Add(flowOk, 0, row);
        lay.SetColumnSpan(flowOk, 2);

        this.Controls.Add(lay);
        this.AcceptButton = btnOk;
        this.CancelButton = btnCancel;

        chkNoEnd.CheckedChanged += (s, e) =>
        {
            dtpEnd.Enabled = !chkNoEnd.Checked;
            if (!chkNoEnd.Checked && dtpEnd.Value == DateTime.MinValue)
                dtpEnd.Value = DateTime.Now.AddHours(1);
        };
        dtpEnd.Enabled = !chkNoEnd.Checked;

        btnOk.Click += (s, e) =>
        {
            cfg.TimerEnabled = chkEn.Checked;
            cfg.TimerStart = dtpStart.Value;
            cfg.TimerEnd = chkNoEnd.Checked ? DateTime.MinValue : dtpEnd.Value;
            cfg.Save();
        };
    }
}