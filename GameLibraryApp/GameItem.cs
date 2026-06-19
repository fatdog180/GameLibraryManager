using System;
using System.Collections.Generic;

namespace GameLibraryApp
{
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
    }
}