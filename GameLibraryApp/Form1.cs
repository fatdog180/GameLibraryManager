using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GameLibraryApp;

public partial class Form1 : Form
{
    public Form1()
    {
        InitializeComponent();
        SetupCustomUI();  // 呼叫自訂的高質感介面佈局
        LoadGameData();   // 載入 JSON 檔案資料
    }

    // === UI 控制項宣告 ===
    private Panel sidebarPanel;
    private ListBox categoryListBox;
    private Button btnAddGame;

    private ListView gamesListView;

    private Panel detailsPanel;
    private Label lblGameTitle;
    private Label lblPlatform;
    private Label lblNotes;
    private Button btnLaunch;

    // === 資料變數 ===
    private List<GameItem> allGames = new List<GameItem>();

    /// <summary>
    /// 純程式碼建構 Steam 風格的 UI 介面
    /// </summary>
    private void SetupCustomUI()
    {
        // 1. 主表單設定
        this.Text = "PixelVault - 跨平台遊戲庫管理器";
        this.Size = new Size(1050, 650);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.FromArgb(27, 40, 56); // Steam 經典深藍底色
        this.ForeColor = Color.White;

        // 2. 左側側邊欄 Panel
        sidebarPanel = new Panel
        {
            Width = 220,
            Dock = DockStyle.Left,
            BackColor = Color.FromArgb(23, 26, 33) // 更深的黑灰色
        };

        // 分類選單
        categoryListBox = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(23, 26, 33),
            BorderStyle = BorderStyle.None,
            Font = new Font("Microsoft JhengHei", 12, FontStyle.Bold),
            ItemHeight = 45,
            DrawMode = DrawMode.OwnerDrawFixed // 啟用自訂繪製以調整行高與間距
        };
        categoryListBox.Items.AddRange(new object[] { " 🎮 所有遊戲", " ⭐ 我的收藏", " 🌐 Steam", " 🔞 DLsite" });
        categoryListBox.SelectedIndex = 0;

        // 美化 ListBox 的渲染效果
        categoryListBox.DrawItem += (s, e) =>
        {
            if (e.Index < 0) return;
            e.DrawBackground();
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Brush textBrush = isSelected ? Brushes.White : new SolidBrush(Color.FromArgb(141, 150, 157));
            if (isSelected)
            {
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(42, 113, 212)), e.Bounds); // 藍色高亮
            }
            e.Graphics.DrawString(categoryListBox.Items[e.Index].ToString(), e.Font, textBrush, e.Bounds.X + 5, e.Bounds.Y + 10);
        };
        sidebarPanel.Controls.Add(categoryListBox);

        // 新增遊戲按鈕
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
        detailsPanel = new Panel
        {
            Width = 320,
            Dock = DockStyle.Right,
            BackColor = Color.FromArgb(23, 26, 33),
            Padding = new Padding(20)
        };

        lblGameTitle = new Label
        {
            Text = "請選擇遊戲",
            Font = new Font("Microsoft JhengHei", 18, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Top,
            Height = 50
        };

        lblPlatform = new Label
        {
            Text = "平台: --",
            Font = new Font("Microsoft JhengHei", 11, FontStyle.Regular),
            ForeColor = Color.FromArgb(141, 150, 157),
            Dock = DockStyle.Top,
            Height = 30
        };

        lblNotes = new Label
        {
            Text = "備忘錄:\n尚未選擇任何遊戲。",
            Font = new Font("Microsoft JhengHei", 10, FontStyle.Regular),
            ForeColor = Color.DarkGray,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 20, 0, 0)
        };

        btnLaunch = new Button
        {
            Text = "▶ 啟動遊戲",
            Height = 55,
            Dock = DockStyle.Bottom,
            BackColor = Color.FromArgb(92, 184, 92), // 經典綠色啟動鈕
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft JhengHei", 14, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Visible = false // 預設隱藏，選取遊戲後再顯示
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

        // 將三大區塊依序加入表單中
        this.Controls.Add(gamesListView);
        this.Controls.Add(detailsPanel);
        this.Controls.Add(sidebarPanel);
    }

    /// <summary>
    /// 從先前寫好的 DataManager 載入資料
    /// </summary>
    private void LoadGameData()
    {
        allGames = GameDataManager.LoadGames();
        RefreshGameList();
    }

    /// <summary>
    /// 刷新畫面上的遊戲列表
    /// </summary>
    private void RefreshGameList()
    {
        gamesListView.Items.Clear();
        foreach (var game in allGames)
        {
            ListViewItem item = new ListViewItem(game.Title);
            item.SubItems.Add(game.Platform);
            item.Tag = game; // 重要：將整顆物件綁在 Tag，方便後續讀取
            gamesListView.Items.Add(item);
        }
    }
}
