using Discord;
using Discord.Interactions;
using ScarchivesBot.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScarchivesBot;

public class Commands : InteractionModuleBase<SocketInteractionContext>
{
    public SoundCloudClient SC { get; set; }

    [SlashCommand("download", "Downloads a SoundCloud track from a URL", runMode: RunMode.Async)]
    public async Task Download(string url)
    {
        await RespondAsync($"Getting track info...");

        var track = await SC.ResolveEntity<Track>(url);
        if (track == null)
        {
            await ModifyOriginalResponseAsync(x => x.Content = "The provided track URL is invalid.");
            return;
        }

        var download = await SC.DownloadTrack(track);
        if (download.Failed)
        {
            if (download.FileSize > Config.FileSizeLimit)
            {
                await ModifyOriginalResponseAsync(x => x.Content = $"The file size of the track is too big. [{download.FileSize}/{Config.FileSizeLimit} bytes]");
                return;
            }

            await ModifyOriginalResponseAsync(x => x.Content = $"Failed to get the track stream.");
            return;
        }

        await ModifyOriginalResponseAsync(x => x.Content = $"Uploading **{track.Title}**...");

        try
        {
            var stream = await download.Content.ReadAsStreamAsync();
            var embed = Program.GenerateEmbedForTrack(track);
            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Content = "";
                msg.Embed = embed;
                msg.Attachments = new FileAttachment[]
                {
                    new(stream, $"{track.Title}.mp3")
                };
            });
        }
        catch
        {
            await ModifyOriginalResponseAsync(x => x.Content = $"Failed to upload the track.");
            return;
        }
    }
}