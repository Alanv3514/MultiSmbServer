namespace MultiSmbServer.Models;

public class ServerConfig
{
    public List<ShareConfig> Shares { get; set; } = new();

    public int Port { get; set; } = 445;

    public string Username { get; set; } = "ps2";

    public string Password { get; set; } = "opl";

    public bool EnableGuest { get; set; } = true;

    public bool LanOnly { get; set; } = true;

    public string Language { get; set; } = "en";
}
