using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class FilesFoldout : ItemFoldout<BaseAssetDataFile, DetailsPageFileItem>
    {
        readonly IAssetDatabaseProxy m_AssetDatabaseProxy;

        class FileItem
        {
            public string Filename => AssetDataFile.Path;
            public string Guid { get; }
            public bool Uploaded => AssetDataFile.Available;

            public BaseAssetData AssetData { get; }
            public BaseAssetDataFile AssetDataFile { get; }

            public FileItem(BaseAssetData assetData, BaseAssetDataFile assetDataFile)
            {
                AssetDataFile = assetDataFile;
                AssetData = assetData;

                var guid = assetDataFile.Guid;
                if (string.IsNullOrEmpty(guid))
                {
                    var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();
                    guid = assetDataManager.GetImportedFileGuid(assetData?.Identifier, assetDataFile.Path);
                }

                Guid = guid;
            }
        }

        List<FileItem> m_FilesList = new();

        public FilesFoldout(VisualElement parent, string foldoutName, string listViewName,
            IAssetDatabaseProxy assetDatabaseProxy, string foldoutTitle = null)
            : base(parent, foldoutName, listViewName, foldoutTitle)
        {
            m_AssetDatabaseProxy = assetDatabaseProxy;
            SelectionChanged += TryPingItem;
        }

        public override void Clear()
        {
            base.Clear();
            m_FilesList.Clear();
        }

        protected override IList PrepareListItem(BaseAssetData assetData, IEnumerable<BaseAssetDataFile> items)
        {
            m_FilesList = new List<FileItem>();

            foreach (var assetDataFile in items.OrderBy(f => f.Path))
            {
                if (AssetDataDependencyHelper.IsASystemFile(assetDataFile.Path))
                    continue;

                m_FilesList.Add(new FileItem(assetData, assetDataFile));
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

            var removable = fileItem.AssetData.CanRemovedFile(fileItem.AssetDataFile);

            element.Refresh(fileItem.Filename, fileItem.Guid, enabled, fileItem.Uploaded, removable);
            element.RemoveClicked = () =>
            {
                fileItem.AssetData.RemoveFile(fileItem.AssetDataFile);
            };
        }

        void TryPingItem(IEnumerable<object> items)
        {
            var fileItems = items.OfType<FileItem>();
            var firstPingableItem = fileItems.FirstOrDefault(fileItem => fileItem.Guid != null);

            if (firstPingableItem != null)
            {
                m_AssetDatabaseProxy.PingAssetByGuid(firstPingableItem.Guid);
            }
        }
    }
}
