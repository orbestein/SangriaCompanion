using UnityEngine;

namespace SangriaCompanion;

internal sealed class DashboardModule
{
    internal void Draw(Rect area, SCStyles styles)
    {
        SCUI.SectionHeader(area, "DASHBOARD", styles);
        BossCatalog.Refresh();
        var bosses = BossCatalog.All;
        var alive = bosses.Count(x => x.Status == CompanionBossStatus.Alive);
        var dead = bosses.Count(x => x.Status == CompanionBossStatus.Dead);
        var favoriteCount = bosses.Count(x => x.IsFavorite);
        var confirmed = alive + dead;
        var next = EventScheduleService.GetNextOccurrence();
        var eventsActive = EventScheduleService.GetUpcoming(5).Count(x => x.IsActive);

        var metrics = new List<(string Title, string Value, string Footer, Color Accent)>();
        if (Plugin.DashboardAlive.Value) metrics.Add(("BOSSES VIVOS", confirmed == 0 ? "--" : alive.ToString(), $"{confirmed}/{bosses.Count} consultados", SCTheme.Green));
        if (Plugin.DashboardDead.Value) metrics.Add(("BOSSES MORTOS", confirmed == 0 ? "--" : dead.ToString(), $"{confirmed}/{bosses.Count} consultados", SCTheme.Blood));
        if (Plugin.DashboardFavorites.Value) metrics.Add(("FAVORITOS", favoriteCount.ToString(), "fixados no Dashboard", SCTheme.Gold));
        if (Plugin.DashboardEventsActive.Value) metrics.Add(("EVENTOS ATIVOS", eventsActive.ToString(), EventScheduleService.IsSynchronized ? "hora oficial" : "sincronizando", SCTheme.Purple));

        var y = area.y + 58f;
        if (metrics.Count > 0)
        {
            const float gap = 10f;
            var columns = Mathf.Min(4, metrics.Count);
            var width = (area.width - gap * (columns - 1)) / columns;
            for (var i = 0; i < metrics.Count; i++)
            {
                var column = i % columns;
                var row = i / columns;
                var item = metrics[i];
                Metric(new Rect(area.x + column * (width + gap), y + row * 122f, width, 112f), item.Title, item.Value, item.Footer, item.Accent, styles);
            }
            y += Mathf.CeilToInt(metrics.Count / (float)columns) * 122f + 6f;
        }

        var showNext = Plugin.DashboardNextEvent.Value;
        var showFavorites = Plugin.DashboardFavoriteBosses.Value;
        if (!showNext && !showFavorites)
        {
            SCUI.Card(new Rect(area.x, y, area.width, 110f), "DASHBOARD PERSONALIZADO", styles);
            SCUI.Label(new Rect(area.x + 18f, y + 54f, area.width - 36f, 42f), "Escolha nas Configurações quais caixas devem aparecer aqui.", styles.Muted);
            return;
        }

        const float panelGap = 12f;
        var panelWidth = showNext && showFavorites ? (area.width - panelGap) / 2f : area.width;
        if (showNext)
        {
            DrawNextEvent(new Rect(area.x, y, panelWidth, 210f), next, styles);
        }
        if (showFavorites)
        {
            var x = showNext ? area.x + panelWidth + panelGap : area.x;
            DrawFavoriteBosses(new Rect(x, y, panelWidth, 210f), bosses, favoriteCount, styles);
        }
    }

    private static void DrawNextEvent(Rect rect, CompanionEventOccurrence next, SCStyles styles)
    {
        SCUI.Card(rect, next.IsActive ? "EVENTO ATIVO" : "PRÓXIMO EVENTO", styles);
        SCUI.Label(new Rect(rect.x + 18f, rect.y + 54f, rect.width - 36f, 42f), next.Name, styles.Gold);
        var detail = next.IsActive
            ? $"Termina em {EventScheduleService.FormatCountdown(next.UntilEnd)}"
            : $"{next.Start.ToString(@"hh\:mm")} • em {EventScheduleService.FormatCountdown(next.UntilStart)}";
        SCUI.Label(new Rect(rect.x + 18f, rect.y + 96f, rect.width - 36f, 34f), detail, styles.Large);
        SCUI.Label(new Rect(rect.x + 18f, rect.y + 142f, rect.width - 36f, 52f), next.Description, styles.Muted);
    }

    private static void DrawFavoriteBosses(Rect rect, IReadOnlyList<CompanionBoss> bosses, int favoriteCount, SCStyles styles)
    {
        SCUI.Card(rect, "BOSSES FAVORITOS", styles);
        var y = rect.y + 52f;
        foreach (var boss in bosses.Where(x => x.IsFavorite && x.TryGetConfirmedDisplay(out _, out _)).Take(5))
        {
            boss.TryGetConfirmedDisplay(out var confirmedStatus, out var confirmedRemaining);
            var status = confirmedStatus == CompanionBossStatus.Alive
                ? "VIVO"
                : "MORTO " + SCUI.FormatTime(confirmedRemaining);
            SCUI.Label(new Rect(rect.x + 18f, y, rect.width - 130f, 25f), $"★ {boss.Name} (Nv. {boss.Level})", styles.Label);
            SCUI.Label(new Rect(rect.xMax - 112f, y, 94f, 25f), status, confirmedStatus == CompanionBossStatus.Alive ? styles.Green : styles.Blood);
            y += 29f;
        }
        if (favoriteCount == 0)
            SCUI.Label(new Rect(rect.x + 18f, y, rect.width - 36f, 40f), "Marque bosses como favoritos para exibi-los aqui.", styles.Muted);
    }

    private static void Metric(Rect rect, string title, string value, string footer, Color accent, SCStyles styles)
    {
        SCUI.Panel(rect, SCTheme.Panel, SCTheme.BorderSoft);
        SCTheme.Fill(new Rect(rect.x, rect.y, 4f, rect.height), accent);
        SCUI.Label(new Rect(rect.x + 14f, rect.y + 14f, rect.width - 20f, 20f), title, styles.Muted);
        var old = styles.Metric.normal.textColor;
        styles.Metric.normal.textColor = accent;
        SCUI.Label(new Rect(rect.x + 14f, rect.y + 42f, rect.width - 20f, 38f), value, styles.Metric);
        styles.Metric.normal.textColor = old;
        SCUI.Label(new Rect(rect.x + 14f, rect.y + 84f, rect.width - 20f, 18f), footer, styles.Tiny);
    }
}
