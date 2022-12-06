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
        var track = await SC.ResolveEntity<Track>(url);
        if (track == null)
        {
            await RespondAsync("The provided track URL is invalid.", ephemeral: true);
            return;
        }

        var download = await SC.DownloadTrack(track);
        if (download.Failed)
        {
            if (download.FileSize > Config.FileSizeLimit)
            {
                await RespondAsync($"The file size of the track is too big. [{download.FileSize}/{Config.FileSizeLimit} bytes]", ephemeral: true);
                return;
            }

            await RespondAsync($"Failed to download the track.", ephemeral: true);
            return;
        }

        await RespondAsync($"Downloading **{track.Title}** *({download.FileSize} bytes)*...");

        await download.DownloadTask;

        try
        {
            var embed = Program.GenerateEmbedForTrack(track);
            await FollowupWithFileAsync(download.DownloadPath, embed: embed);
        }
        catch
        {
            await FollowupAsync("Failed to upload the track.");
            return;
        }
    }
}