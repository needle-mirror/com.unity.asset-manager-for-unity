using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class DetailsPageDependencyItem : VisualElement
    {
        const string k_DetailsPageFileItemUssStyle = "details-page-dependency-item";
        const string k_DetailsPageFileIconItemUssStyle = "details-page-dependency-item-icon";
        const string k_DetailsPageFileLabelItemUssStyle = "details-page-dependency-item-label";

        readonly Button m_Button;
        readonly VisualElement m_Icon;
        readonly Label m_FileName;

        AssetIdentifier m_AssetIdentifier;
        Task<IAssetData> m_FetchingTask;

        CancellationTokenSource m_CancellationTokenSource;

        public DetailsPageDependencyItem(IPageManager pageManager)
        {
            m_Button = new Button(() =>
            {
                if (m_AssetIdentifier != null)
                {
                    pageManager.activePage.selectedAssetId = m_AssetIdentifier;
                }
            });

            Add(m_Button);
            m_Button.AddToClassList(k_DetailsPageFileItemUssStyle);

            m_FileName = new Label("");
            m_Icon = new VisualElement();

            m_Icon.AddToClassList(k_DetailsPageFileIconItemUssStyle);
            m_FileName.AddToClassList(k_DetailsPageFileLabelItemUssStyle);

            m_Button.Add(m_Icon);
            m_Button.Add(m_FileName);
            m_Button.SetEnabled(false);

            m_CancellationTokenSource = new CancellationTokenSource();
        }

        public async Task Refresh(DependencyAsset dependencyAsset)
        {
            m_FileName.text = "Loading...";
            m_Icon.style.backgroundImage = null;

            m_Button.SetEnabled(false);

            if (m_FetchingTask is { IsCompleted: false })
            {
                m_CancellationTokenSource.Cancel();
            }

            m_FetchingTask = FetchAssetData(dependencyAsset.Identifier, m_CancellationTokenSource.Token);

            IAssetData assetData = null;

            try
            {
                assetData = await m_FetchingTask;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                m_FetchingTask = null;
            }

            m_Icon.style.backgroundImage = AssetDataTypeHelper.GetIconForFile(assetData?.primaryExtension);

            m_AssetIdentifier = assetData?.identifier;

            m_FileName.text = assetData != null ? assetData.name : $"{dependencyAsset.Identifier.assetId} (unavailable)";

            m_Button.SetEnabled(m_AssetIdentifier != null);
        }

        static async Task<IAssetData> FetchAssetData(AssetIdentifier assetIdentifier, CancellationToken token)
        {
            var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();

            if (token.IsCancellationRequested)
                return null;

            var assetData = await assetDataManager.GetOrSearchAssetData(assetIdentifier, token);

            if (token.IsCancellationRequested)
                return null;

            if (assetData != null && string.IsNullOrEmpty(assetData.primaryExtension))
            {
                await assetData.ResolvePrimaryExtensionAsync(null, token);
            }

            return assetData;
        }
    }
}
