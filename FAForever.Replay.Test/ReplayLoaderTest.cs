namespace FAForever.Replay.Test;

[TestClass]
public class ReplayLoaderTest
{
    [TestMethod]
    [TestCategory("FAFReplay")]
    [DataRow("assets/faforever/zstd/22338092.fafreplay")]
    [DataRow("assets/faforever/zstd/22373098.fafreplay")]
    [DataRow("assets/faforever/zstd/22425616.fafreplay")]
    public void FAForeverZSTDTest(string file)
    {
        Replay replay = ReplayLoader.LoadFAFReplayFromDisk(file);
        Assert.IsNotNull(replay);
    }

    [TestMethod]
    [TestCategory("FAFReplay")]
    [DataRow("assets/faforever/gzip/22451957.fafreplay")]
    [DataRow("assets/faforever/gzip/22453414.fafreplay")]
    [DataRow("assets/faforever/gzip/22453511.fafreplay")]
    public void FAForeverGZipTest(string file)
    {
        Replay replay = ReplayLoader.LoadFAFReplayFromDisk(file);
        Assert.IsNotNull(replay);
    }

    [TestMethod]
    [TestCategory("FAFReplay")]
    [DataRow("assets/faforever/zstd/22338092.fafreplay", 12)]
    [DataRow("assets/faforever/zstd/22373098.fafreplay", 123)]
    [DataRow("assets/faforever/zstd/22425616.fafreplay", 99)]
    [DataRow("assets/faforever/gzip/22451957.fafreplay", 9)]
    [DataRow("assets/faforever/gzip/22453414.fafreplay", 22)]
    [DataRow("assets/faforever/gzip/22453511.fafreplay", 20)]
    [DataRow("assets/faforever/TestCommands01.fafreplay", 13439)]
    [DataRow("assets/faforever/23225508.fafreplay", 2032)]
    [DataRow("assets/faforever/23225323.fafreplay", 14296)]
    [DataRow("assets/faforever/23225440.fafreplay", 6630)]
    [DataRow("assets/faforever/23225685.fafreplay", 14178)]
    [DataRow("assets/faforever/23225104.fafreplay", 51017)]
    [DataRow("assets/faforever/23555859.fafreplay", 12795)]
    [DataRow("assets/faforever/23962051.fafreplay", 15480)]
    [DataRow("assets/faforever/ai/23374795-zhanghm18.fafreplay", 567)]
    public void FAForeverUserInputCountTest(string file, int expectedCount)
    {
        Replay replay = ReplayLoader.LoadFAFReplayFromDisk(file);
        Assert.AreEqual(expectedCount, replay.Body.UserInput.Count);
    }

    [TestMethod]
    [TestCategory("SCFAReplay")]
    [DataRow("assets/scfa/balthazar-01.SCFAReplay", 6290)]
    [DataRow("assets/scfa/balthazar-02.SCFAReplay", 8377)]
    [DataRow("assets/scfa/balthazar-03.SCFAReplay", 12077)]
    [DataRow("assets/scfa/23555859.SCFAReplay", 12795)]
    public void SCFAUserInputCountTest(string file, int expectedCount)
    {
        Replay replay = ReplayLoader.LoadSCFAReplayFromDisk(file);
        Assert.AreEqual(expectedCount, replay.Body.UserInput.Count);
    }

    [TestMethod]
    [TestCategory("FAFReplay")]
    [DataRow("assets/faforever/TestCommands01.fafreplay", 106)]
    [DataRow("assets/faforever/gzip/22451957.fafreplay", 0)]
    [DataRow("assets/faforever/gzip/22453511.fafreplay", 3)]
    public void FAForeverChatMessageCountTest(string file, int expectedCount)
    {
        Replay replay = ReplayLoader.LoadFAFReplayFromDisk(file);
        List<ReplayChatMessage> chatMessages = ReplaySemantics.GetChatMessages(replay);
        Assert.AreEqual(expectedCount, chatMessages.Count);
    }
}