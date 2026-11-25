using System;
using System.Collections.Generic;
using BuildGuard.Editor.Validation;
using NUnit.Framework;

namespace BuildGuard.Tests.EditMode
{
    /// <summary>
    /// Validator checks run against a snapshot, not against a deliberately broken asset.
    /// That keeps the sample project clean and still covers the failure cases CI must catch.
    /// </summary>
    public sealed class BuildValidatorTests
    {
        [Test]
        public void Scene_NotEnabled_IsAWarning_AndAnEmptyBuildList_IsAnError()
        {
            var report = Validate(
                enabled: new string[0],
                scenes: new[] { "Assets/BuildGuard/Scenes/TrainingScenario.unity" },
                scans: new ScriptScan[0],
                textures: new TextureScan[0]);

            Assert.AreEqual(1, report.ErrorCount);
            Assert.AreEqual(1, report.WarningCount);
            StringAssert.Contains("not enabled", report.Issues[1].Message);
            StringAssert.Contains("empty", report.Issues[0].Message);
        }

        [Test]
        public void Scene_ListedButMissingOnDisk_IsAnError()
        {
            var report = Validate(
                enabled: new[] { "Assets/Missing.unity" },
                scenes: new[] { "Assets/BuildGuard/Scenes/TrainingScenario.unity" },
                scans: new ScriptScan[0],
                textures: new TextureScan[0]);

            Assert.IsTrue(report.HasErrors);
            StringAssert.Contains("Missing.unity", report.ToJson());
        }

        [Test]
        public void Scene_BackslashPath_MatchesTheAsset()
        {
            var report = Validate(
                enabled: new[] { @"Assets\BuildGuard\Scenes\TrainingScenario.unity" },
                scenes: new[] { "Assets/BuildGuard/Scenes/TrainingScenario.unity" },
                scans: new ScriptScan[0],
                textures: new TextureScan[0]);

            Assert.AreEqual(0, report.ErrorCount);
            Assert.AreEqual(0, report.WarningCount);
        }

        [Test]
        public void Scene_DuplicateBuildEntry_IsAWarning()
        {
            var scene = "Assets/BuildGuard/Scenes/TrainingScenario.unity";
            var report = Validate(
                enabled: new[] { scene, scene },
                scenes: new[] { scene },
                scans: new ScriptScan[0],
                textures: new TextureScan[0]);

            Assert.AreEqual(0, report.ErrorCount);
            Assert.AreEqual(1, report.WarningCount);
            StringAssert.Contains("2 times", report.Issues[0].Message);
        }

        [Test]
        public void Scene_NoAssetsAndNoBuildList_IsAnError()
        {
            var report = Validate(
                enabled: new string[0],
                scenes: new string[0],
                scans: new ScriptScan[0],
                textures: new TextureScan[0]);

            Assert.AreEqual(1, report.ErrorCount);
            Assert.AreEqual(0, report.WarningCount);
        }

        [Test]
        public void MissingScripts_Zero_IsClean()
        {
            var report = Run(new MissingScriptCheck(), Context(
                new[] { "Assets/A.unity" },
                new[] { "Assets/A.unity" },
                new[] { new ScriptScan("Assets/A.unity", 0) },
                new TextureScan[0]));

            Assert.AreEqual(0, report.Issues.Count);
        }

        [Test]
        public void MissingScripts_OnAPrefab_IsAnError()
        {
            var report = Run(new MissingScriptCheck(), Context(
                new[] { "Assets/A.unity" },
                new[] { "Assets/A.unity" },
                new[]
                {
                    new ScriptScan("Assets/A.unity", 0),
                    new ScriptScan("Assets/Broken.prefab", 2),
                },
                new TextureScan[0]));

            Assert.AreEqual(1, report.ErrorCount);
            Assert.AreEqual("Assets/Broken.prefab", report.Issues[0].AssetPath);
            StringAssert.Contains("2 missing", report.Issues[0].Message);
        }

        [Test]
        public void Texture_AtBudget_IsClean()
        {
            var report = Run(new TextureBudgetCheck(), Context(
                new[] { "Assets/A.unity" },
                new[] { "Assets/A.unity" },
                new ScriptScan[0],
                new[] { new TextureScan("Assets/Art/Plate.png", 2048, 2048) }));

            Assert.AreEqual(0, report.Issues.Count);
        }

        [Test]
        public void Texture_WiderThanBudget_IsAWarning()
        {
            var report = Run(new TextureBudgetCheck(), Context(
                new[] { "Assets/A.unity" },
                new[] { "Assets/A.unity" },
                new ScriptScan[0],
                new[] { new TextureScan("Assets/Art/Wide.png", 2049, 128) },
                new ValidationSettings(2048, 2048)));

            Assert.AreEqual(0, report.ErrorCount);
            Assert.AreEqual(1, report.WarningCount);
            StringAssert.Contains("2049x128", report.Issues[0].Message);
        }

