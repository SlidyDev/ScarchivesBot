using Newtonsoft.Json;

namespace ScarchivesBot.Entities;

internal abstract class Entity
{
    [JsonProperty("kind")]
    public string EntityKind { get; set; }
}
