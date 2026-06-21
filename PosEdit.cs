using System;
using System.Drawing;
using System.Windows.Forms;

using static ClickHelper.PosForm;
using Font = System.Drawing.Font;
using Size = System.Drawing.Size;

namespace ClickHelper;

public class PosEdit : Form
{
    // ---- 输出属性 ----
    public int NewX, NewY, NewActType, NewActKey, NewModKey, NewOpMode, NewWaitMs;
    public string NewDesc = "";
    public bool NewUseImageMatch;
    public byte[]? NewImageTemplate;
    public float NewThreshold;
    public bool NewUseTxt;
    public string NewTxtMatch = "";
    public int NewTxtMode;
    public float NewTxtThresh;
    // ★ 新增
    public bool NewUseUIA;
    public string NewUIAProc = ""; 
    public string NewTextContent = "";
    public string NewComboKeys = "";

    // ---- 控件 ----
    private NumericUpDown numX, numY, numWait;
    private TextBox txtDesc;
    private ComboBox cboAct, cboMouse, cboKey, cboMod, cboMode;
    private Button btnRec, btnShot;
    private PictureBox picTemp;
    private CheckBox chkUseImg;
    private NumericUpDown numThr;
    private byte[]? curImage;
    private CheckBox chkUseTxt;
    private TextBox txtMatch;
    private NumericUpDown numTxtTh;
    private ComboBox cboTxtMode;
    private Button btnLoadImg;
    private Button btnOcr;
    private Button btnPick;

    // UIA 相关控件
    private CheckBox chkUseUIA;
    private TextBox txtUIAProc;   // ★ 只读显示进程名
    private Button btnSelProc;    // ★ 选择进程按钮
    private TextBox txtText;
    private TextBox txtCombo;

    internal PosEdit(Config.PosData pos)
    {
        this.Text = "坐标编辑器";
        this.Size = new Size(380, 900);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.KeyPreview = true;
        this.BackColor = Color.FromArgb(240, 244, 248);

        var lay = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 17,  // 保持行数不变，将 UIA 复选框和选进程放在同一行
            Padding = new Padding(8),
            BackColor = Color.Transparent
        };
        lay.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        lay.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        Font lblFont = new Font("微软雅黑", 9F);
        Font ctrlFont = new Font("微软雅黑", 9F);
        Color lblCol = Color.FromArgb(40, 60, 90);

        int row = 0;

        // ---- 名称 ----
        lay.Controls.Add(new Label { Text = "名称:", AutoSize = true, Font = lblFont, ForeColor = lblCol }, 0, row);
        txtDesc = new TextBox { Text = pos.Desc ?? "", Dock = DockStyle.Fill, Font = ctrlFont, BackColor = Color.White, ForeColor = Color.FromArgb(30, 60, 90) };
        lay.Controls.Add(txtDesc, 1, row++);

