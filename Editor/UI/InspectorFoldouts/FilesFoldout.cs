using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
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
        readonly Chip m_UVCSChip;

        public FilesFoldout(VisualElement parent, string foldoutName, string listViewName,
            IAssetDatabaseProxy assetDatabaseProxy, string foldoutTitle = null)
            : base(parent, foldoutName, listViewName, foldoutTitle)
        {
            m_AssetDatabaseProxy = assetDatabaseProxy;
            SelectionChanged += TryPingItem;

            m_UVCSChip = new Chip("VCS");
            m_UVCSChip.AddToClassList("details-files-foldout-uvcs-chip");
            m_UVCSChip.tooltip = L10n.Tr(Constants.VCSChipTooltip);
            var icon = new VisualElement();
            icon.AddToClassList("details-files-foldout-uvcs-chip-icon");
            m_UVCSChip.Add(icon);

            UIElementsUtils.Hide(m_UVCSChip);

            var foldout = parent.Q<Foldout>();
            var toggle = foldout.Q<Toggle>();
            toggle.Add(m_UVCSChip);
        }

        public override void Clear()
        {
            base.Clear();
            m_FilesList.Clear();
            UIElementsUtils.Hide(m_UVCSChip);
        }

        protected override IList PrepareListItem(BaseAssetData assetData, IEnumerable<BaseAssetDataFile> items)
        {
            m_FilesList = new List<FileItem>();

            var dataset = assetData.Datasets.FirstOrDefault(d => d.Files.Exists(items.Contains));
            if (dataset != null)
            {
                UIElementsUtils.SetDisplay(m_UVCSChip, dataset.IsSourceControlled);
            }

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
