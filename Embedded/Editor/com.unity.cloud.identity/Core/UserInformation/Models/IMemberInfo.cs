using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.IdentityEmbedded
{

    internal class RangeResultsJson<T>
    {
        public int Offset { get; set; }

        public int Limit { get; set; }

        public int Total { get; set; }

        public IEnumerable<T> Results { get; set; }
    }

    internal class AssetProjectPageResultsJson<T>
    {
        public IEnumerable<T> Projects { get; set; }
    }

    internal class MemberInfoJson
    {
        public string Role { get; set; }

        public string GroupGenesisId { get; set; }

        public string GroupName { get; set; }

        public string UserId { get; set; }

        public string UserGenesisId { get; set; }

        public string UserName { get; set; }

        public string UserEmail { get; set; }

    }

    internal class ProjectMemberInfoJson
    {
        public string Id { get; set; }

        public string GenesisId { get; set; }

        public string Name { get; set; }

        public IEnumerable<ProjectMemberInfoRolesJson> Roles { get; set; }

        public string Email { get; set; }
    }

    internal class ProjectMemberInfoRolesJson
    {
        public string Name { get; set; }

        public bool IsLegacy { get; set; }

        public string EntityType { get; set; }
    }

    interface IMemberInfo : IUserInfo
    {
        public string Role { get; set; }

        public GroupId GroupId { get; set; }

        public string GroupName { get; set; }
    }

    internal class MemberInfo : IMemberInfo
    {
        internal MemberInfo(MemberInfoJson memberInfoJson)
        {
            Role = memberInfoJson.Role;
            GroupId = new GroupId(memberInfoJson.GroupGenesisId);
            GroupName = memberInfoJson.GroupName;
            UserId = new UserId(memberInfoJson.UserGenesisId);
            // UserName can sometime be empty, use email instead
            Name = memberInfoJson.UserName ?? memberInfoJson.UserEmail;
            Email = memberInfoJson.UserEmail;
        }

        internal MemberInfo(ProjectMemberInfoJson projectMemberInfoJson)
        {
            var roles = projectMemberInfoJson.Roles.Where(r => r.IsLegacy).ToList();
            // If only one role listed, it's the organization role that also apply to the project
            Role = roles.Count == 1 ? roles[0].Name : GetProjectMemberLegacyRole(roles);
            Role = roles[0].Name;
            GroupId = GroupId.None;
            GroupName = null;
            UserId = new UserId(projectMemberInfoJson.GenesisId);
            // UserName can sometime be empty, use email instead
            Name = projectMemberInfoJson.Name ?? projectMemberInfoJson.Email;
            Email = projectMemberInfoJson.Email;
        }

        string GetProjectMemberLegacyRole(List<ProjectMemberInfoRolesJson> legacyRoles)
        {
            var legacyProjectRole = legacyRoles.Where(r => r.EntityType.Equals("project")).ToList();
            if (legacyProjectRole.Any())
                return legacyProjectRole.First().Name;

            var legacyOrganizationRole = legacyRoles.Where(r => r.EntityType.Equals("organization")).ToList();
            if (legacyOrganizationRole.Any())
                return legacyOrganizationRole.First().Name;
            // Fallback to lowest role possible on Project
            return "user";
        }

        public string Role { get; set; }
        public GroupId GroupId { get; set; }
        public string GroupName { get; set; }
        public UserId UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
    }
}
