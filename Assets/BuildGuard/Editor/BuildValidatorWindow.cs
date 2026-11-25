using BuildGuard.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace BuildGuard.Editor
{
    /// <summary>
    /// IMGUI window for the same checks CI runs. IMGUI keeps the tool in the Editor
    /// assembly without a UI Toolkit dependency.
    /// </summary>
    public sealed class BuildValidatorWindow : EditorWindow
    {
        private BuildValidationReport _report;
        private Vector2 _scroll;

        /// <summary>Opens the window from the BuildGuard menu.</summary>
        [MenuItem("BuildGuard/Open Build Validator")]
        public static void Open()
        {
            GetWindow<BuildValidatorWindow>("Build Validator");
        }

        /// <summary>Runs validation and writes the JSON report. Used by the menu and by CI's build method.</summary>
        [MenuItem("BuildGuard/Validate Project")]
        public static void ValidateFromMenu()
        {
            var report = BuildValidationRunner.ValidateAndWriteReport();
            var summary = "BuildGuard validation: " + report.ErrorCount + " error(s), "
                + report.WarningCount + " warning(s). Report: " + BuildValidationRunner.ReportRelativePath;
            if (report.HasErrors)
            {
                Debug.LogError(summary + "\n" + report.ToJson());
            }
            else
            {
                Debug.Log(summary);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Build Validator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Errors fail the player build. Warnings are reported and do not stop it. "
                + "The same checks run from BuildScript.BuildLinux64.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Run validation"))
                {
                    _report = BuildValidationRunner.ValidateAndWriteReport();
                }

                if (GUILayout.Button("Create settings asset"))
                {
                    BuildValidationSettingsAsset.CreateDefaultAsset();
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(
                        BuildValidationSettingsAsset.DefaultAssetPath));
                }
            }

            if (_report == null)
            {
                EditorGUILayout.LabelField("No report yet. Run validation to scan the project.");
                return;
            }

            EditorGUILayout.LabelField(
                "Errors: " + _report.ErrorCount + "    Warnings: " + _report.WarningCount);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (var i = 0; i < _report.Issues.Count; i++)
            {
                var issue = _report.Issues[i];
                var prefix = issue.Severity == ValidationSeverity.Error ? "Error" : "Warning";
                EditorGUILayout.HelpBox(prefix + " [" + issue.CheckId + "] " + issue.Message, 
                    issue.Severity == ValidationSeverity.Error ? MessageType.Error : MessageType.Warning);
            }

            if (_report.Issues.Count == 0)
            {
                EditorGUILayout.LabelField("No findings.");
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
