using System.Collections.Concurrent;
// ---------------- Logging ----------------
public static class Log
{
    private static readonly ConcurrentQueue<string> _q = new();
    private const int Max = 300;

    public static void Add(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        _q.Enqueue(line);
        while (_q.Count > Max && _q.TryDequeue(out _)) { }
    }

    public static IEnumerable<string> Tail(int n)
    {
        return _q.Reverse().Take(n).Reverse();
    }
}
