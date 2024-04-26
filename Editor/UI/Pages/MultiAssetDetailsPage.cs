using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class MultiAssetDetailsPage : SelectionInspectorPage
    {
        static readonly string k_InspectorScrollviewContainerClassName = "inspector-page-content-container";
        static readonly string k_MultiSelectionFoldoutClassName = "multi-selection-foldout";
        static readonly string k_MultiSelectionListViewClassName = "multi-selection-listview";
        static readonly string k_MultiSelectionLoadingLabelClassName = "multi-selection-loading-label";
        static readonly string k_MultiSelectionFoldoutTitle = "Selected Assets Files";
        static readonly string k_MultiSelectionFoldoutExpandedClassName = "multi-selection-foldout-expanded";
        static readonly string k_BigButtonClassName = "big-button";
        static readonly string k_MultiSelectionImportName = "multi-selection-import-button";
        static readonly string k_MultiSelectionRemoveName = "multi-selection-remove-button";
        static readonly string k_InspectorFooterContainerName = "footer-container";
        
        List<IAssetData> m_SelectedAssetsData = new();
        MultiSelectionFoldout m_MultiSelectionFoldout;
        Button m_ImportButton;
        Button m_RemoveImportButton;
        VisualElement m_FooterContainer;
        OperationProgressBar m_OperationProgressBar;

        public MultiAssetDetailsPage(IAssetImporter assetImporter, IAssetOperationManager assetOperationManager,
            IStateManager stateManager, IPageManager pageManager, IAssetDataManager assetDataManager,
            IAssetDatabaseProxy assetDatabaseProxy, IProjectOrganizationProvider projectOrganizationProvider,
            ILinksProxy linksProxy, IProjectIconDownloader projectIconDownloader,
            IPermissionsManager permissionsManager)
            : base(assetImporter, assetOperationManager, stateManager, pageManager, assetDataManager,
                assetDatabaseProxy, projectOrganizationProvider, linksProxy, projectIconDownloader, permissionsManager)
        {
            BuildUxmlDocument();
        }

        protected sealed override void BuildUxmlDocument()
        {
            base.BuildUxmlDocument();

            var container = m_ScrollView.Q<VisualElement>(k_InspectorScrollviewContainerClassName);
            m_MultiSelectionFoldout = new MultiSelectionFoldout(container, k_MultiSelectionFoldoutClassName, 
                k_MultiSelectionListViewClassName, k_MultiSelectionLoadingLabelClassName,k_MultiSelectionFoldoutTitle, k_MultiSelectionFoldoutExpandedClassName);
            
            m_MultiSelectionFoldout.RegisterValueChangedCallback(_ =>
            {
                m_StateManager.MultiSelectionFoldoutValue = m_MultiSelectionFoldout.Expanded;
                RefreshScrollView();
            });
            m_MultiSelectionFoldout.Expanded = m_StateManager.MultiSelectionFoldoutValue;

            m_FooterContainer = this.Q<VisualElement>(k_InspectorFooterContainerName);
            m_OperationProgressBar = new OperationProgressBar(() =>
            {
                m_AssetImporter.CancelBulkImport(m_SelectedAssetsData.Select(x => x.Identifier).ToList(), true);
            });
            m_FooterContainer.contentContainer.hierarchy.Add(m_OperationProgressBar);
            
            m_ImportButton = new Button
            {
                text = L10n.Tr(Constants.ImportAllActionText),
                name = k_MultiSelectionImportName
            };
            m_ImportButton.AddToClassList(k_BigButtonClassName);
            m_FooterContainer.contentContainer.hierarchy.Add(m_ImportButton);

            m_RemoveImportButton = new Button
            {
                text = L10n.Tr(Constants.RemoveAllFromProjectActionText),
                name = k_MultiSelectionRemoveName
            };
            m_RemoveImportButton.AddToClassList(k_BigButtonClassName);
            m_FooterContainer.contentContainer.hierarchy.Add(m_RemoveImportButton);
            
            m_ImportButton.clicked += ImportAllAsync;
            m_RemoveImportButton.clicked += RemoveAllFromProject;

            // We need to manually refresh once to make sure the UI is updated when the window is opened.
            if(m_PageManager.ActivePage == null)
                return;
            
            m_SelectedAssetsData = m_AssetDataManager.GetAssetsData(m_PageManager.ActivePage.SelectedAssets);
            RefreshUI();
        }

        protected override async Task SelectAssetDataAsync(List<IAssetData> assetData)
        {
            m_SelectedAssetsData = assetData;
            RefreshUI();
            
            await Task.CompletedTask; // Remove warning about async
        }

        protected override void OnOperationProgress(AssetDataOperation operation)
        {
            
            if(!UIElementsUtils.IsDisplayed(this) || m_SelectedAssetsData == null || !m_SelectedAssetsData.Any() || !m_SelectedAssetsData.Any(x => x.Identifier.Equals(operation.AssetId)))
                return;
            
            m_OperationProgressBar.Refresh(operation);
            
            RefreshUI();
        }

        protected override void OnOperationFinished(AssetDataOperation operation)
        {
            if(!UIElementsUtils.IsDisplayed(this) || m_SelectedAssetsData == null || !m_SelectedAssetsData.Any() || !m_SelectedAssetsData.Any(x => x.Identifier.Equals(operation.AssetId)))
                return;
            
            RefreshUI();
        }

        protected override void OnImportedAssetInfoChanged(AssetChangeArgs args)
        {
            if (!UIElementsUtils.IsDisplayed(this))
                return;
            
            var last = m_SelectedAssetsData.Last();
            foreach (var assetData in m_SelectedAssetsData)
            {
                if (args.Added.Concat(args.Updated).Concat(args.Removed)
                    .Any(a => a.Equals(assetData?.Identifier)))
                {
                    break;
                }
            
                if (assetData.Equals(last))
                    return;
            }
            
            // In case of an import, force a full refresh of the displayed information
            _ = SelectAssetDataAsync(m_SelectedAssetsData);
        }

        protected override void OnAssetDataChanged(AssetChangeArgs args)
        {
            RefreshUI();
        }

        bool UpdateVisibility()
        {
            if (m_SelectedAssetsData.Count <= 1)
            {
                UIElementsUtils.Hide(this);
                return false;
            }

            UIElementsUtils.Show(this);
            return true;
        }

        void RefreshUI()
        {
            if ( m_SelectedAssetsData == null || !UpdateVisibility())
                return;

            m_TitleLabel.text = L10n.Tr(m_SelectedAssetsData.Count + " " + Constants.AssetsSelectedTitle);
            RefreshFoldoutUI();
            
            foreach (var assetData in m_SelectedAssetsData)
            {
                var operation = m_AssetOperationManager.GetAssetOperation(assetData.Identifier);
                //RefreshButtons(assetData, operation); // TODO: Refresh Buttons UIs
                m_OperationProgressBar.Refresh(operation);
            }
        }
        
        void RefreshFoldoutUI()
        {
            m_MultiSelectionFoldout.StartPopulating();
            if (m_SelectedAssetsData.Any())
            {
                m_MultiSelectionFoldout.Populate(null, m_SelectedAssetsData);
            }
            else
            {
                m_MultiSelectionFoldout.Clear();
            }
            m_MultiSelectionFoldout.StopPopulating();
            m_MultiSelectionFoldout.RefreshFoldoutStyleBasedOnExpansionStatus();
        }
        
        void ImportAllAsync()
        {
            // TODO: Re Implement analytics
            try
            {
                m_AssetImporter.StartImportAsync(
                    m_PageManager.ActivePage.SelectedAssets.Select(x => m_AssetDataManager.GetAssetData(x)).ToList());
            }
            catch (Exception e)
            {
               Debug.LogException(e);
            }
        }
        
        void RemoveAllFromProject()
        {
            // TODO: Re Implement analytics
            try
            {
                m_AssetImporter.RemoveBulkImport(m_SelectedAssetsData.Select(x => x.Identifier).ToList(), true);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}