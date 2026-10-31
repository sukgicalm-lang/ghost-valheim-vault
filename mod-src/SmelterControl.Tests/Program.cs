// In-process runner for restricted hosts where VSTest cannot start its testhost.
// Invokes the same xUnit Fact/Theory methods and uses the actual xUnit assertions.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.IO;
using Xunit;

internal static class Program
{
    private static int Main()
    {
        var results = new List<object>(); int passed = 0, failed = 0;
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes().OrderBy(t => t.FullName))
        foreach (var method in type.GetMethods().Where(m => m.GetCustomAttributes<FactAttribute>().Any()))
        {
            var data = method.GetCustomAttributesData().Where(a => a.AttributeType == typeof(InlineDataAttribute)).ToArray();
            var rows = data.Length == 0 ? new[] { Array.Empty<object>() }
                : data.Select(a => ((IEnumerable<CustomAttributeTypedArgument>)a.ConstructorArguments[0].Value)
                    .Select(v => v.Value).ToArray()).ToArray();
            for (int i = 0; i < rows.Length; i++)
            {
                string name = type.Name + "." + method.Name + (data.Length > 0 ? "[" + i + "]" : "");
                try
                {
                    method.Invoke(Activator.CreateInstance(type), rows[i]); passed++;
                    Console.WriteLine("PASS " + name); results.Add(new { name, passed = true });
                }
                catch (Exception e)
                {
                    failed++; var cause = e.InnerException ?? e;
                    Console.WriteLine("FAIL " + name + "\n" + cause);
                    results.Add(new { name, passed = false, error = cause.ToString() });
                }
            }
        }
        File.WriteAllText("test-report.json", JsonSerializer.Serialize(new { passed, failed, results },
            new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"RESULT: {passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}
