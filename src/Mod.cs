using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

[assembly: MelonInfo(typeof(ScheduleI24Seven.Mod), "Schedule I 24/7", "0.4.1-alpha", "AnasaAyatan")]
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
    private static Type? _timeType;
    private static Type? _plantType;
    private static Type? _employeeType;
    private static MethodInfo? _plantMinutes;
    private static MethodInfo? _setGrowthProgress;
    private static PropertyInfo? _getGrowthProgress;
    private static PropertyInfo? _potMoisture;
    private static MethodInfo? _potMinuteMethod;
    private static bool _moisturePatchReady;
    private static MethodInfo? _findPlants;
    private static HarmonyLib.Harmony? _harmony;

    private static MelonPreferences_Entry<bool> _employeeEnabled = null!;
    private static MelonPreferences_Entry<bool> _plantEnabled = null!;
    private static MelonPreferences_Entry<bool> _protectWater = null!;
    private static MelonPreferences_Entry<bool> _diagnostics = null!;
    private static MelonPreferences_Entry<float> _secondsPerMinute = null!;
    private static MelonPreferences_Entry<float> _growthPerMinute = null!;

    public override void OnInitializeMelon()
    {
        var prefs = MelonPreferences.CreateCategory("ScheduleI24Seven");
        _employeeEnabled = prefs.CreateEntry("EmployeesAtEndOfDay", true);
        _plantEnabled = prefs.CreateEntry("PlantGrowthAtEndOfDay", false,
            "Opt in after checking logs and backing up a save. Experimental IL2CPP enumeration.");
        _diagnostics = prefs.CreateEntry("Diagnostics", true);
        _protectWater = prefs.CreateEntry("ProtectWaterDuringFrozenGrowth", true, "Blocks unverified water-consuming growth simulation.");
        _secondsPerMinute = prefs.CreateEntry("SecondsPerPlantMinute", 60f);
        _growthPerMinute = prefs.CreateEntry("NormalizedGrowthPerMinute", 0.01f,
            "Experimental fraction of growth per simulated minute, capped at 0.05.");

        _employeeType = AccessTools.TypeByName("Il2CppScheduleOne.Employees.Employee");
        _plantType = AccessTools.TypeByName("Il2CppScheduleOne.Growing.Plant")
                  ?? AccessTools.TypeByName("Il2CppScheduleOne.ObjectScripts.Plant");
        _timeType = AccessTools.TypeByName("Il2CppScheduleOne.GameTime.TimeManager");
        _harmony = new HarmonyLib.Harmony("com.anasaayatan.schedulei24seven");
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

        MelonLogger.Msg("Schedule I 24/7 v0.4.1-alpha loaded; game-managed wages unchanged.");
        MelonLogger.Msg($"Live preferences: employees={_employeeEnabled.Value}, plants={_plantEnabled.Value}, " +
                        $"secondsPerPlantMinute={_secondsPerMinute.Value}, diagnostics={_diagnostics.Value}; " +
                        $"plantTickMethod={_plantMinutes != null}, plantEnumerator={_findPlants != null}, protectWater={_protectWater.Value}, moisturePatch={_moisturePatchReady}, growthSetter={_setGrowthProgress != null}.");
        _previousPlantEnabled = _plantEnabled.Value;
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
        var elapsed = _lastSeconds <= 0 ? 0 : Math.Clamp(current - _lastSeconds, 0, 1);
        _lastSeconds = current;
        bool frozen = EndOfDay();
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
        _minuteCarry += elapsed;
        int minutes = (int)(_minuteCarry / Math.Max(1f, _secondsPerMinute.Value));
        if (minutes < 1) return;
        minutes = Math.Min(minutes, 5);
        _minuteCarry -= minutes * Math.Max(1f, _secondsPerMinute.Value);

        try
        {
            // Invocation uses IL2CPP-generated reflection wrappers. Test on a backup save.
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
                    var increment = Math.Clamp(_growthPerMinute.Value, 0f, 0.05f) * minutes;
                    if (increment <= 0) continue;
                    float target = Math.Min(1f, Math.Max(0f, currentProgress) + increment);
                    _setGrowthProgress!.Invoke(plant, new object[] { target });
                    if (_diagnostics.Value && count == 0)
                    {
                        var observed = _getGrowthProgress.GetValue(plant);
                        MelonLogger.Msg($"Plant growth check: before={currentProgress:F5}, requested={target:F5}, after={observed}.");
                    }
                }
                else _plantMinutes!.Invoke(plant, new object[] { minutes });
                count++;
            }
            if (_diagnostics.Value) MelonLogger.Msg($"Advanced {count} plants over {minutes} simulated minute(s); waterProtection={_protectWater.Value}.");
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
