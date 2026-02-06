using System;
using Unity.AssetManager.Core.Editor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class DependenciesFoldout : ItemFoldout<AssetIdentifier, DependencyFoldoutItem>
    {
        readonly IPageManager m_PageManager;
        readonly IPopupManager m_PopupManager;
        readonly ISettingsManager m_SettingsManager;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly IUnityConnectProxy m_UnityConnectProxy;
        readonly IAssetDataManager m_AssetDataManager;

        public DependenciesFoldout(VisualElement parent, string foldoutTitle, IPageManager pageManager,
            IPopupManager popupManager, ISettingsManager settingsManager,
            IProjectOrganizationProvider projectOrganizationProvider, IUnityConnectProxy unityConnectProxy,
            IAssetDataManager assetDataManager)
            : base(parent, foldoutTitle, "dependencies-foldout", "dependencies-list", "details-files-foldout",
                "details-files-list")
        {
            m_PageManager = pageManager;
            m_PopupManager = popupManager;
            m_SettingsManager = settingsManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_UnityConnectProxy = unityConnectProxy;
            m_AssetDataManager = assetDataManager;
        }

        protected override DependencyFoldoutItem MakeItem()
        {
            var viewModel = new DependencyFoldoutItemViewModel(m_PageManager, m_SettingsManager,
                m_ProjectOrganizationProvider, m_UnityConnectProxy, m_AssetDataManager);
            return new DependencyFoldoutItem(viewModel, m_PopupManager);
        }

        protected override void BindItem(DependencyFoldoutItem element, int index)
        {
            var dependency = (AssetIdentifier)Items[index];
            TaskUtils.TrackException(element.Bind(dependency));
        }
    }
}
