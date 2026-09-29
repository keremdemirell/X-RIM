// XRim test runner. Hosts NUnit's own framework runner (nunit.framework.dll from Unity's com.unity.ext.nunit
// package) on Unity's bundled .NET, so EditMode tests of engine-free modules run without the Unity Editor.
// Tools/check.py builds it into Temp/XRimCheck/runner/ and starts it; it is not part of the Unity project.
//
// A test runs when its namespace names an engine-free module: XRim.Tests.EditMode.Rules.Paths belongs to
// XRim.Rules (longest module match wins). Every other test is reported as "needs Unity" and is not run.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.RegularExpressions;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace XRim.Tools.TestRunner
{
    internal static class Program
    {
        private const string UnityTestAttributeName = "UnityTestAttribute";
        private static readonly Regex StackFrameLocation = new Regex(@" in (?<file>.+):line (?<line>\d+)\s*$");

        private static int Main(string[] args)
        {
            Options options;
            try
            {
                options = Options.Parse(args);
            }
            catch (ArgumentException error)
            {
                Console.Error.WriteLine(error.Message);
                return 2;
            }

            var report = new Report();
            try
            {
                Run(options, report);
            }
            catch (Exception error)
            {
                report.Error = error.ToString();
            }

            File.WriteAllText(options.ResultsPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            return report.Error == null && report.Failed.Count == 0 ? 0 : 1;
        }

        private static void Run(Options options, Report report)
        {
            InstallAssemblyResolver(options.ProbeDirectories);
            Assembly testAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(options.AssemblyPath));

            var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
            var settings = new Dictionary<string, object>
            {
                ["NumberOfTestWorkers"] = 0,
                ["WorkDirectory"] = options.WorkDirectory,
            };
            ITest root = runner.Load(testAssembly, settings);
            if (root.RunState == RunState.NotRunnable)
            {
                report.Error = "NUnit could not load the test assembly: " + root.Properties.Get(PropertyNames.SkipReason);
                return;
            }

            var selected = new HashSet<string>();
            foreach (ITest test in LeafTests(root))
            {
                string name = test.FullName;
                if (options.Filter.Length > 0 && name.IndexOf(options.Filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                string module = ModuleOf(test, options);
                string unityReason = options.ListOnly ? options.ListOnlyReason
                    : module == null ? "its namespace names no XRim module"
                    : !options.EngineFreeModules.Contains(module) ? module + " uses the Unity engine"
                    : IsUnityTest(test) ? "[UnityTest] needs the Unity player loop"
                    : null;
                if (unityReason != null)
                {
                    string group = options.ListOnly ? options.ListOnlyGroup : module ?? "(no module)";
                    report.NeedsUnity.Add(new NeedsUnityEntry { Name = name, Module = group, Reason = unityReason });
                    continue;
                }

                if (test.RunState == RunState.Explicit && options.Filter.Length == 0)
                {
                    report.Skipped.Add(new TestEntry { Name = name, Message = "[Explicit]" });
                    continue;
                }

                selected.Add(test.Id);
            }

            if (selected.Count == 0)
            {
                return;
            }

            ITestResult result = runner.Run(new SilentListener(), new SelectionFilter(selected));
            var seen = new HashSet<string>();
            foreach (ITestResult leaf in LeafResults(result))
            {
                if (!selected.Contains(leaf.Test.Id) || !seen.Add(leaf.Test.Id))
                {
                    continue;
                }

                Record(report, leaf);
            }

            foreach (ITest test in LeafTests(root))
            {
                if (selected.Contains(test.Id) && !seen.Contains(test.Id))
                {
                    report.Failed.Add(new TestEntry { Name = test.FullName, Message = "The test produced no result." });
                }
            }
        }

        private static void Record(Report report, ITestResult leaf)
        {
            var entry = new TestEntry { Name = leaf.Test.FullName, Message = (leaf.Message ?? string.Empty).Trim() };
            switch (leaf.ResultState.Status)
            {
                case TestStatus.Passed:
                    report.Passed.Add(entry);
                    break;
                case TestStatus.Failed:
                    entry.Location = FirstProjectFrame(leaf.StackTrace);
                    entry.StackTrace = leaf.StackTrace ?? string.Empty;
                    report.Failed.Add(entry);
                    break;
                case TestStatus.Inconclusive:
                    report.Inconclusive.Add(entry);
                    break;
                default:
                    report.Skipped.Add(entry);
                    break;
            }
        }

        private static string ModuleOf(ITest test, Options options)
        {
            string ns = test.TypeInfo != null ? test.TypeInfo.Namespace : null;
            string prefix = options.TestRootNamespace + ".";
            if (ns == null || !ns.StartsWith(prefix, StringComparison.Ordinal))
            {
                return null;
            }

            string rest = ns.Substring(prefix.Length);
            string best = null;
            foreach (string module in options.AllModules)
            {
                bool matches = rest == module || rest.StartsWith(module + ".", StringComparison.Ordinal);
                if (matches && (best == null || module.Length > best.Length))
                {
                    best = module;
                }
            }

            return best;
        }

        private static bool IsUnityTest(ITest test)
        {
            try
            {
                if (test.Method == null)
                {
                    return false;
                }

                foreach (object attribute in test.Method.MethodInfo.GetCustomAttributes(false))
                {
                    if (attribute.GetType().Name == UnityTestAttributeName)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception)
            {
                return true; // the attribute types need Unity to load
            }
        }

        private static string FirstProjectFrame(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
            {
                return string.Empty;
            }

            string fallback = string.Empty;
            foreach (string line in stackTrace.Split('\n'))
            {
                Match match = StackFrameLocation.Match(line.TrimEnd('\r'));
                if (!match.Success)
                {
                    continue;
                }

                string location = match.Groups["file"].Value + ":" + match.Groups["line"].Value;
                if (match.Groups["file"].Value.Replace('\\', '/').Contains("/Assets/"))
                {
                    return location;
                }

                if (fallback.Length == 0)
                {
                    fallback = location;
                }
            }

            return fallback;
        }

        private static IEnumerable<ITest> LeafTests(ITest test)
        {
            if (!test.IsSuite)
            {
                yield return test;
                yield break;
            }

            foreach (ITest child in test.Tests)
            {
                foreach (ITest leaf in LeafTests(child))
                {
                    yield return leaf;
                }
            }
        }

        private static IEnumerable<ITestResult> LeafResults(ITestResult result)
        {
            if (!result.Test.IsSuite)
            {
                yield return result;
                yield break;
            }

            foreach (ITestResult child in result.Children)
            {
                foreach (ITestResult leaf in LeafResults(child))
                {
                    yield return leaf;
                }
            }
        }

        private static void InstallAssemblyResolver(IReadOnlyList<string> directories)
        {
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                foreach (string directory in directories)
                {
                    string candidate = Path.Combine(directory, name.Name + ".dll");
                    if (File.Exists(candidate))
                    {
                        return context.LoadFromAssemblyPath(Path.GetFullPath(candidate));
                    }
                }

                return null;
            };
        }
    }

    /// <summary>Selects exactly the test cases chosen by the runner; suites pass when a descendant is selected.</summary>
    internal sealed class SelectionFilter : TestFilter
    {
        private readonly HashSet<string> _ids;

        public SelectionFilter(HashSet<string> ids)
        {
            _ids = ids;
        }

        public override bool Match(ITest test) => !test.IsSuite && _ids.Contains(test.Id);

        public override TNode AddToXml(TNode parentNode, bool recursive) => parentNode.AddElement("xrim-selection");
    }

    internal sealed class SilentListener : ITestListener
    {
        public void TestStarted(ITest test)
        {
        }

        public void TestFinished(ITestResult result)
        {
        }

        public void TestOutput(TestOutput output)
        {
        }
    }

    internal sealed class Options
    {
        public string AssemblyPath { get; private set; } = string.Empty;
        public string ResultsPath { get; private set; } = string.Empty;
        public string WorkDirectory { get; private set; } = Directory.GetCurrentDirectory();
        public string TestRootNamespace { get; private set; } = string.Empty;
        public string Filter { get; private set; } = string.Empty;
        public bool ListOnly { get; private set; }
        public string ListOnlyReason { get; private set; } = string.Empty;
        public string ListOnlyGroup { get; private set; } = "PlayMode";
        public List<string> ProbeDirectories { get; } = new List<string>();
        public HashSet<string> EngineFreeModules { get; } = new HashSet<string>(StringComparer.Ordinal);
        public List<string> AllModules { get; } = new List<string>();

        public static Options Parse(string[] args)
        {
            var options = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string key = args[i];
                string value = i + 1 < args.Length ? args[i + 1] : throw new ArgumentException("Missing value for " + key);
                i++;
                switch (key)
                {
                    case "--assembly": options.AssemblyPath = value; break;
                    case "--results": options.ResultsPath = value; break;
                    case "--work-directory": options.WorkDirectory = value; break;
                    case "--test-root": options.TestRootNamespace = value; break;
                    case "--filter": options.Filter = value; break;
                    case "--probe": options.ProbeDirectories.Add(value); break;
                    case "--list-only": options.ListOnly = true; options.ListOnlyReason = value; break;
                    case "--list-only-group": options.ListOnlyGroup = value; break;
                    case "--engine-free-module": options.EngineFreeModules.Add(value); options.AllModules.Add(value); break;
                    case "--engine-module": options.AllModules.Add(value); break;
                    default: throw new ArgumentException("Unknown option " + key);
                }
            }

            if (options.AssemblyPath.Length == 0 || options.ResultsPath.Length == 0 || options.TestRootNamespace.Length == 0)
            {
                throw new ArgumentException("--assembly, --results and --test-root are required.");
            }

            return options;
        }
    }

    internal sealed class Report
    {
        public string Error { get; set; }
        public List<TestEntry> Passed { get; } = new List<TestEntry>();
        public List<TestEntry> Failed { get; } = new List<TestEntry>();
        public List<TestEntry> Skipped { get; } = new List<TestEntry>();
        public List<TestEntry> Inconclusive { get; } = new List<TestEntry>();
        public List<NeedsUnityEntry> NeedsUnity { get; } = new List<NeedsUnityEntry>();
    }

    internal sealed class TestEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string StackTrace { get; set; } = string.Empty;
    }

    internal sealed class NeedsUnityEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
