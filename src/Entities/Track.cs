using Newtonsoft.Json;

namespace ScarchivesBot.Entities;

internal class Track
{
    [JsonProperty("title")]
    public string Title { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("artwork_url")]
    public string CoverURL { get; set; }

    [JsonProperty("tag_list")]
    public string TagList { get; set; }

    [JsonProperty("user")]
    public User Author { get; set; }

    [JsonProperty("media")]
    public Media Audio { get; set; }

    [JsonProperty("id")]
    public long ID { get; set; }

    internal class Media
    {
        [JsonProperty("transcodings")]
        public Transcoding[] Transcodings { get; set; }
    }
}
