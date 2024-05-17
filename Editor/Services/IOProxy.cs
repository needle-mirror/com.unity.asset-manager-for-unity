using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface IIOProxy : IService
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
        string GetUniqueTempPathInProject();
        bool DeleteAllFilesAndFoldersFromDirectory(string path);
        FileStream Create(string path, int bufferSize, FileOptions options) => File.Create(path, bufferSize, options);
    }

    class IOProxy : BaseService<IIOProxy>, IIOProxy
    {
        public bool FileExists(string filePath)
        {
            return File.Exists(filePath);
        }

        public void FileMove(string sourceFilePath, string destinationFilePath)
        {
            if (!FileExists(sourceFilePath))
                return;

            new FileInfo(destinationFilePath).Directory?.Create();
            File.Move(sourceFilePath, destinationFilePath);
        }

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

        public void DirectoryDelete(string path, bool recursive)
        {
            if (DirectoryExists(path))
            {
                Directory.Delete(path, recursive);
            }
        }

        public void CreateDirectory(string directoryPath) => Directory.CreateDirectory(directoryPath);

        public string FileReadAllText(string filePath) => File.ReadAllText(filePath);

        public void FileWriteAllText(string filePath, string text) => File.WriteAllText(filePath, text);

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) => Directory.EnumerateFiles(path, searchPattern, searchOption);

        public string GetUniqueTempPathInProject() => FileUtil.GetUniqueTempPathInProject();

        public bool DeleteAllFilesAndFoldersFromDirectory(string path) => Utilities.DeleteAllFilesAndFoldersFromDirectory(path);
        public FileStream Create(string path, int bufferSize, FileOptions options) => File.Create(path, bufferSize, options);

        static bool DirectoryEmpty(DirectoryInfo directoryInfo) => !directoryInfo.EnumerateFiles().Any() && !directoryInfo.EnumerateDirectories().Any();
    }
}
