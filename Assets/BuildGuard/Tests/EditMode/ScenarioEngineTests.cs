using System;
using System.Collections.Generic;
using BuildGuard.Scenario;
using BuildGuard.Training;
using NUnit.Framework;

namespace BuildGuard.Tests.EditMode
{
    /// <summary>
    /// Engine rules, including the edges a trainee can actually hit: unknown buttons,
    /// choices after the scenario ended, negative scores, and a step that loops.
    /// </summary>
    public sealed class ScenarioEngineTests
    {
        private readonly ScenarioEngine _engine = new ScenarioEngine();

        [Test]
        public void Start_BeginsAtTheStartStepWithScoreZero()
        {
            var session = _engine.Start(ScenarioFixtures.TwoStep());

            Assert.AreEqual("start", session.CurrentStepId);
            Assert.AreEqual(0, session.Score);
            Assert.AreEqual(ScenarioStatus.InProgress, session.Status);
            Assert.IsNull(session.Result);
            Assert.AreEqual(0, session.Decisions.Count);
        }

        [Test]
        public void Start_OnATerminalStep_CompletesImmediately()
        {
            var definition = ScenarioDefinition.Create(
                "id",
                "Title",
                "done",
                new List<ScenarioStep> { ScenarioFixtures.Terminal("done") },
                1,
                0);

            var session = _engine.Start(definition);

            Assert.IsTrue(session.IsComplete);
            Assert.AreEqual(0, session.Score);
            Assert.AreEqual(OutcomeBand.Satisfactory, session.Result.Band);
        }

        [Test]
        public void Start_RejectsNullDefinition()
        {
            Assert.Throws<ArgumentNullException>(() => _engine.Start(null));
        }

        [Test]
        public void Choose_HighScorePath_IsExemplary()
        {
            var session = Finish(ScenarioFixtures.TwoStep(), "good", "good");

            Assert.AreEqual(4, session.Score);
            Assert.AreEqual(OutcomeBand.Exemplary, session.Result.Band);
            Assert.AreEqual(2, session.Result.Decisions.Count);
        }

        [Test]
        public void Choose_MiddleScorePath_IsSatisfactory()
        {
            var session = Finish(ScenarioFixtures.TwoStep(), "mid", "mid");

            Assert.AreEqual(2, session.Score);
            Assert.AreEqual(OutcomeBand.Satisfactory, session.Result.Band);
        }

        [Test]
        public void Choose_LowScorePath_NeedsCoaching()
        {
            var session = Finish(ScenarioFixtures.TwoStep(), "poor", "poor");

            Assert.AreEqual(-2, session.Score);
            Assert.AreEqual(OutcomeBand.NeedsCoaching, session.Result.Band);
        }

        [Test]
        public void Choose_UnknownId_IsRejectedAndKeepsTheSession()
        {
            var session = _engine.Start(ScenarioFixtures.TwoStep());

            var result = _engine.Choose(session, "not-a-choice");

            Assert.IsFalse(result.Accepted);
            Assert.AreSame(session, result.Session);
            Assert.AreEqual(0, result.Session.Score);
            Assert.AreEqual("start", result.Session.CurrentStepId);
            StringAssert.Contains("not-a-choice", result.Message);
        }

        [Test]
        public void Choose_BlankId_IsRejected()
        {
            var session = _engine.Start(ScenarioFixtures.TwoStep());

            var blank = _engine.Choose(session, "   ");
            var missing = _engine.Choose(session, null);

            Assert.IsFalse(blank.Accepted);
            Assert.IsFalse(missing.Accepted);
            Assert.AreSame(session, blank.Session);
            Assert.AreSame(session, missing.Session);
        }

        [Test]
        public void Choose_WrongCase_IsRejected()
        {
            var session = _engine.Start(ScenarioFixtures.TwoStep());

            var result = _engine.Choose(session, "GOOD");

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(0, session.Score);
        }

        [Test]
        public void Choose_AfterCompletion_IsRejectedAndKeepsTheResult()
        {
            var session = Finish(ScenarioFixtures.TwoStep(), "good", "good");
            var score = session.Score;

            var result = _engine.Choose(session, "good");

            Assert.IsFalse(result.Accepted);
            Assert.AreSame(session, result.Session);
            Assert.AreEqual(score, result.Session.Score);
            Assert.AreEqual(2, result.Session.Decisions.Count);
        }

        [Test]
        public void Choose_NegativeScore_StillCompletes()
        {
            var session = Finish(ScenarioFixtures.TwoStep(), "poor", "poor");

            Assert.Less(session.Score, 0);
            Assert.IsTrue(session.IsComplete);
            Assert.AreEqual(OutcomeBand.NeedsCoaching, session.Result.Band);
        }

        [Test]
        public void Choose_RevisitsAStep_AndAccumulatesBothDecisions()
        {
            var definition = ScenarioFixtures.Loop();
            var session = _engine.Start(definition);

            var first = _engine.Choose(session, "again");
            var second = _engine.Choose(first.Session, "leave");

            Assert.IsTrue(first.Accepted);
            Assert.AreEqual("start", first.Session.CurrentStepId);
            Assert.AreEqual(1, first.Session.Score);
            Assert.IsTrue(second.Session.IsComplete);
            Assert.AreEqual(2, second.Session.Score);
            Assert.AreEqual(2, second.Session.Decisions.Count);
            Assert.AreEqual("again", second.Session.Decisions[0].ChoiceId);
            Assert.AreEqual("leave", second.Session.Decisions[1].ChoiceId);
        }

