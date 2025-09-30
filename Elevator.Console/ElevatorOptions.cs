
internal sealed class ElevatorOptions
{
    public int MinFloor => 1;
    public int MaxFloor => 10;
    public TimeSpan TravelPerFloor => TimeSpan.FromSeconds(5);
    public TimeSpan Dwell => TimeSpan.FromSeconds(5);
    public static ElevatorOptions Default { get; } = new();
}

