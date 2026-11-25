using System.Collections;
using BuildGuard.Scenario;
using BuildGuard.Training;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BuildGuard.Tests.PlayMode
{
    /// <summary>
    /// The scene test proves the view is wired to the same engine the EditMode tests cover.
    /// It drives choices through the public method so it does not depend on button layout.
    /// </summary>
    public sealed class TrainingScenarioPlayModeTests
    {
        [UnityTest]
        public IEnumerator SceneLoads_AndShowsTheOpeningPrompt()
        {
            yield return LoadTrainingScene();

            var view = Object.FindAnyObjectByType<TrainingScenarioView>();
            var definition = CommunicationScenario.Create();

            Assert.IsNotNull(view);
            Assert.AreEqual(definition.GetStep(definition.StartStepId).Prompt, view.CurrentPrompt);
            Assert.IsFalse(view.IsComplete);
            Assert.IsNull(view.Result);
        }

        [UnityTest]
        public IEnumerator CompletingTheBestPath_ReportsExemplary()
        {
            yield return LoadTrainingScene();

            var view = Object.FindAnyObjectByType<TrainingScenarioView>();
            Assert.IsTrue(view.SelectChoice(CommunicationScenario.Choices.Announce));
            Assert.IsFalse(view.IsComplete);
            Assert.IsTrue(view.SelectChoice(CommunicationScenario.Choices.Acknowledge));
            Assert.IsTrue(view.SelectChoice(CommunicationScenario.Choices.Scope));
            Assert.IsTrue(view.SelectChoice(CommunicationScenario.Choices.Summarize));

            Assert.IsTrue(view.IsComplete);
            Assert.AreEqual(16, view.Result.Score);
            Assert.AreEqual(OutcomeBand.Exemplary, view.Result.Band);
            Assert.IsFalse(view.SelectChoice(CommunicationScenario.Choices.Summarize));
        }

        private static IEnumerator LoadTrainingScene()
        {
            var load = SceneManager.LoadSceneAsync("TrainingScenario", LoadSceneMode.Single);
            Assert.IsNotNull(load, "TrainingScenario is not in Build Settings.");
            while (!load.isDone)
            {
                yield return null;
            }
        }
    }
}
