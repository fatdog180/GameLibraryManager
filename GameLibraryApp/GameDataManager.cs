using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GameLibraryApp
{
    public static class GameDataManager
    {
        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "games.json");
        private static readonly string TempFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "games.tmp.json");
        private static readonly string BackupFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "games.bak.json");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// 非同步安全儲存遊戲清單 (Atomic Write)
        /// 回傳 (是否成功, 錯誤訊息)
        /// </summary>
        public static async Task<(bool success, string errorMessage)> SaveGamesAsync(List<GameItem> games)
        {
            try
            {
                string jsonString = JsonSerializer.Serialize(games, JsonOptions);
                
                // 1. 寫入到暫存檔
                await File.WriteAllTextAsync(TempFilePath, jsonString);

                // 2. 原檔替換 (Atomic replacement)
                if (File.Exists(FilePath))
                {
                    File.Replace(TempFilePath, FilePath, BackupFilePath);
                }
                else
                {
                    File.Move(TempFilePath, FilePath);
                }

                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, $"儲存檔案時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        /// 非同步讀取遊戲清單，包含備份與防呆機制
        /// 回傳 (是否成功, 錯誤訊息, 遊戲清單)
        /// </summary>
        public static async Task<(bool success, string errorMessage, List<GameItem> games)> LoadGamesAsync()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return (true, "", new List<GameItem>());
                }

                string jsonString = await File.ReadAllTextAsync(FilePath);
                var loaded = JsonSerializer.Deserialize<List<GameItem>>(jsonString, JsonOptions);
                
                if (loaded == null)
                {
                    return (false, "遊戲資料庫解析結果為空，可能已損毀。", new List<GameItem>());
                }

                // 每次成功載入時也建立一份備份（避免使用者誤刪）
                File.Copy(FilePath, BackupFilePath, true);

                return (true, "", loaded);
            }
            catch (Exception ex)
            {
                return (false, $"讀取檔案時發生例外錯誤:\n{ex.Message}", new List<GameItem>());
            }
        }
    }
}