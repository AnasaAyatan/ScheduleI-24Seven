using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Text.Json;
using HarmonyLib;
using MelonLoader;

[assembly: MelonInfo(typeof(ScheduleI24Seven.Mod), "Schedule I 24/7", "0.4.8-alpha", "AnasaAyatan")]
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
    private static bool _highBoostWarning;
    private static Type? _timeType;
    private static Type? _plantType;
    private static Type? _employeeType;
    private static Type? _botanistType;
    private static MethodInfo? _getPotForWatering;
    private static MethodInfo? _assignWatering;
    private static FieldInfo? _waterBehaviourField;
    private static FieldInfo? _workBehavioursField;
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

    private static MelonPreferences_Entry<bool> _employeeEnabled = null!;
    private static MelonPreferences_Entry<bool> _plantEnabled = null!;
    private static MelonPreferences_Entry<bool> _botanistWatering = null!;
    private static MelonPreferences_Entry<bool> _protectWater = null!;
    private static MelonPreferences_Entry<bool> _diagnostics = null!;
    private static MelonPreferences_Entry<float> _secondsPerMinute = null!;
    private static MelonPreferences_Entry<float> _growthPerMinute = null!;
    private static MelonPreferences_Entry<float> _afterHoursBoost = null!;
    private static MelonPreferences_Entry<float> _realSecondsPerGameMinute = null!;

    public override void OnInitializeMelon()
    {
        var prefs = MelonPreferences.CreateCategory("Schedule I 24/7_Settings", "Schedule I 24/7");
        _employeeEnabled = prefs.CreateEntry("EmployeesAtEndOfDay", true, "Employees work after 4 AM");
        _plantEnabled = prefs.CreateEntry("PlantGrowthAtEndOfDay", false,
            "Plants grow after 4 AM", "Opt in after backing up a save. Experimental IL2CPP enumeration.");
        _botanistWatering = prefs.CreateEntry("BotanistWateringAfter4AM", false,
            "Continue botanist watering after 4 AM", "Experimental: idle, server-authoritative botanists resume assigned pot watering.");
        _diagnostics = prefs.CreateEntry("Diagnostics", true, "Detailed diagnostic logs");
        _protectWater = prefs.CreateEntry("ProtectWaterDuringFrozenGrowth", true, "Protect pot moisture after 4 AM", "Suppresses pot minute updates during frozen time.");
        _secondsPerMinute = prefs.CreateEntry("SecondsPerPlantMinute", 60f, "Growth update interval (seconds)");
        _realSecondsPerGameMinute = prefs.CreateEntry("RealSecondsPerGameMinute", 1f,
            "Real seconds per game minute", "Time conversion for frozen growth. Default 1.");
        _afterHoursBoost = prefs.CreateEntry("AfterHoursGrowthMultiplier", 1.05f,
            "After-hours growth multiplier", "Default 1.05 (5 percent boost); any positive finite multiplier allowed.");
        _growthPerMinute = prefs.CreateEntry("NormalizedGrowthPerMinute", 0.01f,
            "Fallback growth per interval", "Used only if native growth APIs fail; capped at 0.05.");

        _jsonPath = Path.Combine(Path.Combine(AppContext.BaseDirectory, "UserData"), "ScheduleI24Seven.json");
        InitializeJson();
        _lastPreferencesSnapshot = PreferencesJson();

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

        MelonLogger.Msg("Schedule I 24/7 v0.4.8-alpha loaded; game-managed wages unchanged.");
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
        _waterBehaviourField = _botanistType.GetField("_waterPotBehaviour", All);
        _workBehavioursField = _botanistType.GetField("_workBehaviours", All);
        _isServer = _botanistType.GetProperty("IsServer", All) ??
                    _botanistType.BaseType?.GetProperty("IsServer", All);
        var behaviourType = AccessTools.TypeByName("Il2CppScheduleOne.Employees.Behaviours.GrowContainerBehaviour");
        _assignWatering = behaviourType?.GetMethods(All).FirstOrDefault(m =>
            m.Name == "AssignAndEnable" && m.GetParameters().Length == 1);
        if (update == null || _getPotForWatering == null || _waterBehaviourField == null ||
            _workBehavioursField == null || _isServer == null || _assignWatering == null)
        {
            MelonLogger.Warning($"Botanist watering API incomplete: update={update != null}, getPot={_getPotForWatering != null}, waterBehaviour={_waterBehaviourField != null}, activeWork={_workBehavioursField != null}, isServer={_isServer != null}, assign={_assignWatering != null}. No watering patch applied.");
            return;
        }
        try
        {
            _harmony!.Patch(update, postfix: new HarmonyMethod(typeof(BotanistPatch), nameof(BotanistPatch.Postfix)));
            MelonLogger.Msg("Experimental Botanist.UpdateBehaviour() watering fallback installed (disabled until enabled in settings).");
        }
        catch (Exception e) { MelonLogger.Warning("Botanist patch failed: " + e.Message); }
    }

    public static class BotanistPatch
    {
        public static void Postfix(object __instance)
        {
            if (!_botanistWatering.Value || !_employeeEnabled.Value || !EndOfDay()) return;
            try
            {
                if (_isServer?.GetValue(__instance) is not true) return;
                // Never interrupt another job. If the IL2CPP list cannot be
                // inspected, fail closed rather than risking job reassignment.
                object? work = _workBehavioursField?.GetValue(__instance);
                if (work == null) return;
                bool observedAny = false;
                foreach (var b in EnumeratePlants(work))
                {
                    observedAny = true;
                    var active = b.GetType().GetProperty("Active", All);
                    if (active == null) return;
                    if (active.GetValue(b) is true) return;
                }
                if (!observedAny) return;
                object? pot = _getPotForWatering?.Invoke(__instance, new object[] { 0.01f });
                if (pot == null) return;
                object? behaviour = _waterBehaviourField?.GetValue(__instance);
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
        var current = new
        {
            EmployeesAtEndOfDay = _employeeEnabled.Value,
            BotanistWateringAfter4AM = _botanistWatering.Value,
            PlantGrowthAtEndOfDay = _plantEnabled.Value,
            ProtectWaterDuringFrozenGrowth = _protectWater.Value,
            Diagnostics = _diagnostics.Value,
            SecondsPerPlantMinute = _secondsPerMinute.Value,
            RealSecondsPerGameMinute = _realSecondsPerGameMinute.Value,
            NormalizedGrowthPerMinute = _growthPerMinute.Value,
            AfterHoursGrowthMultiplier = _afterHoursBoost.Value
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
            bool ReadBool(string key, bool existing) =>
                root.TryGetProperty(key, out var p) &&
                (p.ValueKind == JsonValueKind.True || p.ValueKind == JsonValueKind.False)
                    ? p.GetBoolean() : existing;
            float ReadFloat(string key, float existing) =>
                root.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.Number &&
                p.TryGetSingle(out float value) && float.IsFinite(value) ? value : existing;
            // Parse the full file before applying anything, so invalid JSON
            // cannot partially overwrite live preferences.
            _employeeEnabled.Value = ReadBool("EmployeesAtEndOfDay", _employeeEnabled.Value);
            _botanistWatering.Value = ReadBool("BotanistWateringAfter4AM", _botanistWatering.Value);
            _plantEnabled.Value = ReadBool("PlantGrowthAtEndOfDay", _plantEnabled.Value);
            _protectWater.Value = ReadBool("ProtectWaterDuringFrozenGrowth", _protectWater.Value);
            _diagnostics.Value = ReadBool("Diagnostics", _diagnostics.Value);
            _secondsPerMinute.Value = Math.Max(1f, ReadFloat("SecondsPerPlantMinute", _secondsPerMinute.Value));
            _realSecondsPerGameMinute.Value = Math.Max(0.01f, ReadFloat("RealSecondsPerGameMinute", _realSecondsPerGameMinute.Value));
            _growthPerMinute.Value = Math.Max(0f, ReadFloat("NormalizedGrowthPerMinute", _growthPerMinute.Value));
            _afterHoursBoost.Value = Math.Max(0.01f, ReadFloat("AfterHoursGrowthMultiplier", _afterHoursBoost.Value));
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
        // _minuteCarry tracks REAL elapsed seconds, not game minutes.
        // The old code treated one 30-second interval as one game minute,
        // yielding approximately 30x slower growth than native game time.
        _minuteCarry += elapsed;
        float updateInterval = Math.Max(1f, _secondsPerMinute.Value);
        if (_minuteCarry < updateInterval) return;
        double periodSeconds = _minuteCarry;
        _minuteCarry = 0;
        float secondsPerGameMinute = _realSecondsPerGameMinute.Value;
        if (!float.IsFinite(secondsPerGameMinute) || secondsPerGameMinute <= 0f)
            secondsPerGameMinute = 1f;
        float simulatedMinutes = (float)Math.Min(periodSeconds / secondsPerGameMinute, 300d);

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
                    float increment = CalculatePlantGrowth(plant, simulatedMinutes);
                    if (increment <= 0) continue;
                    float target = Math.Min(1f, Math.Max(0f, currentProgress) + increment);
                    _setGrowthProgress!.Invoke(plant, new object[] { target });
                    if (_diagnostics.Value && count == 0)
                    {
                        var observed = _getGrowthProgress.GetValue(plant);
                        MelonLogger.Msg($"Plant growth check: before={currentProgress:F5}, requested={target:F5}, after={observed}.");
                    }
                }
                else _plantMinutes!.Invoke(plant, new object[] { Math.Max(1, (int)Math.Round(simulatedMinutes)) });
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
        else if (boost > 2f && !_highBoostWarning)
        {
            _highBoostWarning = true;
            MelonLogger.Warning($"High after-hours growth multiplier ({boost}x) can skip growth stages.");
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
            float result = (minutes / (hours * 60f)) * light * lightFactor * temperature * potSpeed * boost;
            if (!float.IsFinite(result)) throw new InvalidOperationException("Invalid native growth increment.");
            return Math.Clamp(result, 0f, 1f);
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
            float updateIntervals = minutes * secondsPerGameMinute / Math.Max(1f, _secondsPerMinute.Value);
            return Math.Clamp(_growthPerMinute.Value, 0f, 0.05f) * updateIntervals * boost;
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
