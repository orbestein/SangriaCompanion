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
    internal bool HasWorldPositions { get; set; }
    internal float DistanceMeters { get; set; }
    internal string Direction { get; set; } = "Indisponível";
    internal string Movement { get; set; } = "Fora da área sincronizada";
    internal float LastSeenRealtime { get; set; } = -1f;
    internal string SourcePrefab { get; set; } = string.Empty;
    internal Vector2 PlayerWorldPosition { get; set; }
    internal Vector2 BossWorldPosition { get; set; }
}

/// <summary>
/// Lê a posição do boss móvel selecionado e solicita a rota de patrulha
/// diretamente aos componentes do jogo. Nenhuma posição é aprendida ou salva.
/// </summary>
internal static class MobileBossTrackerService
{
    // Nomes reais dos prefabs VBlood. A versão 2.5.0 usava aliases antigos
    // para Tristan, Bane, Frostmaw e Styx, fazendo com que esses bosses nunca
    // fossem reconhecidos pelo rastreador. Mantemos também tokens curtos como
    // fallback para pequenas alterações de nome entre versões do jogo.
    private static readonly Dictionary<string, string[]> PrefabAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lidia"] = ["CHAR_Bandit_Chaosarrow_VBlood", "Chaosarrow_VBlood", "Chaosarrow"],
        ["goreswine"] = ["CHAR_Undead_BishopOfDeath_VBlood", "BishopOfDeath_VBlood", "BishopOfDeath"],
        ["tristan"] = ["CHAR_VHunter_Leader_VBlood", "VHunter_Leader_VBlood", "VHunter_Leader"],
        ["bane"] = ["CHAR_Undead_Infiltrator_VBlood", "Undead_Infiltrator_VBlood", "Undead_Infiltrator"],
        ["jade"] = ["CHAR_VHunter_Jade_VBlood", "VHunter_Jade_VBlood", "VHunter_Jade"],
        ["frostmaw"] = ["CHAR_Wendigo_VBlood", "Wendigo_VBlood", "Wendigo"],
        ["styx"] = ["CHAR_BatVampire_VBlood", "BatVampire_VBlood", "BatVampire"],
        ["simon"] = [
            "CHAR_VHunter_Simon_VBlood",
            "CHAR_VHunter_SimonBelmont_VBlood",
            "CHAR_VHunter_Belmont_VBlood",
            "SimonBelmont_VBlood",
            "Simon_Belmont",
            "SimonBelmont",
            "Belmont",
            "Simon"
        ]
    };

    private static readonly MobileBossTrackingSnapshot SnapshotValue = new();
    private static readonly Dictionary<PrefabGUID, ResolvedMobileBoss> ResolvedPrefabs = new();
    private static readonly Dictionary<string, BossCandidate> Candidates = new(StringComparer.OrdinalIgnoreCase);
    private static float3 _previousBossPosition;
    private static bool _hasPreviousPosition;
    private static string _previousCommand = string.Empty;
    private static float _nextScanAt;
    private static float _nextPrefabResolveAt;
    private static World? _resolvedWorld;
    private static string _lastDiagnostic = string.Empty;

    internal static MobileBossTrackingSnapshot Snapshot => SnapshotValue;
    internal static bool IsMobile(string command) => PrefabAliases.ContainsKey(command ?? string.Empty);
    internal static IReadOnlyCollection<string> MobileCommands => PrefabAliases.Keys;
    internal static int ResolvedPrefabCount => ResolvedPrefabs.Count;

    internal static bool IsPrefabResolved(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;
        return ResolvedPrefabs.Values.Any(value => value.Command.Equals(command, StringComparison.OrdinalIgnoreCase));
    }

    internal static string DetectionStatus(string command)
    {
        if (!IsMobile(command)) return "Boss de localização fixa";
        if (ResolvedPrefabs.Count == 0) return "Aguardando identificação dos prefabs móveis";
        return IsPrefabResolved(command)
            ? "Boss móvel reconhecido"
            : "Prefab deste boss ainda não foi reconhecido";
    }

    internal static void Update()
    {
        if (Time.unscaledTime < _nextScanAt) return;
        _nextScanAt = Time.unscaledTime + 0.65f;

        var command = Plugin.TrackedBossCommand.Value?.Trim() ?? string.Empty;
        if (!command.Equals(_previousCommand, StringComparison.OrdinalIgnoreCase))
        {
            _previousCommand = command;
            _hasPreviousPosition = false;
        }

        SnapshotValue.IsMobileBoss = IsMobile(command);

        try
        {
            var world = ClientWorldService.World;
            if (world == null || !world.IsCreated)
            {
                if (SnapshotValue.IsMobileBoss) SetUnavailable("Aguardando mundo do cliente");
                return;
            }

            if (!SnapshotValue.IsMobileBoss) return;

            EnsureResolvedPrefabs(world);
            if (ResolvedPrefabs.Count == 0)
            {
                if (SnapshotValue.IsMobileBoss) SetUnavailable("Prefabs móveis não localizados");
                return;
            }

            ScanWorld(world);
            UpdateSelectedSnapshot(world, command);
        }
        catch (Exception ex)
        {
            if (SnapshotValue.IsMobileBoss) SetUnavailable("Falha na leitura do cliente");
            Plugin.Instance.Log.LogWarning("Falha no rastreamento móvel: " + ex.Message);
        }
    }

    private static void EnsureResolvedPrefabs(World world)
    {
        if (ReferenceEquals(_resolvedWorld, world) && Time.unscaledTime < _nextPrefabResolveAt)
            return;

        _resolvedWorld = world;
        _nextPrefabResolveAt = Time.unscaledTime + 30f;
        ResolvedPrefabs.Clear();

        var prefabs = world.GetExistingSystemManaged<ProjectM.PrefabCollectionSystem>();
        if (prefabs == null) return;

        foreach (var pair in prefabs.SpawnableNameToPrefabGuidDictionary)
        {
            foreach (var aliasGroup in PrefabAliases)
            {
                var matched = false;
                foreach (var alias in aliasGroup.Value)
                {
                    if (!pair.Key.Equals(alias, StringComparison.OrdinalIgnoreCase) &&
                        !pair.Key.Contains(alias, StringComparison.OrdinalIgnoreCase)) continue;
                    matched = true;
                    break;
                }

                if (!matched) continue;
                if (!ResolvedPrefabs.ContainsKey(pair.Value))
                {
                    ResolvedPrefabs[pair.Value] = new ResolvedMobileBoss
                    {
                        Command = aliasGroup.Key,
                        PrefabName = pair.Key
                    };
                }
                break;
            }
        }

        var resolvedCommands = ResolvedPrefabs.Values
            .Select(value => value.Command)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var missingCommands = PrefabAliases.Keys
            .Where(command => !resolvedCommands.Contains(command, StringComparer.OrdinalIgnoreCase))
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Plugin.Instance.Log.LogInfo(
            $"Rastreador móvel: {ResolvedPrefabs.Count} prefab(s) reconhecido(s). Bosses: " +
            (resolvedCommands.Length == 0 ? "nenhum" : string.Join(", ", resolvedCommands)));
        if (missingCommands.Length > 0)
            Plugin.Instance.Log.LogWarning("Rastreador móvel: prefab não localizado para " + string.Join(", ", missingCommands));
    }

    private static void ScanWorld(World world)
    {
        Candidates.Clear();
        var em = world.EntityManager;
        var player = ClientChatPatch.LocalCharacter;
        var hasPlayerPosition = player != Entity.Null && em.Exists(player) && em.HasComponent<LocalToWorld>(player);
        var playerPosition = hasPlayerPosition ? em.GetComponentData<LocalToWorld>(player).Position : float3.zero;

        var query = em.CreateEntityQuery(
            ComponentType.ReadOnly<PrefabGUID>(),
            ComponentType.ReadOnly<LocalToWorld>());
        var entities = query.ToEntityArray(Allocator.Temp);
        try
        {
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!em.Exists(entity) || em.HasComponent<Prefab>(entity)) continue;

                var guid = em.GetComponentData<PrefabGUID>(entity);
                if (!ResolvedPrefabs.TryGetValue(guid, out var resolved)) continue;

                var position = em.GetComponentData<LocalToWorld>(entity).Position;

                var distanceSq = hasPlayerPosition
                    ? math.distancesq(new float2(position.x, position.z), new float2(playerPosition.x, playerPosition.z))
                    : 0f;

                if (!Candidates.TryGetValue(resolved.Command, out var current) || distanceSq < current.DistanceSq)
                {
                    Candidates[resolved.Command] = new BossCandidate
                    {
                        Entity = entity,
                        Position = position,
                        PrefabName = resolved.PrefabName,
                        DistanceSq = distanceSq
                    };
                }
            }
        }
        finally
        {
            entities.Dispose();
        }

    }

    private static void UpdateSelectedSnapshot(World world, string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            SnapshotValue.IsAvailable = false;
            SnapshotValue.HasWorldPositions = false;
            return;
        }

        if (!SnapshotValue.IsMobileBoss)
        {
            SetUnavailable("Boss de localização fixa");
            return;
        }

        if (!Candidates.TryGetValue(command, out var candidate))
        {
            MobileBossRouteService.MarkUnavailable(command, "Aproxime-se do boss para carregar a rota real do jogo");
            SetUnavailable("Fora da área sincronizada");
            return;
        }

        var em = world.EntityManager;
        var player = ClientChatPatch.LocalCharacter;
        if (player == Entity.Null || !em.Exists(player) || !em.HasComponent<LocalToWorld>(player))
        {
            SnapshotValue.SourcePrefab = candidate.PrefabName;
            SetUnavailable("Posição do jogador indisponível");
            return;
        }

        var playerPosition = em.GetComponentData<LocalToWorld>(player).Position;
        var bossPosition = candidate.Position;
        var horizontal = new float2(bossPosition.x - playerPosition.x, bossPosition.z - playerPosition.z);

        SnapshotValue.DistanceMeters = math.length(horizontal);
        SnapshotValue.Direction = DirectionFrom(horizontal);
        SnapshotValue.Movement = _hasPreviousPosition && math.distance(_previousBossPosition, bossPosition) > 0.45f
            ? "Em deslocamento"
            : "Parado";
        SnapshotValue.IsAvailable = true;
        SnapshotValue.HasWorldPositions = true;
        SnapshotValue.LastSeenRealtime = Time.unscaledTime;
        SnapshotValue.SourcePrefab = candidate.PrefabName;
        SnapshotValue.PlayerWorldPosition = new Vector2(playerPosition.x, playerPosition.z);
        SnapshotValue.BossWorldPosition = new Vector2(bossPosition.x, bossPosition.z);
        _lastDiagnostic = string.Empty;
        _previousBossPosition = bossPosition;
        _hasPreviousPosition = true;

        MobileBossRouteService.RefreshFromWorld(world, command, candidate.Entity, bossPosition);
    }

    private static void SetUnavailable(string movement)
    {
        SnapshotValue.IsAvailable = false;
        SnapshotValue.HasWorldPositions = false;
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

    private sealed class ResolvedMobileBoss
    {
        internal string Command { get; init; } = string.Empty;
        internal string PrefabName { get; init; } = string.Empty;
    }

    private sealed class BossCandidate
    {
        internal Entity Entity { get; init; }
        internal float3 Position { get; init; }
        internal string PrefabName { get; init; } = string.Empty;
        internal float DistanceSq { get; init; }
    }
}
