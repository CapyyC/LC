// Drives the real LobotomyArchipelago.dll against the simulated game (FakeGame / "Assembly-CSharp").
//
//   ModHarness mock           - plays a scripted session against a mock Lobotomy Corporation Client and asserts
//                               the mod's behaviour (extraction, checks, items, traps, agent slots, goal).
//   ModHarness live [port]    - connects to a running Lobotomy Corporation Client (which is connected to a real
//                               Archipelago server), plays a few days and waits until the server confirms checks.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using LobotomyArchipelago;

internal static class Program
{
    private const long Base = 568220000L;
    private static int _failures;
    private static Runner _runner;
    private static MethodInfo _update;

    private static int Main(string[] args)
    {
        string mode = args.Length > 0 ? args[0] : "mock";
        string dataDir = Path.Combine(Path.GetTempPath(), "lobcorp-ap-harness-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataDir);
        Environment.SetEnvironmentVariable("LOBCORP_AP_DATA", dataDir);
        int port = mode == "live" && args.Length > 1 ? int.Parse(args[1]) : FreePort();

        // The mod reads its port from config.json next to the dll.
        string modDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
        File.WriteAllText(Path.Combine(modDir, "config.json"), "{\"port\": " + port + "}");

        // Make sure the fake game and Harmony are loaded before the mod looks for them.
        GC.KeepAlive(typeof(PlayerModel));
        GC.KeepAlive(typeof(HarmonyLib.Harmony));

        try
        {
            return mode == "live" ? Live() : Mock(port);
        }
        finally
        {
            Plugin.Bridge?.Stop();
        }
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static void StartMod()
    {
        new Harmony_Patch();
        _runner = new Runner();
        Plugin.Runner = _runner;
        _update = typeof(Runner).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (string line in HarmonyBridge.Report) Console.WriteLine("  patch " + line);
    }

    private static void Pump(double seconds)
    {
        DateTime end = DateTime.UtcNow.AddSeconds(seconds);
        do
        {
            _update.Invoke(_runner, null);
            Thread.Sleep(20);
        } while (DateTime.UtcNow < end);
    }

    private static void Check(bool condition, string description)
    {
        Console.WriteLine((condition ? "  PASS " : "  FAIL ") + description);
        if (!condition) _failures++;
    }

    // ------------------------------------------------------------------------------------------------------------
    // Mock client mode
    // ------------------------------------------------------------------------------------------------------------
    private sealed class MockClient
    {
        private readonly TcpListener _listener;
        private StreamWriter _writer;
        private readonly List<Dictionary<string, JsonElement>> _received = new();
        private readonly object _lock = new();

        public MockClient(int port)
        {
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();
            new Thread(Accept) { IsBackground = true }.Start();
        }

        private void Accept()
        {
            TcpClient client = _listener.AcceptTcpClient();
            var stream = client.GetStream();
            _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
            var reader = new StreamReader(stream, Encoding.UTF8);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var message = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line);
                lock (_lock) _received.Add(message);
            }
        }

        public bool Connected => _writer != null;

        public void Send(object message)
        {
            _writer.WriteLine(JsonSerializer.Serialize(message));
        }

        public List<Dictionary<string, JsonElement>> Messages(string cmd)
        {
            lock (_lock) return _received.Where(m => m["cmd"].GetString() == cmd).ToList();
        }

        public HashSet<long> CheckedLocations()
        {
            var result = new HashSet<long>();
            foreach (var message in Messages("LocationChecks"))
                foreach (JsonElement id in message["locations"].EnumerateArray()) result.Add(id.GetInt64());
            return result;
        }
    }

    private static long Obs(int index, int level) => Base + 10_000_000 + index * 100 + level;
    private static long EgoWeapon(int index, int copy) => Base + 10_000_000 + index * 100 + 10 + copy - 1;
    private static long EgoSuit(int index, int copy) => Base + 10_000_000 + index * 100 + 20 + copy - 1;
    private static long Day(int day) => Base + 11_000_000 + day;

