using System;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    static partial class UssStyle
    {
        public const string MetadataPageEntry = "metadata-page-entry";
        public const string KebabButton = "metadata-page-entry-kebab-button";
        public const string MetadataPageEntryLabel = "metadata-page-entry-label";
        public const string MetadataPageEntryMetadataField = "metadata-page-entry-metadata-field";
        public const string MetadataFieldAndButtonContainer = "metadata-field-and-button-container";
        public const string MetadataPageEntryEditing = "metadata-page-entry--editing";
        public const string MetadataPageEntryEditingPopupBelow = "metadata-page-entry--editing-popup-below";
        public const string MetadataPageEntryNoLabel = "metadata-page-entry--no-label";
    }

    class MetadataPageEntry : VisualElement
    {
        Action m_KebabEditAction;
        readonly Action m_KebabRemoveAction;

        public MetadataPageEntry(string title, VisualElement metadataField, Action entryRemovedCallback)
        {
            AddToClassList(UssStyle.MetadataPageEntry);
            if (string.IsNullOrEmpty(title))
                AddToClassList(UssStyle.MetadataPageEntryNoLabel);

            if (!string.IsNullOrEmpty(title))
            {
                var titleElement = new Label(L10n.Tr(title));
                titleElement.AddToClassList(UssStyle.MetadataPageEntryLabel);
                titleElement.tooltip = title;
                Add(titleElement);
            }

            var fieldAndButtonContainer = new VisualElement();
            fieldAndButtonContainer.AddToClassList(UssStyle.MetadataFieldAndButtonContainer);

            metadataField.AddToClassList(UssStyle.MetadataPageEntryMetadataField);
            fieldAndButtonContainer.Add(metadataField);

            m_KebabRemoveAction = () =>
            {
                parent?.Remove(this);
                entryRemovedCallback?.Invoke();
            };

            fieldAndButtonContainer.Add(CreateKebabButton());

            Add(fieldAndButtonContainer);
        }

        /// <summary>
        /// Sets an optional "Edit" action shown at the top of the kebab menu.
        /// Pass <c>null</c> to remove the item (e.g. when leaving inline-edit mode).
        /// </summary>
        protected void SetKebabEditAction(Action editAction)
        {
            m_KebabEditAction = editAction;
        }

        VisualElement CreateKebabButton()
        {
            var kebabButton = new Button();
            kebabButton.ClearClassList();
            kebabButton.focusable = false;
            kebabButton.AddToClassList(UssStyle.KebabButton);
            kebabButton.clicked += ShowKebabMenu;
            return kebabButton;
        }

        void ShowKebabMenu()
        {
            var menu = new GenericMenu();
            if (m_KebabEditAction != null)
                menu.AddItem(new GUIContent("Edit"), false, () => m_KebabEditAction());
            menu.AddItem(new GUIContent("Remove"), false, () => m_KebabRemoveAction());
            menu.ShowAsContext();
        }
    }
}
