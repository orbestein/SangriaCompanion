using UnityEngine;

namespace SangriaCompanion;

internal static class BossNotificationService
{
    private sealed class State
    {
        internal bool HasStableResponse;
        internal CompanionBossStatus StableStatus;
        internal float PreviousRemaining;
        internal DateTime LastUpdate;
        internal readonly HashSet<int> FiredThresholds = new();
    }

    private static readonly Dictionary<string, State> States = new(StringComparer.OrdinalIgnoreCase);
    private static float _nextCheck;
    private static readonly int[] Thresholds = [600, 300, 60];

    internal static void Update()
    {
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + 0.5f;

        foreach (var boss in BossCatalog.All)
        {
            if (!States.TryGetValue(boss.CommandName, out var state))
            {
                state = new State();
                States[boss.CommandName] = state;
            }

            // Querying/Waiting são estados transitórios de cada consulta. Eles não
            // podem substituir o último estado real, senão cada resposta DEAD é
            // interpretada como uma nova derrota e cria alertas sem parar.
            var isStable = boss.Status is CompanionBossStatus.Alive or CompanionBossStatus.Dead;
            if (!isStable) continue;

            var changed = !state.HasStableResponse ||
                          state.StableStatus != boss.Status ||
                          Mathf.Abs(state.PreviousRemaining - boss.RemainingSeconds) > 1f;
            if (changed) state.LastUpdate = DateTime.Now;

            // A primeira resposta real só estabelece a linha de base. Não avisamos
            // todos os bosses imediatamente ao entrar no servidor.
            if (!state.HasStableResponse)
            {
                state.HasStableResponse = true;
                state.StableStatus = boss.Status;
                state.PreviousRemaining = boss.RemainingSeconds;
                if (boss.Status == CompanionBossStatus.Dead)
                    PrimePassedThresholds(state, boss.RemainingSeconds);
                continue;
            }

            var alertsAllowed = Plugin.BossAlertsEnabled.Value && (boss.AlertEnabled || boss.IsFavorite);

            if (boss.Status == CompanionBossStatus.Alive)
            {
                if (alertsAllowed && state.StableStatus == CompanionBossStatus.Dead)
                {
                    NotificationCenter.Enqueue(
                        boss.Name,
                        "ESTÁ VIVO e disponível para enfrentar.",
                        SCTheme.Green,
                        Plugin.BossAlertDuration.Value,
                        "Boss");
                }

                if (state.StableStatus != CompanionBossStatus.Alive)
                    state.FiredThresholds.Clear();
            }
            else // Dead
            {
                // Só é uma nova derrota quando o último estado real era VIVO.
                if (state.StableStatus == CompanionBossStatus.Alive)
                {
                    state.FiredThresholds.Clear();
                    if (alertsAllowed && boss.RemainingSeconds > 0f)
                    {
                        NotificationCenter.Enqueue(
                            boss.Name,
                            $"Foi derrotado • respawn em {SCUI.FormatTime(boss.RemainingSeconds)}.",
                            SCTheme.Blood,
                            Plugin.BossAlertDuration.Value,
                            "Boss");
                    }
                }

                if (alertsAllowed)
                {
                    foreach (var threshold in Thresholds)
                        FireWhenCrossed(state, boss, threshold);

                    var custom = Mathf.Clamp(Plugin.BossAlertSeconds.Value, 5, 600);
                    FireWhenCrossed(state, boss, custom);
                }
            }

            state.StableStatus = boss.Status;
            state.PreviousRemaining = boss.RemainingSeconds;
        }
    }

    private static void FireWhenCrossed(State state, CompanionBoss boss, int threshold)
    {
        // Dispara uma única vez quando a contagem cruza o marco de cima para baixo.
        // O HashSet também evita duplicidade quando o marco personalizado coincide
        // com 10, 5 ou 1 minuto.
        var crossed = state.PreviousRemaining > threshold && boss.RemainingSeconds <= threshold;
        if (!crossed || boss.RemainingSeconds < 0f || !state.FiredThresholds.Add(threshold)) return;

        NotificationCenter.Enqueue(
            boss.Name,
            $"Respawn em {SCUI.FormatTime(boss.RemainingSeconds)}.",
            SCTheme.Gold,
            Plugin.BossAlertDuration.Value,
            "Boss");
    }

    private static void PrimePassedThresholds(State state, float remaining)
    {
        foreach (var threshold in Thresholds)
            if (remaining <= threshold) state.FiredThresholds.Add(threshold);

        var custom = Mathf.Clamp(Plugin.BossAlertSeconds.Value, 5, 600);
        if (remaining <= custom) state.FiredThresholds.Add(custom);
    }

    internal static string LastUpdateText(string command)
    {
        if (!States.TryGetValue(command, out var state) || state.LastUpdate == default) return "ainda não consultado";
        var elapsed = DateTime.Now - state.LastUpdate;
        if (elapsed.TotalSeconds < 3) return "agora";
        if (elapsed.TotalMinutes < 1) return $"há {(int)elapsed.TotalSeconds}s";
        return $"há {(int)elapsed.TotalMinutes} min";
    }

    internal static IReadOnlyList<NotificationHistoryItem> HistoryFor(string command, string displayName) => NotificationCenter.Recent
        .Where(x => x.Category == "Boss" && (x.Title.Equals(displayName, StringComparison.OrdinalIgnoreCase) || x.Title.Equals(command, StringComparison.OrdinalIgnoreCase)))
        .Take(5).ToArray();
}
