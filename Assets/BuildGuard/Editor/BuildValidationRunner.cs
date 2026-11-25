using System.Collections.Generic;
using System.IO;
using BuildGuard.Editor.Validation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// ScriptableObject the Build Validator window edits. Missing asset means
    /// <see cref="ValidationSettings.Default"/>, so CI does not depend on the asset existing.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildValidationSettings", menuName = "BuildGuard/Validation Settings")]
    public sealed class BuildValidationSettingsAsset : ScriptableObject
    {
        /// <summary>Where <see cref="CreateDefaultAsset"/> writes the asset.</summary>
        public const string DefaultAssetPath = "Assets/BuildGuard/Editor/BuildValidationSettings.asset";

        [SerializeField] private int maxTextureWidth = 2048;
        [SerializeField] private int maxTextureHeight = 2048;

        /// <summary>Copies the serialized fields into the immutable settings the checks use.</summary>
        public ValidationSettings ToSettings()
        {
            return new ValidationSettings(maxTextureWidth, maxTextureHeight);
        }

        /// <summary>Loads the first settings asset, or the code default when none exists.</summary>
        public static ValidationSettings LoadOrDefault()
        {
            var guids = AssetDatabase.FindAssets("t:BuildValidationSettingsAsset");
            if (guids.Length == 0)
            {
                return ValidationSettings.Default;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var asset = AssetDatabase.LoadAssetAtPath<BuildValidationSettingsAsset>(path);
            return asset == null ? ValidationSettings.Default : asset.ToSettings();
        }

        /// <summary>Creates the default asset so a designer can edit the budget without code.</summary>
        public static void CreateDefaultAsset()
        {
            var directory = Path.GetDirectoryName(DefaultAssetPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var asset = CreateInstance<BuildValidationSettingsAsset>();
            AssetDatabase.CreateAsset(asset, DefaultAssetPath);
            AssetDatabase.SaveAssets();
        }
    }

    /// <summary>
    /// Reads the Unity project into a <see cref="ValidationContext"/> and runs <see cref="BuildCheckCatalog"/>.
    /// The menu, the window, and <see cref="BuildScript"/> all call this so they cannot drift apart.
    /// </summary>
    public static class BuildValidationRunner
    {
        /// <summary>Project-relative JSON report. CI uploads the BuildReports directory.</summary>
        public const string ReportRelativePath = "BuildReports/build-validation.json";

        /// <summary>Validates the open project and does not write a file.</summary>
        public static BuildValidationReport ValidateProject()
        {
            var context = Capture();
            var validator = new BuildValidator(BuildCheckCatalog.CreateDefault());
            return validator.Validate(context);
        }

        /// <summary>Validates the project and writes <see cref="ReportRelativePath"/>.</summary>
        public static BuildValidationReport ValidateAndWriteReport()
        {
            var report = ValidateProject();
            WriteReport(report);
            return report;
        }

        /// <summary>Writes <paramref name="report"/> to <see cref="ReportRelativePath"/>.</summary>
        public static void WriteReport(BuildValidationReport report)
        {
            var absolute = AbsoluteProjectPath(ReportRelativePath);
            var directory = Path.GetDirectoryName(absolute);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(absolute, report.ToJson());
        }

        /// <summary>Resolves a project-relative path from <see cref="Application.dataPath"/>, not the process working directory.</summary>
        public static string AbsoluteProjectPath(string relativePath)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, relativePath);
        }

        private static ValidationContext Capture()
        {
            var settings = BuildValidationSettingsAsset.LoadOrDefault();
            var enabled = new List<string>();
            var scenes = EditorBuildSettings.scenes;
            for (var i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].enabled)
                {
                    enabled.Add(SceneBuildListCheck.Normalize(scenes[i].path));
                }
            }

            var sceneAssets = new List<string>();
            var sceneGuids = AssetDatabase.FindAssets("t:SceneAsset", new[] { "Assets" });
            for (var i = 0; i < sceneGuids.Length; i++)
            {
                sceneAssets.Add(SceneBuildListCheck.Normalize(AssetDatabase.GUIDToAssetPath(sceneGuids[i])));
            }

            var scans = new List<ScriptScan>();
            scans.AddRange(ScanPrefabs());
            scans.AddRange(ScanScenes(enabled));

            return new ValidationContext(enabled, sceneAssets, scans, ScanTextures(), settings);
        }

        private static List<ScriptScan> ScanPrefabs()
        {
            var scans = new List<ScriptScan>();
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    scans.Add(new ScriptScan(path, CountMissing(root)));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return scans;
        }

        private static List<ScriptScan> ScanScenes(List<string> enabledScenes)
        {
            var scans = new List<ScriptScan>();
            for (var i = 0; i < enabledScenes.Count; i++)
            {
                var path = enabledScenes[i];
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    continue;
                }

                var alreadyOpen = false;
                for (var s = 0; s < SceneManager.sceneCount; s++)
                {
                    var open = SceneManager.GetSceneAt(s);
                    if (open.path == path)
                    {
                        scans.Add(new ScriptScan(path, CountMissing(open)));
                        alreadyOpen = true;
                        break;
                    }
                }

                if (alreadyOpen)
                {
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    scans.Add(new ScriptScan(path, CountMissing(scene)));
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            return scans;
        }

        private static int CountMissing(GameObject gameObject)
        {
            var count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
            var transform = gameObject.transform;
            for (var i = 0; i < transform.childCount; i++)
            {
                count += CountMissing(transform.GetChild(i).gameObject);
            }

            return count;
        }

        private static int CountMissing(Scene scene)
        {
            var count = 0;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                count += CountMissing(roots[i]);
            }

            return count;
        }

        private static List<TextureScan> ScanTextures()
        {
            var scans = new List<TextureScan>();
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                if (texture == null)
                {
                    continue;
                }

                scans.Add(new TextureScan(path, texture.width, texture.height));
            }

            return scans;
        }
    }
}
