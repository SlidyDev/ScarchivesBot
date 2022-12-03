using Newtonsoft.Json;

namespace ScarchivesBot.Entities;

internal class DownloadLink
{
    [JsonProperty("url")]
    public string URL { get; set; }
}
