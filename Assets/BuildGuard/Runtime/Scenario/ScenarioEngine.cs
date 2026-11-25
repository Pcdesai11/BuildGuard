using System;
using System.Collections.Generic;

namespace BuildGuard.Scenario
{
    /// <summary>
    /// A scenario session is an immutable snapshot.
    /// Rejected choices return the same instance so a bad click cannot change the score.
    /// </summary>
    public sealed class ScenarioSession
    {
        internal ScenarioSession(
            ScenarioDefinition definition,
            string currentStepId,
            int score,
            ScenarioStatus status,
            IReadOnlyList<DecisionRecord> decisions,
            ScenarioResult result)
        {
            Definition = definition;
            CurrentStepId = currentStepId;
            Score = score;
            Status = status;
            Decisions = decisions;
            Result = result;
        }

        /// <summary>Definition this session was started from.</summary>
        public ScenarioDefinition Definition { get; }

        /// <summary>Step the trainee is on. After completion this is the last step entered.</summary>
        public string CurrentStepId { get; }

        /// <summary>Running score. May be negative. Not updated by rejected choices.</summary>
        public int Score { get; }

        /// <summary>In progress until a choice ends the scenario or a terminal step is entered.</summary>
        public ScenarioStatus Status { get; }

        /// <summary>Accepted choices, oldest first.</summary>
        public IReadOnlyList<DecisionRecord> Decisions { get; }

        /// <summary>Null until <see cref="Status"/> is <see cref="ScenarioStatus.Completed"/>.</summary>
        public ScenarioResult Result { get; }

        /// <summary>True when <see cref="Result"/> is available.</summary>
        public bool IsComplete
        {
            get { return Status == ScenarioStatus.Completed; }
        }
    }

    /// <summary>
    /// Outcome of one <see cref="IScenarioEngine.Choose"/> call.
    /// A rejected choice is a value, not an exception: the trainee can try again.
    /// </summary>
    public sealed class AdvanceResult
    {
        private AdvanceResult(ScenarioSession session, bool accepted, string message)
        {
            Session = session;
            Accepted = accepted;
            Message = message ?? string.Empty;
        }

        /// <summary>Session to keep. Unchanged when <see cref="Accepted"/> is false.</summary>
        public ScenarioSession Session { get; }

        /// <summary>True when the choice was applied.</summary>
        public bool Accepted { get; }

        /// <summary>Empty when accepted. Otherwise a sentence the view can show.</summary>
        public string Message { get; }

        internal static AdvanceResult Accept(ScenarioSession session)
        {
            return new AdvanceResult(session, true, string.Empty);
        }

        internal static AdvanceResult Reject(ScenarioSession session, string message)
        {
            return new AdvanceResult(session, false, message);
        }
    }

    /// <summary>
    /// Advances a <see cref="ScenarioSession"/>. The view depends on this, not on a concrete engine,
    /// so PlayMode tests can drive the real engine and a future host could substitute another one.
    /// </summary>
    public interface IScenarioEngine
    {
        /// <summary>Opens a session on the definition's start step.</summary>
        ScenarioSession Start(ScenarioDefinition definition);

        /// <summary>Applies <paramref name="choiceId"/> or rejects it without changing <paramref name="session"/>.</summary>
        AdvanceResult Choose(ScenarioSession session, string choiceId);
    }

    /// <summary>
    /// Pure scenario rules. This type does not reference Unity.
    /// </summary>
    public sealed class ScenarioEngine : IScenarioEngine
    {
        /// <summary>Starts a session. A terminal start step completes immediately with score 0.</summary>
        public ScenarioSession Start(ScenarioDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var step = definition.GetStep(definition.StartStepId);
            var decisions = new DecisionRecord[0];
            if (step.IsTerminal)
            {
                return new ScenarioSession(
                    definition,
                    step.Id,
                    0,
                    ScenarioStatus.Completed,
                    decisions,
                    definition.CreateResult(0, decisions));
            }

            return new ScenarioSession(
                definition,
                step.Id,
                0,
                ScenarioStatus.InProgress,
                decisions,
                null);
        }

        /// <summary>
        /// Applies a choice. Unknown ids, blank ids, and choices after completion are rejected.
        /// Programmer errors (a null session) throw.
        /// </summary>
        public AdvanceResult Choose(ScenarioSession session, string choiceId)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (session.Status != ScenarioStatus.InProgress)
            {
                return AdvanceResult.Reject(session, "The scenario is already complete.");
            }

            if (string.IsNullOrWhiteSpace(choiceId))
            {
                return AdvanceResult.Reject(session, "Choose one of the listed options.");
            }

            var step = session.Definition.GetStep(session.CurrentStepId);
            var choice = step.FindChoice(choiceId);
            if (choice == null)
            {
                return AdvanceResult.Reject(
                    session,
                    "'" + choiceId.Trim() + "' is not a choice on this step.");
            }

            var score = session.Score + choice.ScoreDelta;
            var decisions = new DecisionRecord[session.Decisions.Count + 1];
            for (var i = 0; i < session.Decisions.Count; i++)
            {
                decisions[i] = session.Decisions[i];
            }

            decisions[decisions.Length - 1] = new DecisionRecord(
                step.Id,
                choice.Id,
                choice.ScoreDelta,
                choice.Rationale);

            if (choice.NextStepId == null)
            {
                return AdvanceResult.Accept(Completed(session, step.Id, score, decisions));
            }

            var next = session.Definition.GetStep(choice.NextStepId);
            if (next.IsTerminal)
            {
                return AdvanceResult.Accept(Completed(session, next.Id, score, decisions));
            }

            return AdvanceResult.Accept(new ScenarioSession(
                session.Definition,
                next.Id,
                score,
                ScenarioStatus.InProgress,
                decisions,
                null));
        }

        private static ScenarioSession Completed(
            ScenarioSession session,
            string stepId,
            int score,
            IReadOnlyList<DecisionRecord> decisions)
        {
            return new ScenarioSession(
                session.Definition,
                stepId,
                score,
                ScenarioStatus.Completed,
                decisions,
                session.Definition.CreateResult(score, decisions));
        }
    }
}
