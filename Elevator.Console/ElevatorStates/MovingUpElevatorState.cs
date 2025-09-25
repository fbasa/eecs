internal sealed class MovingUpElevatorState : IElevatorState
{
    public static MovingUpElevatorState Instance { get; } = new();

    public string Name => "MovingUp";

    private MovingUpElevatorState() { }

    public async Task HandleStateAsync(Elevator car, CancellationToken token)
    {
        if (car.ShouldStopHere(Direction.Up))
        {
            car.TransitionToStopped();
            return;
        }

        await Task.Delay(car.Settings.TravelPerFloor, token);

        car.CurrentFloor = Math.Min(car.CurrentFloor + 1, car.Settings.MaxFloor);

        Log.Add($"Car#{car.Id} moved to floor {car.CurrentFloor}");

        if (!car.AnyOnboardAbove())
        {
            if (car.AnyOnboardBelow())
            {
                car.Direction = Direction.Down;
                car.TransitionTo(Direction.Down);
                Log.Add($"Car#{car.Id} reversing to Down (no more onboard up-targets)");
            }
            else if (car.HasPickups())
            {
                var pickup = car.NearestPickup();
                if (pickup == null)
                {
                    car.Direction = Direction.None;
                    car.TransitionToIdle();
                    Log.Add($"Car#{car.Id} idling at floor {car.CurrentFloor}");
                }
                else
                {
                    car.Direction = pickup > car.CurrentFloor ? Direction.Up : Direction.Down;
                    car.TransitionTo(car.Direction);
                    //Log.Add($"Car#{car.Id} heading {car.Direction} toward pickup at floor {pickup}");
                }
            }
            else
            {
                car.Direction = Direction.None;
                car.TransitionToIdle();
                Log.Add($"Car#{car.Id} idling at floor {car.CurrentFloor}");
            }
        }
    }
}

