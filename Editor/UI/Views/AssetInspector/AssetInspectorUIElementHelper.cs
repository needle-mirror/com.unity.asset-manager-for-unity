using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    static partial class UssStyle
    {
        public const string LoadingLabel = "details-page-loading-label";
        public const string DetailsPageEntryHeader = "details-page-entry-header";
    }

    static class AssetInspectorUIElementHelper
    {
        public static void AddLoadingText(VisualElement container)
        {
            var loadingLabel = new Label
            {
                text = L10n.Tr(Constants.LoadingText)
            };
            loadingLabel.AddToClassList(UssStyle.LoadingLabel);

            container.Add(loadingLabel);
        }

        public static void AddHeader(VisualElement container, string text)
        {
            var entry = new Label(text);
            entry.AddToClassList(UssStyle.DetailsPageEntryHeader);
            container.Add(entry);
        }

        public static void AddLabel(VisualElement container, string title, string name = null)
        {
            var entry = new DetailsPageEntry(title)
            {
                name = name
            };
            container.Add(entry);
        }

        public static void AddSpace(VisualElement container, int height = 10)
        {
            var space = new VisualElement();
            space.style.height = height;
            container.Add(space);
        }

        public static IEditableEntry AddEditableText(VisualElement container, string assetId, string title, string details, bool isSelectable = false, string name = null)
        {
            var entry = new EditableTextEntry(assetId, title, details, isSelectable)
            {
                name = name
            };
            container.Add(entry);

            return entry;
        }

        public static void AddText(VisualElement container, string title, string details, bool isSelectable = false, string name = null)
        {
            if (string.IsNullOrEmpty(details))
            {
                return;
            }

            var entry = new DetailsPageEntry(title, details, isSelectable)
            {
                name = name
            };
            container.Add(entry);
        }

        public static void AddText(VisualElement container, string title, string details, IEnumerable<string> classNames, string name = null)
        {
            if (string.IsNullOrEmpty(details))
                return;

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

        public static void AddAssetIdentifier(VisualElement entriesContainer, string title, AssetIdentifier identifier)
        {
            if (identifier.IsLocal())
                return;

            AddText(entriesContainer, title, identifier.AssetId, isSelectable:true);
        }

        public static void AddUser(VisualElement container, string title, string details, Type searchFilterType, string name = null)
        {
            if (string.IsNullOrEmpty(details))
                return;

            var entry = new DetailsPageEntry(title)
            {
                name = name
            };
            container.Add(entry);

            var chipContainer = entry.AddChipContainer();
            AddUserChip(chipContainer, details, searchFilterType);
        }

        static async Task AddUserChip(VisualElement container, string userId, Type searchFilterType)
        {
            container.Clear();

            var userChip = await CreateUserChip(userId, searchFilterType);

            UIElementsUtils.SetDisplay(container, userChip != null);

            if (userChip != null)
            {
                container.Add(userChip);
            }
        }

        static async Task<UserChip> CreateUserChip(string userId, Type searchFilterType)
        {
            UserChip userChip = null;

            if (userId is "System" or "Service Account")
            {
                var userInfo = new UserInfo { Name = L10n.Tr(Constants.ServiceAccountText) };
                userChip = new UserChip(userInfo);
            }
            else
            {
                var organizationProvider = ServicesContainer.instance.Resolve<IProjectOrganizationProvider>();
                var userInfos = await organizationProvider.SelectedOrganization.GetUserInfosAsync();
                var userInfo = userInfos.Find(ui => ui.UserId == userId);
                userChip = new UserChip(userInfo);

                var pageManager = ServicesContainer.instance.Resolve<IPageManager>();
                userChip.RegisterCallback<ClickEvent>(_ => TaskUtils.TrackException(pageManager.PageFilterStrategy.ApplyFilter(searchFilterType, new List<string>{userInfo.Name})));
            }

            return userChip;
        }

        public static void AddProjectChips(VisualElement container, string title, string[] projectIds, string name = null)
        {
            var entry = new DetailsPageEntry(title)
            {
                name = name
            };
            container.Add(entry);

            var chipContainer = entry.AddChipContainer();
            chipContainer.Clear();

            foreach (var projectId in projectIds)
            {
                var projectChip = CreateProjectChip(projectId);

                if (projectChip != null)
                {
                    chipContainer.Add(projectChip);
                }
            }

            UIElementsUtils.SetDisplay(chipContainer.parent, chipContainer.childCount > 0);
        }

        static ProjectChip CreateProjectChip(string projectId)
        {
            var organizationProvider = ServicesContainer.instance.Resolve<IProjectOrganizationProvider>();
            var projectInfo = organizationProvider.SelectedOrganization?.ProjectInfos.Find(p => p.Id == projectId);

            if (projectInfo == null)
            {
                return null;
            }

            var projectChip = new ProjectChip(projectInfo);
            projectChip.ProjectChipClickAction += p => { organizationProvider.SelectProject(p.Id); };

            var projectIconDownloader = ServicesContainer.instance.Resolve<IProjectIconDownloader>();
            projectIconDownloader.DownloadIcon(projectInfo.Id, (id, icon) =>
            {
                if (id == projectInfo.Id)
                {
                    projectChip.SetIcon(icon);
                }
            });

            return projectChip;
        }

        public static void AddSelectionChips(VisualElement container, string title, IEnumerable<string> chips, bool isSelectable = false,
            string name = null)
        {
            AddChips(container, title,chips, chipText => new Chip(chipText, isSelectable),  name);
        }

        public static void AddChips(VisualElement container, string title, IEnumerable<string> chips,
            Func<string, Chip> chipCreator, string name = null)
        {
            var enumerable = chips as string[] ?? chips.ToArray();
            if (!enumerable.Any())
            {
                return;
            }

            var entry = new DetailsPageEntry(title)
            {
                name = name
            };
            container.Add(entry);

            var chipsContainer = entry.AddChipContainer();
            chipsContainer.AddToClassList(UssStyle.FlexWrap);

            foreach (var chipText in enumerable)
            {
                var chip = chipCreator?.Invoke(chipText);
                chipsContainer.Add(chip);
            }
        }

        public static EditableListEntry AddEditableTagList(VisualElement container, string assetId, string title, IEnumerable<string> chips,
            string name = null)
        {
            var entry = new EditableListEntry(assetId, title, chips, TagChipCreator)
            {
                name = name
            };
            container.Add(entry);

            return entry;

            Chip TagChipCreator(string chipText)
            {
                var tagChip = new TagChip(chipText);
                tagChip.TagChipPointerUpAction += tagText =>
                {
                    var words = tagText.Split(' ').Where(w => !string.IsNullOrEmpty(w));
                    var pageManager = ServicesContainer.instance.Resolve<IPageManager>();
                    pageManager.PageFilterStrategy.AddSearchFilter(words);
                };

                return tagChip;
            }
        }

        public static EditableDropdownEntry AddEditableStatusDropdown(VisualElement container, string assetId, string title, string selectedValue, IEnumerable<string> options)
        {
            var statusEntry = new EditableDropdownEntry(assetId, title, selectedValue, options, true);
            container.Add(statusEntry);

            return statusEntry;
        }

        public static void AddCollectionChips(VisualElement container, string title, IEnumerable<CollectionIdentifier> collections,
            string name = null)
        {
            if (!collections.Any())
            {
                return;
            }

            var entry = new DetailsPageEntry(title)
            {
                name = name
            };
            container.Add(entry);

            var chipsContainer = entry.AddChipContainer();
            chipsContainer.AddToClassList(UssStyle.FlexWrap);

            foreach (var collection in collections)
            {
                var chip = new CollectionChip(collection);
                chipsContainer.Add(chip);
            }
        }

        public static void AddToggle(VisualElement container, string title, bool toggleValue, string name = null)
        {
            var entry = new DetailsPageEntry(title)
            {
                name = name
            };
            container.Add(entry);

            var toggle = new Toggle
            {
                value = toggleValue
            };
            toggle.SetEnabled(false);

            entry.Add(toggle);
        }

                public static string GetImportButtonLabel(BaseOperation operationInProgress, AssetPreview.IStatus status)
        {
            var isImporting = operationInProgress?.Status == OperationStatus.InProgress;

            if (isImporting)
            {
                return $"{L10n.Tr(Constants.ImportingText)} ({operationInProgress.Progress * 100:0.#}%)";
            }

            return status != null && !string.IsNullOrEmpty(status.ActionText) ? L10n.Tr(status.ActionText) : L10n.Tr(Constants.ImportActionText);
        }

        public static string GetImportButtonTooltip(BaseOperation operationInProgress, UIEnabledStates enabled, bool versionIsImported = true, bool hasFiles = true)
        {
            var isEnabled = operationInProgress?.Status != OperationStatus.InProgress
                && enabled.HasFlag(UIEnabledStates.HasPermissions)
                && enabled.HasFlag(UIEnabledStates.ServicesReachable)
                && hasFiles;

            if (isEnabled)
            {
                if (enabled.HasFlag(UIEnabledStates.InProject) && versionIsImported)
                {
                    return L10n.Tr(Constants.ReimportButtonTooltip);
                }
                return L10n.Tr(Constants.ImportButtonTooltip);
            }

            if (!enabled.HasFlag(UIEnabledStates.ServicesReachable))
            {
                return L10n.Tr(Constants.UploadCloudServicesNotReachableTooltip);
            }

            if (!enabled.HasFlag(UIEnabledStates.HasPermissions))
            {
                return L10n.Tr(Constants.ImportNoPermissionMessage);
            }

            if (!hasFiles)
            {
                return L10n.Tr(Constants.ImportNoFilesTooltip);
            }

            return enabled.HasFlag(UIEnabledStates.IsImporting) ? string.Empty : L10n.Tr(Constants.ImportButtonDisabledToolTip);
        }

        public static UIEnabledStates GetFlag(this UIEnabledStates flag, bool value)
        {
            return value ? flag : UIEnabledStates.None;
        }

        public static bool IsImportAvailable(this UIEnabledStates enabled)
        {
            var isEnabled = !enabled.HasFlag(UIEnabledStates.IsImporting)
                && enabled.HasFlag(UIEnabledStates.CanImport)
                && enabled.HasFlag(UIEnabledStates.HasPermissions)
                && enabled.HasFlag(UIEnabledStates.ServicesReachable)
                && enabled.HasFlag(UIEnabledStates.ValidStatus);

            return isEnabled;
        }
    }
}
