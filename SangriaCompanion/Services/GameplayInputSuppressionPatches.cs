using System.Reflection;
using HarmonyLib;

namespace SangriaCompanion;

/// <summary>
/// Camada de bloqueio específica do V Rising. Em vez de depender somente do
/// UnityEngine.Input, interrompe os sistemas ECS que transformam os controles
/// físicos em comandos do personagem enquanto o painel principal está aberto.
///
/// A descoberta é dinâmica para tolerar pequenas diferenças entre versões do
/// jogo. Nenhuma falha aqui impede o carregamento do Companion.
/// </summary>
internal static class GameplayInputSuppressionPatches
{
    private static readonly string[] CandidateTypes =
    {
        "ProjectM.CreateInputCommandsSystem",
        "ProjectM.GameplayInputSystem",
        "ProjectM.InputCommandStateCopySystem"
    };

    internal static void Apply(Harmony harmony)
    {
        var prefix = new HarmonyMethod(typeof(GameplayInputSuppressionPatches), nameof(Prefix));
        var patched = 0;

        foreach (var typeName in CandidateTypes)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                Plugin.Instance.Log.LogDebug($"Sistema de entrada não localizado: {typeName}");
                continue;
            }

            foreach (var method in FindUpdateMethods(type))
            {
                try
                {
                    harmony.Patch(method, prefix: prefix);
                    patched++;
                    Plugin.Instance.Log.LogInfo($"Bloqueio de gameplay aplicado: {type.FullName}.{method.Name}");
                }
                catch (Exception ex)
                {
                    Plugin.Instance.Log.LogDebug($"Patch de gameplay ignorado em {type.FullName}.{method.Name}: {ex.Message}");
                }
            }
        }

        Plugin.Instance.Log.LogInfo($"Bloqueio interno do V Rising: {patched} método(s) protegido(s).");
    }

    private static IEnumerable<MethodBase> FindUpdateMethods(Type type)
    {
        var seen = new HashSet<MethodBase>();

        // Sistemas ECS normalmente expõem OnUpdate sem parâmetros.
        foreach (var method in AccessTools.GetDeclaredMethods(type))
        {
            if (method.GetParameters().Length != 0) continue;
            if (!method.Name.Equals("OnUpdate", StringComparison.Ordinal) &&
                !method.Name.Contains("__OnUpdate", StringComparison.Ordinal)) continue;
            if (seen.Add(method)) yield return method;
        }

        var direct = AccessTools.Method(type, "OnUpdate", Type.EmptyTypes);
        if (direct != null && seen.Add(direct)) yield return direct;
    }

    private static bool Prefix()
    {
        // Ao retornar false, o sistema não cria/copia comandos de movimento,
        // ataque, habilidade ou interação naquele frame.
        return !InputBlockService.ShouldSuppressGameplayInput;
    }
}
