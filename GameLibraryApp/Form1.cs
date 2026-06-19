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
        private ComboBox cmbTag = null!;
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

        private bool isScaling = false;

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
            cmbTag.SelectedIndexChanged += (s, e) => FilterAndRefreshGrid();
            cmbSort.SelectedIndexChanged += (s, e) => FilterAndRefreshGrid();

            gamesFlowPanel.SizeChanged += (s, e) => CenterFlowPanelCards();
        }

        private void SetupCustomUI()
        {
            this.Text = "PixelVault 數位遊戲館藏儀表板 v10 (Ultimate UX Edition)";
            this.Size = new Size(1280, 760);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(18, 18, 18);
            this.ForeColor = Color.FromArgb(224, 224, 224);

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

            // === 【頂部控制列精密佈局修正】：精密調整座標與高度，防溢位切割 ===
            topHeaderPanel = new Panel { Height = 75, Dock = DockStyle.Top, BackColor = Color.FromArgb(25, 25, 26), Padding = new Padding(15, 10, 15, 10) };

            // 💡 修正 1：將 lblStats 高度撐高至 45px，防止兩行文字切斷
            lblStats = new Label { Text = "載入中...", Font = new Font("Microsoft JhengHei", 9), ForeColor = Color.FromArgb(136, 136, 136), Location = new Point(15, 15), Size = new Size(130, 45), TextAlign = ContentAlignment.MiddleLeft };

            // 下方控制項統一移至 Y=24，精簡下拉選單寬度為 130px 達成緊湊介面
            txtSearch = new TextBox { PlaceholderText = "搜尋代碼/名稱...", Width = 130, Location = new Point(155, 24), BackColor = Color.FromArgb(51, 51, 51), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Microsoft JhengHei", 9) };
            cmbCircle = new ComboBox { Width = 130, Location = new Point(295, 24), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(51, 51, 51), ForeColor = Color.White, Font = new Font("Microsoft JhengHei", 9) };
            cmbTag = new ComboBox { Width = 130, Location = new Point(435, 24), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(51, 51, 51), ForeColor = Color.White, Font = new Font("Microsoft JhengHei", 9) };
            cmbSort = new ComboBox { Width = 160, Location = new Point(575, 24), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(51, 51, 51), ForeColor = Color.White, Font = new Font("Microsoft JhengHei", 9) };

            cmbSort.Items.AddRange(new string[] { "依 遊戲名稱 排序", "依 發售日期 (新 ➔ 舊)", "依 發售日期 (舊 ➔ 新)", "依 最後遊玩 排序", "依 總遊玩時數 排序" });
            cmbSort.SelectedIndex = 0;
            topHeaderPanel.Controls.AddRange(new Control[] { lblStats, txtSearch, cmbCircle, cmbTag, cmbSort });

            gamesFlowPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(18, 18, 18), AutoScroll = true, BorderStyle = BorderStyle.None };

            gameContextMenu = new ContextMenuStrip { BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.White, Font = new Font("Microsoft JhengHei", 9) };

            ToolStripMenuItem menuOpenFolder = new ToolStripMenuItem("📂 開啟所在資料夾");
            menuOpenFolder.Click += (s, e) =>
            {
                if (selectedGame != null && !string.IsNullOrWhiteSpace(selectedGame.ExePath))
                {
                    if (File.Exists(selectedGame.ExePath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"/select,\"{selectedGame.ExePath}\"",
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        MessageBox.Show("找不到執行檔！可能遊戲已被移動或刪除。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };

            ToolStripMenuItem menuFav = new ToolStripMenuItem("⭐ 移入/移出收藏夾");
            menuFav.Click += (s, e) => { if (selectedGame != null) { selectedGame.IsFavorite = !selectedGame.IsFavorite; GameDataManager.SaveGames(allGames); FilterAndRefreshGrid(); } };
            ToolStripMenuItem menuDel = new ToolStripMenuItem("❌ 從資料庫永久移除");
            menuDel.Click += (s, e) => { if (selectedGame != null && MessageBox.Show("確定要將此遊戲移除館藏嗎？", "刪除確認", MessageBoxButtons.YesNo) == DialogResult.Yes) { allGames.Remove(selectedGame); GameDataManager.SaveGames(allGames); LoadGameData(); } };

            gameContextMenu.Items.AddRange(new ToolStripItem[] { menuOpenFolder, new ToolStripSeparator(), menuFav, menuDel });

            this.Controls.AddRange(new Control[] { gamesFlowPanel, topHeaderPanel, detailsPanel, sidebarPanel });
            gamesFlowPanel.BringToFront();
        }

        private void LoadGameData()
        {
            allGames = GameDataManager.LoadGames();

            var circles = allGames.Select(g => g.Circle).Where(c => c != "未知廠商" && c != "Unknown").Distinct().OrderBy(c => c).ToList();
            cmbCircle.Items.Clear();
            cmbCircle.Items.Add("所有廠商");
            foreach (var c in circles) cmbCircle.Items.Add(c);
            cmbCircle.SelectedIndex = 0;

            var tags = allGames.SelectMany(g => g.Tags).Distinct().OrderBy(t => t).ToList();
            cmbTag.Items.Clear();
            cmbTag.Items.Add("所有標籤");
            foreach (var t in tags) cmbTag.Items.Add(t);
            cmbTag.SelectedIndex = 0;

            FilterAndRefreshGrid();
        }

        private void CenterFlowPanelCards()
        {
            if (isScaling) return;
            isScaling = true;

            try
            {
                gamesFlowPanel.AutoScroll = false;
                gamesFlowPanel.SuspendLayout();

                int absoluteWidth = gamesFlowPanel.Width;
                if (absoluteWidth <= 0 || gamesFlowPanel.Controls.Count == 0) return;

                int columns = 3;
                int marginPerCard = 20;
                int totalMarginWidth = columns * marginPerCard;

                int safeWidth = absoluteWidth - SystemInformation.VerticalScrollBarWidth - 10;

                int newCardWidth = (safeWidth - totalMarginWidth) / columns;
                if (newCardWidth < 210) newCardWidth = 210;

                int newCoverHeight = (int)(newCardWidth * 0.714);
                int newCardHeight = newCoverHeight + 120;

                foreach (Control control in gamesFlowPanel.Controls)
                {
                    if (control is Panel card)
                    {
                        card.Size = new Size(newCardWidth, newCardHeight);
                        if (card.Controls["lblCode"] is Label lblCode) lblCode.Size = new Size(newCardWidth, 26);
                        if (card.Controls["pbCover"] is PictureBox pbCover) pbCover.Size = new Size(newCardWidth, newCoverHeight);
                        if (card.Controls["lblTitle"] is Label lblTitle)
                        {
                            lblTitle.Size = new Size(newCardWidth - 20, 40);
                            lblTitle.Location = new Point(10, newCoverHeight + 10);
                        }
                        if (card.Controls["lblCirc"] is Label lblCirc)
                        {
                            lblCirc.Size = new Size(newCardWidth - 20, 20);
                            lblCirc.Location = new Point(10, newCoverHeight + 55);
                        }
                        if (card.Controls["lblTime"] is Label lblTime)
                        {
                            lblTime.Size = new Size(newCardWidth - 20, 15);
                            lblTime.Location = new Point(10, newCoverHeight + 75);
                        }
                    }
                }

                int actualUsedWidth = columns * (newCardWidth + marginPerCard);
                int paddingLeft = Math.Max(0, (safeWidth - actualUsedWidth) / 2);

                gamesFlowPanel.Padding = new Padding(paddingLeft, 15, 0, 15);
            }
            finally
            {
                gamesFlowPanel.ResumeLayout(true);
                gamesFlowPanel.AutoScroll = true;
                isScaling = false;
            }
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

            if (cmbTag.SelectedIndex > 0 && cmbTag.SelectedItem != null)
            {
                string selectedTag = cmbTag.SelectedItem.ToString() ?? "";
                query = query.Where(g => g.Tags.Contains(selectedTag));
            }

            lblStats.Text = $"遊戲總數: {query.Count()} 款\n資料庫狀態: 連線正常";

            int sortIdx = cmbSort.SelectedIndex;
            if (sortIdx == 0) query = query.OrderBy(g => g.Title);
            else if (sortIdx == 1) query = query.OrderByDescending(g => g.ReleaseDate);
            else if (sortIdx == 2) query = query.OrderBy(g => g.ReleaseDate);
            else if (sortIdx == 3) query = query.OrderByDescending(g => g.LastPlayed ?? DateTime.MinValue);
            else if (sortIdx == 4) query = query.OrderByDescending(g => g.TotalPlayTime);

            foreach (var game in query)
            {
                Panel card = new Panel { Size = new Size(210, 270), Margin = new Padding(10), BackColor = Color.FromArgb(30, 30, 30), Cursor = Cursors.Hand };

                Label lblCode = new Label
                {
                    Name = "lblCode",
                    Text = game.Code,
                    Size = new Size(210, 26),
                    Location = new Point(0, 0),
                    BackColor = Color.FromArgb(190, 23, 26, 33),
                    ForeColor = Color.FromArgb(255, 64, 129),
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter
                };

                PictureBox pbCover = new PictureBox { Name = "pbCover", Size = new Size(210, 150), Location = new Point(0, 0), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(45, 45, 48) };

                if (!string.IsNullOrEmpty(game.CoverImagePath))
                {
                    if (game.CoverImagePath.StartsWith("http")) { DownloadImageAsync(game, pbCover); }
                    else if (File.Exists(game.CoverImagePath))
                    {
                        using (var img = Image.FromFile(game.CoverImagePath)) { pbCover.Image = new Bitmap(img); }
                    }
                }

                Label lblTitle = new Label
                {
                    Name = "lblTitle",
                    Text = game.IsFavorite ? $"⭐ {game.Title}" : game.Title,
                    Size = new Size(190, 40),
                    Location = new Point(10, 160),
                    Font = new Font("Microsoft JhengHei", 9, FontStyle.Bold),
                    ForeColor = Color.White,
                    AutoSize = false,
                    AutoEllipsis = true
                };

                Label lblCirc = new Label { Name = "lblCirc", Text = game.Circle, Size = new Size(190, 20), Location = new Point(10, 205), Font = new Font("Microsoft JhengHei", 8), ForeColor = Color.Gray };
                Label lblTime = new Label { Name = "lblTime", Text = $"時數: {game.TotalPlayTime} 分鐘", Size = new Size(190, 15), Location = new Point(10, 225), Font = new Font("Microsoft JhengHei", 8), ForeColor = Color.FromArgb(3, 218, 198) };

                Action selectAction = () =>
                {
                    selectedGame = game;
                    lblGameTitle.Text = game.Title;
                    lblPlatform.Text = $"代碼: {game.Code} | 平台: {game.Platform}";

                    string uiReleaseDate = (game.ReleaseDate == "1970-01-01") ? "未知" : game.ReleaseDate;
                    string displayTags = game.Tags.Count > 0 ? string.Join(", ", game.Tags) : "無標籤";

                    lblNotes.Text = $"社團/廠商：{game.Circle}\n發售日期：{uiReleaseDate}\n最後遊玩：{(game.LastPlayed.HasValue ? game.LastPlayed.Value.ToString("yyyy/MM/dd HH:mm") : "從未遊玩")}\n總遊玩時數：{game.TotalPlayTime} 分鐘\n\n遊戲標籤：\n{displayTags}\n\n個人備忘：\n{game.Notes}";
                    btnLaunch.Visible = true;
                };

                card.Click += (s, e) => selectAction();
                pbCover.Click += (s, e) => selectAction();
                lblTitle.Click += (s, e) => selectAction();

                card.ContextMenuStrip = gameContextMenu;
                pbCover.ContextMenuStrip = gameContextMenu;
                card.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) selectedGame = game; };
                pbCover.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) selectedGame = game; };

                card.Controls.AddRange(new Control[] { pbCover, lblTitle, lblCirc, lblTime, lblCode });
                lblCode.BringToFront();

                gamesFlowPanel.Controls.Add(card);
            }

            CenterFlowPanelCards();
        }

        private async void DownloadImageAsync(GameItem game, PictureBox pb)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                if (game.Platform == "DLsite") client.DefaultRequestHeaders.Add("Referer", "https://www.dlsite.com/");

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
                        using (var ms = new MemoryStream(bytes)) pb.Image = new Bitmap(ms);
                    }));
                }
            }
            catch { }
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
                    GameItem newGame = addForm.NewGame;

                    if (allGames.Any(g => g.Code == newGame.Code))
                    {
                        MessageBox.Show($"遊戲庫中已經存在代碼為「{newGame.Code}」的遊戲囉！\n系統已自動攔截重複匯入。", "重複匯入攔截", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    allGames.Add(newGame);
                    GameDataManager.SaveGames(allGames);
                    LoadGameData();
                }
            }
        }
    }
}