using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class TabView : VisualElement
    {
        static readonly string k_TabButtonUssClassName = "tab-button";
        static readonly string k_TabButtonSelectedUssClassName = "tab-button--selected";
        static readonly string k_TabButtonLabelUssClassName = "tab-button-label";
        static readonly string k_TabButtonUnderscoreBarUssClassName = "tab-button-underscore-bar";

        readonly Dictionary<System.Type, VisualElement> m_TabButtons = new();

        readonly IPageManager m_PageManager;
        readonly IUnityConnectProxy m_UnityConnectProxy;

        public TabView(IPageManager pageManager, IUnityConnectProxy unityConnectProxy)
        {
            m_PageManager = pageManager;
            m_UnityConnectProxy = unityConnectProxy;
            
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        public void AddPage<T>(string tabLabel) where T : IPage
        {
            var button = new VisualElement();
            button.AddToClassList(k_TabButtonUssClassName);

            var label = new Label(tabLabel);
            label.AddToClassList(k_TabButtonLabelUssClassName);
            button.Add(label);

            var underscoreBar = new VisualElement();
            underscoreBar.AddToClassList(k_TabButtonUnderscoreBarUssClassName);
            button.Add(underscoreBar);

            Add(button);
            m_TabButtons.Add(typeof(T), button);
        }

        public void MergePage<T, U>() where T : IPage where U : IPage
        {
            if (m_TabButtons.TryGetValue(typeof(T), out var button))
            {
                m_TabButtons.Add(typeof(U), button);
            }
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_PageManager.ActivePageChanged += OnActivePageChanged;
            m_UnityConnectProxy.OnCloudServicesReachabilityChanged += OnCloudServicesReachabilityChanged;

            foreach (var pair in m_TabButtons)
            {
                var type = pair.Key;
                pair.Value.RegisterCallback(OnClick(type));
            }

            var activePage = m_PageManager.ActivePage;
            if (activePage != null)
            {
                Refresh(activePage);
            }
            else
            {
                m_PageManager.SetActivePage<CollectionPage>();
            }

            OnCloudServicesReachabilityChanged(m_UnityConnectProxy.AreCloudServicesReachable);
        }

        private EventCallback<ClickEvent> OnClick(Type type)
        {
            return _ =>
            {
                var methodName = nameof(m_PageManager.SetActivePage);
                var method = m_PageManager.GetType().GetMethod(methodName);
                if (method != null)
                {
                    var genericMethod = method.MakeGenericMethod(type);
                    genericMethod.Invoke(m_PageManager, new object[] { false });
                }
            };
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_PageManager.ActivePageChanged -= OnActivePageChanged;
            m_UnityConnectProxy.OnCloudServicesReachabilityChanged -= OnCloudServicesReachabilityChanged;
            
            foreach (var pair in m_TabButtons)
            {
                var type = pair.Key;
                pair.Value.UnregisterCallback(OnClick(type));
            }
        }

        void OnActivePageChanged(IPage page)
        {
            Refresh(page);
        }

        void OnCloudServicesReachabilityChanged(bool isReachable)
        {
            foreach (var keyValuePair in m_TabButtons)
            {
                if (keyValuePair.Key != typeof(InProjectPage))
                {
                    keyValuePair.Value.SetEnabled(isReachable);
                }
            }
        }

        void Refresh(IPage page)
        {
            if(page == null)
                return;

            foreach (var tabButton in m_TabButtons.Values)
            {
                tabButton.RemoveFromClassList(k_TabButtonSelectedUssClassName);
            }

            if (m_TabButtons.TryGetValue(page.GetType(), out var button))
            {
                button.AddToClassList(k_TabButtonSelectedUssClassName);
            }
        }
    }
}
