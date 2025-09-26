using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ElevatorConsole.Tests;

[TestClass]
public class ElevatorTests
{
    [TestMethod]
    public void Constructor_SetsInitialState()
    {
        var elevator = new Elevator(id: 1, startFloor: 3);

        Assert.AreEqual(1, elevator.Id);
        Assert.AreEqual(3, elevator.CurrentFloor);
        Assert.AreEqual(Direction.None, elevator.Direction);
        Assert.AreSame(IdleElevatorState.Instance, elevator.CurrentState);
        Assert.AreEqual("Car#1 Floor=3 Direction=Idle", elevator.Snapshot());
    }

    [TestMethod]
    public void CarSelect_AddsUpwardDestinationAndTransitions()
    {
        var elevator = new Elevator(id: 2, startFloor: 1);

        elevator.CarSelect(5);

        Assert.AreEqual(Direction.Up, elevator.Direction);
        Assert.AreSame(MovingUpElevatorState.Instance, elevator.CurrentState);
        Assert.AreEqual("Car#2 Floor=1 Direction=MovingUp", elevator.Snapshot());
    }

    [TestMethod]
    public void CarSelect_AddsDownwardDestinationAndTransitions()
    {
        var elevator = new Elevator(id: 3, startFloor: 5);

        elevator.CarSelect(2);

        Assert.AreEqual(Direction.Down, elevator.Direction);
        Assert.AreSame(MovingDownElevatorState.Instance, elevator.CurrentState);
        Assert.AreEqual("Car#3 Floor=5 Direction=MovingDown", elevator.Snapshot());
    }

    [TestMethod]
    public void AssignPickup_IgnoresWhenDirectionUndefined()
    {
        var elevator = new Elevator(id: 4, startFloor: 4);

        elevator.AssignPickup(6, Direction.None);

        Assert.AreEqual(Direction.None, elevator.Direction);
        Assert.AreSame(IdleElevatorState.Instance, elevator.CurrentState);
    }

    [TestMethod]
    public void AssignPickup_WhenIdleMovesTowardUpRequest()
    {
        var elevator = new Elevator(id: 5, startFloor: 1);

        elevator.AssignPickup(3, Direction.Up);

        Assert.AreEqual(Direction.Up, elevator.Direction);
        Assert.AreSame(MovingUpElevatorState.Instance, elevator.CurrentState);
    }

    [TestMethod]
    public void AssignPickup_WhenIdleMovesTowardDownRequest()
    {
        var elevator = new Elevator(id: 6, startFloor: 5);

        elevator.AssignPickup(2, Direction.Down);

        Assert.AreEqual(Direction.Down, elevator.Direction);
        Assert.AreSame(MovingDownElevatorState.Instance, elevator.CurrentState);
    }

    [TestMethod]
    public async Task AdvanceAsync_WithCancelledToken_ExitsImmediately()
    {
        var elevator = new Elevator(id: 7, startFloor: 2);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await elevator.AdvanceAsync(cts.Token);

        Assert.AreEqual(Direction.None, elevator.Direction);
        Assert.AreSame(IdleElevatorState.Instance, elevator.CurrentState);
    }

    [TestMethod]
    public void ClearCurrentFloorRequests_RemovesStopsAtCurrentFloor()
    {
        var elevator = new Elevator(id: 8, startFloor: 1);
        elevator.CarSelect(4);
        elevator.CurrentFloor = 4;

        Assert.IsTrue(elevator.ShouldStopHere(Direction.Up));

        elevator.ClearCurrentFloorRequests();

        Assert.IsFalse(elevator.ShouldStopHere(Direction.Up));
    }

    [TestMethod]
    public void DeterminePostStopState_SelectsNewDirectionBasedOnRemainingRequests()
    {
        var elevator = new Elevator(id: 9, startFloor: 5);
        elevator.CarSelect(7);
        elevator.AssignPickup(2, Direction.Down);
        elevator.CurrentFloor = 7;
        elevator.ClearCurrentFloorRequests();

        Assert.AreEqual(Direction.Up, elevator.Direction);

        elevator.DeterminePostStopState();

        Assert.AreEqual(Direction.Down, elevator.Direction);
        Assert.AreSame(MovingDownElevatorState.Instance, elevator.CurrentState);
    }
}
