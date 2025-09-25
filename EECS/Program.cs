using System.Collections.Concurrent;

// ---------------------------
// Domain primitives
// ---------------------------
enum Dir { Idle = 0, Up = 1, Down = -1 }

readonly record struct Pickup(int Floor, Dir Dir);

readonly record struct Assignment(int CarId, Pickup Pickup);

readonly record struct CarSnapshot(
    int Id,
    int Floor,
    Dir Dir,
    bool DoorsOpen,
    int OutstandingStops, // onboard + assigned
    IReadOnlyCollection<int> OnboardUp,
    IReadOnlyCollection<int> OnboardDown,
    IReadOnlyCollection<int> PickupUp,
    IReadOnlyCollection<int> PickupDown
);

// ---------------------------
// Request queue
// ---------------------------
sealed class RequestQueue
{
    private readonly ConcurrentQueue<Pickup> _queue = new();

    public void Enqueue(Pickup p)
    {
        _queue.Enqueue(p);
        Console.WriteLine($"[REQ] {p.Dir} request on floor {p.Floor} received");
    }

    public List<Pickup> Drain()
    {
        var list = new List<Pickup>();
        while (_queue.TryDequeue(out var p)) list.Add(p);
        return list;
    }
}

// ---------------------------
// Scheduler (policy-based, pure)
// ---------------------------
interface IScheduler
{
    List<Assignment> Assign(IReadOnlyList<Pickup> requests, IReadOnlyList<CarSnapshot> cars);
}

// Simple "Nearest eligible" with direction-awareness.
// Prefer: cars already heading toward the pickup; then idle; then nearest (fallback).
sealed class NearestEligibleScheduler : IScheduler
{
    public List<Assignment> Assign(IReadOnlyList<Pickup> requests, IReadOnlyList<CarSnapshot> cars)
    {
        var assignments = new List<Assignment>();
        foreach (var req in requests)
        {
            int chosen = -1;
            int bestScore = int.MaxValue; // lower is better

            for (int i = 0; i < cars.Count; i++)
            {
                var c = cars[i];
                var distance = Math.Abs(c.Floor - req.Floor);

                // already has lots of work? add mild penalty
                var loadPenalty = c.OutstandingStops;

                // eligibility scoring
                int score;
                bool sameDirectionToward =
                    c.Dir == req.Dir &&
                    ((req.Dir == Dir.Up && c.Floor <= req.Floor) ||
                     (req.Dir == Dir.Down && c.Floor >= req.Floor));

                if (sameDirectionToward)
                {
                    score = distance * 10 + loadPenalty; // strong preference
                }
                else if (c.Dir == Dir.Idle)
                {
                    score = distance * 20 + loadPenalty * 2; // second choice
                }
                else
                {
                    // moving opposite or away: still possible, but last resort
                    score = distance * 40 + loadPenalty * 3;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    chosen = c.Id;
                }
            }

            if (chosen >= 0)
                assignments.Add(new Assignment(chosen, req));
        }
        return assignments;
    }
}

// ---------------------------
// Car StopPlanner (per-car)
// ---------------------------
sealed class StopPlanner
{
    private readonly int _minFloor;
    private readonly int _maxFloor;

    // Separating by direction keeps "SCAN" behavior simple.
    private readonly SortedSet<int> _onboardUp = new();
    private readonly SortedSet<int> _onboardDown = new();
    private readonly SortedSet<int> _pickupUp = new();
    private readonly SortedSet<int> _pickupDown = new();

    public StopPlanner(int minFloor, int maxFloor)
    {
        _minFloor = minFloor; _maxFloor = maxFloor;
    }

    public void AddPickup(Pickup p)
    {
        if (p.Floor < _minFloor || p.Floor > _maxFloor) return;
        if (p.Dir == Dir.Up) _pickupUp.Add(p.Floor); else _pickupDown.Add(p.Floor);
    }

    public void AddOnboard(int floor, int dest)
    {
        if (dest < _minFloor || dest > _maxFloor || dest == floor) return;
        if (dest > floor) _onboardUp.Add(dest); else _onboardDown.Add(dest);
    }

    public void ClearAt(int floor)
    {
        _onboardUp.Remove(floor);
        _onboardDown.Remove(floor);
        _pickupUp.Remove(floor);
        _pickupDown.Remove(floor);
    }

