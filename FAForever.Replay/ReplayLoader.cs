
using System.Text;
using ZstdSharp;

using System.Text.Json;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace FAForever.Replay
{
    public enum ReplayCompression { Gzip, Zstd }

    public static class ReplayLoader
    {
        /// <summary>
        /// Retrieves a command from the stream.
        /// </summary>
        private static CommandData LoadCommandData(ReplayBinaryReader reader)
        {
            int commandId = reader.ReadInt32();

            // unknown
            int arg1 = reader.ReadInt32();

            CommandType commandType = (CommandType)reader.ReadByte();

            // unknown
            int arg2 = reader.ReadInt32();

            CommandTarget target = LoadCommandTarget(reader);

            // unknown
            byte arg3 = reader.ReadByte();

            CommandFormation formation = LoadCommandFormation(reader);

            string blueprintId = reader.ReadNullTerminatedString();

            // unknown
            int arg4 = reader.ReadInt32();
            int arg5 = reader.ReadInt32();
            int arg6 = reader.ReadInt32();

            LuaData luaData = LuaDataLoader.ReadLuaData(reader);

            bool addToQueue = reader.ReadByte() > 0;

            return new CommandData(commandId, commandType, target, formation, blueprintId, luaData, addToQueue, arg1, arg2, arg3, arg4, arg5, arg6);
        }

        /// <summary>
        /// Retrieves the selection of a command from the stream.
        /// </summary>
        private static CommandUnits LoadCommandUnits(ReplayBinaryReader reader)
        {
            int numberOfEntities = reader.ReadInt32();

            // do not read the entities into memory, instead we skip them. There is no way 
            // for us to know what unit is behind an entity id. The only relevant information is the count.
            reader.BaseStream.Position += 4 * numberOfEntities;

            return new CommandUnits(numberOfEntities);
        }

        /// <summary>
        /// Retrieves the target of a command from the stream.
        /// </summary>
        private static CommandTarget LoadCommandTarget(ReplayBinaryReader reader)
        {
            CommandTargetType eventCommandTargetType = (CommandTargetType)reader.ReadByte();
            switch (eventCommandTargetType)
            {
                case CommandTargetType.Entity:
                    {
                        int entityId = reader.ReadInt32();
                        return new CommandTarget.Entity(entityId);
                    }

                case CommandTargetType.Position:
                    {
                        float x = reader.ReadSingle();
                        float y = reader.ReadSingle();
                        float z = reader.ReadSingle();
                        return new CommandTarget.Position(x, y, z);
                    }

                default:
                    return new CommandTarget.None();
            }
        }

        /// <summary>
        /// Retrieves the formation of a command from the stream.
        /// </summary>
        private static CommandFormation LoadCommandFormation(ReplayBinaryReader reader)
        {
            int formationId = reader.ReadInt32();
            if (formationId == -1)
            {
                return new CommandFormation.NoFormation();
            }

            float heading = reader.ReadSingle();
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            float scale = reader.ReadSingle();

            return new CommandFormation.Formation(formationId, heading, x, y, z, scale);
        }

        /// <summary>
        /// Loads all the user input up to the threshold. If no threshold is defined then all input is loaded by default.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="invariant"></param>
        /// <param name="inputToProcess"></param>
        private static ReplayBodyInvariant LoadReplayInputs(ReplayBinaryReader reader, ReplayBodyInvariant? invariant, int? inputToProcess)
        {
            // setup the locals based on the (optional) invariant
            int estimatedNumberOfInputsRemaining = (int)(20 * Math.Sqrt(reader.BaseStream.Length - reader.BaseStream.Position));
            var (input, tick, source, inSync, hashTick, hashValue, _, startingPointOfStream, _) = invariant ?? new ReplayBodyInvariant(
                input: new List<ReplayInput>(estimatedNumberOfInputsRemaining),
                tick: 0,
                source: 0,
                inSync: true,
                hashTick: 0,
                hashValue: 0,
                endOfStream: false,
                startingPointOfStream: reader.BaseStream.Position,
                percentageProcessed: 0
            );

            int inputProcessed = 0;
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                ReplayInputType type = (ReplayInputType)reader.ReadByte();
                // includes the type and this number of bytes, which is a bit confusing. 
                int numberOfBytes = reader.ReadInt16() - (1 + 2);

                switch (type)
                {
                    case ReplayInputType.Advance:
                        {
                            int ticksToAdvance = reader.ReadInt32();
                            tick += ticksToAdvance;
                            break;
                        }

                    case ReplayInputType.SetCommandSource:
                        {
                            int sourceId = reader.ReadByte();
                            source = sourceId;
                            break;
                        }

                    case ReplayInputType.CommandSourceTerminated:
                        input.Add(new ReplayInput.CommandSourceTerminated(tick, source));
                        break;

                    case ReplayInputType.VerifyChecksum:
                        {
                            long hash = reader.ReadInt64() ^ reader.ReadInt64();
                            int atTick = reader.ReadInt32();
                            if (hashTick != atTick)
                            {
                                hashValue = hash;
                            }
                            else
                            {
                                inSync = hashValue == hash;
                            }
                            break;
                        }

                    case ReplayInputType.RequestPause:
                        input.Add(new ReplayInput.RequestPause(tick, source));
                        break;

                    case ReplayInputType.RequestResume:
                        input.Add(new ReplayInput.RequestResume(tick, source));
                        break;

                    case ReplayInputType.SingleStep:
                        input.Add(new ReplayInput.SingleStep(tick, source));
                        break;

                    case ReplayInputType.CreateUnit:
                        {
                            int armyId = reader.ReadByte();
                            string blueprintId = reader.ReadNullTerminatedString();
                            float x = reader.ReadSingle();
                            float z = reader.ReadSingle();
                            float heading = reader.ReadSingle();
                            input.Add(new ReplayInput.CreateUnit(tick, source, armyId, blueprintId, x, z, heading));
                            break;
                        }

                    case ReplayInputType.CreateProp:
                        {
                            string blueprintId = reader.ReadNullTerminatedString();
                            float x = reader.ReadSingle();
                            float z = reader.ReadSingle();
                            float heading = reader.ReadSingle();
                            input.Add(new ReplayInput.CreateProp(tick, source, blueprintId, x, z, heading));
                            break;
                        }

                    case ReplayInputType.DestroyEntity:
                        {
                            int entityId = reader.ReadInt32();
                            input.Add(new ReplayInput.DestroyEntity(tick, source, entityId));
                            break;
                        }

                    case ReplayInputType.WarpEntity:
                        {
                            int entityId = reader.ReadInt32();
                            float x = reader.ReadSingle();
                            float y = reader.ReadSingle();
                            float z = reader.ReadSingle();
                            input.Add(new ReplayInput.WarpEntity(tick, source, entityId, x, y, z));
                            break;
                        }

                    case ReplayInputType.ProcessInfoPair:
                        {
                            int entityId = reader.ReadInt32();
                            string name = reader.ReadNullTerminatedString();
                            string value = reader.ReadNullTerminatedString();
                            input.Add(new ReplayInput.ProcessInfoPair(tick, source, entityId, name, value));
                            break;
                        }

                    case ReplayInputType.IssueCommand:
                        {
                            CommandUnits units = LoadCommandUnits(reader);
                            CommandData data = LoadCommandData(reader);
                            input.Add(new ReplayInput.IssueCommand(tick, source, units, data));
                            break;
                        }

                    case ReplayInputType.IssueFactoryCommand:
                        {
                            CommandUnits factories = LoadCommandUnits(reader);
                            CommandData data = LoadCommandData(reader);
                            input.Add(new ReplayInput.IssueFactoryCommand(tick, source, factories, data));
                            break;
                        }

                    case ReplayInputType.IncreaseCommandCount:
                        {
                            int commandId = reader.ReadInt32();
                            int delta = reader.ReadInt32();
                            input.Add(new ReplayInput.IncreaseCommandCount(tick, source, commandId, delta));
                            break;
                        }

                    case ReplayInputType.DecreaseCommandCount:
                        {
                            int commandId = reader.ReadInt32();
                            int delta = reader.ReadInt32();
                            input.Add(new ReplayInput.DecreaseCommandCount(tick, source, commandId, delta));
                            break;
                        }

                    case ReplayInputType.UpdateCommandTarget:
                        {
                            int commandId = reader.ReadInt32();
                            CommandTarget target = LoadCommandTarget(reader);
                            input.Add(new ReplayInput.UpdateCommandTarget(tick, source, commandId, target));
                            break;
                        }

                    case ReplayInputType.UpdateCommandType:
                        {
                            int commandId = reader.ReadInt32();
                            CommandType commandType = (CommandType)reader.ReadInt32();
                            input.Add(new ReplayInput.UpdateCommandType(tick, source, commandId, commandType));
                            break;
                        }

                    case ReplayInputType.UpdateCommandParameters:
                        {
                            int commandId = reader.ReadInt32();
                            LuaData luaParameters = LuaDataLoader.ReadLuaData(reader);
                            float x = reader.ReadSingle();
                            float y = reader.ReadSingle();
                            float z = reader.ReadSingle();
                            input.Add(new ReplayInput.UpdateCommandLuaParameters(tick, source, commandId, luaParameters, x, y, z));
                            break;

                        }

                    case ReplayInputType.RemoveFromCommandQueue:
                        {
                            int commandId = reader.ReadInt32();
                            int entityId = reader.ReadInt32();
                            input.Add(new ReplayInput.RemoveCommandFromQueue(tick, source, commandId, entityId));
                            break;
                        }

                    case ReplayInputType.DebugCommand:
                        {
                            string command = reader.ReadNullTerminatedString();
                            float x = reader.ReadSingle();
                            float y = reader.ReadSingle();
                            float z = reader.ReadSingle();
                            byte focusArmy = reader.ReadByte();
                            CommandUnits debugUnits = LoadCommandUnits(reader);
                            input.Add(new ReplayInput.DebugCommand(tick, source, command, x, y, z, focusArmy, debugUnits));
                            break;
                        }

                    case ReplayInputType.ExecuteLuaInSim:
                        {
                            string luaCode = reader.ReadNullTerminatedString();
                            input.Add(new ReplayInput.ExecuteLuaInSim(tick, source, luaCode));
                            break;
                        }

                    case ReplayInputType.Simcallback:
                        {
                            string endpoint = reader.ReadNullTerminatedString();
                            LuaData luaParameters = LuaDataLoader.ReadLuaData(reader);
                            CommandUnits units = LoadCommandUnits(reader);
                            input.Add(new ReplayInput.SimCallback(tick, source, endpoint, luaParameters, units));
                            break;
                        }

                    case ReplayInputType.EndGame:
                        input.Add(new ReplayInput.EndGame(tick, source));
                        break;

                    default:
                        throw new Exception("Unknown replay input type");
                }

                // break out of the loop if we have processed the desired number of inputs
                inputProcessed++;
                if (inputToProcess.HasValue && inputProcessed >= inputToProcess.Value)
                {
                    break;
                }
            }

            int completionPercentage = (int)Math.Round(100 * ((float)reader.BaseStream.Position - startingPointOfStream) / (reader.BaseStream.Length - startingPointOfStream));
            return new ReplayBodyInvariant(input, tick, source, inSync, hashTick, hashValue, reader.BaseStream.Position == reader.BaseStream.Length, startingPointOfStream, completionPercentage);
        }

        /// <summary>
        /// Loads in the scenario information from the replay.
        /// 
        /// In practice this is a copy of the information from the `_scenario.lua` file of the chosen map. Because of that the content is fairly reliable, but at the same time it can contain anything.
        /// </summary>
        /// <param name="luaScenario"></param>
        /// <returns></returns>
        private static ReplayScenarioMap LoadScenarioMap(LuaData.Table luaScenario)
        {
            LuaData.Table? sizeTable = luaScenario.TryGetTableValue("size", out var luaSize) ? luaSize : null;
            int? sizeX = sizeTable != null && sizeTable.TryGetNumberValue("1", out var luaSizeX) ? (int)luaSizeX!.Value : null;
            int? sizeZ = sizeTable != null && sizeTable.TryGetNumberValue("2", out var luaSizeZ) ? (int)luaSizeZ!.Value : null;

            LuaData.Table? reclaimTable = luaScenario.TryGetTableValue("reclaim", out var luaReclaim) ? luaReclaim : null;
            int? massReclaim = reclaimTable != null && reclaimTable.TryGetNumberValue("1", out var luamassReclaim) ? (int)luamassReclaim!.Value : null;
            int? energyReclaim = reclaimTable != null && reclaimTable.TryGetNumberValue("2", out var luaEnergyReclaim) ? (int)luaEnergyReclaim!.Value : null;

            return new ReplayScenarioMap(
                luaScenario.TryGetStringValue("name", out var name) ? name : null,
                luaScenario.TryGetStringValue("description", out var description) ? description : null,
                luaScenario.TryGetStringValue("map", out var scmap) ? scmap : null,
                luaScenario.TryGetStringValue("preview", out var preview) ? preview : null,
                luaScenario.TryGetStringValue("repository", out var repository) ? repository : null,
                luaScenario.TryGetNumberValue("map_version", out var version) ? (int)version! : null,
                sizeX, sizeZ, massReclaim, energyReclaim);
        }

        /// <summary>
        /// Loads in the scenario options as defined in the lobby. 
        /// 
        /// These options reflect the following file: https://github.com/FAForever/fa/blob/develop/lua/ui/lobby/lobbyOptions.lua
        /// In practice however, all mod options are sent to the scenario by default too. As a result this Lua table can contain quite literally anything. 
        /// </summary>
        /// <param name="luaScenario"></param>
        /// <returns></returns>
        private static ReplayScenarioOptions LoadScenarioOptions(LuaData.Table luaScenario)
        {
            return new ReplayScenarioOptions();
        }

        /// <summary>
        /// Loads in the scenario from the stream.
        /// </summary>
        private static ReplayScenario LoadScenario(ReplayBinaryReader reader)
        {
            LuaData luaScenario = LuaDataLoader.ReadLuaData(reader);
            if (!(luaScenario is LuaData.Table scenario))
            {
                throw new Exception("Scenario is not a table");
            }

            return new ReplayScenario(LoadScenarioOptions(scenario), LoadScenarioMap(scenario), scenario.TryGetStringValue("type", out var type) ? type! : null);
        }

        /// <summary>
        /// Loads in the replay header from the stream.
        /// </summary>
        private static ReplayHeader LoadReplayHeader(ReplayBinaryReader reader)
        {
            string gameVersion = reader.ReadNullTerminatedString();

            // Always \r\n
            string Unknown1 = reader.ReadNullTerminatedString();

            String[] replayVersionAndScenario = reader.ReadNullTerminatedString().Split("\r\n");
            String replayVersion = replayVersionAndScenario[0];
            String pathToScenario = replayVersionAndScenario[1];

            // Always \r\n and an unknown character
            string Unknown2 = reader.ReadNullTerminatedString();

            int numberOfBytesForMods = reader.ReadInt32();
            List<LuaData> mods = new List<LuaData>();
            LuaData luaMods = LuaDataLoader.ReadLuaData(reader);
            if (luaMods is LuaData.Table modsTable)
            {
                foreach (var mod in modsTable.Value)
                {
                    if (mod.Value is LuaData.Table modTable)
                    {
                        mods.Add(modTable);
                    }
                }
            }

            int numberOfBytesScenario = reader.ReadInt32();
            ReplayScenario scenario = LoadScenario(reader);


            byte numberOfClients = reader.ReadByte();
            ReplayClient[] clients = new ReplayClient[numberOfClients];
            for (int i = 0; i < numberOfClients; i++)
            {
                clients[i] = new ReplayClient(reader.ReadNullTerminatedString(), reader.ReadInt32());
            }

            Boolean cheatsEnabled = reader.ReadByte() > 0;

            int numberOfArmies = reader.ReadByte();
            for (int i = 0; i < numberOfArmies; i++)
            {
                int numberOfBytesArmyConfig = reader.ReadInt32();
                LuaData armyConfigData = LuaDataLoader.ReadLuaData(reader);
                //byte[] playerOptions = reader.ReadBytes(numberOfBytesPlayerOptions);

                int Unknown4 = reader.ReadByte();

                // ???
                if (Unknown4 != 255)
                {
                    byte[] Unknown3 = reader.ReadBytes(1); // always -1
                }
            }

            int seed = reader.ReadInt32();

            return new ReplayHeader(scenario, clients, mods.ToArray(), new LuaData[] { });
        }

        /// <summary>
        /// Loads a replay.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private static Replay LoadReplay(ReplayBinaryReader reader)
        {
            ReplayHeader replayHeader = LoadReplayHeader(reader);
            ReplayBodyInvariant replayEvents = LoadReplayInputs(reader, null, null);

            return new Replay(
                Header: replayHeader,
                Body: new ReplayBody(replayEvents.Input, replayEvents.InSync)
            );
        }

        /// <summary>
        /// Loads the meta data of a replay that originates from FAForever.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private static ReplayMetadata? LoadReplayMetadata(ReplayBinaryReader reader)
        {
            StringBuilder json = new StringBuilder();

            while (true)
            {
                char c = reader.ReadChar();
                if (c == '\n')
                {
                    break;
                }

                json.Append(c);
            }

            return JsonSerializer.Deserialize<ReplayMetadata>(json.ToString());
        }
        
        /// <summary>
        /// Decompresses the stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="compression"></param>
        /// <returns></returns>
        private static MemoryStream DecompressReplay(Stream stream, ReplayCompression compression)
        {
            MemoryStream replayStream = new MemoryStream();
            switch (compression)
            {
                case ReplayCompression.Gzip:

                    string base64 = new StreamReader(stream).ReadToEnd();
                    byte[] bytes = Convert.FromBase64String(base64);
                    byte[] skipped = new byte[bytes.Length - 4];
                    Array.Copy(bytes, 4, skipped, 0, skipped.Length);
                    using (MemoryStream memoryStream = new MemoryStream(skipped, false))
                    {
                        using (InflaterInputStream decompressor = new InflaterInputStream(memoryStream))
                        {
                            decompressor.CopyTo(replayStream);
                            replayStream.Position = 0;
                            return replayStream;
                        }
                    }

                case ReplayCompression.Zstd:
                    using (DecompressionStream decompressor = new DecompressionStream(stream))
                    {
                        decompressor.CopyTo(replayStream);
                        replayStream.Position = 0;
                        return replayStream;
                    }

                default:
                    throw new ArgumentException("Unknown replay compression");
            }
        }

        /// <summary>
        /// Processes the metadata of a replay from FAForever and returns the intermediate results. 
        /// 
        /// This allows the process to exit periodically, make room for other code to run, and then continue where it left off. This is useful for single threaded environments such as WebAssembly that is used by Blazor.
        /// </summary>
        /// <param name="stage"></param>
        /// <returns></returns>
        public static ReplayLoadingStage ProcessReplayStage(ReplayLoadingStage.NotStarted stage)
        {
            ReplayBinaryReader reader = new ReplayBinaryReader(stage.Stream);
            ReplayMetadata? metadata = LoadReplayMetadata(reader);

            if (metadata is null)
            {
                return new ReplayLoadingStage.Failed("No metadata found.");
            }

            return new ReplayLoadingStage.WithMetadata(stage.Stream, metadata);
        }


        /// <summary>
        /// Decompresses the replay and returns the intermediate results.
        /// 
        /// This allows the process to exit periodically, make room for other code to run, and then continue where it left off. This is useful for single threaded environments such as WebAssembly that is used by Blazor.
        /// </summary>
        /// <param name="stage"></param>
        /// <returns></returns>
        public static ReplayLoadingStage ProcessReplayStage(ReplayLoadingStage.WithMetadata stage)
        {
            ReplayCompression replayCompression = ReplayCompression.Gzip;
            if (stage.Metadata.compression == "zstd")
            {
                replayCompression = ReplayCompression.Zstd;
            }

            MemoryStream? replayStream = DecompressReplay(stage.Stream, replayCompression);
            if (replayStream is null)
            {
                return new ReplayLoadingStage.Failed("Decompression failed.");
            }

            // close the old stream
            stage.Stream.Dispose();

            return new ReplayLoadingStage.Decompressed(replayStream, stage.Metadata);
        }

        /// <summary>
        /// Processes the scenario of a replay and returns the intermediate results.
        /// 
        /// This allows the process to exit periodically, make room for other code to run, and then continue where it left off. This is useful for single threaded environments such as WebAssembly that is used by Blazor.
        /// </summary>
        /// <param name="stage"></param>
        /// <returns></returns>
        public static ReplayLoadingStage ProcessReplayStage(ReplayLoadingStage.Decompressed stage)
        {
            ReplayBinaryReader reader = new ReplayBinaryReader(stage.Stream);
            ReplayHeader replayHeader = LoadReplayHeader(reader);
            return new ReplayLoadingStage.WithScenario(reader, stage.Metadata, replayHeader);
        }

        /// <summary>
        /// Processes a portion of the input of a replay and returns the intermediate results.
        /// 
        /// This allows the process to exit periodically, make room for other code to run, and then continue where it left off. This is useful for single threaded environments such as WebAssembly that is used by Blazor.
        /// </summary>
        /// <param name="stage"></param>
        /// <param name="batchSize"></param>
        /// <returns></returns>
        public static ReplayLoadingStage ProcessReplayStage(ReplayLoadingStage.WithScenario stage, int batchSize = 1000)
        {
            ReplayBodyInvariant replayBodyInvariant = LoadReplayInputs(stage.Stream, null, batchSize);
            return new ReplayLoadingStage.AtInput(stage.Stream, stage.Metadata, stage.Header, replayBodyInvariant);
        }

        /// <summary>
        /// Processes a portion of the input of a replay and returns the intermediate results.
        /// 
        /// This allows the process to exit periodically, make room for other code to run, and then continue where it left off. This is useful for single threaded environments such as WebAssembly that is used by Blazor.
        /// </summary>
        /// <param name="stage"></param>
        /// <param name="batchSize"></param>
        /// <returns></returns>
        public static ReplayLoadingStage ProcessReplayStage(ReplayLoadingStage.AtInput stage, int batchSize = 1000)
        {
            ReplayBodyInvariant replayBodyInvariant = LoadReplayInputs(stage.Stream, stage.BodyInvariant, batchSize);
            if (replayBodyInvariant.EndOfStream)
            {
                return new ReplayLoadingStage.Complete(stage.Stream, stage.Metadata, stage.Header, new ReplayBody(replayBodyInvariant.Input, replayBodyInvariant.InSync));
            }
            else
            {
                return new ReplayLoadingStage.AtInput(stage.Stream, stage.Metadata, stage.Header, replayBodyInvariant);
            }
        }

        /// <summary>
        /// Loads a replay from a stream.
        ///
        /// Loads the replay from start to finish, if intermediate steps are required then please see the ProcessReplayStage methods.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static Replay? LoadReplayFromStream(Stream stream, ReplayType type)
        {
            return type switch
            {
                ReplayType.Steam => LoadScfaReplayFromMemory(stream),
                ReplayType.ForgedAllianceForever => LoadFafReplayFromMemory(stream),
                _ => null
            };
        }
        
        /// <summary>
        /// Loads a FAForever replay from a stream. 
        /// 
        /// Loads the replay from start to finish, if intermediate steps are required then please see the ProcessReplayStage methods.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static Replay LoadFafReplayFromMemory(Stream stream)
        {
            ReplayBinaryReader reader = new ReplayBinaryReader(stream);
            ReplayMetadata replayMetadata = LoadReplayMetadata(reader);


            ReplayCompression replayCompression = ReplayCompression.Gzip;
            if (replayMetadata is { compression: "zstd" })
            {
                replayCompression = ReplayCompression.Zstd;
            }

            MemoryStream decompressedStream = DecompressReplay(stream, replayCompression);
            using ReplayBinaryReader replayBinaryReader = new ReplayBinaryReader(decompressedStream);
            return LoadReplay(replayBinaryReader);
        }

        /// <summary>
        /// Loads a SCFA replay from a stream.
        /// 
        /// Loads the replay from start to finish, if intermediate steps are required then please see the ProcessReplayStage methods.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static Replay LoadScfaReplayFromMemory(Stream stream)
        {
            using (ReplayBinaryReader reader = new ReplayBinaryReader(stream))
            {
                return LoadReplay(reader);
            }
        }
    }
}
