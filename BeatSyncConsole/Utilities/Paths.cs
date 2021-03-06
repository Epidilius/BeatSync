﻿using BeatSyncConsole.Configs;
using BeatSyncConsole.Loggers;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace BeatSyncConsole.Utilities
{
    public static class Paths
    {
        private static bool OsDetected = false;
        private static OsType _operatingSystem;
        public static bool UseLocalTemp = false;
        public const string WorkingDirectoryFlag = "%WORKINGDIR%";
        public const string AssemblyDirectoryFlag = "%ASSEMBLYDIR%";
        public const string UserDirectoryFlag = "%USER%";
        public const string TempDirectoryFlag = "%TEMP%";

        public static OsType OperatingSystem
        {
            get
            {
                if (OsDetected)
                    return _operatingSystem;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    _operatingSystem = OsType.Windows;
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    _operatingSystem = OsType.Linux;
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    _operatingSystem = OsType.OSX;
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
                    _operatingSystem = OsType.FreeBSD;
                else
                {
                    Logger.log?.Warn($"Operating system not recognized: {RuntimeInformation.OSDescription}");
                    _operatingSystem = OsType.Unknown;
                }
                OsDetected = true;
                return _operatingSystem;
            }
        }

        private static string? _assemblyDirectory;
        public static string AssemblyDirectory
        {
            get
            {
                if (_assemblyDirectory == null)
                    _assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                return _assemblyDirectory;
            }
        }
        private static string? _userDirectory;
        public static string UserDirectory
        {
            get
            {
                if (_userDirectory == null)
                {
                    _userDirectory = OperatingSystem switch
                    {
                        OsType.Windows => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        OsType.Linux => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        OsType.OSX => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        OsType.FreeBSD => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        _ => ""
                    };
                }
                return _userDirectory;
            }
        }

        private static string? _tempDirectory;
        public static string TempDirectory
        {
            get
            {
                if (UseLocalTemp)
                    return GetFullPath("Temp", PathRoot.AssemblyDirectory);
                if (_tempDirectory == null)
                    _tempDirectory = Path.Combine(Path.GetTempPath(), "BeatSyncTemp");
                return _tempDirectory;
            }
        }

        private static string? _logDirectory;
        public static string LogDirectory
        {
            get
            {
                //if (UseLocalTemp)
                //    return GetFullPath("Temp", PathRoot.AssemblyDirectory);
                if (_logDirectory == null)
                    _logDirectory = Paths.GetFullPath("logs", PathRoot.AssemblyDirectory);
                return _logDirectory;
            }
        }
        public static string WorkingDirectory => Directory.GetCurrentDirectory();

        public const string Path_CustomLevels = @"Beat Saber_Data\CustomLevels";
        public const string Path_Playlists = @"Playlists";
        public const string Path_History = @"UserData\BeatSyncHistory.json";
        public static BeatSaberInstallLocation ToSongLocation(this BeatSaberInstall install)
        {
            return new BeatSaberInstallLocation(install.InstallPath);
        }

        public static string ReplaceWorkingDirectory(string fullPath) => GetRelativeDirectory(fullPath, PathRoot.WorkingDirectory);

        public static string GetRelativeDirectory(string path, PathRoot pathRoot = PathRoot.AssemblyDirectory)
        {
            return pathRoot switch
            {
                PathRoot.WorkingDirectory => path.Replace(WorkingDirectory, WorkingDirectoryFlag),
                PathRoot.AssemblyDirectory => path.Replace(AssemblyDirectory, AssemblyDirectoryFlag),
                PathRoot.UserDirectory => path.Replace(UserDirectory, UserDirectoryFlag),
                PathRoot.TempDirectory => path.Replace(TempDirectory, TempDirectoryFlag),
                _ => path
            };
        }


        public static string GetFullPath(string path, PathRoot relativeRoot = PathRoot.AssemblyDirectory)
        {
            string basePath = relativeRoot switch
            {
                PathRoot.WorkingDirectory => WorkingDirectory,
                PathRoot.AssemblyDirectory => AssemblyDirectory,
                PathRoot.UserDirectory => UserDirectory,
                PathRoot.TempDirectory => TempDirectory,
                _ => AssemblyDirectory
            };
            return GetFullPath(path, basePath);
        }

        public static string ExpandPath(string path)
        {
            if (path == null || path.Length == 0)
                return path;
            string expandedPath;
            if (path.StartsWith('~') && UserDirectory.Length > 0)
            {
                expandedPath = Path.Combine(UserDirectory, path.Substring(1).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '\\'));
            }
            else if (path.StartsWith(WorkingDirectoryFlag))
                expandedPath = Path.Combine(WorkingDirectory, path.Substring(WorkingDirectoryFlag.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '\\'));
            else if (path.StartsWith(AssemblyDirectoryFlag))
                expandedPath = Path.Combine(AssemblyDirectory, path.Substring(AssemblyDirectoryFlag.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '\\'));
            else if (path.StartsWith(UserDirectoryFlag))
                expandedPath = Path.Combine(UserDirectory, path.Substring(UserDirectoryFlag.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '\\'));
            else if (path.StartsWith(TempDirectoryFlag))
                expandedPath = Path.Combine(TempDirectory, path.Substring(TempDirectoryFlag.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '\\'));
            else
                return path;
            return expandedPath;
        }

        public static string GetFullPath(string path, string basePath)
        {
            string fullPathStr;
            if (basePath != null && path.StartsWith('~') && UserDirectory.Length > 0)
            {
                fullPathStr = Path.Combine(UserDirectory, path.Substring(1).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '\\'));
            }
            else if (Path.IsPathFullyQualified(path))
                return path;
            else if(string.IsNullOrEmpty(basePath))
                fullPathStr = Path.Combine(AssemblyDirectory, path.Substring(AssemblyDirectoryFlag.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '\\'));
            else if (path.StartsWith(WorkingDirectoryFlag))
                fullPathStr = Path.Combine(WorkingDirectory, path.Substring(WorkingDirectoryFlag.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '\\'));
            else if (path.StartsWith(AssemblyDirectoryFlag))
                fullPathStr = Path.Combine(AssemblyDirectory, path.Substring(AssemblyDirectoryFlag.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '\\'));
            else if (path.StartsWith(UserDirectoryFlag))
                fullPathStr = Path.Combine(UserDirectory, path.Substring(UserDirectoryFlag.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '\\'));
            else if (path.StartsWith(TempDirectoryFlag))
                fullPathStr = Path.Combine(TempDirectory, path.Substring(TempDirectoryFlag.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '\\'));
            else
                return Path.Combine(ExpandPath(basePath), path);

            return fullPathStr;
        }
    }

    public enum OsType
    {
        Unknown,
        Windows,
        Linux,
        OSX,
        FreeBSD
    }

    public enum PathRoot
    {
        WorkingDirectory = 0,
        AssemblyDirectory = 1,
        UserDirectory = 2,
        TempDirectory = 3
    }
}
