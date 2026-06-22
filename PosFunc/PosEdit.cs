using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Font = System.Drawing.Font;
using Size = System.Drawing.Size;

namespace ClickHelper;

public class PosEdit : Form
{
    #region 构造函数 & 布局初始化
    private TableLayoutPanel lay;
    private Font lblFnt, ctrlFnt;
    private Color lblCol;
    private Button btnOk, btnCancel;
    private TabControl tabMat;   // 图像/文字选项卡
    internal PosEdit(Config.PosData pos)
    {
        Text = "坐标编辑器";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MaximizeBox = false;
        MinimizeBox = false;
        KeyPreview = true;
        BackColor = Color.FromArgb(240, 244, 248);
        AutoScroll = true; // 内容超屏时自动滚动
        AutoSize = true;   // 宽高自适应
        MinimumSize = new Size(560, 300); // 防止过小
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MaximumSize = new Size(
            Screen.PrimaryScreen.WorkingArea.Width,
            Screen.PrimaryScreen.WorkingArea.Height
        );

        lblFnt = new Font("微软雅黑", 9F);
        ctrlFnt = new Font("微软雅黑", 9F);
        lblCol = Color.FromArgb(40, 60, 90);

        lay = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Padding = new Padding(12),
            BackColor = Color.Transparent
        };
        lay.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        lay.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;
        InitNamCrd(pos, ref row);
        InitActKey(pos, ref row);
        InitModDly(pos, ref row);
        InitUIA(pos, ref row);

        // 仅当操作类型为文本输入(2)或组合键(3)时才显示该行
        if (pos.ActType == 2 || pos.ActType == 3)
            InitTxtCmb(pos, ref row);
        TabPage imgPage, txtPage;
        row = TabControl(row, out imgPage, out txtPage);

        // 初始化选项卡内的内容
        InitImgMat(pos, imgPage);
        InitTxtMat(pos, txtPage);

        // 底部按钮
        InitOkCan(ref row);

        // 实际行数设置
        lay.RowCount = row;
        Controls.Add(lay);

        // 根据所有 TabPage 内容计算 TabControl 所需最小尺寸
        FitTabSize();
        tabMat.SelectedIndexChanged += (s, e) =>
        {
            // 强制 TableLayoutPanel 重新计算布局，获取实际所需尺寸
            this.PerformLayout();
        };

        // 根据内容的理想大小设置窗口客户区，左右加上 Padding 的边距
        int prefW = lay.PreferredSize.Width + lay.Padding.Horizontal;
        int prefH = lay.PreferredSize.Height + lay.Padding.Vertical;
        ClientSize = new Size(prefW, prefH);


