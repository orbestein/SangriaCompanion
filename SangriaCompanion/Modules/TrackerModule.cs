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

        var mobileHeight = selected?.IsMobile == true ? 320f : 250f;
        var card = new Rect(area.x, area.y + 50f, area.width, mobileHeight);
        SCUI.Card(card, "BOSS ACOMPANHADO", styles);

        if (selected == null)
        {
            SCUI.Label(new Rect(card.x + 18f, card.y + 56f, card.width - 36f, 28f), "Nenhum boss selecionado", styles.Gold);
            SCUI.Label(new Rect(card.x + 18f, card.y + 94f, card.width - 36f, 52f), "Abra a aba Bosses e clique em Rastrear. O Companion acompanhará o estado e o tempo de respawn.", styles.Muted);
            SCUI.Label(new Rect(card.x + 18f, card.y + 164f, card.width - 36f, 42f), "Bosses móveis podem exibir distância e direção quando sua entidade real estiver sincronizada com o cliente.", styles.Dim);
            return;
        }

        var statusText = selected.Status switch
        {
            CompanionBossStatus.Alive => "VIVO • disponível para enfrentar",
            CompanionBossStatus.Dead => $"MORTO • respawn em {SCUI.FormatTime(selected.RemainingSeconds)}",
            CompanionBossStatus.Querying => "CONSULTANDO O SERVIDOR",
            CompanionBossStatus.NotFound => "NÃO ENCONTRADO",
            _ => "AGUARDANDO RESPOSTA"
        };

        var accentStyle = selected.Status == CompanionBossStatus.Alive ? styles.Green :
            selected.Status == CompanionBossStatus.Dead ? styles.Gold : styles.Label;

        SCUI.Label(new Rect(card.x + 18f, card.y + 52f, card.width - 360f, 28f), $"{selected.Name} • Ato {selected.Act} • Nível {selected.Level}", styles.Gold);
        SCUI.Label(new Rect(card.x + 18f, card.y + 88f, card.width - 36f, 28f), $"Estado: {statusText}", accentStyle);

        var mainInfo = selected.Status == CompanionBossStatus.Dead
            ? $"Tempo restante: {SCUI.FormatTime(selected.RemainingSeconds)}"
            : selected.Status == CompanionBossStatus.Alive
                ? "Tempo restante: disponível agora"
                : "Tempo restante: aguardando consulta";
        SCUI.Label(new Rect(card.x + 18f, card.y + 122f, card.width - 36f, 26f), mainInfo, styles.Label);
        SCUI.Label(new Rect(card.x + 18f, card.y + 152f, card.width - 36f, 22f), $"Última atualização: {BossNotificationService.LastUpdateText(selected.CommandName)}", styles.Muted);
        SCUI.Label(new Rect(card.x + 18f, card.y + 180f, 190f, 22f), $"Favorito: {(selected.IsFavorite ? "SIM" : "NÃO")}", selected.IsFavorite ? styles.Gold : styles.Muted);
        SCUI.Label(new Rect(card.x + 212f, card.y + 180f, 210f, 22f), $"Aviso na tela: {(selected.AlertEnabled ? "SIM" : "NÃO")}", selected.AlertEnabled ? styles.Green : styles.Muted);

        if (SCUI.Button(new Rect(card.xMax - 344f, card.y + 48f, 100f, 30f), "Atualizar", styles.Button, true))
            BossRespawnApi.Refresh(selected.CommandName);
        if (SCUI.Button(new Rect(card.xMax - 236f, card.y + 48f, 102f, 30f), selected.IsFavorite ? "★ Favorito" : "☆ Favoritar", styles.Button, true))
        {
            selected.IsFavorite = !selected.IsFavorite;
            BossRespawnApi.SetPinned(selected.CommandName, selected.IsFavorite);
            BossPreferenceService.Save();
        }
        if (SCUI.Button(new Rect(card.xMax - 126f, card.y + 48f, 108f, 30f), selected.AlertEnabled ? "Aviso: SIM" : "Aviso: NÃO", styles.Button, true))
        {
            selected.AlertEnabled = !selected.AlertEnabled;
            BossPreferenceService.Save();
        }


        var lowerControlsY = card.y + 210f;
        if (selected.IsMobile)
        {
            var tracking = MobileBossTrackerService.Snapshot;
            var distanceText = tracking.IsAvailable ? Mathf.RoundToInt(tracking.DistanceMeters) + " m" : "Indisponível";
            SCUI.Label(new Rect(card.x + 18f, card.y + 210f, card.width - 36f, 24f), "Distância: " + distanceText, tracking.IsAvailable ? styles.Label : styles.Muted);
            SCUI.Label(new Rect(card.x + 18f, card.y + 236f, card.width - 36f, 24f), "Direção: " + tracking.Direction, tracking.IsAvailable ? styles.Label : styles.Muted);
            SCUI.Label(new Rect(card.x + 18f, card.y + 262f, card.width - 36f, 24f), "Movimento: " + tracking.Movement, tracking.IsAvailable ? styles.Green : styles.Muted);
            SCUI.Label(new Rect(card.x + 146f, card.y + 286f, card.width - 164f, 22f), "Última leitura: " + MobileBossTrackerService.LastReadText(), styles.Muted);
            lowerControlsY = card.y + 282f;
        }

        if (SCUI.Button(new Rect(card.x + 18f, lowerControlsY, 108f, 28f), "Limpar", styles.Button, true))
        {
            state.TrackedBossCommand = string.Empty;
            Plugin.TrackedBossCommand.Value = string.Empty;
            Plugin.SaveState();
            return;
        }

        var configCard = new Rect(area.x, card.yMax + 12f, area.width, 94f);
        SCUI.Card(configCard, "HUD COMPACTA", styles);
        var hudLabel = Plugin.TrackerHudEnabled.Value ? "HUD: LIGADA" : "HUD: DESLIGADA";
        if (SCUI.Button(new Rect(configCard.x + 18f, configCard.y + 50f, 130f, 28f), hudLabel, styles.Button, true))
        {
            Plugin.TrackerHudEnabled.Value = !Plugin.TrackerHudEnabled.Value;
            Plugin.SaveState();
        }

        SCUI.Label(new Rect(configCard.x + 166f, configCard.y + 53f, 126f, 24f), $"Atualizar: {Plugin.TrackerRefreshInterval.Value:0}s", styles.Label);
        if (SCUI.Button(new Rect(configCard.x + 294f, configCard.y + 50f, 28f, 28f), "−", styles.Button, true)) ChangeRefresh(-5f);
        if (SCUI.Button(new Rect(configCard.x + 328f, configCard.y + 50f, 28f, 28f), "+", styles.Button, true)) ChangeRefresh(5f);
        SCUI.Label(new Rect(configCard.x + 374f, configCard.y + 53f, configCard.width - 392f, 24f), $"Tecla rápida: {Plugin.TrackerRefreshKey.Value}", styles.Muted);

        if (selected.IsMobile) return;

        var historyCard = new Rect(area.x, configCard.yMax + 12f, area.width, Mathf.Max(82f, area.yMax - configCard.yMax - 12f));
        SCUI.Card(historyCard, "HISTÓRICO RECENTE", styles);
        var history = BossNotificationService.HistoryFor(selected.CommandName, selected.Name);
        if (history.Count == 0)
        {
            SCUI.Label(new Rect(historyCard.x + 18f, historyCard.y + 54f, historyCard.width - 36f, 24f), "Nenhuma alteração registrada nesta sessão.", styles.Muted);
        }
        else
        {
            var y = historyCard.y + 50f;
            foreach (var item in history.Take(4))
            {
                if (y + 22f > historyCard.yMax - 8f) break;
                SCUI.Label(new Rect(historyCard.x + 18f, y, 58f, 20f), item.CreatedAt.ToString("HH:mm"), styles.Tiny);
                SCUI.Label(new Rect(historyCard.x + 72f, y, historyCard.width - 90f, 20f), item.Message, styles.Label);
                y += 25f;
            }
        }
    }

    private static void ChangeRefresh(float delta)
    {
        Plugin.TrackerRefreshInterval.Value = Mathf.Clamp(Plugin.TrackerRefreshInterval.Value + delta, 10f, 120f);
        Plugin.SaveState();
    }
}
