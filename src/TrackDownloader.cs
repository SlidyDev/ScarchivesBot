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
                download = new(track.ID, path, DownloadTask(track, path));
                _ = WaitAndDisposeDownload(download);
                _downloads.Add(download);
            }
            else
            {
                download = _downloads[downloadIdx];
            }
        }

        return download;
    }

    private async Task WaitAndDisposeDownload(Download download)
    {
        await download.DownloadTask;
        lock (_downloads)
        {
            _downloads.Remove(download);
        }
    }

    private async Task DownloadTask(Track track, string downloadPath)
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
            downloadLinkResult = await _httpClient.GetAsync($"{SCApiUrl}{transcoding.URL}?{args}");
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
            fileResult = await _httpClient.GetAsync(downloadLink, HttpCompletionOption.ResponseHeadersRead);
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
    }

    public class Download
    {
        public string DownloadPath { get; private set; }

        public long TrackID { get; private set; }

        public Task DownloadTask { get; set; }

        public Download(long trackID, string downloadPath, Task downloadTask)
        {
            TrackID = trackID;
            DownloadPath = downloadPath;
            DownloadTask = downloadTask;
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