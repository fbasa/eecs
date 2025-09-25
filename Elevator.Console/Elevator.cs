public sealed class Elevator
{    
    public readonly int Id;
    public int CurrentFloor { get; internal set; }
    public Direction Direction { get; internal set; } = Direction.None;
    internal IElevatorState CurrentState { get; private set; }
    internal ElevatorOptions Settings => ElevatorOptions.Default;
    private readonly ElevatorRequestHandler RequestHandler = new();
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
            RequestHandler.AddOnboard(floor, CurrentFloor);

            Log.Add($"Car#{Id} destination (floor {floor}) added");

            if (IsIdle)
            {
                var next = RequestHandler.NearestOnboard(CurrentFloor);
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

            RequestHandler.AddPickup(floor, direction);

            //Log.Add($"Car#{Id} assigned pickup at floor {floor} ({(direction == Direction.Up ? "Up" : "Down")})");

            if (IsIdle && !RequestHandler.HasOnboard())
            {
                Direction = floor >= CurrentFloor ? Direction.Up : Direction.Down;
                TransitionTo(Direction);
                //Log.Add($"Car#{Id} heading {Direction} toward pickup at floor {floor}");
            }
        }
    }

    public async Task HandleStateAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;

        if (IsIdle && !RequestHandler.HasOnboard() && RequestHandler.HasPickups())
        {
            var target = RequestHandler.NearestPickup(CurrentFloor);
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
            RequestHandler.ClearAt(CurrentFloor, Direction);
        }
    }

    internal void DeterminePostStopState()
    {
        using (_lock.EnterScope())
        {
            var nextDirection = RequestHandler.DetermineNextDirection(Direction, CurrentFloor);

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

    private void TransitionTo(IElevatorState state)
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

    internal bool AnyOnboardAbove() => RequestHandler.AnyOnboardAbove(CurrentFloor);
    internal bool AnyOnboardBelow() => RequestHandler.AnyOnboardBelow(CurrentFloor);
    internal bool HasPickups() => RequestHandler.HasPickups();

    internal bool ShouldStopHere(Direction moving)
    {
        using (_lock.EnterScope())
        {
            return RequestHandler.ShouldStopAt(CurrentFloor, moving);
        }
    }

    internal int? NearestPickup() => RequestHandler.NearestPickup(CurrentFloor);

    public string Snapshot()
    {
        return $"Car#{Id} Floor={CurrentFloor} Direction={CurrentState.Name}";
    }
}