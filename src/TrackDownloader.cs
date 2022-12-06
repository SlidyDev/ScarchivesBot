using System.Web;
using ScarchivesBot.Entities;
using Newtonsoft.Json;

using static ScarchivesBot.Config;

namespace ScarchivesBot;

internal class TrackDownloader
{
    private List<Download> _downloads = new();
    private HttpClient _httpClient;

    public string DownloadPath { get; private set; }

    public TrackDownloader(string downloadPath, HttpClient httpClient)
    {
        DownloadPath = downloadPath;
        _httpClient = httpClient;
    }

    public async Task<Download> DownloadTrack(Track track)
    {
        Download download;
        lock (_downloads)
        {
            var downloadIdx = _downloads.FindIndex(x => x.TrackID == track.ID);

            if (downloadIdx == -1)
            {
                var path = Path.Combine(DownloadPath, $"{track.ID}.mp3");
                download = new(track.ID, path);
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
            return null;
        
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

        download.DownloadTask = DownloadTask(fileResponse.Content, download);
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
        args.Add("client_id", ClientID);

        HttpResponseMessage downloadLinkResult;
        try
        {
            downloadLinkResult = await _httpClient.GetAsync($"{SCApiUrl}{transcoding.URL}?{args}");
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

    private async Task<bool> DownloadTask(HttpContent content, Download download)
    {

        Console.WriteLine($"Downloading {download.FileSize} bytes to '{download.DownloadPath}'");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(download.DownloadPath));
            using var fs = File.Create(download.DownloadPath);
            await content.CopyToAsync(fs);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to download file to '{download.DownloadPath}'");
            Console.WriteLine(ex);

            return false;
        }
        finally
        {
            lock (_downloads)
            {
                _downloads.Remove(download);
            }
        }

        return true;
    }

    public class Download
    {
        public string DownloadPath { get; private set; }

        public long TrackID { get; private set; }

        public Task DownloadTask { get; set; }

        public long FileSize { get; set; }

        public bool Failed => FileSize <= 0 || FileSize > FileSizeLimit || DownloadTask == null;

        public Download(long trackID, string downloadPath)
        {
            TrackID = trackID;
            DownloadPath = downloadPath;
        }

        ~Download()
        {
            if (!File.Exists(DownloadPath))
                return;

            try
            {
                File.Delete(DownloadPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete file '{DownloadPath}'");
                Console.WriteLine(ex);
                return;
            }

            Console.WriteLine($"Successfully deleted file '{DownloadPath}'");
        }
    }
}