using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    static partial class UssStyle
    {
        public const string ImportButtonsContainer = "import-buttons-container";
        public const string ImportButton = "import-button";
        public const string ImportToButton = "import-to-button";
        public const string ImportToButtonCaret = "import-to-button-caret";
        public const string ImportPopupImportTo = "import-popup-import-to";
    }

    class ImportButton : VisualElement
    {
        readonly Button m_ImportButton;
        readonly Button m_ImportToButton;

        public string text
        {
            get => m_ImportButton.text;
            set => m_ImportButton.text = value;
        }

        public ImportButton()
        {
            AddToClassList(UssStyle.ImportButtonsContainer);

            m_ImportButton = CreateImportButton(this);
            m_ImportToButton = CreateImportToButton(this);

            SetEnabled(false);
        }

        public void RegisterCallback(Action<string> beginImport)
        {
            m_ImportButton.clicked += () => beginImport(null);
            m_ImportToButton.clicked += () => ShowImportOptions(beginImport);
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

        static Button CreateImportToButton(VisualElement container)
        {
            var importToButton = new Button
            {
                focusable = false
            };
            importToButton.AddToClassList(UssStyle.ImportToButton);

            var caret = new VisualElement();
            caret.AddToClassList(UssStyle.ImportToButtonCaret);
            importToButton.Add(caret);

            container.Add(importToButton);

            return importToButton;
        }

        void ShowImportOptions(Action<string> beginImport)
        {
            var popupManager = ServicesContainer.instance.Resolve<IPopupManager>();
            var importToText = new TextElement
            {
                text = L10n.Tr(Constants.ImportToActionText)
            };
            importToText.AddToClassList(UssStyle.ImportPopupImportTo);
            importToText.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                popupManager.Hide();

                var importLocation = Utilities.OpenFolderPanelInDirectory(L10n.Tr(Constants.ImportLocationTitle),
                    Constants.AssetsFolderName);

                if (string.IsNullOrEmpty(importLocation))
                {
                    return;
                }

                beginImport(Path.Combine(Constants.AssetsFolderName, importLocation[(Application.dataPath.Length + 1)..]));
            });

            popupManager.Container.Add(importToText);
            popupManager.Show(m_ImportToButton, PopupContainer.PopupAlignment.BottomRight);
        }
    }
}
