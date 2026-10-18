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
    private static double _nextAutomaticLaunderingCheck;
    private static bool _autoLaunderApiWarning;
    private static double _nextAutoStart = 0;
    private static int _nextBusinessIndex;
    private static MethodInfo? _nativeLaunderStart;
    private static MethodInfo? _nativeCashChange;
    private static PropertyInfo? _cashBalanceProperty;
    private static object? _moneyManagerInstance;
    private static bool _autoStartHalted;
    private static object? _pendingAutoBusiness;
    private static double _pendingAutoSince;
    private static int _pendingOperationCount = -1;
    private static float _pendingAmount;
    private static bool _previousTransactionOptIn;

    private static MelonPreferences_Entry<bool> _employeeEnabled = null!;
    private static MelonPreferences_Entry<bool> _plantEnabled = null!;
    private static MelonPreferences_Entry<bool> _botanistWatering = null!;
    private static MelonPreferences_Entry<bool> _protectWater = null!;
    private static MelonPreferences_Entry<bool> _diagnostics = null!;
    private static MelonPreferences_Entry<bool> _launderingAfter4AM = null!;
    // Configuration for the upcoming inventory-backed auto-launder engine.
    // Deliberately no transaction writes until inventory debit is independently verified.
    private static MelonPreferences_Entry<bool> _autoLaunder = null!;
    private static MelonPreferences_Entry<float> _autoLaunderBusinessCap = null!;
    private static MelonPreferences_Entry<bool> _allowAutoTransactions = null!;
    private static MelonPreferences_Entry<float> _secondsPerMinute = null!;
    private static MelonPreferences_Entry<float> _growthPerMinute = null!;
    private static MelonPreferences_Entry<float> _afterHoursBoost = null!;
    private static MelonPreferences_Entry<float> _realSecondsPerGameMinute = null!;

    public override void OnInitializeMelon()
    {
        // Mod Manager & Phone App supports multiple pref categories identified
        // by the mod name prefix, rendering each section under its own header.
        // Stable identifiers preserve entries across future updates.
        var workers = MelonPreferences.CreateCategory("Schedule I 24/7_01_Workers", "01 - Workers");
        var plants = MelonPreferences.CreateCategory("Schedule I 24/7_02_Plants", "02 - Plants");
        var laundering = MelonPreferences.CreateCategory("Schedule I 24/7_03_Laundering", "03 - Laundering");
        var advanced = MelonPreferences.CreateCategory("Schedule I 24/7_04_Advanced", "04 - Advanced");

        _employeeEnabled = workers.CreateEntry("EmployeesAtEndOfDay", true,
            "Workers active after 4 AM", "Workers continue regular tasks when the clock stops.");
        _botanistWatering = workers.CreateEntry("BotanistWateringAfter4AM", true,
            "Botanists keep watering", "Botanists continue watering assigned plants after 4 AM.");

        _plantEnabled = plants.CreateEntry("PlantGrowthAtEndOfDay", true,
            "Plants grow after 4 AM", "Plants keep growing after 4 AM.");
        _protectWater = plants.CreateEntry("ProtectWaterDuringFrozenGrowth", true,
            "Prevent moisture loss", "Prevents pots drying out during frozen time.");
        _secondsPerMinute = plants.CreateEntry("SecondsPerPlantMinute", 60f,
            "Plant update interval (seconds)", "Seconds between plant growth updates.");
        _afterHoursBoost = plants.CreateEntry("AfterHoursGrowthMultiplier", 1.05f,
            "Growth speed multiplier", "1 = normal; 2 = twice as fast.");
        _growthPerMinute = plants.CreateEntry("NormalizedGrowthPerMinute", 0.01f,
            "Backup growth rate", "Used only when native plant calculations are unavailable.");

        _launderingAfter4AM = laundering.CreateEntry("LaunderingProgressAfter4AM", true,
            "Continue operations after 4 AM", "Existing operations keep progressing.");
        _autoLaunder = laundering.CreateEntry("AutoLaunderFromInventory", true,
            "Check available capacity", "Check owned businesses for available capacity.");
        _autoLaunderBusinessCap = laundering.CreateEntry("AutoLaunderBusinessCap", 0f,
            "Maximum amount per business (0 = full)", "Limit each automatic job; 0 uses all available capacity.");
        _allowAutoTransactions = laundering.CreateEntry("EnableAutoLaunderTransactions", false,
            "Experimental automatic jobs", "Enable only with a backed-up game save.");

        _realSecondsPerGameMinute = advanced.CreateEntry("RealSecondsPerGameMinute", 1f,
            "Seconds per game minute", "1 = normal; 0.05 = very fast. Affects plants and laundering.");
        _diagnostics = advanced.CreateEntry("Diagnostics", false,
            "Troubleshooting logs", "Enable for troubleshooting, then turn off.");

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
        InitializeAutoLaunderTransactions();
        _employeeType = AccessTools.TypeByName("Il2CppScheduleOne.Employees.Employee");
        _plantType = AccessTools.TypeByName("Il2CppScheduleOne.Growing.Plant")
                  ?? AccessTools.TypeByName("Il2CppScheduleOne.ObjectScripts.Plant");
        _timeType = AccessTools.TypeByName("Il2CppScheduleOne.GameTime.TimeManager");
        _harmony = new HarmonyLib.Harmony("com.anasaayatan.schedulei24seven");
        InitializeBotanistPatch();
        InitializeManualLaunderingTrace();
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

    private static void InitializeAutoLaunderTransactions()
    {
        // Only use the game's public wrapper methods, not RPC internals.
        _nativeLaunderStart = _businessType?.GetMethod("StartLaunderingOperation",
            All, null, new[] { typeof(float), typeof(int) }, null);
        var moneyType = AccessTools.TypeByName("Il2CppScheduleOne.Money.MoneyManager");
        _nativeCashChange = moneyType?.GetMethod("ChangeCashBalance", All, null,
            new[] { typeof(float), typeof(bool), typeof(bool) }, null);
        _cashBalanceProperty = moneyType?.GetProperty("cashBalance", All);
        if (_nativeLaunderStart == null || _nativeCashChange == null || _cashBalanceProperty == null)
            MelonLogger.Warning("Auto laundering transactions unavailable: required native methods missing.");
    }

    private static object? ResolveMoneyManager()
    {
        // The singleton accessor is inherited by MoneyManager on this IL2CPP build.
        Type? type = _cashBalanceProperty?.DeclaringType;
        while (type != null)
        {
            var property = type.GetProperty("Instance", All | BindingFlags.DeclaredOnly);
            if (property?.GetGetMethod(true)?.IsStatic == true)
            {
                object? instance = property.GetValue(null);
                if (instance != null) return instance;
            }
            type = type.BaseType;
        }
        return null;
    }

    private static object? _observedLaunderingInterface;

    public static class NativeLaunderingInstanceTrace
    {
        public static void Before(object __instance)
        {
            if (__instance == null) return;
            _observedLaunderingInterface = __instance;
            if (_diagnostics.Value)
                MelonLogger.Msg("TDD native laundering interface captured from game initialization.");
        }
    }

    private static object? ResolveLaunderingInterface()
    {
        if (_observedLaunderingInterface != null) return _observedLaunderingInterface;
        var t = AccessTools.TypeByName("Il2CppScheduleOne.UI.LaunderingInterface");
        for (var cursor = t; cursor != null; cursor = cursor.BaseType)
        {
            var singleton = cursor.GetProperty("Instance", All | BindingFlags.DeclaredOnly);
            if (singleton?.GetGetMethod(true)?.IsStatic == true)
            {
                var instance = singleton.GetValue(null);
                if (instance != null) return instance;
            }
        }
        return null;
    }

    private static bool _nativeInterfaceDiagnosticReported;

    private static bool ValidateNativeLaunderingInterface(object business, out string reason)
    {
        reason = "Native laundering UI is not available.";
        try
        {
            var ui = ResolveLaunderingInterface();
            if (ui == null) return false;
            var uiType = ui.GetType();
            var init = uiType.GetMethods(All).FirstOrDefault(m =>
                m.Name == "Initialize" && m.GetParameters().Length == 1 &&
                m.GetParameters()[0].ParameterType.IsAssignableFrom(business.GetType()));
            var confirm = uiType.GetMethod("ConfirmAmount", All, null, Type.EmptyTypes, null);
            var selected = uiType.GetProperty("selectedAmountToLaunder", All);
            var selectedBusiness = uiType.GetProperty("Business", All);
            if (init == null || confirm == null || selected?.CanWrite != true ||
                selectedBusiness?.CanRead != true)
            {
                reason = "Native UI is missing required initialization, amount, confirmation, or business access.";
                return false;
            }
            object? currentBusiness = selectedBusiness.GetValue(ui);
            object? currentAmount = selected.GetValue(ui);
            if (_diagnostics.Value)
                MelonLogger.Msg("TDD native UI state: selectedBusiness=" +
                    (currentBusiness == null ? "null" : currentBusiness.GetType().FullName) +
                    ", selectedAmount=" + (currentAmount?.ToString() ?? "null") +
                    ", requestedBusinessType=" + business.GetType().FullName +
                    ", noStateChanged=true.");
            reason = "Native laundering UI supports business initialization and confirmation; no transaction submitted.";
            return true;
        }
        catch (Exception ex)
        {
            reason = "Native UI validation failed: " + ex.Message;
            return false;
        }
    }

    private static bool PrepareNativeLaunderingRequest(object business, float amount, out string reason)
    {
        reason = "Native laundering request is not eligible.";
        if (!float.IsFinite(amount) || amount < 1f)
        {
            reason = "Requested amount is invalid.";
            return false;
        }
        if (_pendingAutoBusiness != null || _autoStartHalted)
        {
            reason = "Existing request pending or session halted.";
            return false;
        }
        if (_businessIsServer?.GetValue(business) is not true)
        {
            reason = "Business is not authoritative.";
            return false;
        }
        if (_businessOperations == null ||
            EnumerateCommerceCollection(_businessOperations.GetValue(business)).Any())
        {
            reason = "Business already has a laundering operation.";
            return false;
        }
        var capacityProperty = _businessType?.GetProperty("LaunderCapacity", All);
        var limitProperty = _businessType?.GetProperty("appliedLaunderLimit", All);
        var totalProperty = _businessType?.GetProperty("currentLaunderTotal", All);
        if (capacityProperty == null || limitProperty == null || totalProperty == null)
        {
            reason = "Business capacity properties unavailable.";
            return false;
        }
        float capacity = Convert.ToSingle(capacityProperty.GetValue(business));
        float allowed = Convert.ToSingle(limitProperty.GetValue(business));
        float used = Convert.ToSingle(totalProperty.GetValue(business));
        float configuredCap = _autoLaunderBusinessCap.Value;
        if (!float.IsFinite(capacity) || !float.IsFinite(allowed) ||
            !float.IsFinite(used) || !float.IsFinite(configuredCap) ||
            configuredCap < 0)
        {
            reason = "Business capacity or configured cap is invalid.";
            return false;
        }
        float remaining = Math.Max(0, Math.Min(capacity, allowed - used));
        if (configuredCap > 0) remaining = Math.Min(remaining, configuredCap);
        if (amount > remaining)
        {
            reason = "Requested amount exceeds current business capacity or configured cap.";
            return false;
        }
        float cash = ReadCarriedCash();
        if (!float.IsFinite(cash) || cash < amount)
        {
            reason = "Insufficient or invalid cash balance.";
            return false;
        }
        if (!ValidateNativeLaunderingInterface(business, out reason))
            return false;
        reason = "Request passed native UI and game-state preflight; transaction not submitted.";
        return true;
    }

    private static bool _pilotOperationVerified;
    private static float _cashBeforeAutoRequest = float.NaN;

    private static void StartAutoLaundering(object business, float amount)
    {
        bool eligible;
        string reason;
        try
        {
            eligible = PrepareNativeLaunderingRequest(business, amount, out reason);
        }
        catch (Exception ex)
        {
            _autoStartHalted = true;
            MelonLogger.Error("Automatic job preflight failed; submissions halted: " + ex.Message);
            return;
        }
        if (_diagnostics.Value)
            MelonLogger.Msg("Auto laundering native request staging: eligible=" + eligible +
                ", amount=" + amount.ToString("F2") + ", reason=" + reason +
                ", stage=preflight.");
        if (!eligible) return;
        try
        {
            var ui = ResolveLaunderingInterface();
            if (ui == null) throw new InvalidOperationException("Game interface unavailable.");
            var uiType = ui.GetType();
            var initialize = uiType.GetMethods(All).First(m =>
                m.Name == "Initialize" && m.GetParameters().Length == 1 &&
                m.GetParameters()[0].ParameterType.IsAssignableFrom(business.GetType()));
            var selected = uiType.GetProperty("selectedAmountToLaunder", All)!;
            var confirm = uiType.GetMethod("ConfirmAmount", All, null, Type.EmptyTypes, null)!;
            float requestAmount = _pilotOperationVerified ? amount : Math.Min(amount, 100f);
            if (_businessOperations == null || _businessIsServer?.GetValue(business) is not true)
                throw new InvalidOperationException("Game business authority changed before submission.");
            _cashBeforeAutoRequest = ReadCarriedCash();
            _pendingAutoBusiness = business;
            _pendingAmount = requestAmount;
            _pendingOperationCount = EnumerateCommerceCollection(_businessOperations.GetValue(business)).Count();
            if (_pendingOperationCount != 0)
                throw new InvalidOperationException("Game business gained an operation before submission.");
            _pendingAutoSince = Watch.Elapsed.TotalSeconds;
            initialize.Invoke(ui, new object[] { business });
            selected.SetValue(ui, Convert.ChangeType(requestAmount, selected.PropertyType));
            float actualSelected = Convert.ToSingle(selected.GetValue(ui));
            if (!float.IsFinite(actualSelected) || Math.Abs(actualSelected - requestAmount) > 0.05f)
                throw new InvalidOperationException("Game UI did not accept the requested amount.");
            float liveCash = ReadCarriedCash();
            if (!float.IsFinite(liveCash) || liveCash < requestAmount ||
                Math.Abs(liveCash - _cashBeforeAutoRequest) > 0.05f)
                throw new InvalidOperationException("Game cash changed during UI preparation.");
            var selectedBusiness = uiType.GetProperty("Business", All);
            var actualBusiness = selectedBusiness?.GetValue(ui);
            if (actualBusiness == null || !actualBusiness.Equals(business))
                throw new InvalidOperationException("Game UI business selection did not match the requested business.");
            confirm.Invoke(ui, null);
            MelonLogger.Msg("Game auto request submitted using native confirmation: " +
                requestAmount.ToString("F2"));
        }
        catch (Exception ex)
        {
            _autoStartHalted = true;
            _pendingAutoBusiness = null;
            _pendingOperationCount = -1;
            _pendingAmount = 0f;
            _cashBeforeAutoRequest = float.NaN;
            MelonLogger.Error("Game auto request halted after uncertain submission: " + ex.Message);
        }
    }

    private static void EvaluateAutomaticLaundering()
    {
        double now = Watch.Elapsed.TotalSeconds;
        if (_pendingAutoBusiness != null)
        {
            try
            {
                int observedCount = _businessOperations == null ? -1 :
                    EnumerateCommerceCollection(_businessOperations.GetValue(_pendingAutoBusiness)).Count();
                float cashNow = ReadCarriedCash();
                bool cashMatched = float.IsFinite(_cashBeforeAutoRequest) && float.IsFinite(cashNow) &&
                    Math.Abs((_cashBeforeAutoRequest - cashNow) - _pendingAmount) <= 0.05f;
                bool confirmed = _pendingOperationCount >= 0 && observedCount > _pendingOperationCount && cashMatched;
                if (observedCount > _pendingOperationCount && !cashMatched)
                {
                    _autoStartHalted = true;
                    _pendingAutoBusiness = null;
                    _pendingOperationCount = -1;
                    MelonLogger.Error("Auto request halted: game operation appeared without matching cash change.");
                    return;
                }
                if (confirmed)
                {
                    MelonLogger.Msg("Auto laundering: new operation observed after paid request for $" +
                        _pendingAmount.ToString("F2") + ".");
                    _pilotOperationVerified = true;
                    _pendingAutoBusiness = null;
                    _pendingOperationCount = -1;
                }
                else if (now - _pendingAutoSince > 20)
                {
                    _autoStartHalted = true;
                    _pendingAutoBusiness = null;
                    _pendingOperationCount = -1;
                    MelonLogger.Error("Auto laundering halted: request not confirmed within 20 seconds.");
                }
                else return;
            }
            catch (Exception ex)
            {
                _autoStartHalted = true;
                _pendingAutoBusiness = null;
                _pendingOperationCount = -1;
                MelonLogger.Error("Auto laundering halted: confirmation could not be checked. " + ex.Message);
            }
        }
        // Require a deliberate OFF-to-ON transition after a failure. Even
        // if a live setting is toggled later, never restart within this session.
        if (!_allowAutoTransactions.Value)
        {
            _previousTransactionOptIn = false;
            // Keep the read-only planner running while paid transactions are off.
            // This lets development tests use the safe default OFF position.
        }
        else _previousTransactionOptIn = true;
        if (!_autoLaunder.Value || _ownedBusinesses == null || _businessOperations == null) return;
        if (now < _nextAutomaticLaunderingCheck) return;
        _nextAutomaticLaunderingCheck = now + 10;
        try
        {
            var businesses = EnumerateCommerceCollection(_ownedBusinesses.GetValue(null)).ToArray();
            if (businesses.Length == 0) return;
            int startIndex = _nextBusinessIndex % businesses.Length;
            var capacity = _businessType?.GetProperty("LaunderCapacity", All);
            var limit = _businessType?.GetProperty("appliedLaunderLimit", All);
            var total = _businessType?.GetProperty("currentLaunderTotal", All);
            if (capacity == null || limit == null || total == null)
            {
                if (!_autoLaunderApiWarning)
                {
                    _autoLaunderApiWarning = true;
                    MelonLogger.Warning("Automatic laundering: business capacity API unavailable; no transactions started.");
                }
                return;
            }
            float simulatedCash = ReadCarriedCash();
            if (!float.IsFinite(simulatedCash) || simulatedCash < 0)
            {
                if (_diagnostics.Value)
                    MelonLogger.Warning("Laundering planner unavailable: cash balance is invalid.");
                return;
            }
            if (_diagnostics.Value)
                MelonLogger.Msg("TDD laundering allocation preview: cash=" + simulatedCash.ToString("F2") +
                    ", businesses=" + businesses.Length + ", report=allocation-preview.");
            float plannedTotal = 0;
            int eligibleBusinessCount = 0;
            for (int businessOffset = 0; businessOffset < businesses.Length; businessOffset++)
            {
                int businessIndex = (startIndex + businessOffset) % businesses.Length;
                var business = businesses[businessIndex];
                bool authoritative = _businessIsServer?.GetValue(business) is true;
                int active = EnumerateCommerceCollection(_businessOperations.GetValue(business)).Count();
                float businessCapacity = Convert.ToSingle(capacity.GetValue(business));
                float allowed = Convert.ToSingle(limit.GetValue(business));
                float used = Convert.ToSingle(total.GetValue(business));
                if (!float.IsFinite(businessCapacity) || !float.IsFinite(allowed) || !float.IsFinite(used))
                    continue;
                float room = Math.Max(0, Math.Min(businessCapacity, allowed - used));
                if (!float.IsFinite(room)) continue;
                if (authoritative && active == 0)
                {
                    float selectedCap = _autoLaunderBusinessCap.Value;
                    if (!float.IsFinite(selectedCap) || selectedCap < 0)
                    {
                        MelonLogger.Warning("Laundering preview skipped: per-business cap is not a valid nonnegative amount.");
                        continue;
                    }
                    float cap = selectedCap > 0 ? selectedCap : room;
                    float planned = (float)Math.Floor(Math.Max(0, Math.Min(simulatedCash, Math.Min(room, cap))));
                    simulatedCash -= planned;
                    plannedTotal += planned;
                    if (planned > 0) eligibleBusinessCount++;
                    if (_diagnostics.Value)
                        MelonLogger.Msg("TDD laundering allocation preview: businessRoom=" + room.ToString("F2") +
                            ", plannedAmount=" + planned.ToString("F2") +
                            ", remainingCash=" + simulatedCash.ToString("F2") + ", submitted=false.");
                }
                if (_diagnostics.Value && !_nativeInterfaceDiagnosticReported && authoritative)
                {
                    _nativeInterfaceDiagnosticReported = true;
                    bool nativeReady = ValidateNativeLaunderingInterface(business, out string nativeReason);
                    MelonLogger.Msg("TDD native laundering preflight: ready=" + nativeReady +
                        ", reason=" + nativeReason + ", ownedBusiness=true, availableCapacity=" +
                        room.ToString("F2") + ", paymentsDisabled=true.");
                }
                if (_diagnostics.Value)
                    MelonLogger.Msg($"Auto laundering eligibility: server={authoritative}, active={active}, availableCapacity={room:F2}, enabled={_autoLaunder.Value}.");
                if (_diagnostics.Value)
                    MelonLogger.Msg($"Auto laundering gates: transactionsConfigured={_allowAutoTransactions.Value}, transactionsEffective={_allowAutoTransactions.Value && !_autoStartHalted}, halted={_autoStartHalted}, pending={_pendingAutoBusiness != null}, authoritative={authoritative}, active={active}, room={room:F2}, cooldownRemaining={Math.Max(0,_nextAutoStart-now):F1}.");
                if (!_allowAutoTransactions.Value || _autoStartHalted || _pendingAutoBusiness != null ||
                    !authoritative || active != 0 || room < 1 || now < _nextAutoStart)
                    continue;
                // One request per polling window. Stop all future requests on
                // uncertain debit/network failure rather than risking repeats.
                var manager = ResolveMoneyManager();
                if (manager == null || _cashBalanceProperty == null)
                {
                    if (_diagnostics.Value)
                        MelonLogger.Warning($"Auto laundering skipped: MoneyManager.Instance={(manager != null)}, cashBalanceProperty={_cashBalanceProperty != null}. No cash deducted.");
                    continue;
                }
                float cash = Convert.ToSingle(_cashBalanceProperty.GetValue(manager));
                if (!float.IsFinite(cash) || cash < 0) continue;
                float configuredCap = _autoLaunderBusinessCap.Value;
                if (!float.IsFinite(configuredCap) || configuredCap < 0) continue;
                float effectiveRoom = configuredCap > 0 ? Math.Min(room, configuredCap) : room;
                float amount = (float)Math.Floor(Math.Min(cash, effectiveRoom));
                if (amount < 1) continue;
                // The 10-second polling interval already throttles requests.
                // Pending operation verification prevents overlapping submissions.
                _nextAutoStart = now + 10;
                StartAutoLaundering(business, amount);
                _nextBusinessIndex = (businessIndex + 1) % businesses.Length;
                // Rotate the next scan so no owned business is permanently prioritized.
                break;
            }
            if (_diagnostics.Value)
                MelonLogger.Msg("TDD laundering plan summary: eligibleBusinesses=" +
                    eligibleBusinessCount + ", totalPlanned=" + plannedTotal.ToString("F2") +
                    ", remainingCash=" + simulatedCash.ToString("F2") +
                    ", perBusinessCap=" + _autoLaunderBusinessCap.Value.ToString("F2") +
                    ", submitted=false.");
        }
        catch (Exception e)
        {
            if (!_autoLaunderApiWarning)
            {
                _autoLaunderApiWarning = true;
                MelonLogger.Warning("Automatic laundering eligibility unavailable: " + e.Message);
            }
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
        if (!double.IsFinite(elapsed) || elapsed <= 0) return;
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
                        if (_diagnostics.Value)
                            MelonLogger.Msg("TDD after-4AM laundering completion: previousMinutes=" +
                                current + ", targetMinutes=" + next + ", completionThreshold=" + total +
                                ", authoritativeServer=true, nativeCompletionRequested=true.");
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

    public static class NativeLaunderingRequestTrace
    {
        public static void Before(float __0, int __1)
        {
            if (_diagnostics.Value)
                MelonLogger.Msg("TDD native laundering request observed: amount=" +
                    __0.ToString("F2") + ", secondArgument=" + __1 +
                    ", observationalOnly=true.");
        }
    }

    private static void InitializeManualLaunderingTrace()
    {
        var interfaceType = AccessTools.TypeByName("Il2CppScheduleOne.UI.LaunderingInterface");
        var confirm = interfaceType?.GetMethod("ConfirmAmount", All, null, Type.EmptyTypes, null);
        if (_nativeLaunderStart != null)
        {
            try
            {
                _harmony!.Patch(_nativeLaunderStart,
                    prefix: new HarmonyMethod(typeof(NativeLaunderingRequestTrace),
                        nameof(NativeLaunderingRequestTrace.Before)));
                MelonLogger.Msg("TDD native laundering request argument trace installed.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("TDD native laundering request argument trace unavailable: " + ex.Message);
            }
        }
        if (_diagnostics.Value && interfaceType != null)
        {
            foreach (var method in interfaceType.GetMethods(All | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && (m.Name.Contains("Confirm") || m.Name.Contains("Launder") || m.Name.Contains("Amount") || m.Name.Contains("Start"))))
                MelonLogger.Msg("Native laundering UI method: " + method);
        }
        var initialize = interfaceType?.GetMethods(All).FirstOrDefault(m =>
            m.Name == "Initialize" && m.GetParameters().Length == 1);
        if (initialize != null)
        {
            try
            {
                _harmony!.Patch(initialize,
                    prefix: new HarmonyMethod(typeof(NativeLaunderingInstanceTrace),
                        nameof(NativeLaunderingInstanceTrace.Before)));
                MelonLogger.Msg("TDD native laundering instance capture installed.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("TDD native laundering instance capture unavailable: " + ex.Message);
            }
        }
        if (confirm == null)
        {
            MelonLogger.Warning("Manual laundering trace unavailable: ConfirmAmount() not found.");
            return;
        }
        try
        {
            _harmony!.Patch(confirm,
                prefix: new HarmonyMethod(typeof(ManualLaunderingTrace), nameof(ManualLaunderingTrace.Before)),
                postfix: new HarmonyMethod(typeof(ManualLaunderingTrace), nameof(ManualLaunderingTrace.After)));
            MelonLogger.Msg("Read-only manual laundering confirmation tracing enabled.");
        }
        catch (Exception e)
        {
            MelonLogger.Warning("Manual laundering trace could not be installed: " + e.Message);
        }
    }

    private static int CountLaunderingOperations()
    {
        try
        {
            if (_ownedBusinesses == null || _businessOperations == null) return -1;
            int count = 0;
            foreach (var business in EnumerateCommerceCollection(_ownedBusinesses.GetValue(null)))
                count += EnumerateCommerceCollection(_businessOperations.GetValue(business)).Count();
            return count;
        }
        catch (Exception e)
        {
            MelonLogger.Warning("Could not count laundering operations: " + e.Message);
            return -1;
        }
    }

    private static void TraceNativeLaunderingSelection(object ui)
    {
        if (!_diagnostics.Value || ui == null) return;
        try
        {
            var t = ui.GetType();
            foreach (string key in new[] { "Business", "selectedAmountToLaunder", "MinLaunderAmount", "maxLaunderAmount" })
            {
                var property = t.GetProperty(key, All);
                object? value = property?.GetValue(ui);
                if (property == null)
                    MelonLogger.Msg("Native laundering selection " + key + ": unavailable");
                else
                    MelonLogger.Msg("Native laundering selection " + key + "=" + (value?.ToString() ?? "null"));
            }
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("Native laundering selection trace unavailable: " + ex.Message);
        }
    }

    private static float ReadCarriedCash()
    {
        try
        {
            var manager = ResolveMoneyManager();
            var balance = manager == null ? null : _cashBalanceProperty?.GetValue(manager);
            return balance == null ? float.NaN : Convert.ToSingle(balance);
        }
        catch (Exception e)
        {
            if (_diagnostics.Value) MelonLogger.Warning("Read-only cash trace unavailable: " + e.Message);
            return float.NaN;
        }
    }

    private static double _nextManualTraceSample = -1;
    private static int _manualTraceSampleStage;
    private static float _manualTraceStartingCash;

    private static void SampleManualTransaction()
    {
        if (_manualTraceSampleStage == 0 || Watch.Elapsed.TotalSeconds < _nextManualTraceSample) return;
        if (!_diagnostics.Value) { _manualTraceSampleStage = 0; return; }
        int stage = _manualTraceSampleStage;
        MelonLogger.Msg("Manual laundering observation +" + (stage == 1 ? 2 : 10) +
            "s: carriedCash=" + ReadCarriedCash() +
            ", originalCash=" + _manualTraceStartingCash +
            ", operationCount=" + CountLaunderingOperations());
        _manualTraceSampleStage = stage == 1 ? 2 : 0;
        _nextManualTraceSample = Watch.Elapsed.TotalSeconds + 8;
    }

    public static class ManualLaunderingTrace
    {
        public static void Before(object __instance, ref int __state)
        {
            TraceNativeLaunderingSelection(__instance);
            __state = CountLaunderingOperations();
            if (_diagnostics.Value)
            {
                _manualTraceStartingCash = ReadCarriedCash();
                MelonLogger.Msg("Manual laundering cash before confirmation=" + _manualTraceStartingCash);
            }
            if (_diagnostics.Value)
                MelonLogger.Msg("Manual laundering confirm: operations before=" + __state);
        }

        public static void After(int __state)
        {
            if (_diagnostics.Value)
                MelonLogger.Msg("Manual laundering confirm: operations immediately after=" +
                    CountLaunderingOperations() + ", before=" + __state +
                    " (network synchronization may follow later).");
            if (_diagnostics.Value)
            {
                MelonLogger.Msg("Manual laundering cash immediately after=" + ReadCarriedCash() + ", startingCash=" + _manualTraceStartingCash);
                _manualTraceSampleStage = 1;
                _nextManualTraceSample = Watch.Elapsed.TotalSeconds + 2;
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
            Workers = new
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
                EnableAutoLaunderTransactions = _allowAutoTransactions.Value,
                AutoLaunderBusinessCap = _autoLaunderBusinessCap.Value,
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
                if (category == "Workers" && root.TryGetProperty("Employees", out var oldGroup) &&
                    oldGroup.ValueKind == JsonValueKind.Object && oldGroup.TryGetProperty(key, out var oldValue))
                    return oldValue;
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
            _employeeEnabled.Value = ReadBool("Workers", "EmployeesAtEndOfDay", _employeeEnabled.Value);
            _botanistWatering.Value = ReadBool("Workers", "BotanistWateringAfter4AM", _botanistWatering.Value);
            _plantEnabled.Value = ReadBool("Plants", "PlantGrowthAtEndOfDay", _plantEnabled.Value);
            _protectWater.Value = ReadBool("Plants", "ProtectWaterDuringFrozenGrowth", _protectWater.Value);
            _autoLaunder.Value = ReadBool("Laundering", "AutoLaunderFromInventory", _autoLaunder.Value);
            _allowAutoTransactions.Value = ReadBool("Laundering", "EnableAutoLaunderTransactions", _allowAutoTransactions.Value);
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
            _autoLaunderBusinessCap.Value = Math.Max(0f, ReadFloat("Laundering", "AutoLaunderBusinessCap", _autoLaunderBusinessCap.Value));
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
        SampleManualTransaction();
        EvaluateAutomaticLaundering();
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
