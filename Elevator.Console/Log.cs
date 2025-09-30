using System.Collections.Concurrent;

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

    /// <summary>
    /// Lets callers read the most recent log entries. 
    /// It walks the in-memory queue in reverse so it can pull the latest n items, 
    /// then reverses again before returning, 
    /// ensuring you get the newest lines in chronological 
    /// order without mutating or dequeuing them.
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public static IEnumerable<string> Tail(int n)
    {
        return _q.Reverse().Take(n).Reverse();
    }
}
