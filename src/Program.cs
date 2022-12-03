using Discord;
using Discord.Webhook;
using Newtonsoft.Json;
using ScarchivesBot;
using ScarchivesBot.Entities;
using System.Text;
using System.Web;

const long UpdateDelay = 10000;
const long FileSizeLimit = 8000000;
const string SettingsPath = "settings.json";
const string TempPath = "Temp";
const string ClientID = "YeTcsotswIIc4sse5WZsXszVxMtP6eLc";

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

async Task PostTrack(string url)
{
    var args = HttpUtility.ParseQueryString(string.Empty);
    args.Add("client_id", ClientID);
    args.Add("url", url);

    var trackInfoResult = await scClient.GetAsync("resolve?" + args.ToString());
    if (!trackInfoResult.IsSuccessStatusCode)
    {
        Console.WriteLine($"Failed to resolve url `{url}`");
        return;
    }

    var track = JsonConvert.DeserializeObject<Track>(await trackInfoResult.Content.ReadAsStringAsync());


    // Get the file

    var transcoding = track.Audio.Transcodings.FirstOrDefault(x => x.AudioFormat.Protocol == "progressive" && x.AudioFormat.MimeType == "audio/mpeg");
    if (transcoding == null)
    {
        Console.WriteLine($"Failed to get transcoding for '{url}'");
        return;
    }

    args = HttpUtility.ParseQueryString(string.Empty);
    args.Add("client_id", ClientID);

    var downloadLinkResult = await scClient.GetAsync($"{transcoding.URL}?{args}");
    if (!downloadLinkResult.IsSuccessStatusCode)
    {
        Console.WriteLine($"Failed to request a download link for `{url}`");
        return;
    }

    var downloadLinkPage = JsonConvert.DeserializeObject<DownloadLink>(await downloadLinkResult.Content.ReadAsStringAsync());
    var downloadLink = downloadLinkPage.URL;

    var filePath = Path.Combine(TempPath, $"{track.ID}.mp3");
    using (var httpClient = new HttpClient())
    {
        var fileResult = await httpClient.GetAsync(downloadLink);
        if (!fileResult.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to request a download link for `{url}`");
            return;
        }

        var unparsedFileSize = fileResult.Content.Headers.FirstOrDefault(h => h.Key.Equals("Content-Length")).Value.FirstOrDefault();
        if (unparsedFileSize == null || !long.TryParse(unparsedFileSize, out var fileSize))
        {
            Console.WriteLine($"Failed to parse the file size for `{url}`");
            return;
        }

        if (fileSize > FileSizeLimit)
        {
            Console.WriteLine($"The file size of `{url}` ({fileSize} bytes) is too big (max {FileSizeLimit} bytes)");
            return;
        }

        Console.WriteLine($"Downloading {fileSize} bytes for `{url}`");

        Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        using var fs = File.Create(filePath);
        await fileResult.Content.CopyToAsync(fs);
    }


    // Get metadata ready

    var bigCoverURL = track.CoverURL.Replace("large.jpg", "t500x500.jpg");

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

    await webhook.SendFileAsync(filePath, "", username: settings.Username, avatarUrl: settings.PfpLink, embeds: new Embed[] { embedBuilder.Build() });
    File.Delete(filePath);
}

for (; ; )
{

    await Task.Delay(10000);
}