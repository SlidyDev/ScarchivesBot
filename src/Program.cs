using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using ScarchivesBot.DataManagement;
using ScarchivesBot.DiscordUtilities;
using ScarchivesBot.Entities;
using System.Text;
using static ScarchivesBot.Config;

namespace ScarchivesBot;

internal static class Program
{
    internal static DiscordSocketClient DiscordClient;

    private static async Task Main()
    {
        var settingsPath = Path.Combine(DataPath, "settings.json");
        var settings = await Settings.Load<BotSettings>(settingsPath);
        if (settings == null)
        {
            Console.WriteLine("Failed to load the bot settings.");
            return;
        }

        if (!File.Exists(settingsPath))
        {
            await settings.Save();

            Console.WriteLine($"A {settingsPath} file has been created. Please set the 'token' and 'soundCloudClientId' property before proceeding.");
            return;
        }

        if (string.IsNullOrEmpty(settings.Token))
        {
            Console.WriteLine("Please set the 'token' property before proceeding.");
            return;
        }

        if (string.IsNullOrEmpty(settings.SoundCloudClientId))
        {
            Console.WriteLine("Please set the 'soundCloudClientId' property before proceeding.");
            return;
        }

        var services = new ServiceCollection() // I fucking hate this
            .AddSingleton(new SoundCloudClient(settings.SoundCloudClientId))
            .AddSingleton<DiscordSocketClient>()
            .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
            .AddSingleton<CommandHandler>()
            .BuildServiceProvider();

        var soundcloud = services.GetRequiredService<SoundCloudClient>();
        DiscordClient = services.GetRequiredService<DiscordSocketClient>();
        var commands = services.GetRequiredService<InteractionService>();

        DiscordClient.Log += (msg) =>
        {
            Console.WriteLine($"[Discord][{msg.Severity}] {msg.Message}");
            return Task.CompletedTask;
        };

        await DiscordClient.LoginAsync(TokenType.Bot, settings.Token);

        var readyTask = new TaskCompletionSource();
        DiscordClient.Ready += () =>
        {
            readyTask.SetResult();
            return Task.CompletedTask;
        };

        await DiscordClient.StartAsync();

        await services.GetRequiredService<CommandHandler>().InitializeAsync();

        await readyTask.Task;

        await commands.RegisterCommandsToGuildAsync(929186964580753448);
        await commands.RegisterCommandsGloballyAsync();

        await UpdateStatus();

        DateTime nextMinTrackAge = DateTime.UtcNow;
        for (; ; )
        {
            if (DiscordClient.ConnectionState != ConnectionState.Connected || !Directory.Exists(WatchlistPath))
            {
                await Task.Delay(10000);
                continue;
            }

            var minTrackAge = nextMinTrackAge;
            nextMinTrackAge = DateTime.UtcNow;

            var creatorFiles = Directory.EnumerateFiles(WatchlistPath, "*.json");
            foreach (var file in creatorFiles)
            {
                await Task.Delay(1000);
                try
                {
                    var id = Path.GetFileNameWithoutExtension(file);
                    if (!long.TryParse(id, out var parsedId))
                        continue;

                    var creator = await soundcloud.GetUser(parsedId);
                    if (creator == null)
                        continue;

                    var tracksPage = await soundcloud.GetTracksPage(creator);
                    if (tracksPage == null || tracksPage.Tracks == null)
                        continue;

                    CreatorToWatch creatorToWatch = null;
                    var nextTrack = tracksPage.Tracks.FirstOrDefault();
                    for (var idx = 0; ; idx++)
                    {
                        if (idx >= tracksPage.Tracks.Length)
                        {
                            tracksPage = await tracksPage.GetNextPage(soundcloud.ClientId);
                            if (tracksPage == null)
                                break;

                            idx = -1;
                            continue;
                        }

                        var track = tracksPage.Tracks[idx];
                        if (track.CreatedAt < minTrackAge || track.CreatedAt > nextMinTrackAge)
                            break;

                        if (creatorToWatch == null)
                        {
                            creatorToWatch = await Settings.Load<CreatorToWatch>(file);
                            if (creatorToWatch == null)
                                break;

                            if (creatorToWatch.ChannelsWatching == null || creatorToWatch.ChannelsWatching.Count == 0)
                            {
                                File.Delete(file);
                                break;
                            }
                        }

                        var download = await soundcloud.DownloadTrack(track);
                        if (download.Failed)
                            continue;

                        var embed = Program.GenerateEmbedForTrack(track);

                        for (var channelIdx = 0; channelIdx < creatorToWatch.ChannelsWatching.Count; channelIdx++)
                        {
                            var channelId = creatorToWatch.ChannelsWatching[channelIdx];
                            var channel = DiscordClient.GetGuild(channelId.GuildId)?.GetTextChannel(channelId.Id);
                            if (channel == null)
                            {
                                creatorToWatch.ChannelsWatching.RemoveAt(channelIdx);
                                await creatorToWatch.Save();
                                channelIdx--;
                                continue;
                            }

                            try
                            {
                                using var stream = await download.Content.ReadAsStreamAsync();
                                await channel.SendFileAsync(new FileAttachment(stream, $"{track.Title}.mp3"), string.Empty, embed: embed);
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
        }
    }

    public static async Task UpdateStatus()
    {
        var creatorFileCount = Directory.Exists(WatchlistPath) ? Directory.EnumerateFiles(WatchlistPath, "*.json").Count() : 0;
        await DiscordClient.SetGameAsync($"{creatorFileCount} SC creators", type: ActivityType.Watching);
    }

    public static Embed GenerateEmbedForTrack(Track track)
    {
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
            Color = new(255, 85, 0)
        };

        if (!string.IsNullOrEmpty(tags))
        {
            embedBuilder.Fields.Add(new()
            {
                Name = "Tags",
                Value = tags
            });
        }

        return embedBuilder.Build();
    }
}

//for (; ; )
//{
//    await Task.Delay(settings.UpdateDelay);

//    var watchlist = await GetWatchlist();
//    if (watchlist == null)
//        continue;

//    foreach (var userURL in watchlist)
//    {
//        var user = await GetUserFromUrl(userURL);
//        if (user == null)
//            continue;

//        var trackPage = await GetTracksPage(user);
//        if (trackPage == null)
//            continue;

//        var lastTrack = trackPage.Tracks.FirstOrDefault();

//        var userCache = cachedUsers.GetUserCache(user, out var isNew);
//        if (isNew)
//        {
//            if (lastTrack != null)
//                userCache.LastUpload = lastTrack.CreatedAt;

//            continue;
//        }

//        if (lastTrack == null)
//            continue;

//        var lastTrackCreationTime = lastTrack.CreatedAt;
//        var trackId = 0;

//        while (lastTrack.CreatedAt > userCache.LastUpload)
//        {
//            await PostTrack(lastTrack);

//            trackId++;
//            if (trackId < trackPage.Tracks.Length)
//            {
//                lastTrack = trackPage.Tracks[trackId];
//                continue;
//            }

//            trackPage = await trackPage.GetNextPage(ClientID);
//            if (trackPage == null)
//                break;

//            lastTrack = trackPage.Tracks.FirstOrDefault();
//            if (lastTrack == null)
//                break;
//        }

//        userCache.LastUpload = lastTrackCreationTime;
//    }
//}