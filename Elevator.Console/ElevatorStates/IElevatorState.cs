internal interface IElevatorState
{
    Task HandleStateAsync(Elevator car, CancellationToken token);
}