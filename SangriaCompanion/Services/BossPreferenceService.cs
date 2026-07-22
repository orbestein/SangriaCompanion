using BossRespawnOverlay;
namespace SangriaCompanion;
internal static class BossPreferenceService
{
    internal static void Load() => BossCatalog.Refresh();
    internal static void Save()
    {
        Plugin.FavoriteBosses.Value=string.Join(',', BossCatalog.All.Where(b=>b.IsFavorite).Select(b=>b.Name));
        Plugin.AlertBosses.Value=string.Join(',', BossCatalog.All.Where(b=>b.AlertEnabled).Select(b=>b.Name));
        foreach(var boss in BossCatalog.All) BossRespawnApi.SetPinned(boss.CommandName,boss.IsFavorite);
        Plugin.SaveState();
    }
    internal static HashSet<string> ParseNames(string value)=>value.Split(',',StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim()).Where(x=>x.Length>0).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
