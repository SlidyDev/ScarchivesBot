using Newtonsoft.Json;

namespace ScarchivesBot.Entities;

public class TracksPage
{
    [JsonProperty("collection")]
    public Track[] Tracks { get; set; }

    [JsonProperty("next_href")]
    public string NextPageURL { get; set; }

    public async Task<TracksPage> GetNextPage(string clientID)
    {
        if (string.IsNullOrEmpty(NextPageURL))
            return null;

        using var client = new HttpClient();
        var finalURL = $"{NextPageURL}&client_id={clientID}";

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(finalURL);
        }
        catch
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
            return null;

        var nextPage = JsonConvert.DeserializeObject<TracksPage>(await response.Content.ReadAsStringAsync());
        if (nextPage.Tracks == null || nextPage.Tracks.Length == 0)
            return null;

        return nextPage;
    }
}
