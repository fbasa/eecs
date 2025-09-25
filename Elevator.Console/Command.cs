public static class Command
{
    /// <summary>
    /// Sanity-check—it and parses input, routes it to the proper dispatcher actions
    /// </summary>
    /// <param name="line"></param>
    /// <param name="dispatcher"></param>
    public static void Handle(string? line, Dispatcher dispatcher)
    {
        try
        {
            var settings = ElevatorOptions.Default;

            var parts = (line ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0) return;

            var command = parts[0].ToLowerInvariant();

            switch (command)
            {
                case "u":
                    if (parts.Length < 2)
                    {
                        Log.Add("Usage: u <floor>");
                        break;
                    }

                    if (!TryParseInt(parts[1], "floor", out var upFloor)) break;

                    if (upFloor < settings.MinFloor || upFloor > settings.MaxFloor)
                    {
                        Log.Add($"Floor {upFloor} is out of range for up requests (1-10).");
                        break;
                    }

                    dispatcher.RequestPickup(upFloor, Direction.Up);
                    break;

                case "d":
                    if (parts.Length < 2)
                    {
                        Log.Add("Usage: d <floor>");
                        break;
                    }

                    if (!TryParseInt(parts[1], "floor", out var downFloor)) break;

                    if (downFloor <= settings.MinFloor || downFloor > settings.MaxFloor)
                    {
                        Log.Add($"Floor {downFloor} is out of range for down requests (2-10).");
                        break;
                    }

                    dispatcher.RequestPickup(downFloor,Direction.Down);
                    break;

                case "c":
                    if (parts.Length < 3)
                    {
                        Log.Add("Usage: c <carId> <destination>");
                        break;
                    }

                    if (!TryParseInt(parts[1], "car id", out var carId)) break;
                    if (!TryParseInt(parts[2], "destination floor", out var target)) break;

                    if (!dispatcher.HasCar(carId))
                    {
                        Log.Add($"Unknown car id {carId}.");
                        break;
                    }

                    if (target < settings.MinFloor || target > settings.MaxFloor)
                    {
                        Log.Add($"Floor {target} is out of range (1-10).");
                        break;
                    }

                    dispatcher.CarSelect(carId, target);
                    break;

                default:
                    Log.Add($"Unknown command: {string.Join(' ', parts)}");
                    break;
            }

            bool TryParseInt(string value, string name, out int parsed)
            {
                if (int.TryParse(value, out parsed)) return true;

                Log.Add($"Invalid {name}: '{value}' is not a number.");
                return false;
            }
        }
        catch (Exception ex)
        {
            Log.Add($"Command error: {ex.Message}");
        }
    }
}
