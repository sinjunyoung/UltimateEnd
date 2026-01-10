using System;

namespace UltimateEnd.SaveFile
{
    public static class SaveBackupServiceFactoryProvider
    {
        private static Func<GoogleDriveService, ISaveBackupServiceFactory>? _factoryCreator;

        public static void Register(Func<GoogleDriveService, ISaveBackupServiceFactory> factoryCreator)
        {
            _factoryCreator = factoryCreator;
        }

        public static ISaveBackupServiceFactory Create(GoogleDriveService driveService)
        {
            if (_factoryCreator == null)
                throw new InvalidOperationException(
                    "SaveBackupServiceFactory가 등록되지 않았습니다."
                );

            return _factoryCreator(driveService);
        }
    }
}