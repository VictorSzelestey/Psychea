using PsyPath.Models;
using PsyPath.UI;

namespace PsyPath.Services;

public readonly record struct DungeonReward(TimeSpan BestTime, bool IsRecord, bool FirstClear, (string Id, string Name)? Avatar);

public static class DungeonRewards
{
    public static DungeonReward Collect(DungeonDefinition dungeon, TimeSpan time)
    {
        var progress = AppState.P;
        var records = progress.DungeonRecords ??= new();
        long milliseconds = (long)time.TotalMilliseconds;
        bool first = !records.TryGetValue(dungeon.Id, out long oldRecord);
        bool record = first || milliseconds < oldRecord;
        if (record) records[dungeon.Id] = milliseconds;

        (string Id, string Name)? avatarReward = null;
        if (first)
        {
            progress.Crystals += DungeonDefinition.FirstClearCrystals;
            var available = Avatars.All.Where(a => a.Price > 0 && !progress.Owned.Contains(a.Id)).ToArray();
            if (available.Length > 0)
            {
                var avatar = available[Random.Shared.Next(available.Length)];
                progress.Owned.Add(avatar.Id);
                avatarReward = (avatar.Id, avatar.Name);
            }
        }

        AppState.AddXp(DungeonDefinition.ExperienceReward);
        progress.Rating += DungeonDefinition.RatingReward;
        AppState.Save();
        return new DungeonReward(TimeSpan.FromMilliseconds(records[dungeon.Id]), record, first, avatarReward);
    }
}
