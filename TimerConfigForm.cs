using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClickHelper;

public class TimerConfigForm : Form
{
    private Config cfg;
    private CheckBox chkEnable;
    private DateTimePicker dtpStart;
    private DateTimePicker dtpEnd;
    private CheckBox chkNoEnd;

    internal TimerConfigForm(Config config)
    {
        cfg = config;
        this.Text = "定时执行设置";
        this.Size = new Size(400, 210);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(10)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;
        Font font = new Font("微软雅黑", 9F);

        // 启用
        chkEnable = new CheckBox { Text = "启用定时", AutoSize = true, Font = font };
        layout.Controls.Add(chkEnable, 0, row);
        layout.SetColumnSpan(chkEnable, 2);
        row++;

        // 开始时间 + “现在”按钮
        layout.Controls.Add(new Label { Text = "开始时间:", AutoSize = true, Font = font }, 0, row);
        var panelStart = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true
        };
        dtpStart = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd HH:mm:ss",
            ShowUpDown = false,
            Width = 180,
            Font = font,
            Value = cfg.TimerStart
        };
        var btnNow = new Button
        {
            Text = "现在",
            AutoSize = true,
            Font = font
        };
        btnNow.Click += (s, e) => dtpStart.Value = DateTime.Now;
        panelStart.Controls.Add(dtpStart);
        panelStart.Controls.Add(btnNow);
        layout.Controls.Add(panelStart, 1, row);
        row++;

        // 结束时间
        layout.Controls.Add(new Label { Text = "结束时间:", AutoSize = true, Font = font }, 0, row);
        var panelEnd = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true
        };
        // 若配置中结束时间为 MinValue，则显示一个占位值（当前+1小时）
        DateTime endValue = cfg.TimerEnd == DateTime.MinValue ? DateTime.Now.AddHours(1) : cfg.TimerEnd;
        dtpEnd = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd HH:mm:ss",
            ShowUpDown = false,
            Width = 180,
            Font = font,
            Value = endValue
        };
        chkNoEnd = new CheckBox { Text = "无结束", AutoSize = true, Font = font };
        chkNoEnd.Checked = cfg.TimerEnd == DateTime.MinValue;
        panelEnd.Controls.Add(dtpEnd);
        panelEnd.Controls.Add(chkNoEnd);
        layout.Controls.Add(panelEnd, 1, row);
        row++;

        // 确定/取消（确定在左，取消在右）
        var flowOk = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Dock = DockStyle.Bottom,
            Margin = new Padding(0, 10, 0, 0)
        };
        var btnOk = new Button { Text = "确定", DialogResult = DialogResult.OK, Font = font };
        var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Font = font };
        flowOk.Controls.Add(btnOk);
        flowOk.Controls.Add(btnCancel);
        layout.Controls.Add(flowOk, 0, row);
        layout.SetColumnSpan(flowOk, 2);

        this.Controls.Add(layout);
        this.AcceptButton = btnOk;
        this.CancelButton = btnCancel;

        // 事件：无结束复选框控制结束时间启用（但不要修改 Value）
        chkNoEnd.CheckedChanged += (s, e) =>
        {
            dtpEnd.Enabled = !chkNoEnd.Checked;
            // 如果取消勾选“无结束”，确保 dtpEnd 有一个有效值（防止 MinValue）
            if (!chkNoEnd.Checked && dtpEnd.Value == DateTime.MinValue)
            {
                dtpEnd.Value = DateTime.Now.AddHours(1);
            }
        };
        // 初始状态
        dtpEnd.Enabled = !chkNoEnd.Checked;

        // 确定保存
        btnOk.Click += (s, e) =>
        {
            cfg.TimerEnabled = chkEnable.Checked;
            cfg.TimerStart = dtpStart.Value;
            cfg.TimerEnd = chkNoEnd.Checked ? DateTime.MinValue : dtpEnd.Value;
            cfg.Save();
        };
    }
}