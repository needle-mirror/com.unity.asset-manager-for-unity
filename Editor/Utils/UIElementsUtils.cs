using System;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    /// <summary>
    /// An utility class for common UIElements setup method
    /// </summary>
    internal static class UIElementsUtils
    {
        internal static readonly string uiResourcesLocation = $"Packages/{Constants.PackageName}/Editor/Resources/";
        const string k_Icon = "Package-Icon.png";

        internal static Texture GetPackageIcon()
        {
            return AssetDatabase.LoadAssetAtPath(Path.Combine(uiResourcesLocation, "Images", EditorGUIUtility.isProSkin ? "Dark" : "Light", k_Icon), typeof(Texture)) as Texture;
        }

        internal static Texture GetCategoryIcon(string filename)
        {
            return AssetDatabase.LoadAssetAtPath(Path.Combine(uiResourcesLocation, "Images", EditorGUIUtility.isProSkin ? "Dark" : "Light", filename), typeof(Texture)) as Texture;
        }

        internal static Button SetupButton(string buttonName, Action onClickAction, bool isEnabled, VisualElement parent, string text = "", string tooltip = "", bool showIfEnabled = true)
        {
            Button button = parent.Query<Button>(buttonName);
            button.SetEnabled(isEnabled);
            button.clickable = new Clickable(() => onClickAction?.Invoke());
            button.text = text;
            button.tooltip = string.IsNullOrEmpty(tooltip) ? button.text : tooltip;

            if (showIfEnabled && isEnabled)
            {
                Show(button);
            }

            return button;
        }

        internal static Label SetupLabel(string labelName, string text, VisualElement parent, Manipulator manipulator = null)
        {
            Label label = parent.Query<Label>(labelName);
            label.text = text;
            if (manipulator != null)
            {
                label.AddManipulator(manipulator);
            }

            return label;
        }

        internal static ToolbarSearchField SetupToolbarSearchField(string name, EventCallback<ChangeEvent<string>> onValueChanged, VisualElement parent)
        {
            var uxmlField = parent.Q<ToolbarSearchField>(name);
            uxmlField.value = string.Empty;
            uxmlField.SetEnabled(true);
            uxmlField.RegisterCallback(onValueChanged);
            return uxmlField;
        }

        internal static void Hide(VisualElement element)
        {
            if (element == null)
                return;

            element.style.display = DisplayStyle.None;
        }

        internal static void Show(VisualElement element)
        {
            if (element == null)
                return;

            element.style.display = DisplayStyle.Flex;
        }

        internal static VisualTreeAsset LoadUXML(string tabName)
        {
            var path = $"{uiResourcesLocation}/UXML/{tabName}.uxml";
            return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
        }

        internal static void LoadCommonStyleSheet(VisualElement target)
        {
            LoadCustomStyleSheet(target, "Main");
        }

        internal static void LoadCustomStyleSheet(VisualElement target, string Stylesheet)
        {
            var styleSheetFilePath = $"{uiResourcesLocation}/StyleSheets/{Stylesheet}.uss";
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(styleSheetFilePath);
            if (styleSheet != null)
                target.styleSheets.Add(styleSheet);
        }

        internal static void RemoveCustomStylesheets(VisualElement target)
        {
            for (var i = target.styleSheets.count - 1; i > 0; i--)
            {
                target.styleSheets.Remove(target.styleSheets[i]);
            }
        }
    }
}
