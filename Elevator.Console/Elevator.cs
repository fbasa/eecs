
// ---------------- Elevator ----------------
public sealed class Elevator
{
    public readonly int Id;
    public const int MinFloor = 1, MaxFloor = 10;

    public readonly TimeSpan TravelPerFloor = TimeSpan.FromSeconds(10); // spec
    public readonly TimeSpan Dwell = TimeSpan.FromSeconds(10);          // spec

    public int CurrentFloor { get; private set; }
    public EState State { get; private set; } = EState.Idle;
    public Direction Direction { get; private set; } = Direction.None;

    // Onboard passengers’ targets (directional commitment)
    private readonly SortedSet<int> carUp = new();    // > CurrentFloor
    private readonly SortedSet<int> carDown = new();  // < CurrentFloor

    // Assigned hall pickups (floors to stop at, direction-sensitive)
    private readonly SortedSet<int> pickUpUp = new();
    private readonly SortedSet<int> pickUpDown = new();

    private readonly object _lock = new();

    public Elevator(int id, int startFloor)
    {
        Id = id;
        CurrentFloor = startFloor;
        Log.Add($"Car#{Id} initialized at floor {CurrentFloor}");
    }

    // Car select (destinations) – represent onboard passengers
    public void CarSelect(int floor)
    {
        if (floor < MinFloor || floor > MaxFloor) return;
        lock (_lock)
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

            if (State == EState.Idle)
            {
                var next = NearestOnboard();
                if (next != null)
                {
                    Direction = next > CurrentFloor ? Direction.Up : Direction.Down;
                    State = Direction == Direction.Up ? EState.MovingUp : EState.MovingDown;
                    Log.Add($"Car#{Id} starting to move {Direction} from floor {CurrentFloor}");
                }
            }
        }
    }

    // Dispatcher assigns hall pickups (does not break commitment)
    public void AssignPickup(int floor, Direction dir)
    {
        if (floor < MinFloor || floor > MaxFloor) return;
        lock (_lock)
        {
            if (dir == Direction.Up && floor < MaxFloor)
            {
                pickUpUp.Add(floor);
            }

            if (dir == Direction.Down && floor > MinFloor)
            {
                pickUpDown.Add(floor);
            }
            
            Log.Add($"Car#{Id} assigned pickup at floor {floor} ({(dir == Direction.Up ? "↑" : "↓")})");

            if (State == EState.Idle && !HasOnboardLocked())
            {
                Direction = floor >= CurrentFloor ? Direction.Up : Direction.Down;
                State = Direction == Direction.Up ? EState.MovingUp : EState.MovingDown;
                Log.Add($"Car#{Id} heading {Direction} toward pickup at floor {floor}");
            }
        }
    }


    // Main step (10s/floor travel, 10s dwell)
    public async Task StepAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;

        lock (_lock)
        {
            if (State == EState.Idle && !HasOnboardLocked() && (pickUpUp.Count > 0 || pickUpDown.Count > 0))
            {
                var target = NearestPickup();
                if (target != null)
                {
                    Direction = target > CurrentFloor ? Direction.Up : Direction.Down;
                    State = Direction == Direction.Up ? EState.MovingUp : EState.MovingDown;
                    Log.Add($"Car#{Id} leaving Idle to go {Direction} toward pickup at floor {target}");
                }
            }
        }

        switch (State)
        {
            case EState.Idle:
                await Task.Delay(100, ct);
                return;

            case EState.MovingUp:
                await Task.Delay(TravelPerFloor, ct);

                CurrentFloor = Math.Min(CurrentFloor + 1, MaxFloor);

                Log.Add($"Car#{Id} moved to floor {CurrentFloor}");

                if (ShouldStopHere(Direction.Up)) State = EState.Stopped;
                else
                {
                    if (!AnyOnboardAbove())
                    {
                        if (AnyOnboardBelow()) 
                        { 
                            Direction = Direction.Down; 
                            State = EState.MovingDown; 
                            Log.Add($"Car#{Id} reversing to Down (no more onboard up-targets)"); 
                        }
                        else if (HasPickups())
                        {
                            var p = NearestPickup();
                            if (p == null) 
                            { 
                                Direction = Direction.None; 
                                State = EState.Idle; 
                                Log.Add($"Car#{Id} idling at floor {CurrentFloor}"); 
                            }
                            else 
                            { 
                                Direction = p > CurrentFloor ? Direction.Up : Direction.Down; 
                                State = Direction == Direction.Up ? EState.MovingUp : EState.MovingDown; 
                                Log.Add($"Car#{Id} heading {Direction} toward pickup at floor {p}"); 
                            }
                        }
                        else 
                        { 
                            Direction = Direction.None; 
                            State = EState.Idle; 
                            Log.Add($"Car#{Id} idling at floor {CurrentFloor}"); 
                        }
                    }
                }
                return;

            case EState.MovingDown:

                await Task.Delay(TravelPerFloor, ct);
                CurrentFloor = Math.Max(CurrentFloor - 1, MinFloor);

                Log.Add($"Car#{Id} moved to floor {CurrentFloor}");

                if (ShouldStopHere(Direction.Down))
                {
                    State = EState.Stopped;
                }
                else
                {
                    if (!AnyOnboardBelow())
                    {
                        if (AnyOnboardAbove()) 
                        { 
                            Direction = Direction.Up; 
                            State = EState.MovingUp; 
                            Log.Add($"Car#{Id} reversing to Up (no more onboard down-targets)"); 
                        }
                        else if (HasPickups())
                        {
                            var p = NearestPickup();
                            if (p == null) 
                            { 
                                Direction = Direction.None; 
                                State = EState.Idle; 
                                Log.Add($"Car#{Id} idling at floor {CurrentFloor}"); 
                            }
                            else 
                            { 
                                Direction = p > CurrentFloor ? Direction.Up : Direction.Down; 
                                State = Direction == Direction.Up ? EState.MovingUp : EState.MovingDown; 
                                Log.Add($"Car#{Id} heading {Direction} toward pickup at floor {p}"); 
                            }
                        }
                        else 
                        { 
                            Direction = Direction.None; 
                            State = EState.Idle; 
                            Log.Add($"Car#{Id} idling at floor {CurrentFloor}"); 
                        }
                    }
                }
                return;

            case EState.Stopped:
                Log.Add($"Car#{Id} stop at {CurrentFloor} (board/alight)");
                await Task.Delay(Dwell, ct);
                lock (_lock)
                {
                    carUp.Remove(CurrentFloor);
                    carDown.Remove(CurrentFloor);
                    if (Direction == Direction.Up) pickUpUp.Remove(CurrentFloor);
                    if (Direction == Direction.Down) pickUpDown.Remove(CurrentFloor);
                }
                lock (_lock)
                {
                    if (Direction == Direction.Up && AnyOnboardAbove()) 
                    { 
                        State = EState.MovingUp; 
                    }
                    else if (Direction == Direction.Down && AnyOnboardBelow()) 
                    { 
                        State = EState.MovingDown; 
                    }
                    else if (AnyOnboardAbove()) 
                    { 
                        Direction = Direction.Up; 
                        State = EState.MovingUp; 
                    }
                    else if (AnyOnboardBelow()) 
                    { 
                        Direction = Direction.Down; 
                        State = EState.MovingDown; 
                    }
                    else if (HasPickups())
                    {
                        var p = NearestPickup();
                        if (p == null) 
                        { 
                            Direction = Direction.None; 
                            State = EState.Idle; 
                        }
                        else 
                        { 
                            Direction = p > CurrentFloor ? Direction.Up : Direction.Down; 
                            State = Direction == Direction.Up ? EState.MovingUp : EState.MovingDown; 
                        }
                    }
                    else 
                    { 
                        Direction = Direction.None; 
                        State = EState.Idle; 
                    }
                }
                return;
        }
    }

    // Helpers
    private bool HasOnboardLocked() => carUp.Count > 0 || carDown.Count > 0;
    private bool AnyOnboardAbove() => carUp.Count > 0 && carUp.Min > CurrentFloor;
    private bool AnyOnboardBelow() => carDown.Count > 0 && carDown.Max() < CurrentFloor;
    private bool HasPickups() => pickUpUp.Count > 0 || pickUpDown.Count > 0;

    private bool ShouldStopHere(Direction moving)
    {
        lock (_lock)
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

    private int? NearestPickup()
    {
        var ups = pickUpUp.Select(f => (f, Math.Abs(f - CurrentFloor)));
        var dns = pickUpDown.Select(f => (f, Math.Abs(f - CurrentFloor)));
        var all = ups.Concat(dns).OrderBy(t => t.Item2).ToList();
        return all.Count > 0 ? all[0].f : (int?)null;
    }

    public string Snapshot()
    {
        lock (_lock)
        {
            string cu = string.Join(",", carUp);
            string cd = string.Join(",", carDown.Reverse());
            string pu = string.Join(",", pickUpUp);
            string pd = string.Join(",", pickUpDown.Reverse());
            return $"Car#{Id} F={CurrentFloor} {State} Dir={Direction} | Onboard(↑:{cu} ↓:{cd}) Pickups(↑:{pu} ↓:{pd})";
        }
    }
}
