using System;
using System.Collections.Generic;

namespace BuildGuard.Editor.Validation
{
    /// <summary>
    /// One build-health rule. Implement this and register it in <see cref="BuildCheckCatalog"/>
    /// to extend the validator without changing the window or the CI build method.
    /// </summary>
    public interface IBuildCheck
    {
        /// <summary>Stable id written into the JSON report.</summary>
        string Id { get; }

        /// <summary>One sentence shown in the Build Validator window.</summary>
        string Description { get; }

        /// <summary>Returns findings for <paramref name="context"/>. An empty list means the check passed.</summary>
        IReadOnlyList<ValidationIssue> Evaluate(ValidationContext context);
    }

    /// <summary>
    /// Scenes enabled in Build Settings must exist, and the enabled list must not be empty.
    /// Scene assets that are not enabled are warnings: test scenes and sandboxes are legitimate.
    /// </summary>
    public sealed class SceneBuildListCheck : IBuildCheck
    {
        /// <summary>Report id.</summary>
        public string Id
        {
            get { return "scene-build-list"; }
        }

        /// <summary>Window description.</summary>
        public string Description
        {
            get { return "Enabled Build Settings scenes exist, and the player is not given an empty scene list."; }
        }

        /// <summary>Evaluates the scene lists on <paramref name="context"/>.</summary>
        public IReadOnlyList<ValidationIssue> Evaluate(ValidationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var issues = new List<ValidationIssue>();
            var assets = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < context.SceneAssetPaths.Count; i++)
            {
                assets.Add(Normalize(context.SceneAssetPaths[i]));
            }

            var enabledCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < context.EnabledBuildScenes.Count; i++)
            {
                var path = Normalize(context.EnabledBuildScenes[i]);
                int count;
                enabledCounts.TryGetValue(path, out count);
                enabledCounts[path] = count + 1;
                if (!assets.Contains(path))
                {
                    issues.Add(new ValidationIssue(
                        Id,
                        ValidationSeverity.Error,
                        "Build Settings enables '" + path + "', but that scene file is not in the project.",
                        path));
                }
            }

            foreach (var pair in enabledCounts)
            {
                if (pair.Value > 1)
                {
                    issues.Add(new ValidationIssue(
                        Id,
                        ValidationSeverity.Warning,
                        "Scene '" + pair.Key + "' is enabled " + pair.Value + " times in Build Settings.",
                        pair.Key));
                }
            }

            if (context.EnabledBuildScenes.Count == 0)
            {
                issues.Add(new ValidationIssue(
                    Id,
                    ValidationSeverity.Error,
                    "No scenes are enabled in Build Settings, so the player would be empty.",
                    null));
            }

            for (var i = 0; i < context.SceneAssetPaths.Count; i++)
            {
                var path = Normalize(context.SceneAssetPaths[i]);
                if (!enabledCounts.ContainsKey(path))
                {
                    issues.Add(new ValidationIssue(
                        Id,
                        ValidationSeverity.Warning,
                        "Scene '" + path + "' is not enabled in Build Settings.",
                        path));
                }
            }

            return issues;
        }

        internal static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path.Trim().Replace('\\', '/');
        }
    }

    /// <summary>
    /// Missing MonoBehaviour references ship as null components. That is an error:
    /// the object looks present in the editor and does nothing in the player.
    /// </summary>
    public sealed class MissingScriptCheck : IBuildCheck
    {
        /// <summary>Report id.</summary>
        public string Id
        {
            get { return "missing-scripts"; }
        }

        /// <summary>Window description.</summary>
        public string Description
        {
            get { return "Prefabs and enabled scenes have no missing script references."; }
        }

        /// <summary>Emits one error per asset whose missing-script count is above zero.</summary>
        public IReadOnlyList<ValidationIssue> Evaluate(ValidationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var issues = new List<ValidationIssue>();
            for (var i = 0; i < context.ScriptScans.Count; i++)
            {
                var scan = context.ScriptScans[i];
                if (scan.MissingScriptCount > 0)
                {
                    issues.Add(new ValidationIssue(
                        Id,
                        ValidationSeverity.Error,
                        scan.AssetPath + " has " + scan.MissingScriptCount + " missing script reference(s).",
                        scan.AssetPath));
                }
            }

            return issues;
        }
    }

    /// <summary>
    /// Compares imported texture size with <see cref="ValidationSettings"/>.
    /// Over-budget textures are warnings: they are a content-policy miss, not a broken player.
    /// A non-positive budget is an error, because the check would otherwise pass everything.
    /// </summary>
    public sealed class TextureBudgetCheck : IBuildCheck
    {
        /// <summary>Report id.</summary>
        public string Id
        {
            get { return "texture-budget"; }
        }

        /// <summary>Window description.</summary>
        public string Description
        {
            get { return "Imported textures are within the configured width and height budget."; }
        }

        /// <summary>Warns when either imported dimension is strictly greater than the budget.</summary>
        public IReadOnlyList<ValidationIssue> Evaluate(ValidationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var settings = context.Settings;
            if (settings.MaxTextureWidth <= 0 || settings.MaxTextureHeight <= 0)
            {
                return new List<ValidationIssue>
                {
                    new ValidationIssue(
                        Id,
                        ValidationSeverity.Error,
                        "Texture budget must be a positive width and height. Configured "
                        + settings.MaxTextureWidth + "x" + settings.MaxTextureHeight + ".",
                        null),
                };
            }

            var issues = new List<ValidationIssue>();
            for (var i = 0; i < context.Textures.Count; i++)
            {
                var texture = context.Textures[i];
                if (texture.Width > settings.MaxTextureWidth || texture.Height > settings.MaxTextureHeight)
                {
                    issues.Add(new ValidationIssue(
                        Id,
                        ValidationSeverity.Warning,
                        texture.AssetPath + " is " + texture.Width + "x" + texture.Height
                        + ", over the " + settings.MaxTextureWidth + "x" + settings.MaxTextureHeight + " budget.",
                        texture.AssetPath));
                }
            }

            return issues;
        }
    }
}
