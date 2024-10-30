using System;
using System.Threading;
using System.Threading.Tasks;
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

        CancellationTokenSource m_CancellationTokenSource;

        public DetailsPageDependencyItem(IPageManager pageManager)
        {
            m_Button = new Button(() =>
            {
                if (m_AssetIdentifier != null)
                {
                    pageManager.ActivePage.SelectAsset(m_AssetIdentifier, false);
                }
            });

            m_Button.focusable = false;
            Add(m_Button);
            m_Button.AddToClassList(k_DetailsPageFileItemUssStyle);

            m_FileName = new Label("");
            m_Icon = new VisualElement();

            m_Icon.AddToClassList(k_DetailsPageFileIconItemUssStyle);
            m_FileName.AddToClassList(k_DetailsPageFileLabelItemUssStyle);

            m_Button.Add(m_Icon);
            m_Button.Add(m_FileName);
            m_Button.SetEnabled(false);
        }

        ~DetailsPageDependencyItem()
        {
            m_CancellationTokenSource?.Dispose();
        }

        public async Task Refresh(AssetIdentifier dependencyIdentifier)
        {
            // Don't refresh if the asset pointer hasn't changed
            if (m_AssetIdentifier != null && m_AssetIdentifier.Equals(dependencyIdentifier))
            {
                return;
            }

            m_FileName.text = "Loading...";
            m_Icon.style.backgroundImage = null;

            m_Button.SetEnabled(false);

            var token = GetCancellationToken();

            IAssetData assetData = null;

            try
            {
                assetData = await FetchAssetData(dependencyIdentifier, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            m_FileName.text = assetData?.Name ?? $"{dependencyIdentifier.AssetId} (unavailable)";

            m_AssetIdentifier = assetData?.Identifier;

            m_Button.SetEnabled(m_AssetIdentifier != null);

            await SetIcon(assetData, token);
        }

        CancellationToken GetCancellationToken()
        {
            if (m_CancellationTokenSource != null)
            {
                m_CancellationTokenSource.Cancel();
                m_CancellationTokenSource.Dispose();
            }

            m_CancellationTokenSource = new CancellationTokenSource();
            return m_CancellationTokenSource.Token;
        }

        static async Task<IAssetData> FetchAssetData(AssetIdentifier identifier, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();
            var assetData = await assetDataManager.GetOrSearchAssetData(identifier, token);

            token.ThrowIfCancellationRequested();

            return assetData;
        }

        async Task SetIcon(IAssetData assetData, CancellationToken token)
        {
            if (assetData != null && string.IsNullOrEmpty(assetData.PrimarySourceFile?.Extension))
            {
                await assetData.ResolvePrimaryExtensionAsync(null, token);
            }

            m_Icon.style.backgroundImage =
                AssetDataTypeHelper.GetIconForExtension(assetData?.PrimarySourceFile?.Extension);
        }
    }
}
