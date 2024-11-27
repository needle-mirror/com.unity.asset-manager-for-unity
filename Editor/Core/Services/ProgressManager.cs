using System;

namespace Unity.AssetManager.Core.Editor
{
    interface IProgressManager : IService
    {
        void Start(string message);
        void Stop();
        void SetProgress(float progress);

        event Action<string> Show;
        event Action Hide;
        event Action<float> Progress;
    }

    class ProgressManager: BaseService<IProgressManager>, IProgressManager
    {
        public event Action<string> Show;
        public event Action Hide;
        public event Action<float> Progress;

        public void Start(string message)
        {
            Show?.Invoke(message);
        }

        public void Stop()
        {
            Hide?.Invoke();
        }

        public void SetProgress(float progress)
        {
            Progress?.Invoke(progress);
        }
    }
}
