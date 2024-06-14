using System;
using System.Collections.Generic;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class UserEntitlements
    {
        public IEnumerable<UserEntitlement> Results { get; set; }
    }
    
    [Serializable]
    class UserEntitlement
    {
        public string Tag { get; set; }
        // Not all Entitlement contains this field
        public string AssignFrom { get; set; }
    }
}