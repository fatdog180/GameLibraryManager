using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Windows.Forms;

namespace GameLibraryApp
{
    public partial class Form1 : Form
    {
        private Panel sidebarPanel = null!;
        private ListBox categoryListBox = null!;
        private Button btnAddGame = null!;

        private Panel topHeaderPanel = null!;
        private Label lblStats = null!;
        private TextBox txtSearch = null!;
        private ComboBox cmbCircle = null!;
        private ComboBox cmbSort = null!;

        private FlowLayoutPanel gamesFlowPanel = null!;
        private ContextMenuStrip gameContextMenu = null!;

        private Panel detailsPanel = null!;
        private Label lblGameTitle = null!;
        private Label lblPlatform = null!;
        private Label lblNotes = null!;
        private Button btnLaunch = null!;

        private List<GameItem> allGames = new List<GameItem>();
        private GameItem? selectedGame = null;

        public Form1()
        {
            InitializeComponent();
            SetupCustomUI();
            LoadGameData();

            btnAddGame.Click += BtnAddGame_Click;
            btnLaunch.Click += BtnLaunch_Click;
            categoryListBox.SelectedIndexChanged += (s, e) => FilterAndRefreshGrid();
            txtSearch.TextChanged += (s, e) => FilterAndRefreshGrid();
            cmbCircle.SelectedIndexChanged += (s, e) => FilterAndRefreshGrid();
            cmbSort.SelectedIndexChanged += (s, e) => FilterAndRefreshGrid();
        }

