using UnityEngine;

namespace SangriaCompanion;

internal sealed class CollectionModule
{
    private int _tab;

    internal void Draw(Rect area, CompanionState state, SCStyles styles)
    {
        CollectionService.Load();
        SCUI.SectionHeader(area, "COLETA E RECEITAS", styles);
        var top = area.y + 48f;
        var tabWidth = (area.width - 12f) / 3f;
        DrawTab(new Rect(area.x, top, tabWidth, 34f), 0, "PROJETO", styles);
        DrawTab(new Rect(area.x + tabWidth + 6f, top, tabWidth, 34f), 1, "ITENS", styles);
        DrawTab(new Rect(area.x + (tabWidth + 6f) * 2f, top, tabWidth, 34f), 2, "HISTÓRICO", styles);

        var body = new Rect(area.x, top + 44f, area.width, area.height - 92f);
        if (_tab == 0) DrawProject(body, state, styles);
        else if (_tab == 1) DrawItems(body, state, styles);
        else DrawHistory(body, styles);
    }

    private void DrawTab(Rect rect, int tab, string label, SCStyles styles)
    {
        SCUI.Panel(rect, _tab == tab ? SCTheme.PanelHover : SCTheme.PanelAlt, _tab == tab ? SCTheme.Gold : SCTheme.BorderSoft);
        if (SCUI.Button(rect, label, _tab == tab ? styles.Gold : styles.Muted)) _tab = tab;
    }

