using UnityEngine;

namespace SangriaCompanion;

internal sealed class SessionDropsModule
{
    private const float GroupHeaderHeight = 36f;
    private const float RowHeight = 38f;
    private const float EmptyGroupHeight = 30f;
    private const float GroupGap = 8f;

    internal void Draw(Rect area, CompanionState state, SCStyles styles)
    {
        SCUI.SectionHeader(area, "PETS E ALMAS • SESSÃO", styles);

        var metricY = area.y + 48f;
        const float gap = 10f;
        var metricWidth = (area.width - gap * 2f) / 3f;
        SCUI.MetricCard(new Rect(area.x, metricY, metricWidth, 94f), "PETS", SessionDropService.PetQuantity.ToString(), "obtidos nesta sessão", SCTheme.Green, styles);
        SCUI.MetricCard(new Rect(area.x + metricWidth + gap, metricY, metricWidth, 94f), "ALMAS", SessionDropService.SoulQuantity.ToString(), "obtidas nesta sessão", SCTheme.Purple, styles);
        SCUI.MetricCard(new Rect(area.x + (metricWidth + gap) * 2f, metricY, metricWidth, 94f), "TEMPO", FormatElapsed(SessionDropService.Elapsed), SessionDropService.UniqueCount + " tipo(s) diferente(s)", SCTheme.Gold, styles);

        var statusY = metricY + 106f;
        var statusRect = new Rect(area.x, statusY, area.width, 48f);
        SCUI.Panel(statusRect, SCTheme.PanelAlt, InventorySyncService.IsReady ? SCTheme.BorderSoft : SCTheme.Blood);
        var status = InventorySyncService.IsReady ? InventorySyncService.Status : "Aguardando leitura do inventário";
        SCUI.Label(new Rect(statusRect.x + 12f, statusRect.y + 3f, statusRect.width - 24f, 20f), status, InventorySyncService.IsReady ? styles.Label : styles.Blood);
        SCUI.Label(new Rect(statusRect.x + 12f, statusRect.y + 21f, statusRect.width - 24f, 18f), "Drops são agrupados pelo boss/ato reconhecido; itens não mapeados ficam separados.", styles.Tiny);

        var listY = statusRect.yMax + 10f;
        var footerHeight = 42f;
        var listRect = new Rect(area.x, listY, area.width, area.yMax - listY - footerHeight);
        DrawList(listRect, state, styles);

        var footerY = area.yMax - 32f;
        var summary = "Total " + SessionDropService.TotalQuantity +
                      "  •  A1 " + SessionDropService.QuantityForAct(1) +
                      "  •  A2 " + SessionDropService.QuantityForAct(2) +
                      "  •  A3 " + SessionDropService.QuantityForAct(3) +
                      "  •  A4 " + SessionDropService.QuantityForAct(4);
        SCUI.Label(new Rect(area.x, footerY, area.width - 270f, 28f), summary, styles.Gold);
        if (SCUI.Button(new Rect(area.xMax - 258f, footerY, 118f, 28f), "ATUALIZAR", styles.Button, true))
            InventorySyncService.ForceRefresh();
        if (SCUI.Button(new Rect(area.xMax - 132f, footerY, 132f, 28f), "LIMPAR SESSÃO", styles.Button, true))
        {
            SessionDropService.ResetUsingCurrentInventory(InventorySyncService.Snapshot);
            state.SessionDropScroll = 0f;
        }
    }

    private static void DrawList(Rect rect, CompanionState state, SCStyles styles)
    {
        SCUI.Card(rect, "ITENS OBTIDOS POR ATO", styles);
        var entries = SessionDropService.Entries;
        var viewport = new Rect(rect.x + 12f, rect.y + 50f, rect.width - 24f, rect.height - 60f);
        if (entries.Count == 0)
        {
            var message = SessionDropService.BaselineReady
                ? "Nenhum pet ou alma foi detectado nesta sessão."
                : "Estabelecendo a linha de base do inventário...";
            SCUI.Label(new Rect(viewport.x + 8f, viewport.y + 20f, viewport.width - 16f, 52f), message, styles.Muted);
            return;
        }

        var groups = BuildVisibleGroups();
        var contentHeight = CalculateContentHeight(groups, state);
        var maxScroll = Mathf.Max(0f, contentHeight - viewport.height);
        if (viewport.Contains(Event.current.mousePosition) && Event.current.type == EventType.ScrollWheel)
        {
            state.SessionDropScroll = Mathf.Clamp(state.SessionDropScroll + Event.current.delta.y * 28f, 0f, maxScroll);
            Event.current.Use();
        }
        else
        {
            state.SessionDropScroll = Mathf.Clamp(state.SessionDropScroll, 0f, maxScroll);
        }

        var y = viewport.y - state.SessionDropScroll;
        foreach (var group in groups)
        {
            DrawGroupHeader(viewport, ref y, group.Act, group.Entries, state, styles);
            if (state.ExpandedSessionDropActs[group.Act])
            {
                if (group.Entries.Count == 0)
                    DrawEmptyGroup(viewport, ref y, styles);
                else
                    DrawRows(viewport, ref y, group.Entries, styles);
            }
            y += GroupGap;
        }

        DrawScrollbar(viewport, contentHeight, maxScroll, state.SessionDropScroll);
    }

