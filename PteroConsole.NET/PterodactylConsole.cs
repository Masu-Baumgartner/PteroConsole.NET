using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using PteroConsole.NET.Enums;
using PteroConsole.NET.Models;
using PteroConsole.NET.Models.Events;
using RestSharp;

namespace PteroConsole.NET;

public class PterodactylConsole : IDisposable
{
    public static PterodactylConsole Create(string panelUrl, string clientKey, string serverUuid)
    {
        if (!panelUrl.EndsWith("/"))
            panelUrl += "/";

        return new PterodactylConsole()
        {
            PanelUrl = panelUrl,
            ClientKey = clientKey,
            ServerUuid = serverUuid
        };
    }

    private string PanelUrl;
    private string ClientKey;
    private string ServerUuid;

    private ClientWebSocket WebSocket;
    private Task ConsoleTask;
    private CancellationToken CancellationToken;

    // Properties
    public ConsoleStatus Status { get; private set; }

    // Events
    public EventHandler<ConsoleStatus>? StatusChanged { get; set; }
    public EventHandler<ServerResource>? ServerResourcesChanged { get; set; }
    public EventHandler<string>? OutputReceived { get; set; }

    public void Connect()
    {
        // Setup variables
        WebSocket = new();
        CancellationToken = new();
        SetStatus(ConsoleStatus.Disconnected);

        // Setup console task
        ConsoleTask = new Task(async () =>
        {
            while (!CancellationToken.IsCancellationRequested)
            {
                SetStatus(ConsoleStatus.Connecting);

                var wsd = GetWebsocket();
                
                WebSocket.Options.SetRequestHeader("Origin", "https://" + new Uri(PanelUrl).Host);
                WebSocket.Options.SetRequestHeader("Authorization", "Bearer " + wsd.Data.Token);
                
                await WebSocket.ConnectAsync(new Uri(wsd.Data.Socket), CancellationToken);

                if (WebSocket.State != WebSocketState.Connecting && WebSocket.State != WebSocketState.Open)
                {
                    SetStatus(ConsoleStatus.Disconnected);
                    break;
                }
                
                SetStatus(ConsoleStatus.Authenticating);
                var sae = new SendAuthEvent()
                {
                    Args = new[] { wsd.Data.Token }
                };
                await Send(JsonConvert.SerializeObject(sae));

                while (
                    !CancellationToken.IsCancellationRequested &&
                    (WebSocket.State == WebSocketState.Open ||
                     WebSocket.State == WebSocketState.Connecting)
                )
                {
                    var raw = await Receive();

                    var be = JsonConvert.DeserializeObject<BaseEventDTO>(
                        raw
                    );

                    switch (be.Event)
                    {
                        case "jwt error":
                            await WebSocket.CloseAsync(WebSocketCloseStatus.Empty, "Jwt Error detected", CancellationToken.None);
                            SetStatus(ConsoleStatus.Disconnected);
                            break;

                        case "token expired":
                            await WebSocket.CloseAsync(WebSocketCloseStatus.Empty, "Jwt Error detected", CancellationToken.None);
                            SetStatus(ConsoleStatus.Authenticating);
                            break;

                        case "token expiring":
                            SetStatus(ConsoleStatus.Authenticating);

                            wsd = GetWebsocket();
                            
                            sae = new SendAuthEvent()
                            {
                                Args = new[] { wsd.Data.Token }
                            };
                            await Send(JsonConvert.SerializeObject(sae));
                            break;

                        case "auth success":
                            SetStatus(ConsoleStatus.Authenticated);

                            // Sending Intents
                            await Send("{\"event\":\"send logs\",\"args\":[null]}");
                            await Send("{\"event\":\"send stats\",\"args\":[null]}");
                            break;

                        case "stats":
                            var srdto = JsonConvert.DeserializeObject<ServerResource>(be.Args[0]);

                            if (srdto != null)
                            {
                                var statsStatus = ParseStatus(srdto.State);

                                if (Status != statsStatus)
                                {
                                    SetStatus(statsStatus);
                                }
                                
                                ServerResourcesChanged?.Invoke(this, srdto);
                            }
                            break;

                        case "status":
                            var rawState = be.Args[0];
                            SetStatus(ParseStatus(rawState));

                            break;

                        case "console output":
                            foreach (var msg in be.Args)
                            {
                                OutputReceived?.Invoke(this, msg);
                            }

                            break;

                        case "install output":
                            foreach (var msg in be.Args)
                            {
                                OutputReceived?.Invoke(this, msg);
                            }

                            break;
                    }
                }
            }
        });
        
        ConsoleTask.Start();
    }

    private WebsocketDTO GetWebsocket()
    {
        RestClient client = new();
        RestRequest request = new($"{PanelUrl}api/client/servers/{ServerUuid}/websocket");

        request.AddHeader("Authorization", "Bearer " + ClientKey);

        request.AddHeader("Content-Type", "application/json");
        request.AddHeader("Accept", "Application/vnd.pterodactyl.v1+json");

        var response = client.Get(request);

        if (!response.IsSuccessful)
        {
            if (response.StatusCode != 0)
            {
                throw new Exception(
                    $"An error occured: ({response.StatusCode}) {response.Content}"
                );
            }
            
            throw new Exception($"An internal error occured: {response.ErrorMessage}");
        }

        return JsonConvert.DeserializeObject<WebsocketDTO>(response.Content);
    }

    private async Task Send(string message)
    {
        if (WebSocket.State == WebSocketState.Open)
        {
            byte[] byteContentBuffer = Encoding.UTF8.GetBytes(message);
            await WebSocket.SendAsync(new ArraySegment<byte>(byteContentBuffer), WebSocketMessageType.Text, true,
                CancellationToken);
        }
    }

    private async Task<string> Receive()
    {
        ArraySegment<byte> receivedBytes = new ArraySegment<byte>(new byte[1024]);
        WebSocketReceiveResult result = await WebSocket.ReceiveAsync(receivedBytes, CancellationToken);
        return Encoding.UTF8.GetString(receivedBytes.Array, 0, result.Count);
    }

    private void SetStatus(ConsoleStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(this, Status);
    }

    private ConsoleStatus ParseStatus(string status)
    {
        switch (status)
        {
            case "offline":
                return ConsoleStatus.Offline;
            case "starting":
                return ConsoleStatus.Starting;
            case "running":
                return ConsoleStatus.Running;
            case "stopping":
                return ConsoleStatus.Stopping;
            default:
                return ConsoleStatus.Offline;
        }
    }

    public void Dispose()
    {
        WebSocket.CloseAsync(WebSocketCloseStatus.Empty, "Closed", CancellationToken.None);
        WebSocket.Dispose();
        ConsoleTask.Dispose();
    }
}