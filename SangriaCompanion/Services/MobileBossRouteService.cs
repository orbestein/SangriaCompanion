using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SangriaCompanion;

internal sealed class MobileBossRouteSegmentView
{
    internal Vector2 Start { get; init; }
    internal Vector2 End { get; init; }
}

internal sealed class MobileBossRouteInfo
{
    internal string Command { get; init; } = string.Empty;
    internal bool IsAvailable { get; init; }
    internal string Status { get; init; } = "Rota ainda não carregada";
    internal string Source { get; init; } = "Dados do jogo";
    internal string LastUpdatedText { get; init; } = "Nunca";
    internal int NodeCount { get; init; }
    internal IReadOnlyList<MobileBossRouteSegmentView> RouteSegments { get; init; } = Array.Empty<MobileBossRouteSegmentView>();
    internal int TotalSegments => RouteSegments.Count;
}

/// <summary>
/// Lê a rota de patrulha diretamente dos componentes do jogo. Não grava
/// histórico, não aprende posições e não cria arquivos persistentes.
/// </summary>
internal static class MobileBossRouteService
{
    private sealed class RouteCache
    {
        internal readonly List<Vector2> Nodes = new();
        internal readonly List<MobileBossRouteSegmentView> Segments = new();
        internal bool IsAvailable;
        internal string Status = "Aguardando o boss entrar na área sincronizada";
        internal float LastUpdatedRealtime = -1f;
        internal float NextRefreshAt;
    }

    private static readonly Dictionary<string, RouteCache> Cache = new(StringComparer.OrdinalIgnoreCase);

    internal static void RefreshFromWorld(World world, string command, Entity bossEntity, float3 bossPosition, bool force = false)
    {
        if (string.IsNullOrWhiteSpace(command) || world == null || !world.IsCreated) return;

        var route = GetOrCreate(command);
        if (!force && Time.unscaledTime < route.NextRefreshAt) return;
        route.NextRefreshAt = Time.unscaledTime + 4f;

        route.Nodes.Clear();
        route.Segments.Clear();

        try
        {
            var em = world.EntityManager;

            // Primeira tentativa: usa os alvos de waypoint vinculados ao próprio boss.
            // Esse caminho é o mais preciso quando o cliente recebe o buffer de patrulha.
            if (bossEntity != Entity.Null && em.Exists(bossEntity))
                TryReadBossWaypointBuffer(em, bossEntity, route.Nodes);

            // Fallback: lê os nós de patrulha carregados pelo cliente próximos ao boss.
            // Não registra movimento; apenas consulta os nós atuais do World.
            if (route.Nodes.Count < 2)
                ReadNearbyPathNodes(em, command, bossPosition, route.Nodes);

            Deduplicate(route.Nodes, 2.5f);
            BuildSegments(route.Nodes, route.Segments, bossPosition, MaxLinkDistance(command));

            route.IsAvailable = route.Segments.Count > 0;
            route.Status = route.IsAvailable
                ? "Rota de patrulha carregada diretamente do jogo"
                : "O cliente ainda não expôs os waypoints desta rota";
            route.LastUpdatedRealtime = Time.unscaledTime;
        }
        catch (Exception ex)
        {
            route.IsAvailable = false;
            route.Status = "Falha ao ler os waypoints do jogo";
            route.LastUpdatedRealtime = Time.unscaledTime;
            Plugin.Instance.Log.LogWarning($"Falha ao ler rota direta de {command}: {ex.Message}");
        }
    }

