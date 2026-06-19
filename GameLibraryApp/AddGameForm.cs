using System;
using System.Drawing;
using System.Windows.Forms;

namespace GameLibraryApp
{
    public class AddGameForm : Form
    {
        public GameItem NewGame { get; private set; } = new GameItem();

        private TextBox txtCode = null!;
        private TextBox txtTitle = null!;
        private ComboBox cmbPlatform = null!;
        private TextBox txtCircle = null!;
        private TextBox txtExePath = null!;
        private TextBox txtNotes = null!;
        private Button btnFetch = null!;
        private Button btnBrowse = null!;
        private Button btnSave = null!;

        public AddGameForm() { SetupUI(); }

        private void SetupUI()
        {
            this.Text = "新增遊戲至庫中";
            this.Size = new Size(520, 460);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(27, 40, 56);
            this.ForeColor = Color.White;
            this.Font = new Font("Microsoft JhengHei", 10, FontStyle.Regular);

            // 遊戲編號 (ID / RJ號)
            Label lblCode = new Label { Text = "遊戲代碼：", Location = new Point(30, 25), AutoSize = true };
            txtCode = new TextBox { Location = new Point(130, 22), Width = 200, BackColor = Color.FromArgb(23, 26, 33), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

            btnFetch = new Button { Text = "🔍 聯網抓取", Location = new Point(340, 20), Width = 100, Height = 28, BackColor = Color.FromArgb(255, 64, 129), FlatStyle = FlatStyle.Flat };
            btnFetch.FlatAppearance.BorderSize = 0;
            btnFetch.Click += AsyncBtnFetch_Click;

            // 遊戲名稱
            Label lblTitle = new Label { Text = "遊戲名稱：", Location = new Point(30, 75), AutoSize = true };
            txtTitle = new TextBox { Location = new Point(130, 72), Width = 310, BackColor = Color.FromArgb(23, 26, 33), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

            // 平台
            Label lblPlatform = new Label { Text = "遊戲平台：", Location = new Point(30, 125), AutoSize = true };
            cmbPlatform = new ComboBox { Location = new Point(130, 122), Width = 310, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(23, 26, 33), ForeColor = Color.White };
            cmbPlatform.Items.AddRange(new string[] { "Steam", "DLsite" });
            cmbPlatform.SelectedIndex = 0;

            // 製作廠商 / 社團
            Label lblCircle = new Label { Text = "社團/廠商：", Location = new Point(30, 175), AutoSize = true };
            txtCircle = new TextBox { Location = new Point(130, 172), Width = 310, BackColor = Color.FromArgb(23, 26, 33), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

            // 執行檔路徑
            Label lblPath = new Label { Text = "執行檔路徑：", Location = new Point(30, 225), AutoSize = true };
            txtExePath = new TextBox { Location = new Point(130, 222), Width = 220, BackColor = Color.FromArgb(23, 26, 33), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, ReadOnly = true };
            btnBrowse = new Button { Text = "瀏覽...", Location = new Point(360, 220), Width = 80, BackColor = Color.FromArgb(42, 113, 212), FlatStyle = FlatStyle.Flat };
            btnBrowse.FlatAppearance.BorderSize = 0;
            btnBrowse.Click += BtnBrowse_Click;

            // 備忘錄
            Label lblNotes = new Label { Text = "遊戲備忘：", Location = new Point(30, 275), AutoSize = true };
            txtNotes = new TextBox { Location = new Point(130, 272), Width = 310, Height = 70, Multiline = true, BackColor = Color.FromArgb(23, 26, 33), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

            // 儲存按鈕
            btnSave = new Button { Text = "確認匯入", Location = new Point(350, 365), Width = 110, Height = 35, BackColor = Color.FromArgb(92, 184, 92), FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft JhengHei", 10, FontStyle.Bold) };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;

            this.Controls.AddRange(new Control[] { lblCode, txtCode, btnFetch, lblTitle, txtTitle, lblPlatform, cmbPlatform, lblCircle, txtCircle, lblPath, txtExePath, btnBrowse, lblNotes, txtNotes, btnSave });
        }

        private async void AsyncBtnFetch_Click(object? sender, EventArgs e)
        {
            string code = txtCode.Text.Trim();
            if (string.IsNullOrEmpty(code)) { MessageBox.Show("請先輸入遊戲代碼！(如: RJ01177942 或 2127010)"); return; }

            btnFetch.Text = "抓取中...";
            btnFetch.Enabled = false;

            var fetchedGame = await MetadataFetcher.FetchAsync(code, cmbPlatform.SelectedItem?.ToString() ?? "Steam");

            if (fetchedGame != null)
            {
                txtTitle.Text = fetchedGame.Title;
                txtCircle.Text = fetchedGame.Circle;
                // 將爬到的資訊封裝進暫存物件
                NewGame.ReleaseDate = fetchedGame.ReleaseDate;
                NewGame.CoverImagePath = fetchedGame.CoverImagePath; // 先存網址
                MessageBox.Show("成功解析並對齊網路資料！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("無法從網路撈取資料，請手動輸入欄位。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            btnFetch.Text = "🔍 聯網抓取";
            btnFetch.Enabled = true;
        }

        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "應用程式 (*.exe)|*.exe" })
            {
                if (ofd.ShowDialog() == DialogResult.OK) txtExePath.Text = ofd.FileName;
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text)) { MessageBox.Show("遊戲名稱不能為空！"); return; }

            NewGame.Code = string.IsNullOrWhiteSpace(txtCode.Text) ? "MANUAL" : txtCode.Text.Trim().ToUpper();
            NewGame.Title = txtTitle.Text.Trim();
            NewGame.Platform = cmbPlatform.SelectedItem?.ToString() ?? "Steam";
            NewGame.Circle = txtCircle.Text.Trim();
            NewGame.ExePath = txtExePath.Text;
            NewGame.Notes = txtNotes.Text.Trim();

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}