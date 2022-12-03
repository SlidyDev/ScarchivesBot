using Newtonsoft.Json;

namespace ScarchivesBot.Entities;

internal class User
{
    [JsonProperty("username")]
    public string Username { get; set; }

    [JsonProperty("avatar_url")]
    public string AvatarURL { get; set; }

    [JsonProperty("permalink_url")]
    public string URL { get; set; }
}
