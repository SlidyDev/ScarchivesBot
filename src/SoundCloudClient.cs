using Newtonsoft.Json;
using ScarchivesBot.Entities;
using System.Web;

using static ScarchivesBot.Config;

namespace ScarchivesBot;

public class SoundCloudClient
{
    public readonly string ClientId;
    private List<Download> _downloads = new();
    private HttpClient _httpClient = new(new HttpClientHandler()
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 4
    });

    public SoundCloudClient(string clientId)
    {
        this.ClientId = clientId;
    }

    public async Task<T> ResolveEntity<T>(string url) where T : Entity
    {
        HttpResponseMessage uriResponse;
        try
        {
            uriResponse = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        }
        catch
        {
            return null;
        }

        if (!uriResponse.IsSuccessStatusCode)
            return null;

        var uri = uriResponse.RequestMessage.RequestUri;
        if (uri == null)
            return null;

        uri = new(uri.GetLeftPart(UriPartial.Path));

        Console.WriteLine($"Resolving: {uri}");
        var args = HttpUtility.ParseQueryString(string.Empty);
        args.Add("client_id", ClientId);
        args.Add("url", uri.ToString());

        HttpResponseMessage result;
        try
        {
            result = await _httpClient.GetAsync(SCApiUrl + "resolve?" + args.ToString());
        }
        catch
        {
            return null;
        }

        if (!result.IsSuccessStatusCode)
            return null;

        var entity = JsonConvert.DeserializeObject<T>(await result.Content.ReadAsStringAsync());
        if (entity.EntityKind != entity.ExpectedEntityKind)
            return null;

        return entity;
    }

    public async Task<TracksPage> GetTracksPage(User user)
    {
        var args = HttpUtility.ParseQueryString(string.Empty);
        args.Add("client_id", ClientId);
        args.Add("offset", "0");
        args.Add("limit", "20"); // gotta be 20 otherwise sc will act gay
        args.Add("linked_partitioning", "1");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync($"{SCApiUrl}users/{user.ID}/tracks?{args}");
        }
        catch
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
            return null;

        return JsonConvert.DeserializeObject<TracksPage>(await response.Content.ReadAsStringAsync());
    }

    public async Task<User> GetUser(long id)
    {
        var args = HttpUtility.ParseQueryString(string.Empty);
        args.Add("client_id", ClientId);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync($"{SCApiUrl}users/{id}?{args}");
        }
        catch
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
            return null;

        return JsonConvert.DeserializeObject<User>(await response.Content.ReadAsStringAsync());
    }

    public async Task<Download> DownloadTrack(Track track)
    {
        Download download;
        lock (_downloads)
        {
            var downloadIdx = _downloads.FindIndex(x => x.TrackID == track.ID);

            if (downloadIdx == -1)
            {
                download = new(track.ID);
                _downloads.Add(download);
            }
            else
            {
                download = _downloads[downloadIdx];
                return download;
            }
        }

        var fileResponse = await GetFileInfo(track);
        if (fileResponse == null)
            return download;

        var unparsedFileSize = fileResponse.Content.Headers.FirstOrDefault(h => h.Key.Equals("Content-Length")).Value?.FirstOrDefault();
        if (unparsedFileSize == null || !long.TryParse(unparsedFileSize, out var fileSize))
        {
            Console.WriteLine($"Failed to parse the file size for '{track}'");
            return download;
        }

        download.FileSize = fileSize;

        if (fileSize > FileSizeLimit)
        {
            Console.WriteLine($"The file size of '{track}' ({fileSize} bytes) is too big (max {FileSizeLimit} bytes)");
            return download;
        }

        download.Content = fileResponse.Content;

        return download;
    }

    private async Task<HttpResponseMessage> GetFileInfo(Track track)
    {
        var transcoding = track.Audio.Transcodings.FirstOrDefault(x => x.AudioFormat.Protocol == "progressive" && x.AudioFormat.MimeType == "audio/mpeg");
        if (transcoding == null)
        {
            Console.WriteLine($"Failed to get transcoding for '{track}'");
            return null;
        }

        var args = HttpUtility.ParseQueryString(string.Empty);
        args.Add("client_id", ClientId);

        HttpResponseMessage downloadLinkResult;
        try
        {
            downloadLinkResult = await _httpClient.GetAsync($"{transcoding.URL}?{args}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to request a download link for '{track}'");
            Console.WriteLine(ex);
            return null;
        }

        if (!downloadLinkResult.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to request a download link for '{track}'");
            return null;
        }

        var downloadLinkPage = JsonConvert.DeserializeObject<DownloadLink>(await downloadLinkResult.Content.ReadAsStringAsync());
        var downloadLink = downloadLinkPage.URL;

        HttpResponseMessage fileResult;
        try
        {
            fileResult = await _httpClient.GetAsync(downloadLink, HttpCompletionOption.ResponseHeadersRead);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to download the file of '{track}'");
            Console.WriteLine(ex);
            return null;
        }

        if (!fileResult.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to request a download link for '{track}'");
            return null;
        }

        return fileResult;
    }

    public class Download
    {
        public HttpContent Content { get; set; }

        public long TrackID { get; private set; }

        public long FileSize { get; set; }

        public bool Failed => FileSize <= 0 || FileSize > FileSizeLimit || Content == null;

        public Download(long trackID)
        {
            TrackID = trackID;
        }
    }
}
