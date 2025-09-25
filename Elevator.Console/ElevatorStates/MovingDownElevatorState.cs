internal sealed class MovingDownElevatorState : IElevatorState
{
    public static MovingDownElevatorState Instance { get; } = new();

    public string Name => "MovingDown";

    private MovingDownElevatorState() { }

    public async Task HandleStateAsync(Elevator car, CancellationToken token)
    {
        await Task.Delay(car.Settings.TravelPerFloor, token);

        car.CurrentFloor = Math.Max(car.CurrentFloor - 1, car.Settings.MinFloor);

        Log.Add($"Car#{car.Id} moved to floor {car.CurrentFloor}");

        if (car.ShouldStopHere(Direction.Down))
        {
            car.TransitionToStopped();
            return;
        }

        if (!car.AnyOnboardBelow())
        {
            if (car.AnyOnboardAbove())
            {
                car.Direction = Direction.Up;
                car.TransitionTo(Direction.Up);
                Log.Add($"Car#{car.Id} reversing to Up (no more onboard down-targets)");
            }
            else if (car.HasPickups())
            {
                var pickup = car.NearestPickup();
                if (pickup == null)
                {
                    car.Direction = Direction.None;
                    car.TransitionToIdle();
                    Log.Add($"Car#{car.Id} idling at floor pickup == null {car.CurrentFloor}");
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
                Log.Add($"Car#{car.Id} idling at floor AnyOnboardBelow! {car.CurrentFloor}");
            }
        }
    }
}

