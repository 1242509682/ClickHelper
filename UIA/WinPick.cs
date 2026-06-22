using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace ClickHelper;

/// <summary> UI 探测器：仿 PickForm 风格，全屏半透明，点击即获取 UIA 属性 </summary>
public class WinPick : Form
{
    #region 字段与属性
    public string SelProc { get; private set; } = "";
    public string SelAutoId { get; private set; } = "";
    public string SelName { get; private set; } = "";
    public string SelClass { get; private set; } = "";
    public Rectangle SelRect { get; private set; } = Rectangle.Empty;
    #endregion

    #region 构造与初始化
    public WinPick()
    {
        // 1. 基础窗口设置 (完全参考 PickForm)
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        TopMost = true;
        BackColor = Color.Black;
        Opacity = 0.15; // 轻微透明，便于观察背景
        KeyPreview = true;
        ShowInTaskbar = false;
        Cursor = Cursors.Cross; // 使用十字光标，提示用户正在拾取

        // 2. 键盘事件：ESC 取消
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        };

        // 3. 鼠标事件：左键点击拾取
        MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                PickElement(e.Location);
            }
            else if (e.Button == MouseButtons.Right)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        };

        // 4. 提示文字 (参考 PickForm 的布局)
        var lbl = new System.Windows.Forms.Label
        {
            Text = "点击控件获取 UIA 属性 | 右键/ESC 取消",
            ForeColor = Color.White,
            BackColor = Color.FromArgb(80, 0, 0, 0),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei", 14F, FontStyle.Bold),
            Size = new Size(500, 60)
        };

        // 底部居中
        int screenW = Screen.PrimaryScreen.Bounds.Width;
        int screenH = Screen.PrimaryScreen.Bounds.Height;
        lbl.Location = new Point((screenW - lbl.Width) / 2, screenH - lbl.Height - 30);

        Controls.Add(lbl);
    }
    #endregion

    #region 核心逻辑
    /// <summary>
    /// 在指定屏幕坐标拾取 UIA 元素
    /// </summary>
    private void PickElement(Point pt)
    {
        using (var auto = new UIA3Automation())
        {
            AutomationElement el = null;
            try
            {
                // 临时隐藏自身以避免抓到自己 (虽然 Opacity 0.15 通常不会干扰 FromPoint，但为了保险)
                Visible = false;
                System.Threading.Thread.Sleep(10); // 等待窗口隐藏生效

                el = auto.FromPoint(pt);

                // 过滤自身进程
                if (el != null && el.Properties.ProcessId.ValueOrDefault == Process.GetCurrentProcess().Id)
                {
                    el = null;
                }
            }
            catch { }
            finally { Visible = true; }

            if (el == null)
            {
                MessageBox.Show("未检测到有效的 UI 元素。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 吸附逻辑：向上查找更有意义的父级 (最多2层)
            var tmp = el;
            for (int i = 0; i < 2; i++)
            {
                if (!string.IsNullOrEmpty(SafeStr(tmp.Properties.AutomationId))) break;
                var par = tmp.FindFirst(TreeScope.Ancestors, TrueCondition.Default);
                if (par == null) break;
                tmp = par;
            }
            el = tmp;

            // 填充返回结果
            try
            {
                var pid = el.Properties.ProcessId.ValueOrDefault;
                if (pid > 0)
                {
                    using (var p = Process.GetProcessById(pid))
                        SelProc = p.ProcessName;
                }
            }
            catch { SelProc = ""; }

            SelAutoId = SafeStr(el.Properties.AutomationId);
            SelName = SafeStr(el.Properties.Name);
            SelClass = SafeStr(el.Properties.ClassName);

            var r = el.BoundingRectangle;
            SelRect = new Rectangle((int)r.Left, (int)r.Top, (int)r.Width, (int)r.Height);

            // 显示一个简单动画
            new AnimForm(pt).Show(); 

            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// <summary> 安全获取字符串属性 </summary>
    private string SafeStr(AutomationProperty<string> prop)
    {
        try { return prop.Value ?? string.Empty; }
        catch { return string.Empty; }
    }
    #endregion
}