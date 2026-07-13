using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace LawgiversControl
{
    public sealed partial class LawgiversControlMod
    {
        private const int UiPageSize = 7;
        private GameObject _uiRoot;
        private GameObject _uiPanel;
        private RectTransform _uiContent;
        private Font _uiFont;
        private static readonly Dictionary<IntPtr, Action> UiCallbacks = new Dictionary<IntPtr, Action>();
        private static bool _buttonHookPatched;
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
        private string _uiStatus = "Control 버튼으로 메뉴를 열고 닫을 수 있습니다.";

        partial void EnsureControlUi()
        {
            try
            {
                if (_uiRoot != null)
                {
                    RebuildUi();
                    return;
                }

                _uiFont = InvokeStaticResult(typeof(Font), "CreateDynamicFontFromOSFont", new[] { "Malgun Gothic", "Arial" }, 18) as Font;
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

                Button toggle = CreateButton(_uiRoot.transform, "Control", new Vector2(-92f, -40f), new Vector2(150f, 46f), delegate
                {
                    _menuVisible = !_menuVisible;
                    _uiPanel.SetActive(_menuVisible);
                    if (_menuVisible) RebuildUi();
                });
                RectTransform toggleRect = toggle.GetComponent<RectTransform>();
                toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(1f, 1f);

                _uiPanel = CreateRect("Panel", _uiRoot.transform, new Vector2(18f, -18f), new Vector2(720f, 820f), new Vector2(0f, 1f));
                Image panelImage = _uiPanel.AddComponent<Image>();
                panelImage.color = new Color(0.055f, 0.065f, 0.085f, 0.96f);
                _uiContent = CreateRect("Content", _uiPanel.transform, new Vector2(20f, -20f), new Vector2(680f, 780f), new Vector2(0f, 1f)).GetComponent<RectTransform>();
                _uiPanel.SetActive(_menuVisible);
                RebuildUi();

                bool buttonCallback = false;
                Button testButton = CreateButton(_uiRoot.transform, "CallbackTest", new Vector2(-10000f, -10000f), new Vector2(1f, 1f), delegate { buttonCallback = true; });
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
                    ButtonCallback = buttonCallback
                }, Formatting.Indented));
                MelonLogger.Msg("Control retained UI created successfully (EventSystem=" + eventSystemFound + ").");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("Control retained UI creation failed: " + ex);
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
            AddLabel("Lawgivers II Control 1.2.0", ref y, 25, FontStyle.Bold);
            AddRowButton("인물", 0, ref y);
            AddRowButton("정당 전체", 1, ref y, 130f);
            AddRowButton("국가", 2, ref y, 280f);
            AddRowButton("행동력", 3, ref y, 390f);
            AddButton("닫기", new Vector2(560f, y + 8f), new Vector2(110f, 40f), delegate { _menuVisible = false; _uiPanel.SetActive(false); });
            y += 55f;
            AddLabel("상태: " + _uiStatus, ref y, 16, FontStyle.Normal);

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
            AddFullButton("모든 능력치 + 충성도 최대", ref y, delegate
            {
                int changed = MaxPersonAttributes(target);
                _uiStatus = FriendlyName(target) + ": " + changed + "개 능력치를 최대화했습니다.";
                RefreshApplyReport(instanceType, people, parties, nations); RebuildUi();
            });
            InputField input = AddInput(_personMoneyInput, ref y);
            AddFullButton("입력값만큼 개인 자금 추가", ref y, delegate { _personMoneyInput = input.text; RunMoney("개인 자금", input.text, delegate(long amount, out long result) { return AddPersonMoney(target, amount, out result); }, instanceType, people, parties, nations); });
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
            AddFullButton("소속 모든 인물 능력치 + 충성도 최대", ref y, delegate
            {
                int persons; int attrs = MaxPartyMemberAttributes(target, people, out persons);
                _uiStatus = FriendlyName(target) + ": " + persons + "명 / " + attrs + "개 능력치를 최대화했습니다.";
                RefreshApplyReport(instanceType, people, parties, nations); RebuildUi();
            });
            InputField input = AddInput(_partyMoneyInput, ref y);
            AddFullButton("입력값만큼 정당 자금 추가", ref y, delegate { _partyMoneyInput = input.text; RunMoney("정당 자금", input.text, delegate(long amount, out long result) { return AddPartyMoney(target, amount, out result); }, instanceType, people, parties, nations); });
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
            InputField input = AddInput(_nationMoneyInput, ref y);
            AddFullButton("입력값만큼 국가 자금 추가", ref y, delegate { _nationMoneyInput = input.text; RunMoney("국가 자금", input.text, delegate(long amount, out long result) { return AddNationMoney(target, amount, out result); }, instanceType, people, parties, nations); });
        }

        private void BuildActionUi(Type instanceType, List<object> people, List<object> parties, List<object> nations, ref float y)
        {
            object player = GetStatic(instanceType, "Player"); object actions = player == null ? null : Get(player, "actions");
            if (actions == null) { AddLabel("플레이어 행동력 객체를 찾지 못했습니다.", ref y, 18, FontStyle.Normal); return; }
            AddLabel("행동력 현재: " + ToInt(Get(actions, "Points", "points"), 0) + " / 최대: " + ToInt(Get(actions, "Max"), 0), ref y, 18, FontStyle.Bold);
            InputField input = AddInput(_actionPointsInput, ref y);
            AddFullButton("입력값만큼 행동력 추가", ref y, delegate
            {
                _actionPointsInput = input.text; int amount; int result;
                if (!int.TryParse(input.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out amount)) _uiStatus = "행동력 입력값이 올바른 정수가 아닙니다.";
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
            AddButton("이전", new Vector2(0f, -y), new Vector2(110f, 38f), delegate { SetCurrentPage(Math.Max(0, current - 1)); RebuildUi(); });
            CreateText(_uiContent, (page + 1) + " / " + (max + 1) + "  (총 " + count + ")", new Vector2(125f, -y), new Vector2(370f, 38f), 17, TextAnchor.MiddleCenter, FontStyle.Normal);
            AddButton("다음", new Vector2(510f, -y), new Vector2(110f, 38f), delegate { SetCurrentPage(Math.Min(max, current + 1)); RebuildUi(); }); y += 46f;
        }
        private void SetCurrentPage(int page) { if (_uiTab == 0) _personPage = page; else if (_uiTab == 1) _partyPage = page; else _nationPage = page; }
        private void AddRowButton(string label, int tab, ref float y, float x = 0f) { AddButton(label, new Vector2(x, -y), new Vector2(105f + (tab == 1 ? 35f : 0f), 40f), delegate { _uiTab = tab; RebuildUi(); }); }
        private void AddFullButton(string label, ref float y, Action action) { AddButton(label, new Vector2(0f, -y), new Vector2(620f, 40f), action); y += 46f; }
        private void AddLabel(string label, ref float y, int size, FontStyle style) { CreateText(_uiContent, label, new Vector2(0f, -y), new Vector2(660f, 36f), size, TextAnchor.MiddleLeft, style); y += 40f; }
        private InputField AddInput(string value, ref float y)
        {
            GameObject go = CreateRect("Input", _uiContent, new Vector2(0f, -y), new Vector2(620f, 42f), new Vector2(0f, 1f));
            Image image = go.AddComponent<Image>(); image.color = new Color(0.14f, 0.16f, 0.20f, 1f);
            Text text = CreateText(go.transform, value, new Vector2(12f, 0f), new Vector2(596f, 42f), 18, TextAnchor.MiddleLeft, FontStyle.Normal);
            InputField input = go.AddComponent<InputField>(); input.textComponent = text; input.text = value; input.contentType = InputField.ContentType.IntegerNumber; input.lineType = InputField.LineType.SingleLine; y += 49f; return input;
        }
        private Button AddButton(string label, Vector2 position, Vector2 size, Action action)
        {
            Button button = CreateButton(_uiContent, label, position, size, action);
            _contentButtonPointers.Add(ButtonPointer(button));
            return button;
        }
        private Button CreateButton(Transform parent, string label, Vector2 position, Vector2 size, Action action)
        {
            GameObject go = CreateRect("Button." + label, parent, position, size, new Vector2(0f, 1f));
            Image image = go.AddComponent<Image>(); image.color = new Color(0.14f, 0.34f, 0.56f, 1f);
            Button button = go.AddComponent<Button>(); button.targetGraphic = image;
            CreateText(go.transform, label, Vector2.zero, size, 17, TextAnchor.MiddleCenter, FontStyle.Bold);
            object pointerValue = Get(button, "Pointer");
            if (!(pointerValue is IntPtr) || (IntPtr)pointerValue == IntPtr.Zero)
                throw new InvalidOperationException("Unity button pointer was not available.");
            UiCallbacks[(IntPtr)pointerValue] = action;
            return button;
        }
        private Text CreateText(Transform parent, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor anchor, FontStyle style)
        {
            GameObject go = CreateRect("Text", parent, position, size, new Vector2(0f, 1f));
            Text text = go.AddComponent<Text>();
            text.font = _uiFont;
            text.text = value; text.fontSize = fontSize; text.fontStyle = style; text.alignment = anchor; text.color = Color.white;
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
