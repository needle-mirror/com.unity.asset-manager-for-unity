using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    static partial class UssStyle
    {
        public const string ReimportItem = "reimport-item";
        public const string ReimportItemFileInfoContainer = ReimportItem + "-file-info-container";
        public const string ReimportItemAssetName = ReimportItem + "-asset-name";
        public const string ReimportItemResolveDropDown = ReimportItem + "-resolve-dropdown";
    }

    class ReimportItem : VisualElement
    {
        static readonly List<string> k_ImportAssetSelections = new() { L10n.Tr(Constants.ReimportWindowImport), L10n.Tr(Constants.ReimportWindowSkip) };
        static readonly List<string> k_ReImportAssetSelections = new() { L10n.Tr(Constants.ReimportWindowReimport), L10n.Tr(Constants.ReimportWindowSkip) };
        static readonly List<string> k_UpdatedAssetSelections = new() {L10n.Tr(Constants.ReimportWindowUpdate), L10n.Tr(Constants.ReimportWindowSkip) };

        readonly IAssetData m_AssetData;

        ResolutionSelection m_ResolutionSelection;

        internal ResolutionSelection ResolutionSelection => m_ResolutionSelection;
        internal IAssetData AssetData => m_AssetData;

        internal ReimportItem(AssetDataResolutionInfo assetDataResolutionInfo)
        {
            m_AssetData = assetDataResolutionInfo.AssetData;

            AddToClassList(UssStyle.ReimportItem);

            var fileInfoContainer = new VisualElement();
            fileInfoContainer.AddToClassList(UssStyle.ReimportItemFileInfoContainer);
            Add(fileInfoContainer);

            var assetName = new Label(m_AssetData.Name);
            assetName.AddToClassList(UssStyle.ReimportItemAssetName);
            fileInfoContainer.Add(assetName);

            var current = assetDataResolutionInfo.CurrentVersion <= 0 ? $"Pending Ver." : $"Ver. {assetDataResolutionInfo.CurrentVersion}";
            var destination = assetDataResolutionInfo.AssetData.SequenceNumber <= 0 ? $"Pending Ver." : $"Ver. {assetDataResolutionInfo.AssetData.SequenceNumber}";
            string text = assetDataResolutionInfo.HasChanges ?
                $" - {current} > {destination}" :
                $" - {destination}";
            var versions = new Label(text);
            fileInfoContainer.Add(versions);

            List<string> selections;
            if (assetDataResolutionInfo.Existed)
            {
                if (assetDataResolutionInfo.HasChanges)
                {
                    if (assetDataResolutionInfo.AssetData.Identifier.Version == assetDataResolutionInfo.AssetData.Versions.FirstOrDefault()?.Identifier.Version)
                    {
                        selections = k_UpdatedAssetSelections;
                    }
                    else
                    {
                        selections = k_ImportAssetSelections;
                    }
                }
                else
                {
                    selections = k_ReImportAssetSelections;
                }
            }
            else
            {
                selections = k_ImportAssetSelections;
            }

            var resolveDropDown = new DropdownField()
            {
                choices = selections,
                index = 0
            };
            resolveDropDown.RegisterValueChangedCallback(v => { m_ResolutionSelection = (ResolutionSelection)resolveDropDown.index; });
            resolveDropDown.AddToClassList(UssStyle.ReimportItemResolveDropDown);
            Add(resolveDropDown);
        }
    }
}
