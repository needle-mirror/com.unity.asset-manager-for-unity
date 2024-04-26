using System;
using System.Collections.Generic;
using System.IO;

namespace Unity.AssetManager.Editor
{
    interface IDirectoryInfoProxy
    {
        string FullName { get; }
        bool Exists { get; }
        void Create();
        IEnumerable<FileInfo> EnumerateFiles(string searchPattern, SearchOption searchOption);
        IEnumerable<DirectoryInfo> EnumerateDirectories();
        void Delete(bool recursive);
    }

    interface IDirectoryInfoFactory : IService
    {
        IDirectoryInfoProxy Create(string path);
    }

    class DirectoryInfoFactory : BaseService<IDirectoryInfoFactory>, IDirectoryInfoFactory
    {
        public IDirectoryInfoProxy Create(string path) => new DirectoryInfoProxy(path);
    }

    class DirectoryInfoProxy : IDirectoryInfoProxy
    {
        public string FullName => m_DirectoryInfo.FullName;
        public bool Exists => m_DirectoryInfo.Exists;

        readonly DirectoryInfo m_DirectoryInfo;

        public DirectoryInfoProxy(string path)
        {
            m_DirectoryInfo = new DirectoryInfo(path);
        }

        public void Create() => m_DirectoryInfo.Create();

        public IEnumerable<FileInfo> EnumerateFiles(string searchPattern, SearchOption searchOption) =>
            m_DirectoryInfo.EnumerateFiles(searchPattern, searchOption);

        public IEnumerable<DirectoryInfo> EnumerateDirectories() => m_DirectoryInfo.EnumerateDirectories();

        public void Delete(bool recursive) => m_DirectoryInfo.Delete(recursive);
    }
}
