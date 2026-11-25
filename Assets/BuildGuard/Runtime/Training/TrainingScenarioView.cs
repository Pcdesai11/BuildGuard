using System.Collections.Generic;
using BuildGuard.Scenario;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BuildGuard.Training
{
    /// <summary>
    /// Scene-facing adapter. It draws a prompt and one button per choice, and forwards clicks
    /// to <see cref="IScenarioEngine"/>. It does not score decisions itself.
    /// </summary>
    public sealed class TrainingScenarioView : MonoBehaviour
    {
        private IScenarioEngine _engine;
        private ScenarioDefinition _definition;
        private ScenarioSession _session;
        private Text _prompt;
        private Text _status;
        private RectTransform _choiceRoot;
        private readonly List<GameObject> _choiceButtons = new List<GameObject>();
        private bool _started;

        /// <summary>Prompt of the current step, or the result summary once the scenario is complete.</summary>
        public string CurrentPrompt
        {
            get { return _session == null ? string.Empty : _session.Definition.GetStep(_session.CurrentStepId).Prompt; }
        }

        /// <summary>True after the engine reports a completed session.</summary>
        public bool IsComplete
        {
            get { return _session != null && _session.IsComplete; }
        }

        /// <summary>Null until the scenario is complete.</summary>
        public ScenarioResult Result
        {
            get { return _session == null ? null : _session.Result; }
        }

        /// <summary>Last rejection message, or the result summary when complete.</summary>
        public string StatusMessage { get; private set; }

        private void Awake()
        {
            _engine = new ScenarioEngine();
            _definition = CommunicationScenario.Create();
            _session = _engine.Start(_definition);
            StatusMessage = _definition.Title;
            BuildHud();
            Render();
            _started = true;
        }

        /// <summary>
        /// Selects a choice by id. Returns false when the engine rejects it.
        /// PlayMode tests call this instead of clicking generated buttons.
        /// </summary>
        public bool SelectChoice(string choiceId)
        {
            if (!_started || _session == null)
            {
                return false;
            }

            var advance = _engine.Choose(_session, choiceId);
            _session = advance.Session;
            StatusMessage = advance.Accepted
                ? (_session.IsComplete ? _session.Result.Summary : "Score " + _session.Score)
                : advance.Message;
            Render();
            return advance.Accepted;
        }

        private void BuildHud()
        {
            var canvasObject = new GameObject("TrainingCanvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _prompt = CreateText("Prompt", canvasObject.transform, font, 32, TextAnchor.UpperLeft);
            var promptRect = _prompt.rectTransform;
            promptRect.anchorMin = new Vector2(0.08f, 0.62f);
            promptRect.anchorMax = new Vector2(0.92f, 0.92f);
            promptRect.offsetMin = Vector2.zero;
            promptRect.offsetMax = Vector2.zero;
            _prompt.horizontalOverflow = HorizontalWrapMode.Wrap;
            _prompt.verticalOverflow = VerticalWrapMode.Overflow;

            _status = CreateText("Status", canvasObject.transform, font, 22, TextAnchor.LowerLeft);
            var statusRect = _status.rectTransform;
            statusRect.anchorMin = new Vector2(0.08f, 0.04f);
            statusRect.anchorMax = new Vector2(0.92f, 0.14f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;
            _status.color = new Color(0.78f, 0.84f, 0.9f, 1f);

            var choices = new GameObject("Choices");
            choices.transform.SetParent(canvasObject.transform, false);
            _choiceRoot = choices.AddComponent<RectTransform>();
            _choiceRoot.anchorMin = new Vector2(0.08f, 0.18f);
            _choiceRoot.anchorMax = new Vector2(0.92f, 0.58f);
            _choiceRoot.offsetMin = Vector2.zero;
            _choiceRoot.offsetMax = Vector2.zero;
            var layout = choices.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;
        }

        private void Render()
        {
            if (_prompt == null || _session == null)
            {
                return;
            }

            var step = _session.Definition.GetStep(_session.CurrentStepId);
            _prompt.text = _session.IsComplete
                ? _session.Result.Summary + "\nScore " + _session.Result.Score + " · " + _session.Result.Band
                : step.Prompt;
            _status.text = StatusMessage;

            for (var i = 0; i < _choiceButtons.Count; i++)
            {
                Destroy(_choiceButtons[i]);
            }

            _choiceButtons.Clear();
            if (_session.IsComplete)
            {
                return;
            }

            var font = _prompt.font;
            for (var i = 0; i < step.Choices.Count; i++)
            {
                var choice = step.Choices[i];
                _choiceButtons.Add(CreateChoiceButton(choice, font));
            }
        }

        private GameObject CreateChoiceButton(ScenarioChoice choice, Font font)
        {
            var buttonObject = new GameObject("Choice-" + choice.Id);
            buttonObject.transform.SetParent(_choiceRoot, false);
            var image = buttonObject.AddComponent<Image>();
            image.sprite = WhiteSprite();
            image.color = new Color(0.16f, 0.2f, 0.26f, 0.94f);
            var button = buttonObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.24f, 0.32f, 0.4f, 1f);
            colors.pressedColor = new Color(0.12f, 0.45f, 0.38f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            var label = CreateText("Label", buttonObject.transform, font, 22, TextAnchor.MiddleLeft);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(16f, 8f);
            labelRect.offsetMax = new Vector2(-16f, -8f);
            label.text = choice.Text;

            var id = choice.Id;
            button.onClick.AddListener(delegate { SelectChoice(id); });
            return buttonObject;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private static Text CreateText(string name, Transform parent, Font font, int size, TextAnchor anchor)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static Sprite WhiteSprite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
