using System;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
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
