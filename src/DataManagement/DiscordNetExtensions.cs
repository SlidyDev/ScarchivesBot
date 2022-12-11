using Discord.WebSocket;

namespace ScarchivesBot.DataManagement;

internal static class DiscordNetExtensions
{
    public static DirectoryInfo GetDataDir(this SocketGuildChannel channel)
    {
        return new DirectoryInfo(Path.Combine(channel.Guild.GetDataDir().FullName, "Channels", channel.Id.ToString()));
    }

    public static DirectoryInfo GetDataDir(this SocketGuild guild)
    {
        return new DirectoryInfo(Path.Combine("Data", "Guilds", guild.Id.ToString()));
    }

    public static async Task<ChannelSettings> GetSettings(this SocketGuildChannel channel)
    {
        return await Settings.Load<ChannelSettings>(Path.Combine(channel.GetDataDir().FullName, "settings.json"));
    }
}