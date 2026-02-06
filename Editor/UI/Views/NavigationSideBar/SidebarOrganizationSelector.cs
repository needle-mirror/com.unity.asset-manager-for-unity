using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using AuthenticationState = Unity.AssetManager.Core.Editor.AuthenticationState;

namespace Unity.AssetManager.UI.Editor
{
    class SidebarOrganizationSelector : VisualElement
    {
        const string k_UssClassName = "unity-org-selector";
        const string k_ButtonUssClassName = k_UssClassName + "-button";
        const string k_ButtonDisabledUssClassName = k_ButtonUssClassName + "--disabled";
        const string k_OrganizationChoice = k_UssClassName + "-choice";
        const string k_OrganizationChoiceSeparatorLine = k_UssClassName + "-choice-separator-line";
        const string k_OrganizationChoiceText = k_UssClassName + "-choice-text";
        const string k_OrganizationChoiceRoleContainer = k_UssClassName + "-choice-role-container";
        const string k_OrganizationChoiceRole = k_OrganizationChoice + "-role";
        const string k_OrganizationChoiceSeatWarning = k_OrganizationChoice + "-seat-warning";
        const string k_OrganizationChoiceCheckmark = k_OrganizationChoice + "-checkmark";
        const string k_DefaultOrgTooltip = "If configured, the Organization linked within " +
                                           "the Project Settings will display at the top of the Organization List.";

        readonly IPopupManager m_PopupManager;
        readonly SidebarOrganizationSelectorViewmodel m_ViewModel;

        Button m_OrganizationButton;
        TextElement m_OrganizationButtonText;
        VisualElement m_Caret;

        static bool IsSelectionEnabled
        {
            get
            {
                var privateCloudSettings = PrivateCloudSettings.Load();
                return !privateCloudSettings.ServicesEnabled;
            }
        }

        public SidebarOrganizationSelector(SidebarOrganizationSelectorViewmodel viewmodel, IPopupManager popupManager)
        {
            m_ViewModel = viewmodel;

            m_PopupManager = popupManager;

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);

            InitializeUI();
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_ViewModel.BindEvents();
            m_ViewModel.SelectedOrganizationChanged += OnSelectedOrganizationChanged;
            m_ViewModel.UpdateSelectionChanged += UpdateSelectionEnabled;
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_ViewModel.UnbindEvents();
            m_ViewModel.SelectedOrganizationChanged -= OnSelectedOrganizationChanged;
            m_ViewModel.UpdateSelectionChanged -= UpdateSelectionEnabled;
        }

