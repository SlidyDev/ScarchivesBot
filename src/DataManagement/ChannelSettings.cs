using Newtonsoft.Json;

namespace ScarchivesBot.DataManagement;

internal class ChannelSettings : Settings
{
    [JsonProperty("creators")]
    public List<long> Creators { get; set; } = new();
}