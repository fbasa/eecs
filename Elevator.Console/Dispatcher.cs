public sealed class Dispatcher(Elevator[] cars)
{
    private readonly HashSet<(int floor, Direction direction)> pending = new();

    public void RequestPickup(int floor, Direction direction)
    {
        pending.Add((floor, direction));
        var label = direction == Direction.Up ? "UP" : "DOWN";
        Log.Add($"{label} request on floor {floor} received");
    }

    public void CarSelect(int carId, int floor)
    {
        var car = cars.FirstOrDefault(c => c.Id == carId);
        if (car != null)
        {
            car.CarSelect(floor);
        }
    }

    public bool HasCar(int carId) => cars.Any(c => c.Id == carId);

    public void Dispatch()
    {
        if (pending.Count == 0) return;

        var assigned = new List<(int floor, Direction direction)>();

        foreach (var p in pending)
        {
            var (i, _) = cars
                .Select((car, i) => (i, score: Math.Abs(car.CurrentFloor - p.floor)))
                .OrderBy(t => t.score).First();

            cars[i].AssignPickup(p.floor, p.direction);

            var label = p.direction == Direction.Up ? "UP" : "DOWN";
            Log.Add($"Assigned floor {p.floor} ({label}) to Car#{cars[i].Id}");
            assigned.Add(p);
        }

        foreach (var a in assigned)
        {
            pending.Remove(a);
        }
    }
}
