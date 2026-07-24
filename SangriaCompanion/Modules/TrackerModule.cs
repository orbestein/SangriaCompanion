using BossRespawnOverlay;
using UnityEngine;

namespace SangriaCompanion;

internal sealed class TrackerModule
{
    internal void Draw(Rect area, CompanionState state, SCStyles styles)
    {
        SCUI.SectionHeader(area, "RASTREADOR", styles);

        if (string.IsNullOrWhiteSpace(state.TrackedBossCommand) && !string.IsNullOrWhiteSpace(Plugin.TrackedBossCommand.Value))
            state.TrackedBossCommand = Plugin.TrackedBossCommand.Value;

        var selected = BossCatalog.All.FirstOrDefault(x =>
            x.CommandName.Equals(state.TrackedBossCommand, StringComparison.OrdinalIgnoreCase) ||
            x.Name.Equals(state.TrackedBossCommand, StringComparison.OrdinalIgnoreCase));

        if (selected == null)
        {
            DrawEmptyState(new Rect(area.x, area.y + 50f, area.width, 250f), styles);
            return;
        }

        if (selected.IsMobile)
            DrawMobileTracker(area, state, selected, styles);
        else
            DrawFixedTracker(area, state, selected, styles);
    }

    private static void DrawEmptyState(Rect card, SCStyles styles)
    {
        SCUI.Card(card, "BOSS ACOMPANHADO", styles);
        SCUI.Label(new Rect(card.x + 18f, card.y + 56f, card.width - 36f, 28f), "Nenhum boss selecionado", styles.Gold);
        SCUI.Label(new Rect(card.x + 18f, card.y + 94f, card.width - 36f, 52f), "Abra a aba Bosses e clique em Rastrear. O Companion acompanhará o estado e o tempo de respawn.", styles.Muted);
        SCUI.Label(new Rect(card.x + 18f, card.y + 164f, card.width - 36f, 42f), "Bosses móveis exibem a rota de patrulha que o próprio jogo disponibilizar ao cliente. Nenhuma rota é aprendida ou salva.", styles.Dim);
    }

    private static void DrawMobileTracker(Rect area, CompanionState state, CompanionBoss selected, SCStyles styles)
    {
        var card = new Rect(area.x, area.y + 50f, area.width, 194f);
        SCUI.Card(card, "BOSS ACOMPANHADO", styles);
        DrawBossHeader(card, state, selected, styles);

        var statusText = StatusText(selected);
        var statusStyle = StatusStyle(selected, styles);
        SCUI.Label(new Rect(card.x + 18f, card.y + 82f, card.width - 36f, 24f), "Estado: " + statusText, statusStyle);

        var mainInfo = selected.Status == CompanionBossStatus.Dead
            ? $"Tempo restante: {SCUI.FormatTime(selected.RemainingSeconds)}"
            : selected.Status == CompanionBossStatus.Alive
                ? "Tempo restante: disponível agora"
                : "Tempo restante: aguardando consulta";
        SCUI.Label(new Rect(card.x + 18f, card.y + 108f, card.width - 36f, 22f), mainInfo, styles.Label);
        SCUI.Label(new Rect(card.x + 18f, card.y + 132f, card.width - 36f, 20f), $"Última atualização: {BossNotificationService.LastUpdateText(selected.CommandName)}", styles.Muted);

        var tracking = MobileBossTrackerService.Snapshot;
        var distanceText = tracking.IsAvailable ? Mathf.RoundToInt(tracking.DistanceMeters) + " m" : "Indisponível";
        var availableStyle = tracking.IsAvailable ? styles.Label : styles.Muted;
        var columnWidth = (card.width - 36f) / 3f;
        SCUI.Label(new Rect(card.x + 18f, card.y + 158f, columnWidth, 22f), "Distância: " + distanceText, availableStyle);
        SCUI.Label(new Rect(card.x + 18f + columnWidth, card.y + 158f, columnWidth, 22f), "Direção: " + tracking.Direction, availableStyle);
        SCUI.Label(new Rect(card.x + 18f + columnWidth * 2f, card.y + 158f, columnWidth, 22f), "Movimento: " + tracking.Movement, tracking.IsAvailable ? styles.Green : styles.Muted);

        var configCard = new Rect(area.x, card.yMax + 10f, area.width, 62f);
        DrawCompactHudControls(configCard, styles);

        var routeY = configCard.yMax + 10f;
        var routeHeight = Mathf.Max(112f, area.yMax - routeY);
        DrawRouteCard(new Rect(area.x, routeY, area.width, routeHeight), selected, styles);
    }

