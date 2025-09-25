internal sealed class MovingDownElevatorState : IElevatorState
{
    public static MovingDownElevatorState Instance { get; } = new();

    public string Name => "MovingDown";

    private MovingDownElevatorState() { }

    /// <summary>
    /// Advances the elevator through one asynchronous step while the state is active.
    /// </summary>
    /// <param name="car"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task HandleStateAsync(Elevator car, CancellationToken token)
    {
        // Simulate travel (cancellable)
        await Task.Delay(car.Settings.TravelPerFloor, token).ConfigureAwait(false);

        // Move down one floor, clamped
        if (car.CurrentFloor > car.Settings.MinFloor)
            car.CurrentFloor--;

        Log.Add($"Car#{car.Id} moved to floor {car.CurrentFloor}");

        // Stop if this floor is a target on the way down
        if (car.ShouldStopHere(Direction.Down))
        {
            car.TransitionToStopped();
            return;
        }

        // If anyone onboard wants to go lower, keep current "Down" state (no transition)
        if (car.AnyOnboardBelow())
            return;

        // No more down-targets onboard. If there are up-targets onboard, reverse.
        if (car.AnyOnboardAbove())
        {
            car.Direction = Direction.Up;
            car.TransitionTo(Direction.Up);
            Log.Add($"Car#{car.Id} reversing to Up (no more onboard down-targets)");
            return;
        }

        // Nobody onboard needs anything. Check pickups.
        if (car.HasPickups())
        {
            var pickup = car.NearestPickup(); // assume int? (null if none)
            if (pickup != null)
            {
                car.Direction = pickup > car.CurrentFloor ? Direction.Up : Direction.Down;
                car.TransitionTo(car.Direction);
                return;
            }
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

