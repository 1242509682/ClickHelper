using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using static ClickHelper.Config;
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
    internal PosEdit(Config.PosData pos)
    {
        Text = "坐标编辑器";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MaximizeBox = false;
        MinimizeBox = false;
        KeyPreview = true;
        BackColor = Color.FromArgb(240, 244, 248);
        AutoScroll = true;
        AutoSize = true;
        MinimumSize = new Size(560, 300);
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
        TabPage imgPage, txtPage;
        row = TabControl(row, out imgPage, out txtPage);

        InitImgMat(pos, imgPage);
        ocrOpt = pos.OcrOptions ?? new OcrOpt();
        InitTxtMat(pos, txtPage);

        InitOkCan(ref row);

        lay.RowCount = row;
        Controls.Add(lay);

        FitTabSize();
        tabMat.SelectedIndexChanged += (s, e) => this.PerformLayout();

        int prefW = lay.PreferredSize.Width + lay.Padding.Horizontal;
        int prefH = lay.PreferredSize.Height + lay.Padding.Vertical;
        ClientSize = new Size(prefW, prefH);

        Refresh();
    }
    #endregion

    #region 刷新界面状态
    public new void Refresh()
    {
        if (!OcrHelper.IsModelReady())
            OcrHelper.Init(showAsk: false);

        bool isKey = cboAct.SelectedIndex == 1;
        cboMouse.Visible = !isKey;
        hkKey.Visible = isKey;
        cboMod.Enabled = isKey;

        bool img = chkUseImg.Checked;
        bool imgReady = Check.IsReady();

        chkUseImg.Enabled = imgReady;

        if (btnInitImg != null)
        {
            btnInitImg.Enabled = !imgReady;
            btnInitImg.Text = imgReady ? "已就绪" : "初始化";
            btnInitImg.BackColor = imgReady ? SystemColors.Control : Color.FromArgb(255, 240, 225);
            btnInitImg.ForeColor = imgReady ? SystemColors.GrayText : Color.FromArgb(160, 80, 0);
        }

        numThr.Enabled = img;
        btnShot.Enabled = img;
        btnLoadImg.Enabled = img;
        picTemp.Enabled = img;
        numX.Enabled = !img;
        numY.Enabled = !img;
        btnPick.Enabled = !img;
        btnGetUIA.Enabled = img && curImage != null;

        chkUseTxt.Enabled = img;
        if (!img && chkUseTxt.Checked)
            chkUseTxt.Checked = false;

        bool txt = chkUseTxt.Checked;
        txtMatch.Enabled = txt;
        numTxtTh.Enabled = txt;
        cboTxtMode.Enabled = txt;

        if (txtOutput != null)
        {
            int type = cboAct.SelectedIndex;
            txtOutput.Visible = (type == 2 || type == 3);
            if (type == 2)
                txtOutput.PlaceholderText = "输入要打的文字";
            else if (type == 3)
                txtOutput.PlaceholderText = "如 Ctrl+C";
        }

        bool ocrReady = OcrHelper.IsModelReady();

        if (btnInitOcr != null)
        {
            btnInitOcr.Enabled = !ocrReady;
            btnInitOcr.Text = ocrReady ? "已初始化" : "初始化OCR";
        }

        if (btnRecog != null)
        {
            btnRecog.Enabled = ocrReady && picTemp.Image != null;
            btnRecog.BackColor = ocrReady ? Color.FromArgb(240, 248, 255) : SystemColors.Control;
            btnRecog.ForeColor = ocrReady ? Color.FromArgb(0, 80, 180) : SystemColors.GrayText;
        }
    }
    #endregion

    #region 名称 & 坐标
    private TextBox txtDesc;
    private NumericUpDown numX, numY;
    private Button btnPick;
    public string NewDesc = "";
    public int NewX, NewY;

    private void InitNamCrd(Config.PosData pos, ref int row)
    {
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

    #region 操作类型 & 按键（统一使用 HotKeyBox）
    private ComboBox cboAct, cboMouse;
    private HotKeyBox hkKey;  // 替代 cboKey + btnRec
    private TextBox txtOutput;
    public int NewActType, NewActKey;
    public string NewKeyString = ""; // 新增：存储按键字符串

    private void InitActKey(Config.PosData pos, ref int row)
    {
        // 类型 + 输出内容
        lay.Controls.Add(new Label { Text = "类型:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, row);
        var flowType = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            BackColor = Color.Transparent
        };

        cboAct = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 100,
            Font = ctrlFnt,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        cboAct.Items.AddRange(["鼠标点击", "键盘按键", "文本输入", "组合键"]);
        cboAct.SelectedIndex = pos.ActType;
        cboAct.SelectedIndexChanged += ActChanged;
        flowType.Controls.Add(cboAct);

        var lblOutput = new Label
        {
            Text = "输出内容:",
            AutoSize = true,
            Font = lblFnt,
            ForeColor = lblCol,
            Margin = new Padding(10, 3, 2, 0)
        };
        flowType.Controls.Add(lblOutput);

        txtOutput = new TextBox
        {
            Text = pos.ActType == 2 ? pos.TextContent : pos.ComboKeys,
            Width = 150,
            Font = ctrlFnt,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90),
            PlaceholderText = pos.ActType == 2 ? "输入要打的文字" : "如 Ctrl+C"
        };
        flowType.Controls.Add(txtOutput);

        lay.Controls.Add(flowType, 1, row);
        row++;

        // 按键（鼠标下拉 + 键盘组合键输入框）
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
        cboMouse.Items.AddRange(["左键", "中键", "右键"]);
        cboMouse.SelectedIndex = pos.ActType == 0 ? pos.ActKey : 0;
        pan.Controls.Add(cboMouse);

        // ★ 使用 HotKeyBox 替代下拉框
        hkKey = new HotKeyBox { HotKey = pos.ActType == 1 ? GetKeyStringFromCode(pos.ActKey) : "A" };
        pan.Controls.Add(hkKey);

        lay.Controls.Add(pan, 1, row);
        row++;
    }

    // 辅助：将虚拟键码转为显示字符串
    private string GetKeyStringFromCode(int vk)
    {
        try
        {
            return ((Keys)vk).ToString();
        }
        catch
        {
            return "A";
        }
    }
    #endregion

    #region 修饰键 / 模式 / 延迟
    private ComboBox cboMod, cboMode;
    private NumericUpDown numWait;
    public int NewModKey, NewOpMode, NewWaitMs;

    private void InitModDly(Config.PosData pos, ref int row)
    {
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

    #region UIA 探测（保持不变）
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
    public string NewTxtVal = "";
    public string NewCombo = "";

    private void ActChanged(object sender, EventArgs e)
    {
        int type = cboAct.SelectedIndex;
        if (txtOutput != null)
        {
            if (type == 2)
                txtOutput.PlaceholderText = "输入要打的文字";
            else if (type == 3)
                txtOutput.PlaceholderText = "如 Ctrl+C";
            else
                txtOutput.PlaceholderText = "";
            txtOutput.Text = "";
        }
        Refresh();
    }
    #endregion

    #region 图像识别与文字识别 选项卡
    private TabControl tabMat;
    private int TabControl(int row, out TabPage imgPage, out TabPage txtPage)
    {
        tabMat = new TabControl
        {
            Dock = DockStyle.None,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            Margin = new Padding(0, 6, 0, 0),
            TabStop = false,
            AutoSize = false,
            Size = MinimumSize
        };

        imgPage = new TabPage { Text = "图像识别", BackColor = Color.FromArgb(240, 244, 248) };
        imgPage.AutoScroll = true;
        tabMat.TabPages.Add(imgPage);

        txtPage = new TabPage { Text = "文字识别", BackColor = Color.FromArgb(240, 244, 248) };
        txtPage.AutoScroll = true;
        tabMat.TabPages.Add(txtPage);

        lay.Controls.Add(tabMat, 0, row);
        lay.SetColumnSpan(tabMat, 2);
        row++;
        return row;
    }

    private void FitTabSize()
    {
        int maxW = 0;
        int maxH = 0;
        int curIdx = tabMat.SelectedIndex;

        foreach (TabPage page in tabMat.TabPages)
        {
            tabMat.SelectedTab = page;
            page.PerformLayout();
            var inner = page.Controls.Count > 0 ? page.Controls[0] : null;
            if (inner != null)
            {
                int w = inner.PreferredSize.Width + page.Padding.Horizontal;
                int h = inner.PreferredSize.Height + page.Padding.Vertical;
                if (w > maxW) maxW = w;
                if (h > maxH) maxH = h;
            }
        }

        tabMat.SelectedIndex = curIdx;
        int tabHeadH = tabMat.ItemSize.Height + SystemInformation.Border3DSize.Height * 2 + 4;
        int totalW = maxW + SystemInformation.Border3DSize.Width * 2;
        int totalH = maxH + tabHeadH;

        tabMat.MinimumSize = new Size(totalW, totalH);
        tabMat.Size = new Size(Math.Max(tabMat.Width, totalW), Math.Max(tabMat.Height, totalH));
    }
    #endregion

    #region 图像匹配（保持不变）
    private CheckBox chkUseImg;
    private Button btnShot, btnLoadImg, btnGetUIA;
    private NumericUpDown numThr;
    private PictureBox picTemp;
    private byte[]? curImage;
    public bool NewUseImg;
    public byte[]? NewImgTmp;
    public float NewThresh;
    private Button btnInitImg;

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

        btnInitImg = new Button
        {
            Text = "初始化",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(200, 150, 100) },
            BackColor = Color.FromArgb(255, 240, 225),
            ForeColor = Color.FromArgb(160, 80, 0),
            Font = ctrlFnt,
            Enabled = !Check.IsReady()
        };
        btnInitImg.Click += (s, e) => InitImg();
        btnFlow.Controls.Add(btnInitImg);

        btnShot = new Button { Text = "重新截图", AutoSize = true, FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(100, 200, 100) }, BackColor = Color.FromArgb(230, 245, 230), ForeColor = Color.FromArgb(0, 120, 0), Font = ctrlFnt };
        btnShot.Click += ShotClick;

        btnLoadImg = new Button { Text = "加载图片", AutoSize = true, FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(200, 180, 100) }, BackColor = Color.FromArgb(245, 240, 225), ForeColor = Color.FromArgb(160, 120, 0), Font = ctrlFnt };
        btnLoadImg.Click += LdImgClick;

        btnGetUIA = new Button { Text = "获取UIA", AutoSize = true, FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(100, 180, 255) }, BackColor = Color.FromArgb(225, 240, 255), ForeColor = Color.FromArgb(0, 80, 180), Font = ctrlFnt };
        btnGetUIA.Click += GetUIAClick;

        btnFlow.Controls.Add(btnShot);
        btnFlow.Controls.Add(btnLoadImg);
        btnFlow.Controls.Add(btnGetUIA);

        flowImg.Controls.Add(chkUseImg);
        flowImg.Controls.Add(btnFlow);
        layImg.Controls.Add(flowImg, 1, r);
        r++;

        layImg.Controls.Add(new Label { Text = "阈值:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, r);
        var flowThr = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            BackColor = Color.Transparent
        };
        numThr = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 1,
            Increment = 0.05m,
            DecimalPlaces = 2,
            Value = (decimal)pos.Threshold,
            Width = 80,
            Font = ctrlFnt,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        flowThr.Controls.Add(numThr);

        var lblHint = new Label
        {
            Text = "高:匹配严识字难,图小识别度性好 推荐0.8 \n低:匹配松适合大图,保留字多 推荐0.8 \n取值根据桌面DPI缩放125%变化 1 - 0.2 ≈ 0.8 ",
            AutoSize = true,
            Font = new Font("微软雅黑", 7F),
            ForeColor = Color.Gray,
            Margin = new Padding(5, 3, 0, 0)
        };
        flowThr.Controls.Add(lblHint);

        layImg.Controls.Add(flowThr, 1, r);
        r++;

        layImg.Controls.Add(new Label { Text = "预览:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, r);
        picTemp = new PictureBox { Size = new Size(160, 90), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.LightGray, SizeMode = PictureBoxSizeMode.Zoom };
        picTemp.Click += PicClick;
        layImg.Controls.Add(picTemp, 1, r);
        r++;

        curImage = pos.ImageTemp;
        if (curImage != null && curImage.Length > 0)
        {
            Bitmap bmp = ImgMatch.Bytes2Bmp(curImage);
            picTemp.Image = new Bitmap(bmp);
        }

        parent.Controls.Add(layImg);
    }

    private void InitImg()
    {
        if (!Check.IsReady())
        {
            using var dlg = new MissingCvForm();
            dlg.ShowDialog(this);
        }

        if (Check.IsReady())
        {
            var restart = MessageBox.Show(
                "图像识别依赖已就绪，需要重启程序才能生效。\n\n是否立即重启？",
                "重启确认",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
            if (restart == DialogResult.Yes)
                Program.RestartApp();
        }
        else
        {
            MessageBox.Show(
                "依赖未安装，请下载后解压到根目录，然后重新点击【初始化】。",
                "提示",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
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

    #region 文字识别（保持不变）
    private CheckBox chkUseTxt;
    private TextBox txtMatch;
    private NumericUpDown numTxtTh;
    private ComboBox cboTxtMode;
    private Button btnInitOcr;
    private Button btnRecog;
    public bool NewUseTxt;
    public string NewTxtMatch = "";
    public int NewTxtMode;
    public float NewTxtThr;

    private OcrOpt ocrOpt;
    internal OcrOpt NewOcrOpt { get; private set; }

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

        layTxt.Controls.Add(new Label { Text = "启用:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, r);
        var flowTxt = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, BackColor = Color.Transparent };
        chkUseTxt = new CheckBox { Text = "文字点击", AutoSize = true, Font = ctrlFnt, ForeColor = Color.FromArgb(40, 60, 90) };
        chkUseTxt.Checked = pos.UseTxt;
        chkUseTxt.CheckedChanged += (s, e) => Refresh();
        flowTxt.Controls.Add(chkUseTxt);
        layTxt.Controls.Add(flowTxt, 1, r);
        r++;

        layTxt.Controls.Add(new Label { Text = "操作:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, r);
        var flowBtn = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            BackColor = Color.Transparent
        };

        btnInitOcr = new Button
        {
            Text = "初始化OCR",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1 },
            Font = ctrlFnt,
            Enabled = !OcrHelper.IsModelReady()
        };
        btnInitOcr.Click += BtnInitOcr_Click;
        flowBtn.Controls.Add(btnInitOcr);

        btnRecog = new Button
        {
            Text = "识别文字",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(100, 200, 255) },
            BackColor = Color.FromArgb(240, 248, 255),
            ForeColor = Color.FromArgb(0, 80, 180),
            Font = ctrlFnt,
            Enabled = OcrHelper.IsModelReady() && picTemp.Image != null
        };
        btnRecog.Click += BtnRecog_Click;
        flowBtn.Controls.Add(btnRecog);

        var btnOcrParam = new Button
        {
            Text = "OCR参数",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(150, 150, 200) },
            BackColor = Color.FromArgb(240, 240, 255),
            ForeColor = Color.FromArgb(60, 60, 120),
            Font = ctrlFnt
        };
        btnOcrParam.Click += (s, e) =>
        {
            using var dlg = new OcrOptForm(ocrOpt);
            if (dlg.ShowDialog() == DialogResult.OK)
                ocrOpt = dlg.GetResult();
        };
        flowBtn.Controls.Add(btnOcrParam);

        layTxt.Controls.Add(flowBtn, 1, r);
        r++;

        layTxt.Controls.Add(new Label { Text = "文字:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, r);
        txtMatch = new TextBox { Text = pos.TxtMatch ?? "", Dock = DockStyle.Fill, Font = ctrlFnt, BackColor = Color.White };
        layTxt.Controls.Add(txtMatch, 1, r);
        r++;

        layTxt.Controls.Add(new Label { Text = "置信值:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, r);
        var flowConf = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            BackColor = Color.Transparent
        };
        numTxtTh = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 1,
            Increment = 0.05m,
            DecimalPlaces = 2,
            Value = (decimal)pos.TxtThresh,
            Width = 80,
            Font = ctrlFnt,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90)
        };
        flowConf.Controls.Add(numTxtTh);

        var lblHintConf = new Label
        {
            Text = "高:只留准确文字，错字少可能漏 \n低:保留更多文字，错字可能增多 \n截图清晰推荐0.7+ 模糊图推荐0.3-0.5",
            AutoSize = true,
            Font = new Font("微软雅黑", 7F),
            ForeColor = Color.Gray,
            Margin = new Padding(5, 3, 0, 0)
        };
        flowConf.Controls.Add(lblHintConf);

        layTxt.Controls.Add(flowConf, 1, r);
        r++;

        layTxt.Controls.Add(new Label { Text = "模式:", AutoSize = true, Font = lblFnt, ForeColor = lblCol }, 0, r);
        cboTxtMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80, Font = ctrlFnt, BackColor = Color.White, ForeColor = Color.FromArgb(30, 60, 90) };
        cboTxtMode.Items.AddRange(new object[] { "包含", "精确" });
        cboTxtMode.SelectedIndex = pos.TxtMode;
        layTxt.Controls.Add(cboTxtMode, 1, r);
        r++;

        parent.Controls.Add(layTxt);
    }

    private void BtnInitOcr_Click(object sender, EventArgs e)
    {
        if (!OcrHelper.Init(showAsk: true)) return;

        var restart = MessageBox.Show(
            "OCR 模型文件已就绪，需要重启程序才能生效。\n\n是否立即重启？",
            "重启确认",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question
        );
        if (restart == DialogResult.Yes)
            Program.RestartApp();
    }

    private void BtnRecog_Click(object sender, EventArgs e)
    {
        if (picTemp.Image == null)
        {
            MessageBox.Show("请先截图或加载图片。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (picTemp.Image is not Bitmap bmp)
        {
            MessageBox.Show("图像格式不支持识别。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var blocks = OcrHelper.RecogImageBlocks(bmp);
        if (blocks == null || blocks.Count == 0)
        {
            MessageBox.Show("未识别到文字。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string text = string.Join(Environment.NewLine, blocks.Select(b => b.Text.Trim()));
        using var ocrForm = new OcrForm(text);
        ocrForm.TopMost = true;
        if (ocrForm.ShowDialog() == DialogResult.OK)
        {
            if (!string.IsNullOrEmpty(ocrForm.FinalText))
                Clipboard.SetText(ocrForm.FinalText);
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

        // ★ 按键取值：鼠标用下拉，键盘用 HotKeyBox
        if (NewActType == 0)
        {
            NewActKey = cboMouse.SelectedIndex;
        }
        else if (NewActType == 1)
        {
            // 从 HotKeyBox 读取字符串，解析为 Keys
            string keyStr = hkKey.HotKey;
            var (mods, key) = WinApi.ParseHotKey(keyStr);
            NewActKey = (int)key;
            NewModKey = mods switch
            {
                WinApi.MOD_CONTROL => 1,
                WinApi.MOD_SHIFT => 2,
                WinApi.MOD_ALT => 4,
                _ => 0
            };
        }
        else
        {
            NewActKey = 0;
        }

        // 如果不是键盘类型，ModKey 由下拉框决定（但键盘已覆盖）
        if (NewActType != 1)
        {
            NewModKey = cboMod.SelectedIndex switch { 1 => 1, 2 => 2, 3 => 4, _ => 0 };
        }

        NewOpMode = cboMode.SelectedIndex;
        NewWaitMs = (int)numWait.Value;
        NewUseUIA = chkUIA.Checked;
        NewTargets = txtTgt.Text.Split([','], StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => s.Trim()).ToList();
        NewAutoId = txtId.Text.Trim();
        NewUName = txtNm.Text.Trim();
        NewClassN = txtCls.Text.Trim();

        int actType = cboAct.SelectedIndex;
        if (actType == 2)
            NewTxtVal = txtOutput?.Text?.Trim() ?? "";
        else if (actType == 3)
            NewCombo = txtOutput?.Text?.Trim() ?? "";
        else
        {
            NewTxtVal = "";
            NewCombo = "";
        }

        NewUseImg = chkUseImg.Checked;
        NewImgTmp = curImage;
        NewThresh = (float)numThr.Value;
        NewUseTxt = chkUseTxt.Checked;
        NewTxtMatch = txtMatch.Text.Trim();
        NewTxtMode = cboTxtMode.SelectedIndex;
        NewTxtThr = (float)numTxtTh.Value;
        NewOcrOpt = ocrOpt;
    }
    #endregion
}