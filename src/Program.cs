using Discord;
using Discord.Webhook;
using Newtonsoft.Json;
using ScarchivesBot;
using ScarchivesBot.Entities;
using System.Text;
using System.Web;

const int UpdateDelay = 10000;
const long FileSizeLimit = 8000000;
const string SettingsPath = "settings.json";
const string TempPath = "Temp";
const string ClientID = "YeTcsotswIIc4sse5WZsXszVxMtP6eLc";
const string WatchlistURL = "https://raw.githubusercontent.com/SlidyDev/ScarchivesBot/main/data/Watchlist.txt";

if (!File.Exists(SettingsPath))
{
    File.Create(SettingsPath);
    Console.WriteLine("A settings.json file has been created. Please set the 'WebhookURL' property before proceeding.");
    return;
}

Settings settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(SettingsPath));
if (string.IsNullOrEmpty(settings.WebhookURL))
{
    Console.WriteLine("Please set the 'WebhookURL' property before proceeding.");
    return;
}

var webhook = new DiscordWebhookClient(settings.WebhookURL);

var scClient = new HttpClient
{
    BaseAddress = new Uri("https://api-v2.soundcloud.com/")
};

var httpClient = new HttpClient();

async Task PostTrack(Track track)
{
    // Get the file

    var transcoding = track.Audio.Transcodings.FirstOrDefault(x => x.AudioFormat.Protocol == "progressive" && x.AudioFormat.MimeType == "audio/mpeg");
    if (transcoding == null)
    {
        Console.WriteLine($"Failed to get transcoding for '{track}'");
        return;
    }

    var args = HttpUtility.ParseQueryString(string.Empty);
    args.Add("client_id", ClientID);

    HttpResponseMessage downloadLinkResult;
    try
    {
        downloadLinkResult = await scClient.GetAsync($"{transcoding.URL}?{args}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to request a download link for '{track}'");
        Console.WriteLine(ex);
        return;
    }

    if (!downloadLinkResult.IsSuccessStatusCode)
    {
        Console.WriteLine($"Failed to request a download link for '{track}'");
        return;
    }

    var downloadLinkPage = JsonConvert.DeserializeObject<DownloadLink>(await downloadLinkResult.Content.ReadAsStringAsync());
    var downloadLink = downloadLinkPage.URL;

    HttpResponseMessage fileResult;
    try
    {
        fileResult = await httpClient.GetAsync(downloadLink, HttpCompletionOption.ResponseHeadersRead);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to download the file of '{track}'");
        Console.WriteLine(ex);
        return;
    }

    if (!fileResult.IsSuccessStatusCode)
    {
        Console.WriteLine($"Failed to request a download link for '{track}'");
        return;
    }

    var unparsedFileSize = fileResult.Content.Headers.FirstOrDefault(h => h.Key.Equals("Content-Length")).Value?.FirstOrDefault();
    if (unparsedFileSize == null || !long.TryParse(unparsedFileSize, out var fileSize))
    {
        Console.WriteLine($"Failed to parse the file size for '{track}'");
        return;
    }

    if (fileSize > FileSizeLimit)
    {
        Console.WriteLine($"The file size of '{track}' ({fileSize} bytes) is too big (max {FileSizeLimit} bytes)");
        return;
    }

    Console.WriteLine($"Downloading {fileSize} bytes for '{track}'");

    var filePath = Path.Combine(TempPath, $"{track.ID}.mp3");
    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        using var fs = File.Create(filePath);
        await fileResult.Content.CopyToAsync(fs);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to download file for '{track}'");
        Console.WriteLine(ex);

        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
            }
            catch { }
        }

        return;
    }


    // Get metadata ready

    var bigCoverURL = track.CoverURL?.Replace("large.jpg", "t500x500.jpg");

    var tagsStringBuilder = new StringBuilder(track.TagList.Length);
    void AppendTag(ReadOnlySpan<char> tag)
    {
        tagsStringBuilder.Append("`#");
        tagsStringBuilder.Append(tag);
        tagsStringBuilder.Append("` ");
    }

    var ignoreSpaces = false;
    var startReadIdx = 0;
    for (var idx = 0; idx < track.TagList.Length; idx++)
    {
        var c = track.TagList[idx];

        if (c == '"')
        {
            if (ignoreSpaces)
            {
                AppendTag(track.TagList.AsSpan(startReadIdx, idx - startReadIdx));
            }

            ignoreSpaces = !ignoreSpaces;
            startReadIdx = idx + 1;
            continue;
        }

        if (!ignoreSpaces && c == ' ')
        {
            AppendTag(track.TagList.AsSpan(startReadIdx, idx - startReadIdx));
            startReadIdx = idx + 1;
            continue;
        }
    }

    var tags = tagsStringBuilder.ToString();

    var embedBuilder = new EmbedBuilder
    {
        Author = new()
        {
            Name = track.Author.Username,
            IconUrl = track.Author.AvatarURL,
            Url = track.Author.URL
        },
        Title = track.Title,
        Description = track.Description,
        ImageUrl = bigCoverURL,
        Fields = new()
        {
            new()
            {
                Name = "Tags",
                Value = tags
            }
        },
        Color = new(255, 85, 0)
    };

    try
    {
        await webhook.SendFileAsync(filePath, "", username: settings.Username, avatarUrl: settings.PfpLink, embeds: new Embed[] { embedBuilder.Build() });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to send '{track}' to Discord");
        Console.WriteLine(ex);
        return;
    }

    try
    {
        File.Delete(filePath);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to delete cache file '{filePath}'");
        Console.WriteLine(ex);
        return;
    }
}

