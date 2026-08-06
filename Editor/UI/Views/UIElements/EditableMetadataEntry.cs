using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Extends MetadataPageEntry with inline editing support.
    /// In read-only mode: shows a static label. In inline mode: click to reveal the type-specific editor.
    /// Reuses the same layout (title + field + kebab button) as the upload page.
    /// </summary>
    class EditableMetadataEntry : MetadataPageEntry, IEditableEntry
    {
        public string AssetId => m_AssetIdentifier?.AssetId ?? string.Empty;
        public bool IsEditingEnabled { get; private set; }
        public bool AllowMultiSelection => false;

        public event Action<object> EntryEdited;

        // Required by IEditableEntry; metadata entries derive "edited" state from the underlying metadata model
        // (see RefreshEditedIndicator), so this event is intentionally never raised here.
#pragma warning disable CS0067
        public event Func<string, object, bool> IsEntryEdited;
#pragma warning restore CS0067
        public event Action EditingStarted;
        public event Action EditingEnded;

        readonly IMetadata m_Metadata;
        readonly IMetadataFieldDefinition m_FieldDefinition;
        readonly IInlineEditService m_InlineEditService;
        readonly AssetIdentifier m_AssetIdentifier;
        readonly List<UserInfo> m_UserInfos;

        readonly VisualElement m_FieldContainer;
        VisualElement m_ReadOnlyView;
        VisualElement m_EditViewContainer;
        VisualElement m_EditIcon;
        InlineEditSavingIndicator m_SavingIndicator;

        EditingMode m_EditingMode;
        bool m_IsEditing;
        bool m_IsSaving;
        List<IMetadata> m_EditCloneList;
        InlineEditConfirmationPopupContainer m_ConfirmationPopup;
        readonly Action m_OnCancelEdit;

        /// <summary>For text type only: the single TextField used for both readonly and edit (avoids label/field swap).</summary>
        TextField m_TextFieldForTextType;
        string m_OriginalTextValue;

        /// <summary>For number type only: the single DoubleField used for both readonly and edit (avoids label/field swap).</summary>
        DoubleField m_DoubleFieldForNumberType;
        double m_OriginalNumberValue;

        public EditableMetadataEntry(
            IMetadata metadata,
            IMetadataFieldDefinition fieldDefinition,
            IInlineEditService inlineEditService,
            AssetIdentifier identifier,
            List<UserInfo> userInfosForUserType = null,
            Action onRemoved = null,
            Action onCancelEdit = null)
            : base(
                fieldDefinition?.Type == MetadataFieldType.MultiSelection ? string.Empty : metadata?.Name ?? string.Empty,
                CreateInitialView(metadata, fieldDefinition, userInfosForUserType, out var textFieldForText, out var doubleFieldForNumber),
                () => OnRemoveClicked(inlineEditService, identifier, metadata, onRemoved))
        {
            m_Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            m_FieldDefinition = fieldDefinition ?? throw new ArgumentNullException(nameof(fieldDefinition));
            m_InlineEditService = inlineEditService ?? throw new ArgumentNullException(nameof(inlineEditService));
            m_AssetIdentifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
            m_UserInfos = userInfosForUserType ?? new List<UserInfo>();
            m_OnCancelEdit = onCancelEdit;
            m_TextFieldForTextType = textFieldForText;
            m_DoubleFieldForNumberType = doubleFieldForNumber;

            m_FieldContainer = this.Q(className: UssStyle.MetadataFieldAndButtonContainer);
            m_ReadOnlyView = m_FieldContainer?.Q(className: UssStyle.MetadataPageEntryMetadataField);

            ConfigureEditing(EditingMode.ReadOnly);

            if (m_FieldDefinition.Type == MetadataFieldType.User)
                ScheduleUserDisplayResolve();
        }

        void ScheduleUserDisplayResolve()
        {
            if (m_ReadOnlyView is not Label label)
                return;

            var userMeta = m_Metadata as UserMetadata;
            if (userMeta == null)
                return;

            var resolved = m_UserInfos?.FirstOrDefault(u => u.UserId == userMeta.Value)?.Name;
            if (!string.IsNullOrEmpty(resolved))
            {
                label.text = resolved;
                return;
            }

            schedule.Execute(() =>
            {
                var name = m_UserInfos?.FirstOrDefault(u => u.UserId == userMeta.Value)?.Name;
                if (!string.IsNullOrEmpty(name))
                    label.text = name;
            }).StartingIn(100);
        }

        /// <summary>Creates the initial readonly view. For text/number types returns a value row with a single field (no label/field swap).</summary>
        static VisualElement CreateInitialView(IMetadata metadata, IMetadataFieldDefinition fieldDefinition,
            List<UserInfo> userInfos, out TextField textFieldForTextType, out DoubleField doubleFieldForNumberType)
        {
            textFieldForTextType = null;
            doubleFieldForNumberType = null;

            if (fieldDefinition?.Type == MetadataFieldType.Text && metadata is TextMetadata textMeta)
            {
                var valueRow = new VisualElement();
                valueRow.AddToClassList(UssStyle.InlineEditValueRow);

                var textField = new TextField { value = textMeta.Value };
                textField.isReadOnly = true;
                textField.focusable = false;
                textField.delegatesFocus = false;
                textField.AddToClassList(UssStyle.InlineEditTextFieldReadonly);
                valueRow.Add(textField);

                var editIcon = new VisualElement();
                editIcon.AddToClassList(UssStyle.InlineEditIcon);
                valueRow.Add(editIcon);

                textFieldForTextType = textField;
                return valueRow;
            }

            if (fieldDefinition?.Type == MetadataFieldType.Number && metadata is NumberMetadata numberMeta)
            {
                var valueRow = new VisualElement();
                valueRow.AddToClassList(UssStyle.InlineEditValueRow);

                var doubleField = new DoubleField { value = numberMeta.Value };
                doubleField.isReadOnly = true;
                doubleField.focusable = false;
                doubleField.delegatesFocus = false;
                doubleField.AddToClassList(UssStyle.InlineEditTextFieldReadonly);
                valueRow.Add(doubleField);

                var editIcon = new VisualElement();
                editIcon.AddToClassList(UssStyle.InlineEditIcon);
                valueRow.Add(editIcon);

                doubleFieldForNumberType = doubleField;
                return valueRow;
            }

            return CreateReadOnlyViewForMetadata(metadata, userInfos);
        }

        static bool IsTypedFieldType(MetadataFieldType type)
        {
            return type == MetadataFieldType.Text || type == MetadataFieldType.Number;
        }

        static void OnRemoveClicked(IInlineEditService service, AssetIdentifier id, IMetadata metadata, Action callback)
        {
            if (service == null || id == null || metadata == null)
                return;

            TaskUtils.TrackException(RemoveAsync(service, id, metadata.FieldKey, callback));
        }

        static async System.Threading.Tasks.Task RemoveAsync(IInlineEditService service, AssetIdentifier id, string fieldKey, Action callback)
        {
            var result = await service.RemoveFieldAsync(id, fieldKey, default);
            if (result.Success)
                callback?.Invoke();
        }

        static VisualElement CreateReadOnlyViewForMetadata(IMetadata metadata, List<UserInfo> userInfos = null)
        {
            if (metadata == null)
                return new Label();

            switch (metadata.Type)
            {
                case MetadataFieldType.Text:
                    return new Label(((TextMetadata)metadata).Value);
                case MetadataFieldType.Boolean:
                    var toggle = new Toggle { value = ((BooleanMetadata)metadata).Value };
                    toggle.SetEnabled(false);
                    return toggle;
                case MetadataFieldType.Number:
                    return new Label(((NumberMetadata)metadata).Value.ToString(CultureInfo.CurrentCulture));
                case MetadataFieldType.Timestamp:
                    return new Label(Utilities.DatetimeToString(((TimestampMetadata)metadata).Value.DateTime));
                case MetadataFieldType.Url:
                    return CreateUrlReadOnlyView((UrlMetadata)metadata);
                case MetadataFieldType.User:
                    var userMeta = (UserMetadata)metadata;
                    var userDisplay = userInfos?.FirstOrDefault(u => u.UserId == userMeta.Value)?.Name ?? userMeta.Value;
                    return new Label(userDisplay);
                case MetadataFieldType.SingleSelection:
                    return new Label(((SingleSelectionMetadata)metadata).Value);
                case MetadataFieldType.MultiSelection:
                    var chipContainer = new VisualElement();
                    chipContainer.AddToClassList(UssStyle.DetailsPageChipContainer);
                    foreach (var v in ((MultiSelectionMetadata)metadata).Value ?? new List<string>())
                        chipContainer.Add(new Chip(v));
                    return chipContainer;
                default:
                    return new Label(metadata.GetValue()?.ToString() ?? string.Empty);
            }
        }

        /// <summary>
        /// Creates a clickable hyperlink label for a URL metadata field. The label shrinks to its text width
        /// so only the text itself is clickable; clicking outside it still triggers edit mode.
        /// </summary>
        static VisualElement CreateUrlReadOnlyView(UrlMetadata urlMetadata)
        {
            var uriString = urlMetadata?.Value.Uri?.ToString() ?? string.Empty;
            var labelText = urlMetadata?.Value.Label ?? string.Empty;

            if (string.IsNullOrEmpty(uriString))
                return new Label(labelText);

            var displayText = !string.IsNullOrEmpty(labelText) ? labelText : uriString;

            var label = new Label();
            label.enableRichText = true;
            label.text = $"<u>{displayText}</u>";
            label.tooltip = uriString;
            label.AddToClassList(UssStyle.UrlHyperlinkLabel);
            // Absorb all free space to the right so the label stays left-aligned regardless of
            // whether the edit icon is visible
            label.style.marginRight = StyleKeyword.Auto;

            // Open the URL when the hyperlink text is clicked; stop propagation to avoid triggering click-to-edit.
            label.RegisterCallback<ClickEvent>(evt =>
            {
                Application.OpenURL(uriString);
                evt.StopPropagation();
            });

            return label;
        }

        public void ConfigureEditing(EditingMode mode)
        {
            if (m_EditingMode == mode)
                return;

            if (m_EditingMode == EditingMode.Inline && m_FieldContainer != null)
            {
                m_FieldContainer.RemoveFromClassList(UssStyle.InlineEditable);
                m_EditIcon?.RemoveFromHierarchy();
                m_EditIcon = null;
            }

            m_EditingMode = mode;
            IsEditingEnabled = mode == EditingMode.Inline;

            if (IsEditingEnabled)
            {
                SetKebabEditAction(BeginEdit);
                RegisterCallback<ClickEvent>(OnClickToEdit);
                if (m_FieldContainer != null)
                {
                    m_FieldContainer.AddToClassList(UssStyle.InlineEditable);
                    if (!IsTypedFieldType(m_FieldDefinition.Type))
                    {
                        m_EditIcon = new VisualElement();
                        m_EditIcon.AddToClassList(UssStyle.InlineEditIcon);
                        // For URL fields the label uses margin-right: auto to stay left-aligned.
                        // Override the icon's CSS margin-left: auto so the two don't compete.
                        if (m_FieldDefinition.Type == MetadataFieldType.Url)
                            m_EditIcon.style.marginLeft = 0;
                        m_FieldContainer.Insert(m_FieldContainer.childCount - 1, m_EditIcon);
                    }
                }

                var resolvedEditIcon = m_EditIcon ?? m_ReadOnlyView?.Q(className: UssStyle.InlineEditIcon);
                m_SavingIndicator = new InlineEditSavingIndicator(
                    m_InlineEditService, resolvedEditIcon, m_FieldContainer,
                    this, () => m_IsEditing);
            }
            else
            {
                m_SavingIndicator?.Dispose();
                m_SavingIndicator = null;
                SetKebabEditAction(null);
                UnregisterCallback<ClickEvent>(OnClickToEdit);
            }
        }

        public void SetConfirmationPopupParent(VisualElement popupParent)
        {
            m_ConfirmationPopup?.Dispose();
            m_ConfirmationPopup = new InlineEditConfirmationPopupContainer();
            popupParent.Add(m_ConfirmationPopup);
        }

        void OnClickToEdit(ClickEvent evt)
        {
            var target = evt.target as VisualElement;
            while (target != null && target != this)
            {
                if (target.ClassListContains(UssStyle.KebabButton))
                    return;
                target = target.hierarchy.parent;
            }
            StartEdit();
        }

        void StartEdit()
        {
            if (!IsEditingEnabled || m_IsEditing || m_FieldContainer == null || m_ConfirmationPopup == null)
                return;

            if (m_SavingIndicator?.IsSaveInProgress == true)
                return;

            m_IsEditing = true;

            var kebabButton = m_FieldContainer?.Q(className: UssStyle.KebabButton);
            if (kebabButton != null)
                kebabButton.style.display = DisplayStyle.None;

            EditingStarted?.Invoke();

            AddToClassList(UssStyle.MetadataPageEntryEditing);
            if (m_FieldDefinition.Type != MetadataFieldType.Boolean)
                AddToClassList(UssStyle.MetadataPageEntryEditingPopupBelow);
            m_FieldContainer?.AddToClassList(UssStyle.InlineEditableEditing);
            m_EditCloneList = new List<IMetadata> { m_Metadata.Clone() };

            if (m_FieldDefinition.Type == MetadataFieldType.Text && m_TextFieldForTextType != null)
            {
                m_OriginalTextValue = m_TextFieldForTextType.value;
                var textEditIcon = m_ReadOnlyView?.Q(className: UssStyle.InlineEditIcon);
                if (textEditIcon != null)
                    textEditIcon.style.display = DisplayStyle.None;
                m_TextFieldForTextType.isReadOnly = false;
                m_TextFieldForTextType.focusable = true;
                m_TextFieldForTextType.delegatesFocus = true;
                m_TextFieldForTextType.RemoveFromClassList(UssStyle.InlineEditTextFieldReadonly);
                m_TextFieldForTextType.Focus();
                m_TextFieldForTextType.SelectAll();
                m_ConfirmationPopup.Show(m_ReadOnlyView, ConfirmEdit, () => ExitEditMode(isCancel: true));
            }
            else if (m_FieldDefinition.Type == MetadataFieldType.Number && m_DoubleFieldForNumberType != null)
            {
                m_OriginalNumberValue = m_DoubleFieldForNumberType.value;
                var numberEditIcon = m_ReadOnlyView?.Q(className: UssStyle.InlineEditIcon);
                if (numberEditIcon != null)
                    numberEditIcon.style.display = DisplayStyle.None;
                m_DoubleFieldForNumberType.isReadOnly = false;
                m_DoubleFieldForNumberType.focusable = true;
                m_DoubleFieldForNumberType.delegatesFocus = true;
                m_DoubleFieldForNumberType.RemoveFromClassList(UssStyle.InlineEditTextFieldReadonly);
                m_DoubleFieldForNumberType.Focus();
                m_DoubleFieldForNumberType.SelectAll();
                m_ConfirmationPopup.Show(m_ReadOnlyView, ConfirmEdit, () => ExitEditMode(isCancel: true));
            }
            else
            {
                if (m_ReadOnlyView != null)
                    m_ReadOnlyView.style.display = DisplayStyle.None;
                if (m_EditIcon != null)
                    m_EditIcon.style.display = DisplayStyle.None;

                m_EditViewContainer = CreateMetadataFieldForEdit(m_EditCloneList);
                m_EditViewContainer.AddToClassList(UssStyle.MetadataPageEntryMetadataField);
                m_FieldContainer.Insert(0, m_EditViewContainer);

                bool alignBeside = m_FieldDefinition.Type == MetadataFieldType.Boolean;
                var popupOffset = alignBeside ? new Vector2(24f, 0f) : Vector2.zero;
                m_ConfirmationPopup.Show(m_FieldContainer, ConfirmEdit, () => ExitEditMode(isCancel: true),
                    alignWithTarget: alignBeside, positionOffset: popupOffset);
            }
        }

        /// <summary>
        /// Programmatically start inline edit (e.g. when adding a new field so the user can set the value before saving).
        /// </summary>
        public void BeginEdit()
        {
            StartEdit();
        }

        void ShowConfirmationPopup()
        {
            if (m_ConfirmationPopup == null)
                return;

            if (m_FieldDefinition.Type == MetadataFieldType.Text || m_FieldDefinition.Type == MetadataFieldType.Number)
            {
                m_ConfirmationPopup.Show(m_ReadOnlyView, ConfirmEdit, () => ExitEditMode(isCancel: true));
            }
            else
            {
                var alignBeside = m_FieldDefinition.Type == MetadataFieldType.Boolean;
                var popupOffset = alignBeside ? new Vector2(24f, 0f) : Vector2.zero;
                m_ConfirmationPopup.Show(m_FieldContainer, ConfirmEdit, () => ExitEditMode(isCancel: true),
                    alignWithTarget: alignBeside, positionOffset: popupOffset);
            }
        }

        VisualElement CreateMetadataFieldForEdit(List<IMetadata> metadataList)
        {
            return MetadataFieldFactory.CreateEditField(m_FieldDefinition, metadataList, m_UserInfos);
        }

        async void ConfirmEdit()
        {
            if (!m_IsEditing || m_IsSaving)
                return;

            if (m_FieldDefinition.Type == MetadataFieldType.Text && m_TextFieldForTextType != null)
            {
                var textClone = m_EditCloneList?.FirstOrDefault() as TextMetadata;
                if (textClone != null)
                    textClone.Value = m_TextFieldForTextType.value;
            }
            else if (m_FieldDefinition.Type == MetadataFieldType.Number && m_DoubleFieldForNumberType != null)
            {
                var numberClone = m_EditCloneList?.FirstOrDefault() as NumberMetadata;
                if (numberClone != null)
                    numberClone.Value = m_DoubleFieldForNumberType.value;
            }

            m_IsSaving = true;
            try
            {
                var clone = m_EditCloneList?.FirstOrDefault();
                if (clone != null)
                {
                    var edit = new AssetFieldEdit(m_AssetIdentifier, EditField.Custom, clone);
                    var result = await m_InlineEditService.SaveFieldAsync(edit, default);
                    if (result.Success)
                    {
                        UpdateReadOnlyView(clone);
                        EntryEdited?.Invoke(clone);
                        ExitEditMode(isCancel: false);
                    }
                    else
                    {
                        Utilities.DevLogError($"Save failed: {result.ErrorMessage}");
                        // Re-show popup so user can retry or cancel
                        ShowConfirmationPopup();
                    }
                }
                else
                {
                    ExitEditMode(isCancel: false);
                }
            }
            finally
            {
                m_IsSaving = false;
            }
        }

        void UpdateReadOnlyView(IMetadata updated)
        {
            if (m_FieldDefinition.Type == MetadataFieldType.Text && m_TextFieldForTextType != null && updated is TextMetadata textMeta)
            {
                m_TextFieldForTextType.value = textMeta.Value;
                return;
            }
            if (m_FieldDefinition.Type == MetadataFieldType.Number && m_DoubleFieldForNumberType != null && updated is NumberMetadata numberMeta)
            {
                m_DoubleFieldForNumberType.value = numberMeta.Value;
                return;
            }

            m_ReadOnlyView?.RemoveFromHierarchy();
            m_ReadOnlyView = CreateReadOnlyViewForMetadata(updated, m_UserInfos);
            m_ReadOnlyView.AddToClassList(UssStyle.MetadataPageEntryMetadataField);
            m_FieldContainer?.Insert(0, m_ReadOnlyView);
        }

        void ExitEditMode(bool isCancel = false)
        {
            if (!m_IsEditing)
                return;

            m_IsEditing = false;
            m_ConfirmationPopup?.Hide();
            RemoveFromClassList(UssStyle.MetadataPageEntryEditing);
            RemoveFromClassList(UssStyle.MetadataPageEntryEditingPopupBelow);
            m_FieldContainer?.RemoveFromClassList(UssStyle.InlineEditableEditing);

            var kebabButton = m_FieldContainer?.Q(className: UssStyle.KebabButton);
            if (kebabButton != null)
                kebabButton.style.display = StyleKeyword.Null;

            EditingEnded?.Invoke();

            if (m_FieldDefinition.Type == MetadataFieldType.Text && m_TextFieldForTextType != null)
            {
                if (isCancel)
                    m_TextFieldForTextType.value = m_OriginalTextValue;
                m_TextFieldForTextType.isReadOnly = true;
                m_TextFieldForTextType.focusable = false;
                m_TextFieldForTextType.delegatesFocus = false;
                m_TextFieldForTextType.AddToClassList(UssStyle.InlineEditTextFieldReadonly);
                var textEditIcon = m_ReadOnlyView?.Q(className: UssStyle.InlineEditIcon);
                if (textEditIcon != null)
                    textEditIcon.style.display = StyleKeyword.Null;
            }
            else if (m_FieldDefinition.Type == MetadataFieldType.Number && m_DoubleFieldForNumberType != null)
            {
                if (isCancel)
                    m_DoubleFieldForNumberType.value = m_OriginalNumberValue;
                m_DoubleFieldForNumberType.isReadOnly = true;
                m_DoubleFieldForNumberType.focusable = false;
                m_DoubleFieldForNumberType.delegatesFocus = false;
                m_DoubleFieldForNumberType.AddToClassList(UssStyle.InlineEditTextFieldReadonly);
                var numberEditIcon = m_ReadOnlyView?.Q(className: UssStyle.InlineEditIcon);
                if (numberEditIcon != null)
                    numberEditIcon.style.display = StyleKeyword.Null;
            }
            else
            {
                m_EditViewContainer?.RemoveFromHierarchy();
                if (m_ReadOnlyView != null)
                    m_ReadOnlyView.style.display = DisplayStyle.Flex;
                if (m_EditIcon != null)
                    m_EditIcon.style.display = StyleKeyword.Null;
            }

            m_EditViewContainer = null;
            m_EditCloneList = null;

            if (isCancel)
                m_OnCancelEdit?.Invoke();
        }

        public void SavePendingEdits()
        {
            if (m_IsEditing)
                ConfirmEdit();
        }

        public void Dispose()
        {
            m_SavingIndicator?.Dispose();
            m_SavingIndicator = null;
            ExitEditMode(isCancel: true);
            UnregisterCallback<ClickEvent>(OnClickToEdit);
            m_ConfirmationPopup?.Dispose();
            m_ConfirmationPopup = null;
        }
    }
}
