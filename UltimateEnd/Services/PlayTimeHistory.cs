using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UltimateEnd.Enums;
using UltimateEnd.Models;

namespace UltimateEnd.Services
{
    public class PlayTimeHistory
    {
        private readonly SQLiteAsyncConnection _database;
        private readonly string _databasePath;
        private DateTime? _currentSessionStart;
        private string _currentGameId;

        public PlayTimeHistory(string dbPath)
        {
            _databasePath = dbPath;
            _database = new SQLiteAsyncConnection(dbPath);
            _database.CreateTableAsync<GamePlayHistory>().Wait();
        }

        public async Task Update(string fullPath, PlayState state)
        {
            var gameId = GetGameId(fullPath);
            var platformPath = Path.GetDirectoryName(fullPath);
            var gameFileName = Path.GetFileName(fullPath);

            if (state == PlayState.Start) await HandleStart(gameId, platformPath, gameFileName);
            else if (state == PlayState.Stop) await HandleStop(gameId);
        }

        public async Task<GamePlayHistory> GetPlayHistory(string fullPath)
        {
            var gameId = GetGameId(fullPath);

            return await _database.Table<GamePlayHistory>()
                .Where(g => g.Id == gameId)
                .FirstOrDefaultAsync();
        }

        public int GetHistoryCountSync(List<string> validPlatformPaths)
        {
            try
            {
                using var syncDb = new SQLiteConnection(_databasePath);

                syncDb.CreateTable<GamePlayHistory>();

                if (validPlatformPaths == null || validPlatformPaths.Count == 0) return syncDb.Table<GamePlayHistory>().Count();

                return syncDb.Table<GamePlayHistory>()
                    .Where(g => validPlatformPaths.Contains(g.Platform))
                    .Count();
            }
            catch
            {
                return 0;
            }
        }

        public async Task<TimeSpan> GetTotalPlayTime(string fullPath)
        {
            var history = await GetPlayHistory(fullPath);

            return history?.TotalPlayTime ?? TimeSpan.Zero;
        }

