using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using Unity.AssetManager.Upload.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class AssetDependenciesComponent : VisualElement
    {
        readonly DependenciesFoldout m_DependenciesFoldout;

        public AssetDependenciesComponent(VisualElement parent, IPageManager pageManager, IPopupManager popupManager,
            ISettingsManager settingsManager, IProjectOrganizationProvider projectOrganizationProvider, IStateManager stateManager = null)
        {
            var dependenciesContainer = new VisualElement
            {
                name = "dependencies-container",
                style =
                {
                    flexGrow = 1
                }
            };

            var unityConnectProxy = ServicesContainer.instance.Get<IUnityConnectProxy>();
            var assetDataManager = ServicesContainer.instance.Get<IAssetDataManager>();

            parent.Add(dependenciesContainer);
            m_DependenciesFoldout =
                new DependenciesFoldout(dependenciesContainer, Constants.DependenciesText, pageManager, popupManager, settingsManager, projectOrganizationProvider, unityConnectProxy, assetDataManager)
                {
                    Expanded = stateManager?.DependenciesFoldoutValue ?? false
                };
            m_DependenciesFoldout.SetEmptyStateLabel(Constants.NoDependenciesText, "no-dependencies-label");

            if (stateManager != null)
            {
                m_DependenciesFoldout.RegisterValueChangedCallback(value =>
                {
                    stateManager.DependenciesFoldoutValue = value;
                });
            }
        }

        public void RefreshUI(BaseAssetData assetData, bool isLoading = false)
        {
            if (isLoading)
            {
                m_DependenciesFoldout.StartPopulating();
                return;
            }

            RefreshDependenciesInformationUI(assetData);
            m_DependenciesFoldout.RefreshFoldoutStyleBasedOnExpansionStatus();
        }

        void RefreshDependenciesInformationUI(BaseAssetData assetData)
        {
            var dependencies = assetData.Dependencies.ToList();
            m_DependenciesFoldout.Populate(assetData, dependencies);
            m_DependenciesFoldout.StopPopulating();
        }
    }
}
