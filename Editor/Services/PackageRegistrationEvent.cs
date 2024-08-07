using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;

namespace Unity.AssetManager.Editor
{
    /// <summary>
    /// This class will close the Asset Manager windows if it is open at the moment the Asset Manager package is removed.
    /// </summary>
    [InitializeOnLoad]
    static class PackageRegistrationEvent
    {
        static PackageRegistrationEvent()
        {
            void Handle(PackageRegistrationEventArgs args)
            {
                foreach (var info in args.removed)
                {
                    if (info.name == Constants.PackageName)
                    {
                        if (AssetManagerWindow.Instance != null)
                        {
                            AssetManagerWindow.Instance.Close();
                        }
                        return;
                    }
                }
            }
            
            void RefreshAssetManagerWindow(PackageRegistrationEventArgs args)
            {
                foreach (var info in args.changedTo)
                {
                    if (info.name == Constants.PackageName && EditorWindow.HasOpenInstances<AssetManagerWindow>())
                    {
                        var window = EditorWindow.GetWindow<AssetManagerWindow>();
                        window.RefreshAll();
                    }
                }
            }

            //Event raised before applying changes to the registered packages list.
            //Occurs before the asset database begins refreshing.Packages about to be modified or
            //removed are still present and functional, because the package registration process has not yet begun.
            Events.registeringPackages += Handle;

            //Event raised before applying changes to the registered packages list.
            Events.registeredPackages += RefreshAssetManagerWindow;
        }
    }
}
