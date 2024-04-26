using System;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface IApplicationProxy : IService
    {
        RuntimePlatform Platform { get; }
    }

    class ApplicationProxy : BaseService<IApplicationProxy>, IApplicationProxy
    {
        public RuntimePlatform Platform => Application.platform;
    }
}
