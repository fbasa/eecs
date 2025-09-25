public sealed class Elevator
{
    public readonly int Id;
    public const int MinFloor = 1, MaxFloor = 10;
    public int CurrentFloor { get; internal set; }

    public readonly TimeSpan TravelPerFloor = TimeSpan.FromSeconds(10); 
    public readonly TimeSpan Dwell = TimeSpan.FromSeconds(10);          

    public ElevatorState ElevatorState { get; internal set; } = ElevatorState.Idle;
    public Direction Direction { get; internal set; } = Direction.None;

    private readonly SortedSet<int> carUp = new();    // > CurrentFloor
    private readonly SortedSet<int> carDown = new();  // < CurrentFloor

    private readonly SortedSet<int> pickUpUp = new();
    private readonly SortedSet<int> pickUpDown = new();

    Lock _lock = new();

    private readonly IReadOnlyDictionary<ElevatorState, IElevatorState> elevatorStates;

    public Elevator(int id, int startFloor)
    {
        Id = id;
        CurrentFloor = startFloor;
        Log.Add($"Car#{Id} initialized at floor {CurrentFloor}");

        elevatorStates = new Dictionary<ElevatorState, IElevatorState>
        {
            [ElevatorState.Idle] = new IdleElevatorState(),
            [ElevatorState.MovingUp] = new MovingUpElevatorState(),
            [ElevatorState.MovingDown] = new MovingDownElevatorState(),
            [ElevatorState.Stopped] = new StoppedElevatorState()
        };
    }

    public void CarSelect(int floor)
    {
        using (_lock.EnterScope())
        {
            if (floor > CurrentFloor)
            {
                carUp.Add(floor);
            }
            else if (floor < CurrentFloor)
            {
                carDown.Add(floor);
            }
            else
            {
                carUp.Add(floor); // immediate stop case
            }

            Log.Add($"Car#{Id}: destination {floor} added");

            if (ElevatorState == ElevatorState.Idle)
            {
                var next = NearestOnboard();
                if (next != null)
                {
                    Direction = next > CurrentFloor ? Direction.Up : Direction.Down;
                    ElevatorState = Direction == Direction.Up ? ElevatorState.MovingUp : ElevatorState.MovingDown;
                    Log.Add($"Car#{Id} starting to move {Direction} from floor {CurrentFloor}");
                }
            }
        }
    }

    public void AssignPickup(int floor, Direction dir)
    {
        using (_lock.EnterScope())
        {
            if (dir == Direction.Up)
            {
                pickUpUp.Add(floor);
            }
            else if (dir == Direction.Down)
            {
                pickUpDown.Add(floor);
            }
            
            Log.Add($"Car#{Id} assigned pickup at floor {floor} ({(dir == Direction.Up ? "?" : "?")})");

            if (ElevatorState == ElevatorState.Idle && !HasOnboardLocked())
            {
                Direction = floor >= CurrentFloor ? Direction.Up : Direction.Down;
                ElevatorState = Direction == Direction.Up ? ElevatorState.MovingUp : ElevatorState.MovingDown;
                Log.Add($"Car#{Id} heading {Direction} toward pickup at floor {floor}");
            }
        }
    }


    public async Task HandleStateAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;

        TryActivateFromIdleAssignments();

        if (!elevatorStates.TryGetValue(ElevatorState, out var handler))
        {
            Log.Add($"No handler configured for state {ElevatorState}");
            return;
        }

        await handler.HandleStateAsync(this, ct);
    }

    private void TryActivateFromIdleAssignments()
    {
        using (_lock.EnterScope())
        {
            if (ElevatorState == ElevatorState.Idle && !HasOnboardLocked() && (pickUpUp.Count > 0 || pickUpDown.Count > 0))
            {
                var target = NearestPickup();
                if (target != null)
                {
                    Direction = target > CurrentFloor ? Direction.Up : Direction.Down;
                    ElevatorState = Direction == Direction.Up ? ElevatorState.MovingUp : ElevatorState.MovingDown;
                    Log.Add($"Car#{Id} leaving Idle to go {Direction} toward pickup at floor {target}");
                }
            }
        }
    }

    internal void ClearCurrentFloorRequests()
    {
        using (_lock.EnterScope())
        {
            carUp.Remove(CurrentFloor);
            carDown.Remove(CurrentFloor);
            if (Direction == Direction.Up) pickUpUp.Remove(CurrentFloor);
            if (Direction == Direction.Down) pickUpDown.Remove(CurrentFloor);
        }
    }

    internal void DeterminePostStopState()
    {
        using (_lock.EnterScope())
        {
            var nextDirection = Direction.None;

            var onboardAbove = AnyOnboardAbove();
            var onboardBelow = AnyOnboardBelow();

            if (Direction == Direction.Up && onboardAbove)
            {
                nextDirection = Direction.Up;
            }
            else if (Direction == Direction.Down && onboardBelow)
            {
                nextDirection = Direction.Down;
            }
            else if (onboardAbove)
            {
                nextDirection = Direction.Up;
            }
            else if (onboardBelow)
            {
                nextDirection = Direction.Down;
            }
            else
            {
                var pickup = NearestPickup();
                if (pickup != null)
                {
                    nextDirection = pickup > CurrentFloor ? Direction.Up : Direction.Down;
                }
            }

            Direction = nextDirection;
            ElevatorState = nextDirection switch
            {
                Direction.Up => ElevatorState.MovingUp,
                Direction.Down => ElevatorState.MovingDown,
                _ => ElevatorState.Idle
            };
        }
    }

    // Helpers
    private bool HasOnboardLocked() => carUp.Count > 0 || carDown.Count > 0;
    internal bool AnyOnboardAbove() => carUp.Count > 0 && carUp.Min > CurrentFloor;
    internal bool AnyOnboardBelow() => carDown.Count > 0 && carDown.Max() < CurrentFloor;
    internal bool HasPickups() => pickUpUp.Count > 0 || pickUpDown.Count > 0;

    internal bool ShouldStopHere(Direction moving)
    {
        using (_lock.EnterScope())
        {
            if (carUp.Contains(CurrentFloor) || carDown.Contains(CurrentFloor)) return true;
            if (moving == Direction.Up && pickUpUp.Contains(CurrentFloor)) return true;
            if (moving == Direction.Down && pickUpDown.Contains(CurrentFloor)) return true;
            return false;
        }
    }

    private int? NearestOnboard()
    {
        int? cUp = carUp.Count > 0 ? carUp.OrderBy(f => Math.Abs(f - CurrentFloor)).First() : null;
        int? cDn = carDown.Count > 0 ? carDown.OrderBy(f => Math.Abs(f - CurrentFloor)).First() : null;
        if (cUp is null) return cDn;
        if (cDn is null) return cUp;
        return Math.Abs(cUp.Value - CurrentFloor) <= Math.Abs(cDn.Value - CurrentFloor) ? cUp : cDn;
    }

    internal int? NearestPickup()
    {
        var ups = pickUpUp.Select(f => (f, Math.Abs(f - CurrentFloor)));
        var dns = pickUpDown.Select(f => (f, Math.Abs(f - CurrentFloor)));
        var all = ups.Concat(dns).OrderBy(t => t.Item2).ToList();
        return all.Count > 0 ? all[0].f : (int?)null;
    }

    public string Snapshot()
    {
        using (_lock.EnterScope())
        {
            string cu = string.Join(",", carUp);
            string cd = string.Join(",", carDown.Reverse());
            string pu = string.Join(",", pickUpUp);
            string pd = string.Join(",", pickUpDown.Reverse());
            return $"Car#{Id} F={CurrentFloor} {ElevatorState} Dir={Direction} | Onboard(?:{cu} ?:{cd}) Pickups(?:{pu} ?:{pd})";
        }
    }
}

