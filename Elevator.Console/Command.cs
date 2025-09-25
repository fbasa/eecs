public static class Command
{
    public static void Validate(string? line, Dispatcher dispatcher)
    {
        try
        {
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

                    if (upFloor < Elevator.MinFloor || upFloor > Elevator.MaxFloor)
                    {
                        Log.Add($"Floor {upFloor} is out of range for up requests ({Elevator.MinFloor}-{Elevator.MaxFloor - 1}).");
                        break;
                    }

                    dispatcher.MoveUp(upFloor);
                    break;

                case "d":
                    if (parts.Length < 2)
                    {
                        Log.Add("Usage: d <floor>");
                        break;
                    }

                    if (!TryParseInt(parts[1], "floor", out var downFloor)) break;

                    if (downFloor <= Elevator.MinFloor || downFloor > Elevator.MaxFloor)
                    {
                        Log.Add($"Floor {downFloor} is out of range for down requests ({Elevator.MinFloor + 1}-{Elevator.MaxFloor}).");
                        break;
                    }

                    dispatcher.MoveDown(downFloor);
                    break;

                case "c":
                    if (parts.Length < 3)
                    {
                        Log.Add("Usage: c <carId> <destination>");
                        break;
                    }

                    if (!TryParseInt(parts[1], "car id", out var carId)) break;
                    if (!TryParseInt(parts[2], "destination floor", out var destination)) break;

                    if (!dispatcher.HasCar(carId))
                    {
                        Log.Add($"Unknown car id {carId}.");
                        break;
                    }

                    if (destination < Elevator.MinFloor || destination > Elevator.MaxFloor)
                    {
                        Log.Add($"Floor {destination} is out of range ({Elevator.MinFloor}-{Elevator.MaxFloor}).");
                        break;
                    }

                    dispatcher.CarSelect(carId, destination);
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