    private void DrawProject(Rect area, CompanionState state, SCStyles styles)
    {
        SCUI.Panel(area, SCTheme.Panel, SCTheme.BorderSoft);
        var padding = 14f;
        var y = area.y + padding;

        // Linha de pesquisa mais compacta.
        SCUI.Label(new Rect(area.x + padding, y, 132f, 30f), "BUSCAR RECEITA", styles.Gold);
        state.RecipeSearch = SCUI.SearchBox(
            new Rect(area.x + 142f, y, area.width - 322f, 32f),
            state.RecipeSearch,
            styles.Input,
            out var searchFocused,
            "Digite o item que deseja fabricar...");
        state.RecipeSearchFocused = searchFocused;

        if (SCUI.Button(new Rect(area.xMax - 170f, y, 72f, 32f), "LIMPAR", styles.Button, true))
        {
            state.RecipeSearch = string.Empty;
            state.RecipeSearchFocused = false;
        }
        if (SCUI.Button(new Rect(area.xMax - 92f, y, 78f, 32f), "RESETAR", styles.Button, true))
        {
            ResetProject(state);
        }

        y += 40f;
        var matches = CollectionService.SearchRecipes(state.RecipeSearch).Take(4).ToArray();
        if (!string.IsNullOrWhiteSpace(state.RecipeSearch))
        {
            if (matches.Length == 0)
            {
                SCUI.Label(new Rect(area.x + padding, y, area.width - padding * 2f, 28f),
                    "Nenhuma receita encontrada. A busca também verifica ingredientes e matérias-primas.", styles.Muted);
                y += 32f;
            }
            else
            {
                var resultWidth = (area.width - padding * 2f - ((matches.Length - 1) * 6f)) / matches.Length;
                for (var i = 0; i < matches.Length; i++)
                {
                    var resultRect = new Rect(area.x + padding + i * (resultWidth + 6f), y, resultWidth, 28f);
                    var selectedResult = matches[i].Name.Equals(CollectionService.SelectedRecipeName, StringComparison.OrdinalIgnoreCase);
                    SCUI.Panel(resultRect, selectedResult ? SCTheme.PanelHover : SCTheme.PanelAlt,
                        selectedResult ? SCTheme.Gold : SCTheme.BorderSoft);
                    if (SCUI.Button(resultRect, matches[i].Name, selectedResult ? styles.Gold : styles.Tiny))
                    {
                        CollectionService.SelectedRecipeName = matches[i].Name;
                        state.RecipeSearch = string.Empty;
                        state.RecipeSearchFocused = false;
                    }
                }
                y += 36f;
            }
        }

        var recipes = CollectionService.AllRecipes.ToArray();
        var selected = CollectionService.SelectedRecipeName;
        var recipeIndex = Array.FindIndex(recipes, x => x.Name.Equals(selected, StringComparison.OrdinalIgnoreCase));
        if (recipeIndex < 0) recipeIndex = 0;

        var selectorRect = new Rect(area.x + padding, y, area.width - padding * 2f, 34f);
        SCUI.Panel(selectorRect, SCTheme.PanelAlt, SCTheme.BorderSoft);
        if (SCUI.Button(new Rect(selectorRect.x + 4f, selectorRect.y + 4f, 28f, 26f), "‹", styles.Button, true))
        {
            recipeIndex = (recipeIndex - 1 + recipes.Length) % recipes.Length;
            CollectionService.SelectedRecipeName = recipes[recipeIndex].Name;
        }
        SCUI.Label(new Rect(selectorRect.x + 40f, selectorRect.y, selectorRect.width - 310f, selectorRect.height),
            CollectionService.SelectedRecipeName, styles.Gold);
        if (SCUI.Button(new Rect(selectorRect.xMax - 264f, selectorRect.y + 4f, 28f, 26f), "›", styles.Button, true))
        {
            recipeIndex = (recipeIndex + 1) % recipes.Length;
            CollectionService.SelectedRecipeName = recipes[recipeIndex].Name;
        }
        SCUI.Label(new Rect(selectorRect.xMax - 226f, selectorRect.y, 42f, selectorRect.height), "QTD.", styles.Tiny);
        if (SCUI.Button(new Rect(selectorRect.xMax - 184f, selectorRect.y + 4f, 28f, 26f), "−", styles.Button, true))
            CollectionService.DesiredQuantity--;
        SCUI.Label(new Rect(selectorRect.xMax - 152f, selectorRect.y, 34f, selectorRect.height),
            CollectionService.DesiredQuantity.ToString(), styles.Center);
        if (SCUI.Button(new Rect(selectorRect.xMax - 114f, selectorRect.y + 4f, 28f, 26f), "+", styles.Button, true))
            CollectionService.DesiredQuantity++;
        if (SCUI.Button(new Rect(selectorRect.xMax - 80f, selectorRect.y + 4f, 72f, 26f),
            Plugin.CollectionHudEnabled.Value ? "HUD ON" : "HUD OFF", styles.Button, true))
        {
            Plugin.CollectionHudEnabled.Value = !Plugin.CollectionHudEnabled.Value;
            Plugin.SaveState();
        }

        y += 40f;
        var recipe = CollectionService.GetRecipe(CollectionService.SelectedRecipeName);
        SCUI.Label(new Rect(area.x + padding, y, area.width - 300f, 22f),
            recipe == null ? string.Empty : "Estação: " + recipe.Station, styles.Muted);
        if (SCUI.Button(new Rect(area.xMax - 276f, y - 2f, 80f, 26f), "RELER", styles.Button, true))
            RecipeDiscoveryService.Reload();
        if (SCUI.Button(new Rect(area.xMax - 190f, y - 2f, 176f, 26f),
            state.ShowRecipeDetails ? "OCULTAR DETALHES" : "VER RECEITA COMPLETA", styles.Button, true))
        {
            state.ShowRecipeDetails = !state.ShowRecipeDetails;
        }

        y += 27f;
        SCUI.Label(new Rect(area.x + padding, y, area.width - padding * 2f, 18f),
            "Base do jogo: " + RecipeDiscoveryService.Status + " • total disponível: " + CollectionService.RecipeCount,
            RecipeDiscoveryService.IsLoaded ? styles.Green : styles.Tiny);
        y += 21f;
        var contentHeight = area.yMax - y - padding;
        if (contentHeight < 120f) return;

        var gap = 12f;
        var halfWidth = (area.width - padding * 2f - gap) / 2f;
        var left = new Rect(area.x + padding, y, halfWidth, contentHeight);
        var right = new Rect(left.xMax + gap, y, halfWidth, contentHeight);

        if (!state.ShowRecipeDetails)
        {
            DrawCompactRequirementPanel(left, "COLETAR", CollectionService.GetBaseRequirements(), styles, false);
            DrawCompactRequirementPanel(right, "FABRICAR ANTES", CollectionService.GetCraftRequirements(), styles, true);
        }
        else
        {
            DrawCompactRequirementPanel(left, "ITENS DIRETOS", CollectionService.GetDirectRequirements(), styles, true);
            DrawCompactRecipeTree(right, styles);
        }
    }

