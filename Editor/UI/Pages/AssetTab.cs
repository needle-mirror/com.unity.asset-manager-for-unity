using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    static partial class UssStyle
    {
        public const string LoadingLabel = "details-page-loading-label";
    }
    
    abstract class AssetTab : IPageComponent
    {
        public event CreateProjectChip CreateProjectChip;
        public event CreateUserChip CreateUserChip;
        public event Action<IEnumerable<string>> ApplyFilter;
        
        public abstract AssetDetailsPageTabs.TabType Type { get; }
        public abstract bool IsFooterVisible { get; }
        public abstract VisualElement Root { get; }

        protected static void AddLoadingText(VisualElement container)
        {
            var loadingLabel = new Label
            {
                text = L10n.Tr(Constants.LoadingText)
            };
            loadingLabel.AddToClassList(UssStyle.LoadingLabel);
            
            container.Add(loadingLabel);
        }

        protected static void AddLabel(VisualElement container, string title, string name = null)
        {
            var entry = new DetailsPageEntry(title)
            {
                name = name
            };
            container.Add(entry);
        }

        protected static void AddText(VisualElement container, string title, string details, string name = null)
        {
            if (string.IsNullOrEmpty(details))
            {
                return;
            }
            
            var entry = new DetailsPageEntry(title, details)
            {
                name = name
            };
            container.Add(entry);
        }

        protected static void AddText(VisualElement container, string title, string details, IEnumerable<string> classNames, string name = null)
        {
            if (string.IsNullOrEmpty(details))
            {
                return;
            }

            var entry = new DetailsPageEntry(title, details)
            {
                name = name
            };

            if (classNames != null)
            {
                foreach (var className in classNames)
                {
                    entry.AddToClassList(className);
                }
            }

            container.Add(entry);
        }

        protected void AddUser(VisualElement container, string title, string details, Type searchFilterType, string name = null)
        {
            var entry = new DetailsPageEntry(title)
            {
                name = name
            };
            container.Add(entry);

            var chipContainer = entry.AddChipContainer();
            CreateUserChip?.AddUserChip(chipContainer, details, searchFilterType);
        }
        
        protected void AddProject(VisualElement container, string title, string projectId, string name = null)
        {
            var entry = new DetailsPageEntry(title)
            {
                name = name
            };
            container.Add(entry);
            
            var chipContainer = entry.AddChipContainer();
            CreateProjectChip?.AddProjectChip(chipContainer, projectId);
        }

        protected void AddTags(VisualElement container, string title, IEnumerable<string> tags, string name = null)
        {
            var enumerable = tags as string[] ?? tags.ToArray();
            if (!enumerable.Any())
            {
                return;
            }
            
            var entry = new DetailsPageEntry(title)
            {
                name = name
            };
            container.Add(entry);
            
            var tagsContainer = entry.AddChipContainer();
            tagsContainer.AddToClassList(UssStyle.FlexWrap);

            foreach (var tag in enumerable)
            {
                var tagChip = new TagChip(tag);
                tagChip.TagChipPointerUpAction += tagText =>
                {
                    var words = tagText.Split(' ').Where(w => !string.IsNullOrEmpty(w));
                    ApplyFilter?.Invoke(words);
                };
                tagsContainer.Add(tagChip);
            }
        }

        public abstract void OnSelection(IAssetData assetData, bool isLoading);
        public abstract void RefreshUI(IAssetData assetData, bool isLoading = false);
        public abstract void RefreshButtons(UIEnabledStates enabled, IAssetData assetData, BaseOperation operationInProgress);
    }
}