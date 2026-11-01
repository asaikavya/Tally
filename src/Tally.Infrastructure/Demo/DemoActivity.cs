namespace Tally.Infrastructure.Demo;

// When did a visitor last DO something (any POST)? Kept in memory: a restart means "it's been quiet".
public static class DemoActivity
{
    private static long _ticks;   // 0 = nothing since the app started

    public static void Touch() => Interlocked.Exchange(ref _ticks, DateTime.UtcNow.Ticks);

    public static DateTime? LastUtc
    {
        get
        {
            var t = Interlocked.Read(ref _ticks);
            return t == 0 ? null : new DateTime(t, DateTimeKind.Utc);
        }
    }
}