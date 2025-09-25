/// <summary>
/// The paired sets let the car treat committed destinations separately from requests waiting in the lobby.
/// </summary>
internal sealed class ElevatorRequestHandler
{
    private readonly SortedSet<int> carUp = new();
    private readonly SortedSet<int> carDown = new();
    private readonly SortedSet<int> pickUpUp = new();
    private readonly SortedSet<int> pickUpDown = new();

    public void AddOnboard(int floor, int currentFloor)
    {
        if (floor > currentFloor)
        {
            carUp.Add(floor);
        }
        else if (floor < currentFloor)
        {
            carDown.Add(floor);
        }
        else
        {
            carUp.Add(floor);
        }
    }

    public void AddPickup(int floor, Direction direction)
    {
        switch (direction)
        {
            case Direction.Up:
                pickUpUp.Add(floor);
                break;
            case Direction.Down:
                pickUpDown.Add(floor);
                break;
        }
    }

    public void ClearAt(int floor)
    {
        carUp.Remove(floor);
        carDown.Remove(floor);
        pickUpUp.Remove(floor);
        pickUpDown.Remove(floor);
    }

    public bool HasOnboard() => carUp.Count > 0 || carDown.Count > 0;
    public bool HasPickups() => pickUpUp.Count > 0 || pickUpDown.Count > 0;

    public bool AnyOnboardAbove(int currentFloor) => HasAbove(carUp, currentFloor);
    public bool AnyOnboardBelow(int currentFloor) => HasBelow(carDown, currentFloor);

    public bool HasPickupAbove(int currentFloor) => HasAbove(pickUpUp, currentFloor) || HasAbove(pickUpDown, currentFloor);
    public bool HasPickupBelow(int currentFloor) => HasBelow(pickUpUp, currentFloor) || HasBelow(pickUpDown, currentFloor);

    public bool HasTargetsAbove(int currentFloor) => AnyOnboardAbove(currentFloor) || HasPickupAbove(currentFloor);
    public bool HasTargetsBelow(int currentFloor) => AnyOnboardBelow(currentFloor) || HasPickupBelow(currentFloor);

    public bool ShouldStopAt(int floor, Direction moving)
    {
        if (carUp.Contains(floor) || carDown.Contains(floor)) return true;
        if (pickUpUp.Contains(floor) || pickUpDown.Contains(floor)) return true;
        return false;
    }

    public int? NearestOnboard(int currentFloor)
    {
        int? closestUp = carUp.Count > 0 ? carUp.OrderBy(f => Math.Abs(f - currentFloor)).First() : null;
        int? closestDown = carDown.Count > 0 ? carDown.OrderBy(f => Math.Abs(f - currentFloor)).First() : null;

        if (closestUp is null) return closestDown;
        if (closestDown is null) return closestUp;

        return Math.Abs(closestUp.Value - currentFloor) <= Math.Abs(closestDown.Value - currentFloor)
            ? closestUp
            : closestDown;
    }

    public int? NearestPickup(int currentFloor)
    {
        var ups = pickUpUp.Select(f => (floor: f, distance: Math.Abs(f - currentFloor)));
        var downs = pickUpDown.Select(f => (floor: f, distance: Math.Abs(f - currentFloor)));
        var all = ups.Concat(downs).OrderBy(t => t.distance).ToList();
        return all.Count > 0 ? all[0].floor : (int?)null;
    }

    public Direction DetermineNextDirection(Direction currentDirection, int currentFloor)
    {
        var targetsAbove = HasTargetsAbove(currentFloor);
        var targetsBelow = HasTargetsBelow(currentFloor);

        if (currentDirection == Direction.Up && targetsAbove) return Direction.Up;
        if (currentDirection == Direction.Down && targetsBelow) return Direction.Down;

        if (targetsAbove && !targetsBelow) return Direction.Up;
        if (targetsBelow && !targetsAbove) return Direction.Down;

        if (!targetsAbove && !targetsBelow)
            return Direction.None;

        var nextOnboard = NearestOnboard(currentFloor);
        var nextPickup = NearestPickup(currentFloor);

        int? candidate = null;
        if (nextOnboard.HasValue && nextPickup.HasValue)
        {
            var onboardDistance = Math.Abs(nextOnboard.Value - currentFloor);
            var pickupDistance = Math.Abs(nextPickup.Value - currentFloor);
            candidate = onboardDistance <= pickupDistance ? nextOnboard : nextPickup;
        }
        else
        {
            candidate = nextOnboard ?? nextPickup;
        }

        if (!candidate.HasValue)
            return Direction.None;

        return candidate.Value > currentFloor ? Direction.Up : Direction.Down;
    }

    private static bool HasAbove(SortedSet<int> floors, int currentFloor) => floors.Count > 0 && floors.Max > currentFloor;
    private static bool HasBelow(SortedSet<int> floors, int currentFloor) => floors.Count > 0 && floors.Min < currentFloor;
}



