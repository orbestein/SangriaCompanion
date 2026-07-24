using UnityEngine;

namespace SangriaCompanion;

/// <summary>
/// Mantém somente em memória os pets e almas realmente obtidos após a primeira
/// leitura do inventário desta execução. O maior total já observado por item é
/// usado como referência, evitando contar novamente um item que foi movido para
/// um baú e depois devolvido à mochila.
///
/// Cada item também recebe um ato. Primeiro tentamos relacionar o nome interno
/// ao catálogo de bosses; depois aplicamos os mapeamentos manuais da configuração.
/// Itens sem correspondência continuam visíveis na seção "Sem ato definido".
/// </summary>
internal static class SessionDropService
{
    private static readonly Dictionary<string, int> HighestObserved = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, SessionDropEntry> EntriesByItem = new(StringComparer.OrdinalIgnoreCase);
    private static DateTime _startedAt = DateTime.Now;
    private static bool _baselineReady;

    internal static DateTime StartedAt => _startedAt;
    internal static bool BaselineReady => _baselineReady;
    internal static TimeSpan Elapsed => DateTime.Now - _startedAt;
    internal static IReadOnlyList<SessionDropEntry> Entries => EntriesByItem.Values
        .OrderBy(x => x.Act == 0 ? 5 : x.Act)
        .ThenByDescending(x => x.LastSeen)
        .ThenBy(x => x.DisplayName)
        .ToArray();

    internal static int PetQuantity => EntriesByItem.Values.Where(x => x.Kind == SessionDropKind.Pet).Sum(x => x.Quantity);
    internal static int SoulQuantity => EntriesByItem.Values.Where(x => x.Kind == SessionDropKind.Soul).Sum(x => x.Quantity);
    internal static int TotalQuantity => PetQuantity + SoulQuantity;
    internal static int UniqueCount => EntriesByItem.Count;

    internal static IReadOnlyList<SessionDropEntry> EntriesForAct(int act) => EntriesByItem.Values
        .Where(x => x.Act == act)
        .OrderByDescending(x => x.LastSeen)
        .ThenBy(x => x.DisplayName)
        .ToArray();

    internal static int QuantityForAct(int act) => EntriesByItem.Values
        .Where(x => x.Act == act)
        .Sum(x => x.Quantity);

    internal static int UniqueForAct(int act) => EntriesByItem.Values.Count(x => x.Act == act);

    internal static string ActTitle(int act) => act switch
    {
        1 => "ATO 1",
        2 => "ATO 2",
        3 => "ATO 3",
        4 => "ATO 4",
        _ => "SEM ATO DEFINIDO"
    };

    internal static void StartNewSession()
    {
        _startedAt = DateTime.Now;
        _baselineReady = false;
        HighestObserved.Clear();
        EntriesByItem.Clear();
    }

    internal static void ResetUsingCurrentInventory(IReadOnlyDictionary<string, int> inventory)
    {
        _startedAt = DateTime.Now;
        EntriesByItem.Clear();
        HighestObserved.Clear();
        EstablishBaseline(inventory);
        Plugin.Instance.Log.LogInfo("Sessão de pets e almas reiniciada; o inventário atual virou a nova linha de base.");
    }

    internal static void ObserveInventory(IReadOnlyDictionary<string, int> inventory)
    {
        if (!_baselineReady)
        {
            EstablishBaseline(inventory);
            return;
        }

        foreach (var pair in inventory)
        {
            if (!TryClassify(pair.Key, out var kind, out var displayName)) continue;

            var amount = Math.Max(0, pair.Value);
            var previousPeak = HighestObserved.TryGetValue(pair.Key, out var peak) ? peak : 0;
            if (amount <= previousPeak) continue;

            var gained = amount - previousPeak;
            HighestObserved[pair.Key] = amount;
            Record(pair.Key, displayName, kind, gained);
        }
    }

    private static void EstablishBaseline(IReadOnlyDictionary<string, int> inventory)
    {
        HighestObserved.Clear();
        foreach (var pair in inventory)
        {
            if (TryClassify(pair.Key, out _, out _))
                HighestObserved[pair.Key] = Math.Max(0, pair.Value);
        }
        _baselineReady = true;
    }

    private static void Record(string itemName, string displayName, SessionDropKind kind, int quantity)
    {
        if (quantity <= 0) return;
        var now = DateTime.Now;
        if (EntriesByItem.TryGetValue(itemName, out var existing))
        {
            existing.Quantity += quantity;
            existing.LastSeen = now;
        }
        else
        {
            EntriesByItem[itemName] = new SessionDropEntry
            {
                ItemName = itemName,
                DisplayName = displayName,
                Kind = kind,
                Act = ResolveAct(itemName, displayName),
                Quantity = quantity,
                FirstSeen = now,
                LastSeen = now
            };
        }

        var entry = EntriesByItem[itemName];
        var category = kind == SessionDropKind.Soul ? "Alma" : "Pet";
        var actText = entry.Act > 0 ? "Ato " + entry.Act : "Ato não identificado";
        NotificationCenter.Enqueue(
            category + " obtido",
            displayName + "  •  " + actText + "  •  +" + quantity,
            kind == SessionDropKind.Soul ? SCTheme.Purple : SCTheme.Green,
            Plugin.CollectionAlertDuration.Value,
            "Pet/Alma");
        Plugin.Instance.Log.LogInfo($"[Sessão] {category} detectado: {displayName} +{quantity} • {actText} ({itemName}).");
    }