    public bool HasAnyStops =>
        _onboardUp.Count > 0 || _onboardDown.Count > 0 || _pickupUp.Count > 0 || _pickupDown.Count > 0;

    public int OutstandingStopsCount => _onboardUp.Count + _onboardDown.Count + _pickupUp.Count + _pickupDown.Count;

    public bool ShouldStopHere(int floor, Dir dir)
    {
        // stop if onboard wants this floor (always)
        if (_onboardUp.Contains(floor) || _onboardDown.Contains(floor)) return true;

        // pickups: honor direction (SCAN/collective)
        if (dir == Dir.Up && _pickupUp.Contains(floor)) return true;
        if (dir == Dir.Down && _pickupDown.Contains(floor)) return true;

        // If we are idle (starting), allow pickup in either direction at this floor.
        if (dir == Dir.Idle && (_pickupUp.Contains(floor) || _pickupDown.Contains(floor))) return true;
        return false;
    }

    public Dir NextDirection(Dir current, int floor)
    {
        bool anyAbove = _onboardUp.Any(v => v > floor) || _pickupUp.Any(v => v > floor);
        bool anyBelow = _onboardDown.Any(v => v < floor) || _pickupDown.Any(v => v < floor);

        // If moving with passengers, keep direction while there are stops in that direction.
        bool carryingUp = _onboardUp.Count > 0;
        bool carryingDown = _onboardDown.Count > 0;

        if (current == Dir.Up)
        {
            if (carryingUp || _pickupUp.Any(v => v >= floor)) return Dir.Up;
            if (anyBelow) return Dir.Down;
            return Dir.Idle;
        }
        if (current == Dir.Down)
        {
            if (carryingDown || _pickupDown.Any(v => v <= floor)) return Dir.Down;
            if (anyAbove) return Dir.Up;
            return Dir.Idle;
        }

        // Idle: prefer direction that has nearest stop
        var upNearest = NearestAbove(floor);
        var downNearest = NearestBelow(floor);

        if (upNearest.HasValue && downNearest.HasValue)
            return (upNearest.Value - floor) <= (floor - downNearest.Value) ? Dir.Up : Dir.Down;
        if (upNearest.HasValue) return Dir.Up;
        if (downNearest.HasValue) return Dir.Down;
        return Dir.Idle;
    }

    public int? NearestAbove(int floor)
    {
        int? candidate = null;
        if (_onboardUp.Count > 0) candidate = _onboardUp.FirstOrDefault(v => v >= floor);
        candidate ??= _pickupUp.FirstOrDefault(v => v >= floor);
        return candidate == 0 ? null : candidate;
    }

    public int? NearestBelow(int floor)
    {
        int? candidate = null;
        if (_onboardDown.Count > 0) candidate = _onboardDown.Reverse().FirstOrDefault(v => v <= floor);
        candidate ??= _pickupDown.Reverse().FirstOrDefault(v => v <= floor);
        return candidate == 0 ? null : candidate;
    }

    public IReadOnlyCollection<int> OnboardUp => _onboardUp;
    public IReadOnlyCollection<int> OnboardDown => _onboardDown;
    public IReadOnlyCollection<int> PickupUp => _pickupUp;
    public IReadOnlyCollection<int> PickupDown => _pickupDown;
}

// ---------------------------
// Car Controller (FSM)
// ---------------------------
enum CarState { Idle, MovingUp, MovingDown, DoorOpen }

sealed class CarController
{
    public int Id { get; }
    private readonly StopPlanner _planner;
    private readonly int _minFloor;
    private readonly int _maxFloor;

    // timing
    private const int MoveSecondsPerFloor = 10;
    private const int DoorSeconds = 10;

    public int Floor { get; private set; }
    public CarState State { get; private set; } = CarState.Idle;
    public Dir Dir =>
        State == CarState.MovingUp ? Dir.Up :
        State == CarState.MovingDown ? Dir.Down :
        Dir.Idle;

    private int _secondsUntilNext; // countdown for movement/doors

    public CarController(int id, int startFloor, int minFloor, int maxFloor)
    {
        Id = id;
        Floor = startFloor;
        _minFloor = minFloor;
        _maxFloor = maxFloor;
        _planner = new StopPlanner(minFloor, maxFloor);
    }

    public void Apply(Assignment a)
    {
        if (a.CarId != Id) return;
        _planner.AddPickup(a.Pickup);
    }

