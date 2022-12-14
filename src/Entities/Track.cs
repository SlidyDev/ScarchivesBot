using Newtonsoft.Json;

namespace ScarchivesBot.Entities;

public class Track : Entity
{
    public override string ExpectedEntityKind => "track";

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

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    public class Media
    {
        [JsonProperty("transcodings")]
        public Transcoding[] Transcodings { get; set; }
    }

    public override string ToString()
    {
        return $"'{Title}' by {Author.Username}";
    }
}
