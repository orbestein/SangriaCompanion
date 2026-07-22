using UnityEngine;

namespace SangriaCompanion;

/// <summary>
/// Isola falhas dos módulos. Um erro em inventário, receitas ou rastreador
/// não pode derrubar o botão SC, as notificações ou os demais módulos.
/// </summary>
internal static class ModuleRuntime
{
    private static readonly Dictionary<string, bool> Enabled = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> LastErrors = new(StringComparer.OrdinalIgnoreCase);

    internal static bool IsEnabled(string name)
        => !Enabled.TryGetValue(name, out var enabled) || enabled;

    internal static string GetStatus(string name)
    {
        if (IsEnabled(name)) return "OK";
        return LastErrors.TryGetValue(name, out var error) ? "ERRO: " + error : "DESATIVADO";
    }

    internal static void Run(string name, Action action)
    {
        if (!IsEnabled(name)) return;

        try
        {
            action();
        }
        catch (Exception ex)
        {
            Enabled[name] = false;
            LastErrors[name] = ex.GetType().Name + ": " + ex.Message;
            Plugin.Instance.Log.LogError($"[Módulo:{name}] desativado após falha isolada: {ex}");
            NotificationCenter.Enqueue(
                "MÓDULO DESATIVADO",
                name + " apresentou erro. Os demais recursos continuam ativos.",
                SCTheme.Blood,
                8f,
                "Sistema");
        }
    }

    internal static void Retry(string name)
    {
        Enabled[name] = true;
        LastErrors.Remove(name);
        Plugin.Instance.Log.LogInfo($"[Módulo:{name}] reativado para nova tentativa.");
    }
}
