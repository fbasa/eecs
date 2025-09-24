internal sealed class MovingUpElevatorState : IElevatorState
{
    public async Task HandleStateAsync(Elevator car, CancellationToken token)
    {
        await Task.Delay(car.TravelPerFloor, token);

        car.CurrentFloor = Math.Min(car.CurrentFloor + 1, Elevator.MaxFloor);

        Log.Add($"Car#{car.Id} moved to floor {car.CurrentFloor}");

        if (car.ShouldStopHere(Direction.Up))
        {
            car.ElevatorState = ElevatorState.Stopped;
            return;
        }

        if (!car.AnyOnboardAbove())
        {
            if (car.AnyOnboardBelow())
            {
                car.Direction = Direction.Down;
                car.ElevatorState = ElevatorState.MovingDown;
                Log.Add($"Car#{car.Id} reversing to Down (no more onboard up-targets)");
            }
            else if (car.HasPickups())
            {
                var pickup = car.NearestPickup();
                if (pickup == null)
                {
                    car.Direction = Direction.None;
                    car.ElevatorState = ElevatorState.Idle;
                    Log.Add($"Car#{car.Id} idling at floor {car.CurrentFloor}");
                }
                else
                {
                    car.Direction = pickup > car.CurrentFloor ? Direction.Up : Direction.Down;
                    car.ElevatorState = car.Direction == Direction.Up ? ElevatorState.MovingUp : ElevatorState.MovingDown;
                    Log.Add($"Car#{car.Id} heading {car.Direction} toward pickup at floor {pickup}");
                }
            }
            else
            {
                car.Direction = Direction.None;
                car.ElevatorState = ElevatorState.Idle;
                Log.Add($"Car#{car.Id} idling at floor {car.CurrentFloor}");
            }
        }
    }
}