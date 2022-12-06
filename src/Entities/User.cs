using Newtonsoft.Json;

namespace ScarchivesBot.Entities;

public class User : Entity
{
    [JsonProperty("username")]
    public string Username { get; set; }

    [JsonProperty("avatar_url")]
    public string AvatarURL { get; set; }

    [JsonProperty("permalink_url")]
    public string URL { get; set; }

    [JsonProperty("id")]
    public long ID { get; set; }

    public override string ExpectedEntityKind => "user";
}
