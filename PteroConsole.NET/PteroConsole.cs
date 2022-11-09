using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using PteroConsole.NET.Data;
using PteroConsole.NET.Enums;

namespace PteroConsole.NET;

public class PteroConsole : IDisposable
{
    // Internal fields
    private readonly ClientWebSocket WebSocket;
    private Task ConsoleTask;
    private bool CloseRequested = false;

    // Functions
    public Func<PteroConsole, string> RequestToken;

    // Events
    public EventHandler<ConnectionState> OnConnectionStateUpdated;
    public EventHandler<ServerState> OnServerStateUpdated;
    public EventHandler<ServerResource> OnServerResourceUpdated;
    public EventHandler<string> OnMessage;
    public EventHandler<string> OnDebugMessage;

    // Public fields
    public ConnectionState ConnectionState { get; private set; }
    public ServerState ServerState { get; private set; }
    public ServerResource ServerResource { get; private set; }

    public PteroConsole()
    {
        WebSocket = new();
        ConnectionState = ConnectionState.Disconnected;
        ServerState = ServerState.Offline;
        ServerResource = new()
        {
            Network = new()
            {
                RxBytes = 0,
                TxBytes = 0
            },
            State = "offline",
            Uptime = 0,
            CpuAbsolute = 0,
            DiskBytes = 0,
            MemoryBytes = 0,
            MemoryLimitBytes = 0
        };
    }

    public async Task Connect(string origin, string socket, string token)
    {
        UpdateConnectionState(ConnectionState.Connecting);

        WebSocket.Options.SetRequestHeader("Origin", "https://" + new Uri(origin).Host);
        WebSocket.Options.SetRequestHeader("Authorization", "Bearer " + token);

        ConsoleTask = Task.Run(async () =>
        {
            while (true)
            {
                if(CloseRequested)
                    break;
                
                UpdateConnectionState(ConnectionState.Connecting);
                await WebSocket.ConnectAsync(new Uri(socket), CancellationToken.None);
                
                if (WebSocket.State != WebSocketState.Connecting && WebSocket.State != WebSocketState.Open)
                {
                    UpdateConnectionState(ConnectionState.Disconnected);
                    break;
                }
                
                UpdateConnectionState(ConnectionState.Connected);

                var st = new SendToken();
                st.Args.Add(token);
                await SendRaw(JsonConvert.SerializeObject(st));

                while (WebSocket.State == WebSocketState.Open ||
                       WebSocket.State == WebSocketState.Connecting)
                {
                    if(CloseRequested)
                        break;

                    try
                    {
                        var raw = await ReceiveRaw();
                        var eventData = JsonConvert.DeserializeObject<BaseEvent>(raw);
                        
                        if(eventData == null)
                            continue;
                        
                        switch (eventData.Event)
                        {
                            case "jwt error":
                                await WebSocket.CloseAsync(WebSocketCloseStatus.Empty, "Jwt error detected", CancellationToken.None);
                                UpdateServerState(ServerState.Offline);
                                UpdateConnectionState(ConnectionState.Disconnected);
                                break;
                            
                            case "token expired":
                                await WebSocket.CloseAsync(WebSocketCloseStatus.Empty, "Jwt error detected", CancellationToken.None);
                                UpdateServerState(ServerState.Offline);
                                UpdateConnectionState(ConnectionState.Disconnected);
                                break;
                            
                            case "token expiring":
                                token = RequestToken.Invoke(this);
                                st = new SendToken();
                                st.Args.Add(token);
                                await SendRaw(JsonConvert.SerializeObject(st));
                                break;
                            
                            case "auth success":
                                await SendRaw("{\"event\":\"send logs\",\"args\":[null]}");
                                await SendRaw("{\"event\":\"send stats\",\"args\":[null]}");
                                break;
                            
                            case "stats":
                                var stats = JsonConvert.DeserializeObject<ServerResource>(eventData.Args[0]);
                                
                                if(stats == null)
                                    break;

                                var serverState = ParseServerState(stats.State);
                                
                                if(ServerState != serverState)
                                    UpdateServerState(serverState);
                                
                                UpdateServerResource(stats);
                                break;
                            
                            case "status":
                                var serverStateParsed = ParseServerState(eventData.Args[0]);
                                
                                if(ServerState != serverStateParsed)
                                    UpdateServerState(serverStateParsed);
                                break;
                            
                            case "console output":
                                foreach (var line in eventData.Args)
                                {
                                    OnMessage?.Invoke(this, line);
                                }
                                break;
                            
                            case "install output":
                                foreach (var line in eventData.Args)
                                {
                                    OnMessage?.Invoke(this, line);
                                }
                                break;
                            
                            case "daemon message":
                                foreach (var line in eventData.Args)
                                {
                                    OnMessage?.Invoke(this, line);
                                }
                                break;
                            
                            case "install started":
                                UpdateServerState(ServerState.Installing);
                                break;
                            
                            case "install completed":
                                UpdateServerState(ServerState.Offline);
                                break;
                            
                        }
                    }
                    catch (Exception e)
                    {
                        OnMessage?.Invoke(this, "Internal: " + e.Message);
                    }
                }
                
                await WebSocket.CloseAsync(WebSocketCloseStatus.Empty, "Closed", CancellationToken.None);
                WebSocket.Dispose();
                ConsoleTask.Dispose();
                UpdateConnectionState(ConnectionState.Disconnected);
            }
        });
    }

