
namespace FAForever.Replay
{
    /// <summary>
    /// An army that is participating in the scenario. These armies are defined in the lobby and can be either a human player or an AI.
    /// </summary>
    /// <param name="Name"></param>
    /// <param name="Id"></param>
    public record ReplayArmy(String Name, int Id);
}
