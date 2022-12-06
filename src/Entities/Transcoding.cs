using Newtonsoft.Json;

namespace ScarchivesBot.Entities;

public class Transcoding
{
    [JsonProperty("url")]
    public string URL { get; set; }

    [JsonProperty("format")]
    public Format AudioFormat { get; set; }

    public class Format
    {
        [JsonProperty("protocol")]
        public string Protocol { get; set; }

        [JsonProperty("mime_type")]
        public string MimeType { get; set; }
    }
}
