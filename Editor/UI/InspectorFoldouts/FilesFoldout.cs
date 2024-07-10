using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class FilesFoldout : ItemFoldout<IAssetDataFile, DetailsPageFileItem>
    {
        readonly IAssetDataManager m_AssetDataManager;
        readonly IAssetDatabaseProxy m_AssetDatabaseProxy;

        class FileItem
        {
            public string Filename { get; }
            public string Guid { get; }
            public bool Uploaded { get; }

            public FileItem(string filename, string guid, bool uploaded)
            {
                Filename = filename;
                Guid = guid;
                Uploaded = uploaded;
            }
        }

        List<FileItem> m_FilesList = new();

        public FilesFoldout(VisualElement parent, string foldoutName, string listViewName,
            IAssetDataManager assetDataManager, IAssetDatabaseProxy assetDatabaseProxy, string foldoutTitle = null)
            : base(parent, foldoutName, listViewName, foldoutTitle)
        {
            m_AssetDataManager = assetDataManager;
            m_AssetDatabaseProxy = assetDatabaseProxy;
        }

        public override void Clear()
        {
            base.Clear();
            m_FilesList.Clear();
        }

        protected override IList PrepareListItem(IAssetData assetData, IEnumerable<IAssetDataFile> items)
        {
            m_FilesList = new List<FileItem>();

            foreach (var assetDataFile in items.OrderBy(f => f.Path))
            {
                if (AssetDataDependencyHelper.IsASystemFile(assetDataFile.Path))
                    continue;

                var guid = assetDataFile.Guid;
                if (string.IsNullOrEmpty(guid))
                {
                    guid = GetFileGuid(assetData, assetDataFile.Path);
                }

                m_FilesList.Add(new FileItem(assetDataFile.Path, guid, assetDataFile.Available));
            }

            return m_FilesList;
        }

        protected override DetailsPageFileItem MakeItem()
        {
            return new DetailsPageFileItem(m_AssetDatabaseProxy);
        }

        protected override void BindItem(DetailsPageFileItem element, int index)
        {
            var fileItem = m_FilesList[index];

            var enabled = !MetafilesHelper.IsOrphanMetafile(fileItem.Filename, m_FilesList.Select(f => f.Filename).ToList());

            element.Refresh(fileItem.Filename, fileItem.Guid, enabled, fileItem.Uploaded);
        }

        string GetFileGuid(IAssetData assetData, string filename)
        {
            if (assetData == null)
                return null;

            var importedInfo = m_AssetDataManager.GetImportedAssetInfo(assetData.Identifier);
            var normalizedFilename = Utilities.NormalizePathSeparators(filename);
            var importedFileInfo = importedInfo?.FileInfos?.Find(f => Utilities.NormalizePathSeparators(f.OriginalPath).Equals(normalizedFilename));

            return importedFileInfo?.Guid;
        }
    }
}