        Refresh();
    }
    #endregion

    #region 刷新界面状态
    public new void Refresh()
    {
        bool isKey = cboAct.SelectedIndex == 1;
        cboMouse.Visible = !isKey;
        cboKey.Visible = isKey;
        btnRec.Visible = isKey;
        cboMod.Enabled = isKey;

        bool img = chkUseImg.Checked;
        numThr.Enabled = img;
        btnShot.Enabled = img;
        btnLoadImg.Enabled = img;
        picTemp.Enabled = img;
        numX.Enabled = !img;
        numY.Enabled = !img;
        btnPick.Enabled = !img;
        btnGetUIA.Enabled = img && curImage != null;

        bool ocrReady = OcrHelper.Engine != null;
        bool canUseOcr = img && ocrReady;
        bool txt = chkUseTxt.Checked && canUseOcr;
        if (!img && chkUseTxt.Checked) chkUseTxt.Checked = false;
        chkUseTxt.Enabled = canUseOcr;
        txtMatch.Enabled = txt;
        numTxtTh.Enabled = txt;
        cboTxtMode.Enabled = txt;

        btnOcr.Enabled = true;
        if (ocrReady)
        {
            btnOcr.Text = "释放OCR";
            btnOcr.BackColor = Color.FromArgb(225, 240, 255);
            btnOcr.ForeColor = Color.FromArgb(0, 80, 180);
            btnOcr.FlatAppearance.BorderColor = Color.FromArgb(100, 180, 255);
        }
        else
        {
            btnOcr.Text = "初始化OCR";
            btnOcr.BackColor = Color.FromArgb(255, 240, 225);
            btnOcr.ForeColor = Color.FromArgb(160, 80, 0);
            btnOcr.FlatAppearance.BorderColor = Color.FromArgb(200, 150, 100);
        }
    }
    #endregion

    #region 初始化方法 & 名称 & 坐标
    private TextBox txtDesc;
    private NumericUpDown numX, numY;
    private Button btnPick;
    public string NewDesc = "";
    public int NewX, NewY;

    private void InitNamCrd(Config.PosData pos, ref int row)
    {
        // 名称
        lay.Controls.Add(new Label { Text = "名称:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, row);
        txtDesc = new TextBox
        {
            Text = pos.Desc ?? "",
            Dock = DockStyle.Fill,
            Font = ctrlFnt,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        lay.Controls.Add(txtDesc, 1, row);
        row++;

        // 坐标
        lay.Controls.Add(new Label { Text = "坐标:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, row);
        var flowXY = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, BackColor = Color.Transparent };
        flowXY.Controls.Add(new Label { Text = "X:", AutoSize = true, Font = ctrlFnt, ForeColor = lblCol });
        numX = new NumericUpDown { Minimum = -99999, Maximum = 99999, Value = pos.X, Width = 65, Font = ctrlFnt, BackColor = Color.White, ForeColor = Color.FromArgb(30, 60, 90) };
        flowXY.Controls.Add(numX);
        flowXY.Controls.Add(new Label { Text = "Y:", AutoSize = true, Font = ctrlFnt, ForeColor = lblCol });
        numY = new NumericUpDown { Minimum = -99999, Maximum = 99999, Value = pos.Y, Width = 65, Font = ctrlFnt, BackColor = Color.White, ForeColor = Color.FromArgb(30, 60, 90) };
        flowXY.Controls.Add(numY);
        btnPick = new Button
        {
            Text = "重选",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(180, 180, 200) },
            BackColor = Color.FromArgb(235, 235, 245),
            ForeColor = Color.FromArgb(60, 60, 100),
            Font = ctrlFnt
        };
        btnPick.Click += PickClick!;
        flowXY.Controls.Add(btnPick);
        lay.Controls.Add(flowXY, 1, row);
        row++;
    }

    private void PickClick(object sender, EventArgs e)
    {
        using var pick = new PickForm();
        if (pick.ShowDialog() == DialogResult.OK)
        {
            numX.Value = Math.Clamp(pick.PickPoint.X, numX.Minimum, numX.Maximum);
            numY.Value = Math.Clamp(pick.PickPoint.Y, numY.Minimum, numY.Maximum);
        }
    }
    #endregion

    #region 操作类型 & 按键
    private ComboBox cboAct, cboMouse, cboKey;
    private Button btnRec;
    public int NewActType, NewActKey;

    private void InitActKey(Config.PosData pos, ref int row)
    {
        // 类型
        lay.Controls.Add(new Label { Text = "类型:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, row);
        cboAct = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 100,
            Font = ctrlFnt,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        cboAct.Items.AddRange(new object[] { "鼠标点击", "键盘按键", "文本输入", "组合键" });
        cboAct.SelectedIndex = pos.ActType;
        cboAct.SelectedIndexChanged += ActChanged;
        lay.Controls.Add(cboAct, 1, row);
        row++;

        // 按键
        lay.Controls.Add(new Label { Text = "按键:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, row);
        var pan = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, BackColor = Color.Transparent };
        cboMouse = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 80,
            Font = ctrlFnt,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        cboMouse.Items.AddRange(new object[] { "左键", "中键", "右键" });
        cboMouse.SelectedIndex = pos.ActType == 0 ? pos.ActKey : 0;

        cboKey = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 80,
            Font = ctrlFnt,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        var keys = WinApi.GetCommonKeys();
        cboKey.Items.AddRange(keys);
        if (pos.ActType == 1 && cboKey.Items.Contains((Keys)pos.ActKey))
            cboKey.SelectedItem = (Keys)pos.ActKey;
        else
            cboKey.SelectedIndex = 0;

        btnRec = new Button
        {
            Text = "录制",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(200, 150, 255) },
            BackColor = Color.FromArgb(240, 230, 255),
            ForeColor = Color.FromArgb(100, 50, 160),
            Font = ctrlFnt
        };
        btnRec.Click += RecClick;
        pan.Controls.Add(cboMouse);
        pan.Controls.Add(cboKey);
        pan.Controls.Add(btnRec);
        lay.Controls.Add(pan, 1, row);
        row++;
    }

    private void ActChanged(object sender, EventArgs e)
    {
        int type = cboAct.SelectedIndex;
        txtText.Visible = type == 2;
        txtCombo.Visible = type == 3;
        Refresh();
    }

    private void RecClick(object sender, EventArgs e)
    {
        using var rec = new RecordKey();
        if (rec.ShowDialog() == DialogResult.OK)
        {
            if (cboKey.Items.Contains(rec.RecordedKey))
                cboKey.SelectedItem = rec.RecordedKey;
        }
    }
    #endregion

    #region 修饰键 / 模式 / 延迟
    private ComboBox cboMod, cboMode;
    private NumericUpDown numWait;
    public int NewModKey, NewOpMode, NewWaitMs;

    private void InitModDly(Config.PosData pos, ref int row)
    {
        // 修饰键
        lay.Controls.Add(new Label { Text = "修饰键:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, row);
        cboMod = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 80,
            Font = ctrlFnt,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        cboMod.Items.AddRange(new object[] { "无", "Ctrl", "Shift", "Alt" });
        cboMod.SelectedIndex = pos.ActType == 1 ? pos.ModKey == 1 ? 1 : pos.ModKey == 2 ? 2 : pos.ModKey == 4 ? 3 : 0 : 0;
        lay.Controls.Add(cboMod, 1, row);
        row++;

        // 模式
        lay.Controls.Add(new Label { Text = "模式:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, row);
        cboMode = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 80,
            Font = ctrlFnt,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        cboMode.Items.AddRange(new object[] { "单击", "按下", "弹起" });
        cboMode.SelectedIndex = pos.OpMode;
        lay.Controls.Add(cboMode, 1, row);
        row++;

        // 延迟
        lay.Controls.Add(new Label { Text = "延迟:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, row);
        numWait = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 3600000,
            Value = pos.WaitMs,
            Width = 80,
            Font = ctrlFnt,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        lay.Controls.Add(numWait, 1, row);
        row++;
    }
    #endregion

    #region UIA 探测
    private CheckBox chkUIA;
    private TextBox txtTgt, txtId, txtNm, txtCls;
    private Button btnProbe;
    private Panel panBox;
    public bool NewUseUIA;
    public List<string> NewTargets = new();
    public string NewAutoId = "";
    public string NewUName = "";
    public string NewClassN = "";

    private void InitUIA(Config.PosData pos, ref int row)
    {
        // Row 1: UIA 启用 + 识别 + 目标
        lay.Controls.Add(new Label { Text = "UIA:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, row);
        var flow1 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, BackColor = Color.Transparent };
        chkUIA = new CheckBox
        {
            Text = "启用",
            AutoSize = true,
            Checked = pos.UseUIA,
            Font = ctrlFnt,
            ForeColor = lblCol
        };
        chkUIA.CheckedChanged += (s, e) => ToggleUIA();
        flow1.Controls.Add(chkUIA);

        btnProbe = new Button
        {
            Text = "识别窗体",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(200, 150, 255) },
            BackColor = Color.FromArgb(240, 230, 255),
            ForeColor = Color.FromArgb(100, 50, 160),
            Font = ctrlFnt,
            Margin = new Padding(5, 0, 5, 0)
        };
        btnProbe.Click += ProbeClick;
        flow1.Controls.Add(btnProbe);

        txtTgt = new TextBox
        {
            Text = string.Join(",", pos.Targets),
            Width = 160,
            PlaceholderText = "进程名/窗口标题",
            Font = ctrlFnt
        };
        flow1.Controls.Add(txtTgt);
        lay.Controls.Add(flow1, 1, row);
        row++;

        // Row 2: UIA 属性容器（通过 panBox 控制显隐）
        panBox = new Panel
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 3, 0, 3)
        };
        var tblIn = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 3,
            AutoSize = true,
            BackColor = Color.Transparent
        };
        tblIn.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
        tblIn.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        tblIn.Controls.Add(new Label { Text = "ID:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, 0);
        txtId = new TextBox { Text = pos.AutoId ?? "", Dock = DockStyle.Fill, PlaceholderText = "AutomationId", Font = ctrlFnt };
        tblIn.Controls.Add(txtId, 1, 0);

        tblIn.Controls.Add(new Label { Text = "名称:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, 1);
        txtNm = new TextBox { Text = pos.UName ?? "", Dock = DockStyle.Fill, PlaceholderText = "Name", Font = ctrlFnt };
        tblIn.Controls.Add(txtNm, 1, 1);

        tblIn.Controls.Add(new Label { Text = "类名:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, 2);
        txtCls = new TextBox { Text = pos.ClassN ?? "", Dock = DockStyle.Fill, PlaceholderText = "ClassName", Font = ctrlFnt };
        tblIn.Controls.Add(txtCls, 1, 2);

        panBox.Controls.Add(tblIn);
        lay.Controls.Add(panBox, 0, row);
        lay.SetColumnSpan(panBox, 2);
        row++;

        ToggleUIA();
    }

    private void ToggleUIA()
    {
        bool on = chkUIA.Checked;
        btnProbe.Enabled = on;
        btnProbe.BackColor = on ? Color.FromArgb(240, 230, 255) : SystemColors.Control;
        btnProbe.ForeColor = on ? Color.FromArgb(100, 50, 160) : SystemColors.GrayText;
        panBox.Visible = on;
        lay.PerformLayout();
        this.PerformLayout();
    }

    private void ProbeClick(object sender, EventArgs e)
    {
        using var picker = new WinPick();
        if (picker.ShowDialog() == DialogResult.OK)
        {
            if (!string.IsNullOrEmpty(picker.SelProc))
            {
                var list = txtTgt.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(x => x.Trim()).ToList();
                if (!list.Contains(picker.SelProc))
                    list.Add(picker.SelProc);
                txtTgt.Text = string.Join(",", list);
            }
            if (!string.IsNullOrEmpty(picker.SelAutoId)) txtId.Text = picker.SelAutoId;
            if (!string.IsNullOrEmpty(picker.SelName)) txtNm.Text = picker.SelName;
            if (!string.IsNullOrEmpty(picker.SelClass)) txtCls.Text = picker.SelClass;
            if (!string.IsNullOrEmpty(txtTgt.Text) || !string.IsNullOrEmpty(txtId.Text) ||
                !string.IsNullOrEmpty(txtNm.Text) || !string.IsNullOrEmpty(txtCls.Text))
            {
                chkUIA.Checked = true;
            }
        }
    }
    #endregion

    #region 文本输入 / 组合键
    private TextBox txtText, txtCombo;
    public string NewTxtVal = "";
    public string NewCombo = "";
    private void InitTxtCmb(Config.PosData pos, ref int row)
    {
        var flowExtra = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, BackColor = Color.Transparent };
        txtText = new TextBox
        {
            Text = pos.TextContent,
            Width = 150,
            Font = ctrlFnt,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90),
            Visible = pos.ActType == 2
        };

        txtCombo = new TextBox
        {
            Text = pos.ComboKeys,
            Width = 150,
            Font = ctrlFnt,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90),
            Visible = pos.ActType == 3
        };
        flowExtra.Controls.Add(txtText);
        flowExtra.Controls.Add(txtCombo);

        var lab = new Label
        {
            Text = pos.ActType == 2 ? "输文本:" : "组合键:",
            AutoSize = true,
            Font = lblFnt,
            ForeColor = lblCol
        };

        lay.Controls.Add(lab, 0, row);
        lay.Controls.Add(flowExtra, 1, row);
        row++;
    }
    #endregion

    #region 图像识别与文字识别 选项卡
    private int TabControl(int row, out TabPage imgPage, out TabPage txtPage)
    {
        // ----- 创建选项卡容器 -----
        tabMat = new TabControl
        {
            // 不设置 Dock，使用 AutoSize 逻辑
            Dock = DockStyle.None,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            Margin = new Padding(0, 6, 0, 0),
            TabStop = false,
            AutoSize = false,            // 保留手动控制
            Size = MinimumSize   // 初始默认尺寸
        };

        // 图像匹配页
        imgPage = new TabPage { Text = "图像识别", BackColor = Color.FromArgb(240, 244, 248) };
        imgPage.AutoScroll = true;
        tabMat.TabPages.Add(imgPage);

        // 文字匹配页
        txtPage = new TabPage { Text = "文字识别", BackColor = Color.FromArgb(240, 244, 248) };
        txtPage.AutoScroll = true;
        tabMat.TabPages.Add(txtPage);

        // 将选项卡放入主布局（跨两列）
        lay.Controls.Add(tabMat, 0, row);
        lay.SetColumnSpan(tabMat, 2);
        row++;
        return row;
    }

    /// <summary>
    /// 让 TabControl 适应内部所有页面的最大内容尺寸
    /// </summary>
    private void FitTabSize()
    {
        int maxW = 0;
        int maxH = 0;

        // 临时记录当前选中页，遍历结束后恢复
        int curIdx = tabMat.SelectedIndex;

        foreach (TabPage page in tabMat.TabPages)
        {
            // 暂时显示该页以获取正确 PreferredSize（若未显示，尺寸可能为 0）
            tabMat.SelectedTab = page;
            page.PerformLayout();

            // 内部容器的 PreferredSize
            var inner = page.Controls.Count > 0 ? page.Controls[0] : null;
            if (inner != null)
            {
                int w = inner.PreferredSize.Width + page.Padding.Horizontal;
                int h = inner.PreferredSize.Height + page.Padding.Vertical;
                if (w > maxW) maxW = w;
                if (h > maxH) maxH = h;
            }
        }

        // 恢复选中页
        tabMat.SelectedIndex = curIdx;

        // 加上 TabControl 自身的装饰高度（标签头 + 边框）
        int tabHeadH = tabMat.ItemSize.Height + SystemInformation.Border3DSize.Height * 2 + 4;
        int totalW = maxW + SystemInformation.Border3DSize.Width * 2;
        int totalH = maxH + tabHeadH;

        // 设置最小尺寸并调整当前尺寸
        tabMat.MinimumSize = new Size(totalW, totalH);
        tabMat.Size = new Size(Math.Max(tabMat.Width, totalW), Math.Max(tabMat.Height, totalH));
    }
    #endregion

    #region 图像匹配
    private CheckBox chkUseImg;
    private Button btnShot, btnLoadImg, btnGetUIA;
    private NumericUpDown numThr;
    private PictureBox picTemp;
    private byte[]? curImage;
    public bool NewUseImg;
    public byte[]? NewImgTmp;
    public float NewThresh;

    /// <summary>
    /// 初始化图像匹配区域（位于选项卡页内）
    /// </summary>
    private void InitImgMat(Config.PosData pos, Control parent)
    {
        var layImg = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(8),
            BackColor = Color.Transparent
        };
        layImg.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 65));
        layImg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int r = 0;

        // 启用复选框 + 操作按钮
        layImg.Controls.Add(new Label { Text = "启用:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, r);
        var flowImg = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, BackColor = Color.Transparent, WrapContents = false };
        chkUseImg = new CheckBox { Text = "启用", AutoSize = true, Font = ctrlFnt, ForeColor = Color.FromArgb(40, 60, 90) };
        chkUseImg.Checked = pos.UseImage;
        if (!Check.IsReady())
        {
            chkUseImg.Enabled = false;
            if (chkUseImg.Checked) chkUseImg.Checked = false;
        }
        else
            chkUseImg.Enabled = true;
        chkUseImg.CheckedChanged += (s, e) =>
        {
            if (chkUseImg.Checked && !Check.IsReady())
            {
                chkUseImg.Checked = false;
                using var dlg = new MissingCvForm();
                dlg.ShowDialog(this);
            }
            Refresh();
        };

        var btnFlow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, BackColor = Color.Transparent };

        var btnInit = new Button { Text = "初始化", AutoSize = true, FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(200, 150, 100) }, BackColor = Color.FromArgb(255, 240, 225), ForeColor = Color.FromArgb(160, 80, 0), Font = ctrlFnt };
        btnInit.Click += (s, e) =>
        {
            if (Check.IsReady())
            {
                chkUseImg.Enabled = true;
                MessageBox.Show("截图核心已就绪，可以启用图像匹配。", "提示",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                using var dlg = new MissingCvForm();
                dlg.ShowDialog(this);
            }
        };

        btnShot = new Button { Text = "重新截图", AutoSize = true, FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(100, 200, 100) }, BackColor = Color.FromArgb(230, 245, 230), ForeColor = Color.FromArgb(0, 120, 0), Font = ctrlFnt };
        btnShot.Click += ShotClick;

        btnLoadImg = new Button { Text = "加载图片", AutoSize = true, FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(200, 180, 100) }, BackColor = Color.FromArgb(245, 240, 225), ForeColor = Color.FromArgb(160, 120, 0), Font = ctrlFnt };
        btnLoadImg.Click += LdImgClick;

        btnGetUIA = new Button { Text = "获取UIA", AutoSize = true, FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(100, 180, 255) }, BackColor = Color.FromArgb(225, 240, 255), ForeColor = Color.FromArgb(0, 80, 180), Font = ctrlFnt };
        btnGetUIA.Click += GetUIAClick;

        btnFlow.Controls.Add(btnInit);
        btnFlow.Controls.Add(btnShot);
        btnFlow.Controls.Add(btnLoadImg);
        btnFlow.Controls.Add(btnGetUIA);

        flowImg.Controls.Add(chkUseImg);
        flowImg.Controls.Add(btnFlow);
        layImg.Controls.Add(flowImg, 1, r);
        r++;

        // 阈值
        layImg.Controls.Add(new Label { Text = "阈值:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, r);
        numThr = new NumericUpDown { Minimum = 0, Maximum = 1, Increment = 0.05m, DecimalPlaces = 2, Value = (decimal)pos.Threshold, Width = 80, Font = ctrlFnt, BackColor = Color.White, ForeColor = Color.FromArgb(30, 60, 90) };
        layImg.Controls.Add(numThr, 1, r);
        r++;

        // 预览
        layImg.Controls.Add(new Label { Text = "预览:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, r);
        picTemp = new PictureBox { Size = new Size(120, 80), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.LightGray, SizeMode = PictureBoxSizeMode.Zoom };
        picTemp.Click += PicClick;
        layImg.Controls.Add(picTemp, 1, r);
        r++;

        // 恢复图像数据
        curImage = pos.ImageTemp;
        if (curImage != null && curImage.Length > 0)
        {
            Bitmap bmp = ImgMatch.Bytes2Bmp(curImage);
            picTemp.Image = new Bitmap(bmp);
        }

        parent.Controls.Add(layImg);
    }

    private void ShotClick(object sender, EventArgs e)
    {
        using var snap = new SnapForm();
        if (snap.ShowDialog() == DialogResult.OK && snap.GetImage != null)
        {
            curImage = ImgMatch.Bmp2Bytes(snap.GetImage);
            picTemp.Image = new Bitmap(snap.GetImage, new Size(120, 80));
            chkUseImg.Checked = true;
            Refresh();
        }
    }

    private void LdImgClick(object sender, EventArgs e)
    {
        using var openDlg = new OpenFileDialog
        {
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|所有文件|*.*",
            Title = "选择图片文件"
        };
        if (openDlg.ShowDialog() == DialogResult.OK)
        {
            try
            {
                using var bmp = new Bitmap(openDlg.FileName);
                curImage = ImgMatch.Bmp2Bytes(bmp);
                picTemp.Image = new Bitmap(bmp, new Size(120, 80));
                chkUseImg.Checked = true;
                Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载图片失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void PicClick(object sender, EventArgs e)
    {
        if (curImage == null || curImage.Length == 0)
        {
            MessageBox.Show("没有图像可预览", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var prev = new PrevForm(curImage);
        prev.ShowDialog(this);
    }

    private void GetUIAClick(object sender, EventArgs e)
    {
        if (curImage == null || curImage.Length == 0)
        {
            MessageBox.Show("请先截图或加载图片。");
            return;
        }
        if (!Check.IsReady())
        {
            using var dlg = new MissingCvForm();
            dlg.ShowDialog(this);
            if (!Check.IsReady()) return;
        }
        var rect = ImgMatch.FindPoint(curImage, (float)numThr.Value);
        if (!rect.HasValue)
        {
            MessageBox.Show("未在屏幕上找到匹配位置。");
            return;
        }
        int cx = rect.Value.X;
        int cy = rect.Value.Y;
        var elem = FlaUIHelper.GetElemPt(cx, cy);
        if (elem == null)
        {
            MessageBox.Show("无法获取该位置的 UI 元素。");
            return;
        }
        try
        {
            string autoId = elem.Properties.AutomationId.Value ?? "";
            string name = elem.Properties.Name.Value ?? "";
            string cls = elem.Properties.ClassName.Value ?? "";
            int pid = elem.Properties.ProcessId.Value;
            string proc = "";
            using (var p = System.Diagnostics.Process.GetProcessById(pid))
                proc = p.ProcessName;

            if (!string.IsNullOrEmpty(proc))
            {
                var list = txtTgt.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(s => s.Trim()).ToList();
                if (!list.Contains(proc))
                    list.Add(proc);
                txtTgt.Text = string.Join(",", list);
            }
            if (!string.IsNullOrEmpty(autoId)) txtId.Text = autoId;
            if (!string.IsNullOrEmpty(name)) txtNm.Text = name;
            if (!string.IsNullOrEmpty(cls)) txtCls.Text = cls;
            chkUIA.Checked = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("获取属性失败：" + ex.Message);
        }
    }
    #endregion

    #region 文字匹配
    private CheckBox chkUseTxt;
    private TextBox txtMatch;
    private NumericUpDown numTxtTh;
    private ComboBox cboTxtMode;
    private Button btnOcr;
    public bool NewUseTxt;
    public string NewTxtMatch = "";
    public int NewTxtMode;
    public float NewTxtThr;

    /// <summary>
    /// 初始化文字匹配区域（位于选项卡页内）
    /// </summary>
    private void InitTxtMat(Config.PosData pos, Control parent)
    {
        var layTxt = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(8),
            BackColor = Color.Transparent
        };
        layTxt.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 65));
        layTxt.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int r = 0;

        // 启用 + OCR 按钮（同行）
        layTxt.Controls.Add(new Label { Text = "启用:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, r);
        var flowTxt = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, BackColor = Color.Transparent };
        chkUseTxt = new CheckBox { Text = "启用", AutoSize = true, Font = ctrlFnt, ForeColor = Color.FromArgb(40, 60, 90) };
        chkUseTxt.Checked = pos.UseTxt;
        chkUseTxt.CheckedChanged += (s, e) => Refresh();
        flowTxt.Controls.Add(chkUseTxt);

        btnOcr = new Button { Text = "初始化OCR", AutoSize = true, FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 1 }, Font = ctrlFnt };
        btnOcr.Click += OcrClick;
        flowTxt.Controls.Add(btnOcr);

        layTxt.Controls.Add(flowTxt, 1, r);
        r++;

        // 文字内容
        layTxt.Controls.Add(new Label { Text = "文字:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, r);
        txtMatch = new TextBox { Text = pos.TxtMatch ?? "", Dock = DockStyle.Fill, Font = ctrlFnt, BackColor = Color.White };
        layTxt.Controls.Add(txtMatch, 1, r);
        r++;

        // 置信值
        layTxt.Controls.Add(new Label { Text = "置信值:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, r);
        numTxtTh = new NumericUpDown { Minimum = 0, Maximum = 1, Increment = 0.05m, DecimalPlaces = 2, Value = (decimal)pos.TxtThresh, Width = 80, Font = ctrlFnt, BackColor = Color.White, ForeColor = Color.FromArgb(30, 60, 90) };
        layTxt.Controls.Add(numTxtTh, 1, r);
        r++;

        // 匹配模式
        layTxt.Controls.Add(new Label { Text = "模式:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, r);
        cboTxtMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80, Font = ctrlFnt, BackColor = Color.White, ForeColor = Color.FromArgb(30, 60, 90) };
        cboTxtMode.Items.AddRange(new object[] { "包含", "精确" });
        cboTxtMode.SelectedIndex = pos.TxtMode;
        layTxt.Controls.Add(cboTxtMode, 1, r);
        r++;

        parent.Controls.Add(layTxt);
    }

    private void OcrClick(object sender, EventArgs e)
    {
        if (OcrHelper.Engine == null)
        {
            if (!OcrHelper.Init()) return;
            MessageBox.Show("OCR引擎加载成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Refresh();
        }
        else
        {
            var result = MessageBox.Show("确定要释放OCR引擎吗？释放后如需使用需重新初始化。", "确认释放",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                OcrHelper.Dispose();
                chkUseTxt.Checked = false;
                Refresh();
            }
        }
    }
    #endregion

    #region 底部确定/取消按钮
    private void InitOkCan(ref int row)
    {
        var flowOk = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.None, BackColor = Color.Transparent };
        btnOk = new Button
        {
            Text = "确定",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(0, 180, 255) },
            BackColor = Color.FromArgb(225, 240, 255),
            ForeColor = Color.FromArgb(0, 80, 180),
            Font = ctrlFnt
        };
        btnOk.Click += OkClick;
        btnCancel = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(180, 180, 180) },
            BackColor = Color.FromArgb(240, 240, 240),
            ForeColor = Color.FromArgb(80, 80, 80),
            Font = ctrlFnt
        };
        flowOk.Controls.Add(btnOk);
        flowOk.Controls.Add(btnCancel);
        lay.Controls.Add(flowOk, 0, row);
        lay.SetColumnSpan(flowOk, 2);
        row++;
        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }

    private void OkClick(object sender, EventArgs e)
    {
        NewDesc = txtDesc.Text.Trim();
        NewX = (int)numX.Value;
        NewY = (int)numY.Value;
        NewActType = cboAct.SelectedIndex;
        NewActKey = NewActType == 0 ? cboMouse.SelectedIndex : (int)(Keys)cboKey.SelectedItem;
        NewModKey = cboMod.SelectedIndex switch { 1 => 1, 2 => 2, 3 => 4, _ => 0 };
        NewOpMode = cboMode.SelectedIndex;
        NewWaitMs = (int)numWait.Value;
        NewUseUIA = chkUIA.Checked;
        NewTargets = txtTgt.Text.Split([','], StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => s.Trim()).ToList();
        NewAutoId = txtId.Text.Trim();
        NewUName = txtNm.Text.Trim();
        NewClassN = txtCls.Text.Trim();
        NewTxtVal = txtText.Text.Trim();
        NewCombo = txtCombo.Text.Trim();
        NewUseImg = chkUseImg.Checked;
        NewImgTmp = curImage;
        NewThresh = (float)numThr.Value;
        NewUseTxt = chkUseTxt.Checked;
        NewTxtMatch = txtMatch.Text.Trim();
        NewTxtMode = cboTxtMode.SelectedIndex;
        NewTxtThr = (float)numTxtTh.Value;
    }
    #endregion

}