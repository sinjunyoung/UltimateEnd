using System;
using UltimateEnd.Services;

namespace UltimateEnd.SaveFile
{
    public class PathConfigurationRequiredException(string message, ISaveBackupService service, string? currentPath = null) : Exception(message)
    {
        public string? CurrentPath { get; } = currentPath;

        public ISaveBackupService Service { get; } = service;
    }
}