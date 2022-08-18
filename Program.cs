using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace TerraHistoricus
{
    class Program
    {
        static void Main(string[] args)
        {
            TerraHistoricus TH = new TerraHistoricus();
            TH.SaveData();
        }
    }
    class TerraHistoricus
    {
        private static readonly HttpClient client = new HttpClient();
        public class ComicsCid
        {
            public List<ComicsCid> data { get; set; }
            public string cid { get; set; }
        }
        public class ComicInfo
        {
            public ComicInfo data { get; set; }
            public string cid { get; set; }
            public string cover { get; set; }
            public string title { get; set; } = "None";
            public string subtitle { get; set; } = "None";
            public List<string> authors { get; set; }
            public List<string> keywords { get; set; }
            public string introduction { get; set; }
            public string direction { get; set; }
            public List<Episode> episodes { get; set; }
            public long updateTime { get; set; }
        }
        public class Episode
        {
            public string cid { get; set; }
            public string shortTitle { get; set; }
            public string title { get; set; }
            public int displayTime { get; set; }
        }
        public class ComicPageInfos
        {
            public ComicPageInfos data { get; set; }
            public List<ComicPageInfos> pageInfos { get; set; }
        }
        public class ComicData
        {
            public ComicData data { get; set; }
            public string url { get; set; }
        }
        static TerraHistoricus()
        {
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.5112.81 Safari/537.36 Edg/104.0.1293.54");
            client.DefaultRequestHeaders.Add("Referer", "https://terra-historicus.hypergryph.com/");
        }

        private IEnumerable<string> GetComicsCid()
        {
            ComicsCid comicsCid = client.GetFromJsonAsync<ComicsCid>("https://terra-historicus.hypergryph.com/api/comic").Result;
            foreach (var item in comicsCid.data)
            {
                yield return item.cid;
            }
        }

        private IEnumerable<ComicInfo> GetComicInfo()
        {
            foreach (var cid in GetComicsCid())
            {
                ComicInfo comicInfo = client.GetFromJsonAsync<ComicInfo>($"https://terra-historicus.hypergryph.com/api/comic/{cid}").Result;
                yield return comicInfo.data;
            }
        }

        private int GetComicPages(string parentCid, string cid)
        {
            ComicPageInfos comicPageInfos = client.GetFromJsonAsync<ComicPageInfos>($"https://terra-historicus.hypergryph.com/api/comic/{parentCid}/episode/{cid}").Result;
            return comicPageInfos.data.pageInfos.Count;
        }

        private IEnumerable<string> GetComicData(string parentCid, string cid, int pageNums)
        {
            for (int i = 1; i < pageNums; i++)
            {
                ComicData comicData = client.GetFromJsonAsync<ComicData>($"https://terra-historicus.hypergryph.com/api/comic/{parentCid}/episode/{cid}/page?pageNum={i}").Result;
                yield return comicData.data.url;
            }
        }

        public void SaveData()
        {
            string path_detection = "[\\\\/:*?<>\"|]";
            void UrlDownload(string url, string name, string path)
            {
                Byte[] content = client.GetByteArrayAsync(url).Result;
                File.WriteAllBytes($"{path}\\{name}", content);
            }

            string firstPath = "terra-historicus";
            Directory.CreateDirectory(firstPath);
            foreach (ComicInfo comicInfo in GetComicInfo())
            {
                string secondPath = $"{firstPath}\\{Regex.Replace(comicInfo.title, path_detection, "!")}";
                Directory.CreateDirectory(secondPath);

                if (!File.Exists(secondPath + "\\封面." + Regex.Match(comicInfo.cover, ".*\\.(.*)").Groups[1].Value))
                {
                    UrlDownload(
                        comicInfo.cover,
                        "封面." + Regex.Match(comicInfo.cover, ".*\\.(.*)").Groups[1].Value,
                        secondPath
                    );
                }

                long oldTime = 0;
                if (File.Exists(secondPath + "\\info.txt"))
                {
                    using (StreamReader file = File.OpenText(secondPath + "\\info.txt"))
                    {
                        string[] text = file.ReadToEnd().Split('：');
                        oldTime = new DateTimeOffset(DateTime.ParseExact(text[text.Length - 1], "yyyy-MM-dd HH:mm:ss",
                            System.Globalization.CultureInfo.InvariantCulture)).ToUnixTimeSeconds();
                        long upgradeTime = comicInfo.episodes[0].displayTime;
                        if (oldTime >= upgradeTime)
                        {
                            Console.WriteLine($"{comicInfo.title}已是最新");
                            continue;
                        }
                    }
                }
                Console.WriteLine($"正在下载：{comicInfo.title}");

                using (StreamWriter file = File.CreateText(secondPath + "\\info.txt"))
                {
                    file.WriteLine($"作品标题：{comicInfo.title}");
                    file.WriteLine($"作品副标题：{comicInfo.subtitle}");
                    file.WriteLine($"作者：{string.Join('、', comicInfo.authors)}");
                    file.WriteLine($"作品介绍：{comicInfo.introduction.Replace("\n", "\n                ")}");
                    file.WriteLine($"作品标签：{string.Join('、', comicInfo.keywords)}");
                    file.WriteLine($"阅读方向：{comicInfo.direction}");
                    file.WriteLine($"发布时间：{DateTimeOffset.FromUnixTimeSeconds(comicInfo.updateTime).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss")}");
                    file.Write($"更新时间：{DateTimeOffset.FromUnixTimeSeconds(comicInfo.episodes[0].displayTime).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss")}");
                }

                int i = 1;
                comicInfo.episodes.Reverse();
                foreach (Episode episode in comicInfo.episodes)
                {
                    long upgradeTime = episode.displayTime;
                    if (oldTime >= upgradeTime)
                    {
                        i += 1;
                        continue;
                    }
                    string thirdPath = $"{secondPath}\\{i}-{Regex.Replace(episode.shortTitle, path_detection, "!")}" +
                        $" {Regex.Replace(episode.title, path_detection, "!")}";
                    Directory.CreateDirectory(thirdPath);
                    int pageNums = GetComicPages(comicInfo.cid, episode.cid);
                    i += 1;
                    int p = 1;
                    foreach (string url in GetComicData(comicInfo.cid, episode.cid, pageNums))
                    {
                        if (!File.Exists($"{thirdPath}\\P{p}.{Regex.Match(url, ".*\\.(.*)").Groups[1].Value}"))
                        {
                            UrlDownload(url, $"P{p}.{Regex.Match(url, ".*\\.(.*)").Groups[1].Value}", thirdPath);
                        }
                        p += 1;
                    }
                }
            }
        }
    }
}

