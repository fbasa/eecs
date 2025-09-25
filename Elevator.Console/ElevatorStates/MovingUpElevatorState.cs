internal sealed class MovingUpElevatorState : IElevatorState
{
    public static MovingUpElevatorState Instance { get; } = new();

    public string Name => "MovingUp";

    private MovingUpElevatorState() { }

    /// <summary>
    /// Advances the elevator through one asynchronous step while the state is active.
    /// </summary>
    public async Task HandleStateAsync(Elevator car, CancellationToken token)
    {
        await Task.Delay(car.Settings.TravelPerFloor, token);

        if (car.CurrentFloor < car.Settings.MaxFloor)
            car.CurrentFloor++;

        Log.Add($"Car#{car.Id} moved to floor {car.CurrentFloor}");

        if (car.ShouldStopHere(Direction.Up))
        {
            car.TransitionToStopped();
            return;
        }

        if (car.HasTargetsAbove())
            return;

        if (car.HasTargetsBelow())
        {
            car.Direction = Direction.Down;
            car.TransitionTo(Direction.Down);
            Log.Add($"Car#{car.Id} reversing to Down (targets remain below)");
            return;
        }

        SetCarToIdle(car);
    }

    private static void SetCarToIdle(Elevator car)
    {
        car.Direction = Direction.None;
        car.TransitionToIdle();
        Log.Add($"Car#{car.Id} idling at floor {car.CurrentFloor}");
    }
}
