internal sealed class MovingDownElevatorState : IElevatorState
{
    public static MovingDownElevatorState Instance { get; } = new();

    public string Name => "MovingDown";

    private MovingDownElevatorState() { }

    /// <summary>
    /// Advances the elevator through one asynchronous step while the state is active.
    /// </summary>
    public async Task HandleStateAsync(Elevator car, CancellationToken token)
    {
        await Task.Delay(car.Settings.TravelPerFloor, token);

        if (car.CurrentFloor > car.Settings.MinFloor)
            car.CurrentFloor--;

        Log.Add($"Car#{car.Id} moved to floor {car.CurrentFloor}");

        if (car.ShouldStopHere(Direction.Down))
        {
            car.TransitionToStopped();
            return;
        }

        if (car.HasTargetsBelow())
            return;

        if (car.HasTargetsAbove())
        {
            car.Direction = Direction.Up;
            car.TransitionTo(Direction.Up);
            Log.Add($"Car#{car.Id} reversing to Up (targets remain above)");
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
