using UnityEngine;

namespace SangriaCompanion;

internal sealed class BossModule
{
    internal void Draw(Rect area, CompanionState state, SCStyles styles)
    {
        SCUI.SectionHeader(area, "BOSSES", styles);
        var controlsY = area.y + 48f;

        SCUI.Label(new Rect(area.x, controlsY - 20f, 250f, 18f), "PESQUISAR PELO NOME", styles.Tiny);
        state.BossSearch = SCUI.SearchBox(
            new Rect(area.x, controlsY, 250f, 32f),
            state.BossSearch,
            styles.Input,
            out var searchFocused,
            "Digite o nome do boss...");
        state.BossSearchFocused = searchFocused;

        if (!string.IsNullOrEmpty(state.BossSearch) && SCUI.Button(new Rect(area.x + 218f, controlsY + 3f, 28f, 26f), "×", styles.Button, true))
        {
            state.BossSearch = string.Empty;
            GUI.FocusControl(string.Empty);
            state.BossSearchFocused = false;
        }

        if (SCUI.Toggle(new Rect(area.x + 262f, controlsY, 164f, 32f), "Favoritos", Plugin.ShowOnlyFavorites.Value, styles))
        {
            Plugin.ShowOnlyFavorites.Value = !Plugin.ShowOnlyFavorites.Value;
            Plugin.SaveState();
        }

        if (SCUI.Toggle(new Rect(area.x + 438f, controlsY, area.width - 438f, 32f), "Alertas", Plugin.BossAlertsEnabled.Value, styles))
        {
            Plugin.BossAlertsEnabled.Value = !Plugin.BossAlertsEnabled.Value;
            Plugin.SaveState();
        }

        var filtered = BossCatalog.All
            .Where(b => string.IsNullOrWhiteSpace(state.BossSearch) || b.Name.Contains(state.BossSearch, StringComparison.OrdinalIgnoreCase))
            .Where(b => !Plugin.ShowOnlyFavorites.Value || b.IsFavorite)
            .OrderByDescending(b => b.IsFavorite)
            .ThenBy(b => b.Level)
            .ThenBy(b => b.Name)
            .ToList();

        var summaryY = controlsY + 42f;
        var bridgeText = BossRespawnOverlay.BossRespawnApi.IsReady
            ? "consulta conectada"
            : "catálogo local carregado; aguardando conexão";
        SCUI.Label(new Rect(area.x, summaryY, area.width, 22f), $"{filtered.Count} bosses encontrados  •  {bridgeText}", styles.Dim);

        var listY = summaryY + 26f;
        var footerHeight = 48f;
        var viewRect = new Rect(area.x, listY, area.width, area.yMax - listY - footerHeight);
        var contentWidth = viewRect.width - 18f;
        var contentHeight = CalculateContentHeight(filtered, state, contentWidth);
        // GUI.BeginScrollView não é compatível com o IL2CPP desta versão do V Rising.
        // A rolagem abaixo é manual: alteramos o deslocamento com a roda do mouse e
        // desenhamos somente os elementos totalmente visíveis dentro da área da lista.
        HandleManualScroll(viewRect, contentHeight, state);
        var y = viewRect.y - state.BossScroll.y;

        if (!string.IsNullOrWhiteSpace(state.BossSearch) || Plugin.ShowOnlyFavorites.Value)
        {
            DrawCards(filtered, ref y, viewRect, contentWidth, state, styles);
        }
        else
        {
            for (var act = 1; act <= 4; act++)
            {
                var actBosses = filtered.Where(b => b.Act == act).ToList();
                DrawActHeader(act, actBosses.Count, ref y, viewRect, contentWidth, state, styles);
                if (state.ExpandedActs[act - 1]) DrawCards(actBosses, ref y, viewRect, contentWidth, state, styles);
                y += 8f;
            }
        }

        DrawManualScrollbar(viewRect, contentHeight, state.BossScroll.y);

        var footerY = area.yMax - 36f;
        SCUI.Label(new Rect(area.x, footerY, 130f, 28f), $"Avisar: {Plugin.BossAlertSeconds.Value}s", styles.Label);
        if (SCUI.Button(new Rect(area.x + 132f, footerY, 28f, 28f), "−", styles.Button, true)) ChangeAlertSeconds(-5);
        if (SCUI.Button(new Rect(area.x + 164f, footerY, 28f, 28f), "+", styles.Button, true)) ChangeAlertSeconds(5);

        SCUI.Label(new Rect(area.x + 218f, footerY, 145f, 28f), $"Na tela: {Plugin.BossAlertDuration.Value:0}s", styles.Label);
        if (SCUI.Button(new Rect(area.x + 354f, footerY, 28f, 28f), "−", styles.Button, true)) ChangeAlertDuration(-1f);
        if (SCUI.Button(new Rect(area.x + 386f, footerY, 28f, 28f), "+", styles.Button, true)) ChangeAlertDuration(1f);
    }