    internal static bool TryClassify(string itemName, out SessionDropKind kind, out string displayName)
    {
        var raw = itemName ?? string.Empty;
        var normalized = Normalize(raw);

        var customKeywords = (Plugin.SessionDropKeywords.Value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(x => x.Length > 0)
            .ToArray();

        var isSoul = normalized.Contains("soulshard") ||
                     HasKeyword(raw, normalized, "soul") ||
                     HasKeyword(raw, normalized, "alma");
        var isPet = HasKeyword(raw, normalized, "pet") ||
                    HasKeyword(raw, normalized, "familiar") ||
                    HasKeyword(raw, normalized, "companion") ||
                    HasKeyword(raw, normalized, "summon");

        if (!isSoul && !isPet && customKeywords.Length > 0)
        {
            foreach (var keyword in customKeywords)
            {
                if (!HasKeyword(raw, normalized, keyword)) continue;
                isSoul = keyword.Contains("soul") || keyword.Contains("alma");
                isPet = !isSoul;
                break;
            }
        }

        if (!isSoul && !isPet)
        {
            kind = default;
            displayName = string.Empty;
            return false;
        }

        kind = isSoul ? SessionDropKind.Soul : SessionDropKind.Pet;
        displayName = BuildDisplayName(raw, kind);
        return true;
    }

    private static int ResolveAct(string rawItemName, string displayName)
    {
        var raw = Normalize(rawItemName);
        var display = Normalize(displayName);

        // O mapeamento manual tem prioridade. Isso permite classificar nomes
        // personalizados do servidor sem precisar publicar uma nova DLL.
        foreach (var mapping in ParseActMappings())
        {
            if (raw.Contains(mapping.Keyword) || display.Contains(mapping.Keyword))
                return mapping.Act;
        }

        // Para itens cujo nome contém o boss de origem, reutilizamos o mesmo
        // catálogo e a mesma regra de níveis já empregada pela aba Bosses.
        foreach (var boss in BossCatalog.All
                     .OrderByDescending(x => Math.Max(Normalize(x.Name).Length, Normalize(x.CommandName).Length)))
        {
            var name = Normalize(boss.Name);
            var command = Normalize(boss.CommandName);
            if ((name.Length >= 4 && (raw.Contains(name) || display.Contains(name))) ||
                (command.Length >= 4 && (raw.Contains(command) || display.Contains(command))))
                return boss.Act;
        }

        // Alguns itens do servidor trazem o ato explicitamente no nome interno.
        for (var act = 1; act <= 4; act++)
        {
            if (raw.Contains("ato" + act) || raw.Contains("act" + act))
                return act;
        }

        return 0;
    }

    private static IReadOnlyList<(string Keyword, int Act)> ParseActMappings()
    {
        var result = new List<(string Keyword, int Act)>();
        var raw = Plugin.SessionDropActMappings.Value ?? string.Empty;
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0 || separator >= part.Length - 1) continue;
            var keyword = Normalize(part.Substring(0, separator));
            if (keyword.Length < 2) continue;
            if (!int.TryParse(part.Substring(separator + 1).Trim(), out var act)) continue;
            if (act is < 1 or > 4) continue;
            result.Add((keyword, act));
        }
        return result;
    }

    private static string BuildDisplayName(string raw, SessionDropKind kind)
    {
        var text = raw
            .Replace("MagicSource SoulShard ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("MagicSource_SoulShard_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("SoulShard ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("SoulShard_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Item ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Item_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Consumable ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Consumable_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace('_', ' ')
            .Trim();

        if (string.IsNullOrWhiteSpace(text)) text = raw.Trim();
        if (kind == SessionDropKind.Soul && !text.StartsWith("Alma", StringComparison.OrdinalIgnoreCase))
            text = "Alma de " + text;
        return text.Length > 54 ? text.Substring(0, 54) : text;
    }

    private static bool HasKeyword(string raw, string normalized, string keyword)
    {
        var cleanKeyword = Normalize(keyword);
        if (cleanKeyword.Length == 0) return false;
        if (cleanKeyword.Length > 3) return normalized.Contains(cleanKeyword);

        var tokens = (raw ?? string.Empty)
            .Split(new[] { ' ', '_', '-', '.', '/', '\\', ':', ';', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize);
        return tokens.Any(x => x.Equals(cleanKeyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string value)
        => new string((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
