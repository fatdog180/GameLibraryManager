# PixelVault 數位遊戲館藏儀表板

## 📌 專案簡介
**PixelVault** 是一款基於 Windows Forms (.NET) 架構開發的單機數位遊戲庫管理系統。本專案專為熱衷於收藏 Steam 與 DLsite 平台遊戲的玩家設計，旨在解決跨平台遊戲分散、中繼資料（Metadata）難以整合以及遊玩時數無法精確統計的痛點。

透過本系統，使用者能以高度自動化的方式建立精美的視覺化個人遊戲庫，享受流暢且極具現代感的暗黑風格（Dark Mode）互動介面。

---

## 🚀 核心功能特性

### 1. 智慧網路中繼資料抓取 (Metadata Fetcher)
* **自動對齊識別**：支援輸入 Steam AppID 或 DLsite 專屬代碼（RJ/BJ/VJ），系統會利用正規表達式（Regex）進行智慧平台判定與切換建議。
* **聯網即時撈取**：串接 Steam 官方 API 與網頁解析技術，一鍵自動抓取遊戲完整名稱、開發社團/廠商、發售日期、遊戲封面圖以及核心分類標籤（Genres/Tags）。

### 2. 雙模式高效批量匯入 (Batch Import)
* **資料夾自動掃描**：選定本地遊戲根目錄後，系統可自動掃描第一層子資料夾名稱並自動識別潛在代碼。
* **黑名單過濾機制**：具備執行檔黑名單過濾機制，能自動排除 `UnityCrashHandler`、`setup`、`patch` 等非主程式檔案。
* **手動指定主程式**：若遇到無執行檔的特殊目錄，支援雙擊該欄位手動指定主程式。
* **代碼清單文字解析**：支援複製一整串混合代碼（每行一個），系統能自動解析並進行序列式節流抓取（每筆間隔 600ms），有效防止因頻繁請求觸發伺服器 IP 封鎖。

### 3. 動態響應視覺化卡片庫 (Responsive UX)
* **動態網格縮放**：中央遊戲庫採用自定義算法，會隨著視窗拉伸自動計算安全寬度，動態調整卡片排版列數與封面圖尺寸，防止 UI 擠壓或字體切斷。
* **高級側欄高亮**：左側欄整合平台分類與四大遊玩狀態（🏆已通關、🎮正在玩、📦尚未遊玩、💖願望清單）。繪製事件（OwnerDraw）經深度客製化，選取時會顯示對應狀態的專屬高亮色彩（金色/綠色/灰色/粉紅）。
* **狀態 Badge 與詳情面板**：每張卡片下方皆附有半透明彩色狀態標籤。點擊卡片後，右側詳情面板將完整呈現廠商、發售日、標籤陣列與個人文字備忘錄。

### 4. 自動化遊戲啟動與精確時數追蹤
* **一鍵 RUN 啟動**：系統會自動切換至遊戲所在之工作目錄並喚起主程式。
* **背景異步監聽**：精準捕獲遊戲進程退出（Exited 事件）的時間點，自動計算本次遊玩分鐘數並累加至總時數中，隨後即時刷新儀表板。

### 5. 斷電級安全資料庫 (Atomic Write JSON)
* **原子級替換機制**：資料儲存採用先寫入暫存檔（`.tmp.json`），成功後再進行原子替換（Atomic Replacement）的技術防護。
* **安全備份回滾**：若發生異常將自動回滾至備份檔（`.bak.json`），確保館藏資料永不損毀。
* **向下相容性**：若讀取到不含 `Status` 欄位的舊版 JSON 檔案，系統會自動將其賦予預設值 `Unplayed`（尚未遊玩），完美相容開發迭代。

---

## 🛠 開發環境與架構說明
* **目標架構**：.NET 6.0 / 8.0 (Windows Forms)
* **程式碼結構**：
  * `Program.cs`：應用程式啟動入口。
  * `Form1.cs`：主儀表板（館藏瀏覽、動態縮放、右鍵狀態切換、詳情面板與即時過濾）。
  * `AddGameForm.cs`：單筆遊戲匯入表單（含聯網智慧抓取與平台切換引導）。
  * `BatchImportForm.cs`：批量導入控制台（含目錄掃描、進度條回饋、RichTextBox 日誌輸出）。
  * `GameItem.cs`：遊戲核心資料模型（含 `PlayStatus` 列舉與 `Guid` 唯一識別碼）。
  * `GameDataManager.cs`：負責本地 JSON 資料庫的非同步讀取、防呆與備份管理。
  * `MetadataFetcher.cs`：網路爬蟲與 API 核心模組，提供序列化非同步網路存取。

---

## 💻 執行與建置步驟說明

### 1. 複製儲存庫
請開啟終端機（Terminal）或 Git Bash，執行以下指令將專案複製至本地端：
```bash
git clone [https://github.com/fatdog180/GameLibraryManager.git](https://github.com/fatdog180/GameLibraryManager.git)
cd GameLibraryManager
```

### 2. 開啟並編譯專案
* **方法 A (推薦)**：使用 **Visual Studio 2022** 開啟專案資料夾下的 `.slnx` 方案檔。確認目標框架選擇正確後，點擊「開始」或按下 `F5` 進行編譯與偵錯執行。
* **方法 B (命令列)**：在專案根目錄下使用 .NET CLI 指令直接執行：
```bash
dotnet restore
dotnet run
```

---

## 🖼 系統畫面截圖 (System Screenshots)

### 📊 主儀表板與暗黑風格卡片庫
![Main Dashboard](https://github.com/user-attachments/assets/935fbf4a-0a8e-420c-9bd2-445798f67e1f)

### 🔍 單筆聯網抓取與智慧平台切換
![Add Game Form](https://github.com/user-attachments/assets/2f82e389-0fe9-4c31-85aa-28c391b4efc0)

### 📦 批量目錄掃描與進度日誌主機
![Batch Import Control](https://github.com/user-attachments/assets/2819c58d-9e48-4afe-81d0-9b2ee0b8f754)