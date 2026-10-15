using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

[assembly: MelonInfo(typeof(ScheduleI24Seven.Mod), "Schedule I 24/7", "0.2.0-alpha", "AnasaAyatan")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace ScheduleI24Seven;

public sealed class Mod : MelonMod
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic |
                                     BindingFlags.Instance | BindingFlags.Static;
    private static readonly System.Diagnostics.Stopwatch Watch = System.Diagnostics.Stopwatch.StartNew();
    private static double _lastSeconds;
    private static double _minuteCarry;
    private static bool _reportedEnumeratorError;
    private static bool _reportedTimeError;
    private static bool _reportedMissingTime;
    private static bool _reportedMissingPlant;
    private static Type? _timeType;
    private static Type? _plantType;
    private static Type? _employeeType;
    private static MethodInfo? _plantMinutes;
    private static MethodInfo? _findPlants;
    private static Harmony? _harmony;

    private static MelonPreferences_Entry<bool> _employeeEnabled = null!;
    private static MelonPreferences_Entry<bool> _plantEnabled = null!;
    private static MelonPreferences_Entry<bool> _diagnostics = null!;
    private static MelonPreferences_Entry<float> _secondsPerMinute = null!;

    public override void OnInitializeMelon()
    {
        var prefs = MelonPreferences.CreateCategory("ScheduleI24Seven");
        _employeeEnabled = prefs.CreateEntry("EmployeesAtEndOfDay", true);
        _plantEnabled = prefs.CreateEntry("PlantGrowthAtEndOfDay", false,
            "Opt in after checking logs and backing up a save. Experimental IL2CPP enumeration.");
        _diagnostics = prefs.CreateEntry("Diagnostics", true);
        _secondsPerMinute = prefs.CreateEntry("SecondsPerPlantMinute", 60f);

        _employeeType = AccessTools.TypeByName("Il2CppScheduleOne.Employees.Employee");
        _plantType = AccessTools.TypeByName("Il2CppScheduleOne.Growing.Plant")
                  ?? AccessTools.TypeByName("Il2CppScheduleOne.ObjectScripts.Plant");
        _timeType = AccessTools.TypeByName("Il2CppScheduleOne.GameTime.TimeManager");
        _harmony = new Harmony("com.anasaayatan.schedulei24seven");

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

        if (_diagnostics.Value)
        {
            Dump(_timeType, "EndOfDay");
            Dump(_employeeType, "Wage");
            Dump(_employeeType, "Paid");
            Dump(_employeeType, "Bed");
            MelonLogger.Msg("Plant enumerator: " + (_findPlants?.ToString() ?? "not discovered"));
        }

        MelonLogger.Msg("Schedule I 24/7 v0.2.0-alpha loaded; wages NOT implemented. " +
                        "Plant ticking is experimental and off by default.");
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
        if (_timeType == null)
        {
            if (!_reportedMissingTime) { MelonLogger.Warning("TimeManager not found; 24/7 patches inactive."); _reportedMissingTime = true; }
            return false;
        }
        try
        {
            object? instance = _timeType.GetProperty("Instance", All)?.GetValue(null)
                            ?? _timeType.GetField("Instance", All)?.GetValue(null);
            foreach (string key in new[] { "IsEndOfDay", "isEndOfDay", "EndOfDay" })
            {
                var property = _timeType.GetProperty(key, All);
                if (property?.PropertyType == typeof(bool))
                    return (bool)(property.GetValue(property.GetMethod?.IsStatic == true ? null : instance) ?? false);
                var field = _timeType.GetField(key, All);
                if (field?.FieldType == typeof(bool))
                    return (bool)(field.GetValue(field.IsStatic ? null : instance) ?? false);
            }
        }
        catch (Exception e)
        {
            if (!_reportedTimeError) { MelonLogger.Warning("End-of-day check failed: " + e.Message); _reportedTimeError = true; }
        }
        return false;
    }

    public override void OnUpdate()
    {
        var current = Watch.Elapsed.TotalSeconds;
        var elapsed = _lastSeconds <= 0 ? 0 : Math.Clamp(current - _lastSeconds, 0, 1);
        _lastSeconds = current;
        if (!_plantEnabled.Value || !EndOfDay())
        {
            _minuteCarry = 0;
            return;
        }
        if (_findPlants == null || _plantMinutes == null)
        {
            if (!_reportedMissingPlant)
            {
                MelonLogger.Warning("Plant ticking unavailable: required game API was not found.");
                _reportedMissingPlant = true;
            }
            return;
        }

        _minuteCarry += elapsed;
        int minutes = (int)(_minuteCarry / Math.Max(1f, _secondsPerMinute.Value));
        if (minutes < 1) return;
        minutes = Math.Min(minutes, 5);
        _minuteCarry -= minutes * Math.Max(1f, _secondsPerMinute.Value);

        try
        {
            // Invocation uses IL2CPP-generated reflection wrappers. Test on a backup save.
            var plants = _findPlants.Invoke(null, Array.Empty<object>()) as IEnumerable;
            if (plants == null) throw new InvalidOperationException("Plant enumeration yielded no enumerable.");
            int count = 0;
            foreach (var plant in plants)
            {
                if (plant == null) continue;
                _plantMinutes.Invoke(plant, new object[] { minutes });
                count++;
            }
            if (_diagnostics.Value) MelonLogger.Msg($"Advanced {count} plants by {minutes} minute(s).");
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
