using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    class SidebarOrganizationSelectorViewmodel
    {
        [SerializeReference] IPermissionsManager m_PermissionsManager;
        [SerializeReference] IProjectOrganizationProvider m_ProjectOrganizationProvider;
        [SerializeReference] IUnityConnectProxy m_UnityConnectProxy;

        Dictionary<string, NameAndId> m_OrganizationOptions = new();
        Dictionary<string, Role> m_OrganizationRoles = new();
        Dictionary<string, bool> m_OrganizationSeatValidity = new();
        Dictionary<string, Task> m_FetchOrganizationsTasks = new();
        string m_LinkedOrganizationName;

        public event Action<string> SelectedOrganizationChanged;
        public event Action UpdateSelectionChanged;

        public SidebarOrganizationSelectorViewmodel(IPermissionsManager permissionsManager,
            IProjectOrganizationProvider projectOrganizationProvider, IUnityConnectProxy unityConnectProxy)
        {
            m_PermissionsManager = permissionsManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_UnityConnectProxy = unityConnectProxy;

            _ = FetchOrganizationsData();
        }

        public void BindEvents()
        {
            m_PermissionsManager.AuthenticationStateChanged += OnAuthenticationStateChanged;
            m_ProjectOrganizationProvider.OrganizationChanged += OnOrganizationChanged;
            m_UnityConnectProxy.OrganizationIdChanged += OnOrganizationIdChanged;
            PrivateCloudSettings.SettingsUpdated += UpdateSelectionEnabled;
        }

        public void UnbindEvents()
        {
            m_PermissionsManager.AuthenticationStateChanged -= OnAuthenticationStateChanged;
            m_ProjectOrganizationProvider.OrganizationChanged -= OnOrganizationChanged;
            m_UnityConnectProxy.OrganizationIdChanged -= OnOrganizationIdChanged;
            PrivateCloudSettings.SettingsUpdated -= UpdateSelectionEnabled;
        }

        void OnOrganizationChanged(OrganizationInfo organizationInfo)
        {
            _ = FetchOrganizationsData();
        }

        void OnOrganizationIdChanged()
        {
            _ = FetchOrganizationsData();
        }

        void OnAuthenticationStateChanged(AuthenticationState authenticationState)
        {
            if (authenticationState == AuthenticationState.LoggedIn)
            {
                _ = FetchOrganizationsData();
            }
            else
            {
                SelectedOrganizationChanged?.Invoke(string.Empty);
            }

            UpdateSelectionEnabled();
        }

        void UpdateSelectionEnabled()
        {
            UpdateSelectionChanged?.Invoke();
        }

        async Task FetchOrganizationsData()
        {
            m_OrganizationOptions = new Dictionary<string, NameAndId>();
            await foreach (var organization in m_ProjectOrganizationProvider.ListOrganizationsAsync())
            {
                m_OrganizationOptions[organization.Name] = organization;
                if (!m_FetchOrganizationsTasks.ContainsKey(organization.Name))
                    m_FetchOrganizationsTasks[organization.Name] = FetchOrganizationRoleAndEntitlements(organization.Name, organization.Id);
            }

            var linkedOrganizationId = m_UnityConnectProxy.HasValidOrganizationId ? m_UnityConnectProxy.OrganizationId : null;
            m_LinkedOrganizationName =
                linkedOrganizationId != null && m_OrganizationOptions.Values.Any(o => o.Id == linkedOrganizationId)
                    ? m_LinkedOrganizationName = m_OrganizationOptions.Values.First(o => o.Id == linkedOrganizationId).Name
                    : string.Empty;

            var selectedOrganization = m_ProjectOrganizationProvider.SelectedOrganization;

            SelectedOrganizationChanged?.Invoke(selectedOrganization?.Name ?? string.Empty);
        }

        async Task FetchOrganizationRoleAndEntitlements(string organizationName, string organizationId)
        {
            if(!m_OrganizationRoles.ContainsKey(organizationName))
                m_OrganizationRoles[organizationName] = await m_PermissionsManager.GetRoleAsync(organizationId, string.Empty);

            if (!m_OrganizationSeatValidity.ContainsKey(organizationName))
                m_OrganizationSeatValidity[organizationName] = await m_PermissionsManager.CheckSeatValidity(organizationId);
        }

        public string GetLinkedOrganizationName()
        {
            return m_LinkedOrganizationName;
        }

        public bool OrganizationExists(string organizationName)
        {
            return m_OrganizationRoles.ContainsKey(organizationName);
        }

        public string GetOrganizationRole(string organizationName)
        {
            return m_OrganizationRoles[organizationName].ToString();
        }

        public bool IsSeatInvalidForOrganization(string organizationName)
        {
            return m_OrganizationSeatValidity.ContainsKey(organizationName) &&
                   !m_OrganizationSeatValidity[organizationName];
        }

        public Dictionary<string, NameAndId> GetOrganizationOptions()
        {
            return m_OrganizationOptions;
        }

        public void SelectOrganization(string organizationName)
        {
            if (m_OrganizationOptions.TryGetValue(organizationName, out var organization))
                m_ProjectOrganizationProvider.SelectOrganization(organization.Id);

            AnalyticsSender.SendEvent(new OrganizationSelectedEvent());
        }

        public string GetSelectedOrganizationName()
        {
            return m_ProjectOrganizationProvider.SelectedOrganization?.Name ?? string.Empty;
        }
    }
}
