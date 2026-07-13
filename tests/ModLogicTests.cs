using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LawgiversControl;

namespace Lawgivers
{
    public static class Instance
    {
        public static World World { get; set; }
        internal static ClientData Player { get; set; }
    }

    public sealed class World
    {
        public Dictionary<int, Person> people = new Dictionary<int, Person>();
        public Dictionary<int, Party> parties = new Dictionary<int, Party>();
        public Dictionary<int, Nation> nations = new Dictionary<int, Nation>();
    }

    public sealed class Person
    {
        public sealed class Attribute
        {
            private int valueCustom;
            public Attribute(int value) { valueCustom = value; }
            public int Value { get { return valueCustom; } }
            internal void Change(float delta) { valueCustom += (int)delta; }
        }

        public int id;
        public string Name;
        private int partyID;
        public long Wealth;
        public int PartyID { get { return partyID; } }
        public Attribute loyalty;
        public Attribute energy;
        public Attribute experience;
        public Attribute recognition;
        public Attribute popularity;
        public Attribute charm;
        public Attribute eloquence;
        public Attribute cunning;
        public Attribute influence;

        public Person(int id, string name, int partyId, long wealth)
        {
            this.id = id; Name = name; partyID = partyId; Wealth = wealth;
            loyalty = energy = experience = recognition = popularity = charm = eloquence = cunning = influence = new Attribute(10);
            loyalty = new Attribute(10); energy = new Attribute(10); experience = new Attribute(10);
            recognition = new Attribute(10); popularity = new Attribute(10); charm = new Attribute(10);
            eloquence = new Attribute(10); cunning = new Attribute(10); influence = new Attribute(10);
        }

        internal void SetParty(Party party) { partyID = party.id; }
    }

    public sealed class Fund { public long value; }

    public sealed class Party
    {
        public int id;
        public string Name;
        public string nameOriginal;
        public string acronymOriginal;
        public Fund money = new Fund();
        public Party(int id, string name, long money) { this.id = id; Name = name; nameOriginal = name; this.money.value = money; }
    }

    public sealed class Legion { public int units; }

    public sealed class Nation
    {
        public int id;
        public string Name;
        private readonly List<Legion> legions = new List<Legion>();
        internal List<Legion> Legions { get { return legions; } }
        internal int get_Missiles() { return 0; }
        internal void AddLegions(int count) { while (count-- > 0) legions.Add(new Legion()); }
        internal void RemoveLegions(int count) { while (count-- > 0 && legions.Count > 0) legions.RemoveAt(legions.Count - 1); }
        public Nation(int id, string name, int armies) { this.id = id; Name = name; AddLegions(armies); }
    }

    public sealed class ActionPoints
    {
        public int points;
        public int Max;
        public void Set(int value) { points = value; }
    }

    public sealed class ClientData { public ActionPoints actions = new ActionPoints(); }
}