    private static void ResetProject(CompanionState state)
    {
        state.RecipeSearch = string.Empty;
        state.RecipeSearchFocused = false;
        state.ShowRecipeDetails = false;
        CollectionService.DesiredQuantity = 1;
        var first = CollectionService.AllRecipes.FirstOrDefault();
        if (first != null) CollectionService.SelectedRecipeName = first.Name;
        Plugin.CollectionHudEnabled.Value = false;
        Plugin.SaveState();
    }

    private static void DrawCompactRequirementPanel(
        Rect rect,
        string title,
        IReadOnlyList<MaterialRequirement> requirements,
        SCStyles styles,
        bool crafted)
    {
        SCUI.Card(rect, title, styles);
        var y = rect.y + 48f;
        var rowHeight = 31f;
        var maxRows = Math.Max(1, (int)((rect.height - 58f) / (rowHeight + 5f)));

        foreach (var item in requirements.Take(maxRows))
        {
            var row = new Rect(rect.x + 10f, y, rect.width - 20f, rowHeight);
            var completedBackground = new Color(0.05f, 0.18f, 0.11f, 0.78f);
            SCTheme.Fill(row, item.Complete ? completedBackground : SCTheme.PanelAlt);
            SCTheme.BorderRect(row, item.Complete ? SCTheme.Green : SCTheme.BorderSoft);
            var prefix = crafted && item.IsCrafted ? "◆ " : string.Empty;
            SCUI.Label(new Rect(row.x + 8f, row.y, row.width - 116f, row.height),
                prefix + item.ItemName, item.Complete ? styles.Green : styles.Label);
            SCUI.Label(new Rect(row.xMax - 106f, row.y, 64f, row.height),
                item.Collected + "/" + item.Required, item.Complete ? styles.Green : styles.Gold);
            SCUI.Label(new Rect(row.xMax - 40f, row.y, 34f, row.height),
                item.Complete ? "OK" : "−" + item.Missing, item.Complete ? styles.Green : styles.Blood);
            y += rowHeight + 5f;
        }

        if (requirements.Count == 0)
            SCUI.Label(new Rect(rect.x + 14f, y, rect.width - 28f, 42f), "Nenhum item pendente.", styles.Muted);
        else if (requirements.Count > maxRows)
            SCUI.Label(new Rect(rect.x + 12f, rect.yMax - 24f, rect.width - 24f, 18f),
                "+ " + (requirements.Count - maxRows) + " item(ns) na HUD/aba Itens", styles.Tiny);
    }

    private static void DrawCompactRecipeTree(Rect rect, SCStyles styles)
    {
        SCUI.Card(rect, "ÁRVORE DA RECEITA", styles);
        var y = rect.y + 48f;
        var maxRows = Math.Max(1, (int)((rect.height - 58f) / 25f));
        foreach (var row in CollectionService.GetRecipeTree().Take(maxRows))
        {
            var prefix = new string(' ', row.Depth * 2) + (row.Depth == 0 ? "◆ " : "└ ");
            SCUI.Label(new Rect(rect.x + 10f, y, rect.width - 20f, 22f),
                prefix + row.ItemName + " × " + row.Required, row.Crafted ? styles.Gold : styles.Label);
            y += 25f;
        }
    }