    private static void DrawFixedTracker(Rect area, CompanionState state, CompanionBoss selected, SCStyles styles)
    {
        var card = new Rect(area.x, area.y + 50f, area.width, 250f);
        SCUI.Card(card, "BOSS ACOMPANHADO", styles);
        DrawBossHeader(card, state, selected, styles);

        SCUI.Label(new Rect(card.x + 18f, card.y + 88f, card.width - 36f, 28f), "Estado: " + StatusText(selected), StatusStyle(selected, styles));
        var mainInfo = selected.Status == CompanionBossStatus.Dead
            ? $"Tempo restante: {SCUI.FormatTime(selected.RemainingSeconds)}"
            : selected.Status == CompanionBossStatus.Alive
                ? "Tempo restante: disponível agora"
                : "Tempo restante: aguardando consulta";
        SCUI.Label(new Rect(card.x + 18f, card.y + 122f, card.width - 36f, 26f), mainInfo, styles.Label);
        SCUI.Label(new Rect(card.x + 18f, card.y + 152f, card.width - 36f, 22f), $"Última atualização: {BossNotificationService.LastUpdateText(selected.CommandName)}", styles.Muted);
        SCUI.Label(new Rect(card.x + 18f, card.y + 180f, 190f, 22f), $"Favorito: {(selected.IsFavorite ? "SIM" : "NÃO")}", selected.IsFavorite ? styles.Gold : styles.Muted);
        SCUI.Label(new Rect(card.x + 212f, card.y + 180f, 210f, 22f), $"Aviso na tela: {(selected.AlertEnabled ? "SIM" : "NÃO")}", selected.AlertEnabled ? styles.Green : styles.Muted);

        var configCard = new Rect(area.x, card.yMax + 12f, area.width, 62f);
        DrawCompactHudControls(configCard, styles);

        var historyCard = new Rect(area.x, configCard.yMax + 12f, area.width, Mathf.Max(82f, area.yMax - configCard.yMax - 12f));
        SCUI.Card(historyCard, "HISTÓRICO RECENTE", styles);
        var history = BossNotificationService.HistoryFor(selected.CommandName, selected.Name);
        if (history.Count == 0)
        {
            SCUI.Label(new Rect(historyCard.x + 18f, historyCard.y + 54f, historyCard.width - 36f, 24f), "Nenhuma alteração registrada nesta sessão.", styles.Muted);
            return;
        }

        var y = historyCard.y + 50f;
        foreach (var item in history.Take(4))
        {
            if (y + 22f > historyCard.yMax - 8f) break;
            SCUI.Label(new Rect(historyCard.x + 18f, y, 58f, 20f), item.CreatedAt.ToString("HH:mm"), styles.Tiny);
            SCUI.Label(new Rect(historyCard.x + 72f, y, historyCard.width - 90f, 20f), item.Message, styles.Label);
            y += 25f;
        }
    }

    private static void DrawBossHeader(Rect card, CompanionState state, CompanionBoss selected, SCStyles styles)
    {
        var buttonsWidth = 348f;
        SCUI.Label(new Rect(card.x + 18f, card.y + 52f, Mathf.Max(120f, card.width - buttonsWidth - 28f), 28f), $"{selected.Name} • Ato {selected.Act} • Nível {selected.Level}", styles.Gold);

        var x = card.xMax - buttonsWidth;
        if (SCUI.Button(new Rect(x, card.y + 48f, 76f, 30f), "Atualizar", styles.Button, true))
            BossRespawnApi.Refresh(selected.CommandName);
        x += 82f;
        if (SCUI.Button(new Rect(x, card.y + 48f, 92f, 30f), selected.IsFavorite ? "★ Favorito" : "☆ Favoritar", styles.Button, true))
        {
            selected.IsFavorite = !selected.IsFavorite;
            BossRespawnApi.SetPinned(selected.CommandName, selected.IsFavorite);
            BossPreferenceService.Save();
        }
        x += 98f;
        if (SCUI.Button(new Rect(x, card.y + 48f, 92f, 30f), selected.AlertEnabled ? "Aviso: SIM" : "Aviso: NÃO", styles.Button, true))
        {
            selected.AlertEnabled = !selected.AlertEnabled;
            BossPreferenceService.Save();
        }
        x += 98f;
        if (SCUI.Button(new Rect(x, card.y + 48f, 68f, 30f), "Limpar", styles.Button, true))
        {
            state.TrackedBossCommand = string.Empty;
            Plugin.TrackedBossCommand.Value = string.Empty;
            Plugin.SaveState();
        }
    }

