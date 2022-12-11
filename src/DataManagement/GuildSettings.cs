using Newtonsoft.Json;

namespace ScarchivesBot.DataManagement;

internal class GuildSettings : Settings
{
    [JsonProperty("roles")]
    public List<long> Roles { get; set; } = new();
}