        private void SetupCustomUI()
        {
            this.Text = "PixelVault 數位遊戲館藏儀表板 v6.2";
            this.Size = new Size(1220, 760);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(18, 18, 18);
            this.ForeColor = Color.FromArgb(224, 224, 224);

            // 1. 左側功能選單
            sidebarPanel = new Panel { Width = 220, Dock = DockStyle.Left, BackColor = Color.FromArgb(37, 37, 38) };
            categoryListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(37, 37, 38),
                BorderStyle = BorderStyle.None,
                Font = new Font("Microsoft JhengHei", 11, FontStyle.Bold),
                ItemHeight = 45,
                DrawMode = DrawMode.OwnerDrawFixed
            };
            categoryListBox.Items.AddRange(new object[] { " 🎮 所有遊戲庫", " ⭐ 我的收藏夾", " 🌐 Steam 專區", " 🔞 DLsite 專區" });
            categoryListBox.SelectedIndex = 0;
            categoryListBox.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                e.DrawBackground();
                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                Brush textBrush = isSelected ? Brushes.White : new SolidBrush(Color.FromArgb(170, 170, 170));
                if (isSelected) e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(255, 64, 129)), e.Bounds);
                e.Graphics.DrawString(categoryListBox.Items[e.Index].ToString(), e.Font ?? this.Font, textBrush, e.Bounds.X + 5, e.Bounds.Y + 12);
            };
            sidebarPanel.Controls.Add(categoryListBox);

            btnAddGame = new Button
            {
                Text = "➕ 匯入本地遊戲",
                Height = 50,
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(255, 64, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnAddGame.FlatAppearance.BorderSize = 0;
            sidebarPanel.Controls.Add(btnAddGame);

            // 2. 右側詳細資訊面板
            detailsPanel = new Panel { Width = 300, Dock = DockStyle.Right, BackColor = Color.FromArgb(30, 30, 30), Padding = new Padding(15) };
            lblGameTitle = new Label { Text = "未選擇遊戲", Font = new Font("Microsoft JhengHei", 15, FontStyle.Bold), ForeColor = Color.FromArgb(255, 64, 129), Dock = DockStyle.Top, Height = 60 };
            lblPlatform = new Label { Text = "代碼: -- | 平台: --", Font = new Font("Microsoft JhengHei", 10), ForeColor = Color.Gray, Dock = DockStyle.Top, Height = 25 };
            lblNotes = new Label { Text = "請從中央卡片庫選取遊戲，以檢視詳細中繼資料與個人備忘錄。", Font = new Font("Microsoft JhengHei", 10), ForeColor = Color.DarkGray, Dock = DockStyle.Fill, Padding = new Padding(0, 15, 0, 0) };
            btnLaunch = new Button
            {
                Text = "▶ 啟動遊戲 (RUN)",
                Height = 50,
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(3, 218, 198),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei", 12, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Visible = false
            };
            btnLaunch.FlatAppearance.BorderSize = 0;
            detailsPanel.Controls.Add(lblNotes);
            detailsPanel.Controls.Add(lblPlatform);
            detailsPanel.Controls.Add(lblGameTitle);
            detailsPanel.Controls.Add(btnLaunch);

            // 3. 頂部多功能導覽列 (【關鍵調整】: 縮小間距與座標，完美防切割)
            topHeaderPanel = new Panel { Height = 75, Dock = DockStyle.Top, BackColor = Color.FromArgb(25, 25, 26), Padding = new Padding(15, 10, 15, 10) };
            lblStats = new Label { Text = "載入中...", Font = new Font("Microsoft JhengHei", 9), ForeColor = Color.FromArgb(136, 136, 136), Location = new Point(10, 18), Size = new Size(140, 40) };

            txtSearch = new TextBox { PlaceholderText = "搜尋 代碼 / 遊戲名稱...", Width = 160, Location = new Point(160, 22), BackColor = Color.FromArgb(51, 51, 51), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Microsoft JhengHei", 9) };

            cmbCircle = new ComboBox { Width = 160, Location = new Point(330, 22), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(51, 51, 51), ForeColor = Color.White, Font = new Font("Microsoft JhengHei", 9) };

            cmbSort = new ComboBox { Width = 180, Location = new Point(500, 22), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(51, 51, 51), ForeColor = Color.White, Font = new Font("Microsoft JhengHei", 9) };
            cmbSort.Items.AddRange(new string[] { "依 遊戲名稱 排序", "依 發售日期 排序", "依 最後遊玩 排序", "依 總遊玩時數 排序" });
            cmbSort.SelectedIndex = 0;

            topHeaderPanel.Controls.AddRange(new Control[] { lblStats, txtSearch, cmbCircle, cmbSort });

            // 4. 中央動態流動卡片區
            gamesFlowPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(18, 18, 18), AutoScroll = true, Padding = new Padding(15) };

            gameContextMenu = new ContextMenuStrip { BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.White, Font = new Font("Microsoft JhengHei", 9) };
            ToolStripMenuItem menuFav = new ToolStripMenuItem("⭐ 移入/移出收藏夾");
            menuFav.Click += (s, e) => { if (selectedGame != null) { selectedGame.IsFavorite = !selectedGame.IsFavorite; GameDataManager.SaveGames(allGames); FilterAndRefreshGrid(); } };
            ToolStripMenuItem menuDel = new ToolStripMenuItem("❌ 從資料庫永久移除");
            menuDel.Click += (s, e) => { if (selectedGame != null && MessageBox.Show("確定要將此遊戲移除館藏嗎？", "刪除確認", MessageBoxButtons.YesNo) == DialogResult.Yes) { allGames.Remove(selectedGame); GameDataManager.SaveGames(allGames); LoadGameData(); } };
            gameContextMenu.Items.AddRange(new ToolStripItem[] { menuFav, menuDel });

            this.Controls.AddRange(new Control[] { gamesFlowPanel, topHeaderPanel, detailsPanel, sidebarPanel });
        }

        private void LoadGameData()
        {
            allGames = GameDataManager.LoadGames();

            var circles = allGames.Select(g => g.Circle).Distinct().OrderBy(c => c).ToList();
            cmbCircle.Items.Clear();
            cmbCircle.Items.Add("所有社團/廠商");
            foreach (var c in circles) cmbCircle.Items.Add(c);
            cmbCircle.SelectedIndex = 0;

            FilterAndRefreshGrid();
        }

        private void FilterAndRefreshGrid()
        {
            gamesFlowPanel.Controls.Clear();

            IEnumerable<GameItem> query = allGames;
            int sidebarIdx = categoryListBox.SelectedIndex;
            if (sidebarIdx == 1) query = query.Where(g => g.IsFavorite);
            else if (sidebarIdx == 2) query = query.Where(g => g.Platform == "Steam");
            else if (sidebarIdx == 3) query = query.Where(g => g.Platform == "DLsite");

            string searchTxt = txtSearch.Text.Trim().ToUpper();
            if (!string.IsNullOrEmpty(searchTxt))
                query = query.Where(g => g.Title.ToUpper().Contains(searchTxt) || g.Code.ToUpper().Contains(searchTxt));

            if (cmbCircle.SelectedIndex > 0 && cmbCircle.SelectedItem != null)
                query = query.Where(g => g.Circle == cmbCircle.SelectedItem.ToString());

            lblStats.Text = $"遊戲總數: {query.Count()} 款\n資料庫狀態: 連線正常";

            int sortIdx = cmbSort.SelectedIndex;
            if (sortIdx == 0) query = query.OrderBy(g => g.Title);
            else if (sortIdx == 1) query = query.OrderByDescending(g => g.ReleaseDate);
            else if (sortIdx == 2) query = query.OrderByDescending(g => g.LastPlayed ?? DateTime.MinValue);
            else if (sortIdx == 3) query = query.OrderByDescending(g => g.TotalPlayTime);

            foreach (var game in query)
            {
                Panel card = new Panel { Size = new Size(210, 270), Margin = new Padding(10), BackColor = Color.FromArgb(30, 30, 30), Cursor = Cursors.Hand };
                Label lblCode = new Label { Text = game.Code, Size = new Size(110, 20), Location = new Point(5, 5), BackColor = Color.FromArgb(200, 0, 0, 0), ForeColor = Color.FromArgb(255, 64, 129), Font = new Font("Segoe UI", 8, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };

                PictureBox pbCover = new PictureBox { Size = new Size(210, 150), Location = new Point(0, 0), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(45, 45, 48) };

                // 動態封面流處理
                if (!string.IsNullOrEmpty(game.CoverImagePath))
                {
                    if (game.CoverImagePath.StartsWith("http"))
                    {
                        DownloadImageAsync(game, pbCover);
                    }
                    else if (File.Exists(game.CoverImagePath))
                    {
                        // 【非鎖定技術】: 複製到記憶體中渲染，不鎖定本地檔案
                        using (var img = Image.FromFile(game.CoverImagePath))
                        {
                            pbCover.Image = new Bitmap(img);
                        }
                    }
                }

                Label lblTitle = new Label { Text = game.IsFavorite ? $"⭐ {game.Title}" : game.Title, Size = new Size(190, 40), Location = new Point(10, 160), Font = new Font("Microsoft JhengHei", 9, FontStyle.Bold), ForeColor = Color.White };
                Label lblCirc = new Label { Text = game.Circle, Size = new Size(190, 20), Location = new Point(10, 205), Font = new Font("Microsoft JhengHei", 8), ForeColor = Color.Gray };
                Label lblTime = new Label { Text = $"時數: {game.TotalPlayTime} 分鐘", Size = new Size(190, 15), Location = new Point(10, 225), Font = new Font("Microsoft JhengHei", 8), ForeColor = Color.FromArgb(3, 218, 198) };

                Action selectAction = () =>
                {
                    selectedGame = game;
                    lblGameTitle.Text = game.Title;
                    lblPlatform.Text = $"代碼: {game.Code} | 平台: {game.Platform}";
                    lblNotes.Text = $"社團/廠商：{game.Circle}\n發售日期：{game.ReleaseDate}\n最後更新：{game.LastUpdated}\n最後遊玩：{(game.LastPlayed.HasValue ? game.LastPlayed.Value.ToString("yyyy/MM/dd HH:mm") : "從未遊玩")}\n總遊玩時數：{game.TotalPlayTime} 分鐘\n\n個人備忘：\n{game.Notes}";
                    btnLaunch.Visible = true;
                };

                card.Click += (s, e) => selectAction();
                pbCover.Click += (s, e) => selectAction();
                lblTitle.Click += (s, e) => selectAction();

                card.ContextMenuStrip = gameContextMenu;
                pbCover.ContextMenuStrip = gameContextMenu;
                card.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) selectedGame = game; };
                pbCover.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) selectedGame = game; };

                card.Controls.AddRange(new Control[] { lblCode, lblCirc, lblTime, lblTitle, pbCover });
                gamesFlowPanel.Controls.Add(card);
            }
        }

        // 【網路抓取引擎升級】: 加裝高階偽裝標頭，徹底解決 403 無封面問題
        private async void DownloadImageAsync(GameItem game, PictureBox pb)
        {
            try
            {
                using var client = new HttpClient();
                // 1. 注入主流瀏覽器偽裝
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                // 2. 針對 DLsite 特規防盜鏈進行 Referer 突破
                if (game.Platform == "DLsite")
                {
                    client.DefaultRequestHeaders.Add("Referer", "https://www.dlsite.com/");
                }

                byte[] bytes = await client.GetByteArrayAsync(game.CoverImagePath);
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "covers");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string localPath = Path.Combine(dir, $"{game.Code}.jpg");
                await File.WriteAllBytesAsync(localPath, bytes);

                game.CoverImagePath = localPath;

                if (pb.IsHandleCreated)
                {
                    pb.Invoke(new Action(() =>
                    {
                        using (var ms = new MemoryStream(bytes))
                        {
                            pb.Image = new Bitmap(ms);
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"圖片下載失敗: {ex.Message}");
            }
        }

        private void BtnLaunch_Click(object? sender, EventArgs e)
        {
            if (selectedGame == null || string.IsNullOrWhiteSpace(selectedGame.ExePath)) return;
            try
            {
                var startTime = DateTime.Now;

                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = selectedGame.ExePath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(selectedGame.ExePath)
                };

                var proc = System.Diagnostics.Process.Start(startInfo);
                selectedGame.LastPlayed = DateTime.Now;

                if (proc != null)
                {
                    proc.EnableRaisingEvents = true;
                    proc.Exited += (s, ev) =>
                    {
                        int minutes = (int)(DateTime.Now - startTime).TotalMinutes;
                        selectedGame.TotalPlayTime += Math.Max(1, minutes);
                        GameDataManager.SaveGames(allGames);
                        this.Invoke(new Action(() => FilterAndRefreshGrid()));
                    };
                }

                GameDataManager.SaveGames(allGames);
                FilterAndRefreshGrid();
            }
            catch (Exception ex) { MessageBox.Show($"遊戲啟動失敗: {ex.Message}"); }
        }

        private void BtnAddGame_Click(object? sender, EventArgs e)
        {
            using (AddGameForm addForm = new AddGameForm())
            {
                if (addForm.ShowDialog(this) == DialogResult.OK)
                {
                    allGames.Add(addForm.NewGame);
                    GameDataManager.SaveGames(allGames);
                    LoadGameData();
                }
            }
        }
    }
}