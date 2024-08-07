using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    static partial class UssStyle
    {
        public const string ReimportWindow = "reimport-window";
        public const string ReimportWindowContent = ReimportWindow + "-content";
        public const string ReimportWindowFooter = ReimportWindow + "-footer";
        public const string ReimportWindowConflictsTitle = ReimportWindow + "-conflicts-title";
        public const string ReimportWindowWarningContainer = ReimportWindow + "-warning-container";
        public const string ReimportWindowWarningIcon = ReimportWindow + "-warning-icon";
        public const string ReimportWindowDependentsTitle = ReimportWindow + "-dependents-title";
        public const string ReimportWindowUpwardDependenciesTitle = ReimportWindow + "-upward-dependencies-title";
        public const string ReimportWindowGap = ReimportWindow + "-gap";
    }

    class ReimportWindow : EditorWindow
    {
        static readonly Vector2 k_MinWindowSize = new(350, 50);
        static readonly string k_WindowTitle = "Reimport";
        const string k_MainDarkUssName = "MainDark";
        const string k_MainLightUssName = "MainLight";

        Action<IEnumerable<ResolutionData>> m_Callback;
        Action m_CancelCallback;

        VisualElement m_ConflictsContainer;
        VisualElement m_NonConflictingContainer;
        VisualElement m_DependentsContainer;
        VisualElement m_UpwardDependenciesContainer;

        readonly List<VisualElement> m_Gaps = new ();
        readonly List<ConflictsFoldout> m_ConflictsFoldouts = new();
        readonly List<ReimportItem> m_ReimportItems = new();
        IEnumerable<ResolutionData> m_Resolutions;

        public static void CreateModalWindow(UpdatedAssetData data, Action<IEnumerable<ResolutionData>> callback = null, Action cancelCallback = null)
        {
            ReimportWindow window = GetWindow<ReimportWindow>(k_WindowTitle);
            window.minSize = k_MinWindowSize;

            window.m_Callback = callback;
            window.m_CancelCallback = cancelCallback;
            window.CreateConflictsList(data);
            window.CreateDependentsList(data);
            window.CreateUpwardDependenciesList(data);

            window.ShowModal();
        }

        void OnDestroy()
        {
            if(m_Resolutions?.Any() ?? false)
            {
                m_Callback?.Invoke(m_Resolutions);
            }
            else
            {
                m_CancelCallback?.Invoke();
            }
        }

        void CreateGUI()
        {
            UIElementsUtils.LoadCommonStyleSheet(rootVisualElement);
            UIElementsUtils.LoadCustomStyleSheet(rootVisualElement,
                EditorGUIUtility.isProSkin ? k_MainDarkUssName : k_MainLightUssName);

            // Main container
            var content = new ScrollView();
            content.AddToClassList(UssStyle.ReimportWindowContent);
            rootVisualElement.Add(content);

            // Conflicts
            m_ConflictsContainer = new VisualElement();
            content.Add(m_ConflictsContainer);

            m_NonConflictingContainer = new VisualElement();
            content.Add(m_NonConflictingContainer);

            var conflictsTitle = new Label(L10n.Tr(Constants.ReimportWindowConflictsTitle));
            conflictsTitle.AddToClassList(UssStyle.ReimportWindowConflictsTitle);
            m_ConflictsContainer.Add(conflictsTitle);

            var conflictsWarningContainer = new VisualElement();
            conflictsWarningContainer.AddToClassList(UssStyle.ReimportWindowWarningContainer);
            m_ConflictsContainer.Add(conflictsWarningContainer);

            var conflictsWarningIcon = new Image();
            conflictsWarningIcon.AddToClassList(UssStyle.ReimportWindowWarningIcon);
            conflictsWarningContainer.Add(conflictsWarningIcon);

            var conflictsWarning = new Label(L10n.Tr(Constants.ReimportWindowConflictsWarning));
            conflictsWarningContainer.Add(conflictsWarning);

            var gap = new VisualElement();
            gap.AddToClassList(UssStyle.ReimportWindowGap);
            content.Add(gap);
            m_Gaps.Add(gap);

            // Updated Dependencies
            m_DependentsContainer = new VisualElement();
            content.Add(m_DependentsContainer);

            var dependentsTitle = new Label(L10n.Tr(Constants.ReimportWindowDependentsTitle));
            dependentsTitle.AddToClassList(UssStyle.ReimportWindowDependentsTitle);
            m_DependentsContainer.Add(dependentsTitle);

            gap = new VisualElement();
            gap.AddToClassList(UssStyle.ReimportWindowGap);
            content.Add(gap);
            m_Gaps.Add(gap);

            // Upward Dependencies
            m_UpwardDependenciesContainer = new VisualElement();
            content.Add(m_UpwardDependenciesContainer);

            var upwardDependenciesTitle = new Label(L10n.Tr(Constants.ReimportWindowUpwardDependenciesTitle));
            upwardDependenciesTitle.AddToClassList(UssStyle.ReimportWindowUpwardDependenciesTitle);
            m_UpwardDependenciesContainer.Add(upwardDependenciesTitle);

            // Footer button
            var footer = new VisualElement();
            footer.AddToClassList(UssStyle.ReimportWindowFooter);
            rootVisualElement.Add(footer);

            var cancelButton = new Button(() =>
            {
                Close();
            })
            {
                text = L10n.Tr(Constants.ReimportWindowCancel)
            };
            footer.Add(cancelButton);

            var okButton = new Button(() =>
            {
                var conflictsResolutions = m_ConflictsFoldouts.Select(foldout => new ResolutionData
                {
                    AssetData = foldout.AssetData,
                    ResolutionSelection = foldout.ResolutionSelection
                });
                var reimportResolutions = m_ReimportItems.Select(item => new ResolutionData
                {
                    AssetData = item.AssetData,
                    ResolutionSelection = item.ResolutionSelection
                });
                m_Resolutions = conflictsResolutions.Union(reimportResolutions);
                Close();
            })
            {
                text = L10n.Tr(Constants.ReimportWindowImport)
            };
            footer.Add(okButton);
        }

        void CreateConflictsList(UpdatedAssetData updatedAssetData)
        {
            // This will be the right way to do it when conflicts will be properly detected
            // var allConflictedData = updatedAssetData.Assets.Where(a=> a.HasConflicts).Union(updatedAssetData.Dependants.Where(a=>a.HasConflicts));
            var allConflictedData = updatedAssetData.Assets.Where(a=> a.Existed).Union(updatedAssetData.Dependants.Where(a=>a.Existed));

            if (!allConflictedData.Any())
            {
                UIElementsUtils.Hide(m_ConflictsContainer);
                UIElementsUtils.Hide(m_NonConflictingContainer);
                return;
            }

            var showedData = updatedAssetData.Assets.Union(updatedAssetData.Dependants);

            foreach (var data in showedData)
            {
                if (data.HasConflicts)
                {
                    var conflictsFoldout = new ConflictsFoldout(data);
                    m_ConflictsFoldouts.Add(conflictsFoldout);
                    m_ConflictsContainer.Add(conflictsFoldout);
                }
                else
                {
                    var nonConflictingItem = new ReimportItem(data);
                    m_ReimportItems.Add(nonConflictingItem);
                    m_NonConflictingContainer.Add(nonConflictingItem);
                }
            }
        }

        void CreateDependentsList(UpdatedAssetData updatedAssetData)
        {
            // TODO: Keep this hidden until design decision
            UIElementsUtils.Hide(m_DependentsContainer);

            var filteredDependants = updatedAssetData.Dependants.Where(a => a.HasChanges && !a.HasConflicts);

            if (!filteredDependants.Any())
            {
                UIElementsUtils.Hide(m_DependentsContainer);
                return;
            }

            if (UIElementsUtils.IsDisplayed(m_ConflictsContainer))
            {
                UIElementsUtils.Show(m_Gaps[0]);
            }

            foreach (var data in filteredDependants)
            {
                var dependentItem = new ReimportItem(data);
                m_DependentsContainer.Add(dependentItem);
            }
        }

        void CreateUpwardDependenciesList(UpdatedAssetData updatedAssetData)
        {
            if (!updatedAssetData.UpwardDependencies.Any())
            {
                UIElementsUtils.Hide(m_UpwardDependenciesContainer);
                return;
            }

            if (UIElementsUtils.IsDisplayed(m_DependentsContainer) || UIElementsUtils.IsDisplayed(m_ConflictsContainer))
            {
                UIElementsUtils.Show(m_Gaps[1]);
            }

            foreach (var data in updatedAssetData.UpwardDependencies)
            {
                var upwardDependencyItem = new UpwardDependencyItem(data);
                m_UpwardDependenciesContainer.Add(upwardDependencyItem);
            }
        }
    }
}
