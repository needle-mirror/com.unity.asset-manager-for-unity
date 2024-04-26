using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class AssetManagerUserSettingsProvider : SettingsProvider
    {
        const string k_CacheLocationTitle = "Cache location";
        const string k_AccessError = "Some folders or files could not be accessed.";
        const string k_ShowInExplorerLabel = "Show in explorer";
        const string k_ChangeLocationLabel = "Change location";
        const string k_ResetDefaultLocation = "Reset to Default Location";
        const string k_SettingsProviderPath = "Preferences/Asset Manager";
        const string k_MainDarkUssName = "MainDark";
        const string k_MainLightUssName = "MainLight";
        const int k_MinCacheSizeValueGB = 2;
        const int k_MaxCacheSizeValueGB = 200;
        const string k_AssetManagerDropDown = "assetManagerDropDown";
        const string k_AssetManagerCachePath = "assetManagerCachePath";
        const string k_DisabledErrorBox = "disabledErrorBox";
        const string k_CleanCache = "cleanCache";
        const string k_CacheSizeOnDisk = "cacheSizeOnDisk";
        const string k_MaxCacheSize = "maxCacheSize";
        const string k_RefreshButton = "refresh";
        const string k_ClearExtraCache = "clearExtraCache";

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

        readonly IFileInfoWrapper m_FileInfoWrapper;
        readonly ICachePathHelper m_CachePathHelper;
        readonly IIOProxy m_IOProxy;
        readonly ISettingsManager m_SettingsManager;
        Label m_AssetManagerCachePath;
        Label m_CacheSizeOnDisk;
        Button m_ClearExtraCacheButton;

        HelpBox m_ErroLabel;

        AssetManagerUserSettingsProvider(
            ICachePathHelper cachePathHelper, IFileInfoWrapper fileInfoWrapper, ISettingsManager settingsManager,
            IIOProxy ioProxy,
            string path, IEnumerable<string> keywords = null) :
            base(path, SettingsScope.User, keywords)
        {
            m_CachePathHelper = cachePathHelper;
            m_FileInfoWrapper = fileInfoWrapper;
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

            // setup the cache location
            m_AssetManagerCachePath = rootElement.Q<Label>(k_AssetManagerCachePath);
            m_AssetManagerCachePath.text = SetCacheLocationLabelText(m_SettingsManager.BaseCacheLocation);

            // setup extra cache clean up
            m_ClearExtraCacheButton = rootElement.Q<Button>(k_ClearExtraCache);
            m_ClearExtraCacheButton.clicked += ClearExtraCache;
            SetClearExtraCacheButton();

            // setup the refresh button
            var refreshButton = rootElement.Q<Button>(k_RefreshButton);
            refreshButton.clicked += RefreshCacheSizeOnDiskLabel;
            m_ErroLabel = rootElement.Q<HelpBox>(k_DisabledErrorBox);
            UIElementsUtils.Hide(m_ErroLabel);
            var cleanCacheButton = rootElement.Q<Button>(k_CleanCache);
            cleanCacheButton.clicked += CleanCache;
            m_CacheSizeOnDisk = rootElement.Q<Label>(k_CacheSizeOnDisk);
            try
            {
                m_CacheSizeOnDisk.text =
                    $"{Utilities.BytesToReadableString(m_FileInfoWrapper.GetDirectorySizeBytes(m_SettingsManager.BaseCacheLocation))}";
            }
            catch (UnauthorizedAccessException)
            {
                ResetToDefaultLocation();
                RefreshCacheSizeOnDiskLabel();
            }

            SetupToolBarButton(rootElement);
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
            var cacheSizeBytes = m_FileInfoWrapper.GetDirectorySizeBytes(m_SettingsManager.BaseCacheLocation);
            if (cacheSizeBytes == 0)
            {
                m_ClearExtraCacheButton.SetEnabled(false);
            }

            var cacheSizeGb = ByteSizeConverter.ConvertBytesToGb(cacheSizeBytes);
            m_ClearExtraCacheButton.SetEnabled(cacheSizeGb >= m_SettingsManager.MaxCacheSizeGb);
        }

        void SetupToolBarButton(VisualElement rootElement)
        {
            var button = rootElement.Q<ToolbarMenu>(k_AssetManagerDropDown);
            button.menu.AppendAction(k_ShowInExplorerLabel,
                a => { EditorUtility.RevealInFinder(m_SettingsManager.BaseCacheLocation); });
            button.menu.AppendAction(k_ChangeLocationLabel, a =>
            {
                var cacheLocation =
                    EditorUtility.OpenFolderPanel(k_CacheLocationTitle, m_SettingsManager.BaseCacheLocation,
                        string.Empty);

                // the user clicked cancel
                if (string.IsNullOrEmpty(cacheLocation))
                {
                    return;
                }

                UpdateCachePath(cacheLocation);
            });
            button.menu.AppendAction(k_ResetDefaultLocation,
                a => { ResetToDefaultLocation(); });
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
                SetErrorLabel(k_AccessError);
            }
        }

        void RefreshCacheSizeOnDiskLabel()
        {
            m_CacheSizeOnDisk.text =
                $"{Utilities.BytesToReadableString(m_FileInfoWrapper.GetDirectorySizeBytes(m_SettingsManager.BaseCacheLocation))}";
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
        void ResetToDefaultLocation()
        {
            var defaultLocation = m_CachePathHelper.GetDefaultCacheLocation();
            m_SettingsManager.SetCacheLocation(defaultLocation.FullName);
            m_AssetManagerCachePath.text = SetCacheLocationLabelText(defaultLocation.FullName);
            RefreshCacheSizeOnDiskLabel();
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
            m_AssetManagerCachePath.text = SetCacheLocationLabelText(cacheLocation);
            RefreshCacheSizeOnDiskLabel();
        }

        string CreateUpdateCacheErrorMessage(CacheValidationResultError errorType, string path)
        {
            return cacheValidationErrorMessages.TryGetValue(errorType, out var message) ?
                $"{message}. Reverting to default path.\nPath: {path}" :
                null;
        }

        string SetCacheLocationLabelText(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : Utilities.NormalizePath(path);
        }

        [SettingsProvider]
        public static SettingsProvider CreateUserSettingsProvider()
        {
            var container = ServicesContainer.instance;
            return new AssetManagerUserSettingsProvider(container.Resolve<ICachePathHelper>(),
                container.Resolve<IFileInfoWrapper>(), container.Resolve<ISettingsManager>(),
                container.Resolve<IIOProxy>(),
                k_SettingsProviderPath, new List<string>());
        }
    }
}