    public void AddOnboardDestination(int destFloor)
    {
        _planner.AddOnboard(Floor, destFloor);
        Console.WriteLine($"[CAR {Id}] Passenger onboard selects destination {destFloor}");
    }

    public CarSnapshot Snapshot() =>
        new CarSnapshot(Id, Floor, Dir, State == CarState.DoorOpen, _planner.OutstandingStopsCount,
            _planner.OnboardUp.ToArray(), _planner.OnboardDown.ToArray(),
            _planner.PickupUp.ToArray(), _planner.PickupDown.ToArray());

    public void Tick1s()
    {
        if (_secondsUntilNext > 0)
        {
            _secondsUntilNext--;
            return;
        }

        switch (State)
        {
            case CarState.Idle:
                {
                    if (_planner.ShouldStopHere(Floor, Dir.Idle))
                    {
                        // open doors immediately if there's a pickup here
                        OpenDoors();
                        break;
                    }
                    var next = _planner.NextDirection(Dir.Idle, Floor);
                    if (next == Dir.Up) StartMovingUp();
                    else if (next == Dir.Down) StartMovingDown();
                    // else remain idle
                    break;
                }

            case CarState.MovingUp:
                {
                    // move one floor per 10s
                    Floor = Math.Min(_maxFloor, Floor + 1);
                    Console.WriteLine($"[CAR {Id}] Reached floor {Floor} (moving up)");
                    if (_planner.ShouldStopHere(Floor, Dir.Up))
                    {
                        OpenDoors();
                    }
                    else
                    {
                        // continue if we still have up-direction work; otherwise decide after next tick
                        _secondsUntilNext = MoveSecondsPerFloor;
                        var next = _planner.NextDirection(Dir.Up, Floor);
                        if (next == Dir.Down) StartMovingDown(); // only if no more up-direction work
                    }
                    break;
                }

            case CarState.MovingDown:
                {
                    Floor = Math.Max(_minFloor, Floor - 1);
                    Console.WriteLine($"[CAR {Id}] Reached floor {Floor} (moving down)");
                    if (_planner.ShouldStopHere(Floor, Dir.Down))
                    {
                        OpenDoors();
                    }
                    else
                    {
                        _secondsUntilNext = MoveSecondsPerFloor;
                        var next = _planner.NextDirection(Dir.Down, Floor);
                        if (next == Dir.Up) StartMovingUp();
                    }
                    break;
                }

            case CarState.DoorOpen:
                {
                    // Finish dwell, then choose next direction
                    CloseDoorsAndDecide();
                    break;
                }
        }
    }

    private void StartMovingUp()
    {
        if (Floor >= _maxFloor) { State = CarState.Idle; return; }
        State = CarState.MovingUp;
        _secondsUntilNext = MoveSecondsPerFloor;
        Console.WriteLine($"[CAR {Id}] Departing up from floor {Floor}");
    }

    private void StartMovingDown()
    {
        if (Floor <= _minFloor) { State = CarState.Idle; return; }
        State = CarState.MovingDown;
        _secondsUntilNext = MoveSecondsPerFloor;
        Console.WriteLine($"[CAR {Id}] Departing down from floor {Floor}");
    }

    private void OpenDoors()
    {
        State = CarState.DoorOpen;
        _secondsUntilNext = DoorSeconds;
        Console.WriteLine($"[CAR {Id}] Doors open at floor {Floor} (boarding/alighting)");
        // boarding/alighting happens during the 10s dwell
        // Clear completed stops (any pickup at this floor in current-or-any direction, and onboard)
        _planner.ClearAt(Floor);
    }

    private void CloseDoorsAndDecide()
    {
        Console.WriteLine($"[CAR {Id}] Doors closed at floor {Floor}");
        // Decide where to go next (SCAN-like)
        var next = _planner.NextDirection(Dir, Floor);
        if (next == Dir.Up) StartMovingUp();
        else if (next == Dir.Down) StartMovingDown();
        else
        {
            State = CarState.Idle;
            Console.WriteLine($"[CAR {Id}] Idle at floor {Floor}");
        }
    }
}

// ---------------------------
// Fleet Controller
// ---------------------------
sealed class FleetController
{
    private readonly List<CarController> _cars;
    private readonly int _minFloor;
    private readonly int _maxFloor;

