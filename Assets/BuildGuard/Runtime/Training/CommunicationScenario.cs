using System.Collections.Generic;
using BuildGuard.Scenario;

namespace BuildGuard.Training
{
    /// <summary>
    /// Sample public-safety communication scenario.
    /// The content is data. The engine does not know about welfare checks, and the tests
    /// that lock the rules use small fixtures rather than this text.
    /// </summary>
    public static class CommunicationScenario
    {
        /// <summary>Definition id used by logs and the training view.</summary>
        public const string Id = "welfare-check";

        /// <summary>Score at or above this band is exemplary. Four strong choices are 16.</summary>
        public const int ExemplaryMinimum = 12;

        /// <summary>Score at or above this band, and below exemplary, is satisfactory.</summary>
        public const int SatisfactoryMinimum = 6;

        /// <summary>Choice ids the view and PlayMode tests share, so the scene test does not hard-code strings.</summary>
        public static class Choices
        {
            /// <summary>Introduce yourself and why you are there.</summary>
            public const string Announce = "announce";

            /// <summary>Order the person to open the door.</summary>
            public const string Demand = "demand";

            /// <summary>Wait without saying anything.</summary>
            public const string Silent = "silent";

            /// <summary>Acknowledge what you heard and ask one question.</summary>
            public const string Acknowledge = "acknowledge";

            /// <summary>Threaten a citation if they do not calm down.</summary>
            public const string Threaten = "threaten";

            /// <summary>Argue that the person is wrong.</summary>
            public const string Argue = "argue";

            /// <summary>End the contact with no plan.</summary>
            public const string Disengage = "disengage";

            /// <summary>Explain what you can do and the next step.</summary>
            public const string Scope = "scope";

            /// <summary>Promise an arrest you cannot make from this contact.</summary>
            public const string Promise = "promise";

            /// <summary>Tell them to deal with it themselves.</summary>
            public const string Dismiss = "dismiss";

            /// <summary>Summarize the plan and how to follow up.</summary>
            public const string Summarize = "summarize";

            /// <summary>Leave without a summary.</summary>
            public const string Leave = "leave";

            /// <summary>Close with a warning.</summary>
            public const string Warn = "warn";
        }

        /// <summary>Builds the welfare-check scenario.</summary>
        public static ScenarioDefinition Create()
        {
            var arrival = new ScenarioStep(
                "arrival",
                "You are at the door for a welfare check. A neighbor asked for someone to look in. How do you start?",
                new List<ScenarioChoice>
                {
                    new ScenarioChoice(
                        Choices.Announce,
                        "Introduce yourself, your agency, and why you are there.",
                        4,
                        "conversation",
                        "A clear introduction tells the person who you are and why you are at their door."),
                    new ScenarioChoice(
                        Choices.Demand,
                        "Order them to open the door immediately.",
                        -2,
                        "conversation",
                        "An order, before you have explained the visit, raises fear instead of cooperation."),
                    new ScenarioChoice(
                        Choices.Silent,
                        "Wait outside without saying anything.",
                        0,
                        "conversation",
                        "Waiting avoids a threat, but the person still does not know why you are there."),
                });

            var conversation = new ScenarioStep(
                "conversation",
                "They open the door upset and talk over you about a noise complaint. How do you respond?",
                new List<ScenarioChoice>
                {
                    new ScenarioChoice(
                        Choices.Acknowledge,
                        "Acknowledge what you heard, then ask one clear question.",
                        4,
                        "request",
                        "Reflecting the complaint shows you heard it and gives the conversation a single next question."),
                    new ScenarioChoice(
                        Choices.Threaten,
                        "Tell them to calm down or you will cite them.",
                        -2,
                        "request",
                        "A threat about a citation changes the subject from help to punishment."),
                    new ScenarioChoice(
                        Choices.Argue,
                        "Explain why they are wrong about the complaint.",
                        -1,
                        "request",
                        "Correcting them before you understand the situation makes the rest of the contact harder."),
                    new ScenarioChoice(
                        Choices.Disengage,
                        "End the contact without agreeing a next step.",
                        -2,
                        null,
                        "Leaving with no plan drops the reason you were asked to check on them."),
                });

            var request = new ScenarioStep(
                "request",
                "They ask you to make the neighbor stop. What do you commit to?",
                new List<ScenarioChoice>
                {
                    new ScenarioChoice(
                        Choices.Scope,
                        "Explain what you can do today, and name the next step.",
                        4,
                        "close",
                        "Naming the limit and the next step is a promise you can keep."),
                    new ScenarioChoice(
                        Choices.Promise,
                        "Promise you will arrest the neighbor.",
                        -2,
                        "close",
                        "Promising an arrest you have not investigated gives them a commitment you may not be able to meet."),
                    new ScenarioChoice(
                        Choices.Dismiss,
                        "Tell them to deal with the neighbor themselves.",
                        -2,
                        "close",
                        "Sending them back without a next step ignores the reason for the check."),
                });

            var close = new ScenarioStep(
                "close",
                "The contact is ending. How do you close?",
                new List<ScenarioChoice>
                {
                    new ScenarioChoice(
                        Choices.Summarize,
                        "Summarize what you agreed and how they can follow up.",
                        4,
                        null,
                        "A short summary leaves them with the plan, not just the visit."),
                    new ScenarioChoice(
                        Choices.Leave,
                        "Leave without a summary.",
                        0,
                        null,
                        "The contact ends safely, but they have to remember the plan on their own."),
                    new ScenarioChoice(
                        Choices.Warn,
                        "End with a warning so they remember the visit.",
                        -1,
                        null,
                        "A warning at the end reframes a welfare check as a threat."),
                });

            return ScenarioDefinition.Create(
                Id,
                "Welfare check",
                arrival.Id,
                new List<ScenarioStep> { arrival, conversation, request, close },
                ExemplaryMinimum,
                SatisfactoryMinimum);
        }
    }
}
