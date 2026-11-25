using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BuildGuard.Editor.Validation
{
    /// <summary>Aggregated findings from one validator pass.</summary>
    public sealed class BuildValidationReport
    {
        private BuildValidationReport(IReadOnlyList<ValidationIssue> issues, int errorCount, int warningCount)
        {
            Issues = issues;
            ErrorCount = errorCount;
            WarningCount = warningCount;
        }

        /// <summary>JSON schema written by <see cref="ToJson"/>.</summary>
        public const int SchemaVersion = 1;

        /// <summary>Findings in check order.</summary>
        public IReadOnlyList<ValidationIssue> Issues { get; }

        /// <summary>Number of <see cref="ValidationSeverity.Error"/> findings.</summary>
        public int ErrorCount { get; }

        /// <summary>Number of <see cref="ValidationSeverity.Warning"/> findings.</summary>
        public int WarningCount { get; }

        /// <summary>True when <see cref="ErrorCount"/> is above zero. CI fails the build on this.</summary>
        public bool HasErrors
        {
            get { return ErrorCount > 0; }
        }

        /// <summary>Builds a report and counts severities.</summary>
        public static BuildValidationReport Create(IReadOnlyList<ValidationIssue> issues)
        {
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            var copy = new ValidationIssue[issues.Count];
            var errors = 0;
            var warnings = 0;
            for (var i = 0; i < issues.Count; i++)
            {
                if (issues[i] == null)
                {
                    throw new ArgumentException("A report cannot contain a null issue.", nameof(issues));
                }

                copy[i] = issues[i];
                if (issues[i].Severity == ValidationSeverity.Error)
                {
                    errors++;
                }
                else
                {
                    warnings++;
                }
            }

            return new BuildValidationReport(copy, errors, warnings);
        }

        /// <summary>Stable JSON for the artifact and the metrics job. Field names are part of the contract.</summary>
        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append("{\n");
            builder.Append("  \"schemaVersion\": ").Append(SchemaVersion.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            builder.Append("  \"errorCount\": ").Append(ErrorCount.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            builder.Append("  \"warningCount\": ").Append(WarningCount.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            builder.Append("  \"issues\": [\n");
            for (var i = 0; i < Issues.Count; i++)
            {
                var issue = Issues[i];
                builder.Append("    {\n");
                builder.Append("      \"checkId\": \"").Append(Escape(issue.CheckId)).Append("\",\n");
                builder.Append("      \"severity\": \"").Append(issue.Severity.ToString()).Append("\",\n");
                builder.Append("      \"message\": \"").Append(Escape(issue.Message)).Append("\",\n");
                builder.Append("      \"assetPath\": ");
                if (issue.AssetPath == null)
                {
                    builder.Append("null");
                }
                else
                {
                    builder.Append('"').Append(Escape(issue.AssetPath)).Append('"');
                }

                builder.Append("\n    }");
                if (i < Issues.Count - 1)
                {
                    builder.Append(',');
                }

                builder.Append('\n');
            }

            builder.Append("  ]\n}");
            return builder.ToString();
        }

        private static string Escape(string value)
        {
            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                switch (character)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(character);
                        break;
                }
            }

            return builder.ToString();
        }
    }

    /// <summary>Runs a list of checks and aggregates their findings.</summary>
    public sealed class BuildValidator
    {
        private readonly IReadOnlyList<IBuildCheck> _checks;

        /// <summary>Creates a validator. Check ids must be unique.</summary>
        public BuildValidator(IReadOnlyList<IBuildCheck> checks)
        {
            if (checks == null)
            {
                throw new ArgumentNullException(nameof(checks));
            }

            if (checks.Count == 0)
            {
                throw new ArgumentException("Register at least one build check.", nameof(checks));
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var copy = new IBuildCheck[checks.Count];
            for (var i = 0; i < checks.Count; i++)
            {
                var check = checks[i];
                if (check == null)
                {
                    throw new ArgumentException("A validator cannot contain a null check.", nameof(checks));
                }

                if (string.IsNullOrWhiteSpace(check.Id) || !seen.Add(check.Id))
                {
                    throw new ArgumentException(
                        "Build check id '" + check.Id + "' is missing or duplicated.",
                        nameof(checks));
                }

                copy[i] = check;
            }

            _checks = copy;
        }

        /// <summary>Checks registered on this validator, in run order.</summary>
        public IReadOnlyList<IBuildCheck> Checks
        {
            get { return _checks; }
        }

        /// <summary>Runs every check. A check that returns null is a programmer error.</summary>
        public BuildValidationReport Validate(ValidationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var issues = new List<ValidationIssue>();
            for (var i = 0; i < _checks.Count; i++)
            {
                var found = _checks[i].Evaluate(context);
                if (found == null)
                {
                    throw new InvalidOperationException("Check '" + _checks[i].Id + "' returned null.");
                }

                for (var j = 0; j < found.Count; j++)
                {
                    issues.Add(found[j]);
                }
            }

            return BuildValidationReport.Create(issues);
        }
    }

    /// <summary>The checks CI and the editor window both run. Add a new check here.</summary>
    public static class BuildCheckCatalog
    {
        /// <summary>Returns the built-in checks in report order.</summary>
        public static IReadOnlyList<IBuildCheck> CreateDefault()
        {
            return new IBuildCheck[]
            {
                new SceneBuildListCheck(),
                new MissingScriptCheck(),
                new TextureBudgetCheck(),
            };
        }
    }
}