    private static int Mock(int port)
    {
        Console.WriteLine("Mock session on port " + port);
        var client = new MockClient(port);
        StartMod();

        Check(HarmonyBridge.Report.All(line => line.StartsWith("ok")), "every patch target was found and patched");
        for (int i = 0; i < 100 && !client.Connected; i++) Pump(0.05);
        Pump(0.3);
        Check(client.Messages("Hello").Count == 1, "mod says Hello to the client");

        var allowed = new List<long>();
        for (int index = 1; index <= 4; index++)
        {
            for (int level = 1; level <= 4; level++) allowed.Add(Obs(index, level));
            allowed.Add(EgoWeapon(index, 1));
            allowed.Add(EgoSuit(index, 1));
        }
        for (int day = 1; day <= 5; day++) allowed.Add(Day(day));
        allowed.Add(Base + 12_000_000 + 1); // Malkuth mission 1
        var slotData = new Dictionary<string, object>
        {
            ["base_id"] = Base, ["goal_day"] = 2, ["required_core_suppressions"] = 0, ["containment_count"] = 4,
            ["abnormality_pool"] = new long[] { 100009, 100012, 100006, 300003 },
            ["extraction_offers"] = 3, ["observation_checks"] = 4, ["ego_extraction_checks"] = 1,
            ["ego_gift_checks"] = false, ["extraction_after_checks"] = 0, ["randomize_research"] = true,
            ["research"] = new Dictionary<string, object>
            {
                [(Base + 3_000_000 + 0).ToString()] = new object[] { 0, 0, "TT2 Protocol" },
                [(Base + 3_000_000 + 40).ToString()] = new object[] { 4, 0, "Physical Intervention Shield" },
                [(Base + 3_000_000 + 92).ToString()] = new object[] { 9, 2, "Limit Break: The Virtue of Creation", 1003 },
            },
            ["randomize_agent_slots"] = true, ["starting_agent_slots"] = 2,
            ["agent_slot_items"] = new Dictionary<string, object> { [(Base + 4_000_000).ToString()] = 0 },
            ["missions"] = new Dictionary<string, object> { ["1"] = new object[] { 0, 1, Base + 12_000_000 + 1 } },
            ["suppressions"] = new Dictionary<string, object> { ["0"] = Base + 13_000_000 },
            ["lob_points_per_item"] = 4, ["pe_boxes_per_item"] = 30, ["death_link"] = true,
            ["death_link_trigger"] = 0, ["death_link_effect"] = 0,
        };
        client.Send(new Dictionary<string, object>
        {
            ["cmd"] = "Connected", ["protocol"] = 1, ["seed"] = "MOCKSEED", ["slot"] = 1, ["team"] = 0,
            ["slot_name"] = "Manager", ["slot_data"] = slotData, ["checked_locations"] = Array.Empty<long>(),
            ["missing_locations"] = allowed,
        });
        object Item(long id, string name) => new Dictionary<string, object>
        {
            ["item"] = id, ["location"] = -2, ["player"] = 1, ["flags"] = 1, ["item_name"] = name, ["sender"] = "Manager"
        };
        client.Send(new Dictionary<string, object>
        {
            ["cmd"] = "ReceivedItems", ["index"] = 0, ["items"] = new[]
            {
                Item(Base + 100009, "One Sin and Hundreds of Good Deeds"),
                Item(Base + 100012, "Spider Bud"),
                Item(Base + 3_000_000 + 0, "TT2 Protocol"),
                Item(Base + 3_000_000 + 92, "Limit Break: The Virtue of Creation"),
                Item(Base + 1_000_000 + 100009, "Penitence (E.G.O Weapon)"),
                Item(Base + 5_000_000 + 1, "LOB Points"),
                Item(Base + 4_000_000 + 0, "Control Team Agent Slot"),
                Item(Base + 6_000_000 + 1, "Dawn Ordeal Trap"),
            }
        });
        Pump(0.5);
        Check(Plugin.Session.HasSlot && Plugin.Session.SlotName == "Manager", "slot data received");
        Check(Plugin.Session.ReceivedAbnormalities.SequenceEqual(new long[] { 100009, 100012 }),
            "received abnormalities are tracked in order");

        // ----- Facility loads (deployment phase of day 1) -----
        GameManager.currentGameManager = new GameManager();
        PlayerModel.instance.day = 0;
        Pump(1.5);
        Check(MoneyModel.instance.money == 4, "LOB points item applied (" + MoneyModel.instance.money + ")");
        Pump(5.5);
        Check(InventoryModel.Instance.equipList.Count(e => e.metaInfo.id == 200009) == 1,
            "E.G.O weapon item added to the inventory");
        Check(ResearchDataModel.instance.GetModelBySefira("1")[0].curLevel == 1, "TT2 Protocol research unlocked");
        Check(ResearchDataModel.instance.GetModelBySefira("10")[2].curLevel == 1,
            "Hokma research with an id above 1000 unlocked");

        // ----- Extraction offers only received abnormalities -----
        var generator = new CreatureGenerate.CreatureGenerateModel();
        generator.SetCreature();
        Check(generator.creature.SequenceEqual(new long[] { 100009, 100012 }),
            "day 1 extraction offers the received abnormalities: " + string.Join(",", generator.creature));
        var selectUi = new CreatureSelectUI();
        selectUi.GetCreatureList(false);
        Check(selectUi.CurrentCreatures.All(id => id == 100009 || id == 100012), "selection UI is sanitized too");

        // ----- Containment and observation -----
        CreatureModel oneSin = CreatureManager.instance.Add(100009);
        oneSin.observationLevel = 2;
        Pump(1.5);
        HashSet<long> sent = client.CheckedLocations();
        Check(sent.Contains(Obs(1, 1)) && sent.Contains(Obs(1, 2)) && !sent.Contains(Obs(1, 3)),
            "observation levels 1-2 of abnormality #01 sent");

        // ----- E.G.O extraction becomes a check -----
        oneSin.observeInfo.cubeNum = 50;
        int inventoryBefore = InventoryModel.Instance.equipList.Count;
        var root = new CreatureInfoEquipmentRoot { model = oneSin };
        root.OnClickWeapon();
        Pump(2.5);
        Check(client.CheckedLocations().Contains(EgoWeapon(1, 1)), "weapon extraction sent as a check");
        Check(oneSin.observeInfo.cubeNum == 30, "extraction cost paid (" + oneSin.observeInfo.cubeNum + " PE left)");
        Check(InventoryModel.Instance.equipList.Count == inventoryBefore, "no vanilla E.G.O created");
        root.OnClickWeapon();
        Check(InventoryModel.Instance.equipList.Count == inventoryBefore, "extraction blocked once checks are done");
        root.OnClickArmor(); // observation level 2 < suit level 3: vanilla refuses, nothing happens
        Check(!Plugin.Session.LocalChecks.Contains(EgoSuit(1, 1)), "suit extraction requires the observation level");

        // ----- Missions: research choice replaced by a check -----
        MissionManager.instance.cleared.Add(new Mission
        {
            metaInfo = new MissionTypeInfo { id = 1, sefira = SefiraEnum.MALKUT },
            successCondition = new SuccessCondition { condition_Type = ConditionType.WORK }
        });
        var deploy = new DeployUI();
        deploy.CheckResearchAvailable();
        Pump(2.5);
        Check(!deploy.researchWindowOpened, "research selection skipped");
        Check(client.CheckedLocations().Contains(Base + 12_000_000 + 1), "mission check sent");
        Check(MissionManager.instance.cleared.Count == 0, "mission closed");

        // ----- Traps fire during management -----
        GameManager.currentGameManager.StartGame();
        Pump(11.5);
        Check(FakeLog.Events.Any(e => e.StartsWith("ordeal ")), "Dawn Ordeal trap summoned an ordeal");

        // ----- Agent slots -----
        for (int i = 0; i < 4; i++) AgentManager.instance.agents.Add(new AgentModel());
        GameManager.currentGameManager.EndGame();
        Runner.InManagement = false;
        foreach (AgentModel agent in AgentManager.instance.agents) agent.SetCurrentSefira("1");
        int inControl = AgentManager.instance.agents.Count(a => a.currentSefira == "1");
        Check(inControl == 3, "Control Team limited to 3 agents (2 starting + 1 slot item), has " + inControl);

        // ----- Day clear, next day block, goal -----
        GameManager.currentGameManager.ClearAction();
        Pump(2.5);
        Check(client.CheckedLocations().Contains(Day(1)), "Day 1 cleared check sent");
        PlayerModel.instance.day = 1;
        var scene = new GameSceneController();
        scene.OnClickNextDay();
        Check(scene.advanced, "day 2 allowed: Spider Bud is available");
        CreatureManager.instance.Add(100012);
        Pump(1.5);
        scene = new GameSceneController();
        PlayerModel.instance.day = 2;
        scene.OnClickNextDay();
        Check(!scene.advanced, "day 3 blocked: no received abnormality left");
        Check(Plugin.Session.ContainmentIndexOf(100012) == 2, "Spider Bud is abnormality #02");

        PlayerModel.instance.day = 1;
        GameManager.currentGameManager.ClearAction();
        Pump(2.5);
        Check(client.Messages("Goal").Count >= 1, "goal (clear day 2) reported");

        // ----- DeathLink -----
        new EscapeUI().RestartAtCheckpoint();
        Pump(0.5);
        Check(client.Messages("DeathLink").Count == 1, "DeathLink sent on restart");
        GameManager.currentGameManager.StartGame();
        client.Send(new Dictionary<string, object> { ["cmd"] = "DeathLink", ["source"] = "Friend", ["cause"] = "test" });
        Pump(1.5);
        Check(AgentManager.instance.agents.Count(a => a.dead) == 1, "received DeathLink killed one agent");

        // ----- Persistence -----
        Plugin.Session.Save();
        string save = Directory.GetFiles(Environment.GetEnvironmentVariable("LOBCORP_AP_DATA")!, "save_*.json",
            SearchOption.AllDirectories).FirstOrDefault();
        Check(save != null && File.ReadAllText(save).Contains("\"containment\":[100009,100012]"),
            "progress saved per seed");

        Console.WriteLine(_failures == 0 ? "ALL CHECKS PASSED" : _failures + " CHECK(S) FAILED");
        return _failures == 0 ? 0 : 1;
    }

