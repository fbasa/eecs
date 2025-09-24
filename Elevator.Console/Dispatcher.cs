
// ---------------- Dispatcher ----------------
public sealed class Dispatcher(Elevator[] cars)
{
    private readonly HashSet<(int floor, Direction dir)> pending = new();

    public void HallCallUp(int floor)
    {
        if (floor >= Elevator.MinFloor && floor < Elevator.MaxFloor)
        {
            pending.Add((floor, Direction.Up));
            Log.Add($"UP request on floor {floor} received");
        }
    }
    public void HallCallDown(int floor)
    {
        if (floor > Elevator.MinFloor && floor <= Elevator.MaxFloor)
        {
            pending.Add((floor, Direction.Down));
            Log.Add($"DOWN request on floor {floor} received");
        }
    }

    public void CarButton(int carId, int destination)
    {
        var car = cars.FirstOrDefault(c => c.Id == carId);
        if (car != null)
        {
            car.Move(destination);
            // PressCar logs internally as well
        }
    }

    public void Dispatch()
    {
        if (pending.Count == 0) return;
        var assigned = new List<(int floor, Direction dir)>();
        foreach (var p in pending)
        {
            var (idx, _) = cars
                .Select((c, i) => (i, score: Math.Abs(c.CurrentFloor - p.floor)))
                .OrderBy(t => t.score).First();
            cars[idx].AssignPickup(p.floor, p.dir);
            Log.Add($"Assigned floor {p.floor} ({(p.dir == Direction.Up ? "↑" : "↓")}) to Car#{cars[idx].Id}");
            assigned.Add(p);
        }
        foreach (var a in assigned)
        {
            pending.Remove(a);
        }
    }

    public string Snapshot() => $"HallCalls: [{string.Join(" ", pending.Select(p => $"{p.floor}{(p.dir == Direction.Up ? "↑" : "↓")}"))}]";
}
