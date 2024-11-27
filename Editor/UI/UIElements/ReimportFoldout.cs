using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    static partial class UssStyle
    {
        public static readonly string ReimportFoldout = "reimport-foldout";
        public static readonly string ReimportFoldoutHeader = ReimportFoldout + "-header";
        public static readonly string ReimportFoldoutHeaderText = ReimportFoldoutHeader + "-text";
        public static readonly string ReimportFoldoutAssetName = ReimportFoldoutHeaderText + "-name";
        public static readonly string ReimportFoldoutHeaderDropdown = ReimportFoldoutHeader + "-dropdown";
        public static readonly string ReimportFoldoutContent = ReimportFoldout + "-content";
        public static readonly string ReimportFoldoutContentEntry = ReimportFoldoutContent + "-entry";
        public static readonly string ReimportFoldoutContentEntryName = ReimportFoldoutContentEntry + "-name";
    }

    class ReimportFoldout : Foldout
    {
        static readonly string k_WarningStatus = "-status--warning";
        static readonly List<string> k_ConflictSelections = new() { L10n.Tr(Constants.ReimportWindowReplace), L10n.Tr(Constants.ReimportWindowSkip) };
        static readonly List<string> k_ImportAssetSelections = new() { L10n.Tr(Constants.ReimportWindowImport), L10n.Tr(Constants.ReimportWindowSkip) };
        static readonly List<string> k_ReImportAssetSelections = new() { L10n.Tr(Constants.ReimportWindowReimport), L10n.Tr(Constants.ReimportWindowSkip) };
        static readonly List<string> k_UpdatedAssetSelections = new() {L10n.Tr(Constants.ReimportWindowUpdate), L10n.Tr(Constants.ReimportWindowSkip) };

        readonly BaseAssetData m_AssetData;
        readonly AssetDataResolutionInfo m_AssetDataResolutionInfo;
        ResolutionSelection m_ResolutionSelection;

        internal ResolutionSelection ResolutionSelection => m_ResolutionSelection;
        internal BaseAssetData AssetData => m_AssetData;
        internal AssetDataResolutionInfo AssetDataResolutionInfo => m_AssetDataResolutionInfo;

        internal ReimportFoldout(AssetDataResolutionInfo assetDataResolutionInfo)
        {
            m_AssetDataResolutionInfo = assetDataResolutionInfo;
            m_AssetData = assetDataResolutionInfo.AssetData;
            text = string.Empty;
            value = false;
            AddToClassList(UssStyle.ReimportFoldout);

            var header = new VisualElement();
            header.AddToClassList(UssStyle.ReimportFoldoutHeader);
            var toggleContainer = this.Q("unity-checkmark").parent;
            toggleContainer.Add(header);

            var headerTextContainer = new VisualElement();
            headerTextContainer.AddToClassList(UssStyle.ReimportFoldoutHeaderText);
            header.Add(headerTextContainer);

            var assetName = new Label(m_AssetData.Name);
            assetName.AddToClassList(UssStyle.ReimportFoldoutAssetName);
            headerTextContainer.Add(assetName);

            var current = assetDataResolutionInfo.CurrentVersion <= 0 ? L10n.Tr(Constants.PendingVersionText) : $"{L10n.Tr(Constants.VersionText)} {assetDataResolutionInfo.CurrentVersion}";
            var destination = assetDataResolutionInfo.AssetData.SequenceNumber <= 0 ? L10n.Tr(Constants.PendingVersionText) : $"{L10n.Tr(Constants.VersionText)} {assetDataResolutionInfo.AssetData.SequenceNumber}";
            string version = $" - {current} > {destination}";
            var versionsLabel = new Label(version);
            headerTextContainer.Add(versionsLabel);

            var conflicts = assetDataResolutionInfo.FileConflicts;
            if (conflicts.Any())
            {
                 var conflictCount = new Label($" ({conflicts.Count} {L10n.Tr(Constants.Conflicts)})");
                 headerTextContainer.Add(conflictCount);
            }

            var selections = GetSelections(assetDataResolutionInfo);

            var resolveDropDown = new DropdownField()
            {
                choices = selections,
                index = selections == k_ReImportAssetSelections ? 1 : 0
            };
            resolveDropDown.RegisterValueChangedCallback(v =>
            {
                m_ResolutionSelection = (ResolutionSelection)resolveDropDown.index;
            });
            m_ResolutionSelection = (ResolutionSelection)resolveDropDown.index;
            resolveDropDown.AddToClassList(UssStyle.ReimportFoldoutHeaderDropdown);
            header.Add(resolveDropDown);

            var content = new VisualElement();
            content.AddToClassList(UssStyle.ReimportFoldoutContent);
            foreach (var file in m_AssetData.SourceFiles)
            {
                // Hide meta files unless they are in conflict
                if(file.Extension == ".meta" && !conflicts.Contains(file))
                    continue;

                var entry = new VisualElement();
                entry.AddToClassList(UssStyle.ReimportFoldoutContentEntry);

                var path = Utilities.NormalizePathSeparators("Assets/" + file.Path);
                var nameLabel = new Label(path);
                nameLabel.AddToClassList(UssStyle.ReimportFoldoutContentEntryName);
                nameLabel.tooltip = path;
                entry.Add(nameLabel);

                if (conflicts.Contains(file))
                {
                    var status = new VisualElement();
                    status.AddToClassList(UssStyle.ReimportFoldoutContentEntry + k_WarningStatus);
                    entry.Add(status);
                }
                content.Add(entry);
            }

            base.contentContainer.Add(content);
        }

        static List<string> GetSelections(AssetDataResolutionInfo assetDataResolutionInfo)
        {
            List<string> selections;
            if (assetDataResolutionInfo.FileConflicts.Any())
            {
                selections = k_ConflictSelections;
            }
            else if (assetDataResolutionInfo.Existed)
            {
                if (assetDataResolutionInfo.HasChanges)
                {
                    if (assetDataResolutionInfo.IsLatestVersion)
                    {
                        selections = k_ImportAssetSelections;
                    }
                    else
                    {
                        selections = k_UpdatedAssetSelections;
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
            return selections;
        }
    }
}
