using Avalonia.Media.Imaging;
using ReactiveUI;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json.Serialization;
using UltimateEnd.Managers;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.Models
{
    public class GameMetadata : ReactiveObject, IDisposable, ICloneable
    {
        #region Private Fields

        private readonly GameMetadataCache _cache = new();
        private string _basePath = string.Empty;

        private string? _title;
        private string? _description;
        private string? _developer;
        private string? _genre = string.Empty;
        private string? _coverImagePath;
        private string? _logoImagePath;
        private string? _videoPath;
        private string? _emulatorId;

        private bool _isFavorite;
        private bool _ignore;
        private bool _hasKorean;

        private string? _tempTitle;
        private string? _tempDescription;
        private bool _isEditing;
        private bool _isEditingDescription;
        private bool _isSelected;
        private string? _scrapHint;

        #endregion

        #region Public Properties - Basic Info

        [NotNull]
        public string RomFile { get; set; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Title
        {
            get => _title;
            set
            {
                this.RaiseAndSetIfChanged(ref _title, value);
                this.RaisePropertyChanged(nameof(DisplayTitle));

                _cache.SearchableText = null;
            }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;

                    this.RaisePropertyChanged(nameof(Description));
                    this.RaisePropertyChanged(nameof(DisplayDescription));
                    this.RaisePropertyChanged(nameof(HasDescription));
                }
            }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Developer
        {
            get => _developer;
            set
            {
                this.RaiseAndSetIfChanged(ref _developer, value);

                _cache.SearchableText = null;
            }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Genre
        {
            get => _genre;
            set
            {
                this.RaiseAndSetIfChanged(ref _genre, value);

                _cache.SearchableText = null;
            }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool HasKorean
        {
            get => _hasKorean;
            set => this.RaiseAndSetIfChanged(ref _hasKorean, value);
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsFavorite
        {
            get => _isFavorite;
            set => this.RaiseAndSetIfChanged(ref _isFavorite, value);
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Ignore
        {
            get => _ignore;
            set => this.RaiseAndSetIfChanged(ref _ignore, value);
        }

        [JsonIgnore]
        public string? PlatformId { get; set; }

        [JsonIgnore] 
        public string ShortPlatformId => PlatformInfoService.Instance.GetShortestAlias(PlatformId ?? string.Empty).ToUpper();

        [JsonPropertyName("emulatorId")]
        public string? EmulatorId
        {
            get => _emulatorId;
            set => this.RaiseAndSetIfChanged(ref _emulatorId, value);
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? SubFolder { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? ScrapHint
        {
            get => _scrapHint;
            set => this.RaiseAndSetIfChanged(ref _scrapHint, value);
        }

        #endregion

        #region Public Properties - Media Paths

        public string? CoverImagePath
        {
            get => string.IsNullOrEmpty(_coverImagePath) ? _coverImagePath : PathHelper.ToRelativePath(_coverImagePath);
            set
            {
                var absolutePath = string.IsNullOrEmpty(value) ? value : PathHelper.ToAbsolutePath(value);

                _cache.CoverPath = null;

                this.RaiseAndSetIfChanged(ref _coverImagePath, absolutePath);
                this.RaisePropertyChanged(nameof(CoverImage));
                this.RaisePropertyChanged(nameof(HasCoverImage));
            }
        }

        public string? LogoImagePath
        {
            get => string.IsNullOrEmpty(_logoImagePath) ? _logoImagePath : PathHelper.ToRelativePath(_logoImagePath);
            set
            {
                var absolutePath = string.IsNullOrEmpty(value) ? value : PathHelper.ToAbsolutePath(value);

                _cache.LogoPath = null;

                this.RaiseAndSetIfChanged(ref _logoImagePath, absolutePath);
                this.RaisePropertyChanged(nameof(LogoImage));
                this.RaisePropertyChanged(nameof(HasLogoImage));
            }
        }

        public string? VideoPath
        {
            get => string.IsNullOrEmpty(_videoPath) ? _videoPath : PathHelper.ToRelativePath(_videoPath);
            set
            {
                var absolutePath = string.IsNullOrEmpty(value) ? value : PathHelper.ToAbsolutePath(value);

                _cache.VideoPath = null;
                _cache.HasVideo = null;
                this.RaiseAndSetIfChanged(ref _videoPath, absolutePath);
            }
        }

        #endregion

        #region Public Properties - Display

        [JsonIgnore]
        public string DisplayTitle => GetTitle();

        [JsonIgnore]
        public string DisplayDescription => string.IsNullOrWhiteSpace(Description) ? "(설명 없음 - 클릭하여 추가)" : Description;

        [JsonIgnore]
        public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

        [JsonIgnore]
        public string SearchableText
        {
            get
            {
                if (_cache.SearchableText == null)
                {
                    var title = GetTitle().Replace(" ", string.Empty).ToLower();
                    var developer = (Developer ?? string.Empty).Replace(" ", string.Empty).ToLower();
                    var romfile = (RomFile ?? string.Empty).Replace(" ", string.Empty).ToLower();
                    _cache.SearchableText = $"{title}|{developer}|{romfile}";
                }

                return _cache.SearchableText;
            }
        }

        #endregion

        #region Public Properties - Editing

        [JsonIgnore]
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (this.RaiseAndSetIfChanged(ref _isEditing, value) && value) RequestFocus?.Invoke();
            }
        }

        [JsonIgnore]
        public string? TempTitle
        {
            get => _tempTitle;
            set => this.RaiseAndSetIfChanged(ref _tempTitle, value);
        }

        [JsonIgnore]
        public bool IsEditingDescription
        {
            get => _isEditingDescription;
            set => this.RaiseAndSetIfChanged(ref _isEditingDescription, value);
        }

        [JsonIgnore]
        public string? TempDescription
        {
            get => _tempDescription;
            set => this.RaiseAndSetIfChanged(ref _tempDescription, value);
        }

        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set => this.RaiseAndSetIfChanged(ref _isSelected, value);
        }

        #endregion

        #region Public Properties - Media (Computed)

        [JsonIgnore]
        public bool HasVideo
        {
            get
            {
                _cache.HasVideo ??= File.Exists(GetVideoPath());

                return _cache.HasVideo.Value;
            }
        }

        [JsonIgnore]
        public Bitmap? CoverImage
        {
            get
            {
                var path = GetCoverPath();

                if (_cache.LastCoverPath == path && _cache.CoverBitmapRef?.TryGetTarget(out var cached) == true) return cached;

                var bitmap = GameImageLoader.LoadCoverImage(path);

                if (bitmap != null)
                {
                    _cache.CoverBitmapRef = new WeakReference<Bitmap>(bitmap);
                    _cache.LastCoverPath = path;
                }

                return bitmap;
            }
        }

        [JsonIgnore]
        public Bitmap? LogoImage
        {
            get
            {
                var path = GetLogoPath();

                if (_cache.LastLogoPath == path && _cache.LogoBitmapRef?.TryGetTarget(out var cached) == true)
                    return cached;

                var bitmap = GameImageLoader.LoadLogoImage(path);

                if (bitmap != null)
                {
                    _cache.LogoBitmapRef = new WeakReference<Bitmap>(bitmap);
                    _cache.LastLogoPath = path;
                }

                return bitmap;
            }
        }

        [JsonIgnore]
        public bool HasLogoImage => File.Exists(GetLogoPath());

        [JsonIgnore]
        public bool HasCoverImage => File.Exists(GetCoverPath());

        #endregion

        #region Public Properties - Play History

        [JsonIgnore]
        public bool HasPlayHistory
        {
            get
            {
                EnsurePlayHistoryCache();

                return _cache.PlayHistory != null && _cache.PlayHistory.LastPlayedTime.HasValue;
            }
        }

        [JsonIgnore]
        public string LastPlayedDate
        {
            get
            {
                EnsurePlayHistoryCache();

                return _cache.PlayHistory?.LastPlayedTime?.ToString("yy'/'MM'/'dd");
            }
        }

        [JsonIgnore]
        public string TotalPlayTimeText
        {
            get
            {
                EnsurePlayHistoryCache();

                if (_cache.PlayHistory == null) return string.Empty;

                var time = _cache.PlayHistory.TotalPlayTime;

                if (time.TotalHours >= 1)
                    return $"{(int)time.TotalHours}시간";
                else if (time.TotalMinutes >= 1)
                    return $"{(int)time.TotalMinutes}분";
                else
                    return "1분 미만";
            }
        }

        #endregion

        #region Events

        public event Action? RequestFocus;

        #endregion

        #region Public Methods

        public void SetBasePath(string basePath)
        {
            _basePath = basePath;
            _cache.InvalidateAll();

            this.RaisePropertyChanged(nameof(DisplayTitle));
            this.RaisePropertyChanged(nameof(CoverImage));
            this.RaisePropertyChanged(nameof(LogoImage));
            this.RaisePropertyChanged(nameof(HasVideo));
        }

        public string GetBasePath() => _basePath;

        public string GetTitle()
        {
            if (!string.IsNullOrEmpty(Title)) return Title;

            var fileName = Path.GetFileNameWithoutExtension(RomFile);

            return fileName;
        }

        public string GetRomFullPath()
        {
            if (_cache.RomFullPath != null) return _cache.RomFullPath;

            if (PlatformId == GameMetadataManager.SteamKey)
            {
                _cache.RomFullPath = RomFile;

                return _cache.RomFullPath;
            }

            _cache.RomFullPath = Path.IsPathRooted(RomFile) ? RomFile : Path.Combine(_basePath, RomFile);

            return _cache.RomFullPath;
        }

        public string GetLogoPath()
        {
            if (_cache.LogoPath != null) return _cache.LogoPath;

            if (!string.IsNullOrEmpty(_logoImagePath) && File.Exists(_logoImagePath))
            {
                _cache.LogoPath = _logoImagePath;

                return _cache.LogoPath;
            }

            _cache.LogoPath = GameMediaPathResolver.GetLogoPath(this, _basePath);

            return _cache.LogoPath ?? string.Empty;
        }

        public string GetCoverPath()
        {
            if (_cache.CoverPath != null) return _cache.CoverPath;

            if (!string.IsNullOrEmpty(_coverImagePath) && File.Exists(_coverImagePath))
            {
                _cache.CoverPath = _coverImagePath;

                return _cache.CoverPath;
            }

            _cache.CoverPath = GameMediaPathResolver.GetCoverPath(this, _basePath);

            return _cache.CoverPath ?? string.Empty;
        }

        public string GetVideoPath()
        {
            if (_cache.VideoPath != null) return _cache.VideoPath;

            if (!string.IsNullOrEmpty(_videoPath) && File.Exists(_videoPath))
            {
                _cache.VideoPath = _videoPath;

                return _cache.VideoPath;
            }

            _cache.VideoPath = GameMediaPathResolver.GetVideoPath(this, _basePath);

            return _cache.VideoPath;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing) _cache.InvalidateAll();
        }

        public void CopyTo(GameMetadata target)
        {
            target.SubFolder = this.SubFolder;
            target.CoverImagePath = this.CoverImagePath;
            target.LogoImagePath = this.LogoImagePath;
            target.VideoPath = this.VideoPath;
            target.Description = this.Description;
            target.Developer = this.Developer;
            target.EmulatorId = this.EmulatorId;
            target.Genre = this.Genre;
            target.HasKorean = this.HasKorean;
            target.Ignore = this.Ignore;
            target.IsFavorite = this.IsFavorite;
            target.Title = this.Title;
            target.ScrapHint = this.ScrapHint;
        }

        public bool MergeFrom(GameMetadata source)
        {
            bool hasChanges = false;

            if (!string.IsNullOrEmpty(source.SubFolder) && this.SubFolder != source.SubFolder)
            {
                this.SubFolder = source.SubFolder;
                hasChanges = true;
            }

            if (!string.IsNullOrEmpty(source.CoverImagePath) && this.CoverImagePath != source.CoverImagePath)
            {
                this.CoverImagePath = source.CoverImagePath;
                hasChanges = true;
            }

            if (!string.IsNullOrEmpty(source.LogoImagePath) && this.LogoImagePath != source.LogoImagePath)
            {
                this.LogoImagePath = source.LogoImagePath;
                hasChanges = true;
            }

            if (!string.IsNullOrEmpty(source.VideoPath) && this.VideoPath != source.VideoPath)
            {
                this.VideoPath = source.VideoPath;
                hasChanges = true;
            }

            if (!string.IsNullOrEmpty(source.Description) && this.Description != source.Description)
            {
                this.Description = source.Description;
                hasChanges = true;
            }

            if (!string.IsNullOrEmpty(source.Developer) && this.Developer != source.Developer)
            {
                this.Developer = source.Developer;
                hasChanges = true;
            }

            if (!string.IsNullOrEmpty(source.EmulatorId) && this.EmulatorId != source.EmulatorId)
            {
                this.EmulatorId = source.EmulatorId;
                hasChanges = true;
            }

            if (!string.IsNullOrEmpty(source.Genre) && this.Genre != source.Genre)
            {
                this.Genre = source.Genre;
                hasChanges = true;
            }

            if (this.HasKorean != source.HasKorean)
            {
                this.HasKorean = source.HasKorean;
                hasChanges = true;
            }

            if (this.Ignore != source.Ignore)
            {
                this.Ignore = source.Ignore;
                hasChanges = true;
            }

            if (this.IsFavorite != source.IsFavorite)
            {
                this.IsFavorite = source.IsFavorite;
                hasChanges = true;
            }

            if (!string.IsNullOrEmpty(source.Title) && this.Title != source.Title)
            {
                this.Title = source.Title;
                hasChanges = true;
            }

            if (!string.IsNullOrEmpty(source.ScrapHint) && this.ScrapHint != source.ScrapHint)
            {
                this.ScrapHint = source.ScrapHint;
                hasChanges = true;
            }

            return hasChanges;
        }

        public GameMetadata Clone()
        {
            var clone = new GameMetadata
            {
                SubFolder = this.SubFolder,
                PlatformId = this.PlatformId,
                RomFile = this.RomFile,

                CoverImagePath = this.CoverImagePath,
                LogoImagePath = this.LogoImagePath,
                VideoPath = this.VideoPath,
                Description = this.Description,
                Developer = this.Developer,
                EmulatorId = this.EmulatorId,
                Genre = this.Genre,
                HasKorean = this.HasKorean,
                Ignore = this.Ignore,
                IsFavorite = this.IsFavorite,
                Title = this.Title,
                ScrapHint = this.ScrapHint,
            };

            clone.SetBasePath(this._basePath);

            return clone;
        }

        object ICloneable.Clone() => this.Clone();

        public void RefreshPlayHistory()
        {
            _cache.PlayHistoryValid = false;
            _cache.PlayHistory = null;

            this.RaisePropertyChanged(nameof(HasPlayHistory));
            this.RaisePropertyChanged(nameof(LastPlayedDate));
            this.RaisePropertyChanged(nameof(TotalPlayTimeText));
        }

        public void RefreshMediaCache()
        {
            _cache.InvalidateMedia();

            this.RaisePropertyChanged(nameof(CoverImage));
            this.RaisePropertyChanged(nameof(LogoImage));
            this.RaisePropertyChanged(nameof(HasVideo));
            this.RaisePropertyChanged(nameof(HasCoverImage));
            this.RaisePropertyChanged(nameof(HasLogoImage));
        }

        public static void ClearDirectoryCache() => GameMediaPathResolver.ClearDirectoryCache();
        public static void InvalidateDirectoryCache(string basePath) => GameMediaPathResolver.InvalidateDirectoryCache(basePath);

        #endregion

        #region Private Methods

        private void EnsurePlayHistoryCache()
        {
            if (!_cache.PlayHistoryValid)
            {
                var romFullPath = GetRomFullPath();
                var converter = PathConverterFactory.Create?.Invoke();
                var friendlyPath = converter?.RealPathToFriendlyPath(romFullPath) ?? romFullPath;
                _cache.PlayHistory = PlayTimeHistoryFactory.Instance.GetPlayHistorySync(friendlyPath);
                _cache.PlayHistoryValid = true;
            }
        }

        #endregion
    }
}