        [Test]
        public void Texture_TallerThanBudget_IsAWarning()
        {
            var report = Run(new TextureBudgetCheck(), Context(
                new[] { "Assets/A.unity" },
                new[] { "Assets/A.unity" },
                new ScriptScan[0],
                new[] { new TextureScan("Assets/Art/Tall.png", 64, 4096) }));

            Assert.AreEqual(ValidationSeverity.Warning, report.Issues[0].Severity);
            StringAssert.Contains("Tall.png", report.Issues[0].Message);
        }

        [Test]
        public void Texture_NonPositiveBudget_IsAnError_AndSkipsTextureWarnings()
        {
            var report = Run(new TextureBudgetCheck(), Context(
                new[] { "Assets/A.unity" },
                new[] { "Assets/A.unity" },
                new ScriptScan[0],
                new[] { new TextureScan("Assets/Art/Wide.png", 9000, 9000) },
                new ValidationSettings(0, 2048)));

            Assert.AreEqual(1, report.ErrorCount);
            Assert.AreEqual(0, report.WarningCount);
            StringAssert.Contains("positive", report.Issues[0].Message);
        }

        [Test]
        public void Validator_RunsEveryCheck_AndCountsSeverities()
        {
            var report = Validate(
                enabled: new string[0],
                scenes: new[] { "Assets/Only.unity" },
                scans: new[] { new ScriptScan("Assets/Broken.prefab", 1) },
                textures: new[] { new TextureScan("Assets/Art/Wide.png", 3000, 10) });

            Assert.AreEqual(2, report.ErrorCount);
            Assert.AreEqual(2, report.WarningCount);
            Assert.IsTrue(report.HasErrors);
        }

        [Test]
        public void Validator_RejectsDuplicateCheckIds()
        {
            Assert.Throws<ArgumentException>(() =>
                new BuildValidator(new IBuildCheck[]
                {
                    new MissingScriptCheck(),
                    new MissingScriptCheck(),
                }));
        }

        [Test]
        public void Validator_RejectsNullContext()
        {
            var validator = new BuildValidator(BuildCheckCatalog.CreateDefault());
            Assert.Throws<ArgumentNullException>(() => validator.Validate(null));
        }

        [Test]
        public void Validator_RejectsAnEmptyCheckList()
        {
            Assert.Throws<ArgumentException>(() => new BuildValidator(new IBuildCheck[0]));
        }

        [Test]
        public void ReportJson_ContainsCountsAndEscapesQuotes()
        {
            var report = BuildValidationReport.Create(new[]
            {
                new ValidationIssue("texture-budget", ValidationSeverity.Warning, "Say \"hi\"", "Assets/A.png"),
            });

            var json = report.ToJson();
            StringAssert.Contains("\"warningCount\": 1", json);
            StringAssert.Contains("\"errorCount\": 0", json);
            StringAssert.Contains("Say \\\"hi\\\"", json);
            StringAssert.Contains("\"assetPath\": \"Assets/A.png\"", json);
        }

        [Test]
        public void ReportJson_WritesNullAssetPath()
        {
            var report = BuildValidationReport.Create(new[]
            {
                new ValidationIssue("scene-build-list", ValidationSeverity.Error, "No scenes.", null),
            });

            StringAssert.Contains("\"assetPath\": null", report.ToJson());
        }

        [Test]
        public void Catalog_HasTheThreeBuiltInChecks()
        {
            var checks = BuildCheckCatalog.CreateDefault();

            Assert.AreEqual(3, checks.Count);
            CollectionAssert.AreEqual(
                new[] { "scene-build-list", "missing-scripts", "texture-budget" },
                new[] { checks[0].Id, checks[1].Id, checks[2].Id });
        }

        private static BuildValidationReport Validate(
            string[] enabled,
            string[] scenes,
            ScriptScan[] scans,
            TextureScan[] textures)
        {
            return new BuildValidator(BuildCheckCatalog.CreateDefault())
                .Validate(Context(enabled, scenes, scans, textures));
        }

        private static BuildValidationReport Run(IBuildCheck check, ValidationContext context)
        {
            return new BuildValidator(new IBuildCheck[] { check }).Validate(context);
        }

        private static ValidationContext Context(
            string[] enabled,
            string[] scenes,
            ScriptScan[] scans,
            TextureScan[] textures)
        {
            return Context(enabled, scenes, scans, textures, ValidationSettings.Default);
        }

        private static ValidationContext Context(
            string[] enabled,
            string[] scenes,
            ScriptScan[] scans,
            TextureScan[] textures,
            ValidationSettings settings)
        {
            return new ValidationContext(enabled, scenes, scans, textures, settings);
        }
    }
}