    public async Task Disconnect()
    {
        CloseRequested = true;

        Task.Run(async () =>
        {
            await Task.Delay(1000);

            if (WebSocket.State == WebSocketState.Open)
                await WebSocket.CloseAsync(WebSocketCloseStatus.Empty, "", CancellationToken.None);
            
            ConsoleTask.Dispose();
            WebSocket.Dispose();
        });
    }

    public async Task EnterCommand(string content)
    {
        if (ConnectionState == ConnectionState.Connected)
        {
            await SendRaw("{\"event\":\"send command\",\"args\":[\"" + content + "\"]}");
        }
    }

    public async Task SetPowerState(string state)
    {
        if (ConnectionState == ConnectionState.Connected)
        {
            await SendRaw("{\"event\":\"set state\",\"args\":[\"" + state + "\"]}");
        }
    }

    private void UpdateConnectionState(ConnectionState connectionState)
    {
        ConnectionState = connectionState;
        OnConnectionStateUpdated?.Invoke(this, ConnectionState);
    }

    private void UpdateServerState(ServerState serverState)
    {
        ServerState = serverState;
        OnServerStateUpdated?.Invoke(this, ServerState);
    }
    
    private void UpdateServerResource(ServerResource serverResource)
    {
        ServerResource = serverResource;
        OnServerResourceUpdated?.Invoke(this, ServerResource);
    }

    private async Task SendRaw(string text)
    {
        if (WebSocket.State == WebSocketState.Open)
        {
            byte[] byteContentBuffer = Encoding.UTF8.GetBytes(text);
            await WebSocket.SendAsync(new ArraySegment<byte>(byteContentBuffer), WebSocketMessageType.Text, true,
                CancellationToken.None);
        }
    }

    private async Task<string> ReceiveRaw()
    {
        ArraySegment<byte> receivedBytes = new ArraySegment<byte>(new byte[1024]);
        WebSocketReceiveResult result = await WebSocket.ReceiveAsync(receivedBytes, CancellationToken.None);
        return Encoding.UTF8.GetString(receivedBytes.Array, 0, result.Count);
    }

    private ServerState ParseServerState(string raw)
    {
        switch (raw)
        {
            case "offline":
                return ServerState.Offline;
            case "starting":
                return ServerState.Starting;
            case "running":
                return ServerState.Running;
            case "stopping":
                return ServerState.Stopping;
            case "installing":
                return ServerState.Installing;
            default:
                return ServerState.Offline;
        }
    }

    public async void Dispose()
    {
        await WebSocket.CloseAsync(WebSocketCloseStatus.Empty, "Closed", CancellationToken.None);
        WebSocket.Dispose();
        ConsoleTask.Dispose();
    }
}