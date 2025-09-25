var cts = new CancellationTokenSource();

//Initialize cars by id and floor
var cars = new[]
{
    new Elevator(1, 8),
    new Elevator(2, 6),
    new Elevator(3, 7),
    new Elevator(4, 9),
};

var dispatcher = new Dispatcher(cars);

StartDispatcher(cts, dispatcher);

StartElevators(cts, cars);

await StartDashboardAsync(cts, cars, dispatcher);


//-------------------Commands Sample---------------------------------//
// Ex 1: passenger is in 1st floor and wants to to go down in 9th floor
// u 1     -> call for "Up", say car 2 (nearest) will go down to 1st floor for pickup, if car 2 is in 6th floor, 5 x 10s = 50s waiting time
// c 2 9   -> next, instruct car 2 to go to 9th floor
//
//
// Ex 2: passenger is in 10th floor and wants to go down in 5th floor
// d 10     -> call for "Down", say car 4 (nearest) will go up to 10th floor for pickup
// c 4 5    -> next, instruct car 4 to go to 5th floor
//-------------------------------------------------------------------//

static void StartDispatcher(CancellationTokenSource cts, Dispatcher dispatcher)
{
    _ = Task.Run(async () =>
    {
        while (!cts.IsCancellationRequested)
        {
            dispatcher.Dispatch();
            await Task.Delay(250, cts.Token);
        }
    });
}

static void StartElevators(CancellationTokenSource cts, Elevator[] cars)
{
    foreach (var car in cars)
    {
        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                await car.HandleStateAsync(cts.Token);
            }
        });
    }
}

static async Task StartDashboardAsync(CancellationTokenSource cts, Elevator[] cars, Dispatcher dispatcher)
{
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
                Command.Handle(line, dispatcher);
                break;
            }
            await Task.Delay(50, cts.Token);
        }
    }
}