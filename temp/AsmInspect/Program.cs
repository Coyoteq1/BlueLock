using System.Reflection;
using System.Runtime.Loader;

var libs = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "VAutomationCore", "libs"));
if (!Directory.Exists(libs))
{
    Console.Error.WriteLine($"libs not found: {libs}");
    return;
}

var alc = new AssemblyLoadContext("inspect", isCollectible: true);

// Preload all game assemblies in a stable order to reduce type load failures.
foreach (var dll in Directory.GetFiles(libs, "*.dll").OrderBy(p => p))
{
    try { alc.LoadFromAssemblyPath(dll); } catch { }
}

Console.WriteLine();
Console.WriteLine("Searching all libs for method 'UnlockVBlood'...");
foreach (var dll in Directory.GetFiles(libs, "*.dll").OrderBy(p => p))
{
    Assembly a;
    try { a = alc.LoadFromAssemblyPath(dll); } catch { continue; }
    Type[] ts;
    try { ts = a.GetTypes(); }
    catch (ReflectionTypeLoadException ex) { ts = ex.Types.Where(t => t != null).ToArray()!; }

    foreach (var t in ts)
    {
        MethodInfo? m = null;
        try { m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).FirstOrDefault(x => x.Name == "UnlockVBlood"); }
        catch { }
        if (m != null)
        {
            Console.WriteLine($"  {a.GetName().Name}: {t.FullName}.UnlockVBlood");
            goto DoneUnlockSearch;
        }
    }
}
DoneUnlockSearch: ;

