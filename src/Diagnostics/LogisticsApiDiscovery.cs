using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ScheduleI24Seven.Logistics;

namespace ScheduleI24Seven.Diagnostics;

/// <summary>
/// Read-only runtime type discovery for integrating real Schedule I inventory.
/// No methods on discovered game objects are invoked, and no inventory is changed.
/// </summary>
public static class LogisticsApiDiscovery
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance | BindingFlags.Static;

    private static readonly string[] Candidates =
    {
        "Il2CppScheduleOne.Property.Property",
        "Il2CppScheduleOne.Property.Business",
        "Il2CppScheduleOne.Storage.StorageEntity",
        "Il2CppScheduleOne.Storage.StorageDoorAnimation",
        "Il2CppScheduleOne.ItemFramework.ItemSlot",
        "Il2CppScheduleOne.ItemFramework.ItemInstance",
        "Il2CppScheduleOne.ItemFramework.Inventory",
        "Il2CppScheduleOne.ItemFramework.ItemDefinition",
        "Il2CppScheduleOne.PlayerScripts.PlayerInventory",
        "Il2CppScheduleOne.Money.MoneyManager",
        "Il2CppScheduleOne.Economy.Shop",
        "Il2CppScheduleOne.Economy.Supplier"
    };

    public sealed record Member(string Kind, string Name, string Signature);
    public sealed record Result(string TypeName, bool Found,
        IReadOnlyList<Member> Members, string? Error);

    public static IReadOnlyList<Result> Inspect()
    {
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => SafeTypes(a))
            .Where(t => Candidates.Contains(t.FullName, StringComparer.Ordinal))
            .GroupBy(t => t.FullName!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var results = new List<Result>();
        foreach (string name in Candidates)
        {
            if (!types.TryGetValue(name, out var type))
            {
                results.Add(new Result(name, false, Array.Empty<Member>(), null));
                continue;
            }
            try
            {
                var members = new List<Member>();
                foreach (var f in type.GetFields(Flags))
                    members.Add(new Member("Field", f.Name, f.FieldType.FullName ?? f.FieldType.Name));
                foreach (var p in type.GetProperties(Flags))
                    members.Add(new Member("Property", p.Name, p.PropertyType.FullName ?? p.PropertyType.Name));
                foreach (var m in type.GetMethods(Flags).Where(m => !m.IsSpecialName))
                {
                    string signature = (m.ReturnType.FullName ?? m.ReturnType.Name) + " (" +
                        string.Join(", ", m.GetParameters().Select(p =>
                            (p.ParameterType.FullName ?? p.ParameterType.Name) + " " + p.Name)) + ")";
                    members.Add(new Member("Method", m.Name, signature));
                }
                results.Add(new Result(name, true,
                    members.OrderBy(m => m.Kind, StringComparer.Ordinal)
                           .ThenBy(m => m.Name, StringComparer.Ordinal).ToArray(), null));
            }
            catch (Exception ex)
            {
                results.Add(new Result(name, true, Array.Empty<Member>(), ex.GetType().Name));
            }
        }
        return results;
    }

    private static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.OfType<Type>(); }
        catch { return Array.Empty<Type>(); }
    }
}
