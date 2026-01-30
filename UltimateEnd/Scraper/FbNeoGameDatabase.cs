using System;
using System.IO;
using System.Collections.Generic;
using SQLite;
using UltimateEnd.Services;
using UltimateEnd.Scraper.Models;
using System.Threading;

namespace UltimateEnd.Scraper
{
    public static class FbNeoGameDatabase
    {
        private static IAssetPathProvider? _pathProvider;
        private static SQLiteConnection? _db;
        private static readonly Lock _lock = new();

        public static void Initialize(IAssetPathProvider pathProvider)
        {
            _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));

            lock (_lock)
            {
                if (_db != null) return;

                try
                {
                    var dbPath = _pathProvider.GetAssetPath("DBs", "metadata.pegasus.db");

                    if (!File.Exists(dbPath))
                        throw new FileNotFoundException($"DB 파일을 찾을 수 없습니다: {dbPath}");

                    _db = new SQLiteConnection(dbPath);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("RetroDB 초기화 실패", ex);
                }
            }
        }

        public static GameRecord? GetGameByRomFile(string romFile)
        {
            EnsureInitialized();

            try
            {
                return _db.Find<GameRecord>(romFile);
            }
            catch
            {
                return null;
            }
        }

        public static GameRecord? GetGameByPath(string romPath)
        {
            try
            {
                var fileName = Path.GetFileName(romPath);
                return GetGameByRomFile(fileName);
            }
            catch
            {
                return null;
            }
        }

        public static List<GameRecord> GetAllGames()
        {
            EnsureInitialized();

            try
            {
                return [.. _db.Table<GameRecord>()];
            }
            catch
            {
                return [];
            }
        }

        public static List<GameRecord> GetKoreanGames()
        {
            EnsureInitialized();

            try
            {
                return [.. _db.Table<GameRecord>().Where(g => g.HasKorean == 1)];
            }
            catch
            {
                return [];
            }
        }

        public static List<GameRecord> GetCloneGames()
        {
            EnsureInitialized();

            try
            {
                return [.. _db.Table<GameRecord>().Where(g => g.ParentRomFile != null)];
            }
            catch
            {
                return [];
            }
        }

        public static List<GameRecord> SearchByTitle(string keyword)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(keyword))
                return [];

            try
            {
                return [.. _db.Table<GameRecord>().Where(g => g.Title.Contains(keyword))];
            }
            catch
            {
                return [];
            }
        }

        public static void Reload()
        {
            lock (_lock)
            {
                _db?.Close();
                _db = null;

                if (_pathProvider != null)
                    Initialize(_pathProvider);
            }
        }

        private static void EnsureInitialized()
        {
            if (_db == null)
                throw new InvalidOperationException("RetroDB가 초기화되지 않았습니다. Initialize()를 먼저 호출하세요.");
        }
    }
}