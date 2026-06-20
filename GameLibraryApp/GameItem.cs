using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GameLibraryApp
{
    /// <summary>
    /// 遊玩狀態：記錄玩家對此遊戲的進度狀態
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PlayStatus
    {
        Unplayed,   // 尚未遊玩（預設）
        Playing,    // 正在玩
        Completed,  // 已通關
        Wishlist    // 願望清單
    }

    public class GameItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Circle { get; set; } = "未知廠商";
        public string Platform { get; set; } = "Steam";
        public string ExePath { get; set; } = string.Empty;
        public string CoverImagePath { get; set; } = string.Empty;
        public bool IsFavorite { get; set; } = false;
        public string Notes { get; set; } = string.Empty;

        // 核心標籤系統
        public List<string> Tags { get; set; } = new List<string>();

        public string ReleaseDate { get; set; } = "1970-01-01";
        public DateTime? LastPlayed { get; set; }
        public int TotalPlayTime { get; set; } = 0;

        /// <summary>
        /// 遊玩狀態：已通關 / 正在玩 / 尚未遊玩 / 願望清單
        /// 舊 JSON 無此欄位時，預設為 Unplayed（向下相容）
        /// </summary>
        public PlayStatus Status { get; set; } = PlayStatus.Unplayed;
    }
}