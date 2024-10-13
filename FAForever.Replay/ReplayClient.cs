
namespace FAForever.Replay
{
    /// <summary>
    /// Represents a player that is participating in a scenario. The input of the player is recorded in the replay file.
    /// </summary>
    /// <param name="Name"></param>
    /// <param name="Id"></param>
    public record ReplayClient(String Name, int Id);
}
