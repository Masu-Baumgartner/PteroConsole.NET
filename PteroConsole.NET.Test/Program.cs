using System.Net;
using Newtonsoft.Json;
using PteroConsole.NET;
using PteroConsole.NET.Test;

Console.WriteLine("Enter panel url:");
var panelUrl = Console.ReadLine();

Console.WriteLine("Enter client key:");
var clientKey = Console.ReadLine();

Console.WriteLine("Enter server uuid:");
var serverUuid = Console.ReadLine();

Console.WriteLine($"Using {panelUrl}api/client/servers/{serverUuid}/websocket");

var console = new PteroConsole.NET.PteroConsole();

console.OnConnectionStateUpdated += (sender, state) =>
{
    Console.WriteLine($"Console status: {state}");
};

console.OnServerResourceUpdated += (sender, resource) =>
{
    Console.WriteLine($"Stats: {resource.Uptime}, State: {resource.State}");
};

console.OnServerStateUpdated += (sender, state) =>
{
    Console.WriteLine($"State: {state}");
};

console.RequestToken += pteroConsole =>
{
    Console.WriteLine("Revoking token");
    var wc = new WebClient();
    wc.Headers.Add("Authorization", "Bearer " + clientKey);
    var raw = wc.DownloadString($"{panelUrl}api/client/servers/{serverUuid}/websocket");
    var data = JsonConvert.DeserializeObject<WebsocketDataResource>(raw).Data;
    return data.Token;
};

console.OnMessage += (sender, s) =>
{
    Console.WriteLine("Output: " + s);
};

var wc = new WebClient();
wc.Headers.Add("Authorization", "Bearer " + clientKey);
var raw = wc.DownloadString($"{panelUrl}api/client/servers/{serverUuid}/websocket");
var data = JsonConvert.DeserializeObject<WebsocketDataResource>(raw).Data;

await console.Connect(panelUrl, data.Socket, data.Token);

Console.ReadLine();

await console.SetPowerState("start");

Console.ReadLine();

await console.EnterCommand("list");

Console.ReadLine();

await console.Disconnect();

Console.ReadLine();