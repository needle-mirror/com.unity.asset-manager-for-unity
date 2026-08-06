using System;
using System.Collections;
using System.Collections.Generic;
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

        BaseAssetData m_OwnerAssetData;

        /// <summary>
        /// Raised when a dependency row changed its version or version label, carrying the owning asset's
        /// complete dependency set.
        /// </summary>
        public event Action<IEnumerable<AssetIdentifier>> DependenciesEdited;

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

        protected override IList PrepareListItem(BaseAssetData assetData, IEnumerable<AssetIdentifier> items)
        {
            m_OwnerAssetData = assetData;
            return base.PrepareListItem(assetData, items);
        }

        protected override DependencyFoldoutItem MakeItem()
        {
            var viewModel = new DependencyFoldoutItemViewModel(m_PageManager, m_SettingsManager,
                m_ProjectOrganizationProvider, m_UnityConnectProxy, m_AssetDataManager, () => m_OwnerAssetData);
            viewModel.DependenciesEdited += dependencies => DependenciesEdited?.Invoke(dependencies);
            return new DependencyFoldoutItem(viewModel, m_PopupManager);
        }

        protected override void BindItem(DependencyFoldoutItem element, int index)
        {
            var dependency = (AssetIdentifier)Items[index];
            TaskUtils.TrackException(element.Bind(dependency));
        }
    }
}
