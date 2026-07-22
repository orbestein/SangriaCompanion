using ProjectM;
using SangrisInterface.Patches;
using Stunlock.Core;
using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace SangriaCompanion;

/// <summary>
/// Lê periodicamente o inventário principal do personagem local.
/// A primeira leitura estabelece uma linha de base; alterações posteriores
/// alimentam o histórico de coleta/consumo e atualizam o planejador.
/// </summary>
internal static class InventorySyncService
{
    private static readonly Dictionary<int, string> NameCache = new();
    private static readonly Dictionary<string, int> Current = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> Previous = new(StringComparer.OrdinalIgnoreCase);
    private static float _nextScanAt;
    private static bool _baselineReady;
    private static string _status = "Aguardando personagem";
    private static float _nextDiagnosticLogAt;
    private static bool _compatibilityMode;

    internal static bool IsReady { get; private set; }
    internal static string Status => _status;
    internal static IReadOnlyDictionary<string, int> Snapshot => Current;

    internal static int GetAmount(string itemName)
    {
        if (Current.TryGetValue(itemName, out var amount)) return amount;

        var wanted = Normalize(itemName);
        if (wanted.Length == 0) return 0;

        foreach (var pair in Current)
        {
            var available = Normalize(pair.Key);
            if (available == wanted || available.Contains(wanted) || wanted.Contains(available))
                return Math.Max(amount, pair.Value);
        }

        return 0;
    }

    internal static void Update()
    {
        if (Time.unscaledTime < _nextScanAt) return;
        _nextScanAt = Time.unscaledTime + 0.75f;
        ScanInventory();
    }

