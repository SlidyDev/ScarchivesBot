using Newtonsoft.Json;

namespace ScarchivesBot.DiscordUtilities;

internal struct ChannelId
{
    [JsonProperty("id")]
    public ulong Id { get; set; }

    [JsonProperty("guildId")]
    public ulong GuildId { get; set; }
}
