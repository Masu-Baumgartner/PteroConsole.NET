using Newtonsoft.Json;

namespace PteroConsole.NET.Models.Events;

public class BaseEventDTO
{
    [JsonProperty("event")] public string Event { get; set; }

    [JsonProperty("args")] public string[] Args { get; set; }
}