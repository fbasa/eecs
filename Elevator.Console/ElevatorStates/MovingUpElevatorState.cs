internal sealed class MovingUpElevatorState : IElevatorState
{
    public static MovingUpElevatorState Instance { get; } = new();

    public string Name => "MovingUp";

    private MovingUpElevatorState() { }

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

        // Move up one floor, clamped
        if (car.CurrentFloor < car.Settings.MaxFloor)
            car.CurrentFloor++;

        Log.Add($"Car#{car.Id} moved to floor {car.CurrentFloor}");

        // Stop if this floor is a target on the way up
        if (car.ShouldStopHere(Direction.Up))
        {
            car.TransitionToStopped();
            return;
        }

        // If anyone onboard wants to go higher, keep current "Up" state (no transition)
        if (car.AnyOnboardAbove())
            return;

        // No more up-targets onboard. If there are down-targets onboard, reverse.
        if (car.AnyOnboardBelow())
        {
            car.Direction = Direction.Down;
            car.TransitionTo(Direction.Down);
            Log.Add($"Car#{car.Id} reversing to Down (no more onboard up-targets)");
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

