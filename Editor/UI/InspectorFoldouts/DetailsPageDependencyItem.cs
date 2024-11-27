using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using Unity.AssetManager.Upload.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class DetailsPageDependencyItem : VisualElement
    {
        const string k_DetailsPageFileItemUssStyle = "details-page-dependency-item";
        const string k_DetailsPageFileItemInfoUssStyle = "details-page-dependency-item-info";
        const string k_DetailsPageFileItemStatusUssStyle = "details-page-dependency-item-status";
        const string k_DetailsPageFileIconItemUssStyle = "details-page-dependency-item-icon";
        const string k_DetailsPageFileLabelItemUssStyle = "details-page-dependency-item-label";

        readonly Button m_Button;
        readonly VisualElement m_Icon;
        readonly Label m_FileName;
        readonly VisualElement m_ImportedStatusIcon;
        readonly Label m_VersionNumber;

        AssetIdentifier m_AssetIdentifier;
        BaseAssetData m_AssetData;

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

            Add(m_Button);

            m_Button.focusable = false;
            m_Button.AddToClassList(k_DetailsPageFileItemUssStyle);

            var infoElement = new VisualElement();
            infoElement.AddToClassList(k_DetailsPageFileItemInfoUssStyle);

            m_FileName = new Label("");
            m_Icon = new VisualElement();

            m_Icon.AddToClassList(k_DetailsPageFileIconItemUssStyle);
            m_FileName.AddToClassList(k_DetailsPageFileLabelItemUssStyle);

            infoElement.Add(m_Icon);
            infoElement.Add(m_FileName);

            var statusElement = new VisualElement();
            statusElement.AddToClassList(k_DetailsPageFileItemStatusUssStyle);

            m_ImportedStatusIcon = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };

            m_VersionNumber = new Label();
            m_VersionNumber.AddToClassList("asset-version");

            statusElement.Add(m_VersionNumber);
            statusElement.Add(m_ImportedStatusIcon);

            m_Button.Add(infoElement);
            m_Button.Add(statusElement);
            m_Button.SetEnabled(false);
        }

        ~DetailsPageDependencyItem()
        {
            m_CancellationTokenSource?.Dispose();
        }

        public async Task Bind(AssetIdentifier dependencyIdentifier)
        {
            // If the identifier is different, we need to reset the UI
            // otherwise we reload and rebind to new AssetData instance
            if (m_AssetIdentifier == null || m_AssetIdentifier != dependencyIdentifier)
            {
                m_FileName.text = "Loading...";
                m_Icon.style.backgroundImage = null;

                UIElementsUtils.SetDisplay(m_ImportedStatusIcon, false);
                UIElementsUtils.SetDisplay(m_VersionNumber, false);
            }

            m_Button.SetEnabled(false);

            var token = GetCancellationToken();

            if (m_AssetData != null)
            {
                m_AssetData.AssetDataChanged -= OnAssetDataChanged;
            }

            m_AssetData = null;

            try
            {
                m_AssetData = await FetchAssetData(dependencyIdentifier, token);
                m_AssetIdentifier = m_AssetData?.Identifier ?? dependencyIdentifier;

                if (m_AssetData != null)
                {
                    m_AssetData.AssetDataChanged += OnAssetDataChanged;

                    var tasks = new[]
                    {
                        m_AssetData.GetPreviewStatusAsync(null, token),
                        m_AssetData.ResolvePrimaryExtensionAsync(null, token),
                    };

                    await Task.WhenAll(tasks);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            RefreshUI();
        }

        void OnAssetDataChanged(BaseAssetData assetData, AssetDataEventType eventType)
        {
            if (assetData != m_AssetData || m_AssetData == null)
                return;

            RefreshUI();
        }

        void RefreshUI()
        {
            m_Button.SetEnabled(m_AssetIdentifier != null);

            m_FileName.text = m_AssetData?.Name ?? $"{m_AssetIdentifier?.AssetId} (unavailable)";

            SetStatuses(AssetDataStatus.GetIStatusFromAssetDataStatusType(m_AssetData?.PreviewStatus));

            m_Icon.style.backgroundImage = AssetDataTypeHelper.GetIconForExtension(m_AssetData?.PrimaryExtension);

            SetVersionNumber(m_AssetData);
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

        static async Task<BaseAssetData> FetchAssetData(AssetIdentifier identifier, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();
            var assetData = await assetDataManager.GetOrSearchAssetData(identifier, token);

            token.ThrowIfCancellationRequested();

            return assetData;
        }

        // Common code between this and the AssetPreview class
        void SetStatuses(IEnumerable<AssetPreview.IStatus> statuses)
        {
            if (statuses == null)
            {
                UIElementsUtils.SetDisplay(m_ImportedStatusIcon, false);
                return;
            }

            var validStatuses = statuses.Where(s => s != null).ToList();
            validStatuses.Remove(AssetDataStatus.Linked); // Hack : we need to extract the linked from the status and have only one status instead of a list
            var hasStatuses = validStatuses.Any();
            UIElementsUtils.SetDisplay(m_ImportedStatusIcon, hasStatuses);

            m_ImportedStatusIcon.Clear();

            if (!hasStatuses)
                return;

            foreach (var status in validStatuses)
            {
                var statusElement = status.CreateVisualTree();
                statusElement.tooltip = L10n.Tr(status.Description);
                statusElement.style.position = Position.Relative;
                m_ImportedStatusIcon.Add(statusElement);
            }
        }

        // Common code between this and the AssetDetailsHeader class
        void SetVersionNumber(BaseAssetData assetData)
        {
            UIElementsUtils.SetDisplay(m_VersionNumber, assetData != null);
            if (assetData == null)
                return;

            UIElementsUtils.SetSequenceNumberText(m_VersionNumber, assetData);
        }
    }
}
