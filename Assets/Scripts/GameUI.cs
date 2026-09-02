using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace WontFix
{
    // Builds the whole screen at runtime -- Claude can write C# but can't
    // drag things into the Inspector, so there is nothing to author by hand.
    // Dark "bug tracker" look: near-black background, terminal-green numbers.
    public class GameUI : MonoBehaviour
    {
        static readonly Color Background = new Color(0.07f, 0.07f, 0.09f);
        static readonly Color Panel = new Color(0.13f, 0.13f, 0.16f);
        static readonly Color RowAffordable = new Color(0.16f, 0.20f, 0.16f);
        static readonly Color RowLocked = new Color(0.15f, 0.15f, 0.17f);
        static readonly Color TerminalGreen = new Color(0.35f, 0.95f, 0.55f);
        static readonly Color DimText = new Color(0.55f, 0.55f, 0.58f);
        static readonly Color White = Color.white;

        Game game;
        Text bugsText;
        Text rateText;
        Text runTestLabel;
        Text languageToggleText;
        readonly List<Text> rowInfoTexts = new();
        readonly List<Text> rowCostTexts = new();
        readonly List<Button> rowButtons = new();

        Font uiFont;

        void Awake()
        {
            game = GetComponent<Game>();
            uiFont = ResolveFont();

            EnsureEventSystem();
            BuildUI();
        }

        void OnEnable()
        {
            if (game != null) game.Changed += Refresh;
            Localization.Changed += Refresh;
        }

        void OnDisable()
        {
            if (game != null) game.Changed -= Refresh;
            Localization.Changed -= Refresh;
        }

        void Start() => Refresh();

        // ponytail: OS-font lookup can silently return null on an
        // unfamiliar Linux setup; LegacyRuntime.ttf is Unity's built-in
        // fallback so the UI never ends up with a missing font.
        static Font ResolveFont()
        {
            var font = Font.CreateDynamicFontFromOSFont(
                new[] { "Liberation Mono", "DejaVu Sans Mono", "Consolas", "Courier New" }, 16);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        void BuildUI()
        {
            var canvasGO = new GameObject("UI Root", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800);
            scaler.matchWidthOrHeight = 0.5f;

            var root = canvasGO.transform;

            var background = CreatePanel(root, "Background", Background);
            Stretch(background);

            var column = CreatePanel(root, "Column", new Color(0, 0, 0, 0)).gameObject;
            Stretch(column.GetComponent<RectTransform>());
            var columnLayout = column.AddComponent<VerticalLayoutGroup>();
            columnLayout.padding = new RectOffset(24, 24, 24, 24);
            columnLayout.spacing = 12;
            columnLayout.childControlWidth = true;
            columnLayout.childControlHeight = true;
            columnLayout.childForceExpandWidth = true;
            columnLayout.childForceExpandHeight = false;

            BuildHeader(column.transform);
            BuildRunButton(column.transform);
            BuildShop(column.transform);
        }

        void BuildHeader(Transform parent)
        {
            var header = CreatePanel(parent, "Header", Panel).gameObject;
            AddFixedHeight(header, 120);

            var layout = header.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 12, 12);
            layout.spacing = 6;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            bugsText = CreateText(header.transform, "", 32, TerminalGreen, FontStyle.Bold, TextAnchor.MiddleLeft);

            var subHeader = new GameObject("SubHeader", typeof(RectTransform));
            subHeader.transform.SetParent(header.transform, false);
            AddFixedHeight(subHeader, 30);

            var subLayout = subHeader.AddComponent<HorizontalLayoutGroup>();
            subLayout.spacing = 12;
            subLayout.childControlWidth = true;
            subLayout.childControlHeight = true;
            subLayout.childForceExpandHeight = true;

            rateText = CreateText(subHeader.transform, "", 18, DimText, FontStyle.Normal, TextAnchor.MiddleLeft);
            AddFlexibleWidth(rateText.gameObject);

            var toggleGO = CreatePanel(subHeader.transform, "LanguageToggle", Panel).gameObject;
            AddFixedWidth(toggleGO, 90);
            var toggleButton = toggleGO.AddComponent<Button>();
            toggleButton.targetGraphic = toggleGO.GetComponent<Image>();
            languageToggleText = CreateText(toggleGO.transform, "", 16, TerminalGreen, FontStyle.Bold, TextAnchor.MiddleCenter);
            toggleButton.onClick.AddListener(() => Localization.SetLanguage(
                Localization.Current == Language.English ? Language.PortugueseBR : Language.English));
        }

        void BuildRunButton(Transform parent)
        {
            var buttonGO = CreatePanel(parent, "RunTestButton", new Color(0.20f, 0.45f, 0.30f)).gameObject;
            AddFixedHeight(buttonGO, 70);

            var button = buttonGO.AddComponent<Button>();
            button.targetGraphic = buttonGO.GetComponent<Image>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.25f, 0.55f, 0.36f);
            colors.pressedColor = new Color(0.15f, 0.35f, 0.24f);
            button.colors = colors;

            runTestLabel = CreateText(buttonGO.transform, "", 26, White, FontStyle.Bold, TextAnchor.MiddleCenter);
            button.onClick.AddListener(() => game.Click());
        }

        void BuildShop(Transform parent)
        {
            var scrollGO = new GameObject("ShopScroll", typeof(RectTransform));
            scrollGO.transform.SetParent(parent, false);
            AddFlexibleHeight(scrollGO);

            var scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGO.transform.SetParent(scrollGO.transform, false);
            Stretch(viewportGO.GetComponent<RectTransform>());

            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRect = contentGO.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);

            var contentLayout = contentGO.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 6;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            var fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportGO.GetComponent<RectTransform>();
            scrollRect.content = contentRect;

            for (var i = 0; i < GameData.Generators.Length; i++)
                BuildRow(contentGO.transform, i);
        }

        void BuildRow(Transform parent, int index)
        {
            var rowGO = CreatePanel(parent, $"Row{index}", RowLocked).gameObject;
            AddFixedHeight(rowGO, 64);

            var layout = rowGO.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 12;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;

            var info = CreateText(rowGO.transform, "", 16, White, FontStyle.Normal, TextAnchor.MiddleLeft);
            info.horizontalOverflow = HorizontalWrapMode.Wrap;
            AddFlexibleWidth(info.gameObject);

            var buyGO = CreatePanel(rowGO.transform, "Buy", new Color(0.20f, 0.45f, 0.30f)).gameObject;
            AddFixedWidth(buyGO, 160);
            var button = buyGO.AddComponent<Button>();
            button.targetGraphic = buyGO.GetComponent<Image>();
            var cost = CreateText(buyGO.transform, "", 16, White, FontStyle.Bold, TextAnchor.MiddleCenter);

            var capturedIndex = index;
            button.onClick.AddListener(() => game.Buy(capturedIndex));

            rowInfoTexts.Add(info);
            rowCostTexts.Add(cost);
            rowButtons.Add(button);
        }

        void Refresh()
        {
            runTestLabel.text = Localization.Get("ui.run_test");
            languageToggleText.text = Localization.Current == Language.English ? "PT-BR" : "EN";

            bugsText.text = string.Format(Localization.Get("ui.bugs_found"), Economy.Format(game.Bugs));
            rateText.text = string.Format(Localization.Get("ui.bugs_per_second"), Economy.Format(game.BugsPerSecond));

            for (var i = 0; i < GameData.Generators.Length; i++)
            {
                var gen = GameData.Generators[i];
                var owned = game.Owned[i];
                var canBuy = game.CanBuy(i);
                var name = Localization.Get($"gen.{gen.id}.name");
                var flavor = Localization.Get($"gen.{gen.id}.flavor");

                rowInfoTexts[i].text = $"{name}  (x{owned})\n{flavor}";
                rowCostTexts[i].text = Economy.Format(game.CostOf(i));
                rowButtons[i].interactable = canBuy;
                rowButtons[i].GetComponent<Image>().color =
                    canBuy ? new Color(0.20f, 0.45f, 0.30f) : new Color(0.25f, 0.25f, 0.28f);
                rowButtons[i].transform.parent.GetComponent<Image>().color =
                    canBuy ? RowAffordable : RowLocked;
            }
        }

        RectTransform CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go.GetComponent<RectTransform>();
        }

        Text CreateText(Transform parent, string text, int size, Color color, FontStyle style, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = uiFont;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = anchor;
            t.text = text;
            return t;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void AddFixedHeight(GameObject go, float height) =>
            go.AddComponent<LayoutElement>().preferredHeight = height;

        static void AddFixedWidth(GameObject go, float width) =>
            go.AddComponent<LayoutElement>().preferredWidth = width;

        static void AddFlexibleHeight(GameObject go) =>
            go.AddComponent<LayoutElement>().flexibleHeight = 1;

        static void AddFlexibleWidth(GameObject go) =>
            go.AddComponent<LayoutElement>().flexibleWidth = 1;
    }
}
