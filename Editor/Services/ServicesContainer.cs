using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface IService
    {
        bool enabled { get; set; }
        Type registrationType { get; }
    }

    abstract class BaseService : IService
    {
        public abstract Type registrationType { get; }

        bool m_Enabled;

        public bool enabled
        {
            get => m_Enabled;
            set
            {
                if (m_Enabled == value)
                    return;

                if (value)
                {
                    OnEnable();
                }
                else
                {
                    OnDisable();
                }

                m_Enabled = value;
            }
        }

        public virtual void OnEnable() { }

        public virtual void OnDisable() { }
    }

    abstract class BaseService<T> : BaseService where T : IService
    {
        public override Type registrationType => typeof(T);
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class ServiceInjectionAttribute : Attribute { }

    [Serializable]
    class SerializedService
    {
        [SerializeReference]
        public IService Service;

        [SerializeReference]
        public List<IService> Dependencies;
    }

    [Serializable]
    [ExcludeFromCodeCoverage]
    sealed class ServicesContainer : ScriptableSingleton<ServicesContainer>, ISerializationCallbackReceiver
    {
        [SerializeField]
        List<SerializedService> m_SerializedServices = new();

        readonly Dictionary<IService, List<IService>> m_Dependencies = new();

        readonly Dictionary<Type, IService> m_RegisteredServices = new();

        readonly Dictionary<IService, HashSet<IService>> m_ReverseDependencies = new();

        public void OnEnable()
        {
            if (m_RegisteredServices.Count != 0)
                return;

            // Services creation and Dependency Injection happens once.
            InitializeServices();
        }

        public void InitializeServices()
        {
            if (Utilities.IsDevMode)
            {
                Debug.Log("Initializing Asset Manager Services");
            }

            m_RegisteredServices.Clear();

            Register(new StateManager());
            Register(new EditorGUIUtilityProxy());
            Register(new IOProxy());
            Register(new ApplicationProxy());
            Register(new DirectoryInfoFactory());
            Register(new WebRequestProxy());
            Register(new DownloadManager());
            Register(new CachePathHelper());
            Register(new AssetManagerSettingsManager());
            Register(new FileInfoWrapper());
            Register(new CacheEvictionManager());
            Register(new ThumbnailDownloader());
            Register(new UnityConnectProxy());
            Register(new AssetsSdkProvider());
            Register(new ProjectOrganizationProvider());
            Register(new LinksProxy());
            Register(new AssetDataManager());
            Register(new PageManager());
            Register(new ProjectIconDownloader());
            Register(new AssetDatabaseProxy());
            Register(new ImportedAssetsTracker());
            Register(new EditorUtilityProxy());
            Register(new AssetImporter());

            InjectServicesAndBuildDependencies();

            BuildReverseDependencies();
        }

        public void OnDisable()
        {
            foreach (var service in m_RegisteredServices.Values)
            {
                service.enabled = false;
            }
        }

        void RegisterReverseDependencies(IService service)
        {
            if (!m_Dependencies.TryGetValue(service, out var dependencies))
                return;

            foreach (var dependency in dependencies)
            {
                if (m_ReverseDependencies.TryGetValue(dependency, out var result))
                {
                    result.Add(service);
                }
                else
                {
                    m_ReverseDependencies[dependency] = new HashSet<IService> { service };
                }
            }
        }

        public IService Register(IService service)
        {
            if (service == null)
                return null;

            m_RegisteredServices[service.GetType()] = service;

            var registrationType = service.registrationType;
            if (registrationType != null)
            {
                m_RegisteredServices[registrationType] = service;
            }

            return service;
        }

        void BuildReverseDependencies()
        {
            m_ReverseDependencies.Clear();

            foreach (var service in m_RegisteredServices.Values)
            {
                RegisterReverseDependencies(service);
            }
        }

        public T Resolve<T>() where T : class, IService
        {
            var service = m_RegisteredServices.TryGetValue(typeof(T), out var result) ? result as T : null;
            if (service == null || service.enabled)
                return service;

            var serviceEnablingQueue = new Queue<IService>();
            serviceEnablingQueue.Enqueue(service);
            while (serviceEnablingQueue.Count > 0)
            {
                EnableService(serviceEnablingQueue.Dequeue(), serviceEnablingQueue);
            }

            return service;
        }

        void EnableService(IService service, Queue<IService> serviceEnablingQueue)
        {
            if (service == null || service.enabled)
                return;

            if (m_Dependencies.TryGetValue(service, out var dependencies))
            {
                foreach (var dependency in dependencies)
                {
                    EnableService(dependency, serviceEnablingQueue);
                }
            }

            service.enabled = true;

            // All the reverse dependencies go into the queue to avoid nested enabling
            if (m_ReverseDependencies.TryGetValue(service, out var reverseDependencies))
            {
                foreach (var reverseDependency in reverseDependencies)
                {
                    serviceEnablingQueue.Enqueue(reverseDependency);
                }
            }
        }

        public void InjectServicesAndBuildDependencies()
        {
            m_Dependencies.Clear();

            foreach (var service in m_RegisteredServices.Values)
            {
                InjectService(service);
            }
        }

        public void InjectService(IService inst)
        {
            var type = inst.GetType();
            var methods = type.GetMethods();

            foreach (var method in methods)
            {
                var injectAttribute = (ServiceInjectionAttribute)Attribute.GetCustomAttribute(method, typeof(ServiceInjectionAttribute));
                if (injectAttribute == null)
                    continue;

                var parameters = method.GetParameters();
                var parameterValues = new object[parameters.Length];

                for (var i = 0; i < parameters.Length; i++)
                {
                    var parameterType = parameters[i].ParameterType;
                    if (m_RegisteredServices.ContainsKey(parameterType))
                    {
                        parameterValues[i] = m_RegisteredServices[parameterType];
                    }
                    else
                    {
                        throw new InvalidOperationException($"Service of type {parameterType} not registered.");
                    }
                }

                if (m_Dependencies.TryGetValue(inst, out var list))
                {
                    list.AddRange(parameterValues.Cast<IService>().ToList());
                }
                else
                {
                    m_Dependencies[inst] = parameterValues.Cast<IService>().ToList();
                }

                method.Invoke(inst, parameterValues);
            }
        }

        public void OnBeforeSerialize()
        {
            m_SerializedServices = new List<SerializedService>();

            var processedServices = new HashSet<IService>();

            foreach (var service in m_RegisteredServices.Values)
            {
                if (processedServices.Contains(service))
                    continue;

                m_SerializedServices.Add(new SerializedService
                {
                    Service = service,
                    Dependencies = m_Dependencies.TryGetValue(service, out var dependencies)
                        ? dependencies
                        : new List<IService>()
                });

                processedServices.Add(service);
            }
        }

        public void OnAfterDeserialize()
        {
            if (m_SerializedServices == null || m_SerializedServices.Count == 0)
                return;

            foreach (var serviceInfo in m_SerializedServices)
            {
                Register(serviceInfo.Service);
                m_Dependencies.Add(serviceInfo.Service, serviceInfo.Dependencies);
            }

            BuildReverseDependencies();
        }
    }
}
