using Newtonsoft.Json;
using System.Collections.Generic;

namespace ScarchivesBot.DataManagement;

internal class GuildSettings : Settings
{
    [JsonProperty("roles")]
    public List<long> Roles { get; set; } = new();
}