using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class MultiSelectionItem : VisualElement
    {
        const string k_DetailsPageFileItemUssStyle = "details-page-file-item";
        const string k_DetailsPageFileIconItemUssStyle = "details-page-file-item-icon";
        const string k_DetailsPageFileLabelItemUssStyle = "details-page-file-item-label";
        const string k_NoFilesWarningIcon = "no-files-warning-icon";

        readonly Label m_FileName;
        readonly VisualElement m_Icon;
        readonly VisualElement m_WarningIcon;

        public MultiSelectionItem()
        {
            m_FileName = new Label("");
            m_Icon = new VisualElement();
            m_WarningIcon = new VisualElement();

            AddToClassList(k_DetailsPageFileItemUssStyle);
            m_Icon.AddToClassList(k_DetailsPageFileIconItemUssStyle);

            m_WarningIcon.AddToClassList(k_DetailsPageFileIconItemUssStyle);
            m_WarningIcon.AddToClassList(k_NoFilesWarningIcon);
            m_WarningIcon.tooltip = L10n.Tr(Constants.ImportNoFilesTooltip);

            m_FileName.AddToClassList(k_DetailsPageFileLabelItemUssStyle);

            Add(m_Icon);
            Add(m_WarningIcon);
            Add(m_FileName);
        }

        public async Task Refresh(BaseAssetData fileItem)
        {
            m_FileName.text = fileItem.Name;
            m_Icon.style.backgroundImage = AssetDataTypeHelper.GetIconForExtension(fileItem.PrimaryExtension);

            if (fileItem.PrimarySourceFile == null)
            {
                var tasks = new List<Task>
                {
                    fileItem.ResolveDatasetsAsync()
                };

                await TaskUtils.WaitForTasksWithHandleExceptions(tasks);
            }

            var hasFiles = fileItem.HasImportableFiles();
            UIElementsUtils.SetDisplay(m_Icon, hasFiles);
            UIElementsUtils.SetDisplay(m_WarningIcon, !hasFiles);
        }
    }
}
