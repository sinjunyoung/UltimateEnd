using System;

namespace UltimateEnd.SaveFile
{
    public static class SaveBackupServiceLocator
    {
        private static ISaveBackupServiceFactory? _factory;

        public static void Register(ISaveBackupServiceFactory factory)
        {
            _factory = factory;
        }

        public static ISaveBackupServiceFactory Instance
        {
            get
            {
                if (_factory == null)
                    throw new InvalidOperationException(
                        "SaveBackupServiceFactory가 등록되지 않았습니다."
                    );
                return _factory;
            }
        }
    }
}