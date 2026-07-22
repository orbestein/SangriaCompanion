using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace SangriaCompanion;

/// <summary>
/// Importa as receitas diretamente do GameDataSystem carregado pelo V Rising.
/// A leitura usa os buffers reais RecipeRequirementBuffer e RecipeOutputBuffer,
/// evitando manter uma lista manual que fica desatualizada após patches do jogo.
/// </summary>
internal static class RecipeDiscoveryService
{
    private static float _nextAttemptAt;
    private static bool _loaded;
    private static bool _loading;

    internal static bool IsLoaded => _loaded;
    internal static int ImportedRecipes { get; private set; }
    internal static int ScannedRecipePrefabs { get; private set; }
    internal static string Status { get; private set; } = "Aguardando dados do jogo";

    internal static void Update()
    {
        if (_loaded || _loading || Time.unscaledTime < _nextAttemptAt) return;
        _nextAttemptAt = Time.unscaledTime + 5f;
        TryImport();
    }

    internal static void Reload()
    {
        _loaded = false;
        _nextAttemptAt = 0f;
        Status = "Recarregando receitas";
    }

    private static void TryImport()
    {
        _loading = true;
        try
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                Status = "Aguardando mundo do jogo";
                return;
            }

            var gameData = world.GetExistingSystemManaged<GameDataSystem>();
            if (gameData == null)
            {
                Status = "Aguardando GameDataSystem";
                return;
            }

            var entityManager = world.EntityManager;
            var lookup = gameData.RecipeHashLookupMap;
            var arrays = lookup.GetKeyValueArrays(Allocator.Temp);
            try
            {
                ImportedRecipes = 0;
                ScannedRecipePrefabs = arrays.Values.Length;

                for (var i = 0; i < arrays.Values.Length; i++)
                {
                    var data = arrays.Values[i];
                    var recipeEntity = data.Entity;
                    if (recipeEntity == Entity.Null || !entityManager.Exists(recipeEntity)) continue;
                    if (!entityManager.HasBuffer<RecipeRequirementBuffer>(recipeEntity)) continue;
                    if (!entityManager.HasBuffer<RecipeOutputBuffer>(recipeEntity)) continue;

                    var requirementsBuffer = entityManager.GetBuffer<RecipeRequirementBuffer>(recipeEntity, true);
                    var outputsBuffer = entityManager.GetBuffer<RecipeOutputBuffer>(recipeEntity, true);
                    if (requirementsBuffer.Length == 0 || outputsBuffer.Length == 0) continue;

                    var ingredients = new List<RecipeIngredient>();
                    for (var requirementIndex = 0; requirementIndex < requirementsBuffer.Length; requirementIndex++)
                    {
                        var requirement = requirementsBuffer[requirementIndex];
                        if (requirement.Guid.GuidHash == 0 || requirement.Amount <= 0) continue;
                        var ingredientName = InventorySyncService.ResolveItemName(world, requirement.Guid);
                        if (string.IsNullOrWhiteSpace(ingredientName)) continue;
                        ingredients.Add(new RecipeIngredient(ingredientName, requirement.Amount));
                    }

                    if (ingredients.Count == 0) continue;

                    for (var outputIndex = 0; outputIndex < outputsBuffer.Length; outputIndex++)
                    {
                        var output = outputsBuffer[outputIndex];
                        if (output.Guid.GuidHash == 0 || output.Amount <= 0) continue;
                        var outputName = InventorySyncService.ResolveItemName(world, output.Guid);
                        if (string.IsNullOrWhiteSpace(outputName)) continue;

                        CollectionService.RegisterDiscoveredRecipe(new ItemRecipe(
                            outputName,
                            "Receita carregada do V Rising",
                            output.Amount,
                            ingredients.ToArray()));
                        ImportedRecipes++;
                    }
                }
            }
            finally
            {
                arrays.Dispose();
            }

            _loaded = ImportedRecipes > 0;
            Status = _loaded
                ? ImportedRecipes + " receitas reais carregadas"
                : "Nenhuma receita foi localizada";

            Plugin.Instance.Log.LogInfo("Descoberta de receitas: " + Status + " (" + ScannedRecipePrefabs + " prefabs verificados).");
        }
        catch (Exception ex)
        {
            _loaded = false;
            Status = "Falha ao ler receitas reais";
            Plugin.Instance.Log.LogWarning("Falha na descoberta de receitas: " + ex);
        }
        finally
        {
            _loading = false;
        }
    }
}