    // ------------------------------------------------------------------------------------------------------------
    // Live mode: real Lobotomy Corporation Client + Archipelago server
    // ------------------------------------------------------------------------------------------------------------
    private static int Live()
    {
        StartMod();
        Console.WriteLine("Waiting for slot data from the client...");
        for (int i = 0; i < 600 && !(Plugin.Session.HasSlot && Plugin.Session.ServerConnected); i++) Pump(0.1);
        Check(Plugin.Session.HasSlot, "received slot data from the real client");
        Pump(1.0);
        Session session = Plugin.Session;
        Console.WriteLine("  slot " + session.SlotName + ", goal day " + session.GoalDay + ", items " +
                          session.Items.Count + ", allowed locations " + session.AllowedLocations.Count);
        Check(session.ReceivedAbnormalities.Count >= 1, "starting abnormalities received");

        GameManager.currentGameManager = new GameManager();
        PlayerModel.instance.day = 0;
        var generator = new CreatureGenerate.CreatureGenerateModel();
        generator.SetCreature();
        Check(generator.creature.Count > 0 && generator.creature.All(id => session.ReceivedAbnormalities.Contains(id)),
            "extraction offers received abnormalities: " + string.Join(",", generator.creature));
        CreatureModel first = CreatureManager.instance.Add(generator.creature[0]);
        first.observationLevel = 4;
        GameManager.currentGameManager.ClearAction();
        Pump(4.0);
        long day1 = Base + 11_000_000 + 1;
        long obs = Base + 10_000_000 + 100 + 4;
        for (int i = 0; i < 100 && !(session.ServerChecked.Contains(day1) && session.ServerChecked.Contains(obs)); i++)
            Pump(0.1);
        Check(session.ServerChecked.Contains(day1), "server confirmed 'Day 01 Cleared'");
        Check(session.ServerChecked.Contains(obs), "server confirmed 'Abnormality #01 - Observation Level 4'");
        Console.WriteLine("  items after checks: " + session.Items.Count + ", server checked " +
                          session.ServerChecked.Count);
        Console.WriteLine(_failures == 0 ? "ALL CHECKS PASSED" : _failures + " CHECK(S) FAILED");
        return _failures == 0 ? 0 : 1;
    }
}
