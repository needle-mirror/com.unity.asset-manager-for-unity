using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal interface IIOProxy : IService
    {
        bool FileExists(string filePath);
        void FileMove(string sourceFilePath, string destinationFilePath);
        void DeleteFileIfExists(string filePath, bool recursivelyRemoveEmptyParentFolders = false);
        long GetFileSizeInBytes(string filePath);
        bool DirectoryExists(string directoryPath);
        void DirectoryDelete(string path, bool recursive);
        void CreateDirectory(string directoryPath);
        string FileReadAllText(string filePath);
        void FileWriteAllText(string filePath, string text);
        IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);
        string GetRelativePathToProjectFolder(string path);
        string GetUniqueTempPathInProject();
        bool DeleteAllFilesAndFoldersFromDirectory(string path);
    }

    internal class IOProxy : BaseService<IIOProxy>, IIOProxy
    {
        public bool FileExists(string filePath) => File.Exists(filePath);

        public void FileMove(string sourceFilePath, string destinationFilePath)
        {
            new FileInfo(destinationFilePath).Directory?.Create();
            File.Move(sourceFilePath, destinationFilePath);
        }

        private static bool DirectoryEmpty(DirectoryInfo directoryInfo) => !directoryInfo.EnumerateFiles().Any() && !directoryInfo.EnumerateDirectories().Any();

        public void DeleteFileIfExists(string filePath, bool recursivelyRemoveEmptyParentFolders = false)
        {
            if (!File.Exists(filePath))
                return;
            File.Delete(filePath);

            if (!recursivelyRemoveEmptyParentFolders)
                return;

            var parentFolder = Directory.GetParent(filePath);
            while (parentFolder?.Exists == true && DirectoryEmpty(parentFolder))
            {
                DirectoryDelete(parentFolder.FullName, false);
                parentFolder = parentFolder.Parent;
            }
        }

        public long GetFileSizeInBytes(string filePath) => File.Exists(filePath) ? new FileInfo(filePath).Length : -1;

        public bool DirectoryExists(string directoryPath) => Directory.Exists(directoryPath);

        public void DirectoryDelete(string path, bool recursive) => Directory.Delete(path, recursive);

        public void CreateDirectory(string directoryPath) => Directory.CreateDirectory(directoryPath);

        public string FileReadAllText(string filePath) => File.ReadAllText(filePath);

        public void FileWriteAllText(string filePath, string text) => File.WriteAllText(filePath, text);

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) => Directory.EnumerateFiles(path, searchPattern, searchOption);

        private string m_FullAssetsFolderPath;
        private string fullAssetsFolderPath => m_FullAssetsFolderPath ??= Path.GetFullPath(Path.Combine(Application.dataPath, "../"));
        public string GetRelativePathToProjectFolder(string path)
        {
            // It was seen that when using this in conjunction with some Unity APIs (e.g. SceneManager.GetScenePath)
            // it was not working because of backslashes, so we normalize it to forward instead here
            return path.StartsWith(fullAssetsFolderPath) ? path.Substring(fullAssetsFolderPath.Length).Replace(Path.DirectorySeparatorChar, '/') : path;
        }

        public string GetUniqueTempPathInProject() => FileUtil.GetUniqueTempPathInProject();

        public bool DeleteAllFilesAndFoldersFromDirectory(string path) => Utilities.DeleteAllFilesAndFoldersFromDirectory(path);
    }
}