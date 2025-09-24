internal sealed class MovingDownElevatorState : IElevatorState
{
    public async Task HandleStateAsync(Elevator car, CancellationToken token)
    {
        await Task.Delay(car.TravelPerFloor, token);

        car.CurrentFloor = Math.Max(car.CurrentFloor - 1, Elevator.MinFloor);

        Log.Add($"Car#{car.Id} moved to floor {car.CurrentFloor}");

        if (car.ShouldStopHere(Direction.Down))
        {
            car.ElevatorState = ElevatorState.Stopped;
            return;
        }

        if (!car.AnyOnboardBelow())
        {
            if (car.AnyOnboardAbove())
            {
                car.Direction = Direction.Up;
                car.ElevatorState = ElevatorState.MovingUp;
                Log.Add($"Car#{car.Id} reversing to Up (no more onboard down-targets)");
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