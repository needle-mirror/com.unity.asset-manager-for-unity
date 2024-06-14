using System;

namespace Unity.AssetManager.Editor
{
    [Flags]
    enum UIEnabledStates
    {
        None = 0,
        CanImport = 1,
        InProject = 2,
        HasPermissions = 4,
        ServicesReachable = 8,
        ValidStatus = 16,
        IsImporting = 32,
    }
}
