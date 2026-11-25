using System;
using System.Collections.Generic;

namespace BuildGuard.Scenario
{
    /// <summary>
    /// Coaching band derived from a completed scenario's score.
    /// </summary>
    public enum OutcomeBand
    {
        /// <summary>Score is below the satisfactory threshold.</summary>
        NeedsCoaching = 0,

        /// <summary>Score meets the satisfactory threshold and is below exemplary.</summary>
        Satisfactory = 1,

        /// <summary>Score meets the exemplary threshold.</summary>
        Exemplary = 2,
    }

    /// <summary>
    /// Whether a session is waiting for a choice or has produced a result.
    /// </summary>
    public enum ScenarioStatus
    {
        /// <summary>The current step still has choices.</summary>
        InProgress = 0,

        /// <summary>A result is available and further choices are rejected.</summary>
        Completed = 1,
    }

    /// <summary>
    /// One option presented on a <see cref="ScenarioStep"/>.
    /// </summary>
    public sealed class ScenarioChoice
    {
        /// <summary>Creates a choice. <paramref name="nextStepId"/> is null when the choice ends the scenario.</summary>
        /// <param name="id">Stable id, unique among choices on the same step.</param>
        /// <param name="text">Sentence shown to the trainee.</param>
        /// <param name="scoreDelta">Points added to the running score. May be negative.</param>
        /// <param name="nextStepId">Id of the next step, or null to complete the scenario.</param>
        /// <param name="rationale">Why this choice scores the way it does. Shown in the result.</param>
        public ScenarioChoice(string id, string text, int scoreDelta, string nextStepId, string rationale)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Choice id is required.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Choice text is required.", nameof(text));
            }

            if (nextStepId != null && string.IsNullOrWhiteSpace(nextStepId))
            {
                throw new ArgumentException(
                    "Next step id must be a step id or null. An empty string is not a valid end marker.",
                    nameof(nextStepId));
            }

            Id = id.Trim();
            Text = text.Trim();
            ScoreDelta = scoreDelta;
            NextStepId = nextStepId == null ? null : nextStepId.Trim();
            Rationale = rationale == null ? string.Empty : rationale.Trim();
        }

        /// <summary>Stable id, unique among choices on the same step.</summary>
        public string Id { get; }

        /// <summary>Sentence shown to the trainee.</summary>
        public string Text { get; }

        /// <summary>Points added to the running score. May be negative.</summary>
        public int ScoreDelta { get; }

        /// <summary>Id of the next step, or null when this choice completes the scenario.</summary>
        public string NextStepId { get; }

        /// <summary>Why this choice scores the way it does.</summary>
        public string Rationale { get; }
    }

    /// <summary>
    /// One prompt in a scenario, with zero or more choices.
    /// A step with no choices is terminal: entering it completes the scenario.
    /// </summary>
    public sealed class ScenarioStep
    {
        /// <summary>Creates a step.</summary>
        /// <param name="id">Stable id, unique within the scenario.</param>
        /// <param name="prompt">The situation text.</param>
        /// <param name="choices">Choices on this step. An empty list marks a terminal step.</param>
        public ScenarioStep(string id, string prompt, IReadOnlyList<ScenarioChoice> choices)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Step id is required.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new ArgumentException("Step prompt is required.", nameof(prompt));
            }

            if (choices == null)
            {
                throw new ArgumentNullException(nameof(choices));
            }

            var copy = new ScenarioChoice[choices.Count];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                if (choice == null)
                {
                    throw new ArgumentException("A step cannot contain a null choice.", nameof(choices));
                }

                if (!seen.Add(choice.Id))
                {
                    throw new ArgumentException(
                        "Choice id '" + choice.Id + "' is duplicated on step '" + id.Trim() + "'.",
                        nameof(choices));
                }

                copy[i] = choice;
            }

            Id = id.Trim();
            Prompt = prompt.Trim();
            Choices = copy;
        }

        /// <summary>Stable id, unique within the scenario.</summary>
        public string Id { get; }

        /// <summary>The situation text.</summary>
        public string Prompt { get; }

        /// <summary>Choices on this step. Empty when the step is terminal.</summary>
        public IReadOnlyList<ScenarioChoice> Choices { get; }

        /// <summary>True when entering this step completes the scenario.</summary>
        public bool IsTerminal
        {
            get { return Choices.Count == 0; }
        }

        /// <summary>Returns the choice with <paramref name="choiceId"/>, or null when it is not on this step.</summary>
        public ScenarioChoice FindChoice(string choiceId)
        {
            if (string.IsNullOrWhiteSpace(choiceId))
            {
                return null;
            }

            for (var i = 0; i < Choices.Count; i++)
            {
                if (string.Equals(Choices[i].Id, choiceId, StringComparison.Ordinal))
                {
                    return Choices[i];
                }
            }

            return null;
        }
    }

    /// <summary>
    /// One accepted choice, kept in order.
    /// </summary>
    public sealed class DecisionRecord
    {
        /// <summary>Creates a record of an accepted choice.</summary>
        public DecisionRecord(string stepId, string choiceId, int scoreDelta, string rationale)
        {
            StepId = stepId;
            ChoiceId = choiceId;
            ScoreDelta = scoreDelta;
            Rationale = rationale ?? string.Empty;
        }

        /// <summary>Step the choice was made on.</summary>
        public string StepId { get; }

        /// <summary>Choice the trainee selected.</summary>
        public string ChoiceId { get; }

        /// <summary>Points applied for this choice.</summary>
        public int ScoreDelta { get; }

        /// <summary>Rationale copied from the choice at the time it was selected.</summary>
        public string Rationale { get; }
    }

    /// <summary>
    /// Score, band, and decision list for a completed scenario.
    /// </summary>
    public sealed class ScenarioResult
    {
        /// <summary>Creates a result. Callers should use <see cref="ScenarioDefinition.Evaluate"/>.</summary>
        public ScenarioResult(int score, OutcomeBand band, IReadOnlyList<DecisionRecord> decisions, string summary)
        {
            if (decisions == null)
            {
                throw new ArgumentNullException(nameof(decisions));
            }

            Score = score;
            Band = band;
            Decisions = decisions;
            Summary = summary ?? string.Empty;
        }

        /// <summary>Sum of choice score deltas. Not clamped.</summary>
        public int Score { get; }

        /// <summary>Band for <see cref="Score"/> under the scenario's thresholds.</summary>
        public OutcomeBand Band { get; }

        /// <summary>Accepted choices, in the order they were made.</summary>
        public IReadOnlyList<DecisionRecord> Decisions { get; }

        /// <summary>One sentence a coach can read without opening the decision list.</summary>
        public string Summary { get; }
    }
}
