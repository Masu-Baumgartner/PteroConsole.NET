using Newtonsoft.Json;

namespace PteroConsole.NET.Models.Events;

public class SendAuthEvent
{
    [JsonProperty("event")] public string Event { get; set; } = "auth";

    [JsonProperty("args")] public string[] Args { get; set; }
}