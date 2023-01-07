using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using ScarchivesBot.DataManagement;
using ScarchivesBot.Entities;
using System.Text;

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
    public async Task List()
    {
        await RespondAsync("Gathering all user info...", ephemeral: true);
        var msg = await GetOriginalResponseAsync();
        if (msg.Channel is not SocketGuildChannel channel)
        {
            await ModifyOriginalResponseAsync(x => x.Content = "Something is wrong bruh.");
            return;
        }

        await VerifyChannelPerms(msg);

        var settings = await channel.GetSettings();
        if (settings == null)
        {
            await ModifyOriginalResponseAsync(x => x.Content = "Failed to load the channel data.");
            return;
        }

        var allCreatorsBuilder = new StringBuilder();
        foreach (var creatorId in settings.Creators)
        {
            var creator = await SC.GetUser(creatorId);
            if (creator == null)
                continue;

            allCreatorsBuilder.Append('[');
            allCreatorsBuilder.Append(creator.Username);
            allCreatorsBuilder.Append("](");
            allCreatorsBuilder.Append(creator.URL);
            allCreatorsBuilder.AppendLine(")");
        }

        if (allCreatorsBuilder.Length == 0)
        {
            await ModifyOriginalResponseAsync(x => x.Content = "This channel's watchlist is empty.");
            return;
        }

        var embedBuilder = new EmbedBuilder
        {
            Title = "Creator Watchlist",
            Description = allCreatorsBuilder.ToString(),
            Color = new(255, 85, 0)
        };

        await ModifyOriginalResponseAsync(x =>
        {
            x.Content = "";
            x.Embed = embedBuilder.Build();
        });
    }

    [SlashCommand("add", "Adds a SoundCloud creator to this channel's watchlist.")]
    [RequireUserPermission(ChannelPermission.ManageWebhooks)]
    public async Task Add(string url)
    {
        await RespondAsync("Getting user info...");
        var msg = await GetOriginalResponseAsync();
        if (msg.Channel is not SocketTextChannel channel)
        {
            await ModifyOriginalResponseAsync(x => x.Content = "Wrong channel type.");
            return;
        }

        await VerifyChannelPerms(msg);

        var user = await SC.ResolveEntity<User>(url);
        if (user == null)
        {
            await ModifyOriginalResponseAsync(x => x.Content = "The provided user profile URL is invalid.");
            return;
        }

        await ModifyOriginalResponseAsync(x => x.Content = $"Adding **{user.Username}** to the watchlist.");

        var settings = await channel.GetSettings();
        if (settings == null)
        {
            await ModifyOriginalResponseAsync(x => x.Content = "Failed to load the channel data.");
            return;
        }

        var creatorToWatch = await Settings.Load<CreatorToWatch>(Path.Combine(Config.WatchlistPath, $"{user.ID}.json"));
        if (creatorToWatch == null)
        {
            await ModifyOriginalResponseAsync(x => x.Content = "Failed to load the user data.");
            return;
        }

        if (settings.Creators.Contains(user.ID))
        {
            await ModifyOriginalResponseAsync(x => x.Content = "The user is already in the watchlist.");
            return;
        }

        settings.Creators.Add(user.ID);
        creatorToWatch.ChannelsWatching.Add(new(channel));

        await settings.Save();
        await creatorToWatch.Save();

        await ModifyOriginalResponseAsync(x => x.Content = $"**{user.Username}** has been added to this channel's watchlist.");

        await Program.UpdateStatus();
    }

    [SlashCommand("remove", "Removes a SoundCloud creator from this channel's watchlist.")]
    [RequireUserPermission(ChannelPermission.ManageWebhooks)]
    public async Task Remove(string url)
    {
        await RespondAsync("Getting user info...");
        var msg = await GetOriginalResponseAsync();
        if (msg.Channel is not SocketTextChannel channel)
        {
            await ModifyOriginalResponseAsync(x => x.Content = "Wrong channel type.");
            return;
        }

        await VerifyChannelPerms(msg);

        var user = await SC.ResolveEntity<User>(url);
        if (user == null)
        {
            await ModifyOriginalResponseAsync(x => x.Content = "The provided user profile URL is invalid.");
            return;
        }

        await ModifyOriginalResponseAsync(x => x.Content = $"Removing **{user.Username}** from the watchlist.");

        var settings = await channel.GetSettings();
        if (settings == null)
        {
            await ModifyOriginalResponseAsync(x => x.Content = "Failed to load the channel data.");
            return;
        }

        var creatorToWatch = await Settings.Load<CreatorToWatch>(Path.Combine(Config.WatchlistPath, $"{user.ID}.json"));
        if (creatorToWatch == null)
        {
            await ModifyOriginalResponseAsync(x => x.Content = "Failed to load the user data.");
            return;
        }

        if (!settings.Creators.Contains(user.ID))
        {
            await ModifyOriginalResponseAsync(x => x.Content = "The user is not even on the watchlist.");
            return;
        }

        settings.Creators.Remove(user.ID);
        creatorToWatch.ChannelsWatching.Remove(new(channel));

        await settings.Save();
        await creatorToWatch.Save();

        await ModifyOriginalResponseAsync(x => x.Content = $"**{user.Username}** has been removed from this channel's watchlist.");

        await Program.UpdateStatus();
    }

    private static async Task VerifyChannelPerms(IUserMessage msg)
    {
        if (msg.Channel is not SocketTextChannel channel)
            return;

        await channel.AddPermissionOverwriteAsync(msg.Author, new(sendMessages: PermValue.Allow, attachFiles: PermValue.Allow, viewChannel: PermValue.Allow));
    }
}