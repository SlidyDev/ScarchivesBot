using Newtonsoft.Json;

namespace ScarchivesBot.DataManagement;

internal abstract class Settings
{
    [JsonIgnore]
    public string Path { get; set; }

    public async Task Save()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
        await WritePatiently(Path, JsonConvert.SerializeObject(this));
    }

    public static async Task<T> Load<T>(string path) where T : Settings, new()
    {
        if (!File.Exists(path))
            return new T { Path = path };

        return JsonConvert.DeserializeObject<T>(await ReadPatiently(path));
    }

    private static async Task WritePatiently(string path, string contents, int maxAttempts = 10, int retryDelayMs = 200)
    {
        for (var numTries = 0; numTries < maxAttempts; numTries++)
        {
            try
            {
                await File.WriteAllTextAsync(path, contents);
                return;
            }
            catch (IOException)
            {
                await Task.Delay(retryDelayMs);
            }
        }
    }

    private static async Task<string> ReadPatiently(string path, int maxAttempts = 10, int retryDelayMs = 200)
    {
        for (var numTries = 0; numTries < maxAttempts; numTries++)
        {
            try
            {
                return await File.ReadAllTextAsync(path);
            }
            catch (IOException)
            {
                await Task.Delay(retryDelayMs);
            }
        }

        return null;
    }
}