Console.WriteLine();
Console.WriteLine("Unity.Entities.World methods (filtered):");
try
{
    var unityEntitiesAsm = alc.Assemblies.FirstOrDefault(a => string.Equals(a.GetName().Name, "Unity.Entities", StringComparison.OrdinalIgnoreCase));
    var worldType = unityEntitiesAsm?.GetType("Unity.Entities.World");
    if (worldType != null)
    {
        foreach (var m in worldType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                     .Where(m =>
                         m.Name.Contains("ExistingSystem", StringComparison.OrdinalIgnoreCase) ||
                         m.Name.Contains("GetOrCreateSystem", StringComparison.OrdinalIgnoreCase) ||
                         m.Name.Contains("Unmanaged", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(m => m.Name))
        {
            Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
        }
    }
    else
    {
        Console.WriteLine("  Unity.Entities.World type not found.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  Failed to inspect World methods: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("Unity.Entities.WorldUnmanaged methods (filtered):");
try
{
    var unityEntitiesAsm = alc.Assemblies.FirstOrDefault(a => string.Equals(a.GetName().Name, "Unity.Entities", StringComparison.OrdinalIgnoreCase));
    var unmanagedType = unityEntitiesAsm?.GetType("Unity.Entities.WorldUnmanaged");
    if (unmanagedType != null)
    {
        foreach (var m in unmanagedType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                     .Where(m =>
                         m.Name.Contains("GetExistingSystem", StringComparison.OrdinalIgnoreCase) ||
                         m.Name.Contains("GetOrCreateSystem", StringComparison.OrdinalIgnoreCase) ||
                         m.Name.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
                         m.Name.Contains("SystemRef", StringComparison.OrdinalIgnoreCase) ||
                         m.Name.Contains("GetUnsafe", StringComparison.OrdinalIgnoreCase) ||
                         m.Name.Contains("GetSystemState", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(m => m.Name)
                     .Take(60))
        {
            Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
        }
    }
    else
    {
        Console.WriteLine("  Unity.Entities.WorldUnmanaged type not found.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  Failed to inspect WorldUnmanaged methods: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("ProjectM.InventoryUtilitiesServer methods (subset) will print after type scan.");

Assembly Load(string name)
{
    var path = Path.Combine(libs, name);
    try { return alc.LoadFromAssemblyPath(path); } catch { return Assembly.LoadFrom(path); }
}

var asm = Load("ProjectM.dll");
var asmShared = Load("ProjectM.Shared.dll");
var asmScripting = Load("ProjectM.Gameplay.Scripting.dll");
var asmGameplaySystems = Load("ProjectM.Gameplay.Systems.dll");

var patterns = new[]
{
    "UserComponent",
    "Inventory",
    "InventoryBuffer",
    "Equipment",
    "EquippedWeapon",
    "WeaponSocket",
    "VBlood",
    "VBloodProgressionSystem",
    "Spellbook",
    "BuffBuffer",
    "HideOutsideVision",
};

Console.WriteLine($"Assembly: {asm.FullName}");
Console.WriteLine("Matches:");

Type[] types;
try
{
    types = asm.GetTypes();
}
catch (ReflectionTypeLoadException ex)
{
    types = ex.Types.Where(t => t != null).ToArray()!;
    Console.WriteLine("ReflectionTypeLoadException:");
    foreach (var le in ex.LoaderExceptions.Take(20))
        Console.WriteLine($"  loader: {le?.Message}");
}

foreach (var t in types.OrderBy(t => t.FullName))
{
    var name = t.FullName ?? t.Name;
    if (!patterns.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase)))
        continue;

    Console.WriteLine(name);

    if (t.IsValueType && !t.IsEnum)
    {
        var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (var f in fields)
        {
            Console.WriteLine($"  field: {f.FieldType.FullName} {f.Name}");
        }
    }
}

Console.WriteLine();
Console.WriteLine("RepairVBloodProgressionSystem info:");
try
{
    var repairType = types.FirstOrDefault(t => string.Equals(t.FullName, "ProjectM.RepairVBloodProgressionSystem", StringComparison.Ordinal));
    if (repairType == null)
    {
        Console.WriteLine("  Not found in ProjectM types list (may be in a different assembly).");
    }
    else
    {
        Console.WriteLine($"  FullName: {repairType.FullName}");
        Console.WriteLine($"  IsValueType: {repairType.IsValueType}");
        Console.WriteLine($"  BaseType: {repairType.BaseType?.FullName}");
        Console.WriteLine("  OnUpdate overloads:");
        foreach (var m in repairType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                     .Where(m => string.Equals(m.Name, "OnUpdate", StringComparison.Ordinal))
                     .OrderBy(m => m.GetParameters().Length))
        {
            Console.WriteLine($"    {m.ReturnType.Name} OnUpdate({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  Failed to inspect RepairVBloodProgressionSystem: {ex.Message}");
}

Console.WriteLine();
TryDumpType("ProjectM.Network.UserComponent");

void TryDumpType(string fullName)
{
    var type = types.FirstOrDefault(t => string.Equals(t.FullName, fullName, StringComparison.Ordinal));
    if (type == null) return;
Console.WriteLine($"{fullName} fields:");
    foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
    {
        Console.WriteLine($"  {f.Name} : {f.FieldType.Name}");
    }
}
Console.WriteLine();
Console.WriteLine("ProjectM.InventoryUtilitiesServer methods (subset):");
var invUtilServer = types.FirstOrDefault(t => string.Equals(t.FullName, "ProjectM.InventoryUtilitiesServer", StringComparison.Ordinal));
if (invUtilServer != null)
{
    foreach (var m in invUtilServer.GetMethods(BindingFlags.Public | BindingFlags.Static)
                 .Where(m => m.Name.StartsWith("Try", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("Inventory", StringComparison.OrdinalIgnoreCase))
                 .OrderBy(m => m.Name)
                 .Take(80))
    {
        Console.WriteLine($"  {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))}) -> {m.ReturnType.Name}");
    }
}

// Also scan ProjectM.Shared for buffers/components.
Console.WriteLine();
Console.WriteLine($"Assembly: {asmShared.FullName}");
Type[] sharedTypes;
try { sharedTypes = asmShared.GetTypes(); }
catch (ReflectionTypeLoadException ex) { sharedTypes = ex.Types.Where(t => t != null).ToArray()!; }

foreach (var t in sharedTypes.OrderBy(t => t.FullName))
{
    var name = t.FullName ?? t.Name;
    if (!patterns.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase)))
        continue;

    Console.WriteLine(name);
    if (t.IsValueType && !t.IsEnum)
    {
        var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (var f in fields)
            Console.WriteLine($"  field: {f.FieldType.FullName} {f.Name}");
    }
}

Console.WriteLine();
Console.WriteLine($"Assembly: {asmScripting.FullName}");
Type[] scriptTypes;
try { scriptTypes = asmScripting.GetTypes(); }
catch (ReflectionTypeLoadException ex) { scriptTypes = ex.Types.Where(t => t != null).ToArray()!; }

var serverGameManager = scriptTypes.FirstOrDefault(t => string.Equals(t.FullName, "ProjectM.Scripting.ServerGameManager", StringComparison.Ordinal))
    ?? scriptTypes.FirstOrDefault(t => (t.FullName ?? "").EndsWith(".ServerGameManager", StringComparison.Ordinal));

if (serverGameManager != null)
{
    Console.WriteLine("ProjectM.Scripting.ServerGameManager interesting methods:");
    foreach (var m in serverGameManager.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                 .Where(m =>
                     m.Name.Contains("Inventory", StringComparison.OrdinalIgnoreCase) ||
                     m.Name.Contains("Equip", StringComparison.OrdinalIgnoreCase) ||
                     m.Name.Contains("Unequip", StringComparison.OrdinalIgnoreCase) ||
                     m.Name.Contains("Spell", StringComparison.OrdinalIgnoreCase) ||
                     m.Name.Contains("VBlood", StringComparison.OrdinalIgnoreCase) ||
                     m.Name.Contains("Blood", StringComparison.OrdinalIgnoreCase))
                 .OrderBy(m => m.Name))
    {
        Console.WriteLine($"  {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))}) -> {m.ReturnType.Name}");
    }
}
else
{
    Console.WriteLine("ServerGameManager type not found in scripting assembly.");
}

Console.WriteLine();
Console.WriteLine($"Assembly: {asmGameplaySystems.FullName}");
Type[] sysTypes;
try { sysTypes = asmGameplaySystems.GetTypes(); }
catch (ReflectionTypeLoadException ex) { sysTypes = ex.Types.Where(t => t != null).ToArray()!; }

var vbloodSys = sysTypes.FirstOrDefault(t => (t.FullName ?? "").Contains("VBloodProgressionSystem", StringComparison.OrdinalIgnoreCase));
Console.WriteLine(vbloodSys != null ? $"Found: {vbloodSys.FullName}" : "VBloodProgressionSystem not found.");

Console.WriteLine();
Console.WriteLine("Searching all libs for '*VBloodProgressionSystem*' type...");
foreach (var dll in Directory.GetFiles(libs, "*.dll").OrderBy(p => p))
{
    Assembly a;
    try { a = alc.LoadFromAssemblyPath(dll); } catch { continue; }
    Type[] ts;
    try { ts = a.GetTypes(); }
    catch (ReflectionTypeLoadException ex) { ts = ex.Types.Where(t => t != null).ToArray()!; }

    var match = ts.FirstOrDefault(t => (t.FullName ?? "").IndexOf("VBloodProgressionSystem", StringComparison.OrdinalIgnoreCase) >= 0);
    if (match != null)
        Console.WriteLine($"  {Path.GetFileName(dll)}: {match.FullName}");
}
