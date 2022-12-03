using ScarchivesBot.Entities;

namespace ScarchivesBot;

internal class CachedUsers
{
    public List<UserCache> Users { get; private set; } = new();

    public UserCache GetUserCache(User user, out bool isNew)
    {
        isNew = false;
        var result = Users.Find(x => x.UserID == user.ID);
        if (result != null)
            return result;

        isNew = true;
        result = new UserCache(user.ID);
        Users.Add(result);
        return result;
    }
}
