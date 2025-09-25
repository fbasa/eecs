internal sealed class IdleElevatorState : IElevatorState
{
    public static IdleElevatorState Instance { get; } = new();

    public string Name => "Idle";

    private IdleElevatorState() { }

    public Task HandleStateAsync(Elevator elevator, CancellationToken token)
    {
        return Task.Delay(100, token);
    }
}
