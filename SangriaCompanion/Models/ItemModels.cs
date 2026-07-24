namespace SangriaCompanion;

internal sealed class RecipeIngredient
{
    internal RecipeIngredient(string itemName, int quantity)
    {
        ItemName = itemName;
        Quantity = quantity;
    }

    internal string ItemName { get; }
    internal int Quantity { get; }
}

internal sealed class ItemRecipe
{
    internal ItemRecipe(string name, string station, params RecipeIngredient[] ingredients)
        : this(name, station, 1, ingredients)
    {
    }

    internal ItemRecipe(string name, string station, int outputAmount, params RecipeIngredient[] ingredients)
    {
        Name = name;
        Station = station;
        OutputAmount = Math.Max(1, outputAmount);
        Ingredients = ingredients;
    }

    internal string Name { get; }
    internal string Station { get; }
    internal int OutputAmount { get; }
    internal IReadOnlyList<RecipeIngredient> Ingredients { get; }
}

internal readonly record struct CollectionHistoryEntry(DateTime Time, string ItemName, int Delta, int NewTotal);
internal readonly record struct MaterialRequirement(string ItemName, int Required, int Collected, bool IsCrafted)
{
    internal int Missing => Math.Max(0, Required - Collected);
    internal bool Complete => Collected >= Required;
}

internal enum SessionDropKind
{
    Pet,
    Soul
}

internal sealed class SessionDropEntry
{
    internal string ItemName { get; init; } = string.Empty;
    internal string DisplayName { get; init; } = string.Empty;
    internal SessionDropKind Kind { get; init; }
    internal int Act { get; init; }
    internal int Quantity { get; set; }
    internal DateTime FirstSeen { get; init; }
    internal DateTime LastSeen { get; set; }
}
