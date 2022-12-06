using System;
using System.IO;
using Discord.WebSocket;

namespace ScarchivesBot.DataManagement;

internal static class DiscordNetExtensions
{
    public static DirectoryInfo GetDataDir(this SocketTextChannel channel)
    {
        return new DirectoryInfo(Path.Combine(channel.Guild.GetDataDir().FullName, "TextChannels", channel.Id.ToString()));
    }

    public static DirectoryInfo GetDataDir(this SocketGuild guild)
    {
        return new DirectoryInfo(Path.Combine("Data", "Guilds", guild.Id.ToString()));
    }

    public static async Task<GuildSettings> GetSettings(this SocketGuild guild)
    {
        return await Settings.Load<GuildSettings>(Path.Combine(guild.GetDataDir().FullName, "settings.json"));
    }
}