    private static void DrawCompactHudControls(Rect card, SCStyles styles)
    {
        SCUI.Panel(card, SCTheme.Panel, SCTheme.Border);
        SCUI.Label(new Rect(card.x + 14f, card.y + 5f, 128f, 24f), "HUD COMPACTA", styles.Gold);
        var hudLabel = Plugin.TrackerHudEnabled.Value ? "HUD: LIGADA" : "HUD: DESLIGADA";
        if (SCUI.Button(new Rect(card.x + 146f, card.y + 4f, 124f, 26f), hudLabel, styles.Button, true))
        {
            Plugin.TrackerHudEnabled.Value = !Plugin.TrackerHudEnabled.Value;
            Plugin.SaveState();
        }

        var y = card.y + 34f;
        SCUI.Label(new Rect(card.x + 14f, y, 124f, 22f), $"Atualizar: {Plugin.TrackerRefreshInterval.Value:0}s", styles.Label);
        if (SCUI.Button(new Rect(card.x + 142f, y - 2f, 28f, 26f), "−", styles.Button, true)) ChangeRefresh(-5f);
        if (SCUI.Button(new Rect(card.x + 176f, y - 2f, 28f, 26f), "+", styles.Button, true)) ChangeRefresh(5f);
        SCUI.Label(new Rect(card.x + 220f, y, card.width - 234f, 22f), $"Tecla rápida: {Plugin.TrackerRefreshKey.Value}", styles.Muted);
    }

    private static void DrawRouteCard(Rect card, CompanionBoss selected, SCStyles styles)
    {
        SCUI.Card(card, "ÁREA DE PATRULHA • DADOS DO JOGO", styles);

        var buttonY = card.y + 10f;
        var right = card.xMax - 10f;

        var refreshWidth = 96f;
        if (SCUI.Button(new Rect(right - refreshWidth, buttonY, refreshWidth, 26f), "ATUALIZAR", styles.Button, true))
        {
            MobileBossRouteService.ForceRefresh(selected.CommandName);
            BossRespawnApi.Refresh(selected.CommandName);
        }
        right -= refreshWidth + 6f;

        var routeWidth = 94f;
        if (SCUI.Button(new Rect(right - routeWidth, buttonY, routeWidth, 26f), Plugin.ShowMobileBossRoute.Value ? "ROTA: ON" : "ROTA: OFF", styles.Button, true))
        {
            Plugin.ShowMobileBossRoute.Value = !Plugin.ShowMobileBossRoute.Value;
            Plugin.SaveState();
        }

        var info = MobileBossRouteService.GetInfo(selected.CommandName);
        var map = new Rect(card.x + 10f, card.y + 50f, card.width - 20f, Mathf.Max(38f, card.height - 94f));
        DrawRouteMap(map, info, MobileBossTrackerService.Snapshot, styles);

        var detection = MobileBossTrackerService.DetectionStatus(selected.CommandName);
        var detectionStyle = MobileBossTrackerService.IsPrefabResolved(selected.CommandName) ? styles.Green : styles.Muted;
        SCUI.Label(new Rect(card.x + 12f, card.yMax - 40f, card.width - 24f, 18f), "Detecção: " + detection, detectionStyle);

        var stats = $"{info.Status}  •  Nós: {info.NodeCount}  •  Trechos: {info.TotalSegments}  •  Atualizado: {info.LastUpdatedText}";
        SCUI.Label(new Rect(card.x + 12f, card.yMax - 22f, card.width - 24f, 18f), stats, styles.Tiny);
    }

