using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameLibraryApp
{
    public static class GameDataManager
    {
        // === 【路徑修正】：改回執行檔旁邊，使其自動落入 bin/ 中受 .gitignore 保護 ===
        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "games.json");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            // 讓 PlayStatus enum 以字串形式（"Unplayed", "Playing" 等）儲存，方便直接閱讀 JSON
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// 儲存遊戲清單到 JSON 檔案中
        /// </summary>
        public static void SaveGames(List<GameItem> games)
        {
            try
            {
                string jsonString = JsonSerializer.Serialize(games, JsonOptions);
                File.WriteAllText(FilePath, jsonString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"儲存檔案時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        /// 從 JSON 檔案中讀取遊戲清單
        /// </summary>
        public static List<GameItem> LoadGames()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return new List<GameItem>();
                }

                string jsonString = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<List<GameItem>>(jsonString, JsonOptions) ?? new List<GameItem>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"讀取檔案時發生錯誤: {ex.Message}");
                return new List<GameItem>();
            }
        }
    }
}