    private static IReadOnlyList<ActGroup> BuildVisibleGroups()
    {
        var result = new List<ActGroup>();
        for (var act = 1; act <= 4; act++)
        {
            var entries = SessionDropService.EntriesForAct(act);
            if (entries.Count > 0) result.Add(new ActGroup(act, entries));
        }

        var unmapped = SessionDropService.EntriesForAct(0);
        if (unmapped.Count > 0) result.Add(new ActGroup(0, unmapped));
        return result;
    }

    private static float CalculateContentHeight(IReadOnlyList<ActGroup> groups, CompanionState state)
    {
        var height = 0f;
        foreach (var group in groups)
        {
            height += GroupHeaderHeight;
            if (state.ExpandedSessionDropActs[group.Act])
                height += group.Entries.Count == 0 ? EmptyGroupHeight : group.Entries.Count * RowHeight;
            height += GroupGap;
        }
        return height;
    }

    private static void DrawGroupHeader(Rect viewport, ref float y, int act, IReadOnlyList<SessionDropEntry> entries, CompanionState state, SCStyles styles)
    {
        var rect = new Rect(viewport.x, y, viewport.width - 6f, GroupHeaderHeight - 4f);
        if (rect.yMax >= viewport.y && rect.y <= viewport.yMax)
        {
            var expanded = state.ExpandedSessionDropActs[act];
            var total = entries.Sum(x => x.Quantity);
            var title = (expanded ? "−  " : "+  ") + SessionDropService.ActTitle(act) +
                        "  •  " + entries.Count + " tipo(s)" +
                        "  •  " + total + " drop(s)";
            var style = act == 0 ? styles.Muted : styles.Gold;
            if (SCUI.Button(rect, title, style, true))
                state.ExpandedSessionDropActs[act] = !expanded;
        }
        y += GroupHeaderHeight;
    }

    private static void DrawEmptyGroup(Rect viewport, ref float y, SCStyles styles)
    {
        var rect = new Rect(viewport.x + 8f, y, viewport.width - 20f, EmptyGroupHeight - 4f);
        if (rect.yMax >= viewport.y && rect.y <= viewport.yMax)
            SCUI.Label(rect, "Nenhum pet ou alma registrado neste ato.", styles.Muted);
        y += EmptyGroupHeight;
    }

    private static void DrawRows(Rect viewport, ref float y, IReadOnlyList<SessionDropEntry> entries, SCStyles styles)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var row = new Rect(viewport.x + 8f, y, viewport.width - 20f, RowHeight - 4f);
            if (row.yMax >= viewport.y && row.y <= viewport.yMax)
            {
                SCTheme.Fill(row, i % 2 == 0 ? SCTheme.Panel : SCTheme.PanelAlt);
                var accent = entry.Kind == SessionDropKind.Soul ? SCTheme.Purple : SCTheme.Green;
                SCTheme.Fill(new Rect(row.x, row.y, 4f, row.height), accent);
                SCUI.Label(new Rect(row.x + 10f, row.y + 2f, 58f, 20f), entry.Kind == SessionDropKind.Soul ? "ALMA" : "PET", entry.Kind == SessionDropKind.Soul ? styles.Gold : styles.Green);
                SCUI.Label(new Rect(row.x + 68f, row.y + 2f, row.width - 206f, 20f), entry.DisplayName, styles.Label);
                SCUI.Label(new Rect(row.xMax - 128f, row.y + 2f, 54f, 20f), "+" + entry.Quantity, styles.Gold);
                SCUI.Label(new Rect(row.xMax - 72f, row.y + 2f, 66f, 20f), entry.LastSeen.ToString("HH:mm"), styles.Muted);
                SCUI.Label(new Rect(row.x + 68f, row.y + 19f, row.width - 144f, 13f), "Primeiro: " + entry.FirstSeen.ToString("HH:mm:ss") + "  •  Último: " + entry.LastSeen.ToString("HH:mm:ss"), styles.Tiny);
            }
            y += RowHeight;
        }
    }

    private static void DrawScrollbar(Rect viewport, float contentHeight, float maxScroll, float scroll)
    {
        if (contentHeight <= viewport.height) return;

        var track = new Rect(viewport.xMax - 4f, viewport.y, 4f, viewport.height);
        SCTheme.Fill(track, SCTheme.PanelAlt);
        var ratio = viewport.height / contentHeight;
        var thumbHeight = Mathf.Max(26f, viewport.height * ratio);
        var travel = viewport.height - thumbHeight;
        var thumbY = viewport.y + (maxScroll <= 0f ? 0f : scroll / maxScroll * travel);
        SCTheme.Fill(new Rect(track.x, thumbY, track.width, thumbHeight), SCTheme.GoldSoft);
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1d) return $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}";
        return $"{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }

    private sealed record ActGroup(int Act, IReadOnlyList<SessionDropEntry> Entries);
}