        // ---- 坐标 ----
        lay.Controls.Add(new Label { Text = "坐标:", AutoSize = true, Font = lblFont, ForeColor = lblCol }, 0, row);
        var flowXY = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, BackColor = Color.Transparent };
        flowXY.Controls.Add(new Label { Text = "X:", AutoSize = true, Font = ctrlFont, ForeColor = lblCol });
        numX = new NumericUpDown { Minimum = -99999, Maximum = 99999, Value = pos.X, Width = 70, Font = ctrlFont, BackColor = Color.White, ForeColor = Color.FromArgb(30, 60, 90) };
        flowXY.Controls.Add(numX);
        flowXY.Controls.Add(new Label { Text = "Y:", AutoSize = true, Font = ctrlFont, ForeColor = lblCol });
        numY = new NumericUpDown { Minimum = -99999, Maximum = 99999, Value = pos.Y, Width = 70, Font = ctrlFont, BackColor = Color.White, ForeColor = Color.FromArgb(30, 60, 90) };
        flowXY.Controls.Add(numY);
        btnPick = new Button
        {
            Text = "重选",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(180, 180, 200) },
            BackColor = Color.FromArgb(235, 235, 245),
            ForeColor = Color.FromArgb(60, 60, 100),
            Font = ctrlFont
        };
        flowXY.Controls.Add(btnPick);
        lay.Controls.Add(flowXY, 1, row++);

        // ---- 类型 ----
        lay.Controls.Add(new Label { Text = "类型:", AutoSize = true, Font = lblFont, ForeColor = lblCol }, 0, row);
        cboAct = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100, Font = ctrlFont, BackColor = Color.White, ForeColor = Color.FromArgb(30, 60, 90) };
        cboAct.Items.AddRange(new object[] { "鼠标点击", "键盘按键", "文本输入", "组合键" });
        cboAct.SelectedIndex = pos.ActType;
        lay.Controls.Add(cboAct, 1, row++);

        // ---- 按键 ----
        lay.Controls.Add(new Label { Text = "按键:", AutoSize = true, Font = lblFont, ForeColor = lblCol }, 0, row);
        var pan = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, BackColor = Color.Transparent };
        cboMouse = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80, Font = ctrlFont, BackColor = Color.White, ForeColor = Color.FromArgb(30, 60, 90) };
        cboMouse.Items.AddRange(new object[] { "左键", "中键", "右键" });
        cboMouse.SelectedIndex = pos.ActType == 0 ? pos.ActKey : 0;
        cboKey = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80, Font = ctrlFont, BackColor = Color.White, ForeColor = Color.FromArgb(30, 60, 90) };
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
            Font = ctrlFont
        };
        pan.Controls.Add(cboMouse);
        pan.Controls.Add(cboKey);
        pan.Controls.Add(btnRec);
        lay.Controls.Add(pan, 1, row++);
        
        // ---- 修饰键 ----
        lay.Controls.Add(new Label { Text = "修饰键:", AutoSize = true, Font = lblFont, ForeColor = lblCol }, 0, row);
        cboMod = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80, Font = ctrlFont, BackColor = Color.White, ForeColor = Color.FromArgb(30, 60, 90) };
        cboMod.Items.AddRange(new object[] { "无", "Ctrl", "Shift", "Alt" });
        cboMod.SelectedIndex = pos.ActType == 1 ? (pos.ModKey == 1 ? 1 : pos.ModKey == 2 ? 2 : pos.ModKey == 4 ? 3 : 0) : 0;
        lay.Controls.Add(cboMod, 1, row++);

        // ---- 模式 ----
        lay.Controls.Add(new Label { Text = "模式:", AutoSize = true, Font = lblFont, ForeColor = lblCol }, 0, row);
        cboMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80, Font = ctrlFont, BackColor = Color.White, ForeColor = Color.FromArgb(30, 60, 90) };
        cboMode.Items.AddRange(new object[] { "单击", "按下", "弹起" });
        cboMode.SelectedIndex = pos.OpMode;
        lay.Controls.Add(cboMode, 1, row++);

        // ---- 延迟 ----
        lay.Controls.Add(new Label { Text = "延迟:", AutoSize = true, Font = lblFont, ForeColor = lblCol }, 0, row);
        numWait = new NumericUpDown { Minimum = 0, Maximum = 3600000, Value = pos.WaitMs, Width = 80, Font = ctrlFont, BackColor = Color.White, ForeColor = Color.FromArgb(30, 60, 90) };
        lay.Controls.Add(numWait, 1, row++);

        // ---- ★ UIA 选项（复选框 + 选进程按钮 + 显示进程名） ----
        lay.Controls.Add(new Label { Text = "UIA:", AutoSize = true, Font = lblFont, ForeColor = lblCol }, 0, row);
        var flowUIA = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            BackColor = Color.Transparent
        };

        chkUseUIA = new CheckBox
        {
            Text = "不移动鼠标",
            AutoSize = true,
            Font = ctrlFont,
            ForeColor = Color.FromArgb(40, 60, 90),
            Checked = pos.UseUIA
        };
        flowUIA.Controls.Add(chkUseUIA);

        btnSelProc = new Button
        {
            Text = "绑定窗口",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(200, 180, 100) },
            BackColor = Color.FromArgb(245, 240, 225),
            ForeColor = Color.FromArgb(160, 120, 0),
            Font = ctrlFont
        };
        // ★ 添加悬浮提示
        ToolTip tip = new ToolTip();
        tip.SetToolTip(btnSelProc, "非必须，仅用于辅助点击（限定目标进程）");
        flowUIA.Controls.Add(btnSelProc);

        txtUIAProc = new TextBox
        {
            Text = pos.UIAProc,
            Width = 120,
            Font = ctrlFont,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90),
            ReadOnly = true
        };
        flowUIA.Controls.Add(txtUIAProc);

        lay.Controls.Add(flowUIA, 1, row++);

        // ---- 文本/组合键输入（根据类型动态显示） ----
        var flowExtra = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            BackColor = Color.Transparent
        };
        txtText = new TextBox
        {
            Text = pos.TextContent,
            Width = 150,
            Font = ctrlFont,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90),
            Visible = (pos.ActType == 2)
        };
        txtCombo = new TextBox
        {
            Text = pos.ComboKeys,
            Width = 150,
            Font = ctrlFont,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 60, 90),
            Visible = (pos.ActType == 3)
        };
        flowExtra.Controls.Add(txtText);
        flowExtra.Controls.Add(txtCombo);
        lay.Controls.Add(new Label { Text = "组合:", AutoSize = true, Font = lblFont, ForeColor = lblCol }, 0, row);
        lay.Controls.Add(flowExtra, 1, row++);

        // ---- 图像匹配区域 ----
        lay.Controls.Add(new Label { Text = "截图:", AutoSize = true, Font = lblFont, ForeColor = lblCol }, 0, row);
        var flowImg = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            BackColor = Color.Transparent,
            WrapContents = false
        };
        chkUseImg = new CheckBox { Text = "启用", AutoSize = true, Font = ctrlFont, ForeColor = Color.FromArgb(40, 60, 90) };
        chkUseImg.Checked = pos.UseImage;
        // 初始启用状态由依赖决定
        if (!CvCheck.IsReady())
        {
            chkUseImg.Enabled = false;
            if (chkUseImg.Checked)
                chkUseImg.Checked = false;
        }
        else
            chkUseImg.Enabled = true;

        // 添加勾选拦截事件（放在所有初始化之后）
        chkUseImg.CheckedChanged += (s, e) =>
        {
            if (chkUseImg.Checked && !CvCheck.IsReady())
            {
                chkUseImg.Checked = false;
                using var dlg = new MissingCvForm();
                dlg.ShowDialog(this);
            }
        };

        var btnFlow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            BackColor = Color.Transparent
        };
        btnShot = new Button
        {
            Text = "重新截图",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(100, 200, 100) },
            BackColor = Color.FromArgb(230, 245, 230),
            ForeColor = Color.FromArgb(0, 120, 0),
            Font = ctrlFont
        };
        btnLoadImg = new Button
        {
            Text = "加载图片",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(200, 180, 100) },
            BackColor = Color.FromArgb(245, 240, 225),
            ForeColor = Color.FromArgb(160, 120, 0),
            Font = ctrlFont
        };
        var btnInit = new Button
        {
            Text = "初始化",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(200, 150, 100) },
            BackColor = Color.FromArgb(255, 240, 225),
            ForeColor = Color.FromArgb(160, 80, 0),
            Font = ctrlFont
        };
        btnFlow.Controls.Add(btnInit);
        btnInit.Click += (s, e) =>
        {
            if (CvCheck.IsReady())
            {
                chkUseImg.Enabled = true;
                MessageBox.Show("截图核心已就绪，可以启用图像匹配。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                using var dlg = new MissingCvForm();
                dlg.ShowDialog(this);
            }
        };
        btnFlow.Controls.Add(btnShot);
        btnFlow.Controls.Add(btnLoadImg);
        flowImg.Controls.Add(chkUseImg);
        flowImg.Controls.Add(btnFlow);
        lay.Controls.Add(flowImg, 1, row++);

        // ---- 阈值 ----
        lay.Controls.Add(new Label { Text = "阈值:", AutoSize = true, Font = lblFont, ForeColor = lblCol }, 0, row);
        numThr = new NumericUpDown { Minimum = 0, Maximum = 1, Increment = 0.05m, DecimalPlaces = 2, Value = (decimal)pos.Threshold, Width = 80, Font = ctrlFont, BackColor = Color.White, ForeColor = Color.FromArgb(30, 60, 90) };
        lay.Controls.Add(numThr, 1, row++);

        // ---- 预览 ----
        lay.Controls.Add(new Label { Text = "预览:", AutoSize = true, Font = lblFont, ForeColor = lblCol }, 0, row);
        picTemp = new PictureBox
        {
            Size = new Size(120, 80),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.LightGray,
            SizeMode = PictureBoxSizeMode.Zoom
        };
        picTemp.Click += (s, e) =>
        {
            if (curImage == null || curImage.Length == 0)
            {
                MessageBox.Show("没有图像可预览", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var prev = new PrevForm(curImage);
            prev.ShowDialog(this);
        };
        lay.Controls.Add(picTemp, 1, row++);

        // ---- 文字匹配 ----
        lay.Controls.Add(new Label { Text = "识文字:", AutoSize = true, Font = lblFont, ForeColor = lblCol }, 0, row);
        var flowTxt = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, BackColor = Color.Transparent };
        chkUseTxt = new CheckBox { Text = "启用", AutoSize = true, Font = ctrlFont, ForeColor = Color.FromArgb(40, 60, 90) };
        chkUseTxt.Checked = pos.UseTxt;
        flowTxt.Controls.Add(chkUseTxt);
        lay.Controls.Add(flowTxt, 1, row++);

        lay.Controls.Add(new Label { Text = "文字:", AutoSize = true, Font = lblFont, ForeColor = lblCol }, 0, row);
        txtMatch = new TextBox { Text = pos.TxtMatch ?? "", Dock = DockStyle.Fill, Font = ctrlFont, BackColor = Color.White };
        lay.Controls.Add(txtMatch, 1, row++);

        // ---- 操作按钮 ----
        lay.Controls.Add(new Label { Text = "操作:", AutoSize = true, Font = lblFont, ForeColor = lblCol }, 0, row);
        var flowBtns = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, BackColor = Color.Transparent };
        btnOcr = new Button
        {
            Text = "初始化OCR",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1 },
            Font = ctrlFont
        };
        flowBtns.Controls.Add(btnOcr);
        lay.Controls.Add(flowBtns, 1, row++);

        // ---- 置信值与模式 ----
        lay.Controls.Add(new Label { Text = "置信值:", AutoSize = true, Font = lblFont, ForeColor = lblCol }, 0, row);
        numTxtTh = new NumericUpDown { Minimum = 0, Maximum = 1, Increment = 0.05m, DecimalPlaces = 2, Value = (decimal)pos.TxtThresh, Width = 80, Font = ctrlFont, BackColor = Color.White, ForeColor = Color.FromArgb(30, 60, 90) };
        lay.Controls.Add(numTxtTh, 1, row++);

        lay.Controls.Add(new Label { Text = "模式:", AutoSize = true, Font = lblFont, ForeColor = lblCol }, 0, row);
        cboTxtMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80, Font = ctrlFont, BackColor = Color.White, ForeColor = Color.FromArgb(30, 60, 90) };
        cboTxtMode.Items.AddRange(new object[] { "包含", "精确" });
        cboTxtMode.SelectedIndex = pos.TxtMode;
        lay.Controls.Add(cboTxtMode, 1, row++);

        // ---- 底部按钮 ----
        var flowOk = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Bottom, BackColor = Color.Transparent };
        var btnOk = new Button
        {
            Text = "确定",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(0, 180, 255) },
            BackColor = Color.FromArgb(225, 240, 255),
            ForeColor = Color.FromArgb(0, 80, 180),
            Font = ctrlFont
        };
        var btnCancel = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = Color.FromArgb(180, 180, 180) },
            BackColor = Color.FromArgb(240, 240, 240),
            ForeColor = Color.FromArgb(80, 80, 80),
            Font = ctrlFont
        };
        flowOk.Controls.Add(btnOk);
        flowOk.Controls.Add(btnCancel);
        lay.Controls.Add(flowOk, 0, row);
        lay.SetColumnSpan(flowOk, 2);

        this.Controls.Add(lay);
        this.AcceptButton = btnOk;
        this.CancelButton = btnCancel;

        curImage = pos.ImageTemp;
        if (curImage != null && curImage.Length > 0)
        {
            Bitmap bmp = ImgMatch.Bytes2Bmp(curImage);
            picTemp.Image = new Bitmap(bmp);
        }

        // ---- 事件绑定 ----
        cboAct.SelectedIndexChanged += (s, e) =>
        {
            Refresh();
            int type = cboAct.SelectedIndex;
            txtText.Visible = (type == 2);
            txtCombo.Visible = (type == 3);
        };
        chkUseImg.CheckedChanged += (s, e) => Refresh();
        chkUseTxt.CheckedChanged += (s, e) => Refresh();

        btnShot.Click += (s, e) =>
        {
            using var snap = new SnapForm();
            if (snap.ShowDialog() == DialogResult.OK && snap.GetImage != null)
            {
                // 直接使用 Bmp2Bytes，避免 Mat
                curImage = ImgMatch.Bmp2Bytes(snap.GetImage);
                picTemp.Image = new Bitmap(snap.GetImage, new Size(120, 80));
                chkUseImg.Checked = true;
            }
        };

        btnLoadImg.Click += (s, e) =>
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
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"加载图片失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        };

        btnRec.Click += (s, e) =>
        {
            using var rec = new RecordKey();
            if (rec.ShowDialog() == DialogResult.OK)
            {
                if (cboKey.Items.Contains((Keys)rec.RecordedKey))
                    cboKey.SelectedItem = (Keys)rec.RecordedKey;
            }
        };

        btnPick.Click += (s, e) =>
        {
            using var pick = new PickForm();
            if (pick.ShowDialog() == DialogResult.OK)
            {
                numX.Value = Math.Clamp(pick.PickPoint.X, numX.Minimum, numX.Maximum);
                numY.Value = Math.Clamp(pick.PickPoint.Y, numY.Minimum, numY.Maximum);
            }
        };

        // ★ 选进程按钮事件
        btnSelProc.Click += (s, e) =>
        {
            using var pick = new WinPick();
            if (pick.ShowDialog() == DialogResult.OK)
            {
                if (string.IsNullOrEmpty(pick.SelProc))
                {
                    // 解除绑定：清空进程名，但不改变 UIA 启用状态（仅清空进程）
                    txtUIAProc.Text = "";
                }
                else
                {
                    txtUIAProc.Text = pick.SelProc;
                    chkUseUIA.Checked = true; // 自动勾选启用
                }
            }
        };

        btnOcr.Click += (s, e) =>
        {
            if (OcrHelper.Engine == null)
            {
                // ---- 引擎未加载，执行初始化 ----
                if (!OcrHelper.Init()) return;
                MessageBox.Show("OCR引擎加载成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Refresh(); // 更新按钮状态
            }
            else
            {
                // ---- 引擎已加载，执行释放 ----
                var result = MessageBox.Show("确定要释放OCR引擎吗？释放后如需使用需重新初始化。", "确认释放",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    OcrHelper.Dispose();
                    // 同时取消文字匹配勾选（因为引擎已释放）
                    chkUseTxt.Checked = false;
                    Refresh(); // 更新按钮状态
                }
            }
        };

        btnOk.Click += (s, e) =>
        {
            NewX = (int)numX.Value;
            NewY = (int)numY.Value;
            NewDesc = txtDesc.Text.Trim();
            NewActType = cboAct.SelectedIndex;
            if (NewActType == 0)
            {
                NewActKey = cboMouse.SelectedIndex;
                NewModKey = 0;
            }
            else
            {
                NewActKey = (int)((Keys)cboKey.SelectedItem);
                NewModKey = cboMod.SelectedIndex switch { 1 => 1, 2 => 2, 3 => 4, _ => 0 };
            }
            NewOpMode = cboMode.SelectedIndex;
            NewWaitMs = (int)numWait.Value;
            NewUseImageMatch = chkUseImg.Checked;
            NewImageTemplate = curImage;
            NewThreshold = (float)numThr.Value;
            NewUseTxt = chkUseTxt.Checked;
            NewTxtMatch = txtMatch.Text.Trim();
            NewTxtMode = cboTxtMode.SelectedIndex;
            NewTxtThresh = (float)numTxtTh.Value;
            NewUseUIA = chkUseUIA.Checked;    // ★
            NewUIAProc = txtUIAProc.Text.Trim();
            NewTextContent = txtText.Text.Trim();
            NewComboKeys = txtCombo.Text.Trim();
        };

        Refresh();
    }

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

        bool ocrReady = OcrHelper.Engine != null;
        bool canUseOcr = img && ocrReady;

        // 文字匹配相关控件
        bool txt = chkUseTxt.Checked && canUseOcr;
        if (!img && chkUseTxt.Checked) chkUseTxt.Checked = false;
        chkUseTxt.Enabled = canUseOcr;
        txtMatch.Enabled = txt;
        numTxtTh.Enabled = txt;
        cboTxtMode.Enabled = txt;

        btnOcr.Enabled = img;
        if (img)
        {
            if (ocrReady)
            {
                // ★ 引擎已加载 → 显示“释放OCR”
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
        else
        {
            btnOcr.Text = "初始化OCR";
            btnOcr.BackColor = SystemColors.Control;
            btnOcr.ForeColor = SystemColors.GrayText;
            btnOcr.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
        }
    }
}