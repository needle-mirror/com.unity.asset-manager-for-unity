using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class ChipListField : VisualElement
    {
        TextField m_TextField { get; }
        VisualElement m_ChipContainer;
        VisualElement m_TextInput;
        TextElement m_InputTextElement;

        HashSet<string> m_Values;

        public event Action<string> ChipAdded;
        public event Action<string> ChipRemoved;

        public ChipListField(HashSet<string> values, string label = null)
        {
            m_Values = values;

            m_TextField = new TextField();
            if (label != null)
            {
                m_TextField.label = label;
                m_TextField.tooltip = label;
            }
            m_TextField.RegisterCallback<KeyDownEvent>(OnKeyDownEvent);
            m_TextField.RegisterCallback<FocusOutEvent>(_ => OnEntryAdded(m_TextField.value));

            m_ChipContainer = new VisualElement();
            m_ChipContainer.AddToClassList(UssStyle.DetailsPageChipContainer);
            m_ChipContainer.AddToClassList(UssStyle.FlexWrap);
            m_ChipContainer.focusable = false;
            m_ChipContainer.pickingMode = PickingMode.Position;
            m_ChipContainer.RegisterCallback<PointerDownEvent>(OnChipContainerPointerDown);

            m_TextInput = m_TextField.Q("unity-text-input");
            if (m_TextInput != null)
            {
                m_InputTextElement = m_TextInput.Q<TextElement>();
                m_TextInput.style.flexDirection = FlexDirection.Column;
                m_TextInput.Insert(0, m_ChipContainer);
            }
            else
            {
                m_TextField.Add(m_ChipContainer);
            }

            Add(m_TextField);
            RegisterCallback<PointerUpEvent>(OnParentFieldClicked);
        }

        public void UpdateChips(IEnumerable<string> values, bool insertMultiValueChip = false)
        {
            var snapshot = values.ToList();
            m_Values.Clear();
            foreach (var v in snapshot)
                m_Values.Add(v);

            m_ChipContainer.Clear();

            if (insertMultiValueChip)
                m_ChipContainer.Add(CreateMixedValueChip());

            foreach (var chipText in snapshot)
            {
                var chip = EditChipCreator(chipText);
                if (chip != null)
                    m_ChipContainer.Add(chip);
            }
        }

        void OnKeyDownEvent(KeyDownEvent evt)
        {
            if (evt.keyCode is not (KeyCode.Return or KeyCode.KeypadEnter))
                return;

            OnEntryAdded(m_TextField.value);
            evt.StopPropagation();
            evt.PreventDefault();
        }

        void OnParentFieldClicked(PointerUpEvent evt)
        {
            evt.StopImmediatePropagation();
            FocusOnTextInput();
        }

        void OnChipContainerPointerDown(PointerDownEvent evt)
        {
            if (evt.target == m_ChipContainer)
                FocusOnTextInput();
        }

        void FocusOnTextInput()
        {
            // This method is meant to forward focus to the actual text input element.
            // Necessary because the chip container exists within the text field
            // and was otherwise stealing focus from the underlying text element.

            var retryCount = 0;

            m_TextField.schedule.Execute(() =>
            {
                if (m_TextField.panel == null)
                    return;

                m_TextField.schedule.Execute(() =>
                {
                    m_InputTextElement.Focus();
                    m_TextField.SelectAll();
                }).StartingIn(0);

            }).Until(() => m_TextField.panel != null && retryCount++ < 10);
        }

        void OnEntryAdded(string newValue)
        {
            if (string.IsNullOrWhiteSpace(newValue) || m_Values.Contains(newValue))
                return;

            m_Values.Add(newValue);

            var chip = EditChipCreator(newValue);
            if (chip != null)
                m_ChipContainer.Add(chip);

            m_TextField.value = string.Empty;

            // Fire event last: handlers may synchronously call UpdateChips which clears
            // and rebuilds m_ChipContainer, so the chip must already be in the container
            // (or it will be rebuilt from data). Firing earlier caused duplicate chips.
            ChipAdded?.Invoke(newValue);
        }

        void OnEntryRemoved(string value)
        {
            if (!m_Values.Remove(value))
                return;

            ChipRemoved?.Invoke(value);

            foreach (var child in m_ChipContainer.Children().ToList())
            {
                if (child is Chip chip && chip.Text == value)
                {
                    chip.RemoveFromHierarchy();
                    break;
                }
            }
        }

        Chip EditChipCreator(string chipText)
        {
            var chip = new Chip(chipText, isDismissable:true);
            chip.ChipDismissed += OnEntryRemoved;

            return chip;
        }

        static Chip CreateMixedValueChip()
        {
            var chip = new Chip("— Mixed", isDismissable: false);
            chip.style.unityFontStyleAndWeight = FontStyle.Italic;
            return chip;
        }
    }
}
