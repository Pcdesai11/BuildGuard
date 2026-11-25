using System;
using System.Collections.Generic;
using BuildGuard.Scenario;
using NUnit.Framework;

namespace BuildGuard.Tests.EditMode
{
    /// <summary>
    /// The definition rejects graphs that would strand a trainee. Those failures happen at
    /// authoring time, so the engine can assume every next-step id exists.
    /// </summary>
    public sealed class ScenarioDefinitionTests
    {
        [Test]
        public void Create_AcceptsAValidGraph()
        {
            var definition = ScenarioFixtures.TwoStep();

            Assert.AreEqual("fixture", definition.Id);
            Assert.AreEqual("start", definition.StartStepId);
            Assert.AreEqual(2, definition.Steps.Count);
            Assert.AreSame(definition.Steps[0], definition.GetStep("start"));
        }

        [Test]
        public void Create_RejectsEmptyId()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                ScenarioDefinition.Create(
                    "  ",
                    "Title",
                    "start",
                    new List<ScenarioStep> { ScenarioFixtures.Terminal("start") },
                    1,
                    0));

            StringAssert.Contains("id", ex.Message);
        }

        [Test]
        public void Create_RejectsNoSteps()
        {
            Assert.Throws<ArgumentException>(() =>
                ScenarioDefinition.Create("id", "Title", "start", new List<ScenarioStep>(), 1, 0));
        }

        [Test]
        public void Create_RejectsNullStep()
        {
            Assert.Throws<ArgumentException>(() =>
                ScenarioDefinition.Create(
                    "id",
                    "Title",
                    "start",
                    new List<ScenarioStep> { null },
                    1,
                    0));
        }

        [Test]
        public void Create_RejectsDuplicateStepIds()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                ScenarioDefinition.Create(
                    "id",
                    "Title",
                    "start",
                    new List<ScenarioStep>
                    {
                        ScenarioFixtures.Terminal("start"),
                        ScenarioFixtures.Terminal("start"),
                    },
                    1,
                    0));

            StringAssert.Contains("duplicated", ex.Message);
        }

        [Test]
        public void Create_RejectsDuplicateChoiceIdsOnTheSameStep()
        {
            Assert.Throws<ArgumentException>(() =>
                new ScenarioStep(
                    "start",
                    "Prompt",
                    new List<ScenarioChoice>
                    {
                        new ScenarioChoice("same", "One", 1, null, "a"),
                        new ScenarioChoice("same", "Two", 1, null, "b"),
                    }));
        }

        [Test]
        public void Create_AllowsTheSameChoiceIdOnDifferentSteps()
        {
            var first = new ScenarioStep(
                "start",
                "First",
                new List<ScenarioChoice> { new ScenarioChoice("go", "Go", 1, "next", "a") });
            var second = new ScenarioStep(
                "next",
                "Second",
                new List<ScenarioChoice> { new ScenarioChoice("go", "Go again", 1, null, "b") });

            var definition = ScenarioDefinition.Create(
                "id",
                "Title",
                "start",
                new List<ScenarioStep> { first, second },
                2,
                1);

            Assert.AreEqual("go", definition.GetStep("next").Choices[0].Id);
        }

        [Test]
        public void Create_RejectsUnknownStartStep()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                ScenarioDefinition.Create(
                    "id",
                    "Title",
                    "missing",
                    new List<ScenarioStep> { ScenarioFixtures.Terminal("start") },
                    1,
                    0));

            StringAssert.Contains("missing", ex.Message);
        }

        [Test]
        public void Create_RejectsUnknownNextStep()
        {
            var step = new ScenarioStep(
                "start",
                "Prompt",
                new List<ScenarioChoice> { new ScenarioChoice("go", "Go", 1, "nowhere", "a") });

            var ex = Assert.Throws<ArgumentException>(() =>
                ScenarioDefinition.Create("id", "Title", "start", new List<ScenarioStep> { step }, 1, 0));

            StringAssert.Contains("nowhere", ex.Message);
        }

        [Test]
        public void Create_RejectsEmptyNextStepId()
        {
            Assert.Throws<ArgumentException>(() =>
                new ScenarioChoice("go", "Go", 1, "  ", "a"));
        }

        [Test]
        public void Create_RejectsExemplaryThresholdBelowSatisfactory()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                ScenarioDefinition.Create(
                    "id",
                    "Title",
                    "start",
                    new List<ScenarioStep> { ScenarioFixtures.Terminal("start") },
                    exemplaryMinimum: 1,
                    satisfactoryMinimum: 5));

            StringAssert.Contains("Exemplary", ex.Message);
        }

        [Test]
        public void Evaluate_UsesThresholdBoundaries()
        {
            var definition = ScenarioFixtures.TwoStep();

            Assert.AreEqual(OutcomeBand.NeedsCoaching, definition.Evaluate(definition.SatisfactoryMinimum - 1));
            Assert.AreEqual(OutcomeBand.Satisfactory, definition.Evaluate(definition.SatisfactoryMinimum));
            Assert.AreEqual(OutcomeBand.Exemplary, definition.Evaluate(definition.ExemplaryMinimum));
        }
    }
}