    private static float CalculateContentHeight(List<CompanionBoss> filtered, CompanionState state, float width)
    {
        if (!string.IsNullOrWhiteSpace(state.BossSearch) || Plugin.ShowOnlyFavorites.Value)
            return Mathf.CeilToInt(filtered.Count / 2f) * 108f + 8f;

        var height = 0f;
        for (var act = 1; act <= 4; act++)
        {
            height += 42f;
            if (state.ExpandedActs[act - 1])
                height += Mathf.CeilToInt(filtered.Count(b => b.Act == act) / 2f) * 108f;
            height += 8f;
        }
        return height;
    }

    private static void HandleManualScroll(Rect viewRect, float contentHeight, CompanionState state)
    {
        var maxScroll = Mathf.Max(0f, contentHeight - viewRect.height);
        var mouse = Event.current.mousePosition;

        if (viewRect.Contains(mouse) && Event.current.type == EventType.ScrollWheel)
        {
            state.BossScroll = new Vector2(0f, Mathf.Clamp(state.BossScroll.y + Event.current.delta.y * 32f, 0f, maxScroll));
            Event.current.Use();
        }
        else if (state.BossScroll.y > maxScroll)
        {
            state.BossScroll = new Vector2(0f, maxScroll);
        }
    }

    private static void DrawManualScrollbar(Rect viewRect, float contentHeight, float scrollY)
    {
        if (contentHeight <= viewRect.height) return;

        var track = new Rect(viewRect.xMax - 5f, viewRect.y, 4f, viewRect.height);
        SCTheme.Fill(track, SCTheme.PanelAlt);

        var ratio = viewRect.height / contentHeight;
        var thumbHeight = Mathf.Max(30f, viewRect.height * ratio);
        var maxScroll = contentHeight - viewRect.height;
        var travel = viewRect.height - thumbHeight;
        var thumbY = viewRect.y + (maxScroll <= 0f ? 0f : scrollY / maxScroll * travel);
        SCTheme.Fill(new Rect(track.x, thumbY, track.width, thumbHeight), SCTheme.GoldSoft);
    }

    private static bool IsFullyVisible(Rect rect, Rect viewport)
    {
        return rect.y >= viewport.y && rect.yMax <= viewport.yMax;
    }

    private static void DrawActHeader(int act, int count, ref float y, Rect viewport, float width, CompanionState state, SCStyles styles)
    {
        var rect = new Rect(viewport.x, y, width, 36f);
        if (IsFullyVisible(rect, viewport))
        {
            SCUI.Panel(rect, SCTheme.PanelAlt, SCTheme.BorderSoft);
            var opened = state.ExpandedActs[act - 1];
            if (SCUI.Button(rect, $"{(opened ? "−" : "+")}  {BossCatalog.ActTitle(act)}   ({count})", opened ? styles.Gold : styles.Label))
                state.ExpandedActs[act - 1] = !opened;
        }
        y += 42f;
    }

