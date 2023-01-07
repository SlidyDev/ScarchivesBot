namespace ScarchivesBot;

internal static class Config
{
    public const long FileSizeLimit = 8000000;
    public const string DataPath = "Data";
    public const string SCApiUrl = "https://api-v2.soundcloud.com/";

    public static string WatchlistPath = Path.Combine(DataPath, "Watchlist");
}