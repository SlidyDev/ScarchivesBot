using Newtonsoft.Json;

namespace ScarchivesBot.DataManagement;

internal abstract class Settings
{
    [JsonIgnore]
    public string Path { get; set; }

    public async Task Save()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
        await File.WriteAllTextAsync(Path, JsonConvert.SerializeObject(this));
    }

    public static async Task<T> Load<T>(string path) where T : Settings, new()
    {
        if (!File.Exists(path))
            return new T { Path = path };

        return JsonConvert.DeserializeObject<T>(await File.ReadAllTextAsync(path));
    }
}