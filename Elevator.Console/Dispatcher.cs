
public sealed class Dispatcher(Elevator[] cars)
{
    private readonly HashSet<(int floor, Direction direction)> pending = new();

    public void MoveUp(int floor)
    {
        if (floor >= Elevator.MinFloor && floor < Elevator.MaxFloor)
        {
            pending.Add((floor, Direction.Up));
            Log.Add($"UP request on floor {floor} received");
        }
    }
    public void MoveDown(int floor)
    {
        if (floor > Elevator.MinFloor && floor <= Elevator.MaxFloor)
        {
            pending.Add((floor, Direction.Down));
            Log.Add($"DOWN request on floor {floor} received");
        }
    }

    public void CarSelect(int carId, int destination)
    {
        var car = cars.FirstOrDefault(c => c.Id == carId);
        if (car != null)
        {
            car.CarSelect(destination);
        }
    }

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

            Log.Add($"Assigned floor {p.floor} ({(p.direction == Direction.Up ? "↑" : "↓")}) to Car#{cars[i].Id}");
            assigned.Add(p);
        }

        foreach (var a in assigned)
        {
            pending.Remove(a);
        }
    }
}
