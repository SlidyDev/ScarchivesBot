﻿using Discord;
using Discord.Interactions;
using ScarchivesBot.Entities;

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
            using var stream = await download.Content.ReadAsStreamAsync();
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

    [SlashCommand("list", "Gets this channel's watchlist.", runMode: RunMode.Async)]
    [RequireUserPermission(ChannelPermission.ManageWebhooks)]
    public async Task List([Summary("Creator URL", "A SoundCloud profile URL.")] string url)
    {

    }

    [SlashCommand("add", "Adds a SoundCloud creator to this channel's watchlist.")]
    //[RequireUserPermission(ChannelPermission.ManageWebhooks)]
    public async Task Add([Summary("Creator URL", "A SoundCloud profile URL.")] string url)
    {

    }

    [SlashCommand("remove", "Removes a SoundCloud creator from this channel's watchlist.")]
    //[RequireUserPermission(ChannelPermission.ManageWebhooks)]
    public async Task Remove([Summary("Creator URL", "A SoundCloud profile URL.")] string url)
    {

    }
}