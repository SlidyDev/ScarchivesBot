using Newtonsoft.Json;

namespace ScarchivesBot.DataManagement
{
    internal class BotSettings : Settings
    {
        [JsonProperty("token")]
        public string Token { get; set; } = "";

        [JsonProperty("updateDelay")]
        public int UpdateDelay { get; set; } = 60000;
    }
}
