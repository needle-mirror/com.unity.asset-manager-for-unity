using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class FileFoldoutComponent
    {
        readonly VisualElement m_Container;
        readonly IStateManager m_StateManager;
        readonly IAssetDatabaseProxy m_AssetDatabaseProxy;

        List<FilesFoldout> m_FilesFoldouts;

        public FileFoldoutComponent(VisualElement container, IStateManager stateManager, IAssetDatabaseProxy assetDatabaseProxy)
        {
            m_Container = container;
            m_StateManager = stateManager;
            m_AssetDatabaseProxy = assetDatabaseProxy;

            m_FilesFoldouts = new List<FilesFoldout>();
        }

        public void RefreshSourceFilesInformationUI(IEnumerable<AssetDataset> datasets, BaseAssetData selectedAsset)
        {
            ClearFoldouts();

            var foldouts = new List<FilesFoldout>();
            foreach (var dataset in datasets)
            {
                if (!CreateFileFoldout(selectedAsset, dataset, out var filesFoldout)) continue;

                foldouts.Add(filesFoldout);

                var datasetFiles = dataset.Files.Where(f =>
                {
                    if (string.IsNullOrEmpty(f?.Path))
                        return false;

                    return !AssetDataDependencyHelper.IsASystemFile(Path.GetExtension(f.Path));
                });

                if (datasetFiles.Any())
                {
                    filesFoldout.Populate(selectedAsset, datasetFiles);
                }
                else
                {
                    filesFoldout.Clear();
                }

                filesFoldout.StopPopulating();
            }

            m_FilesFoldouts = foldouts.ToList();

            foreach (var foldout in m_FilesFoldouts)
            {
                foldout.RefreshFoldoutStyleBasedOnExpansionStatus();
            }
        }

        void ClearFoldouts()
        {
            m_Container.Clear();
        }

        bool CreateFileFoldout(BaseAssetData assetData,AssetDataset assetDataset, out FilesFoldout filesFoldout)
        {
            filesFoldout = null;

            if (!assetDataset.CanBeImported) return filesFoldout != null;

            var fileFoldoutViewModel = new FileFoldoutViewModel(assetData,assetDataset, m_AssetDatabaseProxy);
            filesFoldout = new FilesFoldout(m_Container, fileFoldoutViewModel)
            {
                Expanded = m_StateManager.GetFilesFoldoutValue(assetDataset.Name)
            };

            filesFoldout.RegisterValueChangedCallback(value =>
            {
                m_StateManager.SetFilesFoldoutValue(assetDataset.Name, value);
            });

            return filesFoldout != null;
        }
    }
}
