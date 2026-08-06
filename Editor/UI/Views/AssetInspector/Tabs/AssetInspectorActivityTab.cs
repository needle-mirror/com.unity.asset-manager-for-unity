using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class AssetInspectorActivityTab : IPageComponent
    {
        const int k_PageSize = 50;

        readonly AssetInspectorViewModel m_ViewModel;
        bool m_IsLoading;
        int m_DisplayCount = k_PageSize;

        public VisualElement Root { get; }

        public AssetInspectorActivityTab(VisualElement visualElement, AssetInspectorViewModel viewModel)
        {
            var root = new VisualElement();
            root.AddToClassList(UssStyle.DetailsPageContentContainer);
            visualElement.Add(root);

            m_ViewModel = viewModel;
            m_ViewModel.UpdateHistoryRefreshed += OnUpdateHistoryRefreshed;
            Root = root;
        }

        void OnUpdateHistoryRefreshed()
        {
            Root.schedule.Execute(() =>
            {
                m_IsLoading = false;
                RefreshUI();
            });
        }

        public void OnSelection()
        {
            m_DisplayCount = k_PageSize;

            // Library assets are immutable, so there is no metadata history to fetch. The tab is
            // hidden for them; leave it empty rather than requesting a history the SDK refuses.
            if (m_ViewModel.IsAssetFromLibrary)
            {
                m_IsLoading = false;
                RefreshUI();
                return;
            }

            m_IsLoading = true;
            RefreshUI();

            // Track analytics for Activity tab view
            var assetId = m_ViewModel?.AssetIdentifier?.AssetId ?? "";
            InlineEditAnalyticsTracker.TrackActivityTab("Viewed", assetId, 0);

            TaskUtils.TrackException(m_ViewModel.RefreshUpdateHistoryAsync());
        }

        public void RefreshUI(bool isLoading = false)
        {
            TaskUtils.TrackException(RefreshUIInternal(isLoading));
        }

        async Task RefreshUIInternal(bool isLoading)
        {
            Root.Clear();

            if (m_IsLoading || isLoading)
            {
                AssetInspectorUIElementHelper.AddLoadingText(Root);
                return;
            }

            var changes = m_ViewModel.UpdateHistoryChanges;
            if (changes == null || changes.Count == 0)
            {
                AssetInspectorUIElementHelper.AddText(Root, null, $"<i>{L10n.Tr(Constants.NoActivityYetText)}</i>");
                return;
            }

            var orderedByDate = changes.OrderBy(c => c.Updated).ToList();
            var firstVersion = orderedByDate.First();
            var allRestWithDiffs = orderedByDate
                .Skip(1)
                .Where(c => c.Changes != null && c.Changes.Count > 0)
                .OrderByDescending(c => c.Updated)
                .ToList();

            var currentVersion = m_ViewModel.SelectedAssetData?.SequenceNumber ?? 0;
            var versionX = currentVersion <= 0
                ? L10n.Tr(Constants.PendingVersionText)
                : currentVersion.ToString();
            var startedVersionText = L10n.Tr(Constants.StartedVersionText) + versionX;

            // Total entries includes the "started version" entry (+1)
            var totalEntries = allRestWithDiffs.Count + 1;
            var restWithDiffs = allRestWithDiffs.Take(m_DisplayCount).ToList();
            var hasMoreItems = m_DisplayCount < totalEntries;

            List<UserInfo> userInfos = null;
            var organizationProvider = ServicesContainer.instance.Resolve<IProjectOrganizationProvider>();
            if (organizationProvider?.SelectedOrganization != null)
                userInfos = await organizationProvider.SelectedOrganization.GetUserInfosAsync();

            // Foldouts with "made N changes" and field diffs (newest first)
            foreach (var change in restWithDiffs)
            {
                var count = change.Changes.Count;
                var dateLine = FormatActivityTimestamp(change.Updated);
                var foldout = new Foldout { text = " ", value = false };
                foldout.AddToClassList(UssStyle.AssetVersionDetailsFoldout);
                foldout.AddToClassList(UssStyle.ActivityFoldout);

                var defaultLabel = foldout.Q<Label>();
                if (defaultLabel != null)
                    defaultLabel.style.display = DisplayStyle.None;

                var toggle = foldout.Q<Toggle>();
                if (toggle != null)
                {
                    // Align toggle to the top (first line) instead of center
                    toggle.style.alignItems = Align.FlexStart;

                    var headerColumn = new VisualElement();
                    headerColumn.AddToClassList(UssStyle.ActivityHeaderColumn);

                    var headerRow = new VisualElement();
                    headerRow.AddToClassList(UssStyle.ActivityHeaderRow);
                    if (!string.IsNullOrEmpty(change.UpdatedBy))
                        AddUserIconAndText(headerRow, change.UpdatedBy, userInfos);
                    else
                        headerRow.Add(new Label(L10n.Tr(Constants.SomeoneText)));

                    var madeText = count == 1
                        ? string.Format(L10n.Tr(Constants.MadeChangesTextSingular), count)
                        : string.Format(L10n.Tr(Constants.MadeChangesTextPlural), count);
                    var madeLabel = new Label(madeText);
                    madeLabel.enableRichText = true;
                    madeLabel.AddToClassList(UssStyle.ActivityHeaderText);
                    headerRow.Add(madeLabel);
                    headerColumn.Add(headerRow);

                    var dateLabel = new Label(dateLine);
                    dateLabel.AddToClassList(UssStyle.ActivityDateLabel);
                    dateLabel.tooltip = FormatFullTimestamp(change.Updated);
                    headerColumn.Add(dateLabel);

                    toggle.Add(headerColumn);
                }

                var content = new VisualElement
                {
                    style =
                    {
                        marginLeft = 0
                    }
                };
                foldout.Add(content);
                var fieldDefs = m_ViewModel.MetadataFieldDefinitions;
                var fieldChanges = change.Changes;
                foreach (var fieldChange in fieldChanges)
                {
                    HistoryChangeUIHelper.Build(content, fieldChange, fieldDefs);
                }

                Root.Add(foldout);
            }

            // Add "Load more" button if there are more items (including the "started version" entry)
            if (hasMoreItems)
            {
                var remainingCount = totalEntries - m_DisplayCount;
                var loadMoreButton = new Button(() =>
                {
                    m_DisplayCount += k_PageSize;

                    // Track analytics for load more action
                    var assetId = m_ViewModel?.AssetIdentifier?.AssetId ?? "";
                    var newDisplayCount = Math.Min(m_DisplayCount, totalEntries);
                    InlineEditAnalyticsTracker.TrackActivityTab("LoadedMore", assetId, newDisplayCount);

                    RefreshUI();
                })
                {
                    text = string.Format(L10n.Tr(Constants.LoadMoreText), remainingCount)
                };
                loadMoreButton.AddToClassList(UssStyle.ActivityLoadMoreButton);
                Root.Add(loadMoreButton);
            }
            else
            {
                // Oldest (first version) at bottom: same style as foldout, no expand toggle, "User started version X"
                // Only shown when all other entries have been loaded
                var createdDate = m_ViewModel.SelectedAssetData?.Created ?? firstVersion.Updated;
                var firstDateLine = FormatActivityTimestamp(createdDate);
                var firstFoldout = new Foldout { text = " ", value = false };
                firstFoldout.AddToClassList(UssStyle.AssetVersionDetailsFoldout);
                firstFoldout.AddToClassList(UssStyle.ActivityFoldout);
                firstFoldout.AddToClassList(UssStyle.ActivityFoldoutNoToggle);

                var firstDefaultLabel = firstFoldout.Q<Label>();
                if (firstDefaultLabel != null)
                    firstDefaultLabel.style.display = DisplayStyle.None;

                // Prevent the foldout from opening since this entry has no content
                firstFoldout.RegisterValueChangedCallback(_ => firstFoldout.SetValueWithoutNotify(false));

                var firstToggle = firstFoldout.Q<Toggle>();
                if (firstToggle != null)
                {
                    // Align toggle to the top (first line) instead of center
                    firstToggle.style.alignItems = Align.FlexStart;

                    var headerColumn = new VisualElement();
                    headerColumn.AddToClassList(UssStyle.ActivityHeaderColumn);

                    var headerRow = new VisualElement();
                    headerRow.AddToClassList(UssStyle.ActivityHeaderRow);
                    var createdBy = m_ViewModel.SelectedAssetData?.CreatedBy ?? firstVersion.UpdatedBy;
                    if (!string.IsNullOrEmpty(createdBy))
                        AddUserIconAndText(headerRow, createdBy, userInfos);
                    else
                        headerRow.Add(new Label(L10n.Tr(Constants.SomeoneText)));

                    var startedLabel = new Label(startedVersionText);
                    startedLabel.AddToClassList(UssStyle.ActivityHeaderText);
                    headerRow.Add(startedLabel);
                    headerColumn.Add(headerRow);

                    var firstDateLabel = new Label(firstDateLine);
                    firstDateLabel.AddToClassList(UssStyle.ActivityDateLabel);
                    firstDateLabel.tooltip = FormatFullTimestamp(createdDate);
                    headerColumn.Add(firstDateLabel);

                    firstToggle.Add(headerColumn);
                }

                Root.Add(firstFoldout);
            }
        }

        public void RefreshButtons(UIEnabledStates enabled, BaseOperation operationInProgress)
        {
            // No buttons on Activity tab
        }

        /// <summary>
        /// Formats timestamp as relative time if within 12 hours, otherwise full date/time to minute.
        /// </summary>
        internal static string FormatActivityTimestamp(DateTime dateTime)
        {
            // Normalize both sides to UTC so the subtraction is correct regardless of
            // the DateTimeKind of the incoming timestamp (the SDK returns UTC datetimes).
            var timeSpan = DateTime.UtcNow - dateTime.ToUniversalTime();

            if (timeSpan.TotalHours < 12)
            {
                if (timeSpan.TotalMinutes < 1)
                    return L10n.Tr(Constants.NowText);
                // Use < 60 minutes instead of < 1 hour to avoid showing "60m ago"
                if (timeSpan.TotalMinutes < 60)
                    return $"{(int)timeSpan.TotalMinutes}m ago";
                return $"{(int)timeSpan.TotalHours}h ago";
            }

            return FormatFullTimestamp(dateTime);
        }

        /// <summary>
        /// Formats a timestamp as an absolute local date/time string: "March 23rd, 2026 3:45 PM".
        /// </summary>
        internal static string FormatFullTimestamp(DateTime dateTime)
        {
            var localTime = dateTime.ToLocalTime();
            var day = localTime.Day;
            var ordinalSuffix = GetOrdinalSuffix(day);
            return $"{localTime:MMMM} {day}{ordinalSuffix}, {localTime:yyyy} {localTime:h:mm tt}";
        }

        /// <summary>
        /// Gets the ordinal suffix for a day number (st, nd, rd, th).
        /// </summary>
        internal static string GetOrdinalSuffix(int day)
        {
            if (day >= 11 && day <= 13)
                return "th";

            return (day % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            };
        }

        /// <summary>
        /// Checks if a user ID represents a system/service account.
        /// </summary>
        static bool IsServiceAccount(string userId)
        {
            return userId == Constants.SystemUserText || userId == Constants.ServiceAccountText;
        }

        /// <summary>
        /// Adds a user display with initials icon and name text (without chip styling).
        /// Used in foldout headers for a cleaner appearance.
        /// </summary>
        static void AddUserIconAndText(VisualElement container, string userId, List<UserInfo> userInfos)
        {
            string userName;

            if (IsServiceAccount(userId))
            {
                userName = L10n.Tr(Constants.ServiceAccountText);
            }
            else
            {
                var userInfo = userInfos?.Find(ui => ui.UserId == userId);
                userName = userInfo?.Name ?? userId;
            }

            // Create initials icon with USS styling
            var icon = InitialsIconHelper.CreateInitialsIcon(userName, userId);
            icon.AddToClassList(UssStyle.ActivityUserIcon);
            container.Add(icon);

            // Add plain text label
            var nameLabel = new Label(userName);
            nameLabel.AddToClassList(UssStyle.ActivityUserName);
            container.Add(nameLabel);
        }
    }
}
