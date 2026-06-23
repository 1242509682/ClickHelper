using System;
using System.Drawing;
using System.Windows.Forms;
using static ClickHelper.Program;

namespace ClickHelper;

public class TimerForm : Form
{
    #region 字段
    private RadioButton rbDate, rbTimer;
    private DateTimePicker dtpStart, dtpEnd;
    private CheckBox chkNoEnd;
    private NumericUpDown numHour, numMin, numSec;
    private RadioButton rbPos, rbMacro;
    private ComboBox cboMacros, cboTimHot;

    // 面板引用（用于可见性控制）
    private FlowLayoutPanel panDate, panTimer;
    #endregion

    #region 构造与初始化
    internal TimerForm()
    {
        InitForm();
        InitLayout();
        InitEvents();
        UpdateUI();
    }

    private void InitForm()
    {
        Text = "定时执行设置";
        Size = new Size(500, 320);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        KeyPreview = true;
        BackColor = Color.FromArgb(240, 244, 248);
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
    }
    #endregion

    #region UI 布局
    private void InitLayout()
    {
        var lay = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(15, 12, 15, 12),
            BackColor = Color.Transparent
        };
        lay.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
        lay.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        Font font = new Font("微软雅黑", 9F);
        Font lblFont = new Font("微软雅黑", 9F, FontStyle.Bold);

        int row = 0;
        InitHotKeyRow(lay, font, lblFont, ref row);
        InitModeRow(lay, font, lblFont, ref row);
        InitTimeParamRow(lay, font, ref row);
        InitTargetRow(lay, font, lblFont, ref row);

