using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class AssetManagerUserSettingsProvider : SettingsProvider
    {
        const int k_MinCacheSizeValueGB = 2;
        const int k_MaxCacheSizeValueGB = 200;

        const string k_SettingsProviderPath = "Preferences/Asset Manager";
        const string k_MainDarkUssName = "MainDark";
        const string k_MainLightUssName = "MainLight";

        const string k_AssetManagerTitle = "titleLabel";

        const string k_ImportSettingsFoldout = "importSettingsFoldout";
        const string k_ImportLocationPath = "importLocationPath";
        const string k_ImportLocationDropdown = "importLocationDropdown";
        const string k_ImportDefaultLocationLabel = "importSettingsDefaultLocationLabel";
        const string k_ImportCreateSubFolderLabel = "importSettingsCreateSubfolderLabel";
        const string k_SubfolderCreationToggle = "subfolderCreationToggle";

        const string k_CacheSettingsFoldout = "cacheSettingsFoldout";
        const string k_CacheLocationDropdown = "cacheLocationDropdown";
        const string k_AssetManagerCachePath = "assetManagerCachePath";
        const string k_DisabledErrorBox = "disabledErrorBox";
        const string k_CleanCache = "cleanCache";
        const string k_CacheSizeOnDisk = "cacheSizeOnDisk";
        const string k_MaxCacheSize = "maxCacheSize";
        const string k_RefreshButton = "refresh";
        const string k_ClearExtraCache = "clearExtraCache";
        const string k_LocationLabel = "cacheManagementLocationLabel";
        const string k_MaxSizeLabel = "cacheManagementMaxSizeLabel";
        const string k_SizeLabel = "cacheManagementSizeLabel";

        const string k_UploadSettingsFoldout = "uploadSettingsFoldout";
        const string k_TagsCreationUploadLabel = "tagsCreationUploadLabel";
        const string k_TagsCreationUploadToggle = "tagsCreationUploadToggle";
        const string k_TagsCreationUploadConfidenceLabel = "tagsCreationUploadConfidenceLabel";
        const string k_TagsCreationUploadConfidenceValue = "tagsCreationUploadConfidenceValue";

        static Dictionary<CacheValidationResultError, string> cacheValidationErrorMessages =
            new()
            {
                { CacheValidationResultError.InvalidPath, "The specified path contains invalid characters" },
                { CacheValidationResultError.DirectoryNotFound, "The specified path is invalid" },
                {
                    CacheValidationResultError.PathTooLong,
                    "The specified path exceeds the system-defined maximum length"
                },
                { CacheValidationResultError.CannotWriteToDirectory, "Could not write to directory" }
            };

        readonly ICachePathHelper m_CachePathHelper;
        readonly IIOProxy m_IOProxy;
        readonly ISettingsManager m_SettingsManager;
        Label m_ImportLocationPathLabel;
        Label m_AssetManagerCachePathLabel;
        Label m_CacheSizeOnDisk;
        Button m_ClearExtraCacheButton;

        HelpBox m_ErroLabel;

        AssetManagerUserSettingsProvider(
            ICachePathHelper cachePathHelper, ISettingsManager settingsManager,
            IIOProxy ioProxy,
            string path, IEnumerable<string> keywords = null) :
            base(path, SettingsScope.User, keywords)
        {
            m_CachePathHelper = cachePathHelper;
            m_SettingsManager = settingsManager;
            m_IOProxy = ioProxy;
        }

        /// <summary>
        /// Initializes all the UI elements
        /// </summary>
        /// <param name="searchContext"></param>
        /// <param name="rootElement">the root visual element</param>
        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            UIElementsUtils.LoadUXML("AssetManagerUserSettings").CloneTree(rootElement);
            UIElementsUtils.LoadCommonStyleSheet(rootElement);
            UIElementsUtils.LoadCustomStyleSheet(rootElement,
                EditorGUIUtility.isProSkin ? k_MainDarkUssName : k_MainLightUssName);

            var assetManagerTitle = rootElement.Q<Label>(k_AssetManagerTitle);
            assetManagerTitle.text = L10n.Tr(Constants.AssetManagerTitle);

            // setup the import settings foldout
            var importSettingsFoldout = rootElement.Q<Foldout>(k_ImportSettingsFoldout);
            importSettingsFoldout.text = L10n.Tr(Constants.ImportSettingsTitle);

            // setup the default import location
            var importDefaultLocationLabel = rootElement.Q<Label>(k_ImportDefaultLocationLabel);
            importDefaultLocationLabel.text = L10n.Tr(Constants.ImportDefaultLocation);
            m_ImportLocationPathLabel = rootElement.Q<Label>(k_ImportLocationPath);
            SetPathLabelTextAndTooltip(m_ImportLocationPathLabel, m_SettingsManager.DefaultImportLocation, true);

            // setup the creation subfolder label
            var importCreateSubFolderLabel = rootElement.Q<Label>(k_ImportCreateSubFolderLabel);
            importCreateSubFolderLabel.text = L10n.Tr(Constants.ImportCreateSubfolders);
            importCreateSubFolderLabel.tooltip = L10n.Tr(Constants.ImportCreateSubfoldersTooltip);

            var cacheSettingsFoldout = rootElement.Q<Foldout>(k_CacheSettingsFoldout);
            cacheSettingsFoldout.text = L10n.Tr(Constants.CacheSettingsTitle);

            // setup the cache location
            var locationLabel = rootElement.Q<Label>(k_LocationLabel);
            locationLabel.text = L10n.Tr(Constants.CacheLocation);
            var maxSizeLabel = rootElement.Q<Label>(k_MaxSizeLabel);
            maxSizeLabel.text = L10n.Tr(Constants.CacheMaxSize);
            var sizeLabel = rootElement.Q<Label>(k_SizeLabel);
            sizeLabel.text = L10n.Tr(Constants.CacheSize);
            m_AssetManagerCachePathLabel = rootElement.Q<Label>(k_AssetManagerCachePath);
            SetPathLabelTextAndTooltip(m_AssetManagerCachePathLabel, m_SettingsManager.BaseCacheLocation, false);

            // setup extra cache clean up
            m_ClearExtraCacheButton = rootElement.Q<Button>(k_ClearExtraCache);
            m_ClearExtraCacheButton.text = L10n.Tr(Constants.ClearExtraCache);
            m_ClearExtraCacheButton.clicked += ClearExtraCache;
            SetClearExtraCacheButton();

            // setup the refresh button
            var refreshButton = rootElement.Q<Button>(k_RefreshButton);
            refreshButton.text = L10n.Tr(Constants.CacheRefresh);
            refreshButton.clicked += RefreshCacheSizeOnDiskLabel;
            m_ErroLabel = rootElement.Q<HelpBox>(k_DisabledErrorBox);
            UIElementsUtils.Hide(m_ErroLabel);
            var cleanCacheButton = rootElement.Q<Button>(k_CleanCache);
            cleanCacheButton.text = L10n.Tr(Constants.CleanCache);
            cleanCacheButton.clicked += CleanCache;
            m_CacheSizeOnDisk = rootElement.Q<Label>(k_CacheSizeOnDisk);
            try
            {
                m_CacheSizeOnDisk.text =
                    $"{Utilities.BytesToReadableString(m_IOProxy.GetDirectorySizeBytes(m_SettingsManager.BaseCacheLocation))}";
            }
            catch (UnauthorizedAccessException)
            {
                ResetCacheLocationToDefault();
                RefreshCacheSizeOnDiskLabel();
            }

            // setup subfolder creation toggle
            var createSubFolderToggle = rootElement.Q<Toggle>(k_SubfolderCreationToggle);
            createSubFolderToggle.value = m_SettingsManager.IsSubfolderCreationEnabled;
            createSubFolderToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
            {
                m_SettingsManager.SetIsSubfolderCreationEnabled(evt.newValue);
            });

            var uploadSettingsFoldout = rootElement.Q<Foldout>(k_UploadSettingsFoldout);
            uploadSettingsFoldout.text = L10n.Tr(Constants.UploadSettingsTitle);

            // Setup tags creation confidence threshold
            var tagsCreationConfidenceLabel = rootElement.Q<Label>(k_TagsCreationUploadConfidenceLabel);
            tagsCreationConfidenceLabel.text = L10n.Tr(Constants.TagsCreationConfidenceLevel);
            tagsCreationConfidenceLabel.tooltip = L10n.Tr(Constants.TagsCreationConfidenceLevelTooltip);
            tagsCreationConfidenceLabel.SetEnabled(m_SettingsManager.IsTagsCreationUploadEnabled);
            var tagsCreationConfidenceValue = rootElement.Q<SliderInt>(k_TagsCreationUploadConfidenceValue);
            tagsCreationConfidenceValue.SetEnabled(m_SettingsManager.IsTagsCreationUploadEnabled);
            tagsCreationConfidenceValue.value = m_SettingsManager.TagsConfidenceThresholdPercent;
            tagsCreationConfidenceValue.RegisterValueChangedCallback(evt =>
            {
                m_SettingsManager.SetTagsCreationConfidenceThresholdPercent(evt.newValue);
            });

            // Setup tags creation on upload toggle
            var tagsCreationUploadLabel = rootElement.Q<Label>(k_TagsCreationUploadLabel);
            tagsCreationUploadLabel.text = L10n.Tr(Constants.TagsCreation);
            var tagsCreationUpload = rootElement.Q<Toggle>(k_TagsCreationUploadToggle);
            tagsCreationUpload.value = m_SettingsManager.IsTagsCreationUploadEnabled;
            tagsCreationUpload.RegisterCallback<ChangeEvent<bool>>(evt =>
            {
                m_SettingsManager.SetIsTagsCreationUploadEnabled(evt.newValue);

                tagsCreationConfidenceValue.SetEnabled(evt.newValue);
                tagsCreationConfidenceLabel.SetEnabled(evt.newValue);
            });

            SetupCacheLocationToolbarButton(rootElement);
            SetupImportLocationToolbarButton(rootElement);
            SetupCacheSize(rootElement);
        }

        void SetupCacheSize(VisualElement rootElement)
        {
            var cacheSize = rootElement.Q<SliderInt>(k_MaxCacheSize);
            cacheSize.highValue = k_MaxCacheSizeValueGB;
            cacheSize.lowValue = k_MinCacheSizeValueGB;
            cacheSize.value = m_SettingsManager.MaxCacheSizeGb;
            cacheSize.RegisterValueChangedCallback(evt => { m_SettingsManager.SetMaxCacheSize(evt.newValue); });
            cacheSize.RegisterCallback<MouseLeaveEvent>(_ => SetClearExtraCacheButton());
        }

        void SetClearExtraCacheButton()
        {
            var cacheSizeBytes = m_IOProxy.GetDirectorySizeBytes(m_SettingsManager.BaseCacheLocation);
            if (cacheSizeBytes == 0)
            {
                m_ClearExtraCacheButton.SetEnabled(false);
            }

            var cacheSizeGb = ByteSizeConverter.ConvertBytesToGb(cacheSizeBytes);
            m_ClearExtraCacheButton.SetEnabled(cacheSizeGb >= m_SettingsManager.MaxCacheSizeGb);
        }

        void SetupCacheLocationToolbarButton(VisualElement rootElement)
        {
            var cacheLocationDropDown = rootElement.Q<ToolbarMenu>(k_CacheLocationDropdown);

            cacheLocationDropDown.menu.AppendAction(GetShowInExplorerLabel(),
                a => { EditorUtility.RevealInFinder(m_SettingsManager.BaseCacheLocation); });
            cacheLocationDropDown.menu.AppendAction(L10n.Tr(Constants.ChangeLocationLabel), a =>
            {
                var cacheLocation =
                    EditorUtility.OpenFolderPanel(L10n.Tr(Constants.CacheLocationTitle), m_SettingsManager.BaseCacheLocation,
                        string.Empty);

                // the user clicked cancel
                if (string.IsNullOrEmpty(cacheLocation))
                {
                    return;
                }

                UpdateCachePath(cacheLocation);
            });
            cacheLocationDropDown.menu.AppendAction(L10n.Tr(Constants.ResetDefaultLocation),
                a => { ResetCacheLocationToDefault(); });
        }

        void SetupImportLocationToolbarButton(VisualElement rootElement)
        {
            var importLocationDropdown = rootElement.Q<ToolbarMenu>(k_ImportLocationDropdown);
            importLocationDropdown.menu.AppendAction(GetShowInExplorerLabel(),
                a => { EditorUtility.RevealInFinder(m_SettingsManager.DefaultImportLocation); });
            importLocationDropdown.menu.AppendAction(L10n.Tr(Constants.ChangeLocationLabel), a =>
            {
                string importLocation = Utilities.OpenFolderPanelInDirectory(L10n.Tr(Constants.ImportLocationTitle),
                    m_SettingsManager.DefaultImportLocation);

                // the user clicked cancel
                if (string.IsNullOrEmpty(importLocation))
                {
                    return;
                }

                UpdateDefaultImportPath(importLocation);
            });
            importLocationDropdown.menu.AppendAction(L10n.Tr(Constants.ResetDefaultLocation),
                a => { ResetImportLocationToDefault(); });
        }

        static string GetShowInExplorerLabel()
        {
            return Application.platform == RuntimePlatform.OSXEditor ? L10n.Tr(Constants.RevealInFinder) : L10n.Tr(Constants.ShowInExplorerLabel);
        }

        void CleanCache()
        {
            var result = true;
            try
            {
                result = m_IOProxy.DeleteAllFilesAndFoldersFromDirectory(m_SettingsManager.BaseCacheLocation);
                RefreshCacheSizeOnDiskLabel();
            }
            catch (UnauthorizedAccessException)
            {
                result = false;
            }

            if (!result)
            {
                SetErrorLabel(L10n.Tr(Constants.AccessError));
            }
        }

        void RefreshCacheSizeOnDiskLabel()
        {
            m_CacheSizeOnDisk.text =
                $"{Utilities.BytesToReadableString(m_IOProxy.GetDirectorySizeBytes(m_SettingsManager.BaseCacheLocation))}";
        }

        void ClearExtraCache()
        {
            CacheEvaluationEvent.RaiseEvent();
        }

        void SetErrorLabel(string message)
        {
            UIElementsUtils.Show(m_ErroLabel);
            m_ErroLabel.text = message;
        }

        /// <summary>
        /// Resets cache to default location depending on the operating system
        /// </summary>
        void ResetCacheLocationToDefault()
        {
            var defaultLocation = m_SettingsManager.ResetCacheLocation();
            SetPathLabelTextAndTooltip(m_AssetManagerCachePathLabel, defaultLocation, false);
            RefreshCacheSizeOnDiskLabel();
        }

        void ResetImportLocationToDefault()
        {
            var defaultLocation = m_SettingsManager.ResetImportLocation();
            SetPathLabelTextAndTooltip(m_ImportLocationPathLabel, defaultLocation, true);
        }

        /// <summary>
        /// Validates the new cache location and updates the settings
        /// if the location is invalid (ex. doesn't exist) an error label will be shown
        /// </summary>
        /// <param name="cacheLocation">the new cache location</param>
        void UpdateCachePath(string cacheLocation)
        {
            var validationResult = m_CachePathHelper.EnsureBaseCacheLocation(cacheLocation);

            if (!validationResult.Success)
            {
                SetErrorLabel(CreateUpdateCacheErrorMessage(validationResult.ErrorType, cacheLocation));
                return;
            }

            UIElementsUtils.Hide(m_ErroLabel);
            m_SettingsManager.SetCacheLocation(cacheLocation);
            SetPathLabelTextAndTooltip(m_AssetManagerCachePathLabel, cacheLocation, false);
            RefreshCacheSizeOnDiskLabel();
        }

        void UpdateDefaultImportPath(string importLocation)
        {
            if (!Directory.Exists(importLocation))
            {
                SetErrorLabel(L10n.Tr(Constants.DirectoryDoesNotExistError));
                return;
            }

            UIElementsUtils.Hide(m_ErroLabel);
            m_SettingsManager.DefaultImportLocation = importLocation;
            SetPathLabelTextAndTooltip(m_ImportLocationPathLabel, importLocation, true);
        }

        static string CreateUpdateCacheErrorMessage(CacheValidationResultError errorType, string path)
        {
            return cacheValidationErrorMessages.TryGetValue(errorType, out var message)
                ? $"{message}. Reverting to default path.\nPath: {path}"
                : null;
        }

        static void SetPathLabelTextAndTooltip(TextElement label, string path, bool isRelativePath)
        {
            string finalPath;
            if (isRelativePath && !string.IsNullOrEmpty(path))
            {
                finalPath = Utilities.GetPathRelativeToAssetsFolderIncludeAssets(path);
            }
            else
            {
                finalPath = path;
            }

            label.text = label.tooltip = Utilities.NormalizePathSeparators(finalPath);
        }

        [SettingsProvider]
        public static SettingsProvider CreateUserSettingsProvider()
        {
            var container = ServicesContainer.instance;
            return new AssetManagerUserSettingsProvider(container.Resolve<ICachePathHelper>(),
                container.Resolve<ISettingsManager>(),
                container.Resolve<IIOProxy>(),
                k_SettingsProviderPath, new List<string>());
        }
    }
}
