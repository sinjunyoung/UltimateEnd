using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UltimateEnd.Services;

namespace UltimateEnd.Extractor
{
    public class GameRepository : IDisposable
    {
        private static GameRepository _instance;
        private static readonly Lock _lock = new();
        private readonly SQLiteConnection _connection;

        private GameRepository()
        {
            var factory = AppBaseFolderProviderFactory.Create.Invoke();
            _connection = new(Path.Combine(factory.GetAssetsFolder(), "DBs", "games.db"));
        }

        public static GameRepository Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                        _instance ??= new GameRepository();
                }
                return _instance;
            }
        }

        public Game GetGame(int platformId, string gameCode)
        {
            return _connection.Table<Game>()
                .Where(x => x.PlatformId == platformId && x.GameCode == gameCode)
                .FirstOrDefault();
        }

        public Game GetGameById(string gameId)
        {
            return _connection.Table<Game>()
                .Where(x => x.GameId == gameId)
                .FirstOrDefault();
        }

        public List<Game> GetGamesByPlatform(int platformId)
        {
            return [.. _connection.Table<Game>().Where(x => x.PlatformId == platformId)];
        }

        public List<Game> GetGamesByCode(string gameCode)
        {
            return [.. _connection.Table<Game>().Where(x => x.GameCode == gameCode)];
        }

        public List<Game> SearchByName(string name)
        {
            return [.. _connection.Table<Game>().Where(x => x.Name.Contains(name) || x.NameEn.Contains(name))];
        }

        public List<Game> GetGamesByGenre(string genreId)
        {
            return [.. _connection.Table<Game>().Where(x => x.GenreId == genreId)];
        }

        public List<Game> GetGamesByRegion(string region)
        {
            return [.. _connection.Table<Game>().Where(x => x.Region == region)];
        }

        public List<Game> GetAllGames()
        {
            return [.. _connection.Table<Game>()];
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _connection?.Close();
                _connection?.Dispose();
                _instance = null;
            }
        }

        public static void Cleanup()
        {
            lock (_lock)
            {
                _instance?.Dispose();
                _instance = null;
            }
        }
    }
}