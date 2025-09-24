public static class Command
{
    public static void Parse(string? line, Dispatcher dispatcher)
    {
        try
        {
            var parts = (line ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0) return;

            switch (parts[0].ToLowerInvariant())
            {
                case "u":
                    if (parts.Length >= 2 && int.TryParse(parts[1], out var fu)) dispatcher.HallCallUp(fu);
                    break;
                case "d":
                    if (parts.Length >= 2 && int.TryParse(parts[1], out var fd)) dispatcher.HallCallDown(fd);
                    break;
                case "c":
                    if (parts.Length >= 3 && int.TryParse(parts[1], out var car) && int.TryParse(parts[2], out var fc))
                        dispatcher.CarButton(car, fc);
                    break;
                default:
                    Log.Add($"Unknown command: {string.Join(' ', parts)}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Add($"Command error: {ex.Message}");
        }
    }
}
