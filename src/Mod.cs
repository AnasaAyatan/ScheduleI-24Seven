using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Text.Json;
using HarmonyLib;
using MelonLoader;

[assembly: MelonInfo(typeof(ScheduleI24Seven.Mod), "Schedule I 24/7", "0.5.1", "AnasaAyatan")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace ScheduleI24Seven;

public sealed class Mod : MelonMod
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic |
                                     BindingFlags.Instance | BindingFlags.Static;
    private static readonly System.Diagnostics.Stopwatch Watch = System.Diagnostics.Stopwatch.StartNew();
    private static double _lastSeconds;
    private static double _minuteCarry;
    private static double _nextStatusLog;
    private static bool _previousFrozen;
    private static bool _previousPlantEnabled;
    private static bool _reportedEnumeratorError;
    private static bool _cachedEndOfDay;
    private static double _lastEndOfDaySample = -1;
    private static bool _reportedMissingPlant;
    private static bool _nativeRateWarning;
    private static string _jsonPath = "";
    private static double _nextJsonCheck;
    private static DateTime _lastJsonWrite = DateTime.MinValue;
    private static bool _jsonErrorReported;
    private static string _lastPreferencesSnapshot = "";
    private static bool _invalidBoostWarning;
    private static Type? _timeType;
    private static Type? _plantType;
    private static Type? _employeeType;
    private static Type? _botanistType;
    private static MethodInfo? _getPotForWatering;
    private static MethodInfo? _assignWatering;
    private static PropertyInfo? _waterBehaviourProperty;
    private static MethodInfo? _anyWorkInProgress;
    private static PropertyInfo? _isServer;
    private static bool _botanistErrorLogged;
    private static double _nextBotanistTrace;
    private static MethodInfo? _plantMinutes;
    private static MethodInfo? _setGrowthProgress;
    private static PropertyInfo? _getGrowthProgress;
    private static PropertyInfo? _growthTime;
    private static PropertyInfo? _plantPot;
    private static MethodInfo? _getLightExposure;
    private static MethodInfo? _getTemperatureMultiplier;
    private static PropertyInfo? _getPotSpeed;
    private static PropertyInfo? _potMoisture;
    private static MethodInfo? _potMinuteMethod;
    private static bool _moisturePatchReady;
    private static MethodInfo? _findPlants;
    private static HarmonyLib.Harmony? _harmony;
    private static Type? _businessType;
    private static PropertyInfo? _ownedBusinesses, _businessOperations, _operationMinutes, _operationCompletion, _businessIsServer;
    private static MethodInfo? _completeOperation;
    private static double _launderingCarry;
    private static bool _launderingFailureReported;
    private static double _nextLaunderingTrace;

    private static MelonPreferences_Entry<bool> _employeeEnabled = null!;
    private static MelonPreferences_Entry<bool> _plantEnabled = null!;
    private static MelonPreferences_Entry<bool> _botanistWatering = null!;
    private static MelonPreferences_Entry<bool> _protectWater = null!;
    private static MelonPreferences_Entry<bool> _diagnostics = null!;
    private static MelonPreferences_Entry<bool> _launderingAfter4AM = null!;
    // Configuration for the upcoming inventory-backed auto-launder engine.
    // Deliberately no transaction writes until inventory debit is independently verified.
    private static MelonPreferences_Entry<bool> _autoLaunder = null!;
    private static MelonPreferences_Entry<float> _secondsPerMinute = null!;
    private static MelonPreferences_Entry<float> _growthPerMinute = null!;
    private static MelonPreferences_Entry<float> _afterHoursBoost = null!;
    private static MelonPreferences_Entry<float> _realSecondsPerGameMinute = null!;

    public override void OnInitializeMelon()
    {
        // Mod Manager & Phone App exposes the mod's original preference category.
        // Keep a single discoverable category so settings do not become a blank
        // legacy panel. Human-readable section prefixes group the controls.
        var prefs = MelonPreferences.CreateCategory("Schedule I 24/7_Settings", "Schedule I 24/7");
        _employeeEnabled = prefs.CreateEntry("EmployeesAtEndOfDay", true,
            "01 Staff | Keep workers active after 4 AM", "Workers continue regular tasks when the clock stops.");
        _botanistWatering = prefs.CreateEntry("BotanistWateringAfter4AM", true,
            "01 Staff | Keep botanists watering", "Botanists continue watering their assigned plants after 4 AM.");
        _plantEnabled = prefs.CreateEntry("PlantGrowthAtEndOfDay", true,
            "02 Plants | Keep plants growing", "Plants keep growing after 4 AM.");
        _protectWater = prefs.CreateEntry("ProtectWaterDuringFrozenGrowth", true,
            "02 Plants | Prevent moisture loss", "Prevents pots drying out during frozen time.");
        _launderingAfter4AM = prefs.CreateEntry("LaunderingProgressAfter4AM", true,
            "03 Laundering | Continue after 4 AM", "Existing laundering operations keep progressing.");
        _autoLaunder = prefs.CreateEntry("AutoLaunderFromInventory", true,
            "03 Laundering | Auto-start (coming soon)", "Preview only. Does not spend cash or start laundering jobs yet.");
        _realSecondsPerGameMinute = prefs.CreateEntry("RealSecondsPerGameMinute", 1f,
            "04 Advanced | Seconds per game minute", "1 = regular test speed; 0.05 = very fast. Affects plants and laundering.");
        _secondsPerMinute = prefs.CreateEntry("SecondsPerPlantMinute", 60f,
            "04 Advanced | Plant update interval", "Seconds between plant growth updates.");
        _afterHoursBoost = prefs.CreateEntry("AfterHoursGrowthMultiplier", 1.05f,
            "04 Advanced | Plant growth multiplier", "1 = normal; 2 = twice as fast.");
        _growthPerMinute = prefs.CreateEntry("NormalizedGrowthPerMinute", 0.01f,
            "04 Advanced | Backup growth rate", "Only used when native plant calculations are unavailable.");
        _diagnostics = prefs.CreateEntry("Diagnostics", false,
            "04 Advanced | Troubleshooting logs", "Enable while investigating bugs, then turn off.");

        _jsonPath = Path.Combine(Path.Combine(AppContext.BaseDirectory, "UserData"), "ScheduleI24Seven.json");
        InitializeJson();
        _lastPreferencesSnapshot = PreferencesJson();

        // Focused runtime API discovery for independent warehouse and laundering support.
        // No transaction or shop-state changes are made during discovery.
        if (_diagnostics.Value)
        {
            string[] typesToInspect =
            {
                "Il2CppScheduleOne.Property.Business",
                "Il2CppScheduleOne.Property.LaunderingOperation",
                "Il2CppScheduleOne.Property.Property",
                "Il2CppScheduleOne.Money.MoneyManager",
                "Il2CppScheduleOne.PlayerScripts.Player",
                "Il2CppScheduleOne.PlayerScripts.PlayerInventory",
                "Il2CppScheduleOne.ItemFramework.CashInstance",
                "Il2CppScheduleOne.ItemFramework.ItemSlot",
                "Il2CppScheduleOne.ItemFramework.Inventory",
                "Il2CppScheduleOne.Money.CashSlot",
                "Il2CppScheduleOne.UI.LaunderingInterface",
                "Il2CppScheduleOne.GameTime.TimeManager",
                "Il2CppScheduleOne.Economy.Shop",
                "Il2CppScheduleOne.Economy.Supplier",
                "Il2CppScheduleOne.UI.LaunderingInterface",
                "Il2CppScheduleOne.ObjectScripts.LaunderingStation",
                "Il2CppScheduleOne.Storage.StorageDoorAnimation"
            };
            foreach (string name in typesToInspect)
                DumpCommerceApi(name);
            // Inspect native transaction entry points without invoking or modifying them.
            DumpTransactionApi("Il2CppScheduleOne.UI.LaunderingInterface");
            DumpTransactionApi("Il2CppScheduleOne.Money.CashSlot");
            DumpTransactionApi("Il2CppScheduleOne.ItemFramework.CashInstance");
            DumpTransactionApi("Il2CppScheduleOne.PlayerScripts.PlayerInventory");
        }
        InitializeLaunderingProgress();
        _employeeType = AccessTools.TypeByName("Il2CppScheduleOne.Employees.Employee");
        _plantType = AccessTools.TypeByName("Il2CppScheduleOne.Growing.Plant")
                  ?? AccessTools.TypeByName("Il2CppScheduleOne.ObjectScripts.Plant");
        _timeType = AccessTools.TypeByName("Il2CppScheduleOne.GameTime.TimeManager");
        _harmony = new HarmonyLib.Harmony("com.anasaayatan.schedulei24seven");
        InitializeBotanistPatch();
        // Observe the live game instance rather than invoking a non-static getter on null.
        var getter = _timeType?.GetProperty("IsEndOfDay", All)?.GetGetMethod(true);
        if (getter != null && getter.ReturnType == typeof(bool))
        {
            try
            {
                _harmony.Patch(getter, postfix: new HarmonyMethod(typeof(TimePatch),
                    nameof(TimePatch.Postfix)));
                MelonLogger.Msg("Patched TimeManager.IsEndOfDay getter for live state observation.");
            }
            catch (Exception e) { MelonLogger.Error("TimeManager getter patch failed: " + e); }
        }
        else MelonLogger.Warning("End-of-day getter unavailable; employee patch will fail closed.");


        if (_employeeType != null)
        {
            var method = _employeeType.GetMethods(All).FirstOrDefault(m =>
                m.Name == "CanWork" && m.ReturnType == typeof(bool) && m.GetParameters().Length == 0);
            if (method != null)
            {
                try
                {
                    _harmony.Patch(method, prefix: new HarmonyMethod(typeof(EmployeePatch),
                        nameof(EmployeePatch.Prefix)));
                    MelonLogger.Msg("Patched Employee.CanWork() (end-of-day only).");
                }
                catch (Exception e) { MelonLogger.Error("Employee patch failed: " + e); }
            }
            else MelonLogger.Warning("Employee.CanWork() signature not found; no patch applied.");
        }
        else MelonLogger.Warning("Employee wrapper not found.");

        _plantMinutes = _plantType?.GetMethods(All).FirstOrDefault(m =>
            m.Name == "MinPass" && m.GetParameters().Length == 1
            && m.GetParameters()[0].ParameterType == typeof(int));
        _setGrowthProgress = _plantType?.GetMethod("SetNormalizedGrowthProgress", All, null, new[] { typeof(float) }, null);
        _getGrowthProgress = _plantType?.GetProperty("NormalizedGrowthProgress", All);
        _growthTime = _plantType?.GetProperty("GrowthTime", All);
        _plantPot = _plantType?.GetProperty("Pot", All);
        if (_plantMinutes == null)
            MelonLogger.Warning("Plant.MinPass(int) not found. Plant growth will be unavailable.");

        // Detect a generic FindObjectsOfType<T>() overload in the generated Unity wrapper.
        // Failure to find an appropriate enumerator leaves plant simulation disabled.
        var unityObject = AccessTools.TypeByName("Il2CppUnityEngine.Object")
                       ?? AccessTools.TypeByName("UnityEngine.Object");
        _findPlants = unityObject?.GetMethods(All)
            .FirstOrDefault(m => m.Name == "FindObjectsOfType"
                                 && m.IsGenericMethodDefinition
                                 && m.GetGenericArguments().Length == 1
                                 && m.GetParameters().Length == 0);
        if (_findPlants != null && _plantType != null)
        {
            try { _findPlants = _findPlants.MakeGenericMethod(_plantType); }
            catch (Exception e) { MelonLogger.Warning("Plant enumeration unavailable: " + e.Message); _findPlants = null; }
        }

        // Previous build patched an IL2CPP field accessor, which Harmony cannot hook.
        // Pot.OnMinPass is a real game method. Suppressing it in the 4 AM freeze
        // prevents its time-driven moisture drain while leaving normal bot
        // watering/RPC operations to the game. This also pauses other pot minute logic.
        var potTypeForPatch = AccessTools.TypeByName("Il2CppScheduleOne.ObjectScripts.Pot");
        _potMinuteMethod = potTypeForPatch?.GetMethod("OnMinPass", All, null, Type.EmptyTypes, null);
        if (_potMinuteMethod != null && _potMinuteMethod.DeclaringType != null)
        {
            try
            {
                _harmony.Patch(_potMinuteMethod, prefix: new HarmonyMethod(typeof(PotMinutePatch),
                    nameof(PotMinutePatch.Prefix)));
                _moisturePatchReady = true;
                MelonLogger.Msg("Patched Pot.OnMinPass() to suppress frozen-period minute updates.");
            }
            catch (Exception e) { MelonLogger.Error("Pot minute patch failed: " + e); }
        }
        else MelonLogger.Warning("Pot.OnMinPass() not found: cannot guarantee frozen-period water preservation.");

        var growContainer = AccessTools.TypeByName("Il2CppScheduleOne.Growing.GrowContainer");
        _getLightExposure = growContainer?.GetMethods(All).FirstOrDefault(m =>
            m.Name == "GetAverageLightExposure" && m.GetParameters().Length == 1);
        _getTemperatureMultiplier = growContainer?.GetMethod("GetTemperatureGrowthMultiplier", All, null, Type.EmptyTypes, null);
        _getPotSpeed = potTypeForPatch?.GetProperty("GrowSpeedMultiplier", All);
        if (_diagnostics.Value)
        {
            Dump(_timeType, "EndOfDay");
            Dump(_employeeType, "Wage");
            Dump(_employeeType, "Paid");
            Dump(_employeeType, "Bed");
            Dump(_plantType, "Water");
            Dump(_plantType, "Pot");
            Dump(_plantType, "Soil");
            Dump(_plantType, "Grow");
            Dump(_plantType, "Progress");
            Dump(_plantType, "MinPass");
            var potType = AccessTools.TypeByName("Il2CppScheduleOne.ObjectScripts.Pot");
            if (potType != null)
            {
                Dump(potType, "Water");
                Dump(potType, "Moist");
                Dump(potType, "Hydrat");
                Dump(potType, "Soil");
                Dump(potType, "Plant");
                Dump(potType, "Grow");
                Dump(potType, "MinPass");
            }
            MelonLogger.Msg("Plant enumerator: " + (_findPlants?.ToString() ?? "not discovered"));
        }

        MelonLogger.Msg("Schedule I 24/7 v0.5.1 loaded.");
        MelonLogger.Msg($"Live preferences: employees={_employeeEnabled.Value}, plants={_plantEnabled.Value}, " +
                        $"secondsPerPlantMinute={_secondsPerMinute.Value}, diagnostics={_diagnostics.Value}; " +
                        $"plantTickMethod={_plantMinutes != null}, plantEnumerator={_findPlants != null}, protectWater={_protectWater.Value}, moisturePatch={_moisturePatchReady}, growthSetter={_setGrowthProgress != null}, growthTime={_growthTime != null}, boost={_afterHoursBoost.Value}, secondsPerGameMinute={_realSecondsPerGameMinute.Value}.");
        _previousPlantEnabled = _plantEnabled.Value;
    }

    private static void InitializeBotanistPatch()
    {
        _botanistType = AccessTools.TypeByName("Il2CppScheduleOne.Employees.Botanist");
        if (_botanistType == null) { MelonLogger.Warning("Botanist wrapper missing; after-hours watering disabled."); return; }
        var update = _botanistType.GetMethod("UpdateBehaviour", All, null, Type.EmptyTypes, null);
        _getPotForWatering = _botanistType.GetMethods(All).FirstOrDefault(m =>
            m.Name == "GetPotForWatering" && m.GetParameters().Length == 1 &&
            m.GetParameters()[0].ParameterType == typeof(float));
        _waterBehaviourProperty = _botanistType.GetProperty("_waterPotBehaviour", All);
        _anyWorkInProgress = _botanistType.GetMethod("IsAnyWorkInProgress", All, null, Type.EmptyTypes, null);
        _isServer = _botanistType.GetProperty("IsServer", All) ??
                    _botanistType.BaseType?.GetProperty("IsServer", All);
        var behaviourType = _waterBehaviourProperty?.PropertyType;
        _assignWatering = behaviourType?.GetMethods(All).FirstOrDefault(m =>
            m.Name == "AssignAndEnable" && m.GetParameters().Length == 1 &&
            _getPotForWatering != null &&
            m.GetParameters()[0].ParameterType.IsAssignableFrom(_getPotForWatering.ReturnType));
        if (_diagnostics.Value)
        {
            DumpBotanistApi(_botanistType);
            var waterType = AccessTools.TypeByName("Il2CppScheduleOne.NPCs.Behaviour.WaterPotBehaviour");
            DumpBotanistApi(waterType);
            DumpBotanistApi(behaviourType);
        }
        if (update == null || _getPotForWatering == null || _waterBehaviourProperty == null ||
            _anyWorkInProgress == null || _isServer == null || _assignWatering == null)
        {
            MelonLogger.Warning($"Botanist watering API incomplete: update={update != null}, getPot={_getPotForWatering != null}, waterBehaviour={_waterBehaviourProperty != null}, activeWork={_anyWorkInProgress != null}, isServer={_isServer != null}, assign={_assignWatering != null}. No watering patch applied.");
            return;
        }
        try
        {
            _harmony!.Patch(update, postfix: new HarmonyMethod(typeof(BotanistPatch), nameof(BotanistPatch.Postfix)));
            MelonLogger.Msg("Botanist after-hours watering hook installed.");
        }
        catch (Exception e) { MelonLogger.Warning("Botanist patch failed: " + e.Message); }
    }

    private static void InitializeLaunderingProgress()
    {
        _businessType = AccessTools.TypeByName("Il2CppScheduleOne.Property.Business");
        var operationType = AccessTools.TypeByName("Il2CppScheduleOne.Property.LaunderingOperation");
        _ownedBusinesses = _businessType?.GetProperty("OwnedBusinesses", All);
        _businessOperations = _businessType?.GetProperty("LaunderingOperations", All);
        _businessIsServer = _businessType?.GetProperty("IsServer", All);
        _operationMinutes = operationType?.GetProperty("minutesSinceStarted", All);
        _operationCompletion = operationType?.GetProperty("completionTime_Minutes", All);
        _completeOperation = operationType == null ? null :
            _businessType?.GetMethod("CompleteOperation", All, null, new[] { operationType }, null);
        if (_ownedBusinesses == null || _businessOperations == null || _businessIsServer == null ||
            _operationMinutes?.CanWrite != true || _operationCompletion == null || _completeOperation == null)
        {
            MelonLogger.Warning("After-hours laundering API incomplete; progress unavailable.");
            _completeOperation = null;
        }
        else MelonLogger.Msg("After-hours laundering progress API ready.");
    }

    private static IEnumerable<object> EnumerateCommerceCollection(object? source)
    {
        if (source == null) yield break;
        if (source is IEnumerable sequence)
        {
            foreach (var obj in sequence) if (obj != null) yield return obj;
            yield break;
        }
        var t = source.GetType();
        var count = t.GetProperty("Count", All) ?? t.GetProperty("Length", All);
        var item = t.GetProperty("Item", All);
        if (count == null || item == null)
            throw new InvalidOperationException("Unexpected IL2CPP list wrapper: " + t);
        int n = Convert.ToInt32(count.GetValue(source));
        for (int i = 0; i < n; i++)
        {
            var obj = item.GetValue(source, new object[] { i });
            if (obj != null) yield return obj;
        }
    }

    private static void AdvanceLaundering(double elapsed, bool frozen)
    {
        if (!frozen || !_launderingAfter4AM.Value || _completeOperation == null)
        {
            _launderingCarry = 0;
            return;
        }
        double secondsPerMinute = _realSecondsPerGameMinute.Value;
        if (!double.IsFinite(secondsPerMinute) || secondsPerMinute <= 0) return;
        _launderingCarry += Math.Min(elapsed, 5d);
        int minutes = (int)Math.Min(60d, Math.Floor(_launderingCarry / secondsPerMinute));
        if (minutes <= 0) return;
        _launderingCarry -= minutes * secondsPerMinute;
        bool trace = _diagnostics.Value && Watch.Elapsed.TotalSeconds >= _nextLaunderingTrace;
        if (trace) _nextLaunderingTrace = Watch.Elapsed.TotalSeconds + 15;
        try
        {
            int inspected = 0, authoritative = 0, active = 0;
            foreach (var business in EnumerateCommerceCollection(_ownedBusinesses!.GetValue(null)))
            {
                inspected++;
                bool server = _businessIsServer!.GetValue(business) is true;
                if (server) authoritative++;
                var operations = EnumerateCommerceCollection(_businessOperations!.GetValue(business)).ToArray();
                if (trace)
                    MelonLogger.Msg($"Laundering trace: business={business.GetType().Name}, server={server}, operations={operations.Length}, tickMinutes={minutes}.");
                if (!server) continue;
                active += operations.Length;
                foreach (var operation in operations)
                {
                    int current = Convert.ToInt32(_operationMinutes!.GetValue(operation));
                    int total = Convert.ToInt32(_operationCompletion!.GetValue(operation));
                    if (trace)
                        MelonLogger.Msg($"Laundering trace: minutesSinceStarted={current}, completionTime_Minutes={total}.");
                    if (total <= 0 || current < 0) continue;
                    int next = (int)Math.Min(total, (long)current + minutes);
                    if (next != current) _operationMinutes.SetValue(operation, next);
                    if (next >= total)
                    {
                        if (_diagnostics.Value) MelonLogger.Msg("Laundering trace: invoking native completion.");
                        _completeOperation.Invoke(business, new[] { operation });
                    }
                }
            }
            if (trace)
                MelonLogger.Msg($"Laundering trace summary: owned={inspected}, authoritative={authoritative}, activeOnServer={active}, speedSecondsPerGameMinute={secondsPerMinute}.");
        }
        catch (Exception ex)
        {
            _completeOperation = null;
            if (!_launderingFailureReported)
            {
                _launderingFailureReported = true;
                MelonLogger.Error("After-hours laundering disabled for this session: " + ex);
            }
        }
    }

    private static void DumpTransactionApi(string name)
    {
        Type? t = AccessTools.TypeByName(name);
        if (t == null) { MelonLogger.Msg("Transaction API unavailable: " + name); return; }
        MelonLogger.Msg("Transaction API signatures: " + name);
        var members = t.GetMembers(All | BindingFlags.DeclaredOnly)
            .Where(m => !m.Name.StartsWith("Native", StringComparison.Ordinal)
                 && !m.Name.StartsWith("RpcReader", StringComparison.Ordinal)
                 && !m.Name.StartsWith("RpcWriter", StringComparison.Ordinal)
                 && !m.Name.StartsWith("RpcLogic", StringComparison.Ordinal)
                 && !m.Name.StartsWith("get_", StringComparison.Ordinal)
                 && !m.Name.StartsWith("set_", StringComparison.Ordinal))
            .OrderBy(m => m.Name).Take(100);
        foreach (var member in members)
            MelonLogger.Msg("Transaction API [" + t.Name + "] " + member.MemberType + ": " + member);
    }

    private static void DumpCommerceApi(string qualifiedName)
    {
        // Only logs relevant signatures when Diagnostics is enabled; never invokes them.
        Type? type = AccessTools.TypeByName(qualifiedName);
        if (type == null)
        {
            MelonLogger.Msg("Commerce API unavailable: " + qualifiedName);
            return;
        }
        string[] terms = { "launder", "operation", "capacity", "cash", "money", "open", "close", "time", "storage", "door", "start", "finish", "complete", "minute", "hour", "schedule", "balance", "business", "inventory" };
        MelonLogger.Msg("Commerce API signatures: " + qualifiedName);
        int emitted = 0;
        foreach (var member in type.GetMembers(All)
            .Where(m => terms.Any(term => m.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Where(m => !m.Name.StartsWith("Native", StringComparison.Ordinal))
            .OrderBy(m => m.Name))
        {
            if (emitted++ >= 65) break;
            MelonLogger.Msg("Commerce API [" + type.Name + "] " + member.MemberType + ": " + member);
        }
    }

    private static void DumpBotanistApi(Type? type)
    {
        if (type == null)
        {
            MelonLogger.Msg("Botanist API diagnostics: requested type unavailable.");
            return;
        }
        MelonLogger.Msg("Botanist API diagnostics for " + type.FullName);
        string[] terms = { "water", "work", "behav", "active", "pot", "assign", "enable", "task", "station" };
        Type? cursor = type;
        for (int depth = 0; cursor != null && depth < 3; cursor = cursor.BaseType, depth++)
        {
            int emitted = 0;
            foreach (var member in cursor.GetMembers(All | BindingFlags.DeclaredOnly)
                         .Where(m => terms.Any(t => m.Name.Contains(t, StringComparison.OrdinalIgnoreCase)))
                         .Where(m => !m.Name.StartsWith("Native", StringComparison.Ordinal))
                         .OrderBy(m => m.Name))
            {
                if (emitted++ >= 65) break;
                MelonLogger.Msg($"Botanist API [{cursor.Name}] {member.MemberType}: {member}");
            }
        }
    }

    public static class BotanistPatch
    {
        public static void Postfix(object __instance)
        {
            if (!_botanistWatering.Value || !_employeeEnabled.Value || !EndOfDay()) return;
            try
            {
                if (_isServer?.GetValue(__instance) is not true) return;
                // Use the game's own work-in-progress check.
                if (_anyWorkInProgress?.Invoke(__instance, Array.Empty<object>()) is not false) return;
                object? pot = _getPotForWatering?.Invoke(__instance, new object[] { 0.01f });
                if (pot == null) return;
                object? behaviour = _waterBehaviourProperty?.GetValue(__instance);
                if (behaviour == null) return;
                _assignWatering?.Invoke(behaviour, new[] { pot });
                if (_diagnostics.Value && Watch.Elapsed.TotalSeconds >= _nextBotanistTrace)
                {
                    _nextBotanistTrace = Watch.Elapsed.TotalSeconds + 30;
                    MelonLogger.Msg("After-hours botanist assigned a pot for real NPC watering.");
                }
            }
            catch (Exception e)
            {
                if (!_botanistErrorLogged)
                {
                    _botanistErrorLogged = true;
                    MelonLogger.Warning("Botanist after-hours watering fallback failed closed: " + e);
                }
            }
        }
    }

    private static string PreferencesJson()
    {
        // Human-readable categories. Keys remain unchanged for compatibility
        // with existing flat settings and MelonPreferences.
        var current = new
        {
            Employees = new
            {
                EmployeesAtEndOfDay = _employeeEnabled.Value,
                BotanistWateringAfter4AM = _botanistWatering.Value
            },
            Plants = new
            {
                PlantGrowthAtEndOfDay = _plantEnabled.Value,
                ProtectWaterDuringFrozenGrowth = _protectWater.Value,
                SecondsPerPlantMinute = _secondsPerMinute.Value,
                NormalizedGrowthPerMinute = _growthPerMinute.Value,
                AfterHoursGrowthMultiplier = _afterHoursBoost.Value
            },
            Laundering = new
            {
                AutoLaunderFromInventory = _autoLaunder.Value,
                LaunderingProgressAfter4AM = _launderingAfter4AM.Value
            },
            Simulation = new
            {
                RealSecondsPerGameMinute = _realSecondsPerGameMinute.Value
            },
            Advanced = new
            {
                Diagnostics = _diagnostics.Value
            }
        };
        return JsonSerializer.Serialize(current, new JsonSerializerOptions { WriteIndented = true });
    }

    private static void SynchronizeSettings()
    {
        // Phone Mod Manager edits MelonPreferences entries directly and writes
        // UserData/MelonPreferences.cfg. Mirror those changes into JSON so the
        // two editors don't disagree. File changes from the JSON editor are
        // imported when no newer live preference edit is pending.
        string snapshot = PreferencesJson();
        if (snapshot != _lastPreferencesSnapshot)
        {
            try
            {
                File.WriteAllText(_jsonPath, snapshot);
                _lastJsonWrite = File.GetLastWriteTimeUtc(_jsonPath);
                _lastPreferencesSnapshot = snapshot;
                MelonLogger.Msg("Saved phone preferences to JSON mirror.");
            }
            catch (Exception e) { MelonLogger.Warning("JSON mirror write failed: " + e.Message); }
            return;
        }
        ReloadJsonIfChanged();
        _lastPreferencesSnapshot = PreferencesJson();
    }

    private static void InitializeJson()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "UserData"));
            if (!File.Exists(_jsonPath))
            {
                File.WriteAllText(_jsonPath, PreferencesJson());
            }
            ReloadJsonIfChanged();
            MelonLogger.Msg("Editable phone JSON configuration: " + _jsonPath);
        }
        catch (Exception e) { MelonLogger.Warning("JSON settings unavailable; using MelonPreferences: " + e.Message); }
    }

    private static void ReloadJsonIfChanged()
    {
        try
        {
            if (!File.Exists(_jsonPath)) return;
            DateTime modified = File.GetLastWriteTimeUtc(_jsonPath);
            if (modified == _lastJsonWrite) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(_jsonPath));
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("JSON settings must be an object.");
            // Prefer a setting in its category; accept legacy top-level keys.
            JsonElement Setting(string category, string key)
            {
                if (root.TryGetProperty(category, out var group) &&
                    group.ValueKind == JsonValueKind.Object &&
                    group.TryGetProperty(key, out var nested))
                    return nested;
                return root.TryGetProperty(key, out var flat) ? flat : default;
            }
            bool ReadBool(string category, string key, bool existing)
            {
                var p = Setting(category, key);
                return p.ValueKind == JsonValueKind.True || p.ValueKind == JsonValueKind.False
                    ? p.GetBoolean() : existing;
            }
            float ReadFloat(string category, string key, float existing)
            {
                var p = Setting(category, key);
                return p.ValueKind == JsonValueKind.Number &&
                    p.TryGetSingle(out float value) && float.IsFinite(value) ? value : existing;
            }
            // Parse the full file before applying anything, so invalid JSON
            // cannot partially overwrite live preferences.
            _employeeEnabled.Value = ReadBool("Employees", "EmployeesAtEndOfDay", _employeeEnabled.Value);
            _botanistWatering.Value = ReadBool("Employees", "BotanistWateringAfter4AM", _botanistWatering.Value);
            _plantEnabled.Value = ReadBool("Plants", "PlantGrowthAtEndOfDay", _plantEnabled.Value);
            _protectWater.Value = ReadBool("Plants", "ProtectWaterDuringFrozenGrowth", _protectWater.Value);
            _autoLaunder.Value = ReadBool("Laundering", "AutoLaunderFromInventory", _autoLaunder.Value);
            _launderingAfter4AM.Value = ReadBool("Laundering", "LaunderingProgressAfter4AM", _launderingAfter4AM.Value);
            _diagnostics.Value = ReadBool("Advanced", "Diagnostics", _diagnostics.Value);
            // Keep valid user values unchanged. Ignore invalid negatives/zeros
            // where positive timing and multipliers are required.
            float positive(string category, string key, float existing) {
                float proposed = ReadFloat(category, key, existing);
                return proposed > 0f ? proposed : existing;
            }
            _secondsPerMinute.Value = positive("Plants", "SecondsPerPlantMinute", _secondsPerMinute.Value);
            _realSecondsPerGameMinute.Value = positive("Simulation", "RealSecondsPerGameMinute", _realSecondsPerGameMinute.Value);
            _growthPerMinute.Value = Math.Max(0f, ReadFloat("Plants", "NormalizedGrowthPerMinute", _growthPerMinute.Value));
            _afterHoursBoost.Value = positive("Plants", "AfterHoursGrowthMultiplier", _afterHoursBoost.Value);
            _lastJsonWrite = modified;
            _jsonErrorReported = false;
            MelonLogger.Msg("Loaded editable JSON settings from " + _jsonPath);
        }
        catch (Exception e)
        {
            if (!_jsonErrorReported)
            {
                _jsonErrorReported = true;
                MelonLogger.Warning("Could not reload JSON settings; keeping previous settings: " + e.Message);
            }
        }
    }

    private static void Dump(Type? type, string fragment)
    {
        if (type == null) return;
        foreach (var member in type.GetMembers(All).Where(m =>
                     m.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)).Take(12))
            MelonLogger.Msg("API [" + type.Name + "]: " + member);
    }

    private static bool EndOfDay()
    {
        // Fail closed if no recent game-side sample is available.
        return _cachedEndOfDay && _lastEndOfDaySample >= 0 &&
               Watch.Elapsed.TotalSeconds - _lastEndOfDaySample < 3.0;
    }

    public static class TimePatch
    {
        public static void Postfix(bool __result)
        {
            _cachedEndOfDay = __result;
            _lastEndOfDaySample = Watch.Elapsed.TotalSeconds;
        }
    }

    public override void OnUpdate()
    {
        var current = Watch.Elapsed.TotalSeconds;
        if (current >= _nextJsonCheck)
        {
            _nextJsonCheck = current + 2;
            SynchronizeSettings();
        }
        var elapsed = _lastSeconds <= 0 ? 0 : Math.Max(0, current - _lastSeconds);
        _lastSeconds = current;
        bool frozen = EndOfDay();
        AdvanceLaundering(elapsed, frozen);
        if (_diagnostics.Value)
        {
            if (frozen != _previousFrozen || _plantEnabled.Value != _previousPlantEnabled || current >= _nextStatusLog)
            {
                MelonLogger.Msg($"Growth status: plantsEnabled={_plantEnabled.Value}, endOfDay={frozen}, " +
                                $"observedEndOfDay={_cachedEndOfDay}, sampleAgeSeconds=" +
                                $"{(_lastEndOfDaySample < 0 ? -1 : current - _lastEndOfDaySample):F1}, " +
                                $"elapsedCarry={_minuteCarry:F1}, plantMethod={_plantMinutes != null}, " +
                                $"enumerator={_findPlants != null}.");
                _nextStatusLog = current + 30;
                _previousFrozen = frozen;
                _previousPlantEnabled = _plantEnabled.Value;
            }
        }
        if (!_plantEnabled.Value || !frozen)
        {
            _minuteCarry = 0;
            return;
        }
        if (_findPlants == null || (_protectWater.Value ? (!_moisturePatchReady || _setGrowthProgress == null || _getGrowthProgress == null) : _plantMinutes == null))
        {
            if (!_reportedMissingPlant)
            {
                MelonLogger.Warning("Plant ticking unavailable: required game API was not found.");
                _reportedMissingPlant = true;
            }
            return;
        }

        // Water-safe mode uses growth progress only; do not call MinPass.
        // It also requires the end-of-day pot moisture setter clamp.
        // _minuteCarry tracks REAL elapsed seconds, not game minutes.
        // The old code treated one 30-second interval as one game minute,
        // yielding approximately 30x slower growth than native game time.
        _minuteCarry += elapsed;
        float updateInterval = float.IsFinite(_secondsPerMinute.Value) && _secondsPerMinute.Value > 0f
            ? _secondsPerMinute.Value : 60f;
        if (_minuteCarry < updateInterval) return;
        double periodSeconds = _minuteCarry;
        _minuteCarry = 0;
        float secondsPerGameMinute = _realSecondsPerGameMinute.Value;
        if (!float.IsFinite(secondsPerGameMinute) || secondsPerGameMinute <= 0f)
            secondsPerGameMinute = 1f;
        double simulatedMinutesRaw = periodSeconds / secondsPerGameMinute;
        // A floating-point limit is not a gameplay speed cap: reject overflow
        // rather than passing infinity or a corrupt duration into the game.
        if (!double.IsFinite(simulatedMinutesRaw) || simulatedMinutesRaw <= 0) return;
        float simulatedMinutes = (float)Math.Min(simulatedMinutesRaw, float.MaxValue);

        try
        {
            // Invoke the game’s IL2CPP plant wrappers.
            var plants = _findPlants.Invoke(null, Array.Empty<object>());
            if (plants == null) throw new InvalidOperationException("Plant enumeration returned null.");
            int count = 0;
            foreach (var plant in EnumeratePlants(plants))
            {
                if (_protectWater.Value)
                {
                    var raw = _getGrowthProgress!.GetValue(plant);
                    if (raw is not float currentProgress || !float.IsFinite(currentProgress)
                        || currentProgress >= 1f) continue;
                    float increment = CalculatePlantGrowth(plant, simulatedMinutes);
                    if (increment <= 0) continue;
                    float target = (float)Math.Min(1d, Math.Max(0d, currentProgress) + increment);
                    _setGrowthProgress!.Invoke(plant, new object[] { target });
                    if (_diagnostics.Value && count == 0)
                    {
                        var observed = _getGrowthProgress.GetValue(plant);
                        MelonLogger.Msg($"Plant growth check: before={currentProgress:F5}, requested={target:F5}, after={observed}.");
                    }
                }
                else
                {
                    // MinPass accepts an int; clamp only to its representable
                    // range, not to an artificial number of game minutes.
                    int wholeMinutes = (int)Math.Min(int.MaxValue, Math.Max(0d, Math.Round(simulatedMinutes)));
                    if (wholeMinutes > 0) _plantMinutes!.Invoke(plant, new object[] { wholeMinutes });
                }
                count++;
            }
            if (_diagnostics.Value) MelonLogger.Msg($"Advanced {count} plants over {simulatedMinutes:F1} simulated minute(s); waterProtection={_protectWater.Value}.");
        }
        catch (Exception e)
        {
            if (!_reportedEnumeratorError)
            {
                MelonLogger.Error("Experimental plant update failed; disabling it for this session: " + e);
                _reportedEnumeratorError = true;
            }
            _plantEnabled.Value = false;
        }
    }

    private static float CalculatePlantGrowth(object plant, float minutes)
    {
        float boost = _afterHoursBoost.Value;
        if (!float.IsFinite(boost) || boost <= 0f)
        {
            if (!_invalidBoostWarning)
            {
                _invalidBoostWarning = true;
                MelonLogger.Warning("Invalid AfterHoursGrowthMultiplier; using 1.0.");
            }
            boost = 1f;
        }
        // The native calculation is based on the plant's own growth duration,
        // temperature, light exposure, and pot growth-speed multiplier.
        // If the game wrapper is missing a required member, preserve the
        // previously tested fixed rate instead of guessing a different one.
        try
        {
            if (_growthTime == null || _plantPot == null || _getLightExposure == null
                || _getTemperatureMultiplier == null || _getPotSpeed == null)
                throw new MissingMemberException("Native growth APIs unavailable.");
            float hours = Convert.ToSingle(_growthTime.GetValue(plant));
            if (!float.IsFinite(hours) || hours <= 0) throw new InvalidOperationException("Invalid growth duration.");
            var pot = _plantPot.GetValue(plant);
            if (pot == null) throw new InvalidOperationException("Missing plant pot.");
            object[] args = { 0f };
            float light = Convert.ToSingle(_getLightExposure.Invoke(pot, args));
            float lightFactor = Convert.ToSingle(args[0]);
            float temperature = Convert.ToSingle(_getTemperatureMultiplier.Invoke(pot, Array.Empty<object>()));
            float potSpeed = Convert.ToSingle(_getPotSpeed.GetValue(pot));
            double result = ((double)minutes / ((double)hours * 60d)) * light * lightFactor * temperature * potSpeed * boost;
            if (double.IsNaN(result)) throw new InvalidOperationException("Invalid native growth increment.");
            return (float)Math.Min(1d, Math.Max(0d, result));
        }
        catch (Exception e)
        {
            if (!_nativeRateWarning)
            {
                _nativeRateWarning = true;
                MelonLogger.Warning("Native plant growth rate unavailable; using previous fixed-rate fallback: " + e.Message);
            }
            // Preserve legacy fallback pacing: one fixed increment per configured
            // update interval, not one per converted game minute.
            float secondsPerGameMinute = _realSecondsPerGameMinute.Value;
            if (!float.IsFinite(secondsPerGameMinute) || secondsPerGameMinute <= 0f)
                secondsPerGameMinute = 1f;
            double interval = float.IsFinite(_secondsPerMinute.Value) && _secondsPerMinute.Value > 0f
                ? _secondsPerMinute.Value : 60d;
            double updateIntervals = (double)minutes * secondsPerGameMinute / interval;
            double fallback = float.IsFinite(_growthPerMinute.Value) && _growthPerMinute.Value > 0f
                ? _growthPerMinute.Value : 0d;
            double amount = fallback * updateIntervals * boost;
            if (double.IsNaN(amount)) return 0f;
            return (float)Math.Min(1d, Math.Max(0d, amount));
        }
    }

    private static IEnumerable<object> EnumeratePlants(object source)
    {
        // Il2CppArrayBase<T> is not guaranteed to implement managed IEnumerable.
        if (source is IEnumerable managed)
        {
            foreach (var item in managed) if (item != null) yield return item;
            yield break;
        }
        var type = source.GetType();
        var lengthProperty = type.GetProperty("Length", All);
        var itemProperty = type.GetProperty("Item", All);
        if (lengthProperty == null || itemProperty == null)
            throw new NotSupportedException("IL2CPP plant array cannot be indexed: " + type);
        int length = Convert.ToInt32(lengthProperty.GetValue(source));
        for (int i = 0; i < length; i++)
        {
            var item = itemProperty.GetValue(source, new object[] { i });
            if (item != null) yield return item;
        }
    }

    public static class PotMinutePatch
    {
        public static bool Prefix()
        {
            // Do not suppress pot ticking during normal hours.
            return !_protectWater.Value || !EndOfDay();
        }
    }

    public static class EmployeePatch
    {
        public static bool Prefix(ref bool __result)
        {
            if (!_employeeEnabled.Value || !EndOfDay()) return true;
            // This bypasses the game's CanWork checks, including non-time eligibility.
            // Do not treat this as validated unpaid/bed eligibility behavior.
            __result = true;
            return false;
        }
    }
}
