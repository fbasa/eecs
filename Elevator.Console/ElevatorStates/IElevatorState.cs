internal interface IElevatorState
{
    string Name { get; }
    Task HandleStateAsync(Elevator car, CancellationToken token);
}
