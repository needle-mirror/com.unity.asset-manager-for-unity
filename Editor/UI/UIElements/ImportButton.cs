using System;
using System.Collections.Generic;
using System.IO;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    static partial class UssStyle
    {
        public const string ImportButtonsContainer = "import-buttons-container";
        public const string ImportButton = "import-button";
        public const string ImportToButton = "import-to-button";
    }

    class ImportButton : VisualElement
    {
        readonly Button m_ImportButton;
        readonly DropdownField m_ImportToDropdown;

        public string text
        {
            get => m_ImportButton.text;
            set => m_ImportButton.text = value;
        }

        public ImportButton()
        {
            AddToClassList(UssStyle.ImportButtonsContainer);

            m_ImportButton = CreateImportButton(this);
            m_ImportToDropdown = CreateImportToButton(this);

            SetEnabled(false);
        }

        public void RegisterCallback(Action<string> beginImport)
        {
            m_ImportButton.clicked += () => beginImport(null);
            m_ImportToDropdown.RegisterValueChangedCallback(value =>
            {
                if (value.newValue == L10n.Tr(Constants.ImportToActionText))
                {
                    ShowImportOptions(beginImport);
                }

                m_ImportToDropdown.value = null;
            });
        }

        static Button CreateImportButton(VisualElement container)
        {
            var button = new Button
            {
                text = L10n.Tr(Constants.ImportActionText),
                focusable = false
            };
            button.AddToClassList(UssStyle.ImportButton);

            container.Add(button);

            return button;
        }

        static DropdownField CreateImportToButton(VisualElement container)
        {
            var importToDropdown = new DropdownField
            {
                focusable = false
            };
            UIElementsUtils.Hide(importToDropdown.Q(null, "unity-base-popup-field__text"));
            importToDropdown.AddToClassList(UssStyle.ImportToButton);

            importToDropdown.choices = new List<string> { L10n.Tr(Constants.ImportToActionText) };

            container.Add(importToDropdown);

            return importToDropdown;
        }

        void ShowImportOptions(Action<string> beginImport)
        {
            var importLocation = Utilities.OpenFolderPanelInDirectory(L10n.Tr(Constants.ImportLocationTitle),
                AssetManagerCoreConstants.AssetsFolderName);

            if (string.IsNullOrEmpty(importLocation))
            {
                return;
            }

            beginImport(Path.Combine(AssetManagerCoreConstants.AssetsFolderName, importLocation[(Application.dataPath.Length + 1)..]));
        }
    }
}
