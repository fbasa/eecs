internal sealed class MovingUpElevatorState : IElevatorState
{
    public static MovingUpElevatorState Instance { get; } = new();

    public string Name => "MovingUp";

    private MovingUpElevatorState() { }

    public async Task HandleStateAsync(Elevator car, CancellationToken token)
    {
        await Task.Delay(car.Settings.TravelPerFloor, token);

        car.CurrentFloor = Math.Min(car.CurrentFloor + 1, car.Settings.MaxFloor);

        Log.Add($"Car#{car.Id} moved to floor {car.CurrentFloor}");

        if (car.ShouldStopHere(Direction.Up))
        {
            car.TransitionToStopped();
            return;
        }

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
                if (pickup != null)
                {
                    car.Direction = pickup > car.CurrentFloor ? Direction.Up : Direction.Down;
                    car.TransitionTo(car.Direction);
                }
                else
                {
                    SetCarToIdle(car);
                }
            }
            else
            {
                SetCarToIdle(car);
            }
        }
    }

    private static void SetCarToIdle(Elevator car)
    {
        car.Direction = Direction.None;
        car.TransitionToIdle();
        Log.Add($"Car#{car.Id} idling at floor {car.CurrentFloor}");
    }
}

