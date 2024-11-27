using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    interface IIOProxy : IService
    {
        // Directory
        public bool DirectoryExists(string directoryPath);
        public void DirectoryDelete(string path, bool recursive);
        public void CreateDirectory(string directoryPath);
        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);
        public bool DeleteAllFilesAndFoldersFromDirectory(string path);

        // Directory Info
        public string GetDirectoryInfoFullName(string path);
        public double GetDirectorySizeBytes(string folderPath);
        public string GetUniqueTempPathInProject();

        // File
        public bool FileExists(string filePath);
        public void DeleteFile(FileInfo file);
        public void DeleteFile(string filePath, bool recursivelyRemoveEmptyParentFolders = false);
        public FileStream Create(string path, int bufferSize, FileOptions options);
        public void FileMove(string sourceFilePath, string destinationFilePath);
        public string FileReadAllText(string filePath);
        public void FileWriteAllText(string filePath, string text);

        // File Info
        public long GetFileLength(string path);
        public double GetFileLengthMb(string filePath);
        public double GetFileLengthMb(FileInfo file);
        public double GetFilesSizeMb(IEnumerable<FileInfo> files);
        public IEnumerable<FileInfo> GetOldestFilesFromDirectory(string directoryPath);
    }

    class IOProxy : BaseService<IIOProxy>, IIOProxy
    {
        // Directory
        public bool DirectoryExists(string directoryPath) => Directory.Exists(directoryPath);

        public void DirectoryDelete(string path, bool recursive)
        {
            if (DirectoryExists(path))
            {
                Directory.Delete(path, recursive);
            }
        }

        public void CreateDirectory(string directoryPath) => Directory.CreateDirectory(directoryPath);

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) => Directory.EnumerateFiles(path, searchPattern, searchOption);

        public bool DeleteAllFilesAndFoldersFromDirectory(string path) => Utilities.DeleteAllFilesAndFoldersFromDirectory(path);

        // Directory Info
        public string GetDirectoryInfoFullName(string path)
        {
            if (!Directory.Exists(path))
            {
                return string.Empty;
            }

            var directoryInfo = new DirectoryInfo(path);
            return directoryInfo.FullName;
        }

        public double GetDirectorySizeBytes(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                return 0;
            }

            var directoryInfo = new DirectoryInfo(folderPath);
            return directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(fi => fi.Length);
        }

        static bool DirectoryEmpty(DirectoryInfo directoryInfo) => !directoryInfo.EnumerateFiles().Any() && !directoryInfo.EnumerateDirectories().Any();

        public string GetUniqueTempPathInProject() => FileUtil.GetUniqueTempPathInProject();

        // File
        public bool FileExists(string filePath)
        {
            return File.Exists(filePath);
        }

        static bool IsFileLocked(FileInfo file)
        {
            try
            {
                using var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
                stream.Close();
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }

            //file is not locked
            return false;
        }

        public void DeleteFile(FileInfo file)
        {
            if (!file.Exists || IsFileLocked(file))
            {
                return;
            }

            try
            {
                file.Delete();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                throw;
            }
        }

        public void DeleteFile(string filePath, bool recursivelyRemoveEmptyParentFolders = false)
        {
            if (!File.Exists(filePath))
                return;

            DeleteFile(new FileInfo(filePath));

            if (!recursivelyRemoveEmptyParentFolders)
                return;

            var parentFolder = Directory.GetParent(filePath);
            while (parentFolder?.Exists == true && DirectoryEmpty(parentFolder))
            {
                DirectoryDelete(parentFolder.FullName, false);
                parentFolder = parentFolder.Parent;
            }
        }

        public FileStream Create(string path, int bufferSize, FileOptions options) => File.Create(path, bufferSize, options);

        public void FileMove(string sourceFilePath, string destinationFilePath)
        {
            if (!FileExists(sourceFilePath))
                return;

            new FileInfo(destinationFilePath).Directory?.Create();
            File.Move(sourceFilePath, destinationFilePath);
        }

        public string FileReadAllText(string filePath) => File.ReadAllText(filePath);

        public void FileWriteAllText(string filePath, string text) => File.WriteAllText(filePath, text);

        // File Info
        public long GetFileLength(string path)
        {
            if (!File.Exists(path))
            {
                return 0;
            }

            var fileInfo = new FileInfo(path);
            return fileInfo.Length;
        }

        public double GetFileLengthMb(string filePath)
        {
            return ByteSizeConverter.ConvertBytesToMb(GetFileLength(filePath));
        }

        public double GetFileLengthMb(FileInfo file)
        {
            return ByteSizeConverter.ConvertBytesToMb(file.Length);
        }

        public double GetFilesSizeMb(IEnumerable<FileInfo> files)
        {
            return ByteSizeConverter.ConvertBytesToMb(files.Sum(x => x.Length));
        }

        public IEnumerable<FileInfo> GetOldestFilesFromDirectory(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                return new List<FileInfo>();
            }

            return Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
                .Select(f => new FileInfo(f))
                .Where(x => x.LastAccessTimeUtc <= DateTime.UtcNow.AddMinutes(1))
                .OrderByDescending(x => x.LastAccessTimeUtc);
        }

    }
}
