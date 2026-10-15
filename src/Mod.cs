using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

[assembly: MelonInfo(typeof(ScheduleI24Seven.Mod), "Schedule I 24/7", "0.1.0-alpha", "AnasaAyatan")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace ScheduleI24Seven;

/// <summary>
/// Compatibility-first prototype. Runtime lookups avoid the removed Employee.GetBed API.
/// Not yet validated against Schedule I 0.4.7f12; inspect logs and test on a backup save.
/// </summary>
public sealed class Mod : MelonMod
{
    internal static MelonPreferences_Entry<bool> Employees = null!;
    internal static MelonPreferences_Entry<bool> Plants = null!;
    internal static MelonPreferences_Entry<bool> Diagnostics = null!;
    internal static MelonPreferences_Entry<float> SecondsPerPlantMinute = null!;

    private Harmony? _harmony;
    private static Type? _employeeType;
    private static Type? _plantType;
    private static Type? _timeType;
    private static bool _warnedTime;
    private static float _accumulator;
    private static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
    private static double _lastUpdateSeconds;
    private static MethodInfo? _minPass;

    public override void OnInitializeMelon()
    {
        var cat = MelonPreferences.CreateCategory("ScheduleI24Seven");
        Employees = cat.CreateEntry("EmployeesAtEndOfDay", true, "Permit employees to work at end-of-day");
        Plants = cat.CreateEntry("PlantGrowthAtEndOfDay", false, "Experimental: simulate plant minutes at frozen 4 AM");
        Diagnostics = cat.CreateEntry("Diagnostics", true, "Log detected game API and patch status");
        SecondsPerPlantMinute = cat.CreateEntry("SecondsPerPlantMinute", 60f,
            "Real seconds per simulated plant-growth minute during end-of-day (experimental)");

        _employeeType = AccessTools.TypeByName("Il2CppScheduleOne.Employees.Employee");
        _plantType = AccessTools.TypeByName("Il2CppScheduleOne.Growing.Plant")
                     ?? AccessTools.TypeByName("Il2CppScheduleOne.ObjectScripts.Plant");
        _timeType = AccessTools.TypeByName("Il2CppScheduleOne.GameTime.TimeManager");
        _harmony = new Harmony("com.anasaayatan.schedulei24seven");

        Patch(_employeeType, "CanWork", nameof(WorkPatch.Prefix));
        // A postfix on UpdateBehaviour is useful for diagnostics but DOES NOT alter pay.
        Inspect(_employeeType, "UpdateBehaviour");
        Inspect(_plantType, "MinPass");
        Inspect(_timeType, "IsEndOfDay");
        MelonLogger.Msg("24/7 alpha initialized. Wage modification not implemented; plant ticks disabled by default.");
    }

    private void Patch(Type? type, string name, string patchName)
    {
        if (type == null) { MelonLogger.Warning($"Missing class for {name}; skipping."); return; }
        var target = AccessTools.Method(type, name);
        if (target == null) { MelonLogger.Warning($"Missing method {type.FullName}.{name}; skipping."); return; }
        try
        {
            _harmony!.Patch(target, prefix: new HarmonyMethod(typeof(WorkPatch), patchName));
            MelonLogger.Msg($"Patched {type.FullName}.{name}");
        }
        catch (Exception ex) { MelonLogger.Error($"Could not patch {name}: {ex}"); }
    }

    private static void Inspect(Type? type, string method)
    {
        if (!Diagnostics.Value || type == null) return;
        var found = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                        .Where(x => x.Name.Contains(method, StringComparison.OrdinalIgnoreCase))
                        .Take(10);
        foreach (var x in found) MelonLogger.Msg($"API: {x}");
    }

    internal static bool AtEndOfDay()
    {
        if (_timeType == null) return false;
        try
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            object? manager = _timeType.GetProperty("Instance", flags)?.GetValue(null)
                             ?? _timeType.GetField("Instance", flags)?.GetValue(null);
            foreach (var name in new[] { "IsEndOfDay", "isEndOfDay", "EndOfDay" })
            {
                var prop = _timeType.GetProperty(name, flags);
                if (prop?.PropertyType == typeof(bool)) return (bool)prop.GetValue(prop.GetMethod?.IsStatic == true ? null : manager)!;
                var field = _timeType.GetField(name, flags);
                if (field?.FieldType == typeof(bool)) return (bool)field.GetValue(field.IsStatic ? null : manager)!;
            }
        }
        catch (Exception ex)
        {
            if (!_warnedTime) { MelonLogger.Warning($"End-of-day lookup failed: {ex.Message}"); _warnedTime = true; }
        }
        return false; // fail closed instead of applying patches during normal hours
    }

    public override void OnUpdate()
    {
        if (!Plants.Value || !AtEndOfDay() || _plantType == null) { _accumulator = 0; return; }
        // An opt-in diagnostic tick: only uses a known MinPass(int) method. All other
        // signatures fail closed until checked against generated game assemblies.
        _minPass ??= _plantType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                              .FirstOrDefault(m => m.Name == "MinPass" && m.GetParameters().Length == 1 &&
                                                   m.GetParameters()[0].ParameterType == typeof(int));
        if (_minPass == null) return;
        double nowSeconds = Clock.Elapsed.TotalSeconds;
        if (_lastUpdateSeconds == 0) _lastUpdateSeconds = nowSeconds;
        _accumulator += (float)Math.Min(1.0, nowSeconds - _lastUpdateSeconds);
        _lastUpdateSeconds = nowSeconds;
        float seconds = Math.Max(1f, SecondsPerPlantMinute.Value);
        if (_accumulator < seconds) return;
        _accumulator -= seconds;
        // Managed Unity FindObjectsOfType cannot be assumed to enumerate IL2CPP objects
        // via System.Type. Avoid unsafe enumeration: plant ticking requires a verified
        // wrapper-specific implementation for the user's generated assemblies.
        if (Diagnostics.Value) MelonLogger.Warning("Plant tick due; enumerator not yet validated for 0.4.7f12.");
        Plants.Value = false;
    }

    public static class WorkPatch
    {
        // Using only __result keeps this compatible with changed Employee.GetBed signatures.
        public static bool Prefix(ref bool __result)
        {
            if (!Employees.Value || !AtEndOfDay()) return true;
            __result = true;
            return false;
        }
    }
}
