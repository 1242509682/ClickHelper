using System.Drawing;
using System.Windows.Forms;

namespace ClickHelper;

public partial class PosForm
{
    public class RecordKey : Form
    {
        public Keys RecordedKey { get; private set; } = Keys.None;
        private bool rec = false;
        private Label lbl;

        public RecordKey()
        {
            this.Text = "录制按键";
            this.Size = new Size(250, 100);
            this.StartPosition = FormStartPosition.CenterParent;
            this.KeyPreview = true;
            this.BackColor = Color.FromArgb(240, 244, 248);
            lbl = new Label { Text = "按下任意键...", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("微软雅黑", 9F), ForeColor = Color.FromArgb(40, 60, 90) };
            this.Controls.Add(lbl);
            this.Shown += (s, e) => { rec = true; };
            this.KeyDown += (s, e) =>
            {
                if (rec && e.KeyCode != Keys.ControlKey && e.KeyCode != Keys.ShiftKey && e.KeyCode != Keys.Menu)
                {
                    RecordedKey = e.KeyCode;
                    rec = false;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
        }
    }
}