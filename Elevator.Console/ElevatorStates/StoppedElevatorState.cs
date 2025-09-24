internal sealed class StoppedElevatorState : IElevatorState
{
    public async Task HandleStateAsync(Elevator car, CancellationToken token)
    {
        Log.Add($"Car#{car.Id} stop at {car.CurrentFloor} (board/alight)");
        await Task.Delay(car.Dwell, token);

        car.ClearCurrentFloorRequests();
        car.DeterminePostStopState();
    }
}