    private static void DrawItems(Rect area, CompanionState state, SCStyles styles)
    {
        SCUI.Panel(area, SCTheme.Panel, SCTheme.BorderSoft);
        state.ItemSearch = SCUI.SearchBox(new Rect(area.x + 14f, area.y + 14f, area.width - 160f, 32f), state.ItemSearch, styles.Input, out var focused, "Pesquisar item...");
        state.ItemSearchFocused = focused;
        if (SCUI.Button(new Rect(area.xMax - 136f, area.y + 14f, 122f, 32f), "ZERAR SESSÃO", styles.Button, true)) CollectionService.ResetSession();

        var items = CollectionService.AllKnownItems().Where(x => string.IsNullOrWhiteSpace(state.ItemSearch) || x.Contains(state.ItemSearch, StringComparison.OrdinalIgnoreCase)).ToArray();
        var listTop = area.y + 58f;
        var rowHeight = 38f;
        var viewHeight = area.height - 72f;
        var totalHeight = items.Length * rowHeight;
        var maxScroll = Math.Max(0f, totalHeight - viewHeight);
        if (new Rect(area.x, listTop, area.width, viewHeight).Contains(Event.current.mousePosition) && Event.current.type == EventType.ScrollWheel)
        {
            state.CollectionScroll = Mathf.Clamp(state.CollectionScroll + Event.current.delta.y * 24f, 0f, maxScroll);
            Event.current.Use();
        }
        state.CollectionScroll = Mathf.Clamp(state.CollectionScroll, 0f, maxScroll);
        var first = Math.Max(0, (int)(state.CollectionScroll / rowHeight));
        var visible = Math.Min(items.Length, first + (int)(viewHeight / rowHeight) + 2);
        for (var i = first; i < visible; i++)
        {
            var y = listTop + i * rowHeight - state.CollectionScroll;
            var row = new Rect(area.x + 14f, y, area.width - 28f, 34f);
            SCUI.Panel(row, SCTheme.PanelAlt, SCTheme.BorderSoft);
            SCUI.Label(new Rect(row.x + 9f, row.y, row.width - 250f, row.height), items[i], styles.Label);
            SCUI.Label(new Rect(row.xMax - 238f, row.y, 52f, row.height), CollectionService.GetCollected(items[i]).ToString(), styles.Gold);
            if (SCUI.Button(new Rect(row.xMax - 180f, row.y + 4f, 38f, 26f), "−1", styles.Button, true)) CollectionService.Add(items[i], -1);
            if (SCUI.Button(new Rect(row.xMax - 138f, row.y + 4f, 38f, 26f), "+1", styles.Button, true)) CollectionService.Add(items[i], 1);
            if (SCUI.Button(new Rect(row.xMax - 96f, row.y + 4f, 38f, 26f), "+5", styles.Button, true)) CollectionService.Add(items[i], 5);
            if (SCUI.Button(new Rect(row.xMax - 54f, row.y + 4f, 42f, 26f), "+10", styles.Button, true)) CollectionService.Add(items[i], 10);
        }
    }

    private static void DrawHistory(Rect area, SCStyles styles)
    {
        SCUI.Card(area, "HISTÓRICO DA SESSÃO", styles);
        var y = area.y + 54f;
        foreach (var entry in CollectionService.RecentHistory.Take(11))
        {
            var sign = entry.Delta > 0 ? "+" : string.Empty;
            SCUI.Label(new Rect(area.x + 14f, y, 58f, 24f), entry.Time.ToString("HH:mm"), styles.Tiny);
            SCUI.Label(new Rect(area.x + 72f, y, area.width - 190f, 24f), entry.ItemName, styles.Label);
            SCUI.Label(new Rect(area.xMax - 116f, y, 48f, 24f), sign + entry.Delta, entry.Delta > 0 ? styles.Green : styles.Blood);
            SCUI.Label(new Rect(area.xMax - 66f, y, 52f, 24f), "Total " + entry.NewTotal, styles.Gold);
            y += 28f;
        }
        if (CollectionService.RecentHistory.Count == 0)
            SCUI.Label(new Rect(area.x + 14f, y, area.width - 28f, 50f), "Use a aba Itens para registrar as quantidades coletadas.", styles.Muted);
    }
}