        public TimeSpan GetTotalPlayTimeSync(string fullPath)
        {
            try
            {
                using var syncDb = new SQLiteConnection(_databasePath);

                var gameId = GetGameId(fullPath);

                var history = syncDb.Table<GamePlayHistory>()
                    .Where(g => g.Id == gameId)
                    .FirstOrDefault();

                return history?.TotalPlayTime ?? TimeSpan.Zero;
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        public async Task<DateTime?> GetLastPlayedTime(string fullPath)
        {
            var history = await GetPlayHistory(fullPath);

            return history?.LastPlayedTime;
        }

        public DateTime? GetLastPlayedTimeSync(string fullPath)
        {
            try
            {
                using var syncDb = new SQLiteConnection(_databasePath);

                var gameId = GetGameId(fullPath);

                var history = syncDb.Table<GamePlayHistory>()
                    .Where(g => g.Id == gameId)
                    .FirstOrDefault();

                return history?.LastPlayedTime;
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> Delete(string fullPath)
        {
            var gameId = GetGameId(fullPath);

            var rowsDeleted = await _database.Table<GamePlayHistory>()
                .DeleteAsync(g => g.Id == gameId);

            return rowsDeleted > 0;
        }

        public bool DeleteSync(string fullPath)
        {
            try
            {
                using var syncDb = new SQLiteConnection(_databasePath);

                var gameId = GetGameId(fullPath);

                return syncDb.Table<GamePlayHistory>().Delete(g => g.Id == gameId) > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<GamePlayHistory>> GetAllHistory() => await _database.Table<GamePlayHistory>().ToListAsync();

        public List<GamePlayHistory> GetAllHistorySync(List<string> validPlatformPaths = null)
        {
            try
            {
                using var syncDb = new SQLiteConnection(_databasePath);

                syncDb.CreateTable<GamePlayHistory>();

                if (validPlatformPaths == null || validPlatformPaths.Count == 0) return [.. syncDb.Table<GamePlayHistory>()];

                return [.. syncDb.Table<GamePlayHistory>().Where(g => validPlatformPaths.Contains(g.Platform))];
            }
            catch
            {
                return [];
            }
        }

        public async Task<List<GamePlayHistory>> GetHistoryByPlatform(string platformPath)
        {
            return await _database.Table<GamePlayHistory>()
                .Where(g => g.Platform == platformPath)
                .ToListAsync();
        }

        public List<GamePlayHistory> GetHistoryByPlatformSync(string platformPath)
        {
            try
            {
                using var syncDb = new SQLiteConnection(_databasePath);

                syncDb.CreateTable<GamePlayHistory>();

                return [.. syncDb.Table<GamePlayHistory>().Where(g => g.Platform == platformPath)];
            }
            catch
            {
                return [];
            }
        }

        public async Task RecoverUnfinishedSessions()
        {
            var playingGames = await _database.Table<GamePlayHistory>()
                .Where(g => g.IsPlaying == true)
                .ToListAsync();

            foreach (var game in playingGames)
            {
                if (game.CurrentSessionStart.HasValue)
                {
                    var elapsed = DateTime.Now - game.CurrentSessionStart.Value;

                    if (elapsed.TotalHours < 2) game.TotalPlayTimeSeconds += (long)elapsed.TotalSeconds;
                }

                game.IsPlaying = false;
                game.CurrentSessionStart = null;
                game.LastPlayedTime = DateTime.Now;
                await _database.UpdateAsync(game);
            }
        }

        public async Task StopAllActiveSessions()
        {
            var playingGames = await _database.Table<GamePlayHistory>()
                .Where(g => g.IsPlaying == true)
                .ToListAsync();

            foreach (var game in playingGames) await HandleStop(game.Id);
        }

        private static string GetGameId(string fullPath) => fullPath.Replace("\\", "/").ToLowerInvariant();

        private async Task HandleStart(string gameId, string platformPath, string gameFileName)
        {
            if (_currentGameId != null && _currentSessionStart.HasValue) await HandleStop(_currentGameId);

            _currentGameId = gameId;
            _currentSessionStart = DateTime.Now;

            var existing = await _database.Table<GamePlayHistory>()
                .Where(g => g.Id == gameId)
                .FirstOrDefaultAsync();

            if (existing == null)
            {
                await _database.InsertAsync(new GamePlayHistory
                {
                    Id = gameId,
                    Platform = platformPath,
                    GameFileName = gameFileName,
                    LastPlayedTime = _currentSessionStart,
                    TotalPlayTimeSeconds = 0,
                    IsPlaying = true,
                    CurrentSessionStart = _currentSessionStart
                });
            }
            else
            {
                existing.IsPlaying = true;
                existing.CurrentSessionStart = _currentSessionStart;
                existing.LastPlayedTime = _currentSessionStart.Value;
                await _database.UpdateAsync(existing);
            }
        }

        private async Task HandleStop(string gameId)
        {
            if (string.IsNullOrEmpty(gameId)) return;

            var history = await _database.Table<GamePlayHistory>()
                .Where(g => g.Id == gameId)
                .FirstOrDefaultAsync();

            if (history == null) return;

            if (history.CurrentSessionStart.HasValue)
            {
                var sessionDuration = DateTime.Now - history.CurrentSessionStart.Value;

                if (sessionDuration.TotalHours < 2)
                    history.TotalPlayTimeSeconds += (long)sessionDuration.TotalSeconds;
            }

            history.IsPlaying = false;
            history.CurrentSessionStart = null;
            history.LastPlayedTime = DateTime.Now;

            await _database.UpdateAsync(history);

            if (_currentGameId == gameId)
            {
                _currentSessionStart = null;
                _currentGameId = null;
            }
        }

        public GamePlayHistory GetPlayHistorySync(string fullPath)
        {
            try
            {
                using var syncDb = new SQLiteConnection(_databasePath);

                var gameId = GetGameId(fullPath);

                return syncDb.Table<GamePlayHistory>().Where(g => g.Id == gameId).FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }
    }
}