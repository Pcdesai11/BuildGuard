using System;
using System.Collections.Generic;

namespace BuildGuard.Editor.Validation
{
    /// <summary>How a finding affects the build.</summary>
    public enum ValidationSeverity
    {
        /// <summary>Reported and counted. Does not fail the build.</summary>
        Warning = 0,

        /// <summary>Fails the build. The player is not produced.</summary>
        Error = 1,
    }

    /// <summary>One finding from a <see cref="IBuildCheck"/>.</summary>
    public sealed class ValidationIssue
    {
        /// <summary>Creates an issue. <paramref name="assetPath"/> may be null for project-wide findings.</summary>
        public ValidationIssue(string checkId, ValidationSeverity severity, string message, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(checkId))
            {
                throw new ArgumentException("Check id is required.", nameof(checkId));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Issue message is required.", nameof(message));
            }

            CheckId = checkId;
            Severity = severity;
            Message = message;
            AssetPath = string.IsNullOrWhiteSpace(assetPath) ? null : assetPath;
        }

        /// <summary>Id of the check that produced this issue.</summary>
        public string CheckId { get; }

        /// <summary>Warning or error.</summary>
        public ValidationSeverity Severity { get; }

        /// <summary>Sentence a developer can act on.</summary>
        public string Message { get; }

        /// <summary>Project-relative asset path, or null when the finding is not about one asset.</summary>
        public string AssetPath { get; }
    }

    /// <summary>Limits the texture check compares imported textures against.</summary>
    public sealed class ValidationSettings
    {
        /// <summary>Default budget: 2048 on each side. This is a policy default, not a measured result.</summary>
        public static readonly ValidationSettings Default = new ValidationSettings(2048, 2048);

        /// <summary>Creates a budget. Non-positive limits are preserved so the check can report them as misconfiguration.</summary>
        public ValidationSettings(int maxTextureWidth, int maxTextureHeight)
        {
            MaxTextureWidth = maxTextureWidth;
            MaxTextureHeight = maxTextureHeight;
        }

        /// <summary>Imported width at or below this value passes.</summary>
        public int MaxTextureWidth { get; }

        /// <summary>Imported height at or below this value passes.</summary>
        public int MaxTextureHeight { get; }
    }

    /// <summary>One asset scanned for missing MonoBehaviour scripts.</summary>
    public sealed class ScriptScan
    {
        /// <summary>Creates a scan result.</summary>
        public ScriptScan(string assetPath, int missingScriptCount)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new ArgumentException("Asset path is required.", nameof(assetPath));
            }

            if (missingScriptCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(missingScriptCount));
            }

            AssetPath = assetPath;
            MissingScriptCount = missingScriptCount;
        }

        /// <summary>Prefab or scene path.</summary>
        public string AssetPath { get; }

        /// <summary>Missing MonoBehaviour references on the asset, including children.</summary>
        public int MissingScriptCount { get; }
    }

    /// <summary>Imported size of one texture. Width and height are the sizes Unity will ship, not the source file.</summary>
    public sealed class TextureScan
    {
        /// <summary>Creates a texture measurement.</summary>
        public TextureScan(string assetPath, int width, int height)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new ArgumentException("Asset path is required.", nameof(assetPath));
            }

            AssetPath = assetPath;
            Width = width;
            Height = height;
        }

        /// <summary>Texture asset path.</summary>
        public string AssetPath { get; }

        /// <summary>Imported width in pixels.</summary>
        public int Width { get; }

        /// <summary>Imported height in pixels.</summary>
        public int Height { get; }
    }

    /// <summary>
    /// Snapshot the checks evaluate. The Editor window and <c>BuildScript</c> fill this from the
    /// Unity project. Tests fill it by hand, so they do not need a broken prefab in the repo.
    /// </summary>
    public sealed class ValidationContext
    {
        /// <summary>Creates a snapshot. Lists are copied.</summary>
        public ValidationContext(
            IReadOnlyList<string> enabledBuildScenes,
            IReadOnlyList<string> sceneAssetPaths,
            IReadOnlyList<ScriptScan> scriptScans,
            IReadOnlyList<TextureScan> textures,
            ValidationSettings settings)
        {
            if (enabledBuildScenes == null)
            {
                throw new ArgumentNullException(nameof(enabledBuildScenes));
            }

            if (sceneAssetPaths == null)
            {
                throw new ArgumentNullException(nameof(sceneAssetPaths));
            }

            if (scriptScans == null)
            {
                throw new ArgumentNullException(nameof(scriptScans));
            }

            if (textures == null)
            {
                throw new ArgumentNullException(nameof(textures));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            EnabledBuildScenes = Copy(enabledBuildScenes);
            SceneAssetPaths = Copy(sceneAssetPaths);
            ScriptScans = Copy(scriptScans);
            Textures = Copy(textures);
            Settings = settings;
        }

        /// <summary>Enabled scene paths from Build Settings.</summary>
        public IReadOnlyList<string> EnabledBuildScenes { get; }

        /// <summary>Scene assets found under Assets.</summary>
        public IReadOnlyList<string> SceneAssetPaths { get; }

        /// <summary>Prefabs and scenes scanned for missing scripts.</summary>
        public IReadOnlyList<ScriptScan> ScriptScans { get; }

        /// <summary>Textures measured for the size budget.</summary>
        public IReadOnlyList<TextureScan> Textures { get; }

        /// <summary>Budget and other policy values.</summary>
        public ValidationSettings Settings { get; }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
        {
            var copy = new T[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                if (source[i] == null)
                {
                    throw new ArgumentException("Snapshot lists cannot contain null entries.");
                }

                copy[i] = source[i];
            }

            return copy;
        }
    }
}