    private static void DrawCards(List<CompanionBoss> bosses, ref float y, Rect viewport, float width, CompanionState state, SCStyles styles)
    {
        const int columns = 2;
        const float gap = 10f;
        var cardWidth = (width - gap) / columns;
        for (var i = 0; i < bosses.Count; i++)
        {
            var row = i / columns;
            var column = i % columns;
            var rect = new Rect(viewport.x + column * (cardWidth + gap), y + row * 108f, cardWidth, 98f);
            if (IsFullyVisible(rect, viewport)) DrawCard(bosses[i], rect, state, styles);
        }
        y += Mathf.CeilToInt(bosses.Count / 2f) * 108f;
    }

    private static void DrawCard(CompanionBoss boss, Rect rect, CompanionState state, SCStyles styles)
    {
        var hovered = rect.Contains(Event.current.mousePosition);
        var border = boss.IsFavorite ? SCTheme.Gold : hovered ? SCTheme.GoldSoft : SCTheme.BorderSoft;
        SCUI.Panel(rect, hovered ? SCTheme.PanelHover : SCTheme.Panel, border);

        var statusColor = boss.Status == CompanionBossStatus.Alive ? SCTheme.Green : boss.Status == CompanionBossStatus.Dead ? SCTheme.Blood : SCTheme.Gold;
        SCTheme.Fill(new Rect(rect.x, rect.y, 4f, rect.height), statusColor);

        SCUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 126f, 24f), boss.Name, boss.IsFavorite ? styles.Gold : styles.Label);
        SCUI.Label(new Rect(rect.x + 12f, rect.y + 30f, 82f, 20f), $"Nível {boss.Level}", styles.Muted);

        var statusText = boss.Status switch
        {
            CompanionBossStatus.Alive => "VIVO",
            CompanionBossStatus.Dead => $"MORTO  {SCUI.FormatTime(boss.RemainingSeconds)}",
            CompanionBossStatus.Querying => "CONSULTANDO",
            CompanionBossStatus.NotFound => "NÃO ENCONTRADO",
            _ => "AGUARDANDO"
        };
        var statusStyle = boss.Status == CompanionBossStatus.Alive ? styles.Green : boss.Status == CompanionBossStatus.Dead ? styles.Blood : styles.Gold;
        SCUI.Label(new Rect(rect.xMax - 122f, rect.y + 8f, 110f, 24f), statusText, statusStyle);

        if (SCUI.Button(new Rect(rect.x + 12f, rect.y + 62f, 86f, 25f), boss.IsFavorite ? "★ Favorito" : "☆ Favoritar", styles.Button, true))
        {
            boss.IsFavorite = !boss.IsFavorite;
            BossRespawnOverlay.BossRespawnApi.SetPinned(boss.CommandName, boss.IsFavorite);
            BossPreferenceService.Save();
        }

        if (SCUI.Button(new Rect(rect.x + 104f, rect.y + 62f, 78f, 25f), boss.AlertEnabled ? "Alerta: SIM" : "Alerta", styles.Button, true))
        {
            boss.AlertEnabled = !boss.AlertEnabled;
            BossPreferenceService.Save();
        }

        if (SCUI.Button(new Rect(rect.xMax - 82f, rect.y + 62f, 70f, 25f), "Rastrear", styles.Button, true))
        {
            state.TrackedBossCommand = boss.CommandName;
            Plugin.TrackedBossCommand.Value = boss.CommandName;
            Plugin.SaveState();
            state.Page = CompanionPage.Tracker;
            BossRespawnOverlay.BossRespawnApi.Refresh(boss.CommandName);
            Plugin.Instance.Log.LogInfo($"Boss selecionado no Rastreador: {boss.Name} ({boss.CommandName})");
        }
    }

    private static void ChangeAlertSeconds(int delta)
    {
        Plugin.BossAlertSeconds.Value = Mathf.Clamp(Plugin.BossAlertSeconds.Value + delta, 5, 600);
        Plugin.SaveState();
    }

    private static void ChangeAlertDuration(float delta)
    {
        Plugin.BossAlertDuration.Value = Mathf.Clamp(Plugin.BossAlertDuration.Value + delta, 1f, 30f);
        Plugin.SaveState();
    }
}