internal static class ModLogicTests
{
    private static int Main()
    {
        string temp = Path.Combine(Path.GetTempPath(), "LawgiversControlTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var alpha = new Lawgivers.Party(1, "Alpha", 100);
            var beta = new Lawgivers.Party(2, "Beta", 200);
            var alice = new Lawgivers.Person(10, "Alice", 1, 10);
            var bob = new Lawgivers.Person(11, "Bob", 1, 20);
            var eve = new Lawgivers.Person(12, "Eve", 2, 30);
            var nation = new Lawgivers.Nation(20, "Testland", 1);
            Lawgivers.Instance.World = new Lawgivers.World();
            Lawgivers.Instance.World.parties.Add(1, alpha);
            Lawgivers.Instance.World.parties.Add(2, beta);
            Lawgivers.Instance.World.people.Add(10, alice);
            Lawgivers.Instance.World.people.Add(11, bob);
            Lawgivers.Instance.World.people.Add(12, eve);
            Lawgivers.Instance.World.nations.Add(20, nation);
            Lawgivers.Instance.Player = new Lawgivers.ClientData();

            var config = new ModConfig
            {
                DumpCatalog = true,
                ForceProbabilitySuccess = true,
                ActionPoints = new ActionPointRule { Value = 42, Max = 150 },
                People = new List<PersonRule>
                {
                    new PersonRule
                    {
                        Id = 10,
                        Changes = new PersonChanges
                        {
                            Wealth = 999,
                            Loyalty = 88,
                            PartyId = 2,
                            Attributes = new Dictionary<string, int> { { "cunning", 77 } }
                        }
                    }
                },
                Parties = new List<PartyRule>
                {
                    new PartyRule
                    {
                        Id = 1,
                        Money = 555,
                        Members = new PersonChanges
                        {
                            Wealth = 111,
                            Attributes = new Dictionary<string, int> { { "energy", 66 } }
                        }
                    }
                },
                Nations = new List<NationRule>
                {
                    new NationRule { Id = 20, Armies = 3, ArmyUnits = 25, Missiles = 7 }
                }
            };
            config.Normalize();

            Type mod = typeof(LawgiversControlMod);
            SetStatic(mod, "_config", config);
            string catalog = Path.Combine(temp, "catalog.json");
            string report = Path.Combine(temp, "last-apply.json");
            SetStatic(mod, "_catalogPath", catalog);
            SetStatic(mod, "_reportPath", report);
            bool applied = (bool)InvokeStatic(mod, "TryApply");

            Check(applied, "configuration applied");
            Check(alice.Wealth == 999, "specific person wealth");
            Check(alice.loyalty.Value == 88, "specific person loyalty");
            Check(alice.cunning.Value == 77, "specific person ability");
            Check(alice.PartyID == 2, "specific person party transfer");
            Check(bob.Wealth == 111 && bob.energy.Value == 66, "party-member batch changes");
            Check(eve.Wealth == 30, "unmatched person unchanged");
            Check(alpha.money.value == 555, "party money");
            Check(Lawgivers.Instance.Player.actions.points == 42 && Lawgivers.Instance.Player.actions.Max == 150, "action points");
            Check(nation.Legions.Count == 3 && nation.Legions.All(x => x.units == 25), "armies and units");

            object[] missileArgs = { nation, 0 };
            bool runOriginal = (bool)InvokeStatic(mod, "MissilesPrefix", missileArgs);
            Check(!runOriginal && (int)missileArgs[1] == 7, "missile override");

            object[] probabilityArgs = { false };
            bool runProbabilityOriginal = (bool)InvokeStatic(mod, "ProbabilityPrefix", probabilityArgs);
            Check(!runProbabilityOriginal && (bool)probabilityArgs[0], "100 percent probability prefix");
            config.ForceProbabilitySuccess = false;
            probabilityArgs = new object[] { false };
            runProbabilityOriginal = (bool)InvokeStatic(mod, "ProbabilityPrefix", probabilityArgs);
            Check(runProbabilityOriginal && !(bool)probabilityArgs[0], "probability override can be disabled");
            config.ForceProbabilitySuccess = true;
            Check(File.Exists(catalog) && File.ReadAllText(catalog).Contains("Alice") && File.ReadAllText(catalog).Contains("Testland"), "catalog output");
            Check(File.Exists(report) && File.ReadAllText(report).Contains("\"Value\": 42") && File.ReadAllText(report).Contains("\"Money\": 555"), "live apply report");

            string runtimeConfig = Path.Combine(temp, "config.json");
            File.WriteAllText(runtimeConfig, "{\"AutoApply\":true,\"DumpCatalog\":false,\"ActionPoints\":{\"Value\":77,\"Max\":160},\"People\":[],\"Parties\":[],\"Nations\":[]}");
            SetStatic(mod, "_configPath", runtimeConfig);
            SetStatic(mod, "_configWriteUtc", DateTime.MinValue);
            SetStatic(mod, "_applyPending", false);
            InvokeStatic(mod, "MarkApplyPendingPrefix");
            InvokeStatic(mod, "WeekPostfix");
            Check(Lawgivers.Instance.Player.actions.points == 77 && Lawgivers.Instance.Player.actions.Max == 160, "config reload on game load");
            Console.WriteLine("PASS: all LawgiversControl logic tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex);
            return 1;
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Assertion failed: " + name);
        Console.WriteLine("PASS: " + name);
    }

    private static void SetStatic(Type type, string name, object value)
    {
        type.GetField(name, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, value);
    }

    private static object InvokeStatic(Type type, string name, params object[] args)
    {
        return type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
    }
}
