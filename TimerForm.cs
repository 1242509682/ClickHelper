using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClickHelper;

public class TimerForm : Form
{
    private Config cfg;
    private RadioButton rbDate;
    private RadioButton rbTimer;
    private DateTimePicker dtpStart, dtpEnd;
    private CheckBox chkNoEnd;
    private NumericUpDown numHour, numMin, numSec;
    private RadioButton rbPos;
    private RadioButton rbMacro;
    private ComboBox cboMacros;
    private ComboBox cboTimHot;

    internal TimerForm(Config config)
    {
        cfg = config;
        this.Text = "定时执行设置";
        this.Size = new Size(500, 320);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.KeyPreview = true;
        this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) this.Close(); };

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

        // 行0：定时热键
        lay.Controls.Add(new Label { Text = "定时热键:", AutoSize = true, Font = font }, 0, row);
        var flowHot = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        cboTimHot = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100, Font = font };
        var keys = WinApi.GetCommonKeys();
        cboTimHot.Items.AddRange(keys);
        if (cboTimHot.Items.Contains((Keys)cfg.TimerHotKey))
            cboTimHot.SelectedItem = (Keys)cfg.TimerHotKey;
        else
            cboTimHot.SelectedItem = Keys.F8;
        flowHot.Controls.Add(cboTimHot);
        lay.Controls.Add(flowHot, 1, row);
        row++;

        // 行1：模式选择
        lay.Controls.Add(new Label { Text = "定时模式:", AutoSize = true, Font = font }, 0, row);
        var flowMode = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        rbDate = new RadioButton { Text = "日期模式", AutoSize = true, Font = font, Checked = (cfg.TimerMode == 0) };
        rbTimer = new RadioButton { Text = "计时模式", AutoSize = true, Font = font, Checked = (cfg.TimerMode == 1) };
        flowMode.Controls.Add(rbDate);
        flowMode.Controls.Add(rbTimer);
        lay.Controls.Add(flowMode, 1, row);
        row++;

        // 行2：日期/计时参数
        lay.Controls.Add(new Label { Text = "时间参数:", AutoSize = true, Font = font }, 0, row);
        var panParam = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };

        var panDate = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true };
        var panStart = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        dtpStart = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm:ss", Width = 220, Font = font, Value = cfg.TimerStart };
        var btnNow = new Button { Text = "现在", AutoSize = true, Font = font };
        btnNow.Click += (s, e) => dtpStart.Value = DateTime.Now;
        panStart.Controls.Add(dtpStart);
        panStart.Controls.Add(btnNow);
        panDate.Controls.Add(panStart);

        var panEnd = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        DateTime endVal = cfg.TimerEnd == DateTime.MinValue ? DateTime.Now.AddHours(1) : cfg.TimerEnd;
        dtpEnd = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm:ss", Width = 220, Font = font, Value = endVal };
        chkNoEnd = new CheckBox { Text = "无结束", AutoSize = true, Font = font };
        chkNoEnd.Checked = cfg.TimerEnd == DateTime.MinValue;
        panEnd.Controls.Add(dtpEnd);
        panEnd.Controls.Add(chkNoEnd);
        panDate.Controls.Add(panEnd);

        var panTimer = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        numHour = new NumericUpDown { Minimum = 0, Maximum = 23, Value = cfg.TimerDuration / 3600, Width = 50, Font = font };
        numMin = new NumericUpDown { Minimum = 0, Maximum = 59, Value = (cfg.TimerDuration % 3600) / 60, Width = 50, Font = font };
        numSec = new NumericUpDown { Minimum = 0, Maximum = 59, Value = cfg.TimerDuration % 60, Width = 50, Font = font };
        panTimer.Controls.Add(numHour);
        panTimer.Controls.Add(new Label { Text = "时", AutoSize = true, Font = font });
        panTimer.Controls.Add(numMin);
        panTimer.Controls.Add(new Label { Text = "分", AutoSize = true, Font = font });
        panTimer.Controls.Add(numSec);
        panTimer.Controls.Add(new Label { Text = "秒", AutoSize = true, Font = font });

        panParam.Controls.Add(panDate);
        panParam.Controls.Add(panTimer);
        panDate.Visible = (cfg.TimerMode == 0);
        panTimer.Visible = (cfg.TimerMode == 1);

        lay.Controls.Add(panParam, 1, row);
        row++;

        // 行3：执行目标
        lay.Controls.Add(new Label { Text = "执行目标:", AutoSize = true, Font = font }, 0, row);
        var flowTarget = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        rbPos = new RadioButton { Text = "位置列表", AutoSize = true, Font = font, Checked = (cfg.TimeType == 0) };
        rbMacro = new RadioButton { Text = "宏播放", AutoSize = true, Font = font, Checked = (cfg.TimeType == 1) };
        cboMacros = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, Font = font };
        var names = Macro.MacIO.GetMacroNames();
        Array.Sort(names);
        cboMacros.Items.AddRange(names);
        if (!string.IsNullOrEmpty(cfg.MacroName) && cboMacros.Items.Contains(cfg.MacroName))
            cboMacros.SelectedItem = cfg.MacroName;
        else if (cboMacros.Items.Count > 0)
            cboMacros.SelectedIndex = 0;

        flowTarget.Controls.Add(rbPos);
        flowTarget.Controls.Add(rbMacro);
        flowTarget.Controls.Add(cboMacros);
        lay.Controls.Add(flowTarget, 1, row);

        this.Controls.Add(lay);

        // ---- 事件绑定 ----
        rbDate.CheckedChanged += (s, e) => { panDate.Visible = rbDate.Checked; panTimer.Visible = !rbDate.Checked; SaveConfig(); };
        rbTimer.CheckedChanged += (s, e) => { panDate.Visible = !rbTimer.Checked; panTimer.Visible = rbTimer.Checked; SaveConfig(); };

        rbPos.CheckedChanged += (s, e) => { cboMacros.Visible = rbMacro.Checked; SaveConfig(); };
        rbMacro.CheckedChanged += (s, e) => { cboMacros.Visible = rbMacro.Checked; SaveConfig(); };
        cboMacros.Visible = rbMacro.Checked;

        chkNoEnd.CheckedChanged += (s, e) =>
        {
            dtpEnd.Enabled = !chkNoEnd.Checked;
            if (!chkNoEnd.Checked && dtpEnd.Value == DateTime.MinValue)
                dtpEnd.Value = DateTime.Now.AddHours(1);
            SaveConfig();
        };
        dtpEnd.Enabled = !chkNoEnd.Checked;

        cboTimHot.SelectedIndexChanged += (s, e) => SaveConfig();
        dtpStart.ValueChanged += (s, e) => SaveConfig();
        dtpEnd.ValueChanged += (s, e) => SaveConfig();
        numHour.ValueChanged += (s, e) => SaveConfig();
        numMin.ValueChanged += (s, e) => SaveConfig();
        numSec.ValueChanged += (s, e) => SaveConfig();
        cboMacros.SelectedIndexChanged += (s, e) => SaveConfig();

        UpdateUI();
    }

    private void SaveConfig()
    {
        cfg.TimerMode = rbDate.Checked ? 0 : 1;
        cfg.TimerStart = dtpStart.Value;
        cfg.TimerEnd = chkNoEnd.Checked ? DateTime.MinValue : dtpEnd.Value;
        cfg.TimerDuration = (int)(numHour.Value * 3600 + numMin.Value * 60 + numSec.Value);
        cfg.TimeType = rbPos.Checked ? 0 : 1;
        cfg.MacroName = cboMacros.SelectedItem?.ToString() ?? "";
        if (cboTimHot.SelectedItem is Keys key)
            cfg.TimerHotKey = (int)key;
        cfg.Save();
    }

    private void UpdateUI()
    {
        bool enabled = cfg.TimerEnabled;
        cboTimHot.Enabled = !enabled;
        rbDate.Enabled = !enabled;
        rbTimer.Enabled = !enabled;
        dtpStart.Enabled = !enabled;
        dtpEnd.Enabled = !enabled && !chkNoEnd.Checked;
        chkNoEnd.Enabled = !enabled;
        numHour.Enabled = !enabled;
        numMin.Enabled = !enabled;
        numSec.Enabled = !enabled;
        rbPos.Enabled = !enabled;
        rbMacro.Enabled = !enabled;
        cboMacros.Enabled = !enabled;
    }
}