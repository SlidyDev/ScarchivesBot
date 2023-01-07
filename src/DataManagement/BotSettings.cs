using Newtonsoft.Json;

namespace ScarchivesBot.DataManagement;

internal class BotSettings : Settings
{
    [JsonProperty("token")]
    public string Token { get; set; } = "";

    [JsonProperty("soundCloudClientId")]
    public string SoundCloudClientId { get; set; } = "";
}
