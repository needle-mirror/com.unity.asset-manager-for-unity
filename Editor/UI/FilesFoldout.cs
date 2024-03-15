using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class FilesFoldout : ItemFoldout<IAssetDataFile, DetailsPageFileItem>
    {
        readonly IAssetDataManager m_AssetDataManager;
        readonly IPageManager m_PageManager;
        readonly IAssetImporter m_AssetImporter;
        readonly IAssetDatabaseProxy m_AssetDatabaseProxy;

        List<string> m_FilesList = new();

        public FilesFoldout(VisualElement parent, string foldoutName, string listViewName, string loadingLabelName,
            IAssetDataManager assetDataManager, IPageManager pageManager, IAssetImporter assetImporter,
            IAssetDatabaseProxy assetDatabaseProxy)
            : base(parent, foldoutName, listViewName, loadingLabelName)
        {
            m_AssetDataManager = assetDataManager;
            m_PageManager = pageManager;
            m_AssetImporter = assetImporter;
            m_AssetDatabaseProxy = assetDatabaseProxy;
        }

        public override void Clear()
        {
            base.Clear();
            m_FilesList.Clear();
        }

        protected override IList PrepareListItem(IEnumerable<IAssetDataFile> items)
        {
            m_FilesList = items.Where(f => !AssetDataDependencyHelper.IsASystemFile(f.path)).OrderBy(f => f.path).Select(f => f.path).ToList();
            return m_FilesList;
        }

        protected override DetailsPageFileItem MakeItem()
        {
            return new DetailsPageFileItem(m_AssetDataManager, m_PageManager, m_AssetImporter, m_AssetDatabaseProxy);
        }

        protected override void BindItem(DetailsPageFileItem element, int index)
        {
            element.Refresh(m_FilesList[index], m_FilesList);
        }
    }
}