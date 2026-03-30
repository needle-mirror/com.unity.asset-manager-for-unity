using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Determines which keyboard shortcut confirms an inline edit.
    /// Escape always cancels regardless of mode.
    /// </summary>
    enum InlineEditConfirmMode
    {
        Enter,
        ModifierEnter,
        ButtonOnly
    }

    /// <summary>
    /// Popup for inline-edit confirmation (checkmark / cancel buttons).
    /// Lives as a permanent child of a high-level container (e.g. scroll content)
    /// so it draws above entries. Positions itself relative to the target using
    /// coordinate math and scrolls naturally with the container.
    /// </summary>
    class InlineEditConfirmationPopupContainer : VisualElement
    {
        static readonly float k_Gap = 2f;
        static readonly float k_RightMargin = 6f;

        VisualElement m_Target;
        Action m_OnConfirm;
        Action m_OnCancel;
        VisualElement m_ConfirmButton;
        VisualElement m_CancelButton;
        bool m_SuppressOutsideClick;
        InlineEditConfirmMode m_ConfirmMode;
        VisualElement m_RegisteredRoot;
        bool m_AlignWithTarget;
        Vector2 m_PositionOffset;
        bool m_PositionPending;

        public InlineEditConfirmationPopupContainer()
        {
            focusable = false;
            UIElementsUtils.Hide(this);

            m_ConfirmButton = CreateButton(UssStyle.InlineEditConfirmationBtnConfirm, L10n.Tr("Confirm"), HandleConfirm);
            m_CancelButton = CreateButton(UssStyle.InlineEditConfirmationBtnCancel, L10n.Tr("Cancel"), HandleCancel);

            var divider = new VisualElement();
            divider.AddToClassList(UssStyle.InlineEditConfirmationDivider);
            divider.pickingMode = PickingMode.Ignore;

            Add(m_ConfirmButton);
            Add(divider);
            Add(m_CancelButton);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        /// <summary>
        /// Hides the popup, clears callbacks, and removes it from the visual tree.
        /// </summary>
        public void Dispose()
        {
            Hide();
            RemoveFromHierarchy();
        }

        static VisualElement CreateButton(string iconClass, string tooltip, Action onClick)
        {
            var wrapper = new VisualElement();
            wrapper.AddToClassList(UssStyle.InlineEditConfirmationBtn);
            wrapper.tooltip = tooltip;
            wrapper.focusable = false;
            wrapper.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                onClick?.Invoke();
            });

            var icon = new VisualElement();
            icon.AddToClassList(UssStyle.InlineEditConfirmationBtnIcon);
            icon.AddToClassList(iconClass);
            icon.pickingMode = PickingMode.Ignore;

            wrapper.Add(icon);
            return wrapper;
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            RegisterCallback<GeometryChangedEvent>(OnResized);
            if (parent != null)
                parent.RegisterCallback<GeometryChangedEvent>(OnResized);
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            UnregisterCallback<GeometryChangedEvent>(OnResized);
            if (parent != null)
                parent.UnregisterCallback<GeometryChangedEvent>(OnResized);
        }

        /// <param name="positionOffset">Optional offset applied to the popup position. x: extra offset from right (positive = further left). y: extra offset from top (positive = further down).</param>
        public void Show(VisualElement target, Action onConfirm, Action onCancel,
            InlineEditConfirmMode confirmMode = InlineEditConfirmMode.Enter,
            bool alignWithTarget = false, Vector2 positionOffset = default)
        {
            if (target?.panel == null || parent == null)
                return;

            if (m_Target != null && m_Target != target)
                HandleCancel();

            m_Target = target;
            m_OnConfirm = onConfirm;
            m_OnCancel = onCancel;
            m_ConfirmMode = confirmMode;
            m_AlignWithTarget = alignWithTarget;
            m_PositionOffset = positionOffset;
            m_SuppressOutsideClick = false;

            UIElementsUtils.Show(this);
            BringToFront();

            UnregisterTargetKeyboard();
            UnregisterGlobalPointerDown();
            RegisterGlobalPointerDown();
            RegisterTargetKeyboard();

            if (m_PositionPending)
            {
                m_PositionPending = false;
                UnregisterCallback<GeometryChangedEvent>(OnPositionGeometryReady);
                m_Target?.UnregisterCallback<GeometryChangedEvent>(OnTargetGeometryReady);
            }

            SchedulePositionUpdate(target);
        }

        public void Hide()
        {
            // Clean up position pending callbacks
            if (m_PositionPending)
            {
                m_PositionPending = false;
                UnregisterCallback<GeometryChangedEvent>(OnPositionGeometryReady);
                m_Target?.UnregisterCallback<GeometryChangedEvent>(OnTargetGeometryReady);
            }

            UnregisterTargetKeyboard();
            UnregisterGlobalPointerDown();
            m_Target = null;
            m_OnConfirm = null;
            m_OnCancel = null;
            m_SuppressOutsideClick = false;
            UIElementsUtils.Hide(this);
        }

        public void SetVisible(bool visible)
        {
            style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// When true, outside clicks will not trigger cancel (e.g. while dropdown menu is open).
        /// </summary>
        public void SetSuppressOutsideClick(bool suppress)
        {
            m_SuppressOutsideClick = suppress;
        }

        void SchedulePositionUpdate(VisualElement target)
        {
            if (target == null || parent == null)
                return;

            // If geometry is already resolved, update immediately
            if (target.resolvedStyle.width > 0 && resolvedStyle.width > 0)
            {
                UpdatePosition(target);
                return;
            }

            // Otherwise wait for geometry to be resolved
            m_PositionPending = true;
            RegisterCallback<GeometryChangedEvent>(OnPositionGeometryReady);
            target.RegisterCallback<GeometryChangedEvent>(OnTargetGeometryReady);
        }

        void OnPositionGeometryReady(GeometryChangedEvent evt)
        {
            if (!m_PositionPending || m_Target == null)
                return;

            if (resolvedStyle.width > 0 && m_Target.resolvedStyle.width > 0)
            {
                m_PositionPending = false;
                UnregisterCallback<GeometryChangedEvent>(OnPositionGeometryReady);
                m_Target.UnregisterCallback<GeometryChangedEvent>(OnTargetGeometryReady);
                UpdatePosition(m_Target);
            }
        }

        void OnTargetGeometryReady(GeometryChangedEvent evt)
        {
            if (!m_PositionPending || m_Target == null)
                return;

            if (resolvedStyle.width > 0 && m_Target.resolvedStyle.width > 0)
            {
                m_PositionPending = false;
                UnregisterCallback<GeometryChangedEvent>(OnPositionGeometryReady);
                m_Target.UnregisterCallback<GeometryChangedEvent>(OnTargetGeometryReady);
                UpdatePosition(m_Target);
            }
        }

        void UpdatePosition(VisualElement target)
        {
            if (target == null || parent == null)
                return;

            var worldPos = target.LocalToWorld(Vector2.zero);
            var localPos = parent.WorldToLocal(worldPos);
            var targetRight = localPos.x + target.resolvedStyle.width;
            var rightOffset = parent.resolvedStyle.width - targetRight + k_RightMargin;
            if (rightOffset < 0)
                rightOffset = 0;

            style.right = rightOffset + m_PositionOffset.x;
            style.left = StyleKeyword.Auto;

            float baseTop;
            if (m_AlignWithTarget)
                baseTop = localPos.y + (target.resolvedStyle.height - resolvedStyle.height) / 2f;
            else
                baseTop = localPos.y + target.resolvedStyle.height + k_Gap;
            style.top = baseTop + m_PositionOffset.y;
        }

        void OnResized(GeometryChangedEvent evt)
        {
            if (m_Target != null && !m_PositionPending)
                UpdatePosition(m_Target);
        }

        void RegisterGlobalPointerDown()
        {
            m_RegisteredRoot = panel?.visualTree;
            m_RegisteredRoot?.RegisterCallback<PointerDownEvent>(OnGlobalPointerDown, TrickleDown.TrickleDown);
        }

        void UnregisterGlobalPointerDown()
        {
            m_RegisteredRoot?.UnregisterCallback<PointerDownEvent>(OnGlobalPointerDown, TrickleDown.TrickleDown);
            m_RegisteredRoot = null;
        }

        void OnGlobalPointerDown(PointerDownEvent evt)
        {
            if (m_Target == null || m_SuppressOutsideClick)
                return;

            var clicked = evt.target as VisualElement;
            if (clicked == null)
                return;
            if (clicked == this || Contains(clicked))
                return;
            if (clicked == m_Target || m_Target.Contains(clicked))
                return;

            HandleCancel();
        }

        void RegisterTargetKeyboard()
        {
            m_Target?.RegisterCallback<KeyDownEvent>(OnTargetKeyDown, TrickleDown.TrickleDown);
        }

        void UnregisterTargetKeyboard()
        {
            m_Target?.UnregisterCallback<KeyDownEvent>(OnTargetKeyDown, TrickleDown.TrickleDown);
        }

        void OnTargetKeyDown(KeyDownEvent evt)
        {
            if (m_SuppressOutsideClick)
                return;

            if (evt.keyCode == KeyCode.Escape)
            {
                HandleCancel();
                evt.StopPropagation();
                evt.PreventDefault();
                return;
            }

            if (evt.keyCode is not (KeyCode.Return or KeyCode.KeypadEnter))
                return;

            switch (m_ConfirmMode)
            {
                case InlineEditConfirmMode.Enter:
                    HandleConfirm();
                    evt.StopPropagation();
                    evt.PreventDefault();
                    break;
                case InlineEditConfirmMode.ModifierEnter when evt.ctrlKey || evt.commandKey:
                    HandleConfirm();
                    evt.StopPropagation();
                    evt.PreventDefault();
                    break;
            }
        }

        void HandleConfirm()
        {
            var onConfirm = m_OnConfirm;
            Hide();
            onConfirm?.Invoke();
        }

        void HandleCancel()
        {
            var onCancel = m_OnCancel;
            Hide();
            onCancel?.Invoke();
        }
    }
}