        [Test]
        public void Choose_NullNextStep_CompletesWithoutAnotherStep()
        {
            var session = _engine.Start(ScenarioFixtures.TwoStep());

            var result = _engine.Choose(session, "stop");

            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(result.Session.IsComplete);
            Assert.AreEqual("start", result.Session.CurrentStepId);
            Assert.AreEqual(1, result.Session.Decisions.Count);
        }

        [Test]
        public void Choose_EnteringAStepWithNoChoices_Completes()
        {
            var start = new ScenarioStep(
                "start",
                "Go",
                new List<ScenarioChoice> { new ScenarioChoice("go", "Go", 3, "done", "moved") });
            var definition = ScenarioDefinition.Create(
                "id",
                "Title",
                "start",
                new List<ScenarioStep> { start, ScenarioFixtures.Terminal("done") },
                3,
                1);

            var result = _engine.Choose(_engine.Start(definition), "go");

            Assert.IsTrue(result.Session.IsComplete);
            Assert.AreEqual("done", result.Session.CurrentStepId);
            Assert.AreEqual(3, result.Session.Score);
            Assert.AreEqual(OutcomeBand.Exemplary, result.Session.Result.Band);
        }

        [Test]
        public void Choose_NullSession_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _engine.Choose(null, "good"));
        }

        [Test]
        public void Choose_DoesNotMutateThePreviousSnapshot()
        {
            var session = _engine.Start(ScenarioFixtures.TwoStep());

            var result = _engine.Choose(session, "good");

            Assert.AreEqual(0, session.Score);
            Assert.AreEqual(ScenarioStatus.InProgress, session.Status);
            Assert.AreEqual(2, result.Session.Score);
            Assert.AreEqual("next", result.Session.CurrentStepId);
        }

        [Test]
        public void SampleBestPath_IsExemplary()
        {
            var session = Finish(
                CommunicationScenario.Create(),
                CommunicationScenario.Choices.Announce,
                CommunicationScenario.Choices.Acknowledge,
                CommunicationScenario.Choices.Scope,
                CommunicationScenario.Choices.Summarize);

            Assert.AreEqual(16, session.Score);
            Assert.AreEqual(OutcomeBand.Exemplary, session.Result.Band);
            Assert.AreEqual(4, session.Result.Decisions.Count);
        }

        [Test]
        public void SamplePoorPath_NeedsCoaching()
        {
            var session = Finish(
                CommunicationScenario.Create(),
                CommunicationScenario.Choices.Demand,
                CommunicationScenario.Choices.Threaten,
                CommunicationScenario.Choices.Dismiss,
                CommunicationScenario.Choices.Warn);

            Assert.AreEqual(-7, session.Score);
            Assert.AreEqual(OutcomeBand.NeedsCoaching, session.Result.Band);
        }

        [Test]
        public void SampleEarlyExit_CompletesOnTheSecondStep()
        {
            var session = Finish(
                CommunicationScenario.Create(),
                CommunicationScenario.Choices.Silent,
                CommunicationScenario.Choices.Disengage);

            Assert.IsTrue(session.IsComplete);
            Assert.AreEqual("conversation", session.CurrentStepId);
            Assert.AreEqual(-2, session.Score);
            Assert.AreEqual(2, session.Decisions.Count);
        }

        private ScenarioSession Finish(ScenarioDefinition definition, params string[] choiceIds)
        {
            var session = _engine.Start(definition);
            for (var i = 0; i < choiceIds.Length; i++)
            {
                var advance = _engine.Choose(session, choiceIds[i]);
                Assert.IsTrue(advance.Accepted, advance.Message);
                session = advance.Session;
            }

            Assert.IsTrue(session.IsComplete);
            return session;
        }
    }

    /// <summary>Small graphs shared by the definition and engine tests.</summary>
    internal static class ScenarioFixtures
    {
        public static ScenarioStep Terminal(string id)
        {
            return new ScenarioStep(id, "Done", new List<ScenarioChoice>());
        }

        /// <summary>
        /// Start step with good (+2), mid (+1), poor (-1), and stop (+1, ends).
        /// Next step repeats good/mid/poor and then ends. Thresholds: exemplary 4, satisfactory 2.
        /// </summary>
        public static ScenarioDefinition TwoStep()
        {
            var start = new ScenarioStep(
                "start",
                "Start",
                new List<ScenarioChoice>
                {
                    new ScenarioChoice("good", "Good", 2, "next", "strong"),
                    new ScenarioChoice("mid", "Mid", 1, "next", "ok"),
                    new ScenarioChoice("poor", "Poor", -1, "next", "weak"),
                    new ScenarioChoice("stop", "Stop", 1, null, "leave"),
                });
            var next = new ScenarioStep(
                "next",
                "Next",
                new List<ScenarioChoice>
                {
                    new ScenarioChoice("good", "Good", 2, null, "strong"),
                    new ScenarioChoice("mid", "Mid", 1, null, "ok"),
                    new ScenarioChoice("poor", "Poor", -1, null, "weak"),
                });
            return ScenarioDefinition.Create(
                "fixture",
                "Fixture",
                "start",
                new List<ScenarioStep> { start, next },
                exemplaryMinimum: 4,
                satisfactoryMinimum: 2);
        }

        public static ScenarioDefinition Loop()
        {
            var start = new ScenarioStep(
                "start",
                "Loop",
                new List<ScenarioChoice>
                {
                    new ScenarioChoice("again", "Again", 1, "start", "repeat"),
                    new ScenarioChoice("leave", "Leave", 1, null, "done"),
                });
            return ScenarioDefinition.Create(
                "loop",
                "Loop",
                "start",
                new List<ScenarioStep> { start },
                exemplaryMinimum: 5,
                satisfactoryMinimum: 1);
        }
    }
}
