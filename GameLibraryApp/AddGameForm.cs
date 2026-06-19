using System;
using System.Drawing;
using System.Windows.Forms;

namespace GameLibraryApp
{
    public class AddGameForm : Form
    {
        // 供外部讀取的結果物件
        public GameItem NewGame { get; private set; } = new GameItem();

        // UI 控制項
        private TextBox txtTitle = null!;
        private ComboBox cmbPlatform = null!;
        private TextBox txtExePath = null!;
        private TextBox txtNotes = null!;
        private Button btnBrowse = null!;
        private Button btnSave = null!;
        private Button btnCancel = null!;

        public AddGameForm()
        {
            SetupUI();
        }

        private void SetupUI()
        {
            this.Text = "新增遊戲至 PixelVault";
            this.Size = new Size(500, 420);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(27, 40, 56);
            this.ForeColor = Color.White;
            this.Font = new Font("Microsoft JhengHei", 10, FontStyle.Regular);

            // 遊戲名稱標籤與輸入框
            Label lblTitle = new Label { Text = "遊戲名稱：", Location = new Point(30, 30), AutoSize = true };
            txtTitle = new TextBox { Location = new Point(130, 27), Width = 310, BackColor = Color.FromArgb(23, 26, 33), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

            // 平台標籤與下拉選單
            Label lblPlatform = new Label { Text = "遊戲平台：", Location = new Point(30, 80), AutoSize = true };
            cmbPlatform = new ComboBox { Location = new Point(130, 77), Width = 310, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(23, 26, 33), ForeColor = Color.White };
            cmbPlatform.Items.AddRange(new string[] { "Steam", "DLsite", "Epic", "Independent" });
            cmbPlatform.SelectedIndex = 0;

            // 執行檔路徑與瀏覽按鈕
            Label lblPath = new Label { Text = "執行檔路徑：", Location = new Point(30, 130), AutoSize = true };
            txtExePath = new TextBox { Location = new Point(130, 127), Width = 220, BackColor = Color.FromArgb(23, 26, 33), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, ReadOnly = true };
            btnBrowse = new Button { Text = "瀏覽...", Location = new Point(360, 125), Width = 80, BackColor = Color.FromArgb(42, 113, 212), FlatStyle = FlatStyle.Flat };
            btnBrowse.FlatAppearance.BorderSize = 0;
            btnBrowse.Click += BtnBrowse_Click;

            // 備忘錄
            Label lblNotes = new Label { Text = "備忘錄：", Location = new Point(30, 180), AutoSize = true };
            txtNotes = new TextBox { Location = new Point(130, 177), Width = 310, Height = 100, Multiline = true, BackColor = Color.FromArgb(23, 26, 33), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

            // 取消按鈕
            btnCancel = new Button { Text = "取消", Location = new Point(240, 310), Width = 90, Height = 35, BackColor = Color.FromArgb(141, 150, 157), FlatStyle = FlatStyle.Flat };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;

            // 儲存按鈕
            btnSave = new Button { Text = "儲存", Location = new Point(350, 310), Width = 90, Height = 35, BackColor = Color.FromArgb(92, 184, 92), FlatStyle = FlatStyle.Flat };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;

            // 加入至表單
            this.Controls.AddRange(new Control[] { lblTitle, txtTitle, lblPlatform, cmbPlatform, lblPath, txtExePath, btnBrowse, lblNotes, txtNotes, btnCancel, btnSave });
        }

        // 點擊「瀏覽...」按鈕：打開檔案選擇器尋找 .exe 檔
        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "應用程式 (*.exe)|*.exe|所有檔案 (*.*)|*.*";
                ofd.Title = "選擇遊戲執行檔 (.exe)";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtExePath.Text = ofd.FileName;
                    // 如果遊戲名稱還是空的，自動把檔名抓過來當作預設名稱
                    if (string.IsNullOrWhiteSpace(txtTitle.Text))
                    {
                        txtTitle.Text = System.IO.Path.GetFileNameWithoutExtension(ofd.FileName);
                    }
                }
            }
        }

        // 點擊「儲存」：驗證並包裝資料
        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text))
            {
                MessageBox.Show("請輸入遊戲名稱！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 封裝成 GameItem 物件
            NewGame = new GameItem
            {
                Title = txtTitle.Text.Trim(),
                Platform = cmbPlatform.SelectedItem?.ToString() ?? "Steam",
                ExePath = txtExePath.Text,
                Notes = txtNotes.Text.Trim()
            };

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}