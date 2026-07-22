using UnityEngine;

namespace SangriaCompanion;

internal sealed class CompanionState
{
    internal CompanionPage Page { get; set; } = CompanionPage.Dashboard;
    internal bool PanelVisible { get; set; }
    internal Rect PanelRect { get; set; }
    internal Vector2 BossScroll { get; set; }
    internal string BossSearch { get; set; } = string.Empty;
    internal bool BossSearchFocused { get; set; }
    internal bool[] ExpandedActs { get; } = [true, true, true, true];
    internal string TrackedBossCommand { get; set; } = string.Empty;
    internal string RecipeSearch { get; set; } = string.Empty;
    internal bool RecipeSearchFocused { get; set; }
    internal float RecipeSearchScroll { get; set; }
    internal string ItemSearch { get; set; } = string.Empty;
    internal bool ItemSearchFocused { get; set; }
    internal float CollectionScroll { get; set; }
    internal bool ShowRecipeDetails { get; set; }
}
