using Newtonsoft.Json;
using ScarchivesBot.DiscordUtilities;

namespace ScarchivesBot.DataManagement;

internal class CreatorToWatch : Settings
{
    [JsonProperty("channelsWatching")]
    public List<ChannelId> ChannelsWatching { get; set; } = new();

    public override async Task Save()
    {
        if (ChannelsWatching.Count > 0)
        {
            await base.Save();
            return;
        }

        try
        {
            File.Delete(Path);
        }
        catch { }
    }
}
