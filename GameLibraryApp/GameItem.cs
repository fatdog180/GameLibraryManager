using System;
using System.Collections.Generic;

namespace GameLibraryApp
{
    public class GameItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Code { get; set; } = string.Empty;           // 識別碼 (如 RJ01177942 或 2127010)
        public string Title { get; set; } = string.Empty;          // 遊戲名稱
        public string Circle { get; set; } = "未知廠商";            // 社團 / 開發商
        public string Platform { get; set; } = "Steam";            // 平台種類
        public string ExePath { get; set; } = string.Empty;         // 遊戲執行路徑
        public string CoverImagePath { get; set; } = string.Empty;    // 封面圖片本地路徑
        public bool IsFavorite { get; set; } = false;              // 是否收藏
        public string Notes { get; set; } = string.Empty;             // 備忘錄

        public List<string> Tags { get; set; } = new List<string>();
        public string ReleaseDate { get; set; } = "1970-01-01";    // 發售日期
        public string LastUpdated { get; set; } = "無";             // 最後更新日
        public DateTime? LastPlayed { get; set; }                  // 最後遊玩時間
        public int TotalPlayTime { get; set; } = 0;                // 總遊玩時間 (分鐘)
    }
}