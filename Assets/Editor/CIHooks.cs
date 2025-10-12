#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.TestTools;

public static class CIHooks
{
    public static void CompileAndQuit()
    {
        // 方法能被调用即意味着脚本编译成功，直接以 0 退出。
        EditorApplication.Exit(0);
    }

    public static void RunEditModeTests()
    {
        RunTests(TestMode.EditMode);
    }

    public static void RunPlayModeTests()
    {
        RunTests(TestMode.PlayMode);
    }

    private static void RunTests(TestMode mode)
    {
        var resultsPath = Environment.GetEnvironmentVariable("CI_TEST_RESULTS_PATH");
        if (string.IsNullOrEmpty(resultsPath))
        {
            var fileName = $"results_{mode.ToString().ToLowerInvariant()}.xml";
            resultsPath = Path.Combine(Environment.CurrentDirectory, "AutomationOutputs", fileName);
        }

        var directory = Path.GetDirectoryName(resultsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        var callbacks = new TestRunCallbacks(resultsPath);
        api.RegisterCallbacks(callbacks);

        var filter = BuildFilter(mode);
        var settings = new ExecutionSettings(filter)
        {
            runSynchronously = true
        };

        api.Execute(settings);
    }

    private static Filter BuildFilter(TestMode mode)
    {
        var filter = new Filter { testMode = mode };

        var categoryEnv = Environment.GetEnvironmentVariable("CI_TEST_CATEGORY");
        if (!string.IsNullOrWhiteSpace(categoryEnv))
        {
            filter.categoryNames = categoryEnv.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        var assemblyEnv = Environment.GetEnvironmentVariable("CI_TEST_ASSEMBLIES");
        if (!string.IsNullOrWhiteSpace(assemblyEnv))
        {
            filter.assemblyNames = assemblyEnv.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        var nameEnv = Environment.GetEnvironmentVariable("CI_TEST_NAMES");
        if (!string.IsNullOrWhiteSpace(nameEnv))
        {
            filter.testNames = nameEnv.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        return filter;
    }

    private sealed class TestRunCallbacks : ICallbacks
    {
        private readonly string resultsPath;

        public TestRunCallbacks(string resultsPath)
        {
            this.resultsPath = resultsPath;
        }

        public void RunStarted(ITestAdaptor testsToRun)
        {
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            try
            {
                WriteResults(result);
            }
            finally
            {
                var exitCode = result.FailCount > 0 ? 1 : 0;
                EditorApplication.Exit(exitCode);
            }
        }

        private void WriteResults(ITestResultAdaptor result)
        {
            var document = new XDocument(CreateElement(result, isRoot: true));
            document.Save(resultsPath);
        }

        private static XElement CreateElement(ITestResultAdaptor result, bool isRoot)
        {
            var elementName = isRoot ? "test-run" : (result.HasChildren ? "test-suite" : "test-case");
            var element = new XElement(elementName,
                new XAttribute("name", result.Name ?? string.Empty),
                new XAttribute("fullname", result.FullName ?? string.Empty),
                new XAttribute("result", result.ResultState ?? string.Empty),
                new XAttribute("duration", result.Duration.ToString("F6", CultureInfo.InvariantCulture)),
                new XAttribute("passed", result.PassCount),
                new XAttribute("failed", result.FailCount),
                new XAttribute("skipped", result.SkipCount),
                new XAttribute("inconclusive", result.InconclusiveCount),
                new XAttribute("asserts", result.AssertCount));

            var total = result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount;
            element.SetAttributeValue("total", total);

            if (result.HasChildren)
            {
                foreach (var child in result.Children)
                {
                    element.Add(CreateElement(child, isRoot: false));
                }
            }
            else if (result.FailCount > 0 && (!string.IsNullOrEmpty(result.Message) || !string.IsNullOrEmpty(result.StackTrace)))
            {
                var failure = new XElement("failure");
                if (!string.IsNullOrEmpty(result.Message))
                {
                    failure.Add(new XElement("message", result.Message));
                }

                if (!string.IsNullOrEmpty(result.StackTrace))
                {
                    failure.Add(new XElement("stack-trace", result.StackTrace));
                }

                element.Add(failure);
            }

            return element;
        }
    }
}
#endif
