using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;

[assembly: MelonInfo(typeof(LawgiversControl.LawgiversControlMod), "Lawgivers II Control", "1.3.1", "OpenAI")]
[assembly: MelonGame("SomniumSoft", "Lawgivers II")]

namespace LawgiversControl
{
    public sealed partial class LawgiversControlMod : MelonMod
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static ModConfig _config;
        private static bool _probabilityPatched;
        private static bool _missilesPatched;
        private static bool _loadHooksPatched;
        private static bool _applyPending = true;
        private static HarmonyLib.Harmony _harmony;
        private static string _catalogPath;
        private static string _reportPath;

        private string _directory;
        private static string _configPath;
        private string _applyPath;
        private string _selfTestFlagPath;
        private string _selfTestReportPath;
        private static DateTime _configWriteUtc;
        private DateTime _nextPollUtc;
        private bool _firstApplyPending = true;
        private bool _menuVisible;
        private static readonly string[] PersonAttributeNames =
        {
            "recognition", "energy", "experience", "loyalty", "popularity",
            "charm", "eloquence", "cunning", "influence"
        };

        partial void EnsureControlUi();
        partial void RefreshControlUi();

        public override void OnInitializeMelon()
        {
            _menuVisible = string.Equals(Environment.GetEnvironmentVariable("LAW_GIVERS_CONTROL_OPEN_UI"), "1", StringComparison.Ordinal);
            _directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "LawgiversControl");
            _configPath = Path.Combine(_directory, "config.json");
            _applyPath = Path.Combine(_directory, "apply.flag");
            _selfTestFlagPath = Path.Combine(_directory, "runtime-self-test.flag");
            _selfTestReportPath = Path.Combine(_directory, "runtime-self-test.json");
            _catalogPath = Path.Combine(_directory, "catalog.json");
            _reportPath = Path.Combine(_directory, "last-apply.json");
            Directory.CreateDirectory(_directory);
            if (!File.Exists(_configPath))
                File.WriteAllText(_configPath, JsonConvert.SerializeObject(ModConfig.CreateDefault(), Formatting.Indented));
            LoadConfig();
            if (_menuVisible)
                MelonLogger.Msg("Control UI auto-open enabled for runtime verification.");
            _harmony = new HarmonyLib.Harmony("openai.lawgivers2.control");
            MelonLogger.Msg("Ready. Configuration is applied whenever a game scene is loaded.");
        }

        public override void OnLateInitializeMelon()
        {
            EnsureControlUi();
            TryInstallPatches();
            TryRunRuntimeSelfTest();
        }

        public override void OnUpdate()
        {
            if (DateTime.UtcNow < _nextPollUtc)
                return;
            _nextPollUtc = DateTime.UtcNow.AddSeconds(Math.Max(0.25, _config == null ? 1.0 : _config.PollSeconds));

            RefreshControlUi();
            TryInstallPatches();
            bool requested = false;
            try
            {
                DateTime write = File.GetLastWriteTimeUtc(_configPath);
                if (write != _configWriteUtc)
                {
                    LoadConfig();
                    requested = _config != null && _config.AutoApply;
                }
                if (File.Exists(_applyPath))
                {
                    File.Delete(_applyPath);
                    requested = true;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error("Config polling failed: " + ex.Message);
            }

            if (_firstApplyPending && _config != null && _config.AutoApply)
                requested = true;
            if (requested && TryApply())
            {
                _firstApplyPending = false;
                _applyPending = false;
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            EnsureControlUi();
            LoadConfig();
            _applyPending = true;
            TryInstallPatches();
            TryRunRuntimeSelfTest();
            if (_config != null && _config.AutoApply && TryApply())
            {
                _firstApplyPending = false;
                _applyPending = false;
            }
        }

        private static void LoadConfig()
        {
            try
            {
                _config = JsonConvert.DeserializeObject<ModConfig>(File.ReadAllText(_configPath)) ?? ModConfig.CreateDefault();
                _config.Normalize();
                _configWriteUtc = File.GetLastWriteTimeUtc(_configPath);
                MelonLogger.Msg("Configuration loaded.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("Invalid config.json; keeping the previous configuration: " + ex.Message);
            }
        }

        private static void TryInstallPatches()
        {
            if (_harmony == null || _config == null)
                return;
            if (!_loadHooksPatched)
            {
                Type instance = FindType("Lawgivers.Instance");
                MethodInfo week = instance == null ? null : instance.GetMethod("get_Week", All);
                if (week != null)
                {
                    _harmony.Patch(week, postfix: new HarmonyMethod(typeof(LawgiversControlMod).GetMethod("WeekPostfix", All)));
                    foreach (MethodInfo load in instance.GetMethods(All).Where(m => m.Name == "Load"))
                        _harmony.Patch(load, prefix: new HarmonyMethod(typeof(LawgiversControlMod).GetMethod("MarkApplyPendingPrefix", All)));
                    _loadHooksPatched = true;
                    MelonLogger.Msg("Game-load application hooks enabled.");
                }
            }
            if (_config.ForceProbabilitySuccess && !_probabilityPatched)
            {
                int patched = 0;
                TryLoadAssembly("Somnium.Math");
                TryLoadAssembly("Il2CppSomnium.Math");
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!string.Equals(assembly.GetName().Name, "Somnium.Math", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(assembly.GetName().Name, "Il2CppSomnium.Math", StringComparison.OrdinalIgnoreCase))
                        continue;
                    Type random = SafeTypes(assembly).FirstOrDefault(t => t.FullName == "Random" || t.FullName == "Il2Cpp.Random");
                    if (random == null)
                        continue;
                    foreach (MethodInfo method in random.GetMethods(All).Where(m => m.Name == "Probability" && m.ReturnType == typeof(bool)))
                    {
                        _harmony.Patch(method, new HarmonyMethod(typeof(LawgiversControlMod).GetMethod("ProbabilityPrefix", All)));
                        patched++;
                    }
                    Type data = random.GetNestedType("Data", All);
                    if (data != null)
                    {
                        foreach (MethodInfo method in data.GetMethods(All).Where(m => m.Name == "Probability" && m.ReturnType == typeof(bool)))
                        {
                            _harmony.Patch(method, new HarmonyMethod(typeof(LawgiversControlMod).GetMethod("ProbabilityPrefix", All)));
                            patched++;
                        }
                    }
                }
                _probabilityPatched = patched > 0;
                if (_probabilityPatched)
                    MelonLogger.Msg("100% probability mode enabled (" + patched + " probability methods patched). ");
            }

            if (!_missilesPatched && _config.Nations.Any(n => n.Missiles.HasValue))
            {
                Type nation = FindType("Lawgivers.Nation");
                MethodInfo getter = nation == null ? null : nation.GetMethod("get_Missiles", All);
                if (getter != null)
                {
                    _harmony.Patch(getter, new HarmonyMethod(typeof(LawgiversControlMod).GetMethod("MissilesPrefix", All)));
                    _missilesPatched = true;
                    MelonLogger.Msg("Nation missile count override enabled.");
                }
            }
        }

        private static bool ProbabilityPrefix(ref bool __result)
        {
            if (_config == null || !_config.ForceProbabilitySuccess)
                return true;
            __result = true;
            return false;
        }

        private static bool MissilesPrefix(object __instance, ref int __result)
        {
            NationRule rule = _config == null ? null : _config.Nations.FirstOrDefault(r => r.Missiles.HasValue && MatchesNation(__instance, r));
            if (rule == null)
                return true;
            __result = Math.Max(0, rule.Missiles.Value);
            return false;
        }

        private static void MarkApplyPendingPrefix()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_configPath) && File.Exists(_configPath) &&
                    File.GetLastWriteTimeUtc(_configPath) != _configWriteUtc)
                {
                    LoadConfig();
                    TryInstallPatches();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error("Config reload during game load failed: " + ex.Message);
            }
            _applyPending = true;
        }

        private static void WeekPostfix()
        {
            if (_applyPending && _config != null && _config.AutoApply && TryApply())
                _applyPending = false;
        }

        private static bool TryApply()
        {
            try
            {
                Type instance = FindType("Lawgivers.Instance");
                object world = instance == null ? null : GetStatic(instance, "World");
                if (world == null)
                    return false;

                List<object> parties = Values(Get(world, "parties")).ToList();
                List<object> people = Values(Get(world, "people")).ToList();
                int peopleChanged = 0;
                int partiesChanged = 0;
                int nationsChanged = 0;

                foreach (PersonRule rule in _config.People)
                {
                    foreach (object person in people.Where(p => MatchesPerson(p, rule, parties)))
                    {
                        ApplyPerson(person, rule.Changes, parties);
                        peopleChanged++;
                    }
                }

                foreach (PartyRule rule in _config.Parties)
                {
                    object party = parties.FirstOrDefault(p => MatchesParty(p, rule.Id, rule.Name));
                    if (party == null)
                    {
                        MelonLogger.Warning("Party not found: " + (rule.Name ?? Convert.ToString(rule.Id, CultureInfo.InvariantCulture)));
                        continue;
                    }
                    if (rule.Money.HasValue)
                    {
                        object fund = Get(party, "money");
                        if (fund != null)
                            Set(fund, "value", rule.Money.Value);
                    }
                    if (rule.Members != null)
                    {
                        int partyId = ToInt(Get(party, "id"), int.MinValue);
                        foreach (object person in people.Where(p => ToInt(Get(p, "PartyID", "partyID"), int.MinValue) == partyId))
                        {
                            ApplyPerson(person, rule.Members, parties);
                            peopleChanged++;
                        }
                    }
                    partiesChanged++;
                }

                ApplyActionPoints(instance);

                List<object> nations = Values(Get(world, "nations")).ToList();
                if (_config.DumpCatalog)
                    WriteCatalog(people, parties, nations);
                foreach (NationRule rule in _config.Nations)
                {
                    object nation = nations.FirstOrDefault(n => MatchesNation(n, rule));
                    if (nation == null)
                    {
                        MelonLogger.Warning("Nation not found: " + (rule.Name ?? Convert.ToString(rule.Id, CultureInfo.InvariantCulture)));
                        continue;
                    }
                    ApplyNation(nation, rule);
                    nationsChanged++;
                }

                WriteApplyReport(instance, people, parties, nations);

                MelonLogger.Msg(string.Format(CultureInfo.InvariantCulture,
                    "Applied configuration: {0} person operation(s), {1} party operation(s), {2} nation operation(s).",
                    peopleChanged, partiesChanged, nationsChanged));
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error("Apply failed: " + ex);
                return false;
            }
        }

        private static void ApplyPerson(object person, PersonChanges changes, List<object> parties)
        {
            if (changes == null)
                return;
            if (changes.Wealth.HasValue)
                Set(person, "Wealth", changes.Wealth.Value);
            if (changes.Loyalty.HasValue)
                SetAttribute(person, "loyalty", changes.Loyalty.Value);
            foreach (KeyValuePair<string, int> pair in changes.Attributes)
                SetAttribute(person, pair.Key, pair.Value);

            object targetParty = null;
            if (changes.PartyId.HasValue || !string.IsNullOrWhiteSpace(changes.Party))
                targetParty = parties.FirstOrDefault(p => MatchesParty(p, changes.PartyId, changes.Party));
            if (targetParty != null)
            {
                if (!Invoke(person, "SetParty", targetParty))
                    Set(person, "PartyID", ToInt(Get(targetParty, "id"), -1));
            }
            else if (changes.PartyId.HasValue || !string.IsNullOrWhiteSpace(changes.Party))
            {
                MelonLogger.Warning("Target party not found for person " + FriendlyName(person) + ": " + (changes.Party ?? changes.PartyId.ToString()));
            }
        }

        private static int MaxPersonAttributes(object person)
        {
            if (person == null)
                return 0;
            int changed = 0;
            foreach (string name in PersonAttributeNames)
            {
                object attribute = Get(person, name);
                if (attribute == null)
                    continue;
                int maximum = GetAttributeMaximum(attribute);
                SetAttribute(person, name, maximum);
                if (ToInt(Get(attribute, "Value", "valueCustom"), int.MinValue) == maximum)
                    changed++;
            }
            return changed;
        }

        private static int GetAttributeMaximum(object attribute)
        {
            int maximum = ToInt(InvokeResult(attribute, "Max"), 100);
            return maximum <= 0 || maximum > 255 ? 100 : maximum;
        }

        private static int MaxPartyMemberAttributes(object party, IEnumerable<object> people, out int peopleChanged)
        {
            peopleChanged = 0;
            if (party == null || people == null)
                return 0;
            int partyId = ToInt(Get(party, "id"), int.MinValue);
            int attributesChanged = 0;
            foreach (object person in people.Where(p => ToInt(Get(p, "PartyID", "partyID"), int.MinValue) == partyId).ToList())
            {
                attributesChanged += MaxPersonAttributes(person);
                peopleChanged++;
            }
            return attributesChanged;
        }

        private static bool AddPersonMoney(object person, long amount, out long result)
        {
            return AddLongValue(person, new[] { "Wealth", "wealth" }, amount, out result);
        }

        private static bool AddPartyMoney(object party, long amount, out long result)
        {
            return AddLongValue(Get(party, "money"), new[] { "value", "Value" }, amount, out result);
        }

        private static bool AddNationMoney(object nation, long amount, out long result)
        {
            return AddLongValue(Get(nation, "budget"), new[] { "value", "Value" }, amount, out result);
        }

        private static bool AddLongValue(object target, string[] names, long amount, out long result)
        {
            result = 0;
            if (target == null || names == null || names.Length == 0)
                return false;
            long current = ToLong(Get(target, names), 0);
            try { result = checked(current + amount); }
            catch (OverflowException) { return false; }
            foreach (string name in names)
            {
                if (Set(target, name, result))
                    return ToLong(Get(target, names), long.MinValue) == result;
            }
            return false;
        }

        private static bool AddActionPoints(object actions, int amount, out int result)
        {
            result = 0;
            if (actions == null)
                return false;
            int current = ToInt(Get(actions, "Points", "points"), 0);
            try { result = checked(current + amount); }
            catch (OverflowException) { return false; }
            result = Math.Max(0, result);
            if (Set(actions, "Points", result) || Set(actions, "points", result))
                return ToInt(Get(actions, "Points", "points"), int.MinValue) == result;
            if (!Invoke(actions, "Set", result))
                return false;
            return ToInt(Get(actions, "Points", "points"), int.MinValue) == result;
        }

        private static bool TryGetWorld(out Type instanceType, out object world, out List<object> people, out List<object> parties, out List<object> nations)
        {
            instanceType = FindType("Lawgivers.Instance");
            world = instanceType == null ? null : GetStatic(instanceType, "World");
            people = world == null ? new List<object>() : Values(Get(world, "people")).Where(x => x != null).ToList();
            parties = world == null ? new List<object>() : Values(Get(world, "parties")).Where(x => x != null).ToList();
            nations = world == null ? new List<object>() : Values(Get(world, "nations")).Where(x => x != null).ToList();
            return world != null;
        }

        private static void RefreshApplyReport(Type instanceType, List<object> people, List<object> parties, List<object> nations)
        {
            try { WriteApplyReport(instanceType, people, parties, nations); }
            catch (Exception ex) { MelonLogger.Warning("UI result report failed: " + ex.Message); }
        }

        private static void SetAttribute(object person, string name, int target)
        {
            object attribute = Get(person, name);
            if (attribute == null)
            {
                MelonLogger.Warning("Unknown person attribute '" + name + "' for " + FriendlyName(person));
                return;
            }
            int current = ToInt(Get(attribute, "Value", "valueCustom"), 0);
            if (!Invoke(attribute, "Change", (float)(target - current)))
                Set(attribute, "valueCustom", Math.Max(0, Math.Min(255, target)));
        }

        private static void ApplyActionPoints(Type instance)
        {
            if (_config.ActionPoints == null || (!_config.ActionPoints.Value.HasValue && !_config.ActionPoints.Max.HasValue))
                return;
            object player = GetStatic(instance, "Player");
            object actions = player == null ? null : Get(player, "actions");
            if (actions == null)
                return;
            ApplyActionPointsObject(actions, _config.ActionPoints);
        }

        private static void ApplyActionPointsObject(object actions, ActionPointRule rule)
        {
            if (actions == null || rule == null)
                return;
            if (rule.Max.HasValue)
                Set(actions, "Max", Math.Max(1, rule.Max.Value));
            if (rule.Value.HasValue)
                Invoke(actions, "Set", Math.Max(0, rule.Value.Value));
        }

        private static void ApplyNation(object nation, NationRule rule)
        {
            if (rule.Armies.HasValue)
            {
                int current = Values(Get(nation, "Legions")).Count();
                int target = Math.Max(0, rule.Armies.Value);
                if (target > current)
                    Invoke(nation, "AddLegions", target - current);
                else if (target < current)
                    Invoke(nation, "RemoveLegions", current - target);
            }
            if (rule.ArmyUnits.HasValue)
            {
                foreach (object legion in Values(Get(nation, "Legions")))
                    Set(legion, "units", Math.Max(0, rule.ArmyUnits.Value));
            }
        }

        private static void WriteCatalog(List<object> people, List<object> parties, List<object> nations)
        {
            var partyNames = parties.ToDictionary(
                p => ToInt(Get(p, "id"), int.MinValue),
                p => FriendlyName(p));
            var catalog = new
            {
                GeneratedUtc = DateTime.UtcNow,
                People = people.Select(p => new
                {
                    Id = ToInt(Get(p, "id"), -1),
                    Name = FriendlyName(p),
                    PartyId = ToInt(Get(p, "PartyID", "partyID"), -1),
                    Party = partyNames.ContainsKey(ToInt(Get(p, "PartyID", "partyID"), int.MinValue))
                        ? partyNames[ToInt(Get(p, "PartyID", "partyID"), int.MinValue)] : null
                }).OrderBy(p => p.Name).ToList(),
                Parties = parties.Select(p => new { Id = ToInt(Get(p, "id"), -1), Name = FriendlyName(p) }).OrderBy(p => p.Name).ToList(),
                Nations = nations.Select(n => new { Id = ToInt(Get(n, "id"), -1), Name = FriendlyName(n) }).OrderBy(n => n.Name).ToList()
            };
            File.WriteAllText(_catalogPath, JsonConvert.SerializeObject(catalog, Formatting.Indented));
        }

        private static void WriteApplyReport(Type instance, List<object> people, List<object> parties, List<object> nations)
        {
            if (string.IsNullOrWhiteSpace(_reportPath))
                return;
            string[] attributeNames = { "recognition", "energy", "experience", "loyalty", "popularity", "charm", "eloquence", "cunning", "influence" };
            object player = GetStatic(instance, "Player");
            object actions = player == null ? null : Get(player, "actions");
            var report = new
            {
                AppliedUtc = DateTime.UtcNow,
                People = people.Select(p => new
                {
                    Id = ToInt(Get(p, "id"), -1),
                    Name = FriendlyName(p),
                    PartyId = ToInt(Get(p, "PartyID", "partyID"), -1),
                    Wealth = Get(p, "Wealth", "wealth"),
                    Attributes = attributeNames.ToDictionary(n => n, n => ToInt(Get(Get(p, n), "Value", "valueCustom"), -1))
                }).OrderBy(p => p.Name).ToList(),
                Parties = parties.Select(p => new
                {
                    Id = ToInt(Get(p, "id"), -1),
                    Name = FriendlyName(p),
                    Money = Get(Get(p, "money"), "value")
                }).OrderBy(p => p.Name).ToList(),
                ActionPoints = actions == null ? null : new
                {
                    Value = ToInt(Get(actions, "Points", "points"), -1),
                    Max = ToInt(Get(actions, "Max"), -1)
                },
                Nations = nations.Select(n => new
                {
                    Id = ToInt(Get(n, "id"), -1),
                    Name = FriendlyName(n),
                    Armies = Values(Get(n, "Legions")).Count(),
                    ArmyUnits = Values(Get(n, "Legions")).Select(l => ToInt(Get(l, "units"), -1)).ToList(),
                    Missiles = ToInt(Get(n, "Missiles"), -1)
                }).OrderBy(n => n.Name).ToList()
            };
            File.WriteAllText(_reportPath, JsonConvert.SerializeObject(report, Formatting.Indented));
        }

        private static bool MatchesPerson(object person, PersonRule rule, List<object> parties)
        {
            if (rule.Id.HasValue && ToInt(Get(person, "id"), int.MinValue) != rule.Id.Value)
                return false;
            if (!string.IsNullOrWhiteSpace(rule.Name) && !Same(FriendlyName(person), rule.Name))
                return false;
            if (!string.IsNullOrWhiteSpace(rule.Party) || rule.PartyId.HasValue)
            {
                int partyId = ToInt(Get(person, "PartyID", "partyID"), int.MinValue);
                object party = parties.FirstOrDefault(p => ToInt(Get(p, "id"), int.MaxValue) == partyId);
                if (party == null || !MatchesParty(party, rule.PartyId, rule.Party))
                    return false;
            }
            return rule.Id.HasValue || !string.IsNullOrWhiteSpace(rule.Name) || rule.PartyId.HasValue || !string.IsNullOrWhiteSpace(rule.Party);
        }

        private static bool MatchesParty(object party, int? id, string name)
        {
            if (id.HasValue && ToInt(Get(party, "id"), int.MinValue) != id.Value)
                return false;
            if (!string.IsNullOrWhiteSpace(name))
            {
                string[] names = { Text(Get(party, "Name")), Text(Get(party, "nameOriginal")), Text(Get(party, "acronymOriginal")), Text(Get(party, "nameInternational")), Text(Get(party, "acronymInternational")) };
                if (!names.Any(n => Same(n, name)))
                    return false;
            }
            return id.HasValue || !string.IsNullOrWhiteSpace(name);
        }

        private static bool MatchesNation(object nation, NationRule rule)
        {
            if (nation == null || rule == null)
                return false;
            if (rule.Id.HasValue && ToInt(Get(nation, "id"), int.MinValue) != rule.Id.Value)
                return false;
            if (!string.IsNullOrWhiteSpace(rule.Name))
            {
                string[] names = { Text(Get(nation, "Name")), Text(Get(nation, "name")), Text(Get(nation, "nameOriginal")), Text(Get(nation, "nameInternational")) };
                if (!names.Any(n => Same(n, rule.Name)))
                    return false;
            }
            return rule.Id.HasValue || !string.IsNullOrWhiteSpace(rule.Name);
        }

        private static string FriendlyName(object value)
        {
            return Text(Get(value, "Name", "name", "nameOriginal")) ?? "#" + ToInt(Get(value, "id"), -1);
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false) ?? assembly.GetType("Il2Cpp" + fullName, false) ?? assembly.GetType("Il2Cpp." + fullName, false);
                if (type != null)
                    return type;
            }
            int separator = fullName.IndexOf('.');
            if (separator > 0)
            {
                string root = fullName.Substring(0, separator);
                foreach (string assemblyName in new[] { root, "Il2Cpp" + root })
                {
                    Assembly assembly = TryLoadAssembly(assemblyName);
                    Type type = assembly == null ? null : (assembly.GetType(fullName, false) ?? assembly.GetType("Il2Cpp" + fullName, false) ?? assembly.GetType("Il2Cpp." + fullName, false));
                    if (type != null)
                        return type;
                }
            }
            return null;
        }

        private void TryRunRuntimeSelfTest()
        {
            if (string.IsNullOrWhiteSpace(_selfTestFlagPath) || !File.Exists(_selfTestFlagPath))
                return;

            var checks = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            string error = null;
            ModConfig originalConfig = _config;
            try
            {
                Type partyType = FindType("Lawgivers.Party");
                Type personType = FindType("Lawgivers.Person");
                Type fundType = FindType("Fund");
                Type actionType = FindType("ActionPoints");
                Type nationType = FindType("Lawgivers.Nation");
                Type legionType = FindType("Lawgivers.Legion");
                if (partyType == null || personType == null || fundType == null || actionType == null || nationType == null || legionType == null)
                    throw new InvalidOperationException("One or more generated IL2CPP game types could not be found.");

                object partyA = Activator.CreateInstance(partyType);
                object partyB = Activator.CreateInstance(partyType);
                Set(partyA, "id", 910001);
                Set(partyB, "id", 910002);
                Set(partyA, "nameOriginal", "Runtime Alpha");
                Set(partyB, "nameOriginal", "Runtime Beta");
                object fund = Activator.CreateInstance(fundType);
                Set(fund, "value", 100L);
                Set(partyA, "money", fund);

                object person = CreateUninitializedRuntimeObject(personType);
                Set(person, "id", 920001);
                Set(person, "Name", "Runtime Person");
                Set(person, "PartyID", 910001);
                Set(person, "Wealth", 10L);
                Type attributeType = personType.GetNestedType("Attribute", All);
                if (attributeType == null)
                    throw new InvalidOperationException("Person.Attribute runtime type was not found.");
                foreach (string name in new[] { "recognition", "energy", "experience", "loyalty", "popularity", "charm", "eloquence", "cunning", "influence" })
                {
                    object attribute = Activator.CreateInstance(attributeType);
                    Set(attribute, "personID", 920001);
                    Set(attribute, "valueCustom", 10);
                    Set(person, name, attribute);
                }

                var parties = new List<object> { partyA, partyB };
                var selector = new PersonRule { Id = 920001, PartyId = 910001 };
                selector.Normalize();
                checks["SpecificPersonSelector"] = MatchesPerson(person, selector, parties);
                var groupSelector = new PersonRule { PartyId = 910001 };
                groupSelector.Normalize();
                checks["PartyMemberSelector"] = MatchesPerson(person, groupSelector, parties);

                ApplyPerson(person, new PersonChanges
                {
                    Wealth = 999L,
                    Loyalty = 88,
                    PartyId = 910002,
                    Attributes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { { "cunning", 77 }, { "energy", 66 } }
                }, parties);
                checks["PersonWealth"] = Convert.ToInt64(Get(person, "Wealth", "wealth"), CultureInfo.InvariantCulture) == 999L;
                checks["PersonLoyalty"] = ToInt(Get(Get(person, "loyalty"), "Value", "valueCustom"), -1) == 88;
                checks["PersonAbility"] = ToInt(Get(Get(person, "cunning"), "Value", "valueCustom"), -1) == 77;
                checks["PersonPartyChange"] = ToInt(Get(person, "PartyID", "partyID"), -1) == 910002;

                int maxed = MaxPersonAttributes(person);
                checks["UiPersonMaxAll"] = maxed == PersonAttributeNames.Length && PersonAttributeNames.All(n =>
                {
                    object attribute = Get(person, n);
                    return ToInt(Get(attribute, "Value", "valueCustom"), -1) == GetAttributeMaximum(attribute);
                });
                long personMoneyResult;
                checks["UiPersonMoneyAdd"] = AddPersonMoney(person, 101L, out personMoneyResult) && personMoneyResult == 1100L;

                Set(person, "PartyID", 910001);
                int groupPeople;
                int groupAttributes = MaxPartyMemberAttributes(partyA, new List<object> { person }, out groupPeople);
                checks["UiPartyMembersMaxAll"] = groupPeople == 1 && groupAttributes == PersonAttributeNames.Length;

                Set(Get(partyA, "money"), "value", 555L);
                checks["PartyMoney"] = Convert.ToInt64(Get(Get(partyA, "money"), "value"), CultureInfo.InvariantCulture) == 555L;
                long partyMoneyResult;
                checks["UiPartyMoneyAdd"] = AddPartyMoney(partyA, 100L, out partyMoneyResult) && partyMoneyResult == 655L;

                object actions = Activator.CreateInstance(actionType);
                ApplyActionPointsObject(actions, new ActionPointRule { Value = 42, Max = 150 });
                checks["ActionPoints"] = ToInt(Get(actions, "Points", "points"), -1) == 42 && ToInt(Get(actions, "Max"), -1) == 150;
                int actionPointsResult;
                checks["UiActionPointsAdd"] = AddActionPoints(actions, 8, out actionPointsResult) && actionPointsResult == 50;

                object nation = Activator.CreateInstance(nationType);
                Set(nation, "id", 930001);
                object nationBudget = Activator.CreateInstance(fundType);
                Set(nationBudget, "value", 1000L);
                Set(nation, "budget", nationBudget);
                long nationMoneyResult;
                checks["UiNationMoneyAdd"] = AddNationMoney(nation, 250L, out nationMoneyResult) && nationMoneyResult == 1250L;
                object legion = Activator.CreateInstance(legionType);
                Set(legion, "units", 5);
                Set(legion, "units", 25);
                checks["ArmyUnits"] = ToInt(Get(legion, "units"), -1) == 25;
                checks["ArmyCountMethods"] = nationType.GetMethod("AddLegions", All) != null && nationType.GetMethod("RemoveLegions", All) != null;

                _config = new ModConfig
                {
                    ForceProbabilitySuccess = true,
                    Nations = new List<NationRule> { new NationRule { Id = 930001, Missiles = 7 } }
                };
                _config.Normalize();
                int missileResult = 0;
                checks["Missiles"] = !MissilesPrefix(nation, ref missileResult) && missileResult == 7;
                bool probabilityResult = false;
                checks["Probability100Percent"] = !ProbabilityPrefix(ref probabilityResult) && probabilityResult;
                _config.ForceProbabilitySuccess = false;
                probabilityResult = false;
                checks["ProbabilityCanDisable"] = ProbabilityPrefix(ref probabilityResult) && !probabilityResult;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                MelonLogger.Error("Runtime self-test failed: " + ex);
            }
            finally
            {
                _config = originalConfig;
                bool passed = error == null && checks.Count >= 12 && checks.Values.All(v => v is bool && (bool)v);
                var report = new { GeneratedUtc = DateTime.UtcNow, Passed = passed, Checks = checks, Error = error };
                File.WriteAllText(_selfTestReportPath, JsonConvert.SerializeObject(report, Formatting.Indented));
                try { File.Delete(_selfTestFlagPath); } catch { }
                MelonLogger.Msg("Runtime self-test " + (passed ? "passed." : "failed; see runtime-self-test.json."));
            }
        }

        private static Assembly TryLoadAssembly(string name)
        {
            try
            {
                return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
                    string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
                    ?? Assembly.Load(new AssemblyName(name));
            }
            catch
            {
                return null;
            }
        }

        private static object CreateUninitializedRuntimeObject(Type type)
        {
            Type pointerStore = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Il2CppInterop.Runtime.Il2CppClassPointerStore", false))
                .FirstOrDefault(t => t != null);
            Type il2Cpp = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Il2CppInterop.Runtime.IL2CPP", false))
                .FirstOrDefault(t => t != null);
            if (pointerStore == null || il2Cpp == null)
                throw new InvalidOperationException("Il2CppInterop allocation API was not found.");
            MethodInfo getClass = pointerStore.GetMethod("GetNativeClassPointer", All, null, new[] { typeof(Type) }, null);
            MethodInfo objectNew = il2Cpp.GetMethod("il2cpp_object_new", All, null, new[] { typeof(IntPtr) }, null);
            ConstructorInfo wrapper = type.GetConstructor(All, null, new[] { typeof(IntPtr) }, null);
            if (getClass == null || objectNew == null || wrapper == null)
                throw new InvalidOperationException("Generated IL2CPP wrapper allocation contract is incomplete for " + type.FullName + ".");
            IntPtr classPointer = (IntPtr)getClass.Invoke(null, new object[] { type });
            IntPtr objectPointer = (IntPtr)objectNew.Invoke(null, new object[] { classPointer });
            if (objectPointer == IntPtr.Zero)
                throw new InvalidOperationException("IL2CPP object allocation returned null for " + type.FullName + ".");
            return wrapper.Invoke(new object[] { objectPointer });
        }

        private static IEnumerable<Type> SafeTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
            catch { return Enumerable.Empty<Type>(); }
        }

        private static object Get(object target, params string[] names)
        {
            if (target == null)
                return null;
            Type type = target as Type ?? target.GetType();
            object instance = target is Type ? null : target;
            foreach (string name in names)
            {
                PropertyInfo property = type.GetProperty(name, All);
                if (property != null)
                {
                    try { return property.GetValue(instance, null); } catch { }
                }
                FieldInfo field = type.GetField(name, All);
                if (field != null)
                {
                    try { return field.GetValue(instance); } catch { }
                }
            }
            return null;
        }

        private static object GetStatic(Type type, params string[] names)
        {
            foreach (string name in names)
            {
                PropertyInfo property = type.GetProperty(name, All);
                if (property != null)
                {
                    try { return property.GetValue(null, null); } catch { }
                }
                MethodInfo getter = type.GetMethod("get_" + name, All);
                if (getter != null)
                {
                    try { return getter.Invoke(null, null); } catch { }
                }
            }
            return null;
        }

        private static bool Set(object target, string name, object value)
        {
            if (target == null)
                return false;
            Type type = target.GetType();
            PropertyInfo property = type.GetProperty(name, All);
            if (property != null && property.CanWrite)
            {
                try { property.SetValue(target, ConvertValue(value, property.PropertyType), null); return true; } catch { }
            }
            FieldInfo field = type.GetField(name, All);
            if (field != null)
            {
                try { field.SetValue(target, ConvertValue(value, field.FieldType)); return true; } catch { }
            }
            return Invoke(target, "set_" + name, value);
        }

        private static bool Invoke(object target, string name, params object[] args)
        {
            if (target == null)
                return false;
            Type type = target.GetType();
            foreach (MethodInfo method in type.GetMethods(All).Where(m => m.Name == name && m.GetParameters().Length == args.Length))
            {
                try
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    object[] converted = args.Select((a, i) => ConvertValue(a, parameters[i].ParameterType)).ToArray();
                    method.Invoke(target, converted);
                    return true;
                }
                catch { }
            }
            return false;
        }

        private static object InvokeResult(object target, string name, params object[] args)
        {
            if (target == null)
                return null;
            return InvokeMethodResult(target.GetType(), target, name, args);
        }

        private static object InvokeStaticResult(Type type, string name, params object[] args)
        {
            return type == null ? null : InvokeMethodResult(type, null, name, args);
        }

        private static object InvokeMethodResult(Type type, object target, string name, object[] args)
        {
            args = args ?? new object[0];
            foreach (MethodInfo method in type.GetMethods(All).Where(m => m.Name == name && m.IsStatic == (target == null) && m.GetParameters().Length == args.Length))
            {
                try
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    object[] converted = args.Select((a, i) => ConvertValue(a, parameters[i].ParameterType)).ToArray();
                    return method.Invoke(target, converted);
                }
                catch { }
            }
            return null;
        }

        private static bool HasStaticSignature(Type type, string name, params Type[] parameterTypes)
        {
            if (type == null)
                return false;
            parameterTypes = parameterTypes ?? new Type[0];
            return type.GetMethods(All).Any(method =>
            {
                try
                {
                    if (method == null || !method.IsStatic || method.Name != name)
                        return false;
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters == null || parameters.Length != parameterTypes.Length)
                        return false;
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i] == null || parameters[i].ParameterType == null)
                            return false;
                        if (parameterTypes[i] == null)
                        {
                            if (!parameters[i].ParameterType.IsArray)
                                return false;
                        }
                        else if (parameters[i].ParameterType != parameterTypes[i])
                            return false;
                    }
                    return true;
                }
                catch { return false; }
            });
        }

        private static IEnumerable<object> Values(object value)
        {
            if (value == null)
                yield break;
            object values = Get(value, "Values");
            if (values != null && !ReferenceEquals(values, value))
            {
                foreach (object item in Values(values))
                    yield return item;
                yield break;
            }
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                foreach (object item in enumerable)
                    yield return item;
                yield break;
            }
            MethodInfo getEnumerator = value.GetType().GetMethod("GetEnumerator", All, null, Type.EmptyTypes, null);
            if (getEnumerator == null)
                yield break;
            object enumerator = getEnumerator.Invoke(value, null);
            MethodInfo moveNext = enumerator.GetType().GetMethod("MoveNext", All);
            while (moveNext != null && (bool)moveNext.Invoke(enumerator, null))
                yield return Get(enumerator, "Current");
        }

        private static object ConvertValue(object value, Type type)
        {
            if (value == null || type.IsInstanceOfType(value))
                return value;
            Type underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (underlying.IsEnum)
                return Enum.ToObject(underlying, value);
            return Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
        }

        private static int ToInt(object value, int fallback)
        {
            try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        private static long ToLong(object value, long fallback)
        {
            try { return Convert.ToInt64(value, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        private static bool ToBool(object value)
        {
            try { return Convert.ToBoolean(value, CultureInfo.InvariantCulture); }
            catch { return false; }
        }

        private static string Text(object value) { return value == null ? null : value.ToString(); }
        private static bool Same(string a, string b) { return string.Equals(a == null ? null : a.Trim(), b == null ? null : b.Trim(), StringComparison.OrdinalIgnoreCase); }
    }

    public sealed class ModConfig
    {
        public bool AutoApply = true;
        public double PollSeconds = 1.0;
        public bool DumpCatalog = true;
        public bool ForceProbabilitySuccess = false;
        public ActionPointRule ActionPoints = new ActionPointRule();
        public List<PersonRule> People = new List<PersonRule>();
        public List<PartyRule> Parties = new List<PartyRule>();
        public List<NationRule> Nations = new List<NationRule>();

        public void Normalize()
        {
            ActionPoints = ActionPoints ?? new ActionPointRule();
            People = People ?? new List<PersonRule>();
            Parties = Parties ?? new List<PartyRule>();
            Nations = Nations ?? new List<NationRule>();
            People.RemoveAll(rule => rule == null || (!rule.Id.HasValue && !string.IsNullOrWhiteSpace(rule.Name) && rule.Name.StartsWith("EDIT_", StringComparison.OrdinalIgnoreCase)));
            Parties.RemoveAll(rule => rule == null || (!rule.Id.HasValue && !string.IsNullOrWhiteSpace(rule.Name) && rule.Name.StartsWith("EDIT_", StringComparison.OrdinalIgnoreCase)));
            Nations.RemoveAll(rule => rule == null || (!rule.Id.HasValue && !string.IsNullOrWhiteSpace(rule.Name) && rule.Name.StartsWith("EDIT_", StringComparison.OrdinalIgnoreCase)));
            foreach (PersonRule rule in People) rule.Normalize();
            foreach (PartyRule rule in Parties) if (rule.Members != null) rule.Members.Normalize();
        }

        public static ModConfig CreateDefault()
        {
            return new ModConfig
            {
                ActionPoints = new ActionPointRule(),
                People = new List<PersonRule>
                {
                    new PersonRule
                    {
                        Name = "EDIT_PERSON_NAME",
                        Changes = new PersonChanges
                        {
                            Wealth = 1000000,
                            Loyalty = 100,
                            Attributes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                            {
                                { "energy", 100 }, { "experience", 100 }, { "recognition", 100 },
                                { "popularity", 100 }, { "charm", 100 }, { "eloquence", 100 },
                                { "cunning", 100 }, { "influence", 100 }
                            }
                        }
                    }
                }
            };
        }
    }

    public sealed class ActionPointRule { public int? Value; public int? Max; }

    public sealed class PersonRule
    {
        public int? Id;
        public string Name;
        public int? PartyId;
        public string Party;
        public PersonChanges Changes = new PersonChanges();
        public void Normalize() { Changes = Changes ?? new PersonChanges(); Changes.Normalize(); }
    }

    public sealed class PersonChanges
    {
        public long? Wealth;
        public int? Loyalty;
        public int? PartyId;
        public string Party;
        public Dictionary<string, int> Attributes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public void Normalize() { Attributes = Attributes ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); }
    }

    public sealed class PartyRule
    {
        public int? Id;
        public string Name;
        public long? Money;
        public PersonChanges Members;
    }

    public sealed class NationRule
    {
        public int? Id;
        public string Name;
        public int? Armies;
        public int? ArmyUnits;
        public int? Missiles;
    }
}