async Task<User> GetUserFromUrl(string url)
{
    var args = HttpUtility.ParseQueryString(string.Empty);
    args.Add("client_id", ClientID);
    args.Add("url", url);

    HttpResponseMessage result;
    try
    {
        result = await scClient.GetAsync("resolve?" + args.ToString());
    }
    catch
    {
        return null;
    }

    if (!result.IsSuccessStatusCode)
        return null;

    var user = JsonConvert.DeserializeObject<User>(await result.Content.ReadAsStringAsync());
    if (user.EntityKind != "user")
        return null;

    return user;
}

async Task<List<string>> GetWatchlist()
{
    string watchlistResponse;
    try
    {
        watchlistResponse = await httpClient.GetStringAsync(WatchlistURL);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to get the watchlist from '{WatchlistURL}'");
        Console.WriteLine(ex.ToString());
        return null;
    }

    var result = new List<string>();
    using (var reader = new StringReader(watchlistResponse))
    {
        for (; ; )
        {
            var line = await reader.ReadLineAsync();
            if (line == null)
                break;

            line = line.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            result.Add(line);
        }
    }

    return result;
}

async Task<TracksPage> GetLatestTrack(User user)
{
    var args = HttpUtility.ParseQueryString(string.Empty);
    args.Add("client_id", ClientID);
    args.Add("offset", "0");
    args.Add("limit", "1");
    args.Add("linked_partitioning", "1");

    HttpResponseMessage response;
    try
    {
        response = await scClient.GetAsync($"users/{user.ID}/tracks?" + args.ToString());
    }
    catch
    {
        return null;
    }

    if (!response.IsSuccessStatusCode)
        return null;

    return JsonConvert.DeserializeObject<TracksPage>(await response.Content.ReadAsStringAsync());
}

var cachedUsers = new CachedUsers();

for (; ; )
{
    await Task.Delay(UpdateDelay);

    var watchlist = await GetWatchlist();
    if (watchlist == null)
        continue;

    foreach (var userURL in watchlist)
    {
        var user = await GetUserFromUrl(userURL);
        if (user == null)
            continue;

        var trackPage = await GetLatestTrack(user);
        if (trackPage == null)
            continue;

        var lastTrack = trackPage.Tracks.FirstOrDefault();


        var userCache = cachedUsers.GetUserCache(user, out var isNew);
        if (isNew)
        {
            if (lastTrack != null)
                userCache.LastUpload = lastTrack.CreatedAt;

            continue;
        }

        if (lastTrack == null)
            continue;

        var lastTrackCreationTime = lastTrack.CreatedAt;

        while (lastTrack.CreatedAt > userCache.LastUpload)
        {
            await PostTrack(lastTrack);

            trackPage = await trackPage.GetNextPage(ClientID);
            if (trackPage == null)
                break;

            lastTrack = trackPage.Tracks.FirstOrDefault();
            if (lastTrack == null)
                break;
        }

        userCache.LastUpload = lastTrackCreationTime;
    }
}