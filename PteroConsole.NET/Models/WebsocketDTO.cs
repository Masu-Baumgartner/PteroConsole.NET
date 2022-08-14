using Newtonsoft.Json;

namespace PteroConsole.NET.Models;

public class WebsocketDTO
{
    [JsonProperty("data")]
    public WebSocketData Data { get; set; }
    
    public class WebSocketData
    {
        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("socket")]
        public string Socket { get; set; }
    }
}