    public FleetController(int numCars, int minFloor, int maxFloor, int startFloor = 1)
    {
        _minFloor = minFloor; _maxFloor = maxFloor;
        _cars = Enumerable.Range(1, numCars)
            .Select(i => new CarController(i, startFloor, minFloor, maxFloor)).ToList();
    }

    public IReadOnlyList<CarSnapshot> Snapshots() => _cars.Select(c => c.Snapshot()).ToList();

    public void ApplyAssignments(IEnumerable<Assignment> assignments)
    {
        foreach (var a in assignments)
            _cars.First(c => c.Id == a.CarId).Apply(a);
    }

    public void Tick1s()
    {
        foreach (var c in _cars) c.Tick1s();
    }

    public bool TryAddOnboard(int carId, int dest)
    {
        var car = _cars.FirstOrDefault(c => c.Id == carId);
        if (car is null) return false;
        if (dest < _minFloor || dest > _maxFloor) return false;
        car.AddOnboardDestination(dest);
        return true;
    }
}

// ---------------------------
// Program wiring (CommandHandler + loop)
// ---------------------------
static class Program
{
    const int MinFloor = 1;
    const int MaxFloor = 10;
    const int Cars = 4;

    static async Task Main()
    {
        Console.WriteLine("Elevator simulation started.");
        Console.WriteLine("Commands:  up <floor> | down <floor> | dest <carId> <floor> | help | quit");
        Console.WriteLine("Example:   up 7   /   down 3   /   dest 1 9");

        var requests = new RequestQueue();
        var scheduler = new NearestEligibleScheduler();
        var fleet = new FleetController(Cars, MinFloor, MaxFloor);

        var cts = new CancellationTokenSource();

        // Input listener
        _ = Task.Run(() => InputLoop(requests, fleet, cts.Token));

        // Main simulation loop (1-second ticks)
        while (!cts.IsCancellationRequested)
        {
            // 1) Drain requests and schedule
            var newRequests = requests.Drain();
            if (newRequests.Count > 0)
            {
                var assigns = scheduler.Assign(newRequests, fleet.Snapshots());
                foreach (var a in assigns)
                    Console.WriteLine($"[SCHED] Assign pickup {a.Pickup.Dir} at floor {a.Pickup.Floor} -> car {a.CarId}");
                fleet.ApplyAssignments(assigns);
            }

            // 2) Tick cars (advance time)
            fleet.Tick1s();

            // 3) Render brief status
            //var snap = fleet.Snapshots();
            //var line = string.Join(" | ", snap.Select(s =>
            //    $"car {s.Id}: floor {s.Floor} {(s.DoorsOpen ? "[doors]" : "")} {(s.Dir == Dir.Idle ? "idle" : s.Dir.ToString().ToLower())}"));
            //Console.WriteLine("[STATE] " + line);

            await Task.Delay(1000);
        }
    }

    static void InputLoop(RequestQueue rq, FleetController fleet, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var line = Console.ReadLine();
            if (line == null) continue;
            var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0) continue;

            switch (parts[0].ToLowerInvariant())
            {
                case "up":
                    if (parts.Length >= 2 && int.TryParse(parts[1], out var fUp))
                        rq.Enqueue(new Pickup(fUp, Dir.Up));
                    else Console.WriteLine("Usage: up <floor>");
                    break;

                case "down":
                    if (parts.Length >= 2 && int.TryParse(parts[1], out var fDown))
                        rq.Enqueue(new Pickup(fDown, Dir.Down));
                    else Console.WriteLine("Usage: down <floor>");
                    break;

                case "dest":
                    if (parts.Length >= 3 &&
                        int.TryParse(parts[1], out var carId) &&
                        int.TryParse(parts[2], out var dest))
                    {
                        if (!fleet.TryAddOnboard(carId, dest))
                            Console.WriteLine("Invalid car or floor. Example: dest 1 9");
                    }
                    else Console.WriteLine("Usage: dest <carId> <floor>");
                    break;

                case "help":
                    Console.WriteLine("Commands: up <floor> | down <floor> | dest <carId> <floor> | quit");
                    break;

                case "quit":
                case "exit":
                    Environment.Exit(0);
                    break;

                default:
                    Console.WriteLine("Unknown command. Try: up 7, down 3, dest 1 9, help, quit");
                    break;
            }
        }
    }
}
