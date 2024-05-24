using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface IFileInfoWrapper : IService
    {
        long GetFileLength(string path);
        IEnumerable<FileInfo> GetOldestFilesFromDirectory(string directoryPath);
        void DeleteFile(FileInfo file);
        double GetFileLengthMb(string filePath);
        double GetFilesSizeMb(IEnumerable<FileInfo> files);
        double GetFileLengthMb(FileInfo file);
        double GetDirectorySizeBytes(string folderPath);
        string GetFullPath(string path);
    }

    class FileInfoWrapper : BaseService<IFileInfoWrapper>, IFileInfoWrapper
    {
        public long GetFileLength(string path)
        {
            if (!File.Exists(path))
            {
                return 0;
            }

            var fileInfo = new FileInfo(path);
            return fileInfo.Length;
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

        public IEnumerable<FileInfo> GetOldestFilesFromDirectory(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                return new List<FileInfo>();
            }

            return Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
                .Select(f => new FileInfo(f)).OrderByDescending(x => x.LastAccessTimeUtc)
                .Where(x => x.LastAccessTimeUtc <= DateTime.UtcNow.AddMinutes(1));
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

        public double GetFilesSizeMb(IEnumerable<FileInfo> files)
        {
            return ByteSizeConverter.ConvertBytesToMb(files.Sum(x => x.Length));
        }

        public double GetFileLengthMb(string filePath)
        {
            return ByteSizeConverter.ConvertBytesToMb(GetFileLength(filePath));
        }

        public double GetFileLengthMb(FileInfo file)
        {
            return ByteSizeConverter.ConvertBytesToMb(file.Length);
        }

        public string GetFullPath(string path)
        {
            return Path.GetFullPath(path);
        }

        bool IsFileLocked(FileInfo file)
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
    }
}
