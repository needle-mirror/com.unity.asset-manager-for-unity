using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    static partial class UssStyle
    {
        public const string FlexDirectionColumn = "flex-direction-column";
        public const string UrlMetadataLabels = "metadata-text-field-label";
        public const string InvalidFieldLabel = "invalid-field-label";
        public const string InvalidTextFieldLabel = "invalid-text-field";
        public const string UnityTextElement = "unity-text-element";
    }

    abstract class MetadataElement : VisualElement
    {
        public Action ValueChanged;

        protected void InvokeValueChanged()
        {
            ValueChanged?.Invoke();
        }
    }

    abstract class MetadataField<T> : MetadataElement where T : IMetadata
    {
        protected List<T> m_RepresentedMetadata;

        protected MetadataField(List<T> representedMetadata)
        {
            m_RepresentedMetadata = representedMetadata;
        }
    }

    class TextMetadataField : MetadataField<TextMetadata>
    {
        public TextMetadataField(List<TextMetadata> representedMetadata)
            : base(representedMetadata)
        {
            var textField = new MultiValueTextField(representedMetadata.Select(x => x.Value).ToList());
            textField.RegisterValueChangedCallback(OnValueChanged);
            textField.RegisterCallback<FocusOutEvent>(_ => InvokeValueChanged());

            Add(textField);
        }

        void OnValueChanged(ChangeEvent<string> evt)
        {
            m_RepresentedMetadata.ForEach(x => x.Value = evt.newValue);
        }
    }

    class NumberMetadataField : MetadataField<NumberMetadata>
    {
        public NumberMetadataField(List<NumberMetadata> representedMetadata)
            : base(representedMetadata)
        {
            var doubleField = new MultiValueDoubleField(representedMetadata.Select(x => x.Value).ToList());
            doubleField.RegisterValueChangedCallback(OnValueChanged);
            doubleField.RegisterCallback<FocusOutEvent>(_ => InvokeValueChanged());

            Add(doubleField);
        }

        void OnValueChanged(ChangeEvent<double> evt)
        {
            m_RepresentedMetadata.ForEach(x => x.Value = evt.newValue);
        }
    }

    class BooleanMetadataField : MetadataField<BooleanMetadata>
    {
        public BooleanMetadataField(List<BooleanMetadata> representedMetadata)
            : base(representedMetadata)
        {
            var toggle = new MultiValueToggle(representedMetadata.Select(x => x.Value).ToList());
            toggle.RegisterValueChangedCallback(OnValueChanged);

            Add(toggle);
        }

        void OnValueChanged(ChangeEvent<bool> evt)
        {
            m_RepresentedMetadata.ForEach(x => x.Value = evt.newValue);
            InvokeValueChanged();
        }
    }

    class UrlMetadataField : MetadataField<UrlMetadata>
    {
        readonly TextField m_UrlTextField;
        readonly Label m_UrlLabel;

        public UrlMetadataField(List<UrlMetadata> representedMetadata)
            :base(representedMetadata)
        {
            m_UrlTextField = new MultiValueTextField(representedMetadata.Select(x =>
                x?.Value.Uri == null ? string.Empty : x.Value.Uri.ToString()).ToList());
            m_UrlTextField.RegisterValueChangedCallback(OnUrlChanged);

            m_UrlLabel = new Label(Constants.UrlLabel);
            m_UrlLabel.AddToClassList(UssStyle.UrlMetadataLabels);

            var labelField = new MultiValueTextField(representedMetadata.Select(x => x.Value.Label).ToList());
            labelField.RegisterValueChangedCallback(OnLabelChanged);
            labelField.RegisterCallback<FocusOutEvent>(_ => InvokeValueChanged());

            var hyperlinkLabel = new Label(Constants.HyperlinkLabel);
            hyperlinkLabel.AddToClassList(UssStyle.UrlMetadataLabels);

            var verticalContainer = new VisualElement();
            verticalContainer.AddToClassList(UssStyle.FlexDirectionColumn);
            verticalContainer.Add(m_UrlTextField);
            verticalContainer.Add(m_UrlLabel);
            verticalContainer.Add(labelField);
            verticalContainer.Add(hyperlinkLabel);

            Add(verticalContainer);
        }

        void OnUrlChanged(ChangeEvent<string> evt)
        {
            if (Uri.TryCreate(evt.newValue, UriKind.Absolute, out _))
            {
                m_RepresentedMetadata.ForEach(x => x.Value = new UriEntry(new Uri(evt.newValue), x.Value.Label));

                m_UrlLabel.RemoveFromClassList(UssStyle.InvalidFieldLabel);
                m_UrlLabel.AddToClassList(UssStyle.UnityTextElement);
                m_UrlLabel.text = Constants.UrlLabel;

                m_UrlTextField.RemoveFromClassList(UssStyle.InvalidTextFieldLabel);
            }
            else
            {
                m_UrlLabel.AddToClassList(UssStyle.InvalidFieldLabel);
                m_UrlLabel.RemoveFromClassList(UssStyle.UnityTextElement);
                m_UrlLabel.text = Constants.InvalidUrlFormat;

                m_UrlTextField.AddToClassList(UssStyle.InvalidTextFieldLabel);
            }

            InvokeValueChanged();
        }

        void OnLabelChanged(ChangeEvent<string> evt)
        {
            m_RepresentedMetadata.ForEach(x => x.Value = new UriEntry(x.Value.Uri, evt.newValue));
        }
    }

    class TimestampMetadataField : MetadataField<TimestampMetadata>
    {
        public TimestampMetadataField(List<TimestampMetadata> representedMetadata)
            : base(representedMetadata)
        {
            var picker = new MultiValueTimestampPicker(representedMetadata.Select(x => x.Value.DateTime).ToList());
            picker.ValueChanged += OnValueChanged;

            Add(picker);
        }

        void OnValueChanged(DateTime dateTime)
        {
            m_RepresentedMetadata.ForEach(x => x.Value = new DateTimeEntry(dateTime));
            InvokeValueChanged();
        }
    }

    class UserMetadataField : MetadataField<UserMetadata>
    {
        readonly List<UserInfo> m_UserInfos;

        readonly MultiValueDropdownField m_MultiValueDropdownField;

        public UserMetadataField(List<UserMetadata> representedMetadata,
            List<UserInfo> userInfos)
            : base(representedMetadata)
        {
            m_UserInfos = userInfos;

            var values = m_UserInfos
                .Where(x => representedMetadata.Select(y => y.Value).ToList().Contains(x.UserId))
                .Select(z => z.Name).ToList();

            m_MultiValueDropdownField = new MultiValueDropdownField(values, m_UserInfos.Select(x => x.Name).ToList());
            m_MultiValueDropdownField.RegisterValueChangedCallback(evt => OnValueChanged());

            Add(m_MultiValueDropdownField);
        }

        void OnValueChanged()
        {
            m_RepresentedMetadata.ForEach(x =>
                x.Value = m_UserInfos[m_MultiValueDropdownField.index].UserId);
            InvokeValueChanged();
        }
    }

    class SingleSelectionMetadataField : MetadataField<SingleSelectionMetadata>
    {
        public SingleSelectionMetadataField(List<SingleSelectionMetadata> representedMetadata,
            List<string> choices)
            : base(representedMetadata)
        {
            var dropdownField = new MultiValueDropdownField(representedMetadata.Select(x => x.Value).ToList(),
                choices);
            dropdownField.RegisterValueChangedCallback(OnValueChanged);

            Add(dropdownField);
        }

        void OnValueChanged(ChangeEvent<string> evt)
        {
            m_RepresentedMetadata.ForEach(x => x.Value = evt.newValue);
            InvokeValueChanged();
        }
    }

    class MultiSelectionMetadataField : MetadataField<MultiSelectionMetadata>
    {
        public MultiSelectionMetadataField(string displayName,
            List<MultiSelectionMetadata> representedMetadata, List<string> choices)
            :base(representedMetadata)
        {
            var multiSelectionPicker = new MultiValueMultiSelectionPicker(displayName, representedMetadata
                .Select(x => x.Value).ToList(), choices);
            multiSelectionPicker.ValueChanged += OnValueChanged;

            Add(multiSelectionPicker);
        }

        void OnValueChanged(string option, bool newToggleValue)
        {
            m_RepresentedMetadata.ForEach(x =>
            {
                if (newToggleValue && !x.Value.Contains(option))
                {
                    x.Value.Add(option);
                }
                else if(x.Value.Contains(option))
                {
                    x.Value.Remove(option);
                }
            });
            InvokeValueChanged();
        }
    }
}
