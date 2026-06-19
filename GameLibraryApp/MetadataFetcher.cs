using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GameLibraryApp
{
    public static class MetadataFetcher
    {
        private static readonly HttpClient client;

        static MetadataFetcher()
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = true };
            client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        public static async Task<GameItem?> FetchAsync(string code, string platform)
        {
            try
            {
                if (platform == "Steam")
                {
                    string appId = Regex.Replace(code, @"\D", "");
                    string url = $"https://store.steampowered.com/api/appdetails?appids={appId}&l=tchinese";

                    string json = await client.GetStringAsync(url);
                    using JsonDocument doc = JsonDocument.Parse(json);
                    JsonElement root = doc.RootElement;

                    if (root.TryGetProperty(appId, out JsonElement appMeta) && appMeta.GetProperty("success").GetBoolean())
                    {
                        JsonElement data = appMeta.GetProperty("data");
                        var item = new GameItem { Code = "STEAM" + appId, Platform = "Steam" };
                        item.Title = data.GetProperty("name").GetString() ?? "";

                        if (data.TryGetProperty("developers", out JsonElement devs) && devs.GetArrayLength() > 0)
                            item.Circle = devs[0].GetString() ?? "未知廠商";

                        if (data.TryGetProperty("header_image", out JsonElement img))
                            item.CoverImagePath = img.GetString() ?? "";

                        if (data.TryGetProperty("release_date", out JsonElement rd))
                            item.ReleaseDate = rd.GetProperty("date").GetString() ?? "1970-01-01";

                        // === 【補齊：對齊 Python 腳本，透過 API 獲取最新新聞時間作為最後更新日】 ===
                        try
                        {
                            string api_url = $"https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid={appId}&count=1";
                            string apiJson = await client.GetStringAsync(api_url);
                            using JsonDocument apiDoc = JsonDocument.Parse(apiJson);
                            if (apiDoc.RootElement.TryGetProperty("appnews", out JsonElement appnews) &&
                                appnews.TryGetProperty("newsitems", out JsonElement newsitems) &&
                                newsitems.GetArrayLength() > 0)
                            {
                                long timestamp = newsitems[0].GetProperty("date").GetInt64();
                                DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
                                item.LastUpdated = dateTime.ToString("yyyy-MM-dd");
                            }
                        }
                        catch { item.LastUpdated = "無"; }

                        return item;
                    }
                }
                else if (platform == "DLsite")
                {
                    string rjId = code.Trim().ToUpper();
                    string category = rjId.StartsWith("BJ") ? "books" : "maniax";
                    string url = $"https://www.dlsite.com/{category}/work/=/product_id/{rjId}.html?locale=zh_TW";

                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("Cookie", "adultchecked=1");

                    var response = await client.SendAsync(request);
                    if (!response.IsSuccessStatusCode) return null;

                    string html = await response.Content.ReadAsStringAsync();
                    var item = new GameItem { Code = rjId, Platform = "DLsite" };

                    var titleMatch = Regex.Match(html, @"<h1[^>]*id=""work_name""[^>]*>(.*?)</h1>", RegexOptions.IgnoreCase);
                    if (titleMatch.Success) item.Title = Regex.Replace(titleMatch.Groups[1].Value, "<.*?>", "").Trim();

                    var circleMatch = Regex.Match(html, @"<span[^>]*class=""maker_name""[^>]*>.*?<a[^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (circleMatch.Success) item.Circle = circleMatch.Groups[1].Value.Trim();

                    var imgMatch = Regex.Match(html, @"<meta[^>]*property=""og:image""[^>]*content=""(.*?)""", RegexOptions.IgnoreCase);
                    if (imgMatch.Success) item.CoverImagePath = imgMatch.Groups[1].Value.Trim();

                    var dateMatch = Regex.Match(html, @"<th>(?:販賣日|販売日)</th>.*?<td>.*?(\d{4}年\d{2}月\d{2}日)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (dateMatch.Success) item.ReleaseDate = dateMatch.Groups[1].Value.Trim();

                    // === 【補齊：網頁 Outline 表格的「更新情報」欄位正則匹配】 ===
                    var updateMatch = Regex.Match(html, @"<th>(?:更新情報|最終更新日|更新日期)</th>.*?<td>.*?(\d{4}年\d{2}月\d{2}日)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (updateMatch.Success)
                        item.LastUpdated = updateMatch.Groups[1].Value.Trim();
                    else
                        item.LastUpdated = "無";

                    return item;
                }
            }
            catch { }
            return null;
        }
    }
}