    private static void ScanInventory()
    {
        try
        {
            var character = ClientChatPatch.LocalCharacter;
            // LocalCharacter pertence ao mesmo World do ClientChatSystem.
            // Usar World.DefaultGameObjectInjectionWorld pode misturar Entity e
            // EntityManager de mundos diferentes, fazendo a mochila parecer vazia.
            var world = ClientWorldService.World;

            if (world == null || !world.IsCreated || character == Entity.Null)
            {
                IsReady = false;
                _status = world == null ? "Aguardando mundo do cliente" : "Aguardando personagem";
                return;
            }

            var em = world.EntityManager;
            if (!em.Exists(character))
            {
                IsReady = false;
                _status = "Aguardando inventário";
                return;
            }

            // O personagem pode possuir mais de um inventário sincronizado
            // (mochila principal, bolsas/abas adicionais e inventários externos).
            // A versão anterior parava no primeiro InventoryBuffer localizado,
            // que em algumas instalações não é a mochila principal. Agora todos
            // os inventários pertencentes ao personagem são agregados.
            var inventoryEntities = new List<Entity>();
            var seen = new HashSet<Entity>();

            static void AddInventoryEntity(EntityManager manager, Entity entity, List<Entity> result, HashSet<Entity> known)
            {
                if (entity == Entity.Null || !manager.Exists(entity) || !known.Add(entity)) return;
                if (manager.HasBuffer<InventoryBuffer>(entity)) result.Add(entity);
            }

            AddInventoryEntity(em, character, inventoryEntities, seen);

            if (em.HasBuffer<InventoryInstanceElement>(character))
            {
                var instances = em.GetBuffer<InventoryInstanceElement>(character, true);
                for (var i = 0; i < instances.Length; i++)
                {
                    var candidate = instances[i].ExternalInventoryEntity.GetSyncedEntityOrNull();
                    AddInventoryEntity(em, candidate, inventoryEntities, seen);
                }
            }

            Current.Clear();
            var slotsRead = ReadInventoryEntities(world, em, inventoryEntities, Current, aggregateByMaximum: false);
            _compatibilityMode = false;

            // Algumas versões do cliente não expõem a mochila principal diretamente
            // pelo LocalCharacter/InventoryInstanceElement. Se a leitura vinculada vier
            // vazia, fazemos uma varredura compatível dos InventoryBuffer carregados.
            // Para reduzir duplicidade entre espelhos de rede, usamos o maior valor de
            // cada item encontrado, em vez de somar todos os recipientes.
            if (slotsRead == 0)
            {
                var query = em.CreateEntityQuery(ComponentType.ReadOnly<InventoryBuffer>());
                var allInventories = query.ToEntityArray(Allocator.Temp);
                try
                {
                    var candidates = new List<Entity>(allInventories.Length);
                    for (var i = 0; i < allInventories.Length; i++)
                    {
                        var entity = allInventories[i];
                        if (entity != Entity.Null && em.Exists(entity)) candidates.Add(entity);
                    }

                    Current.Clear();
                    slotsRead = ReadInventoryEntities(world, em, candidates, Current, aggregateByMaximum: true);
                    inventoryEntities = candidates;
                    _compatibilityMode = slotsRead > 0;
                }
                finally
                {
                    allInventories.Dispose();
                }
            }

            if (slotsRead == 0)
            {
                IsReady = false;
                _status = "Inventário localizado, mas sem pilhas legíveis";
                return;
            }

            if (_baselineReady)
            {
                var names = Previous.Keys.Concat(Current.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                foreach (var name in names)
                {
                    var before = Previous.TryGetValue(name, out var oldValue) ? oldValue : 0;
                    var now = Current.TryGetValue(name, out var newValue) ? newValue : 0;
                    var delta = now - before;
                    if (delta != 0)
                        CollectionService.RecordInventoryChange(name, delta, now);
                }
            }

            Previous.Clear();
            foreach (var pair in Current) Previous[pair.Key] = pair.Value;
            _baselineReady = true;
            IsReady = true;
            _status = (_compatibilityMode ? "Inventário em modo compatibilidade" : "Inventário sincronizado")
                + " • mundo=" + world.Name
                + " • " + inventoryEntities.Count + " fonte(s) • " + slotsRead + " pilha(s)";

            if (Time.unscaledTime >= _nextDiagnosticLogAt)
            {
                _nextDiagnosticLogAt = Time.unscaledTime + 10f;
                var sample = string.Join(", ", Current.OrderByDescending(x => x.Value).Take(12).Select(x => x.Key + "=" + x.Value));
                Plugin.Instance.Log.LogInfo("[Inventário] " + _status + " | " + sample);
            }
        }
        catch (Exception ex)
        {
            IsReady = false;
            _status = "Falha ao ler inventário";
            Plugin.Instance.Log.LogWarning($"Falha ao sincronizar inventário: {ex}");
        }
    }

    private static int ReadInventoryEntities(
        World world,
        EntityManager em,
        IReadOnlyList<Entity> inventoryEntities,
        Dictionary<string, int> target,
        bool aggregateByMaximum)
    {
        var slotsRead = 0;
        for (var entityIndex = 0; entityIndex < inventoryEntities.Count; entityIndex++)
        {
            var inventoryEntity = inventoryEntities[entityIndex];
            if (inventoryEntity == Entity.Null || !em.Exists(inventoryEntity) || !em.HasBuffer<InventoryBuffer>(inventoryEntity)) continue;

            var buffer = em.GetBuffer<InventoryBuffer>(inventoryEntity, true);
            var localTotals = aggregateByMaximum
                ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                : null;

            for (var i = 0; i < buffer.Length; i++)
            {
                var slot = buffer[i];
                if (slot.Amount <= 0 || slot.ItemType.GuidHash == 0) continue;

                var name = ResolveItemName(world, slot.ItemType);
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (localTotals != null)
                    localTotals[name] = localTotals.TryGetValue(name, out var localAmount) ? localAmount + slot.Amount : slot.Amount;
                else
                    target[name] = target.TryGetValue(name, out var amount) ? amount + slot.Amount : slot.Amount;

                slotsRead++;
            }

            if (localTotals == null) continue;
            foreach (var pair in localTotals)
            {
                if (!target.TryGetValue(pair.Key, out var current) || pair.Value > current)
                    target[pair.Key] = pair.Value;
            }
        }

        return slotsRead;
    }

    internal static string ResolveItemName(World world, PrefabGUID guid)
    {
        if (NameCache.TryGetValue(guid.GuidHash, out var cached)) return cached;

        var prefabName = string.Empty;
        try
        {
            var prefabs = world.GetExistingSystemManaged<PrefabCollectionSystem>();
            if (prefabs != null)
            {
                foreach (var pair in prefabs.SpawnableNameToPrefabGuidDictionary)
                {
                    if (pair.Value == guid)
                    {
                        prefabName = pair.Key;
                        break;
                    }
                }
            }
        }
        catch { }

        var resolved = TranslateKnownItem(prefabName);
        if (string.IsNullOrWhiteSpace(resolved))
            resolved = CleanPrefabName(prefabName, guid.GuidHash);

        NameCache[guid.GuidHash] = resolved;
        return resolved;
    }

    private static string TranslateKnownItem(string prefab)
    {
        var value = Normalize(prefab);
        if (value.Contains("reinforcedplank")) return "Tábua Reforçada";
        if (value.Contains("plank")) return "Tábua";
        if (value.Contains("ironore")) return "Minério de Ferro";
        if (value.Contains("ironingot")) return "Lingote de Ferro";
        if (value.Contains("sulphur") || value.Contains("sulfur")) return "Enxofre";
        if (value.Contains("techscrap") || value.Contains("technologicalscrap")) return "Sucata Tecnológica";
        if (value.Contains("radiumalloy")) return "Liga de Rádio";
        if (value.Contains("chargedbattery")) return "Bateria Carregada";
        if (value.Contains("powercore")) return "Núcleo de Energia";
        if (value.Contains("emptycanister") || value.Contains("emptycontainer")) return "Recipiente Vazio";
        if (value.Contains("sludgefilledcanister") || value.Contains("sludgecontainer")) return "Recipiente de Lodo";
        if (value.Contains("mutantgrease") || value.Contains("mutantsludge")) return "Lodo Mutante";
        return string.Empty;
    }

    private static string Normalize(string value)
        => new string((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string CleanPrefabName(string prefab, int hash)
    {
        if (string.IsNullOrWhiteSpace(prefab)) return $"Item {hash}";
        var text = prefab
            .Replace("Item_", string.Empty)
            .Replace("Ingredient_", string.Empty)
            .Replace("Consumable_", string.Empty)
            .Replace('_', ' ')
            .Trim();
        return text.Length > 48 ? text.Substring(0, 48) : text;
    }
}
