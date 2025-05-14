using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEditor;

namespace Unity.AssetManager.UI.Editor
{
    static class OpenInAssetManagerHook
    {
        static AssetManagerWindowHook s_AssetManagerWindowHook = new ();

        [MenuItem("Assets/Show in Asset Manager", false, 22)]
        static void OpenInAssetManagerMenuItem()
        {
            s_AssetManagerWindowHook.OrganizationLoaded += LoadInProjectPage;
            s_AssetManagerWindowHook.OpenAssetManagerWindow();
        }

        [MenuItem("Assets/Show in Asset Manager", true, 22)]
        static bool OpenInAssetManagerMenuItemValidation()
        {
            if (Selection.assetGUIDs.Length == 0 || Selection.activeObject == null)
                return false;

            var assetData = GetAssetData(Selection.assetGUIDs);
            return assetData != null && assetData.Any();
        }

        static void LoadInProjectPage()
        {
            s_AssetManagerWindowHook.OrganizationLoaded -= LoadInProjectPage;

            AssetManagerWindow.Instance.Focus();

            var projectOrganizationProvider = ServicesContainer.instance.Resolve<IProjectOrganizationProvider>();
            if (string.IsNullOrEmpty(projectOrganizationProvider.SelectedOrganization?.Id))
                return;

            var pageManager = ServicesContainer.instance.Resolve<IPageManager>();
            if (pageManager.ActivePage is not InProjectPage)
                pageManager.SetActivePage<InProjectPage>();

            var inProjectPage = pageManager.ActivePage as InProjectPage;
            if (inProjectPage == null)
                return;

            var assetDatas = GetAssetData(Selection.assetGUIDs);
            if (assetDatas == null || !assetDatas.Any())
                return;

            inProjectPage.ClearSelection();
            inProjectPage.SelectAssets(assetDatas.Select(asset => asset.Identifier).ToArray());
        }

        static IEnumerable<BaseAssetData> GetAssetData(string[] guids)
        {
            var assetDatas = new List<BaseAssetData>();

            foreach (var guid in guids)
            {
                var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();
                var importedAssetInfos = assetDataManager.GetImportedAssetInfosFromFileGuid(guid);
                if (importedAssetInfos == null || importedAssetInfos.Count == 0)
                    continue;

                assetDatas.AddRange(importedAssetInfos.Select(info => info.AssetData));
            }

            return assetDatas;
        }
    }
}
