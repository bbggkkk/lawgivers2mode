using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace LawgiversControl
{
    public sealed partial class LawgiversControlMod
    {
        private static readonly Color IntegratedButtonColor = new Color(0.32f, 0.33f, 0.35f, 0.98f);
        private static readonly Color IntegratedAccentColor = new Color(0.92f, 0.36f, 0.16f, 1f);
        private static readonly Dictionary<IntPtr, Action> IntegratedCallbacks = new Dictionary<IntPtr, Action>();
        private static LawgiversControlMod _uiOwner;
        private static bool _integratedHooksPatched;
        private static bool _buttonHookPatched;
        private static object _partyMembersOpening;
        private static object _partyMembersContext;
        private static object _partyMemberList;
        private object _uiFont;
        private object _uiFontMaterial;

        partial void EnsureControlUi()
        {
            _uiOwner = this;
            if (_integratedHooksPatched || _harmony == null)
                return;

            try
            {
                EnsureButtonHook();
                Type personAttributes = FindType("Il2CppLawgivers.Interface.UIPersonAttributes");
                Type partyMembers = FindType("Il2CppLawgivers.Interface.UIPartyMembers");
                Type uiList = FindType("Il2CppLawgivers.Interface.UIList");
                if (personAttributes == null || partyMembers == null || uiList == null)
                    throw new TypeLoadException("Required Lawgivers UI types are not loaded yet.");

                PatchPostfix(personAttributes, "RefreshAlways", "PersonAttributesRefreshPostfix");
                PatchPrefix(partyMembers, "ButtonPeople", "PartyMembersButtonPrefix");
                PatchPostfix(partyMembers, "ButtonPeople", "PartyMembersButtonPostfix");
                PatchPostfix(uiList, "RefreshAlways", "UiListRefreshPostfix");
                foreach (MethodInfo open in uiList.GetMethods(All).Where(m => m.Name == "Open" && m.DeclaringType == uiList))
                    _harmony.Patch(open, postfix: new HarmonyMethod(typeof(LawgiversControlMod).GetMethod("UiListOpenPostfix", All)));

                _integratedHooksPatched = true;
                WriteIntegratedUiReport("hooks-installed", null);
                MelonLogger.Msg("Context-integrated person and party controls enabled.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Context UI hooks are not ready: " + ex.Message);
            }
        }

        private static void PatchPrefix(Type type, string methodName, string patchName)
        {
            MethodInfo method = type.GetMethod(methodName, All);
            if (method == null)
                throw new MissingMethodException(type.FullName, methodName);
            _harmony.Patch(method, prefix: new HarmonyMethod(typeof(LawgiversControlMod).GetMethod(patchName, All)));
        }

        private static void PatchPostfix(Type type, string methodName, string patchName)
        {
            MethodInfo method = type.GetMethod(methodName, All);
            if (method == null)
                throw new MissingMethodException(type.FullName, methodName);
            _harmony.Patch(method, postfix: new HarmonyMethod(typeof(LawgiversControlMod).GetMethod(patchName, All)));
        }

        private static void PersonAttributesRefreshPostfix(object __instance)
        {
            try { _uiOwner?.AttachPersonMaxButton(__instance); }
            catch (Exception ex) { MelonLogger.Error("Person cheat button failed: " + ex); }
        }

        private static void PartyMembersButtonPrefix(object __instance)
        {
            _partyMembersOpening = __instance;
            _partyMembersContext = __instance;
            _partyMemberList = null;
        }

        private static void UiListOpenPostfix(object __instance)
        {
            if (_partyMembersOpening != null)
                _partyMemberList = __instance;
        }

        private static void PartyMembersButtonPostfix(object __instance)
        {
            try
            {
                if (_partyMemberList != null)
                    _uiOwner?.AttachPartyMaxButton(__instance, _partyMemberList);
            }
            catch (Exception ex) { MelonLogger.Error("Party cheat button failed: " + ex); }
            finally { _partyMembersOpening = null; }
        }

        private static void UiListRefreshPostfix(object __instance)
        {
            try
            {
                if (_partyMemberList != null && SameNativeObject(__instance, _partyMemberList) && _partyMembersContext != null)
                    _uiOwner?.AttachPartyMaxButton(_partyMembersContext, __instance);
            }
            catch (Exception ex) { MelonLogger.Error("Party list cheat refresh failed: " + ex); }
        }

        private void AttachPersonMaxButton(object attributes)
        {
            Transform root = Get(attributes, "transform") as Transform;
            if (root == null)
                return;

            bool layout = root.gameObject.GetComponent<VerticalLayoutGroup>() != null;
            Transform target = root;
            RectTransform reference = null;
            if (!layout)
            {
                Transform cunning = Get(Get(attributes, "Cunning"), "transform") as Transform;
                Transform statRow = cunning == null ? null : cunning.parent;
                if (statRow is RectTransform && statRow.parent != null)
                {
                    reference = (RectTransform)statRow;
                    target = statRow.parent;
                }
            }
            if (FindDirectChild(target, "LawgiversControl.PersonMax") != null)
                return;

            AcquireFont(target);
            if (_uiFont == null)
                return;

            GameObject row = CreateIntegratedButton(target, "LawgiversControl.PersonMax", "CHEAT  ·  모두 최대", delegate
            {
                object person = Get(attributes, "Person");
                if (person == null)
                {
                    MelonLogger.Warning("The person window no longer has a selected person.");
                    return;
                }
                int changed = MaxPersonAttributes(person);
                Invoke(attributes, "RefreshAlways");
                MelonLogger.Msg(FriendlyName(person) + ": maximized " + changed + " attributes.");
            });

            RectTransform rect = row.GetComponent<RectTransform>();
            if (layout)
            {
                LayoutElement element = row.AddComponent<LayoutElement>();
                element.minHeight = 42f;
                element.preferredHeight = 42f;
                element.flexibleWidth = 1f;
                row.transform.SetAsLastSibling();
            }
            else
            {
                if (reference != null)
                {
                    rect.anchorMin = reference.anchorMin;
                    rect.anchorMax = reference.anchorMax;
                    rect.pivot = reference.pivot;
                    float height = Math.Max(36f, Math.Abs(reference.rect.height));
                    rect.anchoredPosition = reference.anchoredPosition + new Vector2(0f, -(height + 8f));
                    rect.sizeDelta = new Vector2(reference.sizeDelta.x, 42f);
                }
                else
                {
                    rect.anchorMin = new Vector2(0f, 0f);
                    rect.anchorMax = new Vector2(1f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    rect.anchoredPosition = new Vector2(0f, 12f);
                    rect.sizeDelta = new Vector2(-16f, 42f);
                }
            }
            WriteIntegratedUiReport("person-attached", target);
        }

        private void AttachPartyMaxButton(object partyMembers, object uiList)
        {
            Transform directory = Get(uiList, "Directory") as Transform;
            if (directory == null || FindDirectChild(directory, "LawgiversControl.PartyMax") != null)
                return;

            AcquireFont(directory);
            if (_uiFont == null)
                return;

            GameObject row = CreateIntegratedButton(directory, "LawgiversControl.PartyMax", "CHEAT  ·  모두 최대", delegate
            {
                object party = Get(partyMembers, "Party");
                Type instanceType;
                object world;
                List<object> people;
                List<object> parties;
                List<object> nations;
                if (party == null || !TryGetWorld(out instanceType, out world, out people, out parties, out nations))
                {
                    MelonLogger.Warning("The party member list no longer has a valid party.");
                    return;
                }
                int persons;
                int changed = MaxPartyMemberAttributes(party, people, out persons);
                Invoke(partyMembers, "RefreshAlways");
                Invoke(uiList, "RefreshAlways");
                MelonLogger.Msg(FriendlyName(party) + ": maximized " + changed + " attributes for " + persons + " members.");
            });

            LayoutElement element = row.AddComponent<LayoutElement>();
            element.minHeight = 42f;
            element.preferredHeight = 42f;
            element.flexibleWidth = 1f;
            row.transform.SetAsFirstSibling();
            WriteIntegratedUiReport("party-attached", directory);
        }

        private GameObject CreateIntegratedButton(Transform parent, string name, string label, Action action)
        {
            GameObject row = CreateRect(name, parent);
            Image background = row.AddComponent<Image>();
            background.color = IntegratedButtonColor;
            Outline outline = row.AddComponent<Outline>();
            outline.effectColor = new Color(0.18f, 0.18f, 0.19f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);

            Button button = row.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.40f, 0.41f, 0.43f, 1f);
            colors.pressedColor = new Color(0.24f, 0.25f, 0.27f, 1f);
            button.colors = colors;

            GameObject accent = CreateRect("CheatAccent", row.transform);
            RectTransform accentRect = accent.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.anchoredPosition = Vector2.zero;
            accentRect.sizeDelta = new Vector2(5f, 0f);
            accent.AddComponent<Image>().color = IntegratedAccentColor;

            GameObject textObject = CreateRect("Label", row.transform);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 0f);
            textRect.offsetMax = Vector2.zero;
            object text = AddComponent(textObject, FindType("TMPro.TextMeshProUGUI"));
            Set(text, "font", _uiFont);
            if (_uiFontMaterial != null)
                Set(text, "fontSharedMaterial", _uiFontMaterial);
            Set(text, "text", label);
            Set(text, "fontSize", 18f);
            SetEnum(text, "fontStyle", "Bold");
            SetEnum(text, "alignment", "Center");
            Set(text, "color", Color.white);
            Invoke(text, "SetAllDirty");

            IntPtr pointer = ButtonPointer(button);
            if (pointer == IntPtr.Zero)
                throw new InvalidOperationException("Unity button pointer is unavailable.");
            IntegratedCallbacks[pointer] = action;
            return row;
        }

        private void AcquireFont(Transform context)
        {
            if (_uiFont != null)
                return;
            Type textType = FindType("TMPro.TextMeshProUGUI");
            if (textType == null)
                return;
            object nativeType = NativeType(textType);
            object local = InvokeResult(context.gameObject, "GetComponentsInChildren", nativeType, true);
            object candidate = Values(local).FirstOrDefault(x => Get(x, "font") != null);
            if (candidate == null)
            {
                object all = InvokeStaticResult(typeof(Resources), "FindObjectsOfTypeAll", nativeType);
                candidate = Values(all).FirstOrDefault(x => Get(x, "font") != null);
            }
            _uiFont = Get(candidate, "font");
            _uiFontMaterial = Get(candidate, "fontSharedMaterial", "fontMaterial");
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            object rectType = NativeType(typeof(RectTransform));
            Type arrayType = Type.GetType("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray`1, Il2CppInterop.Runtime", true).MakeGenericType(rectType.GetType());
            object componentTypes = Activator.CreateInstance(arrayType, new object[] { 1L });
            arrayType.GetProperty("Item").SetValue(componentTypes, rectType, new object[] { 0 });
            GameObject go = (GameObject)Activator.CreateInstance(typeof(GameObject), new object[] { name, componentTypes });
            go.transform.SetParent(parent, false);
            return go;
        }

        private static object NativeType(Type type)
        {
            Type il2CppType = Type.GetType("Il2CppInterop.Runtime.Il2CppType, Il2CppInterop.Runtime", true);
            return il2CppType.GetMethod("From", All, null, new[] { typeof(Type) }, null).Invoke(null, new object[] { type });
        }

        private static object AddComponent(GameObject gameObject, Type componentType)
        {
            return gameObject == null || componentType == null ? null : InvokeResult(gameObject, "AddComponent", NativeType(componentType));
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

        private static Transform FindDirectChild(Transform parent, string name)
        {
            if (parent == null)
                return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && string.Equals(child.gameObject.name, name, StringComparison.Ordinal))
                    return child;
            }
            return null;
        }

        private static bool SameNativeObject(object left, object right)
        {
            if (ReferenceEquals(left, right))
                return true;
            object a = Get(left, "Pointer");
            object b = Get(right, "Pointer");
            return a is IntPtr && b is IntPtr && (IntPtr)a != IntPtr.Zero && (IntPtr)a == (IntPtr)b;
        }

        private static IntPtr ButtonPointer(Button button)
        {
            object pointer = Get(button, "Pointer");
            return pointer is IntPtr ? (IntPtr)pointer : IntPtr.Zero;
        }

        private static void EnsureButtonHook()
        {
            if (_buttonHookPatched)
                return;
            MethodInfo press = typeof(Button).GetMethod("Press", All);
            if (press == null)
                throw new MissingMethodException("UnityEngine.UI.Button.Press");
            _harmony.Patch(press, postfix: new HarmonyMethod(typeof(LawgiversControlMod).GetMethod("ButtonPressPostfix", All)));
            _buttonHookPatched = true;
        }

        private static void ButtonPressPostfix(object __instance)
        {
            try
            {
                object pointer = Get(__instance, "Pointer");
                Action callback;
                if (pointer is IntPtr && IntegratedCallbacks.TryGetValue((IntPtr)pointer, out callback))
                    callback();
            }
            catch (Exception ex) { MelonLogger.Error("Integrated cheat action failed: " + ex); }
        }

        private void WriteIntegratedUiReport(string state, Transform parent)
        {
            try
            {
                string report = Path.Combine(_directory, "ui-runtime.json");
                File.WriteAllText(report, JsonConvert.SerializeObject(new
                {
                    GeneratedUtc = DateTime.UtcNow,
                    Mode = "ContextIntegrated",
                    State = state,
                    Parent = parent == null ? null : parent.gameObject.name,
                    SeparateOverlay = false,
                    CustomInputFields = false
                }, Formatting.Indented));
            }
            catch { }
        }
    }
}
