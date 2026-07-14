using System;
using System.Collections.Generic;
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
        private static readonly Color IntegratedButtonColor = new Color(0.32f, 0.33f, 0.35f, 0.98f);
        private static readonly Color IntegratedAccentColor = new Color(0.92f, 0.36f, 0.16f, 1f);
        private static bool _integratedUiReady;
        private static Type _personAttributesType;
        private static Type _partyMembersType;
        private static Type _uiListType;
        private readonly List<object> _nativeClickDelegates = new List<object>();
        private readonly List<Action> _managedClickActions = new List<Action>();
        private bool _nativeButtonCallbackVerified;
        private object _uiFont;
        private object _uiFontMaterial;

        partial void EnsureControlUi()
        {
            if (_integratedUiReady)
                return;

            try
            {
                _personAttributesType = FindType("Il2CppLawgivers.Interface.UIPersonAttributes");
                _partyMembersType = FindType("Il2CppLawgivers.Interface.UIPartyMembers");
                _uiListType = FindType("Il2CppLawgivers.Interface.UIList");
                if (_personAttributesType == null || _partyMembersType == null || _uiListType == null)
                    throw new TypeLoadException("Required Lawgivers UI types are not loaded yet.");

                VerifyNativeButtonCallback();
                _integratedUiReady = true;
                WriteIntegratedUiReport("polling-ready", null);
                MelonLogger.Msg("Context-integrated UI polling enabled.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Context UI hooks are not ready: " + ex.Message);
            }
        }

        private void VerifyNativeButtonCallback()
        {
            GameObject probe = null;
            try
            {
                probe = CreateRect("LawgiversControl.NativeButtonProbe", null);
                Button button = probe.AddComponent<Button>();
                bool invoked = false;
                BindNativeClick(button, delegate { invoked = true; });
                if (!Invoke(Get(button, "onClick"), "Invoke") || !invoked)
                    throw new InvalidOperationException("Native UnityEvent did not invoke the managed callback.");
                _nativeButtonCallbackVerified = true;
            }
            finally
            {
                if (probe != null)
                    UnityEngine.Object.Destroy(probe);
            }
        }

        partial void RefreshControlUi()
        {
            EnsureControlUi();
            if (!_integratedUiReady)
                return;

            try
            {
                foreach (object attributes in FindActiveUiObjects(_personAttributesType))
                {
                    if (Get(attributes, "Person") != null)
                        AttachPersonMaxButton(attributes);
                }

                List<object> partyComponents = FindActiveUiObjects(_partyMembersType)
                    .Where(x => Get(x, "Party") != null).ToList();
                if (partyComponents.Count == 0)
                    return;

                foreach (object list in FindActiveUiObjects(_uiListType))
                {
                    object matchingParty = partyComponents.FirstOrDefault(p => IsPartyMemberList(list, Get(p, "Party")));
                    if (matchingParty != null)
                        AttachPartyMaxButton(matchingParty, list);
                }
            }
            catch (Exception ex) { MelonLogger.Error("Context UI polling failed: " + ex); }
        }

        private static List<object> FindActiveUiObjects(Type type)
        {
            if (type == null)
                return new List<object>();
            object found = InvokeStaticResult(typeof(Resources), "FindObjectsOfTypeAll", NativeType(type));
            return Values(found).Where(IsActiveUiObject).ToList();
        }

        private static bool IsActiveUiObject(object instance)
        {
            GameObject gameObject = Get(instance, "gameObject") as GameObject;
            return gameObject != null && gameObject.activeInHierarchy;
        }

        private static bool IsPartyMemberList(object uiList, object party)
        {
            int partyId = ToInt(Get(party, "id"), int.MinValue);
            foreach (object uiBox in Values(Get(uiList, "Boxes")))
            {
                object data = Get(Get(uiBox, "box"), "data");
                if (data == null || !data.GetType().FullName.EndsWith(".Person", StringComparison.Ordinal))
                    continue;
                return partyId == int.MinValue || ToInt(Get(data, "PartyID", "partyID"), int.MinValue) == partyId;
            }
            return false;
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
            MelonLogger.Msg("Attached CHEAT control to active person attributes UI.");
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
            MelonLogger.Msg("Attached CHEAT control to active party member list.");
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

            BindNativeClick(button, action);
            return row;
        }

        private void BindNativeClick(Button button, Action action)
        {
            Type unityActionType = FindType("UnityEngine.Events.UnityAction");
            if (unityActionType == null)
                throw new TypeLoadException("UnityEngine.Events.UnityAction");
            MethodInfo converter = unityActionType.GetMethods(All).FirstOrDefault(method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return method.IsStatic && method.Name == "op_Implicit" && parameters.Length == 1 && parameters[0].ParameterType == typeof(Action);
            });
            if (converter == null)
                throw new MissingMethodException(unityActionType.FullName, "op_Implicit(System.Action)");

            object nativeAction = converter.Invoke(null, new object[] { action });
            object onClick = Get(button, "onClick");
            MethodInfo addListener = onClick == null ? null : onClick.GetType().GetMethods(All)
                .FirstOrDefault(method => method.Name == "AddListener" && method.GetParameters().Length == 1);
            if (nativeAction == null || addListener == null)
                throw new MissingMethodException("Button.onClick.AddListener(UnityAction)");
            addListener.Invoke(onClick, new[] { nativeAction });
            _managedClickActions.Add(action);
            _nativeClickDelegates.Add(nativeAction);
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
                    CustomInputFields = false,
                    NativeButtonCallback = _nativeButtonCallbackVerified
                }, Formatting.Indented));
            }
            catch { }
        }
    }
}
