using System.Reflection.Emit;

public sealed class Dispatcher(Elevator[] cars)
{
    private readonly HashSet<(int floor, Direction direction)> pickupRequests = new();

    /// <summary>
    ///  Record a hall-call so it can be assigned later or dispatch
    /// </summary>
    /// <param name="floor"></param>
    /// <param name="direction"></param>
    public void RequestPickup(int floor, Direction direction)
    {
        pickupRequests.Add((floor, direction));
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

    /// <summary>
    /// Assigns each pending pickup request to the closest car and clears the request once dispatched.
    /// </summary>
    public void Dispatch()
    {
        if (pickupRequests.Count == 0) return;

        var assigned = new List<(int floor, Direction direction)>();

        foreach (var request in pickupRequests)
        {
            var (car, _) = cars
                .Where(car => car.CurrentState == IdleElevatorState.Instance)
                .Select((car, i) => (car, distance: Math.Abs(car.CurrentFloor - request.floor)))
                .OrderBy(t => t.distance).FirstOrDefault();

            if (car is null)
            {
                (car, _) = cars
                    .Select((car, i) => (car, distance: Math.Abs(car.CurrentFloor - request.floor)))
                    .OrderBy(t => t.distance).First();
            }

            if (car is not null)
            {
                car.AssignPickup(request.floor, request.direction);

                var label = request.direction == Direction.Up ? "UP" : "DOWN";
                Log.Add($"Assigned floor {request.floor} ({label}) to Car#{car.Id}");
                assigned.Add(request);
            }
        }

        foreach (var req in assigned)
        {
            pickupRequests.Remove(req);
        }
    }
}