        void InitializeUI()
        {
            AddToClassList(k_UssClassName);

            m_OrganizationButton = new Button
            {
                tooltip = k_DefaultOrgTooltip
            };
            m_OrganizationButton.AddToClassList(k_ButtonUssClassName);

            m_Caret = new VisualElement();
            m_Caret.AddToClassList("unity-org-selector-caret");

            m_OrganizationButtonText = new TextElement();
            m_OrganizationButtonText.text = L10n.Tr(Constants.LoadingText);
            m_OrganizationButtonText.AddToClassList("unity-text-element");
            m_OrganizationButton.Add(m_OrganizationButtonText);

            m_OrganizationButton.Add(m_Caret);
            Add(m_OrganizationButton);

            m_OrganizationButton.RegisterCallback<ClickEvent>( evt =>
            {
                if (!IsSelectionEnabled || m_ViewModel.GetOrganizationOptions().Count <= 1)
                    return;

                BuildOrganizationSelection();
                m_PopupManager.Show(m_OrganizationButton, PopupContainer.PopupAlignment.BottomLeft);
                m_OrganizationButton.tooltip = null;
            });

            m_PopupManager.Container.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (m_PopupManager.Container.style.display == DisplayStyle.None && string.IsNullOrEmpty(m_OrganizationButton.tooltip))
                    m_OrganizationButton.tooltip = IsSelectionEnabled ? k_DefaultOrgTooltip : null;
            });

            UpdateSelectionEnabled();
        }

        void OnSelectionChanged(string organizationName)
        {
            m_ViewModel.SelectOrganization(organizationName);
        }

        void OnSelectedOrganizationChanged(string selectedOrganizationName)
        {
            if (!string.IsNullOrEmpty(selectedOrganizationName))
            {
                SetSelectedOrganizationWithoutNotify(selectedOrganizationName);
            }
        }


        void BuildOrganizationSelection()
        {
            m_PopupManager.Clear();
            var organizationSelection = new ScrollView();
            var selectedOrganizationName = m_ViewModel.GetSelectedOrganizationName();

            if (!string.IsNullOrEmpty(m_ViewModel.GetLinkedOrganizationName()))
            {
                AddOrganizationItem(organizationSelection, m_ViewModel.GetLinkedOrganizationName(), selectedOrganizationName);

                var line = new VisualElement();
                line.AddToClassList(k_OrganizationChoiceSeparatorLine);
                organizationSelection.Add(line);
            }

            foreach (var organizationName in m_ViewModel.GetOrganizationOptions().Keys.OrderBy(organization => organization).ToList())
            {
                if (organizationName == m_ViewModel.GetLinkedOrganizationName())
                    continue;

                AddOrganizationItem(organizationSelection, organizationName, selectedOrganizationName);
            }

            m_PopupManager.Container.Add(organizationSelection);
        }

        void AddOrganizationItem(ScrollView organizationSelection, string organizationName, string selectedOrganizationName)
        {
            var organizationChoice = new VisualElement();
            organizationChoice.AddToClassList(k_OrganizationChoice);

            // Add checkmark or spacer for alignment
            var checkmark = new VisualElement();
            checkmark.AddToClassList(k_OrganizationChoiceCheckmark);
            if (organizationName != selectedOrganizationName)
            {
                checkmark.style.opacity = 0; // Invisible spacer for alignment
            }

            organizationChoice.Add(checkmark);

            var organizationChoiceName = new TextElement();
            organizationChoiceName.AddToClassList(k_OrganizationChoiceText);
            organizationChoiceName.text = organizationName;
            organizationChoice.Add(organizationChoiceName);

            var roleAndSeatContainer = new VisualElement();
            roleAndSeatContainer.AddToClassList(k_OrganizationChoiceRoleContainer);

            if (m_ViewModel.OrganizationExists(organizationName))
            {
                var orgRoleCapsule = new TextElement
                {
                    text = m_ViewModel.GetOrganizationRole(organizationName)
                };
                orgRoleCapsule.AddToClassList(k_OrganizationChoiceRole);
                roleAndSeatContainer.Add(orgRoleCapsule);

                if (m_ViewModel.IsSeatInvalidForOrganization(organizationName))
                {
                    //add a warning
                    var seatWarning = new Image();
                    seatWarning.AddToClassList(k_OrganizationChoiceSeatWarning);
                    seatWarning.tooltip = Constants.NoSeatAssignedWarning;
                    roleAndSeatContainer.Add(seatWarning);
                }
            }

            organizationChoice.Add(roleAndSeatContainer);

            organizationChoice.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                OnSelectionChanged(organizationName);
                m_PopupManager.Hide();
            });


            organizationSelection.Add(organizationChoice);
        }

        void UpdateSelectionEnabled()
        {
            if (IsSelectionEnabled)
            {
                m_OrganizationButton.RemoveFromClassList(k_ButtonDisabledUssClassName);
                m_Caret.style.display = DisplayStyle.Flex;
                m_OrganizationButton.tooltip = k_DefaultOrgTooltip;
            }
            else
            {
                m_OrganizationButton.AddToClassList(k_ButtonDisabledUssClassName);
                m_Caret.style.display = DisplayStyle.None;
                m_OrganizationButton.tooltip = null;
            }
        }

        void SetSelectedOrganizationWithoutNotify(string organizationName)
        {
            if (m_ViewModel.GetOrganizationOptions().ContainsKey(organizationName))
                m_OrganizationButtonText.text = organizationName;
        }
    }
}
