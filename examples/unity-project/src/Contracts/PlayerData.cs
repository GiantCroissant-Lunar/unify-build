namespace Contracts;

/// <summary>
/// Shared data model used by both the .NET server and Unity client.
/// </summary>
public class PlayerData
{
    public string? PlayerId { get; set; }
    public string? DisplayName { get; set; }
    public int Score { get; set; }
}
