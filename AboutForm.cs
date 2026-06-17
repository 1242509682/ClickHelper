using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ClickHelper
{
    public partial class AboutForm : Form
    {
        private Config cfg;

        internal AboutForm(Config config)
        {
            cfg = config;
            InitializeComponents();
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;

            this.FormClosing += (s, e) =>
            {
                if (chkNoShow.Checked)
                {
                    cfg.SkipAbout = true;
                    cfg.Save();
                }
            };
        }

        private void InitializeComponents()
        {
            this.lblTitle = new Label();
            this.lblVersion = new LinkLabel();
            this.lblAuthor = new Label();
            this.lblFeaturesTitle = new Label();
            this.lblFeaturesList = new Label();
            this.chkNoShow = new CheckBox();
            this.btnOk = new Button();

            this.SuspendLayout();

            // 标题
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new Font("微软雅黑", 14F, FontStyle.Bold);
            this.lblTitle.Text = "点击助手";
            this.lblTitle.Location = new Point(15, 15);

            // 版本号
            this.lblVersion.AutoSize = true;
            this.lblVersion.Font = new Font("微软雅黑", 9F);
            this.lblVersion.Text = $"版本 {Program.ver}";
            this.lblVersion.LinkArea = new LinkArea(0, this.lblVersion.Text.Length);
            this.lblVersion.LinkBehavior = LinkBehavior.HoverUnderline;
            this.lblVersion.LinkClicked += (s, e) => OpenGitHub();
            this.lblVersion.Location = new Point(15, 50);

            // 作者
            this.lblAuthor.AutoSize = true;
            this.lblAuthor.Font = new Font("微软雅黑", 9F);
            this.lblAuthor.Text = "开发 羽学QQ1242509682";
            this.lblAuthor.Location = new Point(15, 78);

            // 改进功能标题
            this.lblFeaturesTitle.AutoSize = true;
            this.lblFeaturesTitle.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            this.lblFeaturesTitle.Text = "✨ 改进功能：";
            this.lblFeaturesTitle.Location = new Point(15, 110);

            // 功能列表（高度增加到 150 以容纳所有行）
            this.lblFeaturesList.AutoSize = false;
            this.lblFeaturesList.Font = new Font("微软雅黑", 9F);
            this.lblFeaturesList.Size = new Size(280, 150);
            this.lblFeaturesList.Text =
                "• 宏管理器（录制/回放/编辑）\n" +
                "• 位置列表支持Esc与Ctrl+C\n" +
                "• 指定坐标延迟执行\n" +
                "• 手动记录坐标音效\n" +
                "• 自动最小化运行\n" +
                "• 运行状态显示\n" +
                "• 禁止程序多开";
            this.lblFeaturesList.Location = new Point(15, 138);

            // 复选框
            this.chkNoShow.AutoSize = true;
            this.chkNoShow.Font = new Font("微软雅黑", 9F);
            this.chkNoShow.Text = "不再显示此窗口";
            this.chkNoShow.Location = new Point(15, 300);

            // 确定按钮
            this.btnOk.Text = "确定";
            this.btnOk.DialogResult = DialogResult.OK;
            this.btnOk.Size = new Size(80, 28);
            this.btnOk.Location = new Point(15, 330);

            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.lblVersion);
            this.Controls.Add(this.lblAuthor);
            this.Controls.Add(this.lblFeaturesTitle);
            this.Controls.Add(this.lblFeaturesList);
            this.Controls.Add(this.chkNoShow);
            this.Controls.Add(this.btnOk);

            this.ClientSize = new Size(230, 380);   // 固定大小
            this.Text = "关于点击助手";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void OpenGitHub()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/1242509682/ClickHelper",
                UseShellExecute = true
            });
        }

        private Label lblTitle;
        private LinkLabel lblVersion;
        private Label lblAuthor;
        private Label lblFeaturesTitle;
        private Label lblFeaturesList;
        private CheckBox chkNoShow;
        private Button btnOk;
    }
}