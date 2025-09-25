public sealed class Elevator
{    
    public readonly int Id;

    public int CurrentFloor { get; internal set; }

    public Direction Direction { get; internal set; } = Direction.None;
    internal IElevatorState CurrentState { get; private set; }
    internal ElevatorOptions Settings => ElevatorOptions.Default;
    private readonly ElevatorStops stops = new();

    private readonly Lock _lock = new();

    private bool IsIdle => ReferenceEquals(CurrentState, IdleElevatorState.Instance);

     public Elevator(int id, int startFloor)
    {
        Id = id;
        CurrentFloor = startFloor;
        CurrentState = IdleElevatorState.Instance;

        Log.Add($"Car#{Id} initialized at floor {CurrentFloor}");
    }

    public void CarSelect(int floor)
    {
        using (_lock.EnterScope())
        {
            stops.AddOnboard(floor, CurrentFloor);

            Log.Add($"Car#{Id} destination {floor} added");

            if (IsIdle)
            {
                var next = NearestOnboard();
                if (next != null)
                {
                    Direction = next > CurrentFloor ? Direction.Up : Direction.Down;
                    TransitionTo(Direction);
                    Log.Add($"Car#{Id} starting to move {Direction} from floor {CurrentFloor}");
                }
            }
        }
    }

    public void AssignPickup(int floor, Direction direction)
    {
        using (_lock.EnterScope())
        {
            if (direction == Direction.None)
            {
                Log.Add($"Car has no direction.");
                return;
            }

            stops.AddPickup(floor, direction);

            Log.Add($"Car#{Id} assigned pickup at floor {floor} ({(direction == Direction.Up ? "Up" : "Down")})");

            if (IsIdle && !HasOnboardRequests())
            {
                Direction = floor >= CurrentFloor ? Direction.Up : Direction.Down;
                TransitionTo(Direction);
                Log.Add($"Car#{Id} heading {Direction} toward pickup at floor {floor}");
            }
        }
    }

    public async Task HandleStateAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;

        if (IsIdle && !HasOnboardRequests() && stops.HasPickups())
        {
            var target = NearestPickup();
            Direction = target > CurrentFloor ? Direction.Up : Direction.Down;
            TransitionTo(Direction);
            Log.Add($"Car#{Id} leaving Idle to go {Direction} toward pickup at floor {target}");
        }

        await CurrentState.HandleStateAsync(this, ct);
    }

    internal void ClearCurrentFloorRequests()
    {
        using (_lock.EnterScope())
        {
            stops.ClearAt(CurrentFloor, Direction);
        }
    }

    internal void DeterminePostStopState()
    {
        using (_lock.EnterScope())
        {
            var nextDirection = stops.DetermineNextDirection(Direction, CurrentFloor);

            Direction = nextDirection;
            TransitionTo(nextDirection);
        }
    }

    internal void TransitionTo(Direction direction)
    {
        TransitionTo(direction switch
        {
            Direction.Up => MovingUpElevatorState.Instance,
            Direction.Down => MovingDownElevatorState.Instance,
            _ => IdleElevatorState.Instance
        });
    }

    internal void TransitionTo(IElevatorState state)
    {
        if (state is null)
        {
            Log.Add("Invalid state");
            return;
        }
        CurrentState = state;
    }

    internal void TransitionToIdle() => TransitionTo(IdleElevatorState.Instance);

    internal void TransitionToStopped() => TransitionTo(StoppedElevatorState.Instance);



    // Helpers
    private bool HasOnboardRequests() => stops.HasOnboard();
    internal bool AnyOnboardAbove() => stops.AnyOnboardAbove(CurrentFloor);
    internal bool AnyOnboardBelow() => stops.AnyOnboardBelow(CurrentFloor);
    internal bool HasPickups() => stops.HasPickups();

    internal bool ShouldStopHere(Direction moving)
    {
        using (_lock.EnterScope())
        {
            return stops.ShouldStopAt(CurrentFloor, moving);
        }
    }

    private int? NearestOnboard() => stops.NearestOnboard(CurrentFloor);

    internal int? NearestPickup() => stops.NearestPickup(CurrentFloor);

    public string Snapshot()
    {
        return $"Car#{Id} Floor={CurrentFloor} Direction={CurrentState.Name}";
    }
}