using Newtonsoft.Json;

namespace ScarchivesBot.Entities;

public abstract class Entity
{
    [JsonProperty("kind")]
    public string EntityKind { get; set; }

    [JsonIgnore]
    public abstract string ExpectedEntityKind { get; }
}
