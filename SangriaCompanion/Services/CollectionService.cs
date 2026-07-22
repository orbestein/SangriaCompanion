using System.Globalization;

namespace SangriaCompanion;

internal static class CollectionService
{
    private static readonly Dictionary<string, ItemRecipe> Recipes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Núcleo de Energia"] = new ItemRecipe("Núcleo de Energia", "Fabricador",
            new RecipeIngredient("Liga de Rádio", 3),
            new RecipeIngredient("Bateria Carregada", 3)),
        ["Liga de Rádio"] = new ItemRecipe("Liga de Rádio", "Fornalha Avançada",
            new RecipeIngredient("Recipiente de Lodo", 1),
            new RecipeIngredient("Enxofre", 4),
            new RecipeIngredient("Sucata Tecnológica", 60)),
        ["Recipiente de Lodo"] = new ItemRecipe("Recipiente de Lodo", "Fabricador",
            new RecipeIngredient("Recipiente Vazio", 1),
            new RecipeIngredient("Lodo Mutante", 1)),
        ["Lingote de Ferro"] = new ItemRecipe("Lingote de Ferro", "Fornalha",
            new RecipeIngredient("Minério de Ferro", 20)),
        ["Tábua Reforçada"] = new ItemRecipe("Tábua Reforçada", "Serraria",
            new RecipeIngredient("Tábua", 4),
            new RecipeIngredient("Lingote de Ferro", 4)),
    };

    private static readonly Dictionary<string, int> Collected = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<CollectionHistoryEntry> History = new();
    private static bool _loaded;
    private static readonly HashSet<string> CompletedRequirementKeys = new(StringComparer.OrdinalIgnoreCase);

    internal static IReadOnlyCollection<ItemRecipe> AllRecipes => Recipes.Values.OrderBy(x => x.Name).ToArray();
    internal static IReadOnlyList<CollectionHistoryEntry> RecentHistory => History;

    internal static int RecipeCount => Recipes.Count;

    internal static void RegisterDiscoveredRecipe(ItemRecipe recipe)
    {
        if (recipe == null || string.IsNullOrWhiteSpace(recipe.Name) || recipe.Ingredients.Count == 0) return;
        Recipes[recipe.Name] = recipe;
    }

    internal static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        Collected.Clear();
        History.Clear();

        foreach (var part in (Plugin.CollectionQuantities.Value ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var index = part.LastIndexOf('=');
            if (index <= 0) continue;
            var name = Uri.UnescapeDataString(part.Substring(0, index));
            if (int.TryParse(part.Substring(index + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                Collected[name] = Math.Max(0, value);
        }

        foreach (var part in (Plugin.CollectionHistory.Value ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = part.Split(';');
            if (fields.Length != 4) continue;
            if (!long.TryParse(fields[0], out var ticks)) continue;
            if (!int.TryParse(fields[2], out var delta)) continue;
            if (!int.TryParse(fields[3], out var total)) continue;
            try { History.Add(new CollectionHistoryEntry(new DateTime(ticks), Uri.UnescapeDataString(fields[1]), delta, total)); }
            catch { }
        }
        TrimHistory();
    }

    internal static int GetCollected(string itemName)
    {
        Load();
        if (InventorySyncService.IsReady)
            return InventorySyncService.GetAmount(itemName);
        return Collected.TryGetValue(itemName, out var value) ? value : 0;
    }

    internal static void RecordInventoryChange(string itemName, int delta, int newTotal)
    {
        Load();
        if (string.IsNullOrWhiteSpace(itemName) || delta == 0) return;
        History.Insert(0, new CollectionHistoryEntry(DateTime.Now, itemName, delta, Math.Max(0, newTotal)));
        TrimHistory();
        EvaluateRequirementCompletion(itemName, newTotal);
        Save();
    }

    private static void EvaluateRequirementCompletion(string itemName, int currentAmount)
    {
        var required = GetBaseRequirements().Concat(GetCraftRequirements())
            .Where(x => x.ItemName.Equals(itemName, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Required)
            .DefaultIfEmpty(0)
            .Max();
        if (required <= 0) return;

        var key = SelectedRecipeName + "|" + DesiredQuantity + "|" + itemName + "|" + required;
        if (currentAmount >= required)
        {
            if (CompletedRequirementKeys.Add(key))
                NotificationCenter.Enqueue(itemName, "Quantidade suficiente: " + currentAmount + " / " + required + ".", SCTheme.Green, 5f, "Coleta");
        }
        else
        {
            CompletedRequirementKeys.Remove(key);
        }
    }

    internal static void Add(string itemName, int delta)
    {
        Load();
        if (string.IsNullOrWhiteSpace(itemName) || delta == 0) return;
        var next = Math.Max(0, GetCollected(itemName) + delta);
        var actualDelta = next - GetCollected(itemName);
        if (actualDelta == 0) return;
        Collected[itemName] = next;
        History.Insert(0, new CollectionHistoryEntry(DateTime.Now, itemName, actualDelta, next));
        TrimHistory();
        Save();
    }

    internal static void ResetSession()
    {
        Collected.Clear();
        History.Clear();
        Save();
    }

    internal static ItemRecipe? GetRecipe(string name)
        => Recipes.TryGetValue(name ?? string.Empty, out var recipe) ? recipe : null;

    internal static IReadOnlyList<ItemRecipe> SearchRecipes(string query)
    {
        var normalized = (query ?? string.Empty).Trim();
        if (normalized.Length == 0)
            return AllRecipes.ToArray();

        return Recipes.Values
            .Where(recipe =>
                recipe.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                recipe.Station.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                recipe.Ingredients.Any(ingredient =>
                    ingredient.ItemName.Contains(normalized, StringComparison.OrdinalIgnoreCase)) ||
                GetBaseIngredientNames(recipe.Name).Any(item =>
                    item.Contains(normalized, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(recipe => recipe.Name.StartsWith(normalized, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(recipe => recipe.Name)
            .ToArray();
    }

    private static IEnumerable<string> GetBaseIngredientNames(string recipeName)
    {
        var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Expand(recipeName, 1, totals, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return totals.Keys;
    }

    internal static string SelectedRecipeName
    {
        get
        {
            var selected = Plugin.SelectedRecipe.Value;
            return Recipes.ContainsKey(selected) ? selected : Recipes.Keys.First();
        }
        set
        {
            if (!Recipes.ContainsKey(value)) return;
            Plugin.SelectedRecipe.Value = value;
            Plugin.SaveState();
        }
    }

    internal static int DesiredQuantity
    {
        get => Math.Clamp(Plugin.DesiredRecipeQuantity.Value, 1, 999);
        set
        {
            Plugin.DesiredRecipeQuantity.Value = Math.Clamp(value, 1, 999);
            Plugin.SaveState();
        }
    }

    internal static IReadOnlyList<MaterialRequirement> GetDirectRequirements()
    {
        var recipe = GetRecipe(SelectedRecipeName);
        if (recipe == null) return Array.Empty<MaterialRequirement>();
        var batches = GetRequiredBatches(DesiredQuantity, recipe.OutputAmount);
        return recipe.Ingredients.Select(x => new MaterialRequirement(
            x.ItemName,
            x.Quantity * batches,
            GetCollected(x.ItemName),
            Recipes.ContainsKey(x.ItemName))).ToArray();
    }

    internal static IReadOnlyList<MaterialRequirement> GetBaseRequirements()
    {
        var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Expand(SelectedRecipeName, DesiredQuantity, totals, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return totals.OrderBy(x => x.Key)
            .Select(x => new MaterialRequirement(x.Key, x.Value, GetCollected(x.Key), false))
            .ToArray();
    }

    internal static IReadOnlyList<MaterialRequirement> GetCraftRequirements()
    {
        var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        AccumulateCrafted(SelectedRecipeName, DesiredQuantity, true, totals, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return totals.OrderBy(x => x.Key)
            .Select(x => new MaterialRequirement(x.Key, x.Value, GetCollected(x.Key), true))
            .ToArray();
    }

    internal static IReadOnlyList<(int Depth, string ItemName, int Required, bool Crafted)> GetRecipeTree()
    {
        var rows = new List<(int, string, int, bool)>();
        BuildTree(SelectedRecipeName, DesiredQuantity, 0, rows, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return rows;
    }

    internal static IEnumerable<string> AllKnownItems()
    {
        Load();
        return Recipes.Keys
            .Concat(Recipes.Values.SelectMany(x => x.Ingredients.Select(i => i.ItemName)))
            .Concat(Collected.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x);
    }

    private static void AccumulateCrafted(string itemName, int amount, bool isRoot, Dictionary<string, int> totals, HashSet<string> path)
    {
        if (!Recipes.TryGetValue(itemName, out var recipe)) return;
        if (!isRoot)
            totals[itemName] = totals.TryGetValue(itemName, out var current) ? current + amount : amount;
        if (!path.Add(itemName)) return;
        var batches = GetRequiredBatches(amount, recipe.OutputAmount);
        foreach (var ingredient in recipe.Ingredients)
            AccumulateCrafted(ingredient.ItemName, batches * ingredient.Quantity, false, totals, path);
        path.Remove(itemName);
    }

    private static void Expand(string itemName, int amount, Dictionary<string, int> totals, HashSet<string> path)
    {
        if (!Recipes.TryGetValue(itemName, out var recipe))
        {
            totals[itemName] = totals.TryGetValue(itemName, out var current) ? current + amount : amount;
            return;
        }
        if (!path.Add(itemName)) return;
        var batches = GetRequiredBatches(amount, recipe.OutputAmount);
        foreach (var ingredient in recipe.Ingredients)
            Expand(ingredient.ItemName, batches * ingredient.Quantity, totals, path);
        path.Remove(itemName);
    }

    private static void BuildTree(string itemName, int amount, int depth, List<(int, string, int, bool)> rows, HashSet<string> path)
    {
        var crafted = Recipes.ContainsKey(itemName);
        rows.Add((depth, itemName, amount, crafted));
        if (!crafted || !path.Add(itemName)) return;
        var recipe = Recipes[itemName];
        var batches = GetRequiredBatches(amount, recipe.OutputAmount);
        foreach (var ingredient in recipe.Ingredients)
            BuildTree(ingredient.ItemName, batches * ingredient.Quantity, depth + 1, rows, path);
        path.Remove(itemName);
    }

    private static int GetRequiredBatches(int desiredAmount, int outputAmount)
    {
        var safeDesired = Math.Max(0, desiredAmount);
        var safeOutput = Math.Max(1, outputAmount);
        return (safeDesired + safeOutput - 1) / safeOutput;
    }

    private static void TrimHistory()
    {
        if (History.Count > 80) History.RemoveRange(80, History.Count - 80);
    }

    private static void Save()
    {
        Plugin.CollectionQuantities.Value = string.Join("|", Collected.OrderBy(x => x.Key).Select(x => Uri.EscapeDataString(x.Key) + "=" + x.Value.ToString(CultureInfo.InvariantCulture)));
        Plugin.CollectionHistory.Value = string.Join("|", History.Take(80).Select(x => x.Time.Ticks + ";" + Uri.EscapeDataString(x.ItemName) + ";" + x.Delta + ";" + x.NewTotal));
        Plugin.SaveState();
    }
}
