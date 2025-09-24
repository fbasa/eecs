
var cts = new CancellationTokenSource();

var cars = new[]
{
    new Elevator(1, 1),
    new Elevator(2, 10),
    new Elevator(3, 3),
    new Elevator(4, 6),
};

var dispatcher = new Dispatcher(cars);

_ = Task.Run(async () =>
{
    while (!cts.IsCancellationRequested)
    {
        dispatcher.Dispatch();
        await Task.Delay(250, cts.Token);
    }
});

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

while (!cts.IsCancellationRequested)
{
    Console.Clear();
    Console.WriteLine("Elevator (4 cars, 10 floors, 10s/floor travel, 10s dwell)");
    Console.WriteLine(new string('-', 78));

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
        await Task.Delay(50, cts.Token);
    }
}
