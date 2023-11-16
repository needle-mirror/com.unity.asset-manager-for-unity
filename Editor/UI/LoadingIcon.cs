using System;
using System.Collections;
using Unity.EditorCoroutines.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class LoadingIcon : VisualElement
    {
        const string k_StyleSheetClassName = "loading-icon";

        // Height must be set programmatically for rotation of icon
        const int k_Height = 20;
        const int k_Width = k_Height;

        const float k_TimeBetweenFrames = 0.05f;

        static Quaternion s_LastRotation;
        static Vector3 s_LastPosition;
        static float s_LastAngle;
        float m_CurrentAngle;
        bool m_Interrupt;

        internal LoadingIcon()
        {
            AddToClassList(k_StyleSheetClassName);

            style.height = k_Height;
            style.width = k_Width;

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            EditorCoroutineUtility.StartCoroutineOwnerless(NextFrame());
        }

        IEnumerator NextFrame()
        {
            while (!m_Interrupt)
            {
                
                transform.rotation = Quaternion.Euler(0, 0, m_CurrentAngle);
                m_CurrentAngle += 20;
                yield return new EditorWaitForSeconds(k_TimeBetweenFrames);
            }
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            transform.rotation = s_LastRotation.normalized;
            transform.position = s_LastPosition;
            m_CurrentAngle = s_LastAngle;
        }

        void OnDetachFromPanel(DetachFromPanelEvent e)
        {
            s_LastRotation = transform.rotation;
            s_LastPosition = transform.position;
            s_LastAngle = m_CurrentAngle;
            m_Interrupt = true;
        }
    }
}
