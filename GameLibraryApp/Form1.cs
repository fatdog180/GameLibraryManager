using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GameLibraryApp
{
    public partial class Form1 : Form
    {
        private Panel sidebarPanel = null!;
        private ListBox categoryListBox = null!;
        private Button btnAddGame = null!;
        private Button btnBatchImport = null!;

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
        private Panel pnlLoading = null!;

        // === 側欄項目定義 ===
        // 索引 0-3：平台/收藏分類；索引 4：分隔線；索引 5-8：遊玩狀態分類
        private static readonly string[] SidebarItems = new[]
        {
            " 🎮 所有遊戲庫",
            " ⭐ 我的收藏夾",
            " 🌐 Steam 專區",
            " 🔞 DLsite 專區",
            "─────────────",   // 分隔線（索引 4，不可選）
            " 🏆 已通關",
            " 🎮 正在玩",
            " 📦 尚未遊玩",
            " 💖 願望清單"
        };
        private const int SidebarSeparatorIndex = 4;

        // 將側欄索引對應到 PlayStatus
        private static readonly Dictionary<int, PlayStatus> SidebarStatusMap = new()
        {
            { 5, PlayStatus.Completed },
            { 6, PlayStatus.Playing },
            { 7, PlayStatus.Unplayed },
            { 8, PlayStatus.Wishlist }
        };

        // 各狀態對應的顏色（用於 Badge 和側欄高亮）
        private static readonly Dictionary<PlayStatus, Color> StatusColors = new()
        {
            { PlayStatus.Completed, Color.FromArgb(255, 215, 0) },    // 金色
            { PlayStatus.Playing,   Color.FromArgb(76, 175, 80) },    // 綠色
            { PlayStatus.Unplayed,  Color.FromArgb(120, 120, 120) },  // 灰色
            { PlayStatus.Wishlist,  Color.FromArgb(255, 64, 129) }    // 粉紅
        };

        // 各狀態對應的顯示文字
        private static readonly Dictionary<PlayStatus, string> StatusLabels = new()
        {
            { PlayStatus.Completed, "🏆 已通關" },
            { PlayStatus.Playing,   "🎮 正在玩" },
            { PlayStatus.Unplayed,  "📦 尚未遊玩" },
            { PlayStatus.Wishlist,  "💖 願望清單" }
        };

        public Form1()
        {
            InitializeComponent();
            SetupCustomUI();
            InitializeLoadingPanel();

            this.Shown += async (s, e) => 
            {
                this.Refresh(); // 強制視窗立刻重繪（畫出 Loading 面板）
                await Task.Delay(100); // 讓 UI 執行緒稍微喘息，確保佈局完成
                await LoadGameDataAsync();
            };

            btnAddGame.Click += BtnAddGame_Click;
            btnBatchImport.Click += BtnBatchImport_Click;
            btnLaunch.Click += BtnLaunch_Click;
            categoryListBox.SelectedIndexChanged += CategoryListBox_SelectedIndexChanged;
            txtSearch.TextChanged += (s, e) => FilterAndRefreshGrid();
            cmbCircle.SelectedIndexChanged += (s, e) => FilterAndRefreshGrid();
            cmbTag.SelectedIndexChanged += (s, e) => FilterAndRefreshGrid();
            cmbSort.SelectedIndexChanged += (s, e) => FilterAndRefreshGrid();

            gamesFlowPanel.SizeChanged += (s, e) => CenterFlowPanelCards();
        }

        private void CategoryListBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // 若點到分隔線，自動跳回前一個有效項目（預設跳回索引 0）
            if (categoryListBox.SelectedIndex == SidebarSeparatorIndex)
            {
                categoryListBox.SelectedIndex = 0;
                return;
            }
            FilterAndRefreshGrid();
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
            categoryListBox.Items.AddRange(SidebarItems);
            categoryListBox.SelectedIndex = 0;
            categoryListBox.DrawItem += CategoryListBox_DrawItem;
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

            btnBatchImport = new Button
            {
                Text = "📦 批量匯入",
                Height = 50,
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(38, 90, 170),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnBatchImport.FlatAppearance.BorderSize = 0;

            // 加入順序決定 DockStyle.Bottom 停靠位置：後加入者優先度較高，停靠在最底部
            // → btnBatchImport（先加，index+1）停靠在 btnAddGame（後加，index+2）上方
            sidebarPanel.Controls.Add(btnBatchImport); // 上方
            sidebarPanel.Controls.Add(btnAddGame);     // 最底部

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
            menuFav.Click += async (s, e) => { if (selectedGame != null) { selectedGame.IsFavorite = !selectedGame.IsFavorite; await SaveGamesHelperAsync(); FilterAndRefreshGrid(); } };

            // === 【新增】更改遊玩狀態子選單 ===
            ToolStripMenuItem menuStatus = new ToolStripMenuItem("📋 更改遊玩狀態");
            var menuStatusCompleted = new ToolStripMenuItem("🏆 標記為已通關");
            var menuStatusPlaying   = new ToolStripMenuItem("🎮 標記為正在玩");
            var menuStatusUnplayed  = new ToolStripMenuItem("📦 標記為尚未遊玩");
            var menuStatusWishlist  = new ToolStripMenuItem("💖 加入願望清單");

            menuStatusCompleted.Click += (s, e) => SetSelectedGameStatus(PlayStatus.Completed);
            menuStatusPlaying.Click   += (s, e) => SetSelectedGameStatus(PlayStatus.Playing);
            menuStatusUnplayed.Click  += (s, e) => SetSelectedGameStatus(PlayStatus.Unplayed);
            menuStatusWishlist.Click  += (s, e) => SetSelectedGameStatus(PlayStatus.Wishlist);

            menuStatus.DropDownItems.AddRange(new ToolStripItem[]
            {
                menuStatusCompleted, menuStatusPlaying, menuStatusUnplayed, menuStatusWishlist
            });

            ToolStripMenuItem menuDel = new ToolStripMenuItem("❌ 從資料庫永久移除");
            menuDel.Click += async (s, e) => { if (selectedGame != null && MessageBox.Show("確定要將此遊戲移除館藏嗎？", "刪除確認", MessageBoxButtons.YesNo) == DialogResult.Yes) { allGames.Remove(selectedGame); await SaveGamesHelperAsync(); await LoadGameDataAsync(); } };

            gameContextMenu.Items.AddRange(new ToolStripItem[] { menuOpenFolder, new ToolStripSeparator(), menuFav, menuStatus, new ToolStripSeparator(), menuDel });

            this.Controls.AddRange(new Control[] { gamesFlowPanel, topHeaderPanel, detailsPanel, sidebarPanel });
            gamesFlowPanel.BringToFront();
        }

        /// <summary>
        /// 自訂側欄 ListBox 繪製：分隔線項目、狀態項目各用不同顏色
        /// </summary>
        private void CategoryListBox_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            e.DrawBackground();

            string itemText = categoryListBox.Items[e.Index]?.ToString() ?? "";

            // 分隔線：灰色橫線樣式，不可被選取
            if (e.Index == SidebarSeparatorIndex)
            {
                int midY = e.Bounds.Y + e.Bounds.Height / 2;
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(37, 37, 38)), e.Bounds);
                e.Graphics.DrawLine(new Pen(Color.FromArgb(80, 80, 80), 1), e.Bounds.X + 10, midY, e.Bounds.Right - 10, midY);
                return;
            }

            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            bool isStatusItem = SidebarStatusMap.ContainsKey(e.Index);

            if (isSelected)
            {
                // 選中狀態項目時，用對應狀態顏色高亮
                Color highlightColor = isStatusItem
                    ? StatusColors[SidebarStatusMap[e.Index]]
                    : Color.FromArgb(255, 64, 129);
                e.Graphics.FillRectangle(new SolidBrush(highlightColor), e.Bounds);
                e.Graphics.DrawString(itemText, e.Font ?? this.Font, Brushes.White, e.Bounds.X + 5, e.Bounds.Y + 12);
            }
            else
            {
                // 未選中：狀態項目用其對應淡色，平台項目用預設灰色
                Color textColor = isStatusItem
                    ? StatusColors[SidebarStatusMap[e.Index]]
                    : Color.FromArgb(170, 170, 170);
                e.Graphics.DrawString(itemText, e.Font ?? this.Font, new SolidBrush(textColor), e.Bounds.X + 5, e.Bounds.Y + 12);
            }
        }

        private async Task SaveGamesHelperAsync()
        {
            var result = await GameDataManager.SaveGamesAsync(allGames);
            if (!result.success)
            {
                MessageBox.Show(result.errorMessage, "存檔失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadGameDataAsync()
        {
            var result = await GameDataManager.LoadGamesAsync();
            if (!result.success && !string.IsNullOrEmpty(result.errorMessage))
            {
                MessageBox.Show(result.errorMessage, "讀取錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            allGames = result.games;

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
                            lblTime.Size = new Size(newCardWidth - 60, 15);
                            lblTime.Location = new Point(10, newCoverHeight + 75);
                        }
                        // 調整狀態 Badge 位置至右下角
                        if (card.Controls["lblStatus"] is Label lblStatus)
                        {
                            lblStatus.Size = new Size(newCardWidth - 10, 18);
                            lblStatus.Location = new Point(5, newCoverHeight + 97);
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

        private int filterRequestId = 0;
        private async void FilterAndRefreshGrid()
        {
            int currentRequestId = ++filterRequestId;
            if (pnlLoading != null)
            {
                pnlLoading.BackColor = Color.FromArgb(200, 18, 18, 18); // 切換時使用半透明遮罩
                pnlLoading.Visible = true;
                pnlLoading.BringToFront();
                await Task.Delay(20);
            }

            if (currentRequestId != filterRequestId) return;

            gamesFlowPanel.SuspendLayout();
            
            foreach (Control ctrl in gamesFlowPanel.Controls)
            {
                if (ctrl is Panel p)
                {
                    if (p.Controls["pbCover"] is PictureBox pb && pb.Image != null)
                    {
                        var img = pb.Image;
                        pb.Image = null;
                        img.Dispose();
                    }
                    foreach (Control child in p.Controls) child.Dispose();
                }
                ctrl.Dispose();
            }
            gamesFlowPanel.Controls.Clear();

            IEnumerable<GameItem> query = allGames;
            int sidebarIdx = categoryListBox.SelectedIndex;

            // 平台/收藏篩選（索引 0-3）
            if (sidebarIdx == 1) query = query.Where(g => g.IsFavorite);
            else if (sidebarIdx == 2) query = query.Where(g => g.Platform == "Steam");
            else if (sidebarIdx == 3) query = query.Where(g => g.Platform == "DLsite");
            // 遊玩狀態篩選（索引 5-8）
            else if (SidebarStatusMap.ContainsKey(sidebarIdx))
                query = query.Where(g => g.Status == SidebarStatusMap[sidebarIdx]);

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

            List<Control> newCards = new List<Control>();

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
                        try
                        {
                            using (var stream = new FileStream(game.CoverImagePath, FileMode.Open, FileAccess.Read))
                            using (var tempImg = Image.FromStream(stream))
                            {
                                pbCover.Image = new Bitmap(tempImg);
                            }
                        }
                        catch { }
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
                Label lblTime = new Label { Name = "lblTime", Text = $"時數: {game.TotalPlayTime} 分鐘", Size = new Size(130, 15), Location = new Point(10, 225), Font = new Font("Microsoft JhengHei", 8), ForeColor = Color.FromArgb(3, 218, 198) };

                // === 【新增】遊玩狀態 Badge ===
                Color badgeColor = StatusColors[game.Status];
                Label lblStatus = new Label
                {
                    Name = "lblStatus",
                    Text = StatusLabels[game.Status],
                    Size = new Size(200, 18),
                    Location = new Point(5, 247),
                    Font = new Font("Microsoft JhengHei", 7.5f, FontStyle.Bold),
                    ForeColor = badgeColor,
                    BackColor = Color.FromArgb(40, badgeColor.R, badgeColor.G, badgeColor.B),
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                Action selectAction = () =>
                {
                    selectedGame = game;
                    lblGameTitle.Text = game.Title;
                    lblPlatform.Text = $"代碼: {game.Code} | 平台: {game.Platform}";

                    string uiReleaseDate = (game.ReleaseDate == "1970-01-01") ? "未知" : game.ReleaseDate;
                    string displayTags = game.Tags.Count > 0 ? string.Join(", ", game.Tags) : "無標籤";
                    string displayStatus = StatusLabels[game.Status];

                    lblNotes.Text = $"社團/廠商：{game.Circle}\n發售日期：{uiReleaseDate}\n最後遊玩：{(game.LastPlayed.HasValue ? game.LastPlayed.Value.ToString("yyyy/MM/dd HH:mm") : "從未遊玩")}\n總遊玩時數：{game.TotalPlayTime} 分鐘\n\n遊玩狀態：{displayStatus}\n\n遊戲標籤：\n{displayTags}\n\n個人備忘：\n{game.Notes}";
                    btnLaunch.Visible = true;
                };

                card.Click += (s, e) => selectAction();
                pbCover.Click += (s, e) => selectAction();
                lblTitle.Click += (s, e) => selectAction();

                card.ContextMenuStrip = gameContextMenu;
                pbCover.ContextMenuStrip = gameContextMenu;
                card.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) selectedGame = game; };
                pbCover.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) selectedGame = game; };

                card.Controls.AddRange(new Control[] { pbCover, lblTitle, lblCirc, lblTime, lblStatus, lblCode });
                lblCode.BringToFront();

                newCards.Add(card);
            }

            gamesFlowPanel.Controls.AddRange(newCards.ToArray());
            CenterFlowPanelCards();
            gamesFlowPanel.ResumeLayout(true);

            if (currentRequestId == filterRequestId && pnlLoading != null) pnlLoading.Visible = false;
        }

        /// <summary>
        /// 透過右鍵選單更改選取遊戲的遊玩狀態，並儲存、刷新介面
        /// </summary>
        private async void SetSelectedGameStatus(PlayStatus newStatus)
        {
            if (selectedGame == null) return;
            selectedGame.Status = newStatus;
            await SaveGamesHelperAsync();
            FilterAndRefreshGrid();

            // 若右側詳情面板正在顯示此遊戲，即時更新狀態文字
            if (lblGameTitle.Text == selectedGame.Title)
            {
                string displayStatus = StatusLabels[newStatus];
                string uiReleaseDate = (selectedGame.ReleaseDate == "1970-01-01") ? "未知" : selectedGame.ReleaseDate;
                string displayTags = selectedGame.Tags.Count > 0 ? string.Join(", ", selectedGame.Tags) : "無標籤";
                lblNotes.Text = $"社團/廠商：{selectedGame.Circle}\n發售日期：{uiReleaseDate}\n最後遊玩：{(selectedGame.LastPlayed.HasValue ? selectedGame.LastPlayed.Value.ToString("yyyy/MM/dd HH:mm") : "從未遊玩")}\n總遊玩時數：{selectedGame.TotalPlayTime} 分鐘\n\n遊玩狀態：{displayStatus}\n\n遊戲標籤：\n{displayTags}\n\n個人備忘：\n{selectedGame.Notes}";
            }
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

        private async void BtnLaunch_Click(object? sender, EventArgs e)
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
                        this.BeginInvoke(new Action(async () =>
                        {
                            int minutes = (int)(DateTime.Now - startTime).TotalMinutes;
                            selectedGame.TotalPlayTime += Math.Max(1, minutes);
                            await SaveGamesHelperAsync();
                            MessageBox.Show($"更新「{selectedGame.Title}」時數為 {selectedGame.TotalPlayTime} 分鐘。", "時數更新", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            FilterAndRefreshGrid();
                        }));
                    };
                }
                await SaveGamesHelperAsync();
                FilterAndRefreshGrid();
            }
            catch (Exception ex) { MessageBox.Show($"遊戲啟動失敗: {ex.Message}"); }
        }

        private async void BtnAddGame_Click(object? sender, EventArgs e)
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
                    await SaveGamesHelperAsync();
                    await LoadGameDataAsync();
                }
            }
        }

        private async void BtnBatchImport_Click(object? sender, EventArgs e)
        {
            using var batchForm = new BatchImportForm(allGames);
            if (batchForm.ShowDialog(this) == DialogResult.OK && batchForm.ImportedGames.Count > 0)
            {
                int added = 0;
                foreach (var game in batchForm.ImportedGames)
                {
                    if (!allGames.Any(g => g.Code.Equals(game.Code, StringComparison.OrdinalIgnoreCase)))
                    {
                        allGames.Add(game);
                        added++;
                    }
                }
                if (added > 0)
                {
                    await SaveGamesHelperAsync();
                    await LoadGameDataAsync();
                    MessageBox.Show($"🎮 成功批量匯入 {added} 款遊戲！", "匯入完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void InitializeLoadingPanel()
        {
            pnlLoading = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(255, 18, 18, 18), // 初始載入時使用全不透明背景，遮住尚未成型的 UI
                Visible = true
            };
            var lblLoading = new Label
            {
                Text = "Loading...",
                Font = new Font("Segoe UI", 24, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            pnlLoading.Controls.Add(lblLoading);
            this.Controls.Add(pnlLoading);
            pnlLoading.BringToFront();
        }
    }
}