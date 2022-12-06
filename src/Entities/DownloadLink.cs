using Newtonsoft.Json;

namespace ScarchivesBot.Entities;

public class DownloadLink
{
    [JsonProperty("url")]
    public string URL { get; set; }
}
