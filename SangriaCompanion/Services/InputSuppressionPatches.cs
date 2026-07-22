using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SangriaCompanion;

/// <summary>
/// Bloqueia o Input legado enquanto o mouse está sobre alguma área do Companion
/// ou enquanto um campo de pesquisa está ativo.
/// </summary>
[HarmonyPatch]
internal static class InputBoolSuppressionPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var signatures = new (string Name, Type[] Args)[]
        {
            (nameof(Input.GetMouseButton), new[] { typeof(int) }),
            (nameof(Input.GetMouseButtonDown), new[] { typeof(int) }),
            (nameof(Input.GetMouseButtonUp), new[] { typeof(int) }),
            (nameof(Input.GetKey), new[] { typeof(KeyCode) }),
            (nameof(Input.GetKeyDown), new[] { typeof(KeyCode) }),
            (nameof(Input.GetKeyUp), new[] { typeof(KeyCode) }),
            (nameof(Input.GetButton), new[] { typeof(string) }),
            (nameof(Input.GetButtonDown), new[] { typeof(string) }),
            (nameof(Input.GetButtonUp), new[] { typeof(string) })
        };

        foreach (var signature in signatures)
        {
            var method = AccessTools.Method(typeof(Input), signature.Name, signature.Args);
            if (method != null) yield return method;
        }
    }

    private static bool Prefix(ref bool __result)
    {
        if (!InputBlockService.ShouldSuppressGameplayInput) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch]
internal static class InputAxisSuppressionPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var axis = AccessTools.Method(typeof(Input), nameof(Input.GetAxis), new[] { typeof(string) });
        var raw = AccessTools.Method(typeof(Input), nameof(Input.GetAxisRaw), new[] { typeof(string) });
        if (axis != null) yield return axis;
        if (raw != null) yield return raw;
    }

    private static bool Prefix(ref float __result)
    {
        if (!InputBlockService.ShouldSuppressGameplayInput) return true;
        __result = 0f;
        return false;
    }
}

/// <summary>
/// O V Rising usa principalmente o novo Unity Input System. A aplicação é feita
/// método a método, com isolamento de falhas, para que uma diferença de versão
/// nunca impeça o carregamento da HUD.
/// </summary>
internal static class InputSystemSuppressionPatches
{
    private static readonly HashSet<string> AcceptedNames = new(StringComparer.Ordinal)
    {
        "IsPressed",
        "WasPressedThisFrame",
        "WasReleasedThisFrame",
        "ReadValueAsButton"
    };

    internal static void Apply(Harmony harmony)
    {
        var prefix = new HarmonyMethod(typeof(InputSystemSuppressionPatches), nameof(BoolPrefix));
        var patched = 0;

        foreach (var typeName in new[]
                 {
                     "UnityEngine.InputSystem.InputAction",
                     "UnityEngine.InputSystem.InputActionState"
                 })
        {
            var type = AccessTools.TypeByName(typeName);
            if (type == null) continue;

            foreach (var method in AccessTools.GetDeclaredMethods(type))
            {
                if (method.ReturnType != typeof(bool) || !AcceptedNames.Contains(method.Name)) continue;

                try
                {
                    harmony.Patch(method, prefix: prefix);
                    patched++;
                }
                catch (Exception ex)
                {
                    Plugin.Instance.Log.LogDebug($"Patch de entrada ignorado em {typeName}.{method.Name}: {ex.Message}");
                }
            }
        }

        Plugin.Instance.Log.LogInfo($"Bloqueio do novo Input System: {patched} método(s) protegido(s).");
    }

    private static bool BoolPrefix(ref bool __result)
    {
        if (!InputBlockService.ShouldSuppressGameplayInput) return true;
        __result = false;
        return false;
    }
}
