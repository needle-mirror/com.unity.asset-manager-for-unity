using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class FilesFoldout : ItemFoldout<BaseAssetDataFile, FileFoldoutItem>
    {
        readonly FileFoldoutViewModel m_ViewModel;

        public FilesFoldout(VisualElement parent, FileFoldoutViewModel viewModel)
            : base(parent, viewModel.Name, $"files-foldout-{GetSuffix(viewModel.Name)}", $"files-list-{GetSuffix(viewModel.Name)}", "details-files-foldout", "details-files-list")
        {
            m_ViewModel = viewModel;

            var uvcsChip = new Chip("VCS");
            uvcsChip.AddToClassList("details-files-foldout-uvcs-chip");
            uvcsChip.tooltip = L10n.Tr(Constants.VCSChipTooltip);
            var icon = new VisualElement();
            icon.AddToClassList("details-files-foldout-uvcs-chip-icon");
            uvcsChip.Add(icon);
            m_FoldoutToggle?.Add(uvcsChip);

            UIElementsUtils.SetDisplay(uvcsChip, viewModel.IsSourceControlled);
        }

        protected override FileFoldoutItem MakeItem()
        {
            return new FileFoldoutItem();
        }

        protected override void BindItem(FileFoldoutItem element, int index)
        {
            var viewModel = m_ViewModel.GetFileFoldoutItemViewModel(index);
            var enabled = !MetafilesHelper.IsOrphanMetafile(viewModel.Filename, m_ViewModel.Files.Select(f => f.Path).ToList());

            element.Bind(viewModel);
            element.Refresh(enabled);
            element.RemoveClicked = () =>
            {
                viewModel.Remove();
            };
        }

        static string GetSuffix(string title)
        {
            return title.ToLower().Replace(' ', '-');
        }
    }
}
