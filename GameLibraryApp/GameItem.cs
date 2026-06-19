using System;

namespace GameLibraryApp
{
    public class GameItem
    {
        // 使用 Guid 確保每款遊戲都有獨一無二的 ID，方便未來做刪除或修改
        public Guid Id { get; set; } = Guid.NewGuid();

        // 遊戲名稱
        public string Title { get; set; } = string.Empty;

        // 遊戲平台 (例如: Steam, DLsite, Epic, Independent)
        public string Platform { get; set; } = "Steam";

        // 遊戲執行檔 (.exe) 的絕對路徑
        public string ExePath { get; set; } = string.Empty;

        // 封面圖片路徑 (可以是本地圖片路徑)
        public string CoverImagePath { get; set; } = string.Empty;

        // 是否加入收藏
        public bool IsFavorite { get; set; } = false;

        // 備忘錄（例如：DLsite 的券號、通關心得、或者是備份密碼）
        public string Notes { get; set; } = string.Empty;

        // 記錄最後一次遊玩的時間
        public DateTime? LastPlayed { get; set; }
    }
}