
namespace FAForever.Replay
{
    public record ReplayHeader(ReplayScenario Scenario, ReplayClient[] Clients, LuaData[] Mods, LuaData[] ArmyOptions);
}
