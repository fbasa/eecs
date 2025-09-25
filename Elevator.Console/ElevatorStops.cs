using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class ElevatorStops
{
    private readonly SortedSet<int> carUp = new();    // > current floor
    private readonly SortedSet<int> carDown = new();  // < current floor

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

    public void ClearAt(int floor, Direction direction)
    {
        carUp.Remove(floor);
        carDown.Remove(floor);

        if (direction == Direction.Up)
        {
            pickUpUp.Remove(floor);
        }
        else if (direction == Direction.Down)
        {
            pickUpDown.Remove(floor);
        }
    }

    public bool HasOnboard() => carUp.Count > 0 || carDown.Count > 0;

    public bool AnyOnboardAbove(int currentFloor) => carUp.Count > 0 && carUp.Min > currentFloor;

    public bool AnyOnboardBelow(int currentFloor) => carDown.Count > 0 && carDown.Max < currentFloor;

    public bool HasPickups() => pickUpUp.Count > 0 || pickUpDown.Count > 0;

    public bool ShouldStopAt(int floor, Direction moving)
    {
        if (carUp.Contains(floor) || carDown.Contains(floor)) return true;
        if (moving == Direction.Up && pickUpUp.Contains(floor)) return true;
        if (moving == Direction.Down && pickUpDown.Contains(floor)) return true;
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
        var onboardAbove = AnyOnboardAbove(currentFloor);
        var onboardBelow = AnyOnboardBelow(currentFloor);

        if (currentDirection == Direction.Up && onboardAbove) return Direction.Up;
        if (currentDirection == Direction.Down && onboardBelow) return Direction.Down;
        if (onboardAbove) return Direction.Up;
        if (onboardBelow) return Direction.Down;

        var pickup = NearestPickup(currentFloor);
        if (pickup.HasValue)
        {
            return pickup.Value > currentFloor ? Direction.Up : Direction.Down;
        }

        return Direction.None;
    }
}

