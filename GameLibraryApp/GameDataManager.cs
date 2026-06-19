using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace GameLibraryApp
{
    public static class GameDataManager
    {
        // 定義 JSON 檔案要存放在哪裡（這裡設定在程式執行檔的同一個資料夾下，名為 games.json）
        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "games.json");

        // 為了讓 JSON 看起來漂亮（有縮排），設定寫入選項
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        /// <summary>
        /// 儲存遊戲清單到 JSON 檔案中
        /// </summary>
        public static void SaveGames(List<GameItem> games)
        {
            try
            {
                // 將物件轉換為 JSON 字串
                string jsonString = JsonSerializer.Serialize(games, JsonOptions);
                // 寫入檔案（如果檔案不存在會自動建立，存在則會覆蓋）
                File.WriteAllText(FilePath, jsonString);
            }
            catch (Exception ex)
            {
                // 如果發生錯誤，可以在這裡處理（例如彈出視窗，稍後接上 UI 後可以用 MessageBox 顯示）
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
                // 如果檔案不存在，代表是第一次開啟程式，直接回傳一個空清單
                if (!File.Exists(FilePath))
                {
                    return new List<GameItem>();
                }

                // 讀取檔案內的 JSON 字串
                string jsonString = File.ReadAllText(FilePath);

                // 將 JSON 字串還原成 C# 的 List<GameItem> 物件
                // 如果還原失敗，利用 ?? 語法確保回傳一個空清單，避免程式崩潰
                return JsonSerializer.Deserialize<List<GameItem>>(jsonString) ?? new List<GameItem>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"讀取檔案時發生錯誤: {ex.Message}");
                return new List<GameItem>();
            }
        }
    }
}