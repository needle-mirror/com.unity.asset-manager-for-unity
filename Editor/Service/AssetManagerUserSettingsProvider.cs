using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class AssetManagerUserSettingsProvider : SettingsProvider
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
        internal const string k_AssetManagerDropDown = "assetManagerDropDown";
        internal const string k_AssetManagerCachePath = "assetManagerCachePath";
        internal const string k_DisabledErrorBox = "disabledErrorBox";
        internal const string k_CleanCache = "cleanCache";
        internal const string k_CacheSizeOnDisk = "cacheSizeOnDisk";
        internal const string k_MaxCacheSize = "maxCacheSize";
        internal const string k_RefreshButton = "refresh";
        internal const string k_ClearExtraCache = "clearExtraCache";

        HelpBox m_ErroLabel;
        Label m_AssetManagerCachePath;
        Label m_CacheSizeOnDisk;
        Button m_ClearExtraCacheButton;

        private readonly IFileInfoWrapper FileInfoWrapper;
        private readonly IIOProxy m_IOProxy;
        private readonly ISettingsManager m_SettingsManager;
        internal AssetManagerUserSettingsProvider(
            IFileInfoWrapper fileInfoWrapper, ISettingsManager settingsManager, IIOProxy ioProxy,
            string path, IEnumerable<string> keywords = null) :
            base(path, SettingsScope.User, keywords)
        {
            FileInfoWrapper = fileInfoWrapper;
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
                    $"{Utilities.BytesToReadableString(FileInfoWrapper.GetDirectorySizeBytes(m_SettingsManager.BaseCacheLocation))}";
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
            cacheSize.RegisterValueChangedCallback((evt) =>
            {
                m_SettingsManager.SetMaxCacheSize(evt.newValue);
            });
            cacheSize.RegisterCallback<MouseLeaveEvent>(_ => SetClearExtraCacheButton());
        }

        private void SetClearExtraCacheButton()
        {
            var cacheSizeBytes = FileInfoWrapper.GetDirectorySizeBytes(m_SettingsManager.BaseCacheLocation);
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
                $"{Utilities.BytesToReadableString(FileInfoWrapper.GetDirectorySizeBytes(m_SettingsManager.BaseCacheLocation))}";
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
        internal void ResetToDefaultLocation()
        {
            var defaultLocation = CachePathHelper.GetDefaultCacheLocation();
            m_SettingsManager.SetCacheLocation(defaultLocation);
            m_AssetManagerCachePath.text = SetCacheLocationLabelText(defaultLocation);
            RefreshCacheSizeOnDiskLabel();
        }

        /// <summary>
        /// Validates the new cache location and updates the settings
        /// if the location is invalid (ex. doesn't exist) an error label will be shown
        /// </summary>
        /// <param name="cacheLocation">the new cache location</param>
        internal void UpdateCachePath(string cacheLocation)
        {
            var validationResult = CachePathHelper.ValidateBaseCacheLocation(cacheLocation);
            if (!validationResult.Success)
            {
                SetErrorLabel(validationResult.Message);
                return;
            }

            UIElementsUtils.Hide(m_ErroLabel);
            m_SettingsManager.SetCacheLocation(cacheLocation);
            m_AssetManagerCachePath.text = SetCacheLocationLabelText(cacheLocation);
            RefreshCacheSizeOnDiskLabel();
        }

        private string SetCacheLocationLabelText(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : Utilities.NormalizePath(path);
        }

        [SettingsProvider]
        public static SettingsProvider CreateUserSettingsProvider()
        {
            var container = ServicesContainer.instance;
            return new AssetManagerUserSettingsProvider(container.Resolve<IFileInfoWrapper>(), container.Resolve<ISettingsManager>(), container.Resolve<IIOProxy>(),
                k_SettingsProviderPath, new List<string>());
        }
    }
}