    private static void DrawRouteMap(Rect map, MobileBossRouteInfo info, MobileBossTrackingSnapshot tracking, SCStyles styles)
    {
        SCUI.Panel(map, SCTheme.Backdrop, SCTheme.BorderSoft);

        IReadOnlyList<MobileBossRouteSegmentView> visibleRoute = Plugin.ShowMobileBossRoute.Value
            ? info.RouteSegments
            : Array.Empty<MobileBossRouteSegmentView>();

        var points = new List<Vector2>();
        foreach (var segment in visibleRoute)
        {
            points.Add(segment.Start);
            points.Add(segment.End);
        }
        if (tracking.HasWorldPositions)
        {
            points.Add(tracking.PlayerWorldPosition);
            points.Add(tracking.BossWorldPosition);
        }

        if (points.Count == 0)
        {
            var message = !MobileBossTrackerService.IsPrefabResolved(info.Command)
                ? "Simon e os demais bosses móveis agora são reconhecidos pelo catálogo. Aproxime-se do boss para o cliente carregar a entidade e os waypoints da patrulha."
                : "Boss reconhecido, mas os waypoints ainda não foram enviados ao cliente. Aproxime-se dele e pressione ATUALIZAR.";
            SCUI.Label(new Rect(map.x + 12f, map.y + 8f, map.width - 24f, map.height - 16f), message, styles.Muted);
            return;
        }

        var minX = points.Min(point => point.x);
        var maxX = points.Max(point => point.x);
        var minY = points.Min(point => point.y);
        var maxY = points.Max(point => point.y);
        if (maxX - minX < 24f)
        {
            minX -= 12f;
            maxX += 12f;
        }
        if (maxY - minY < 24f)
        {
            minY -= 12f;
            maxY += 12f;
        }

        const float padding = 8f;
        Vector2 ToScreen(Vector2 world)
        {
            var x = Mathf.InverseLerp(minX, maxX, world.x);
            var y = Mathf.InverseLerp(minY, maxY, world.y);
            return new Vector2(
                Mathf.Lerp(map.x + padding, map.xMax - padding, x),
                Mathf.Lerp(map.yMax - padding, map.y + padding, y));
        }

        var gridColor = SCTheme.BorderSoft;
        gridColor.a = 0.45f;
        SCTheme.Line(new Vector2(map.center.x, map.y + 5f), new Vector2(map.center.x, map.yMax - 5f), gridColor, 1f);
        SCTheme.Line(new Vector2(map.x + 5f, map.center.y), new Vector2(map.xMax - 5f, map.center.y), gridColor, 1f);

        foreach (var segment in visibleRoute)
        {
            var color = SCTheme.Gold;
            color.a = 0.9f;
            SCTheme.Line(ToScreen(segment.Start), ToScreen(segment.End), color, 2.2f);
        }

        if (tracking.HasWorldPositions)
        {
            SCTheme.Dot(ToScreen(tracking.PlayerWorldPosition), 7f, SCTheme.Blue);
            SCTheme.Dot(ToScreen(tracking.BossWorldPosition), 8f, SCTheme.Blood);
        }
    }

    private static string StatusText(CompanionBoss selected)
    {
        return selected.Status switch
        {
            CompanionBossStatus.Alive => "VIVO • disponível para enfrentar",
            CompanionBossStatus.Dead => $"MORTO • respawn em {SCUI.FormatTime(selected.RemainingSeconds)}",
            CompanionBossStatus.Querying => "CONSULTANDO O SERVIDOR",
            CompanionBossStatus.NotFound => "NÃO ENCONTRADO",
            _ => "AGUARDANDO RESPOSTA"
        };
    }

    private static GUIStyle StatusStyle(CompanionBoss selected, SCStyles styles)
    {
        return selected.Status == CompanionBossStatus.Alive ? styles.Green :
            selected.Status == CompanionBossStatus.Dead ? styles.Gold : styles.Label;
    }

    private static void ChangeRefresh(float delta)
    {
        Plugin.TrackerRefreshInterval.Value = Mathf.Clamp(Plugin.TrackerRefreshInterval.Value + delta, 10f, 120f);
        Plugin.SaveState();
    }
}
