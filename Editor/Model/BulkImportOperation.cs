using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.AssetManager.Editor
{
    class BulkImportOperation : BaseOperation
    {
        public override string OperationName => "Importing all selected assets";
        public override string Description { get; }
        public override float Progress { get; }
        public override bool StartIndefinite => true;

        List<ImportOperation> m_ImportOperations;

        public BulkImportOperation(List<ImportOperation> mImportOperations)
        {
            m_ImportOperations = mImportOperations;
            foreach (var importOperation in m_ImportOperations)
            {
                importOperation.Finished += OnImportCompleted;
            }
        }

        public void OnImportCompleted(OperationStatus status)
        {
            if (status is OperationStatus.Cancelled or OperationStatus.Error
                || m_ImportOperations.TrueForAll(x => x.Status == OperationStatus.Success))
            {
                SendImportEndAnalytic(status);
                Finish(status);
            }
        }

        void SendImportEndAnalytic(OperationStatus finalStatus)
        {
            string errorMessage = string.Empty;
            ImportEndStatus status;
            switch (finalStatus)
            {
                case OperationStatus.Success:
                    status = ImportEndStatus.Ok;
                    break;
                case OperationStatus.Cancelled:
                    status = ImportEndStatus.Cancelled;
                    break;
                case OperationStatus.Error:
                    status = ImportEndStatus.DownloadError;
                    foreach (var importOperation in m_ImportOperations)
                    {
                        if(importOperation.Status == OperationStatus.Error){
                            foreach (var downloadOperation in importOperation.Downloads)
                            {
                                if (downloadOperation.Status == OperationStatus.Error)
                                {
                                    errorMessage = downloadOperation.Error;
                                    break;
                                }
                            }

                            break;
                        }
                    }
                    break;
                default:
                    status = ImportEndStatus.GenericError;
                    break;
            }

            AnalyticsSender.SendEvent(new ImportEndEvent(status,
                m_ImportOperations.Select(io => io.AssetId.AssetId).ToList(),
                m_ImportOperations.Min(io => io.StartTime),
                DateTime.Now,
                errorMessage));
        }
    }
}