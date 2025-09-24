internal sealed class IdleElevatorState : IElevatorState
{
    public Task HandleStateAsync(Elevator elevator, CancellationToken token)
    {
        return Task.Delay(100, token);
    }
}