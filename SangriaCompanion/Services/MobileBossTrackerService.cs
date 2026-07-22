using SangrisInterface.Patches;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SangriaCompanion;

internal sealed class MobileBossTrackingSnapshot
{
    internal bool IsMobileBoss { get; set; }
    internal bool IsAvailable { get; set; }
    internal float DistanceMeters { get; set; }
    internal string Direction { get; set; } = "Indisponível";
    internal string Movement { get; set; } = "Fora da área sincronizada";
    internal float LastSeenRealtime { get; set; } = -1f;
    internal string SourcePrefab { get; set; } = string.Empty;
}

/// <summary>
/// Lê somente posições realmente presentes no World do cliente. Não inventa
/// coordenadas nem usa uma posição fixa. Se o servidor não replicar a entidade,
/// a localização é informada como indisponível.
/// </summary>
internal static class MobileBossTrackerService
{
    private static readonly Dictionary<string, string[]> PrefabAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lidia"] = ["CHAR_Bandit_Chaosarrow_VBlood"],
        ["goreswine"] = ["CHAR_Undead_BishopOfDeath_VBlood"],
        ["tristan"] = ["CHAR_VampireHunter_VBlood", "CHAR_VampireHunter_Tristan_VBlood"],
        ["bane"] = ["CHAR_Undead_Assassin_VBlood"],
        ["jade"] = ["CHAR_VampireHunter_Jade_VBlood", "CHAR_VHunter_Jade_VBlood"],
        ["frostmaw"] = ["CHAR_Winter_Yeti_VBlood", "CHAR_Winter_Yeti_Roaming_VBlood"],
        ["styx"] = ["CHAR_Undead_ZealousCultist_VBlood", "CHAR_Undead_Overseer_VBlood"]
    };

    private static readonly MobileBossTrackingSnapshot SnapshotValue = new();
    private static float3 _previousBossPosition;
    private static bool _hasPreviousPosition;
    private static string _previousCommand = string.Empty;
    private static float _nextScanAt;
    private static string _lastDiagnostic = string.Empty;

    internal static MobileBossTrackingSnapshot Snapshot => SnapshotValue;
    internal static bool IsMobile(string command) => PrefabAliases.ContainsKey(command ?? string.Empty);

    internal static void Update()
    {
        if (Time.unscaledTime < _nextScanAt) return;
        _nextScanAt = Time.unscaledTime + 0.5f;

        var command = Plugin.TrackedBossCommand.Value?.Trim() ?? string.Empty;
        if (!command.Equals(_previousCommand, StringComparison.OrdinalIgnoreCase))
        {
            _previousCommand = command;
            _hasPreviousPosition = false;
        }

        SnapshotValue.IsMobileBoss = IsMobile(command);
        if (!SnapshotValue.IsMobileBoss)
        {
            SetUnavailable("Boss de localização fixa");
            return;
        }

        TryScan(command);
    }

    private static void TryScan(string command)
    {
        try
        {
            var world = ClientWorldService.World;
            var player = ClientChatPatch.LocalCharacter;
            if (world == null || !world.IsCreated || player == Entity.Null)
            {
                SetUnavailable("Aguardando mundo do cliente");
                return;
            }

            var em = world.EntityManager;
            if (!em.Exists(player) || !em.HasComponent<LocalToWorld>(player))
            {
                SetUnavailable("Posição do jogador indisponível");
                return;
            }

            if (!TryResolveBossGuid(world, command, out var bossGuid, out var prefabName))
            {
                SetUnavailable("Prefab do boss não localizado");
                return;
            }

            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<PrefabGUID>(),
                ComponentType.ReadOnly<LocalToWorld>());
            var entities = query.ToEntityArray(Allocator.Temp);
            try
            {
                Entity bossEntity = Entity.Null;
                for (var i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    if (!em.Exists(entity)) continue;
                    var guid = em.GetComponentData<PrefabGUID>(entity);
                    if (guid == bossGuid)
                    {
                        bossEntity = entity;
                        break;
                    }
                }

                if (bossEntity == Entity.Null)
                {
                    SetUnavailable("Fora da área sincronizada");
                    SnapshotValue.SourcePrefab = prefabName;
                    return;
                }

                var playerPosition = em.GetComponentData<LocalToWorld>(player).Position;
                var bossPosition = em.GetComponentData<LocalToWorld>(bossEntity).Position;
                var horizontal = new float2(bossPosition.x - playerPosition.x, bossPosition.z - playerPosition.z);
                SnapshotValue.DistanceMeters = math.length(horizontal);
                SnapshotValue.Direction = DirectionFrom(horizontal);
                SnapshotValue.Movement = _hasPreviousPosition && math.distance(_previousBossPosition, bossPosition) > 0.45f
                    ? "Em deslocamento"
                    : "Parado";
                SnapshotValue.IsAvailable = true;
                SnapshotValue.LastSeenRealtime = Time.unscaledTime;
                SnapshotValue.SourcePrefab = prefabName;
                _lastDiagnostic = string.Empty;
                _previousBossPosition = bossPosition;
                _hasPreviousPosition = true;
            }
            finally
            {
                entities.Dispose();
            }
        }
        catch (Exception ex)
        {
            SetUnavailable("Falha na leitura do cliente");
            Plugin.Instance.Log.LogWarning("Falha no rastreamento móvel: " + ex.Message);
        }
    }

    private static bool TryResolveBossGuid(World world, string command, out PrefabGUID guid, out string prefabName)
    {
        guid = default;
        prefabName = string.Empty;
        if (!PrefabAliases.TryGetValue(command, out var aliases)) return false;

        var prefabs = world.GetExistingSystemManaged<ProjectM.PrefabCollectionSystem>();
        if (prefabs == null) return false;

        foreach (var pair in prefabs.SpawnableNameToPrefabGuidDictionary)
        {
            for (var i = 0; i < aliases.Length; i++)
            {
                if (!pair.Key.Equals(aliases[i], StringComparison.OrdinalIgnoreCase) &&
                    !pair.Key.Contains(aliases[i], StringComparison.OrdinalIgnoreCase)) continue;
                guid = pair.Value;
                prefabName = pair.Key;
                return true;
            }
        }
        return false;
    }

    private static void SetUnavailable(string movement)
    {
        SnapshotValue.IsAvailable = false;
        SnapshotValue.DistanceMeters = 0f;
        SnapshotValue.Direction = "Indisponível";
        SnapshotValue.Movement = movement;
        if (!_lastDiagnostic.Equals(movement, StringComparison.Ordinal))
        {
            _lastDiagnostic = movement;
            Plugin.Instance.Log.LogInfo("Rastreador móvel: " + movement);
        }
    }

    private static string DirectionFrom(float2 vector)
    {
        if (math.lengthsq(vector) < 0.01f) return "No mesmo local";
        var angle = math.degrees(math.atan2(vector.x, vector.y));
        if (angle < 0f) angle += 360f;
        if (angle < 22.5f || angle >= 337.5f) return "Norte";
        if (angle < 67.5f) return "Nordeste";
        if (angle < 112.5f) return "Leste";
        if (angle < 157.5f) return "Sudeste";
        if (angle < 202.5f) return "Sul";
        if (angle < 247.5f) return "Sudoeste";
        if (angle < 292.5f) return "Oeste";
        return "Noroeste";
    }

    internal static string LastReadText()
    {
        if (SnapshotValue.LastSeenRealtime < 0f) return "Nunca";
        var elapsed = Mathf.Max(0, Mathf.FloorToInt(Time.unscaledTime - SnapshotValue.LastSeenRealtime));
        return elapsed <= 1 ? "Agora" : elapsed + " s atrás";
    }
}
