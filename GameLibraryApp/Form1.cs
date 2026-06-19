using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GameLibraryApp
{
    public partial class Form1 : Form
    {
        // === UI 控制項宣告 ===
        private Panel sidebarPanel = null!;
        private ListBox categoryListBox = null!;
        private Button btnAddGame = null!;

        private ListView gamesListView = null!;

        private Panel detailsPanel = null!;
        private Label lblGameTitle = null!;
        private Label lblPlatform = null!;
        private Label lblNotes = null!;
        private Button btnLaunch = null!;

        // === 資料變數 ===
        private List<GameItem> allGames = new List<GameItem>();
        private GameItem? selectedGame = null; // 追蹤當前選取的遊戲

        public Form1()
        {
            InitializeComponent();
            SetupCustomUI();  // 呼叫自訂的高質感介面佈局
            LoadGameData();   // 載入 JSON 檔案資料

            // === 綁定事件 ===
            btnAddGame.Click += BtnAddGame_Click;
            gamesListView.SelectedIndexChanged += GamesListView_SelectedIndexChanged;
            btnLaunch.Click += BtnLaunch_Click;
        }

        /// <summary>
        /// 純程式碼建構 Steam 風格的 UI 介面
        /// </summary>
        private void SetupCustomUI()
        {
            // 1. 主表單設定
            this.Text = "PixelVault - 跨平台遊戲庫管理器";
            this.Size = new Size(1050, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(27, 40, 56);
            this.ForeColor = Color.White;

            // 2. 左側側邊欄 Panel
            sidebarPanel = new Panel { Width = 220, Dock = DockStyle.Left, BackColor = Color.FromArgb(23, 26, 33) };

            categoryListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(23, 26, 33),
                BorderStyle = BorderStyle.None,
                Font = new Font("Microsoft JhengHei", 12, FontStyle.Bold),
                ItemHeight = 45,
                DrawMode = DrawMode.OwnerDrawFixed
            };
            categoryListBox.Items.AddRange(new object[] { " 🎮 所有遊戲", " ⭐ 我的收藏", " 🌐 Steam", " 🔞 DLsite" });
            categoryListBox.SelectedIndex = 0;

            categoryListBox.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                e.DrawBackground();
                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                Brush textBrush = isSelected ? Brushes.White : new SolidBrush(Color.FromArgb(141, 150, 157));
                if (isSelected)
                {
                    e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(42, 113, 212)), e.Bounds);
                }
                e.Graphics.DrawString(categoryListBox.Items[e.Index].ToString(), e.Font ?? this.Font, textBrush, e.Bounds.X + 5, e.Bounds.Y + 10);
            };
            sidebarPanel.Controls.Add(categoryListBox);

            btnAddGame = new Button
            {
                Text = "➕ 新增遊戲至庫",
                Height = 50,
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(42, 113, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnAddGame.FlatAppearance.BorderSize = 0;
            sidebarPanel.Controls.Add(btnAddGame);

            // 3. 右側詳細資訊欄 Panel
            detailsPanel = new Panel { Width = 320, Dock = DockStyle.Right, BackColor = Color.FromArgb(23, 26, 33), Padding = new Padding(20) };

            lblGameTitle = new Label { Text = "請選擇遊戲", Font = new Font("Microsoft JhengHei", 18, FontStyle.Bold), ForeColor = Color.White, Dock = DockStyle.Top, Height = 50 };
            lblPlatform = new Label { Text = "平台: --", Font = new Font("Microsoft JhengHei", 11, FontStyle.Regular), ForeColor = Color.FromArgb(141, 150, 157), Dock = DockStyle.Top, Height = 30 };
            lblNotes = new Label { Text = "備忘錄:\n尚未選擇任何遊戲。", Font = new Font("Microsoft JhengHei", 10, FontStyle.Regular), ForeColor = Color.DarkGray, Dock = DockStyle.Fill, Padding = new Padding(0, 20, 0, 0) };

            btnLaunch = new Button
            {
                Text = "▶ 啟動遊戲",
                Height = 55,
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(92, 184, 92),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei", 14, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Visible = false
            };
            btnLaunch.FlatAppearance.BorderSize = 0;

            detailsPanel.Controls.Add(lblNotes);
            detailsPanel.Controls.Add(lblPlatform);
            detailsPanel.Controls.Add(lblGameTitle);
            detailsPanel.Controls.Add(btnLaunch);

            // 4. 中央主遊戲列表 (ListView)
            gamesListView = new ListView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(27, 40, 56),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                View = View.Details,
                FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                Font = new Font("Microsoft JhengHei", 11, FontStyle.Regular)
            };
            gamesListView.Columns.Add("遊戲名稱", 350);
            gamesListView.Columns.Add("平台", 120);

            this.Controls.Add(gamesListView);
            this.Controls.Add(detailsPanel);
            this.Controls.Add(sidebarPanel);
        }

        /// <summary>
        /// 當使用者點選列表中的遊戲時觸發
        /// </summary>
        private void GamesListView_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // 檢查是否有選取項目
            if (gamesListView.SelectedItems.Count > 0)
            {
                // 從被點選項目的 Tag 中把當初綁定的 GameItem 物件挖出來
                selectedGame = gamesListView.SelectedItems[0].Tag as GameItem;

                if (selectedGame != null)
                {
                    lblGameTitle.Text = selectedGame.Title;
                    lblPlatform.Text = $"平台: {selectedGame.Platform}";

                    // 格式化最後遊玩時間
                    string lastPlayedStr = selectedGame.LastPlayed.HasValue
                        ? selectedGame.LastPlayed.Value.ToString("yyyy/MM/dd HH:mm")
                        : "從未遊玩";

                    lblNotes.Text = $"最後遊玩：{lastPlayedStr}\n\n備忘錄：\n{selectedGame.Notes}";
                    btnLaunch.Visible = true; // 秀出啟動按鈕
                }
            }
            else
            {
                // 沒選取任何東西時，重設介面
                selectedGame = null;
                lblGameTitle.Text = "請選擇遊戲";
                lblPlatform.Text = "平台: --";
                lblNotes.Text = "備忘錄:\n尚未選擇任何遊戲。";
                btnLaunch.Visible = false;
            }
        }

        /// <summary>
        /// 點擊「▶ 啟動遊戲」的核心邏輯
        /// </summary>
        private void BtnLaunch_Click(object? sender, EventArgs e)
        {
            if (selectedGame == null) return;

            // 防呆：如果根本沒選路徑
            if (string.IsNullOrWhiteSpace(selectedGame.ExePath))
            {
                MessageBox.Show("此遊戲未設定正確的執行檔路徑！", "無法啟動", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // 💡 關鍵細節：現代 .NET Core 之後，Process.Start 預設關閉了 Shell 執行
                // 我們必須手動將 UseShellExecute 設為 true，否則執行檔路徑包含空白時會引發當機
                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = selectedGame.ExePath,
                    UseShellExecute = true,
                    WorkingDirectory = System.IO.Path.GetDirectoryName(selectedGame.ExePath) // 設定工作目錄，防止遊戲找不到自家的資料檔
                };

                System.Diagnostics.Process.Start(startInfo);

                // 更新遊玩時間
                selectedGame.LastPlayed = DateTime.Now;
                GameDataManager.SaveGames(allGames);

                // 即時重新整理右側文字面板
                string lastPlayedStr = selectedGame.LastPlayed.Value.ToString("yyyy/MM/dd HH:mm");
                lblNotes.Text = $"最後遊玩：{lastPlayedStr}\n\n備忘錄：\n{selectedGame.Notes}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"遊戲啟動失敗！\n錯誤訊息：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnAddGame_Click(object? sender, EventArgs e)
        {
            using (AddGameForm addForm = new AddGameForm())
            {
                if (addForm.ShowDialog(this) == DialogResult.OK)
                {
                    GameItem newGame = addForm.NewGame;
                    allGames.Add(newGame);
                    GameDataManager.SaveGames(allGames);
                    RefreshGameList();
                    MessageBox.Show($"成功將《{newGame.Title}》新增至您的庫中！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void LoadGameData()
        {
            allGames = GameDataManager.LoadGames();
            RefreshGameList();
        }

        private void RefreshGameList()
        {
            gamesListView.Items.Clear();
            foreach (var game in allGames)
            {
                ListViewItem item = new ListViewItem(game.Title);
                item.SubItems.Add(game.Platform);
                item.Tag = game;
                gamesListView.Items.Add(item);
            }
        }
    }
}