using BossRespawnOverlay;

namespace SangriaCompanion;

internal enum CompanionBossStatus { Waiting, Querying, Alive, Dead, NotFound }

internal sealed class CompanionBoss
{
    internal string Name { get; set; } = string.Empty;
    internal string CommandName { get; set; } = string.Empty;
    internal int Level { get; set; }
    internal int Act => Level <= 47 ? 1 : Level <= 68 ? 2 : Level <= 75 ? 3 : 4;
    internal bool IsFavorite { get; set; }
    internal bool AlertEnabled { get; set; }
    internal CompanionBossStatus Status { get; set; }
    internal float RemainingSeconds { get; set; }
    internal CompanionBossStatus LastConfirmedStatus { get; set; } = CompanionBossStatus.Waiting;
    internal float LastConfirmedRemainingSeconds { get; set; }

    internal bool TryGetConfirmedDisplay(out CompanionBossStatus status, out float remainingSeconds)
    {
        if (Status == CompanionBossStatus.Alive || Status == CompanionBossStatus.Dead)
        {
            status = Status;
            remainingSeconds = RemainingSeconds;
            return true;
        }

        if (LastConfirmedStatus == CompanionBossStatus.Alive || LastConfirmedStatus == CompanionBossStatus.Dead)
        {
            status = LastConfirmedStatus;
            remainingSeconds = LastConfirmedRemainingSeconds;
            return true;
        }

        status = CompanionBossStatus.Waiting;
        remainingSeconds = 0f;
        return false;
    }
}

internal static class BossCatalog
{
    // Catálogo local deliberadamente independente do bridge. Assim a tela nunca
    // fica vazia, mesmo antes do BossRespawnOverlay inicializar ou entrar no mundo.
    private static readonly (string Name, int Level, string Command)[] Defaults =
    [
        ("Keely",30,"keely"),("Errol",30,"errol"),("Rufus",30,"rufus"),("Grayson",37,"grayson"),("Goreswine",37,"goreswine"),("Lidia",40,"lidia"),("Clive",40,"clive"),("Finn",42,"finn"),("Polora",45,"polora"),("Kodia",45,"kodia"),("Nicolau",45,"nicolau"),("Quincey",47,"quincey"),
        ("Beatrice",50,"beatrice"),("Vincent",54,"vincent"),("Christina",54,"christina"),("Tristan",54,"tristan"),("Erwin",56,"erwin"),("Kriig",57,"kriig"),("Leandra",57,"leandra"),("Maja",57,"maja"),("Bane",60,"bane"),("Grethel",60,"grethel"),("Meredith",60,"meredith"),("Terah",63,"terah"),("Frostmaw",63,"frostmaw"),("Elena",63,"elena"),("Gaius",65,"gaius"),("Cassius",67,"cassius"),("Jade",67,"jade"),("Raziel",67,"raziel"),("Octavian",68,"octavian"),
        ("Ziva",70,"ziva"),("Domina",70,"domina"),("Angram",71,"angram"),("Ungora",73,"ungora"),("Ben",73,"ben"),("Foulrot",73,"foulrot"),("Albert",74,"albert"),("Willfred",74,"willfred"),("Cyril",75,"cyril"),
        ("Magnus",76,"magnus"),("Barão",80,"bar~ao"),("Morian",80,"morian"),("Mairwyn",80,"mairwyn"),("Henry",84,"henry"),("Jakira",85,"jakira"),("Stavros",85,"stavros"),("Lucile",86,"lucile"),("Matka",86,"matka"),("Terrorclaw",86,"terrorclaw"),("Azariel",89,"azariel"),("Voltatia",89,"voltatia"),("Simon",90,"simon"),("Dantos",92,"dantos"),("Styx",94,"styx"),("Gorecrusher",94,"gorecrusher"),("Valencia",94,"valencia"),("Solarus",96,"solarus"),("Talzur",96,"talzur"),("Megara",98,"megara"),("Adam",98,"adam")
    ];

    private static readonly List<CompanionBoss> Items = Defaults.Select(x => new CompanionBoss
    {
        Name = x.Name,
        CommandName = x.Command,
        Level = x.Level,
        Status = CompanionBossStatus.Waiting
    }).ToList();

    internal static IReadOnlyList<CompanionBoss> All => Items;

    internal static void Refresh()
    {
        var favorites = BossPreferenceService.ParseNames(Plugin.FavoriteBosses.Value);
        var alerts = BossPreferenceService.ParseNames(Plugin.AlertBosses.Value);

        foreach (var item in Items)
        {
            item.IsFavorite = favorites.Contains(item.Name) || favorites.Contains(item.CommandName);
            item.AlertEnabled = alerts.Contains(item.Name) || alerts.Contains(item.CommandName);
        }

        IReadOnlyList<BossRespawnSnapshot> snapshots;
        try { snapshots = BossRespawnApi.GetBosses(); }
        catch { return; }

        foreach (var snap in snapshots)
        {
            var item = Items.FirstOrDefault(x => x.CommandName.Equals(snap.CommandName, StringComparison.OrdinalIgnoreCase));
            if (item == null) continue;
            item.Name = snap.DisplayName;
            item.Level = snap.Level;
            item.IsFavorite = item.IsFavorite || snap.IsPinned;
            item.RemainingSeconds = snap.RemainingSeconds;
            item.Status = snap.IsQuerying ? CompanionBossStatus.Querying
                : !snap.HasResponse ? CompanionBossStatus.Waiting
                : snap.HasError ? CompanionBossStatus.NotFound
                : snap.IsAlive ? CompanionBossStatus.Alive
                : CompanionBossStatus.Dead;

            if (item.Status == CompanionBossStatus.Alive || item.Status == CompanionBossStatus.Dead)
            {
                item.LastConfirmedStatus = item.Status;
                item.LastConfirmedRemainingSeconds = item.RemainingSeconds;
            }
        }
    }

    internal static string ActTitle(int act) => act switch
    {
        1 => "ATO 1  •  NÍVEIS 30–47",
        2 => "ATO 2  •  NÍVEIS 50–68",
        3 => "ATO 3  •  NÍVEIS 70–75",
        _ => "ATO 4  •  NÍVEIS 76+"
    };
}
