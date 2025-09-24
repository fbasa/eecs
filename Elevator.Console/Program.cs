
var cts = new CancellationTokenSource();

var cars = new[]
{
    new Elevator(1, 1),
    new Elevator(2, 2),
    new Elevator(3, 3),
    new Elevator(4, 4),
};

var dispatcher = new Dispatcher(cars);

// Dispatch loop
_ = Task.Run(async () =>
{
    while (!cts.IsCancellationRequested)
    {
        dispatcher.Dispatch();
        await Task.Delay(250, cts.Token);
    }
});

// Car loops
foreach (var car in cars)
{
    _ = Task.Run(async () =>
    {
        while (!cts.IsCancellationRequested)
        {
            await car.StepAsync(cts.Token);
        }
    });
}

// Console dashboard + log tail
while (!cts.IsCancellationRequested)
{
    Console.Clear();
    Console.WriteLine("Elevator (4 cars, 10 floors, 10s/floor travel, 10s dwell)");
    Console.WriteLine("Rule: keep direction while onboard targets remain ahead; no ping-ponging.");
    Console.WriteLine(new string('-', 78));
    Console.WriteLine(dispatcher.Snapshot());

    foreach (var car in cars)
    {
        Console.WriteLine(car.Snapshot());
    }

    Console.WriteLine(new string('-', 78));
    Console.WriteLine("Commands: u <f> | d <f> | c <carId> <dest>   (Ctrl+C to quit)");
    Console.Write("> ");

    // Print event log
    Console.WriteLine();
    Console.WriteLine("Events:");

    foreach (var line in Log.Tail(14))
    {
        Console.WriteLine(line);
    }

    // Non-blocking input window
    var start = DateTime.UtcNow;
    while ((DateTime.UtcNow - start).TotalMilliseconds < 900)
    {
        if (Console.KeyAvailable)
        {
            var line = Console.ReadLine();
            if (line == null) break;
            Command.Parse(line, dispatcher);
            break;
        }
        await Task.Delay(500, cts.Token);
    }
}
