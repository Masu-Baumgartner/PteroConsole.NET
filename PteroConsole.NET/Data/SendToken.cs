namespace PteroConsole.NET.Data;

public class SendToken
{
    public string Event { get; set; } = "auth";

    public List<string> Args = new();
}