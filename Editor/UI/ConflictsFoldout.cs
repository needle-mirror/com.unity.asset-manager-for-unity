using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    static partial class UssStyle
    {
        public static readonly string ConflictsFoldout = "conflicts-foldout";
        public static readonly string ConflictsFoldoutHeader = ConflictsFoldout + "-header";
        public static readonly string ConflictsFoldoutHeaderText = ConflictsFoldoutHeader + "-text";
        public static readonly string ConflictsFoldoutAssetName = ConflictsFoldoutHeaderText + "-name";
        public static readonly string ConflictsFoldoutHeaderDropdown = ConflictsFoldoutHeader + "-dropdown";
        public static readonly string ConflictsFoldoutContent = ConflictsFoldout + "-content";
        public static readonly string ConflictsFoldoutContentEntry = ConflictsFoldoutContent + "-entry";
        public static readonly string ConflictsFoldoutContentEntryName = ConflictsFoldoutContentEntry + "-name";
    }

    class ConflictsFoldout : Foldout
    {
        static readonly string k_WarningStatus = "-status--warning";
        static readonly List<string> k_IgnoreExtensions = new() { ".meta", ".am4u_dep", ".am4u_guid" };
        static readonly List<string> k_ConflictSelections = new() { "Replace", "Skip" };

        readonly IAssetData m_AssetData;
        ResolutionSelection m_ResolutionSelection;

        internal ResolutionSelection ResolutionSelection => m_ResolutionSelection;
        internal IAssetData AssetData => m_AssetData;

        internal ConflictsFoldout(AssetDataResolutionInfo assetDataResolutionInfo)
        {
            m_AssetData = assetDataResolutionInfo.AssetData;
            text = string.Empty;
            AddToClassList(UssStyle.ConflictsFoldout);

            var header = new VisualElement();
            header.AddToClassList(UssStyle.ConflictsFoldoutHeader);
            var toggleContainer = this.Q("unity-checkmark").parent;
            toggleContainer.Add(header);

            var headerTextContainer = new VisualElement();
            headerTextContainer.AddToClassList(UssStyle.ConflictsFoldoutHeaderText);
            header.Add(headerTextContainer);

            var assetName = new Label(m_AssetData.Name);
            assetName.AddToClassList(UssStyle.ConflictsFoldoutAssetName);
            headerTextContainer.Add(assetName);

            var conflicts = m_AssetData.SourceFiles.ToList();
            conflicts.AddRange(m_AssetData.UVCSFiles);
            var filteredConflicts = conflicts.Where(c => !k_IgnoreExtensions.Contains(Path.GetExtension(c.Path))).ToList();
            var conflictCount = new Label($" - {filteredConflicts.Count} conflicts");
            headerTextContainer.Add(conflictCount);

            List<string> selections;
            selections = k_ConflictSelections;

            var resolveDropDown = new DropdownField()
            {
                choices = selections,
                index = 0
            };
            resolveDropDown.RegisterValueChangedCallback(v =>
            {
                m_ResolutionSelection = (ResolutionSelection)resolveDropDown.index;
            });
            resolveDropDown.AddToClassList(UssStyle.ConflictsFoldoutHeaderDropdown);
            header.Add(resolveDropDown);

            var content = new VisualElement();
            content.AddToClassList(UssStyle.ConflictsFoldoutContent);
            foreach (var file in filteredConflicts)
            {
                var entry = new VisualElement();
                entry.AddToClassList(UssStyle.ConflictsFoldoutContentEntry);

                var nameLabel = new Label(Path.GetFileName(file.Path));
                nameLabel.AddToClassList(UssStyle.ConflictsFoldoutContentEntryName);
                entry.Add(nameLabel);

                var status = new VisualElement();
                status.AddToClassList(UssStyle.ConflictsFoldoutContentEntry + k_WarningStatus);
                entry.Add(status);
                content.Add(entry);
            }

            base.contentContainer.Add(content);
        }
    }
}
