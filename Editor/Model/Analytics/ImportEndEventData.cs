using System;
using UnityEngine.Analytics;

namespace Unity.AssetManager.Editor.Model.Analytics
{
#if UNITY_2023_2_OR_NEWER
    [Serializable]
    internal class ImportEndEventData : IAnalytic.IData
#else
    struct ImportEndEventData
#endif
    {
#if !UNITY_2023_2_OR_NEWER
        public const string eventName = "assetManagerImportEnd";
        public const int eventVersion = 2;
#endif

        /// <summary>
        /// The ID of the asset being imported
        /// </summary>
        public string assetId;
        
        /// <summary>
        /// The amount of milliseconds the import operation took
        /// </summary>
        public long elapsedTime;
        
        /// <summary>
        /// The error message if any
        /// </summary>
        public string errorMessage;
        
        /// <summary>
        /// Which action triggered the import operation. See Enums/ImportAction.cs
        /// </summary>
        public string importAction;
        
        /// <summary>
        /// A string identifying the target of the import operation (e.g. project browser, inspector). See Enums/ImportEndImportTarget.cs
        /// </summary>
        public string importTarget;
        
        /// <summary>
        /// Timestamp when the operation started
        /// </summary>
        public long startTime;

        /// <summary>
        /// The ending status of the operation. See Enums/ImportEndStatus.cs
        /// </summary>
        public string status;
    }
}