        Controls.Add(lay);
    }

    private void InitHotKeyRow(TableLayoutPanel lay, Font font, Font lblFont, ref int row)
    {
        var lblHot = new Label
        {
            Text = "定时热键:",
            AutoSize = true,
            Font = lblFont,
            ForeColor = Color.FromArgb(40, 60, 90),
            Margin = new Padding(0, 6, 0, 0)
        };
        lay.Controls.Add(lblHot, 0, row);

        var flowHot = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        cboTimHot = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 100,
            Font = font,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        var keys = WinApi.GetCommonKeys();
        cboTimHot.Items.AddRange(keys);
        cboTimHot.SelectedItem = cboTimHot.Items.Contains((Keys)cfg.TimerHotKey) ? (Keys)cfg.TimerHotKey : Keys.F8;
        flowHot.Controls.Add(cboTimHot);
        lay.Controls.Add(flowHot, 1, row);
        row++;
    }

    private void InitModeRow(TableLayoutPanel lay, Font font, Font lblFont, ref int row)
    {
        var lblMode = new Label
        {
            Text = "定时模式:",
            AutoSize = true,
            Font = lblFont,
            ForeColor = Color.FromArgb(40, 60, 90),
            Margin = new Padding(0, 6, 0, 0)
        };
        lay.Controls.Add(lblMode, 0, row);

        var flowMode = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        rbDate = new RadioButton
        {
            Text = "日期模式",
            AutoSize = true,
            Font = font,
            Checked = cfg.TimerMode == 0,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        rbTimer = new RadioButton
        {
            Text = "计时模式",
            AutoSize = true,
            Font = font,
            Checked = cfg.TimerMode == 1,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        flowMode.Controls.Add(rbDate);
        flowMode.Controls.Add(rbTimer);
        lay.Controls.Add(flowMode, 1, row);
        row++;
    }

    private void InitTimeParamRow(TableLayoutPanel lay, Font font, ref int row)
    {
        var lblParam = new Label
        {
            Text = "时间参数:",
            AutoSize = true,
            Font = new Font("微软雅黑", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(40, 60, 90),
            Margin = new Padding(0, 6, 0, 0)
        };
        lay.Controls.Add(lblParam, 0, row);

        var panParam = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        panDate = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true };
        panTimer = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };

        // 日期面板
        var panStart = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        dtpStart = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd HH:mm:ss",
            Width = 220,
            Font = font,
            Value = cfg.TimerStart,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        var btnNow = new Button
        {
            Text = "现在",
            AutoSize = true,
            Font = font,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(100, 180, 255) },
            BackColor = Color.FromArgb(225, 240, 255),
            ForeColor = Color.FromArgb(0, 80, 180)
        };
        btnNow.Click += (s, e) => dtpStart.Value = DateTime.Now;
        panStart.Controls.Add(dtpStart);
        panStart.Controls.Add(btnNow);
        panDate.Controls.Add(panStart);

        var panEnd = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        DateTime endVal = cfg.TimerEnd == DateTime.MinValue ? DateTime.Now.AddHours(1) : cfg.TimerEnd;
        dtpEnd = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd HH:mm:ss",
            Width = 220,
            Font = font,
            Value = endVal,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        chkNoEnd = new CheckBox
        {
            Text = "无结束",
            AutoSize = true,
            Font = font,
            Checked = cfg.TimerEnd == DateTime.MinValue,
            ForeColor = Color.FromArgb(40, 60, 90)
        };
        panEnd.Controls.Add(dtpEnd);
        panEnd.Controls.Add(chkNoEnd);
        panDate.Controls.Add(panEnd);

        // 计时面板
        numHour = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 23,
            Value = cfg.TimerDuration / 3600,
            Width = 50,
            Font = font,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        numMin = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 59,
            Value = cfg.TimerDuration % 3600 / 60,
            Width = 50,
            Font = font,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        numSec = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 59,
            Value = cfg.TimerDuration % 60,
            Width = 50,
            Font = font,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        panTimer.Controls.Add(numHour);
        panTimer.Controls.Add(new Label { Text = "时", AutoSize = true, Font = font, ForeColor = Color.DimGray });
        panTimer.Controls.Add(numMin);
        panTimer.Controls.Add(new Label { Text = "分", AutoSize = true, Font = font, ForeColor = Color.DimGray });
        panTimer.Controls.Add(numSec);
        panTimer.Controls.Add(new Label { Text = "秒", AutoSize = true, Font = font, ForeColor = Color.DimGray });

        // 加入主容器
        panParam.Controls.Add(panDate);
        panParam.Controls.Add(panTimer);
        panDate.Visible = cfg.TimerMode == 0;
        panTimer.Visible = cfg.TimerMode == 1;

        lay.Controls.Add(panParam, 1, row);
        row++;
    }

    private void InitTargetRow(TableLayoutPanel lay, Font font, Font lblFont, ref int row)
    {
        var lblTarget = new Label
        {
            Text = "执行目标:",
            AutoSize = true,
            Font = lblFont,
            ForeColor = Color.FromArgb(40, 60, 90),
            Margin = new Padding(0, 6, 0, 0)
        };
        lay.Controls.Add(lblTarget, 0, row);

        var flowTarget = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        rbPos = new RadioButton
        {
            Text = "坐标表",
            AutoSize = true,
            Font = font,
            Checked = cfg.TimeType == 0,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        rbMacro = new RadioButton
        {
            Text = "宏播放",
            AutoSize = true,
            Font = font,
            Checked = cfg.TimeType == 1,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        cboMacros = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 120,
            Font = font,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        var names = MacIO.GetMacroNames();
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
        // row++ 不需要，因为这是最后一行
    }
    #endregion

    #region 事件绑定
    private void InitEvents()
    {
        rbDate.CheckedChanged += (s, e) =>
        {
            panDate.Visible = rbDate.Checked;
            panTimer.Visible = !rbDate.Checked;
            SaveConfig();
        };
        rbTimer.CheckedChanged += (s, e) =>
        {
            panDate.Visible = !rbTimer.Checked;
            panTimer.Visible = rbTimer.Checked;
            SaveConfig();
        };

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
    }
    #endregion

    #region 配置保存 & UI更新
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
    #endregion
}