using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BuildGuard.Editor
{
    /// <summary>
    /// Batchmode entry point for GameCI. The method validates first and returns a non-zero
    /// exit code without calling <see cref="BuildPipeline.BuildPlayer"/> when validation has errors.
    /// </summary>
    public static class BuildScript
    {
        /// <summary>Project-relative JSON summary of the player build. Written only when a build was attempted.</summary>
        public const string PlayerBuildReportPath = "BuildReports/player-build.json";

        /// <summary>
        /// GameCI calls this by name: <c>BuildGuard.Editor.BuildScript.BuildLinux64</c>.
        /// Exit 0 means validation passed and the player build succeeded.
        /// Exit 1 means validation reported errors. Exit 2 means the player build failed.
        /// </summary>
        public static void BuildLinux64()
        {
            var exitCode = Run();
            if (Application.isBatchMode && exitCode != 0)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static int Run()
        {
            var report = BuildValidationRunner.ValidateAndWriteReport();
            Log("Validation errors=" + report.ErrorCount + " warnings=" + report.WarningCount);
            if (report.HasErrors)
            {
                Fail("Validation failed. Player build was not started.\n" + report.ToJson());
                return 1;
            }

            var outputPath = ResolveOutputPath();

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var scenes = EditorBuildSettings.scenes;
            var enabled = new List<string>();
            for (var i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].enabled)
                {
                    enabled.Add(scenes[i].path);
                }
            }

            var options = new BuildPlayerOptions
            {
                scenes = enabled.ToArray(),
                locationPathName = outputPath,
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None,
            };

            var build = BuildPipeline.BuildPlayer(options);
            var seconds = build.summary.totalTime.TotalSeconds;
            WritePlayerReport(build, outputPath, seconds);

            if (build.summary.result == BuildResult.Succeeded)
            {
                Log("Build succeeded: " + outputPath + " (" + build.summary.totalSize + " bytes)");
                return 0;
            }

            Fail("Build failed: " + build.summary.result);
            return 2;
        }

        private static string ResolveOutputPath()
        {
            var fromArgs = ReadCommandLineFlag("customBuildPath");
            if (!string.IsNullOrWhiteSpace(fromArgs))
            {
                return fromArgs;
            }

            var fromEnv = Environment.GetEnvironmentVariable("BUILD_PATH");
            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                return fromEnv;
            }

            return "build/StandaloneLinux64/BuildGuard";
        }

        private static string ReadCommandLineFlag(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            var prefix = "-" + flag;
            for (var i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static void WritePlayerReport(BuildReport build, string outputPath, double durationSeconds)
        {
            var absolute = BuildValidationRunner.AbsoluteProjectPath(PlayerBuildReportPath);
            var directory = Path.GetDirectoryName(absolute);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var result = build.summary.result.ToString();
            var size = build.summary.totalSize.ToString(CultureInfo.InvariantCulture);
            var duration = durationSeconds.ToString("0.###", CultureInfo.InvariantCulture);
            var json = "{\n"
                + "  \"schemaVersion\": 1,\n"
                + "  \"result\": \"" + result + "\",\n"
                + "  \"durationSeconds\": " + duration + ",\n"
                + "  \"totalSizeBytes\": " + size + ",\n"
                + "  \"outputPath\": \"" + outputPath.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"\n"
                + "}";
            File.WriteAllText(absolute, json);
        }

        private static void Log(string message)
        {
            Console.WriteLine("[BuildGuard] " + message);
            Debug.Log("[BuildGuard] " + message);
        }

        private static void Fail(string message)
        {
            Console.Error.WriteLine("[BuildGuard] " + message);
            Debug.LogError("[BuildGuard] " + message);
        }
    }
}
