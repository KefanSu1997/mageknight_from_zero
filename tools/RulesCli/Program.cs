using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using MK.Logic.Data;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Filters;

// This runner compiles the real production sources and the same NUnit tests Unity uses.
// An explicit fixture/method filter is mandatory; it never silently runs the full library.
if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: RulesCli <project-root> <fully-qualified-fixture-or-method> [...]");
    return 2;
}
var root = Path.GetFullPath(args[0]);
if (!File.Exists(Path.Combine(root, "Assets/Logic/MageKnight.Logic.asmdef")))
    throw new ArgumentException("Not a MageKnight project root");
Directory.SetCurrentDirectory(root);
CardJsonLoader.SetBaseDirectory(Path.Combine(root, "resources/text_json"));
var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
runner.Load(Assembly.GetExecutingAssembly(), new Dictionary<string, object>());
var filters = new List<ITestFilter>();
foreach (var name in args.Skip(1).Distinct())
{
    var filter = new FullNameFilter(name);
    if (runner.CountTestCases(filter) == 0)
        throw new ArgumentException("Unknown or empty test selection: " + name);
    filters.Add(filter);
}
var selected = new OrFilter(filters.ToArray());
var result = runner.Run(TestListener.NULL, selected);
var output = Path.Combine(root, "AutomationOutputs/RulesState", DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
Directory.CreateDirectory(output);
File.WriteAllText(Path.Combine(output, "results.xml"), result.ToXml(true).OuterXml);
var inputs = Directory.GetFiles(Path.Combine(root, "Assets/Logic"), "*.cs", SearchOption.AllDirectories)
    .Concat(Directory.GetFiles(Path.Combine(root, "Assets/Tests/RulesState"), "*.cs", SearchOption.AllDirectories))
    .Concat(Directory.GetFiles(Path.Combine(root, "resources/text_json"), "*.json"))
    .Concat(new[] { "CardCombatRulesTests.cs", "CardWoundConservationTests.cs", "PossessResolutionTests.cs",
        "MistFormResolutionTests.cs", "OriginalUnitDamageTests.cs" }.Select(name => Path.Combine(root, "Assets/Tests", name)));
var fingerprints = inputs.OrderBy(p => p, StringComparer.Ordinal).ToDictionary(
    p => Path.GetRelativePath(root, p), p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant());
bool passed = result.ResultState.Status == TestStatus.Passed && result.FailCount == 0 && result.SkipCount == 0 && result.InconclusiveCount == 0;
var summary = new { status = passed ? "passed" : "failed", scope = "backend state only; scene acceptance still required",
    selected = args.Skip(1).ToArray(), total = result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount, passed = result.PassCount, failed = result.FailCount,
    skipped = result.SkipCount, durationSeconds = result.Duration, sourceFingerprints = fingerprints };
File.WriteAllText(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"{summary.status}: {summary.passed}/{summary.total}, failed={summary.failed}, skipped={summary.skipped}, {summary.durationSeconds:F3}s");
Console.WriteLine(output);
return passed ? 0 : 1;
