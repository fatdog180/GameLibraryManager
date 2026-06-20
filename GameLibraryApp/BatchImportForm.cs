using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GameLibraryApp
{
    /// <summary>
    /// 批量匯入視窗，提供「資料夾掃描」和「代碼清單」兩種匯入模式
    /// </summary>
    public class BatchImportForm : Form
    {
        // ── 執行檔黑名單（含這些關鍵字的 .exe 視為非主程式，予以排除）──
        private static readonly string[] ExcludedExeKeywords =
        {
            "setup", "install", "uninstall",
            "unity", "unitycrashandler", "ue4", "ue5", "unreal",
            "dxsetup", "vcredist", "dotnet", "directx", "dxwebsetup", "redist",
            "crash", "crashreport", "crashhandler", "bugsplat",
            "config", "configurator", "configuration",
            "update", "updater", "patch", "patcher",
            "prerequisite", "helper", "tool",
            "launcher_old", "launch_old"
        };

        private readonly List<GameItem> existingGames;
        public List<GameItem> ImportedGames { get; } = new List<GameItem>();

        // ── Tab 1: 資料夾掃描 ──
        private TextBox txtFolderPath = null!;
        private Button btnBrowseFolder = null!;
        private Button btnScan = null!;
        private Label lblScanHint = null!;
        private DataGridView dgvScan = null!;
        private Button btnFetchScan = null!;
        private ProgressBar pbScan = null!;
        private Label lblProgressScan = null!;
        private RichTextBox rtbLogScan = null!;

        // ── Tab 2: 代碼清單 ──
        private TextBox txtCodeInput = null!;
        private Button btnParseCode = null!;
        private DataGridView dgvCodes = null!;
        private Button btnFetchCodes = null!;
        private ProgressBar pbCodes = null!;
        private Label lblProgressCodes = null!;
        private RichTextBox rtbLogCodes = null!;

        // ── 底部按鈕 ──
        private Button btnImport = null!;

        // ── 平行資料（fetchedList[i] 對應 dgv 第 i 行抓取到的 GameItem）──
        private readonly List<GameItem?> scanFetched = new();
        private readonly List<GameItem?> codeFetched = new();
        private bool isFetching = false;

        public BatchImportForm(List<GameItem> existing)
        {
            existingGames = existing;
            Build();
        }

        // ═════════════════════════════════════════════════════
        //  UI 建置
        // ═════════════════════════════════════════════════════

        private void Build()
        {
            Text = "📦 批量匯入遊戲";
            Size = new Size(980, 730);
            MinimumSize = new Size(820, 580);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(18, 25, 38);
            ForeColor = Color.White;
            Font = new Font("Microsoft JhengHei", 9.5f);

            // ── 底部按鈕列 ─────────────────────────────────
            var pnlBottom = new Panel
            {
                Height = 58,
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(22, 30, 46),
                Padding = new Padding(12, 9, 12, 9)
            };
            btnImport = new Button
            {
                Text = "✅  確認匯入成功抓取的遊戲",
                Location = new Point(12, 9),
                Size = new Size(295, 38),
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnImport.FlatAppearance.BorderSize = 0;
            btnImport.Click += BtnImport_Click;

            var btnClose = new Button
            {
                Text = "關閉",
                Size = new Size(100, 38),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.FromArgb(75, 88, 105),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei", 10),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => Close();
            // 動態定位關閉按鈕至右側
            pnlBottom.SizeChanged += (s, e) => btnClose.Location = new Point(pnlBottom.Width - 112, 9);
            pnlBottom.Controls.AddRange(new Control[] { btnImport, btnClose });

            // ── TabControl ─────────────────────────────────
            var tc = new TabControl
            {
                Dock = DockStyle.Fill,
                DrawMode = TabDrawMode.OwnerDrawFixed,
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(210, 38),
                Font = new Font("Microsoft JhengHei", 10, FontStyle.Bold)
            };
            tc.DrawItem += (s, e) =>
            {
                bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                e.Graphics.FillRectangle(
                    new SolidBrush(sel ? Color.FromArgb(42, 113, 212) : Color.FromArgb(28, 40, 60)),
                    e.Bounds);
                using var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                e.Graphics.DrawString(tc.TabPages[e.Index].Text, tc.Font, Brushes.White, e.Bounds, sf);
            };

            var tab1 = new TabPage("📂  資料夾掃描") { BackColor = Color.FromArgb(18, 25, 38), ForeColor = Color.White };
            var tab2 = new TabPage("📋  代碼清單") { BackColor = Color.FromArgb(18, 25, 38), ForeColor = Color.White };
            BuildTab1(tab1);
            BuildTab2(tab2);
            tc.TabPages.AddRange(new[] { tab1, tab2 });

            // 注意加入順序與 Z-order：
            // WinForms 中，Z-order 最小（位於最上層、Index=0）的控件會「最後」進行 Dock 計算。
            // 因此 DockStyle.Fill 的控件必須透過 BringToFront() 確保其位於最上層，才不會提前佔用空間。
            Controls.Add(pnlBottom);
            Controls.Add(tc);
            tc.BringToFront(); // 確保 TabControl 最後計算 Dock，不會被 pnlBottom 遮擋底部
        }

        // ── Tab 1: 資料夾掃描 ────────────────────────────────

        private void BuildTab1(TabPage tab)
        {
            // [Top] 資料夾路徑列
            var pnlFolder = new Panel
            {
                Height = 44,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(22, 32, 50),
                Padding = new Padding(10, 8, 10, 8)
            };
            var lblFolderLabel = new Label
            {
                Text = "遊戲根目錄：",
                Dock = DockStyle.Left,
                Width = 95,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(200, 210, 225)
            };
            txtFolderPath = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(23, 33, 52),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                Font = new Font("Consolas", 9.5f)
            };
            btnScan = new Button
            {
                Text = "🔍 掃描",
                Dock = DockStyle.Right,
                Width = 80,
                BackColor = Color.FromArgb(255, 64, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnScan.FlatAppearance.BorderSize = 0;
            btnScan.Click += BtnScan_Click;
            btnBrowseFolder = new Button
            {
                Text = "📁 瀏覽",
                Dock = DockStyle.Right,
                Width = 82,
                BackColor = Color.FromArgb(42, 113, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnBrowseFolder.FlatAppearance.BorderSize = 0;
            btnBrowseFolder.Click += (s, e) =>
            {
                using var fbd = new FolderBrowserDialog
                {
                    Description = "選擇遊戲根目錄（掃描第一層子資料夾）",
                    UseDescriptionForTitle = true
                };
                if (fbd.ShowDialog() == DialogResult.OK)
                    txtFolderPath.Text = fbd.SelectedPath;
            };
            pnlFolder.Controls.Add(txtFolderPath);    // Fill
            pnlFolder.Controls.Add(btnBrowseFolder);  // Right
            pnlFolder.Controls.Add(btnScan);          // Right
            pnlFolder.Controls.Add(lblFolderLabel);   // Left
            txtFolderPath.BringToFront();             // 確保 Fill 最後計算

            // [Top] 提示標籤（雙行，含 ID 填入指引）
            lblScanHint = new Label
            {
                Text = "提示：掃描第一層子資料夾，自動識別 RJ/Steam 代碼。",
                Dock = DockStyle.Top,
                Height = 22,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                ForeColor = Color.FromArgb(120, 155, 185),
                Font = new Font("Microsoft JhengHei", 8.5f)
            };
            // 編輯指引標籤（醒目色，獨立一行）
            var lblEditHint = new Label
            {
                Text = "✏  ⚠ 未識別代碼 的列：請直接雙擊「遊戲代碼」欄位輸入對應代碼，再點「批量抓取」。",
                Dock = DockStyle.Top,
                Height = 22,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                ForeColor = Color.FromArgb(255, 175, 50),
                Font = new Font("Microsoft JhengHei", 8.5f, FontStyle.Bold)
            };

            // [Fill] DataGridView
            dgvScan = CreateDgv(hasExePath: true);
            dgvScan.Dock = DockStyle.Fill;
            // 當使用者雙擊「遊戲代碼」欄位開始編輯時，顯示工具提示指引
            var toolTipScan = new ToolTip { InitialDelay = 0, AutoPopDelay = 4000, ReshowDelay = 0 };
            dgvScan.CellBeginEdit += (s, e) =>
            {
                if (dgvScan.Columns["colCode"] is { } colCodeCol && e.ColumnIndex == colCodeCol.Index)
                    toolTipScan.Show(
                        "雙擊輸入遊戲代碼，例如：RJ123456  或  2254470（Steam AppID）",
                        dgvScan, dgvScan.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true).Location,
                        3500);
            };
            // ── 當使用者編輯「遊戲代碼」欄位完成時，自動辨識並切換平台 ──
            dgvScan.CellEndEdit += (s, e) => {
                toolTipScan.Hide(dgvScan);
                if (dgvScan.Columns["colCode"] is { } col && e.ColumnIndex == col.Index) {
                    var cell = dgvScan.Rows[e.RowIndex].Cells[e.ColumnIndex];
                    string val = cell.Value?.ToString() ?? "";
                    if (!string.IsNullOrWhiteSpace(val)) {
                        var parsed = ParseCodeAndPlatform(val);
                        cell.Value = parsed.code;
                        dgvScan.Rows[e.RowIndex].Cells["colPlatform"].Value = parsed.platform;
                    }
                }
            };

            // ── 雙擊無執行檔的列，開啟手動選擇視窗 ──
            dgvScan.CellDoubleClick += (s, e) => {
                if (e.RowIndex < 0) return;
                var row = dgvScan.Rows[e.RowIndex];
                if (row.Cells["colStatus"].Value?.ToString()?.Contains("無執行檔") == true) {
                    string folderName = row.Cells["colExe"].Value?.ToString() ?? "";
                    string rootPath = txtFolderPath.Text.Trim();
                    string fullPath = Path.Combine(rootPath, folderName);
                    if (Directory.Exists(fullPath)) {
                        using var ofd = new OpenFileDialog {
                            InitialDirectory = fullPath,
                            Filter = "執行檔 (*.exe)|*.exe|所有檔案 (*.*)|*.*",
                            Title = $"手動選擇 {folderName} 的主程式"
                        };
                        if (ofd.ShowDialog() == DialogResult.OK) {
                            row.Tag = ofd.FileName;
                            row.Cells["colExe"].Value = Path.GetFileName(ofd.FileName);
                            row.Cells["colStatus"].Value = "待抓取";
                            row.Cells["colCheck"].ReadOnly = false;
                            row.Cells["colCheck"].Value = true; // 自動勾選
                            row.DefaultCellStyle.ForeColor = Color.White;
                            row.DefaultCellStyle.BackColor = e.RowIndex % 2 == 0 ? Color.FromArgb(20, 30, 48) : Color.FromArgb(25, 37, 58);
                        }
                    }
                }
            };

            // ── 底部固定區：將「抓取進度列」和「日誌」包入同一個 Panel ──
            // 原因：多個 DockStyle.Bottom 控件疊加在 TabPage 時，
            // 會干擾 DockStyle.Fill 控件（DGV）的可捲動高度計算。
            // 合併成單一 pnlBottom1 後，DGV 的 Fill 區域才能正確計算。
            var pnlBottom1 = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 128,   // 38px 抓取列 + 90px 日誌
                BackColor = Color.FromArgb(20, 28, 44)
            };
            var pnlFetch1 = BuildFetchRow(out btnFetchScan, out pbScan, out lblProgressScan);
            pnlFetch1.Dock = DockStyle.Top;   // 在 pnlBottom1 內部靠頂
            pnlFetch1.Height = 38;
            btnFetchScan.Enabled = false;
            btnFetchScan.Click += async (s, e) =>
                await StartFetchAsync(dgvScan, scanFetched, pbScan, lblProgressScan, rtbLogScan);

            rtbLogScan = CreateLogBox();
            rtbLogScan.Dock = DockStyle.Fill;  // 填滿 pnlBottom1 的剩餘空間

            // pnlBottom1 內部加入順序
            pnlBottom1.Controls.Add(rtbLogScan);   // Fill
            pnlBottom1.Controls.Add(pnlFetch1);    // Top
            rtbLogScan.BringToFront();             // 確保 Fill 最後計算

            // ── 使用 TableLayoutPanel 確保排版不會重疊 ──
            var tlp1 = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            tlp1.RowStyles.Add(new RowStyle(SizeType.Absolute, 88f));  // pnlFolder(44) + lblScanHint(22) + lblEditHint(22)
            tlp1.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // DGV 佔滿剩餘空間
            tlp1.RowStyles.Add(new RowStyle(SizeType.Absolute, 128f)); // pnlBottom1

            var pnlTop1 = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            pnlTop1.Controls.Add(lblEditHint);    // Top
            pnlTop1.Controls.Add(lblScanHint);    // Top
            pnlTop1.Controls.Add(pnlFolder);      // Top
            pnlFolder.BringToFront();
            lblScanHint.BringToFront();
            lblEditHint.BringToFront();

            tlp1.Controls.Add(pnlTop1, 0, 0);
            tlp1.Controls.Add(dgvScan, 0, 1);
            tlp1.Controls.Add(pnlBottom1, 0, 2);

            tab.Controls.Add(tlp1);
        }

        // ── Tab 2: 代碼清單 ──────────────────────────────────

        private void BuildTab2(TabPage tab)
        {
            // [Top] 說明標籤
            var lblHint = new Label
            {
                Text = "貼入遊戲代碼（每行一個，支援 RJ/BJ/VJ 及 Steam AppID 混合格式）：",
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                ForeColor = Color.White,
                Font = new Font("Microsoft JhengHei", 9.5f)
            };

            // [Top] 代碼輸入區
            var pnlCodeArea = new Panel
            {
                Height = 152,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(22, 32, 50),
                Padding = new Padding(10, 8, 10, 6)
            };
            txtCodeInput = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 28, 44),
                ForeColor = Color.FromArgb(200, 230, 255),
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 10.5f),
                PlaceholderText = "RJ123456\nRJ789012\n2254470\n1234567"
            };
            btnParseCode = new Button
            {
                Text = "📝  解析代碼清單",
                Dock = DockStyle.Bottom,
                Height = 32,
                BackColor = Color.FromArgb(42, 113, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnParseCode.FlatAppearance.BorderSize = 0;
            btnParseCode.Click += BtnParseCode_Click;
            pnlCodeArea.Controls.Add(txtCodeInput); // Fill
            pnlCodeArea.Controls.Add(btnParseCode); // Bottom
            txtCodeInput.BringToFront();            // 確保 Fill 最後計算

            // [Fill] DataGridView
            dgvCodes = CreateDgv(hasExePath: false);
            dgvCodes.Dock = DockStyle.Fill;

            // ── 底部固定區：同 Tab1，合併成單一 pnlBottom2 避免多 Bottom 干擾 ──
            var pnlBottom2 = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 128,
                BackColor = Color.FromArgb(20, 28, 44)
            };
            var pnlFetch2 = BuildFetchRow(out btnFetchCodes, out pbCodes, out lblProgressCodes);
            pnlFetch2.Dock = DockStyle.Top;
            pnlFetch2.Height = 38;
            btnFetchCodes.Enabled = false;
            btnFetchCodes.Click += async (s, e) =>
                await StartFetchAsync(dgvCodes, codeFetched, pbCodes, lblProgressCodes, rtbLogCodes);

            rtbLogCodes = CreateLogBox();
            rtbLogCodes.Dock = DockStyle.Fill;

            pnlBottom2.Controls.Add(rtbLogCodes);   // Fill
            pnlBottom2.Controls.Add(pnlFetch2);     // Top
            rtbLogCodes.BringToFront();             // 確保 Fill 最後計算

            // ── 使用 TableLayoutPanel 確保排版不會重疊 ──
            var tlp2 = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            tlp2.RowStyles.Add(new RowStyle(SizeType.Absolute, 180f)); // lblHint(28) + pnlCodeArea(152)
            tlp2.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            tlp2.RowStyles.Add(new RowStyle(SizeType.Absolute, 128f));

            var pnlTop2 = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            pnlTop2.Controls.Add(pnlCodeArea);
            pnlTop2.Controls.Add(lblHint);
            lblHint.BringToFront();
            pnlCodeArea.BringToFront();

            tlp2.Controls.Add(pnlTop2, 0, 0);
            tlp2.Controls.Add(dgvCodes, 0, 1);
            tlp2.Controls.Add(pnlBottom2, 0, 2);

            tab.Controls.Add(tlp2);
        }

        // ── 共用控件工廠 ─────────────────────────────────────

        private DataGridView CreateDgv(bool hasExePath)
        {
            var dgv = new DataGridView
            {
                BackgroundColor = Color.FromArgb(20, 30, 48),
                GridColor = Color.FromArgb(35, 50, 72),
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 32,
                RowTemplate = { Height = 28 }
            };
            dgv.EnableHeadersVisualStyles = false;
            // 避免 ComboBox 欄位在值不符時拋出 DataError
            dgv.DataError += (s, e) => { e.ThrowException = false; };

            dgv.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(15, 22, 38),
                ForeColor = Color.FromArgb(170, 195, 220),
                Font = new Font("Microsoft JhengHei", 9, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleLeft
            };
            dgv.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(20, 30, 48),
                ForeColor = Color.White,
                SelectionBackColor = Color.FromArgb(42, 88, 150),
                SelectionForeColor = Color.White
            };

            // 欄位定義
            var colCheck = new DataGridViewCheckBoxColumn
            {
                Name = "colCheck", HeaderText = "☑", FillWeight = 5,
                TrueValue = true, FalseValue = false, IndeterminateValue = false
            };
            var colCode = new DataGridViewTextBoxColumn
            {
                Name = "colCode", HeaderText = "遊戲代碼", FillWeight = 18
            };
            var colPlat = new DataGridViewComboBoxColumn
            {
                Name = "colPlatform", HeaderText = "平台", FillWeight = 10,
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
            };
            colPlat.Items.AddRange("DLsite", "Steam");
            var colTitle = new DataGridViewTextBoxColumn
            {
                Name = "colTitle", HeaderText = "遊戲名稱（抓取後顯示）", FillWeight = 37, ReadOnly = true
            };
            var colStatus = new DataGridViewTextBoxColumn
            {
                Name = "colStatus", HeaderText = "狀態", FillWeight = 18, ReadOnly = true
            };
            dgv.Columns.AddRange(colCheck, colCode, colPlat, colTitle, colStatus);

            if (hasExePath)
            {
                var colExe = new DataGridViewTextBoxColumn
                {
                    Name = "colExe", HeaderText = "執行檔（偵測）", FillWeight = 22, ReadOnly = true
                };
                dgv.Columns.Add(colExe);
                colTitle.FillWeight = 25;
            }
            return dgv;
        }

        private Panel BuildFetchRow(out Button fetchBtn, out ProgressBar pb, out Label lblProg)
        {
            var pnl = new Panel
            {
                Height = 38,
                BackColor = Color.FromArgb(20, 28, 44),
                Padding = new Padding(10, 5, 10, 5)
            };
            fetchBtn = new Button
            {
                Text = "▶  批量抓取選取項目",
                Location = new Point(10, 5),
                Size = new Size(188, 28),
                BackColor = Color.FromArgb(210, 110, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            fetchBtn.FlatAppearance.BorderSize = 0;

            pb = new ProgressBar
            {
                Location = new Point(206, 9),
                Size = new Size(400, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Style = ProgressBarStyle.Continuous
            };
            lblProg = new Label
            {
                Location = new Point(616, 9),
                Size = new Size(140, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.FromArgb(160, 195, 220),
                Font = new Font("Microsoft JhengHei", 8.5f)
            };
            // C# 不允許在 lambda 中捕獲 out 參數，使用區域變數轉接
            var pbLocal = pb;
            var lblProgLocal = lblProg;
            pnl.SizeChanged += (s, e) =>
            {
                pbLocal.Width = Math.Max(10, pnl.Width - 210 - 155);
                lblProgLocal.Location = new Point(pnl.Width - 148, 9);
            };
            pnl.Controls.AddRange(new Control[] { fetchBtn, pb, lblProg });
            return pnl;
        }

        private RichTextBox CreateLogBox() => new RichTextBox
        {
            BackColor = Color.FromArgb(12, 18, 28),
            ForeColor = Color.FromArgb(170, 195, 210),
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 8.5f),
            ScrollBars = RichTextBoxScrollBars.Vertical
        };

        // ═════════════════════════════════════════════════════
        //  事件處理
        // ═════════════════════════════════════════════════════

        private async void BtnScan_Click(object? sender, EventArgs e)
        {
            string path = txtFolderPath.Text.Trim();
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                MessageBox.Show("請先選擇一個有效的資料夾路徑！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnScan.Enabled = false;
            btnBrowseFolder.Enabled = false;
            try
            {
                dgvScan.Rows.Clear();
                scanFetched.Clear();
                rtbLogScan.Clear();

                string[] subDirs = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly);
                int found = 0;
                int skipped = 0;
                var skippedNames = new List<string>();

                foreach (string dir in subDirs)
                {
                    string folderName = Path.GetFileName(dir);
                    string? bestExe = FindBestExe(dir);

                    var parsedCode = ParseCodeAndPlatform(folderName);
                    string code = parsedCode.success ? parsedCode.code : "識別失敗";
                    string platform = parsedCode.platform;

                    if (!parsedCode.success)
                    {
                        lblProgressScan.Text = $"正在透過 Steam 搜尋: {folderName}...";
                        string? steamId = await SearchSteamIdAsync(folderName);
                        if (!string.IsNullOrEmpty(steamId))
                        {
                            code = steamId;
                            platform = "Steam";
                        }
                        lblProgressScan.Text = "";
                    }

                    if (bestExe == null)
                    {
                        int skipIdx = dgvScan.Rows.Add(false, code, platform, "", "❌ 無執行檔 (雙擊列選擇)", folderName);
                        dgvScan.Rows[skipIdx].Cells["colCheck"].ReadOnly = true;
                        dgvScan.Rows[skipIdx].DefaultCellStyle.ForeColor = Color.FromArgb(80, 80, 80);
                        dgvScan.Rows[skipIdx].DefaultCellStyle.BackColor = Color.FromArgb(18, 22, 32);
                        dgvScan.Rows[skipIdx].Tag = null;
                        scanFetched.Add(null);
                        skippedNames.Add(folderName);
                        skipped++;
                        continue;
                    }

                    bool dup = !string.IsNullOrEmpty(code) && code != "識別失敗" &&
                               existingGames.Any(g => g.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

                    string status = dup ? "⏭ 已在庫中"
                                  : string.IsNullOrEmpty(code) || code == "識別失敗" ? "⚠ 未識別代碼"
                                  : "待抓取";

                    int idx = dgvScan.Rows.Add(!dup, code, platform, "", status, Path.GetFileName(bestExe));
                    dgvScan.Rows[idx].Tag = bestExe;

                    if (dup) dgvScan.Rows[idx].DefaultCellStyle.ForeColor = Color.FromArgb(100, 100, 100);
                    else if (string.IsNullOrEmpty(code) || code == "識別失敗") dgvScan.Rows[idx].DefaultCellStyle.ForeColor = Color.FromArgb(255, 175, 50);

                    scanFetched.Add(null);
                    found++;
                }

                ApplyAltRows(dgvScan);
                lblScanHint.Text = $"掃描完成：找到 {found} 款可用 / 共 {subDirs.Length} 個子目錄（{skipped} 個無執行檔）";
                btnFetchScan.Enabled = found > 0;
                AppendLog(rtbLogScan, $"[掃描完成] ✅ {found} 款遊戲  ❌ {skipped} 個目錄無有效執行檔（顯示為灰色列）", Color.FromArgb(3, 218, 198));
                if (skipped > 0)
                {
                    AppendLog(rtbLogScan, $"─ 無執行檔的目錄：\n" + string.Join("\n", skippedNames.Select(n => $"  • {n}")), Color.FromArgb(120, 120, 120));
                }
            }
            finally
            {
                btnScan.Enabled = true;
                btnBrowseFolder.Enabled = true;
                lblProgressScan.Text = "";
            }
        }

        private void BtnParseCode_Click(object? sender, EventArgs e)
        {
            string raw = txtCodeInput.Text;
            if (string.IsNullOrWhiteSpace(raw))
            {
                MessageBox.Show("請先輸入遊戲代碼！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            dgvCodes.Rows.Clear();
            codeFetched.Clear();
            rtbLogCodes.Clear();
            int parsed = 0;

            foreach (string line in raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string s = line.Trim().ToUpper();
                if (string.IsNullOrEmpty(s)) continue;

                var parsedResult = ParseCodeAndPlatform(s);
                string code = parsedResult.code;
                string platform = parsedResult.platform;

                bool dup = !string.IsNullOrEmpty(code) && code != "識別失敗" &&
                           existingGames.Any(g => g.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
                int idx = dgvCodes.Rows.Add(!dup, code, platform, "", dup ? "⏭ 已在庫中" : "待抓取");
                if (dup) dgvCodes.Rows[idx].DefaultCellStyle.ForeColor = Color.FromArgb(100, 100, 100);
                codeFetched.Add(null);
                parsed++;
            }

            ApplyAltRows(dgvCodes);
            btnFetchCodes.Enabled = parsed > 0;
            AppendLog(rtbLogCodes, $"[解析完成] {parsed} 個代碼已載入，請確認後按「批量抓取」。", Color.FromArgb(3, 218, 198));
        }

        // ═════════════════════════════════════════════════════
        //  批量抓取核心邏輯（序列式，每筆間隔 600ms）
        // ═════════════════════════════════════════════════════

        private async Task StartFetchAsync(
            DataGridView dgv,
            List<GameItem?> fetchedList,
            ProgressBar pb,
            Label lblProg,
            RichTextBox log)
        {
            if (isFetching)
            {
                MessageBox.Show("目前正在抓取中，請稍候！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool hasScanExe = dgv.Columns.Contains("colExe");

            // 蒐集待抓取項目（已勾選、代碼非空、尚未成功的列）
            var queue = new List<(int rowIdx, string code, string platform, string? fullExePath)>();
            for (int i = 0; i < dgv.Rows.Count; i++)
            {
                if (dgv.Rows[i].Cells["colCheck"].Value is not true) continue;

                string code = dgv.Rows[i].Cells["colCode"].Value?.ToString()?.Trim() ?? "";
                string platform = dgv.Rows[i].Cells["colPlatform"].Value?.ToString() ?? "DLsite";
                string status = dgv.Rows[i].Cells["colStatus"].Value?.ToString() ?? "";

                if (string.IsNullOrWhiteSpace(code))
                {
                    // 代碼未填：略過並標記（使用者可在格子裡填入後重新點抓取）
                    SetRowStatus(dgv, i, "⏭ 代碼未填，已略過", Color.FromArgb(120, 120, 120));
                    continue;
                }
                if (status is "✅ 成功" or "⏭ 已在庫中") continue;

                string? fullExe = hasScanExe ? (dgv.Rows[i].Tag as string) : null;
                queue.Add((i, code, platform, fullExe));
            }

            if (queue.Count == 0)
            {
                AppendLog(log, "沒有可抓取的項目（請勾選有代碼且狀態非「成功/在庫中」的列）。", Color.FromArgb(255, 140, 0));
                return;
            }

            isFetching = true;
            SetFetchUIEnabled(false);
            pb.Minimum = 0;
            pb.Maximum = queue.Count;
            pb.Value = 0;
            int ok = 0, fail = 0;

            AppendLog(log, $"━━ 開始批量抓取，共 {queue.Count} 款遊戲 ━━", Color.FromArgb(255, 215, 0));

            for (int i = 0; i < queue.Count; i++)
            {
                var (rowIdx, code, platform, fullExe) = queue[i];
                lblProg.Text = $"{i + 1} / {queue.Count}";
                AppendLog(log, $"[{i + 1}/{queue.Count}] {code} ({platform})...", Color.FromArgb(180, 205, 255));
                SetRowStatus(dgv, rowIdx, "⏳ 抓取中...", Color.FromArgb(255, 215, 0));

                var game = await MetadataFetcher.FetchAsync(code, platform);

                if (game != null)
                {
                    // 資料夾掃描模式：將偵測到的 exe 路徑寫入 ExePath
                    if (!string.IsNullOrEmpty(fullExe))
                        game.ExePath = fullExe;

                    bool dup = existingGames.Any(g => g.Code.Equals(game.Code, StringComparison.OrdinalIgnoreCase));
                    if (dup)
                    {
                        fetchedList[rowIdx] = null;
                        dgv.Rows[rowIdx].Cells["colTitle"].Value = game.Title;
                        SetRowStatus(dgv, rowIdx, "⏭ 已在庫中", Color.FromArgb(100, 100, 100));
                        AppendLog(log, $"  ⏭ {code} — {game.Title}（已在庫中）", Color.FromArgb(100, 100, 100));
                    }
                    else
                    {
                        fetchedList[rowIdx] = game;
                        dgv.Rows[rowIdx].Cells["colTitle"].Value = game.Title;
                        SetRowStatus(dgv, rowIdx, "✅ 成功", Color.FromArgb(76, 210, 90));
                        AppendLog(log, $"  ✅ {code} — {game.Title}", Color.FromArgb(76, 210, 90));
                        ok++;
                    }
                }
                else
                {
                    fetchedList[rowIdx] = null;
                    SetRowStatus(dgv, rowIdx, "⚠ 抓取失敗", Color.FromArgb(255, 82, 82));
                    AppendLog(log, $"  ⚠ {code} — 抓取失敗（請確認代碼正確）", Color.FromArgb(255, 82, 82));
                    fail++;
                }

                pb.Value = i + 1;

                // 序列式節流：每筆間隔 600ms，避免觸發 DLsite IP 封鎖
                if (i < queue.Count - 1)
                    await Task.Delay(600);
            }

            lblProg.Text = $"✅ {ok}  ⚠ {fail}";
            AppendLog(log, $"\n━━ 抓取完成！✅ 成功 {ok} 款，⚠ 失敗 {fail} 款 ━━", Color.FromArgb(255, 215, 0));
            isFetching = false;
            SetFetchUIEnabled(true);
        }

        private void BtnImport_Click(object? sender, EventArgs e)
        {
            ImportedGames.Clear();
            CollectFrom(dgvScan, scanFetched);
            CollectFrom(dgvCodes, codeFetched);

            if (ImportedGames.Count == 0)
            {
                MessageBox.Show(
                    "目前沒有可匯入的遊戲。\n請先執行批量抓取，並確認有「✅ 成功」的勾選項目。",
                    "無可匯入遊戲", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        // ═════════════════════════════════════════════════════
        //  輔助方法
        // ═════════════════════════════════════════════════════

        /// <summary>
        /// 在指定目錄中找到最合適的主程式 exe（排除安裝/工具類，取最短名稱）
        /// </summary>
        private string? FindBestExe(string dir)
        {
            try
            {
                return Directory
                    .GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly)
                    .Where(f => !ExcludedExeKeywords.Any(kw =>
                        Path.GetFileNameWithoutExtension(f).ToLowerInvariant().Contains(kw)))
                    .OrderBy(f => Path.GetFileName(f).Length)
                    .FirstOrDefault();
            }
            catch { return null; }
        }

        private void CollectFrom(DataGridView dgv, List<GameItem?> list)
        {
            for (int i = 0; i < dgv.Rows.Count && i < list.Count; i++)
            {
                bool isChecked = dgv.Rows[i].Cells["colCheck"].Value is true;
                string status = dgv.Rows[i].Cells["colStatus"].Value?.ToString() ?? "";
                if (isChecked && status == "✅ 成功" && list[i] != null)
                    ImportedGames.Add(list[i]!);
            }
        }

        private void SetRowStatus(DataGridView dgv, int row, string status, Color color)
        {
            if (row < dgv.Rows.Count)
            {
                dgv.Rows[row].Cells["colStatus"].Value = status;
                dgv.Rows[row].DefaultCellStyle.ForeColor = color;
            }
        }

        private void SetFetchUIEnabled(bool enabled)
        {
            btnFetchScan.Enabled = enabled && dgvScan.Rows.Count > 0;
            btnFetchCodes.Enabled = enabled && dgvCodes.Rows.Count > 0;
            btnScan.Enabled = enabled;
            btnParseCode.Enabled = enabled;
        }

        private void ApplyAltRows(DataGridView dgv)
        {
            for (int i = 0; i < dgv.Rows.Count; i++)
            {
                Color fc = dgv.Rows[i].DefaultCellStyle.ForeColor;
                // 只對「未特別標色」的列套用交替背景
                if (fc == Color.White || fc == Color.Empty)
                {
                    dgv.Rows[i].DefaultCellStyle.BackColor = i % 2 == 0
                        ? Color.FromArgb(20, 30, 48)
                        : Color.FromArgb(25, 37, 58);
                }
            }
        }

        private void AppendLog(RichTextBox rtb, string msg, Color color)
        {
            if (rtb.InvokeRequired) { rtb.Invoke(() => AppendLog(rtb, msg, color)); return; }
            rtb.SelectionStart = rtb.TextLength;
            rtb.SelectionLength = 0;
            rtb.SelectionColor = color;
            rtb.AppendText(msg + "\n");
            rtb.SelectionColor = rtb.ForeColor;
            rtb.ScrollToCaret();
        }

        // ── 智慧解析：從字串（代碼或 URL）提取對應的 Code 和 平台 ──
        private (string code, string platform, bool success) ParseCodeAndPlatform(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return ("", "DLsite", false);

            // Steam URL (例如 https://store.steampowered.com/app/3557620/...)
            var stmUrl = Regex.Match(input, @"app/(\d+)");
            if (stmUrl.Success) return ("STEAM" + stmUrl.Groups[1].Value, "Steam", true);

            // DLsite 代碼
            var dlm = Regex.Match(input, @"(RJ|BJ|VJ)\d{6,}", RegexOptions.IgnoreCase);
            if (dlm.Success) return (dlm.Value.ToUpper(), "DLsite", true);

            // Steam ID 前綴任意位置 (例如 [STEAM2178330])
            var stmPrefix = Regex.Match(input, @"STEAM\s*(\d{5,10})", RegexOptions.IgnoreCase);
            if (stmPrefix.Success) return ("STEAM" + stmPrefix.Groups[1].Value, "Steam", true);

            // 僅 Steam ID (全數字，剛好 5-10 位)
            var stmExact = Regex.Match(input, @"^(\d{5,10})$", RegexOptions.IgnoreCase);
            if (stmExact.Success) return ("STEAM" + stmExact.Groups[1].Value, "Steam", true);

            // 預設為原本輸入 (識別失敗)
            return (input.Trim(), "DLsite", false);
        }

        private async Task<string?> SearchSteamIdAsync(string gameName)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                string url = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(gameName)}&l=english&cc=US";
                string json = await client.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("total", out var totalElem) && totalElem.GetInt32() > 0)
                {
                    var items = root.GetProperty("items");
                    if (items.GetArrayLength() > 0)
                    {
                        var firstItem = items[0];
                        if (firstItem.TryGetProperty("id", out var idElem))
                        {
                            return "STEAM" + idElem.GetInt32().ToString();
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
