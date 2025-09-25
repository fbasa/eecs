internal sealed class StoppedElevatorState : IElevatorState
{
    public static StoppedElevatorState Instance { get; } = new();

    public string Name => "Stopped";

    private StoppedElevatorState() { }

    public async Task HandleStateAsync(Elevator car, CancellationToken token)
    {
        Log.Add($"Car#{car.Id} stop at {car.CurrentFloor} (board/alight)");
        await Task.Delay(car.Settings.Dwell, token);

        car.ClearCurrentFloorRequests();
        car.DeterminePostStopState();
    }
}
