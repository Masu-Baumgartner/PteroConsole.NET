using PteroConsole.NET;

Console.WriteLine("Enter panel url:");
var panelUrl = Console.ReadLine();

Console.WriteLine("Enter client key:");
var clientKey = Console.ReadLine();

Console.WriteLine("Enter server uuid:");
var serverUuid = Console.ReadLine();

var console = PterodactylConsole.Create(panelUrl, clientKey, serverUuid);

console.OutputReceived += (sender, msg) =>
{
    Console.WriteLine($"OUTPUT: {msg}");
};

console.StatusChanged += (sender, status) =>
{
    Console.WriteLine($"STATUS: {status}");
};

console.Connect();

Thread.Sleep(-1);