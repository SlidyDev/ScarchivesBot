using Newtonsoft.Json;
using ScarchivesBot.DiscordUtilities;

namespace ScarchivesBot.DataManagement;

internal class CreatorToWatch : Settings
{
    [JsonProperty("channelsWatching")]
    public List<ChannelId> ChannelsWatching { get; set; } = new();
}
