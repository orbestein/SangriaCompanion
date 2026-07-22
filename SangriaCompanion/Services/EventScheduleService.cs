using SangriaEventAlerts;
namespace SangriaCompanion;
internal readonly record struct CompanionEventOccurrence(string Name, TimeSpan Start, TimeSpan UntilStart, bool IsActive, TimeSpan UntilEnd, string Description);
internal static class EventScheduleService
{
    internal static bool IsSynchronized => SangriaEventApi.GetState(1).Synchronized;
    internal static TimeSpan ServerTime => SangriaEventApi.GetState(1).ServerTime;
    internal static CompanionEventOccurrence GetNextOccurrence()
    {
        var s=SangriaEventApi.GetState(1); var x=s.Occurrences.FirstOrDefault();
        return x==null ? new("Aguardando servidor",TimeSpan.Zero,TimeSpan.Zero,false,TimeSpan.Zero,"Sincronizando horário oficial do Sangria...") : new(x.Name,x.Start,x.UntilStart,x.Active,x.UntilEnd,x.Description);
    }
    internal static IReadOnlyList<CompanionEventOccurrence> GetUpcoming(int count)=>SangriaEventApi.GetState(count).Occurrences.Select(x=>new CompanionEventOccurrence(x.Name,x.Start,x.UntilStart,x.Active,x.UntilEnd,x.Description)).ToArray();
    internal static string FormatCountdown(TimeSpan value){var seconds=Math.Max(0,(int)Math.Ceiling(value.TotalSeconds)); return seconds>=3600?$"{seconds/3600:00}:{seconds/60%60:00}:{seconds%60:00}":$"{seconds/60:00}:{seconds%60:00}";}
}
