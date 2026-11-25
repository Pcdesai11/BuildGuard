using System;
using System.Collections.Generic;

namespace BuildGuard.Scenario
{
    /// <summary>
    /// Immutable scenario graph: steps, the step a session starts on, and score thresholds.
    /// </summary>
    public sealed class ScenarioDefinition
    {
        private readonly Dictionary<string, ScenarioStep> _stepsById;

        private ScenarioDefinition(
            string id,
            string title,
            string startStepId,
            ScenarioStep[] steps,
            Dictionary<string, ScenarioStep> stepsById,
            int exemplaryMinimum,
            int satisfactoryMinimum)
        {
            Id = id;
            Title = title;
            StartStepId = startStepId;
            Steps = steps;
            _stepsById = stepsById;
            ExemplaryMinimum = exemplaryMinimum;
            SatisfactoryMinimum = satisfactoryMinimum;
        }

        /// <summary>Stable id for logs and reports.</summary>
        public string Id { get; }

        /// <summary>Short title shown in the training view.</summary>
        public string Title { get; }

        /// <summary>Step id passed to a new session.</summary>
        public string StartStepId { get; }

        /// <summary>Steps in authoring order.</summary>
        public IReadOnlyList<ScenarioStep> Steps { get; }

        /// <summary>Scores at or above this value are <see cref="OutcomeBand.Exemplary"/>.</summary>
        public int ExemplaryMinimum { get; }

        /// <summary>
        /// Scores at or above this value, and below <see cref="ExemplaryMinimum"/>,
        /// are <see cref="OutcomeBand.Satisfactory"/>.
        /// </summary>
        public int SatisfactoryMinimum { get; }

        /// <summary>
        /// Validates and returns a definition.
        /// Invalid graphs throw <see cref="ArgumentException"/> so a broken scenario never reaches a trainee.
        /// </summary>
        public static ScenarioDefinition Create(
            string id,
            string title,
            string startStepId,
            IReadOnlyList<ScenarioStep> steps,
            int exemplaryMinimum,
            int satisfactoryMinimum)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Scenario id is required.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Scenario title is required.", nameof(title));
            }

            if (string.IsNullOrWhiteSpace(startStepId))
            {
                throw new ArgumentException("Start step id is required.", nameof(startStepId));
            }

            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            if (steps.Count == 0)
            {
                throw new ArgumentException("A scenario needs at least one step.", nameof(steps));
            }

            if (exemplaryMinimum < satisfactoryMinimum)
            {
                throw new ArgumentException(
                    "Exemplary minimum (" + exemplaryMinimum + ") cannot be below the satisfactory minimum ("
                    + satisfactoryMinimum + ").",
                    nameof(exemplaryMinimum));
            }

            var copy = new ScenarioStep[steps.Count];
            var byId = new Dictionary<string, ScenarioStep>(StringComparer.Ordinal);
            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step == null)
                {
                    throw new ArgumentException("A scenario cannot contain a null step.", nameof(steps));
                }

                if (byId.ContainsKey(step.Id))
                {
                    throw new ArgumentException(
                        "Step id '" + step.Id + "' is duplicated.",
                        nameof(steps));
                }

                byId.Add(step.Id, step);
                copy[i] = step;
            }

            var start = startStepId.Trim();
            if (!byId.ContainsKey(start))
            {
                throw new ArgumentException(
                    "Start step '" + start + "' is not in the scenario.",
                    nameof(startStepId));
            }

            for (var i = 0; i < copy.Length; i++)
            {
                var step = copy[i];
                for (var c = 0; c < step.Choices.Count; c++)
                {
                    var next = step.Choices[c].NextStepId;
                    if (next != null && !byId.ContainsKey(next))
                    {
                        throw new ArgumentException(
                            "Choice '" + step.Choices[c].Id + "' on step '" + step.Id
                            + "' points at missing step '" + next + "'.",
                            nameof(steps));
                    }
                }
            }

            return new ScenarioDefinition(
                id.Trim(),
                title.Trim(),
                start,
                copy,
                byId,
                exemplaryMinimum,
                satisfactoryMinimum);
        }

        /// <summary>Returns the step with <paramref name="stepId"/>.</summary>
        /// <exception cref="ArgumentException">The id is not in this definition.</exception>
        public ScenarioStep GetStep(string stepId)
        {
            ScenarioStep step;
            if (stepId != null && _stepsById.TryGetValue(stepId, out step))
            {
                return step;
            }

            throw new ArgumentException(
                "Step '" + stepId + "' is not in scenario '" + Id + "'.",
                nameof(stepId));
        }

        /// <summary>Maps a score to a band using this definition's thresholds.</summary>
        public OutcomeBand Evaluate(int score)
        {
            if (score >= ExemplaryMinimum)
            {
                return OutcomeBand.Exemplary;
            }

            if (score >= SatisfactoryMinimum)
            {
                return OutcomeBand.Satisfactory;
            }

            return OutcomeBand.NeedsCoaching;
        }

        /// <summary>Builds the result a completed session exposes.</summary>
        public ScenarioResult CreateResult(int score, IReadOnlyList<DecisionRecord> decisions)
        {
            var band = Evaluate(score);
            return new ScenarioResult(score, band, decisions, SummaryFor(band));
        }

        /// <summary>Coach-facing sentence for a band. Kept on the definition so the view does not invent copy.</summary>
        public static string SummaryFor(OutcomeBand band)
        {
            switch (band)
            {
                case OutcomeBand.Exemplary:
                    return "Exemplary. The choices were clear, calm, and specific.";
                case OutcomeBand.Satisfactory:
                    return "Satisfactory. The contact was handled, with room to be more specific.";
                default:
                    return "Needs coaching. Review the choices that lost trust or skipped a next step.";
            }
        }
    }
}
