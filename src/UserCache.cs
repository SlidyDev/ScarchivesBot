namespace ScarchivesBot;

internal class UserCache
{
    public long UserID { get; set; }

    public DateTime LastUpload { get; set; }

    public UserCache(long userID)
    {
        UserID = userID;
    }
}
