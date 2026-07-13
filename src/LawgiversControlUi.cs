using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace LawgiversControl
{
    public sealed partial class LawgiversControlMod
    {
        private const int UiPageSize = 7;
        private const float UiContentWidth = 488f;
        private static readonly Color CheatAccent = new Color(0.92f, 0.36f, 0.16f, 1f);
        private static readonly Color CheatAccentDark = new Color(0.72f, 0.22f, 0.10f, 1f);
        private static readonly Color PanelColor = new Color(0.78f, 0.79f, 0.80f, 0.96f);
        private static readonly Color ControlColor = new Color(0.42f, 0.44f, 0.47f, 0.96f);
        private static readonly Color ControlSelectedColor = new Color(0.22f, 0.39f, 0.58f, 1f);
        private static readonly Color TextColor = new Color(0.12f, 0.13f, 0.15f, 1f);
        private GameObject _uiRoot;
        private GameObject _uiPanel;
        private RectTransform _uiContent;
        private object _uiFont;
        private object _uiFontMaterial;
        private string _uiFontSource;
        private static readonly Dictionary<IntPtr, Action> UiCallbacks = new Dictionary<IntPtr, Action>();
        private static bool _buttonHookPatched;
        private static bool _textReadyHookPatched;
        private static LawgiversControlMod _uiOwner;
        private static bool _uiCreationPending;
        private bool _uiCreating;
        private readonly List<IntPtr> _contentButtonPointers = new List<IntPtr>();
        private int _uiTab;
        private int _personPage;
        private int _partyPage;
        private int _nationPage;
        private int? _selectedPersonId;
        private int? _selectedPartyId;
        private int? _selectedNationId;
        private string _personMoneyInput = "1000000";
        private string _partyMoneyInput = "1000000";
        private string _nationMoneyInput = "1000000";
        private string _actionPointsInput = "10";
        private string _uiStatus = "패널 옆 CHEAT 탭으로 메뉴를 열고 닫을 수 있습니다.";

        partial void EnsureControlUi()
        {
            _uiOwner = this;
            try
            {
                EnsureTextReadyHook();
                if (_uiCreating)
                    return;
                if (_uiRoot != null)
                {
                    object refreshedFont = FindGameUiFont();
                    if (refreshedFont != null)
                    {
                        _uiFont = refreshedFont;
                        MelonLogger.Msg("Control UI font refreshed from: " + _uiFontSource);
                    }
                    RebuildUi();
                    return;
                }

                object discoveredFont = FindGameUiFont();
                if (discoveredFont != null)
                    _uiFont = discoveredFont;
                if (_uiFont == null)
                {
                    MelonLogger.Msg("Control UI is waiting for the game TextMeshPro font.");
                    return;
                }
                _uiCreating = true;
                EnsureButtonHook();
                _uiRoot = new GameObject("LawgiversControl.Canvas");
                UnityEngine.Object.DontDestroyOnLoad(_uiRoot);
                Canvas canvas = _uiRoot.AddComponent<Canvas>();
                Set(canvas, "renderMode", RenderMode.ScreenSpaceOverlay);
                Set(canvas, "sortingOrder", 32760);
                CanvasScaler scaler = _uiRoot.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 1f;
                _uiRoot.AddComponent<GraphicRaycaster>();

                Button toggle = CreateButton(_uiRoot.transform, "CHEAT", new Vector2(532f, -72f), new Vector2(112f, 42f), delegate
                {
                    _menuVisible = !_menuVisible;
                    _uiPanel.SetActive(_menuVisible);
                    if (_menuVisible) RebuildUi();
                }, CheatAccent);
                _uiPanel = CreateRect("Panel", _uiRoot.transform, new Vector2(12f, -72f), new Vector2(520f, 820f), new Vector2(0f, 1f));
                Image panelImage = _uiPanel.AddComponent<Image>();
                panelImage.color = PanelColor;
                Outline panelOutline = _uiPanel.AddComponent<Outline>();
                panelOutline.effectColor = new Color(0.26f, 0.27f, 0.29f, 0.85f);
                panelOutline.effectDistance = new Vector2(2f, -2f);
                _uiContent = CreateRect("Content", _uiPanel.transform, new Vector2(16f, -14f), new Vector2(UiContentWidth, 792f), new Vector2(0f, 1f)).GetComponent<RectTransform>();
                _uiPanel.SetActive(_menuVisible);
                RebuildUi();

                bool buttonCallback = false;
                Button testButton = CreateButton(_uiRoot.transform, "CallbackTest", new Vector2(-10000f, -10000f), new Vector2(1f, 1f), delegate { buttonCallback = true; }, CheatAccent);
                Invoke(testButton, "Press");
                UiCallbacks.Remove(ButtonPointer(testButton));
                UnityEngine.Object.Destroy(testButton.gameObject);

                bool eventSystemFound = FindType("UnityEngine.EventSystems.EventSystem") != null;
                string report = Path.Combine(_directory, "ui-runtime.json");
                File.WriteAllText(report, JsonConvert.SerializeObject(new
                {
                    GeneratedUtc = DateTime.UtcNow,
                    Created = true,
                    Canvas = canvas != null,
                    ToggleButton = toggle != null,
                    Panel = _uiPanel != null,
                    EventSystemFound = eventSystemFound,
                    ButtonCallback = buttonCallback,
                    FontLoaded = _uiFont != null,
                    FontName = _uiFont == null ? null : Text(Get(_uiFont, "name")),
                    FontSource = _uiFontSource,
                    FontMaterial = _uiFontMaterial != null,
                    Theme = "LawgiversGreyCheatAccent"
                }, Formatting.Indented));
                MelonLogger.Msg("Control retained UI created successfully (EventSystem=" + eventSystemFound + ").");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("Control retained UI creation failed: " + ex);
            }
            finally
            {
                _uiCreating = false;
            }
        }

        private void RebuildUi()
        {
            if (_uiContent == null)
                return;
            for (int i = _uiContent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_uiContent.GetChild(i).gameObject);
            foreach (IntPtr pointer in _contentButtonPointers)
                UiCallbacks.Remove(pointer);
            _contentButtonPointers.Clear();

            float y = 0f;
            object title = CreateText(_uiContent, "CHEAT CONTROL", new Vector2(0f, -y), new Vector2(290f, 34f), 22, TextAnchor.MiddleLeft, FontStyle.Bold);
            Set(title, "color", CheatAccentDark);
            object warning = CreateText(_uiContent, "SINGLE PLAYER MOD", new Vector2(292f, -y), new Vector2(138f, 34f), 12, TextAnchor.MiddleRight, FontStyle.Bold);
            Set(warning, "color", CheatAccentDark);
            AddButton("X", new Vector2(440f, -y), new Vector2(48f, 34f), delegate { _menuVisible = false; _uiPanel.SetActive(false); }, CheatAccentDark);
            y += 42f;
            AddRowButton("인물", 0, ref y, 0f, 88f);
            AddRowButton("정당 전체", 1, ref y, 94f, 116f);
            AddRowButton("국가", 2, ref y, 216f, 78f);
            AddRowButton("행동력", 3, ref y, 300f, 92f);
            y += 45f;
            AddStatus("!  " + _uiStatus, ref y);

            Type instanceType;
            object world;
            List<object> people;
            List<object> parties;
            List<object> nations;
            if (!TryGetWorld(out instanceType, out world, out people, out parties, out nations))
            {
                AddLabel("싱글플레이 저장 게임을 불러오면 제어 항목이 표시됩니다.", ref y, 18, FontStyle.Normal);
                return;
            }
            if (_uiTab == 0) BuildPersonUi(instanceType, people, parties, nations, ref y);
            else if (_uiTab == 1) BuildPartyUi(instanceType, people, parties, nations, ref y);
            else if (_uiTab == 2) BuildNationUi(instanceType, people, parties, nations, ref y);
            else BuildActionUi(instanceType, people, parties, nations, ref y);
        }

        private void BuildPersonUi(Type instanceType, List<object> people, List<object> parties, List<object> nations, ref float y)
        {
            AddLabel("특정 인물을 선택한 뒤 즉시 적용합니다.", ref y, 18, FontStyle.Bold);
            List<object> ordered = people.OrderBy(FriendlyName).ToList();
            AddPager(ordered.Count, ref _personPage, ref y);
            foreach (object person in ordered.Skip(_personPage * UiPageSize).Take(UiPageSize))
            {
                object captured = person;
                int id = ToInt(Get(person, "id"), -1);
                AddFullButton((_selectedPersonId == id ? "▶ " : "") + FriendlyName(person) + "  (#" + id + ")", ref y, delegate { _selectedPersonId = ToInt(Get(captured, "id"), -1); RebuildUi(); });
            }
            object selected = ordered.FirstOrDefault(p => ToInt(Get(p, "id"), -1) == _selectedPersonId);
            if (selected == null) { AddLabel("선택된 인물이 없습니다.", ref y, 18, FontStyle.Normal); return; }
            AddLabel("선택: " + FriendlyName(selected) + " | 개인 자금: " + ToLong(Get(selected, "Wealth", "wealth"), 0).ToString("N0", CultureInfo.InvariantCulture), ref y, 17, FontStyle.Normal);
            object target = selected;
            AddActionButton("MAX  모든 능력치 + 충성도 최대", ref y, delegate
            {
                int changed = MaxPersonAttributes(target);
                _uiStatus = FriendlyName(target) + ": " + changed + "개 능력치를 최대화했습니다.";
                RefreshApplyReport(instanceType, people, parties, nations); RebuildUi();
            });
            object input = AddInput(_personMoneyInput, ref y);
            AddActionButton("ADD  입력값만큼 개인 자금 추가", ref y, delegate { _personMoneyInput = Text(Get(input, "text")); RunMoney("개인 자금", _personMoneyInput, delegate(long amount, out long result) { return AddPersonMoney(target, amount, out result); }, instanceType, people, parties, nations); });
        }

        private void BuildPartyUi(Type instanceType, List<object> people, List<object> parties, List<object> nations, ref float y)
        {
            AddLabel("정당 소속 모든 인물에게 즉시 적용합니다.", ref y, 18, FontStyle.Bold);
            List<object> ordered = parties.OrderBy(FriendlyName).ToList();
            AddPager(ordered.Count, ref _partyPage, ref y);
            foreach (object party in ordered.Skip(_partyPage * UiPageSize).Take(UiPageSize))
            {
                object captured = party; int id = ToInt(Get(party, "id"), -1);
                AddFullButton((_selectedPartyId == id ? "▶ " : "") + FriendlyName(party) + "  (#" + id + ")", ref y, delegate { _selectedPartyId = ToInt(Get(captured, "id"), -1); RebuildUi(); });
            }
            object selected = ordered.FirstOrDefault(p => ToInt(Get(p, "id"), -1) == _selectedPartyId);
            if (selected == null) { AddLabel("선택된 정당이 없습니다.", ref y, 18, FontStyle.Normal); return; }
            int partyId = ToInt(Get(selected, "id"), int.MinValue);
            AddLabel("선택: " + FriendlyName(selected) + " | 소속: " + people.Count(p => ToInt(Get(p, "PartyID", "partyID"), int.MinValue) == partyId) + "명 | 자금: " + ToLong(Get(Get(selected, "money"), "value"), 0).ToString("N0", CultureInfo.InvariantCulture), ref y, 17, FontStyle.Normal);
            object target = selected;
            AddActionButton("MAX  소속 전원 능력치 + 충성도 최대", ref y, delegate
            {
                int persons; int attrs = MaxPartyMemberAttributes(target, people, out persons);
                _uiStatus = FriendlyName(target) + ": " + persons + "명 / " + attrs + "개 능력치를 최대화했습니다.";
                RefreshApplyReport(instanceType, people, parties, nations); RebuildUi();
            });
            object input = AddInput(_partyMoneyInput, ref y);
            AddActionButton("ADD  입력값만큼 정당 자금 추가", ref y, delegate { _partyMoneyInput = Text(Get(input, "text")); RunMoney("정당 자금", _partyMoneyInput, delegate(long amount, out long result) { return AddPartyMoney(target, amount, out result); }, instanceType, people, parties, nations); });
        }

        private void BuildNationUi(Type instanceType, List<object> people, List<object> parties, List<object> nations, ref float y)
        {
            AddLabel("국가를 선택하고 추가할 금액을 입력합니다.", ref y, 18, FontStyle.Bold);
            List<object> ordered = nations.OrderBy(FriendlyName).ToList();
            AddPager(ordered.Count, ref _nationPage, ref y);
            foreach (object nation in ordered.Skip(_nationPage * UiPageSize).Take(UiPageSize))
            {
                object captured = nation; int id = ToInt(Get(nation, "id"), -1);
                AddFullButton((_selectedNationId == id ? "▶ " : "") + FriendlyName(nation) + "  (#" + id + ")", ref y, delegate { _selectedNationId = ToInt(Get(captured, "id"), -1); RebuildUi(); });
            }
            object selected = ordered.FirstOrDefault(n => ToInt(Get(n, "id"), -1) == _selectedNationId);
            if (selected == null) { AddLabel("선택된 국가가 없습니다.", ref y, 18, FontStyle.Normal); return; }
            AddLabel("선택: " + FriendlyName(selected) + " | 국가 자금: " + ToLong(Get(Get(selected, "budget"), "value"), 0).ToString("N0", CultureInfo.InvariantCulture), ref y, 17, FontStyle.Normal);
            object target = selected;
            object input = AddInput(_nationMoneyInput, ref y);
            AddActionButton("ADD  입력값만큼 국가 자금 추가", ref y, delegate { _nationMoneyInput = Text(Get(input, "text")); RunMoney("국가 자금", _nationMoneyInput, delegate(long amount, out long result) { return AddNationMoney(target, amount, out result); }, instanceType, people, parties, nations); });
        }

        private void BuildActionUi(Type instanceType, List<object> people, List<object> parties, List<object> nations, ref float y)
        {
            object player = GetStatic(instanceType, "Player"); object actions = player == null ? null : Get(player, "actions");
            if (actions == null) { AddLabel("플레이어 행동력 객체를 찾지 못했습니다.", ref y, 18, FontStyle.Normal); return; }
            AddLabel("행동력 현재: " + ToInt(Get(actions, "Points", "points"), 0) + " / 최대: " + ToInt(Get(actions, "Max"), 0), ref y, 18, FontStyle.Bold);
            object input = AddInput(_actionPointsInput, ref y);
            AddActionButton("ADD  입력값만큼 행동력 추가", ref y, delegate
            {
                _actionPointsInput = Text(Get(input, "text")); int amount; int result;
                if (!int.TryParse(_actionPointsInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out amount)) _uiStatus = "행동력 입력값이 올바른 정수가 아닙니다.";
                else if (!AddActionPoints(actions, amount, out result)) _uiStatus = "행동력 추가에 실패했습니다.";
                else { _uiStatus = "행동력: " + amount + " 추가 → " + result; RefreshApplyReport(instanceType, people, parties, nations); }
                RebuildUi();
            });
        }

        private delegate bool MoneyAction(long amount, out long result);
        private void RunMoney(string label, string input, MoneyAction action, Type instanceType, List<object> people, List<object> parties, List<object> nations)
        {
            long amount; long result;
            if (!long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out amount)) _uiStatus = label + " 입력값이 올바른 정수가 아닙니다.";
            else if (!action(amount, out result)) _uiStatus = label + " 추가에 실패했습니다.";
            else { _uiStatus = label + ": " + amount.ToString("N0", CultureInfo.InvariantCulture) + " 추가 → " + result.ToString("N0", CultureInfo.InvariantCulture); RefreshApplyReport(instanceType, people, parties, nations); }
            RebuildUi();
        }

        private void AddPager(int count, ref int page, ref float y)
        {
            int max = Math.Max(0, (count - 1) / UiPageSize); page = Math.Max(0, Math.Min(page, max)); int current = page;
            AddButton("이전", new Vector2(0f, -y), new Vector2(92f, 36f), delegate { SetCurrentPage(Math.Max(0, current - 1)); RebuildUi(); });
            CreateText(_uiContent, (page + 1) + " / " + (max + 1) + "  (총 " + count + ")", new Vector2(100f, -y), new Vector2(288f, 36f), 16, TextAnchor.MiddleCenter, FontStyle.Bold);
            AddButton("다음", new Vector2(396f, -y), new Vector2(92f, 36f), delegate { SetCurrentPage(Math.Min(max, current + 1)); RebuildUi(); }); y += 42f;
        }
        private void SetCurrentPage(int page) { if (_uiTab == 0) _personPage = page; else if (_uiTab == 1) _partyPage = page; else _nationPage = page; }
        private void AddRowButton(string label, int tab, ref float y, float x, float width) { AddButton(label, new Vector2(x, -y), new Vector2(width, 37f), delegate { _uiTab = tab; RebuildUi(); }, _uiTab == tab ? CheatAccent : ControlColor); }
        private void AddFullButton(string label, ref float y, Action action) { AddButton(label, new Vector2(0f, -y), new Vector2(UiContentWidth, 37f), action, ControlSelectedColor); y += 42f; }
        private void AddActionButton(string label, ref float y, Action action) { AddButton(label, new Vector2(0f, -y), new Vector2(UiContentWidth, 40f), action, CheatAccent); y += 46f; }
        private void AddLabel(string label, ref float y, int size, FontStyle style) { CreateText(_uiContent, label, new Vector2(4f, -y), new Vector2(UiContentWidth - 8f, 32f), size, TextAnchor.MiddleLeft, style); y += 36f; }
        private void AddStatus(string label, ref float y)
        {
            GameObject bar = CreateRect("Status", _uiContent, new Vector2(0f, -y), new Vector2(UiContentWidth, 38f), new Vector2(0f, 1f));
            Image image = bar.AddComponent<Image>(); image.color = new Color(0.93f, 0.73f, 0.62f, 0.92f);
            object text = CreateText(bar.transform, label, new Vector2(10f, 0f), new Vector2(UiContentWidth - 20f, 38f), 14, TextAnchor.MiddleLeft, FontStyle.Bold);
            Set(text, "color", CheatAccentDark);
            y += 44f;
        }
        private object AddInput(string value, ref float y)
        {
            GameObject go = CreateRect("Input", _uiContent, new Vector2(0f, -y), new Vector2(UiContentWidth, 40f), new Vector2(0f, 1f));
            Image image = go.AddComponent<Image>(); image.color = new Color(0.94f, 0.94f, 0.94f, 1f);
            Outline outline = go.AddComponent<Outline>(); outline.effectColor = new Color(0.35f, 0.36f, 0.38f, 0.85f); outline.effectDistance = new Vector2(1f, -1f);
            object text = CreateText(go.transform, value, new Vector2(12f, 0f), new Vector2(UiContentWidth - 24f, 40f), 17, TextAnchor.MiddleLeft, FontStyle.Bold);
            Set(text, "color", TextColor);
            object input = AddComponent(go, FindType("TMPro.TMP_InputField"));
            if (input == null)
                throw new InvalidOperationException("TMP_InputField could not be created.");
            Set(input, "textViewport", go.GetComponent<RectTransform>());
            Set(input, "textComponent", text);
            Set(input, "text", value);
            SetEnum(input, "contentType", "IntegerNumber");
            SetEnum(input, "lineType", "SingleLine");
            y += 46f; return input;
        }
        private Button AddButton(string label, Vector2 position, Vector2 size, Action action)
        {
            return AddButton(label, position, size, action, ControlColor);
        }
        private Button AddButton(string label, Vector2 position, Vector2 size, Action action, Color color)
        {
            Button button = CreateButton(_uiContent, label, position, size, action, color);
            _contentButtonPointers.Add(ButtonPointer(button));
            return button;
        }
        private Button CreateButton(Transform parent, string label, Vector2 position, Vector2 size, Action action, Color color)
        {
            GameObject go = CreateRect("Button." + label, parent, position, size, new Vector2(0f, 1f));
            Image image = go.AddComponent<Image>(); image.color = color;
            Button button = go.AddComponent<Button>(); button.targetGraphic = image;
            object buttonText = CreateText(go.transform, label, Vector2.zero, size, 16, TextAnchor.MiddleCenter, FontStyle.Bold);
            Set(buttonText, "color", Color.white);
            object pointerValue = Get(button, "Pointer");
            if (!(pointerValue is IntPtr) || (IntPtr)pointerValue == IntPtr.Zero)
                throw new InvalidOperationException("Unity button pointer was not available.");
            UiCallbacks[(IntPtr)pointerValue] = action;
            return button;
        }
        private object CreateText(Transform parent, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor anchor, FontStyle style)
        {
            GameObject go = CreateRect("Text", parent, position, size, new Vector2(0f, 1f));
            object text = AddComponent(go, FindType("TMPro.TextMeshProUGUI"));
            if (text == null)
                throw new InvalidOperationException("TextMeshProUGUI could not be created.");
            Set(text, "font", _uiFont);
            if (_uiFontMaterial != null)
                Set(text, "fontSharedMaterial", _uiFontMaterial);
            Set(text, "text", value);
            Invoke(text, "SetText", value);
            Set(text, "fontSize", (float)fontSize);
            SetEnum(text, "fontStyle", style == FontStyle.Bold ? "Bold" : "Normal");
            SetEnum(text, "alignment", anchor == TextAnchor.MiddleCenter ? "Midline" : anchor == TextAnchor.MiddleRight ? "MidlineRight" : "MidlineLeft");
            Set(text, "color", TextColor);
            Set(text, "enabled", true);
            Invoke(text, "SetAllDirty");
            Invoke(text, "ForceMeshUpdate");
            return text;
        }
        private static GameObject CreateRect(string name, Transform parent, Vector2 position, Vector2 size, Vector2 anchor)
        {
            Type il2CppType = Type.GetType("Il2CppInterop.Runtime.Il2CppType, Il2CppInterop.Runtime", true);
            object rectTransformType = il2CppType.GetMethod("From", All, null, new[] { typeof(Type) }, null).Invoke(null, new object[] { typeof(RectTransform) });
            Type referenceArray = Type.GetType("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray`1, Il2CppInterop.Runtime", true).MakeGenericType(rectTransformType.GetType());
            object componentTypes = Activator.CreateInstance(referenceArray, new object[] { 1L });
            referenceArray.GetProperty("Item").SetValue(componentTypes, rectTransformType, new object[] { 0 });
            GameObject go = (GameObject)Activator.CreateInstance(typeof(GameObject), new object[] { name, componentTypes });
            go.transform.SetParent(parent, false); RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(0f, 1f); rect.anchoredPosition = position; rect.sizeDelta = size; return go;
        }

        private static IntPtr ButtonPointer(Button button)
        {
            object raw = Get(button, "Pointer");
            return raw is IntPtr ? (IntPtr)raw : IntPtr.Zero;
        }

        private object FindGameUiFont()
        {
            try
            {
                Type textType = FindType("TMPro.TextMeshProUGUI");
                if (textType == null)
                    return null;
                Type il2CppType = Type.GetType("Il2CppInterop.Runtime.Il2CppType, Il2CppInterop.Runtime", true);
                object nativeTextType = il2CppType.GetMethod("From", All, null, new[] { typeof(Type) }, null).Invoke(null, new object[] { textType });
                object foundTexts = InvokeStaticResult(typeof(Resources), "FindObjectsOfTypeAll", nativeTextType);
                foreach (object item in Values(foundTexts))
                {
                    object transform = Get(item, "transform");
                    object root = Get(transform, "root");
                    object rootObject = Get(root, "gameObject");
                    if (Same(Text(Get(rootObject, "name")), "LawgiversControl.Canvas"))
                        continue;
                    object font = Get(item, "font");
                    string content = Text(Get(item, "text"));
                    if (font == null || string.IsNullOrWhiteSpace(content))
                        continue;
                    _uiFontMaterial = Get(item, "fontSharedMaterial", "fontMaterial");
                    _uiFontSource = content.Length > 32 ? content.Substring(0, 32) : content;
                    return font;
                }

                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Could not enumerate game fonts: " + ex.Message);
                return null;
            }
        }

        private static object AddComponent(GameObject gameObject, Type componentType)
        {
            if (gameObject == null || componentType == null)
                return null;
            Type il2CppType = Type.GetType("Il2CppInterop.Runtime.Il2CppType, Il2CppInterop.Runtime", true);
            object nativeType = il2CppType.GetMethod("From", All, null, new[] { typeof(Type) }, null).Invoke(null, new object[] { componentType });
            return InvokeResult(gameObject, "AddComponent", nativeType);
        }

        private static bool SetEnum(object target, string propertyName, string value)
        {
            if (target == null)
                return false;
            try
            {
                PropertyInfo property = target.GetType().GetProperty(propertyName, All);
                if (property == null || !property.PropertyType.IsEnum)
                    return false;
                property.SetValue(target, Enum.Parse(property.PropertyType, value, true), null);
                return true;
            }
            catch { return false; }
        }

        private static void EnsureButtonHook()
        {
            if (_buttonHookPatched)
                return;
            var press = typeof(Button).GetMethod("Press", All);
            if (press == null)
                throw new MissingMethodException("UnityEngine.UI.Button.Press");
            _harmony.Patch(press, postfix: new HarmonyLib.HarmonyMethod(typeof(LawgiversControlMod).GetMethod("ButtonPressPostfix", All)));
            _buttonHookPatched = true;
        }

        private static void EnsureTextReadyHook()
        {
            if (_textReadyHookPatched)
                return;
            Type textType = FindType("TMPro.TextMeshProUGUI");
            MethodInfo onEnable = textType == null ? null : textType.GetMethod("OnEnable", All);
            MethodInfo rebuild = textType == null ? null : textType.GetMethod("Rebuild", All);
            Type registryType = FindType("UnityEngine.UI.CanvasUpdateRegistry");
            MethodInfo performUpdate = registryType == null ? null : registryType.GetMethod("PerformUpdate", All);
            if (onEnable == null || rebuild == null || performUpdate == null)
                throw new MissingMethodException("TMPro text or CanvasUpdateRegistry update hook");
            _harmony.Patch(onEnable, postfix: new HarmonyLib.HarmonyMethod(typeof(LawgiversControlMod).GetMethod("TextReadyPostfix", All)));
            _harmony.Patch(rebuild, postfix: new HarmonyLib.HarmonyMethod(typeof(LawgiversControlMod).GetMethod("TextReadyPostfix", All)));
            _harmony.Patch(performUpdate, postfix: new HarmonyLib.HarmonyMethod(typeof(LawgiversControlMod).GetMethod("UiUpdatePostfix", All)));
            _textReadyHookPatched = true;
        }

        private static void TextReadyPostfix(object __instance)
        {
            try
            {
                LawgiversControlMod owner = _uiOwner;
                if (owner == null || owner._uiRoot != null || owner._uiCreating)
                    return;
                object font = Get(__instance, "font");
                string content = Text(Get(__instance, "text"));
                if (font == null || string.IsNullOrWhiteSpace(content))
                    return;
                owner._uiFont = font;
                owner._uiFontMaterial = Get(__instance, "fontSharedMaterial", "fontMaterial");
                owner._uiFontSource = content.Length > 32 ? content.Substring(0, 32) : content;
                _uiCreationPending = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error("Control UI text-ready hook failed: " + ex);
            }
        }

        private static void UiUpdatePostfix()
        {
            try
            {
                LawgiversControlMod owner = _uiOwner;
                if (!_uiCreationPending || owner == null || owner._uiRoot != null || owner._uiCreating)
                    return;
                _uiCreationPending = false;
                owner.EnsureControlUi();
            }
            catch (Exception ex)
            {
                MelonLogger.Error("Control UI deferred creation failed: " + ex);
            }
        }

        private static void ButtonPressPostfix(object __instance)
        {
            try
            {
                object raw = Get(__instance, "Pointer");
                Action callback;
                if (raw is IntPtr && UiCallbacks.TryGetValue((IntPtr)raw, out callback))
                    callback();
            }
            catch (Exception ex)
            {
                MelonLogger.Error("Control button action failed: " + ex);
            }
        }
    }
}
