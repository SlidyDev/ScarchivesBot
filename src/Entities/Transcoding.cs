using Newtonsoft.Json;

namespace ScarchivesBot.Entities;

internal class Transcoding
{
    [JsonProperty("url")]
    public string URL { get; set; }

    [JsonProperty("format")]
    public Format AudioFormat { get; set; }

    internal class Format
    {
        [JsonProperty("protocol")]
        public string Protocol { get; set; }

        [JsonProperty("mime_type")]
        public string MimeType { get; set; }
    }
}
