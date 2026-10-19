using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ScheduleI24Seven.Logistics;

namespace ScheduleI24Seven.Diagnostics;

/// <summary>
/// Diagnostic-only inventory API manifest. Invoke manually while diagnostics
/// are enabled; never performs inventory or financial operations.
/// </summary>
public static class LogisticsDiagnosticReport
{
    public static string Write(string userDataDirectory)
    {
        if (string.IsNullOrWhiteSpace(userDataDirectory))
            throw new ArgumentException("UserData directory required.", nameof(userDataDirectory));
        Directory.CreateDirectory(userDataDirectory);
        var results = LogisticsApiDiscovery.Inspect();
        var report = new
        {
            SchemaVersion = 1,
            GeneratedUtc = DateTime.UtcNow,
            ReadOnly = true,
            Types = results
        };
        var path = Path.Combine(userDataDirectory, "ScheduleI24Seven-Logistics-API.json");
        // Report is metadata only: class/member signatures, no game inventory or balances.
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, true);
        return path;
    }
}
