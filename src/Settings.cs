using Newtonsoft.Json;

namespace ScarchivesBot
{
    internal class Settings
    {
        [JsonProperty]
        public string WebhookURL { get; set; } = "";

        [JsonProperty]
        public string Username { get; set; } = "No Name";

        [JsonProperty]
        public string PfpLink { get; set; } = "";
    }
}
