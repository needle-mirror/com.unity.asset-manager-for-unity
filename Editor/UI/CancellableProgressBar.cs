using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class CancellableProgressBar : VisualElement
    {
        const string k_UssClassName = "cancellable-progress-bar";
        const string k_ProgressBarClassName = k_UssClassName + "--foreground";
        const string k_CancelButtonClassName = k_UssClassName + "--cancel-button";

        VisualElement m_ProgressBar;
        Button m_CancelButton;

        public event Action onCancel;

        /// <summary>
        /// Allows the visual indication of progress to be set and updated
        /// </summary>
        public float Progress
        {
            set { m_ProgressBar.style.width = new Length(Mathf.Clamp01(value) * 100f, LengthUnit.Percent); }
        }

        public CancellableProgressBar()
        {
            AddToClassList(k_UssClassName);
            pickingMode = PickingMode.Ignore;

            m_ProgressBar = new VisualElement();
            m_ProgressBar.AddToClassList(k_ProgressBarClassName);
            m_ProgressBar.style.width = new Length(0, LengthUnit.Percent);
            m_ProgressBar.pickingMode = PickingMode.Ignore;

            m_CancelButton = new Button();
            m_CancelButton.RegisterCallback<ClickEvent>(OnClickEvent);
            m_CancelButton.AddToClassList(k_CancelButtonClassName);
            m_CancelButton.style.backgroundImage = EditorGUIUtility.Load(EditorGUIUtility.isProSkin ? "d_winbtn_win_close_a" : "winbtn_win_close_a") as Texture2D;

            Add(m_ProgressBar);
            Add(m_CancelButton);
        }

        void OnClickEvent(ClickEvent clickEvent)
        {
            clickEvent.StopImmediatePropagation();
            onCancel?.Invoke();
        }
    }
}
