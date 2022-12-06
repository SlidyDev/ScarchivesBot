using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using ScarchivesBot.DataManagement;
using ScarchivesBot.DiscordUtilities;
using ScarchivesBot.Entities;
using System.Net.Http;
using System.Text;
using System.Web;
using static ScarchivesBot.Config;

namespace ScarchivesBot;

internal static class Program
{
    private static async Task Main()
    {
        var settings = await Settings.Load<BotSettings>(SettingsPath);
        if (!File.Exists(SettingsPath))
        {
            await settings.Save();

            Console.WriteLine($"A {SettingsPath} file has been created. Please set the 'token' property before proceeding.");
            return;
        }

        if (string.IsNullOrEmpty(settings.Token))
        {
            Console.WriteLine("Please set the 'token' property before proceeding.");
            return;
        }

        var services = new ServiceCollection() // I fucking hate this
            .AddSingleton(x => new SoundCloudClient(settings.DownloadsPath))
            .AddSingleton<DiscordSocketClient>()
            .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
            .AddSingleton<CommandHandler>()
            .BuildServiceProvider();

        var discord = services.GetRequiredService<DiscordSocketClient>();
        var commands = services.GetRequiredService<InteractionService>();

        discord.Log += (msg) =>
        {
            Console.WriteLine($"[Discord][{msg.Severity}] {msg.Message}");
            return Task.CompletedTask;
        };

        await discord.LoginAsync(TokenType.Bot, settings.Token);

        var readyTask = new TaskCompletionSource();
        discord.Ready += () =>
        {
            readyTask.SetResult();
            return Task.CompletedTask;
        };

        await discord.StartAsync();

        await services.GetRequiredService<CommandHandler>().InitializeAsync();

        await readyTask.Task;
        await commands.RegisterCommandsToGuildAsync(929186964580753448);

        await Task.Delay(-1);
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