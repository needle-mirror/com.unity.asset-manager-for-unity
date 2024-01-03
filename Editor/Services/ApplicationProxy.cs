using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal interface IApplicationProxy: IService
    {
       RuntimePlatform platform { get; }
    } 
    
    internal class ApplicationProxy: BaseService<IApplicationProxy>, IApplicationProxy
    {
        public RuntimePlatform platform => Application.platform;
    }
}