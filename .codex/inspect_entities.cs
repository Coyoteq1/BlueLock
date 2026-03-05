using System;
using System.Linq;
using System.Reflection;
class P {
  static void Main() {
    var asm = Assembly.LoadFrom(@"C:\Program Files (x86)\Steam\steamapps\common\VRising\Server\BepInEx\interop\Unity.Entities.dll");
    var world = asm.GetType("Unity.Entities.World");
    Console.WriteLine("WORLD=" + (world?.FullName ?? "null"));
    foreach (var m in world.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Where(m => m.Name.Contains("System")).OrderBy(m => m.Name)) {
      var pars = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.FullName));
      Console.WriteLine(m.Name + " | generic=" + m.IsGenericMethodDefinition + " | " + pars);
    }
    var em = asm.GetType("Unity.Entities.EntityManager");
    Console.WriteLine("EM=" + (em?.FullName ?? "null"));
    foreach (var m in em.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Where(m => m.Name.Contains("ComponentData") || m.Name=="AddComponent" || m.Name=="CreateEntity").OrderBy(m => m.Name)) {
      var pars = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.FullName));
      Console.WriteLine(m.Name + " | generic=" + m.IsGenericMethodDefinition + " | " + pars);
    }
  }
}