    internal static void MarkUnavailable(string command, string reason)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        var route = GetOrCreate(command);
        route.IsAvailable = route.Segments.Count > 0;
        if (!route.IsAvailable) route.Status = reason;
    }

    internal static MobileBossRouteInfo GetInfo(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return new MobileBossRouteInfo();

        var route = GetOrCreate(command);
        return new MobileBossRouteInfo
        {
            Command = command,
            IsAvailable = route.IsAvailable,
            Status = route.Status,
            Source = "Waypoints do jogo (sem aprendizado)",
            LastUpdatedText = LastUpdatedText(route.LastUpdatedRealtime),
            NodeCount = route.Nodes.Count,
            RouteSegments = route.Segments.ToArray()
        };
    }

    internal static void ForceRefresh(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        GetOrCreate(command).NextRefreshAt = 0f;
    }

    private static RouteCache GetOrCreate(string command)
    {
        if (!Cache.TryGetValue(command, out var route))
        {
            route = new RouteCache();
            Cache[command] = route;
        }
        return route;
    }

    private static void TryReadBossWaypointBuffer(EntityManager em, Entity bossEntity, List<Vector2> output)
    {
        try
        {
            if (!em.HasBuffer<ProjectM.WaypointTargetBufferEntry>(bossEntity)) return;
            var buffer = em.GetBuffer<ProjectM.WaypointTargetBufferEntry>(bossEntity, true);
            for (var i = 0; i < buffer.Length; i++)
            {
                object boxed = buffer[i];
                foreach (var target in ExtractEntityReferences(boxed))
                    if (TryGetPosition(em, target, out var position))
                        output.Add(new Vector2(position.x, position.z));
            }
        }
        catch
        {
            // Alguns bosses usam uma entidade intermediária de patrulha. Nesse
            // caso o fallback por PathWaypointNode fará a leitura disponível.
        }
    }

    private static IEnumerable<Entity> ExtractEntityReferences(object boxed)
    {
        var type = boxed.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (var field in type.GetFields(flags))
        {
            if (field.FieldType != typeof(Entity)) continue;
            Entity value;
            try { value = (Entity)(field.GetValue(boxed) ?? Entity.Null); }
            catch { continue; }
            if (value != Entity.Null) yield return value;
        }

        foreach (var property in type.GetProperties(flags))
        {
            if (property.PropertyType != typeof(Entity) || property.GetIndexParameters().Length != 0) continue;
            Entity value;
            try { value = (Entity)(property.GetValue(boxed) ?? Entity.Null); }
            catch { continue; }
            if (value != Entity.Null) yield return value;
        }
    }

    private static void ReadNearbyPathNodes(EntityManager em, string command, float3 bossPosition, List<Vector2> output)
    {
        var query = em.CreateEntityQuery(ComponentType.ReadOnly<ProjectM.PathWaypointNode>());
        var entities = query.ToEntityArray(Allocator.Temp);
        try
        {
            var radius = SearchRadius(command);
            var radiusSq = radius * radius;
            var boss2 = new float2(bossPosition.x, bossPosition.z);

            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!em.Exists(entity) || !TryGetPosition(em, entity, out var position)) continue;
                var point = new float2(position.x, position.z);
                if (math.distancesq(point, boss2) > radiusSq) continue;
                output.Add(new Vector2(point.x, point.y));
            }
        }
        finally
        {
            entities.Dispose();
            query.Dispose();
        }
    }

    private static bool TryGetPosition(EntityManager em, Entity entity, out float3 position)
    {
        position = float3.zero;
        if (entity == Entity.Null || !em.Exists(entity)) return false;

        if (em.HasComponent<LocalToWorld>(entity))
        {
            position = em.GetComponentData<LocalToWorld>(entity).Position;
            return true;
        }

        if (em.HasComponent<Translation>(entity))
        {
            position = em.GetComponentData<Translation>(entity).Value;
            return true;
        }

        return false;
    }

    private static void Deduplicate(List<Vector2> points, float minDistance)
    {
        if (points.Count < 2) return;
        var unique = new List<Vector2>(points.Count);
        var minSq = minDistance * minDistance;
        foreach (var point in points)
        {
            var duplicate = unique.Any(existing => (existing - point).sqrMagnitude <= minSq);
            if (!duplicate) unique.Add(point);
        }
        points.Clear();
        points.AddRange(unique);
    }

    private static void BuildSegments(
        List<Vector2> nodes,
        List<MobileBossRouteSegmentView> segments,
        float3 bossPosition,
        float maxLinkDistance)
    {
        if (nodes.Count < 2) return;

        // Constrói somente o componente conectado mais próximo ao boss. Isso
        // evita misturar rotas diferentes que estejam carregadas na mesma região.
        var startPosition = new Vector2(bossPosition.x, bossPosition.z);
        var startIndex = 0;
        var bestStart = float.MaxValue;
        for (var i = 0; i < nodes.Count; i++)
        {
            var distance = (nodes[i] - startPosition).sqrMagnitude;
            if (distance >= bestStart) continue;
            bestStart = distance;
            startIndex = i;
        }

        var visited = new HashSet<int> { startIndex };
        var maxLinkSq = maxLinkDistance * maxLinkDistance;

        while (visited.Count < nodes.Count)
        {
            var bestFrom = -1;
            var bestTo = -1;
            var bestDistance = float.MaxValue;

            foreach (var from in visited)
            {
                for (var to = 0; to < nodes.Count; to++)
                {
                    if (visited.Contains(to)) continue;
                    var distance = (nodes[from] - nodes[to]).sqrMagnitude;
                    if (distance > maxLinkSq || distance >= bestDistance) continue;
                    bestDistance = distance;
                    bestFrom = from;
                    bestTo = to;
                }
            }

            if (bestFrom < 0 || bestTo < 0) break;
            segments.Add(new MobileBossRouteSegmentView { Start = nodes[bestFrom], End = nodes[bestTo] });
            visited.Add(bestTo);
        }

        if (segments.Count == 0)
        {
            // Fallback visual para dois ou mais nós muito espaçados.
            var ordered = nodes.OrderBy(point => (point - startPosition).sqrMagnitude).Take(24).ToArray();
            for (var i = 1; i < ordered.Length; i++)
                if ((ordered[i] - ordered[i - 1]).sqrMagnitude <= maxLinkSq * 2.25f)
                    segments.Add(new MobileBossRouteSegmentView { Start = ordered[i - 1], End = ordered[i] });
        }
    }

    private static float SearchRadius(string command) =>
        command.Equals("simon", StringComparison.OrdinalIgnoreCase) ? 1100f : 700f;

    private static float MaxLinkDistance(string command) =>
        command.Equals("simon", StringComparison.OrdinalIgnoreCase) ? 190f : 130f;

    private static string LastUpdatedText(float realtime)
    {
        if (realtime < 0f) return "Nunca";
        var elapsed = Mathf.Max(0, Mathf.FloorToInt(Time.unscaledTime - realtime));
        return elapsed <= 1 ? "Agora" : elapsed + " s atrás";
    }
}
