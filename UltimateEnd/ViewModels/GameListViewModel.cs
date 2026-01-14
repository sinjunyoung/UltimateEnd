using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Coordinators;
using UltimateEnd.Enums;
using UltimateEnd.Managers;
using UltimateEnd.Models;
using UltimateEnd.Orchestrators;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.ViewModels
{   
    public class GameListViewModel : ViewModelBase, IDisposable
    {
        #region Separated Components

        private readonly VideoPlaybackCoordinator _videoCoordinator;
        private readonly GameLaunchOrchestrator _launchOrchestrator;
        private readonly GameCollectionManager _collectionManager;
        private readonly MediaAssetManager _mediaAssetManager;
        private readonly EmulatorConfigService _emulatorConfigService;
        private readonly MetadataPersistenceService _persistenceService;

        #endregion

        #region Fields

        private GameViewMode _viewMode = GameViewMode.List;
        private Platform _platform;
        private GameMetadata? _contextMenuTargetGame;
        private string _errorMessage = string.Empty;
        private bool _isEditingDescriptionOverlay;
        private bool _disposed;
        private bool _isErrorVisible;

        private readonly Subject<GameMetadata> _gameSelectionSubject = new();
        private IDisposable? _selectionSubscription;
        private bool _isLaunchingGame = false;
        private bool _isDownloadingMedia = false;

        private string? _currentSubFolder = null;
        private ObservableCollection<FolderItem> _displayItems = [];
        private FolderItem? _selectedItem;       

        #endregion

        #region Properties

        public GameViewMode ViewMode
        {
            get => _viewMode;
            set
            {
                this.RaiseAndSetIfChanged(ref _viewMode, value);

                if (value == GameViewMode.Grid)
                {
                    DisableVideoPlayback();
                    IsVideoContainerVisible = false;
                }
                else if (value == GameViewMode.List)
                {
                    IsVideoContainerVisible = true;
                    EnableVideoPlayback();
                }
            }
        }

        public Platform Platform
        {
            get => _platform;
            set => this.RaiseAndSetIfChanged(ref _platform, value);
        }

        public GameMetadata? SelectedGame
        {
            get => _collectionManager.SelectedGame;
            set
            {
                _collectionManager.SelectedGame = value;

                if (value != null) _gameSelectionSubject.OnNext(value);
            }
        }

        public GameMetadata? ContextMenuTargetGame
        {
            get => _contextMenuTargetGame;
            set => this.RaiseAndSetIfChanged(ref _contextMenuTargetGame, value);
        }

        public object? MediaPlayer => _videoCoordinator.MediaPlayer;

        public string SearchText
        {
            get => _collectionManager.SearchText;
            set => _collectionManager.SearchText = value;
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
        }

        public bool IsEditingDescriptionOverlay
        {
            get => _isEditingDescriptionOverlay;
            set => this.RaiseAndSetIfChanged(ref _isEditingDescriptionOverlay, value);
        }

        public bool IsErrorVisible
        {
            get => _isErrorVisible;
            set => this.RaiseAndSetIfChanged(ref _isErrorVisible, value);
        }

        public bool IsVideoContainerVisible
        {
            get => _videoCoordinator.IsVideoContainerVisible;
            set
            {
                _videoCoordinator.IsVideoContainerVisible = value;
                this.RaisePropertyChanged();
            }
        }

        public ObservableCollection<GameMetadata> Games => _collectionManager.Games;
        public ObservableCollection<string> Genres => _collectionManager.Genres;
        public ObservableCollection<GameGenreItem> EditingGenres => _collectionManager.EditingGenres;

        public string SelectedGenre
        {
            get => _collectionManager.SelectedGenre;
            set => _collectionManager.SelectedGenre = value;
        }

        public bool IsLaunchingGame
        {
            get => _isLaunchingGame;
            set => this.RaiseAndSetIfChanged(ref _isLaunchingGame, value);
        }

        public bool IsDownloadingMedia
        {
            get => _isDownloadingMedia;
            set => this.RaiseAndSetIfChanged(ref _isDownloadingMedia, value);
        }

        public bool IsShowingDeletedGames { get => _collectionManager.IsShowingDeletedGames; }

        public bool IsNativeAppPlatform => Platform?.Id == "desktop" || Platform?.Id == "android";

        public string? CurrentSubFolder
        {
            get => _currentSubFolder;
            private set => this.RaiseAndSetIfChanged(ref _currentSubFolder, value);
        }

        public bool IsInSubFolder => _currentSubFolder != null;

        public string CurrentPath => _currentSubFolder != null ? $"{Platform.Name} > {_currentSubFolder}" : Platform.Name;

        public ObservableCollection<FolderItem> DisplayItems
        {
            get => _displayItems;
            private set => this.RaiseAndSetIfChanged(ref _displayItems, value);
        }

        public FolderItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem != null)
                    _selectedItem.IsSelected = false;

                this.RaiseAndSetIfChanged(ref _selectedItem, value);

                if (value != null)
                {
                    value.IsSelected = true;

                    if (value.IsGame)
                        SelectedGame = value.Game;
                    else if (value.IsFolder)
                         _videoCoordinator.ReleaseMedia();
                }
            }
        }

        #endregion

        #region Reactive Commands

        public ReactiveCommand<GameMetadata, Unit> RenameGameCommand { get; set; }
        public ReactiveCommand<GameMetadata, Unit> FinishRenameCommand { get; set; }

        public ReactiveCommand<GameMetadata, Unit> PlayInitialVideoCommand
        {
            get => _videoCoordinator.PlayInitialVideoCommand;
            set => _videoCoordinator.PlayInitialVideoCommand = value;
        }

        public ReactiveCommand<GameMetadata, Unit> SetLogoImageCommand
        {
            get => _mediaAssetManager.SetLogoImageCommand;
            set => _mediaAssetManager.SetLogoImageCommand = value;
        }

        public ReactiveCommand<GameMetadata, Unit> SetCoverImageCommand
        {
            get => _mediaAssetManager.SetCoverImageCommand;
            set => _mediaAssetManager.SetCoverImageCommand = value;
        }

        public ReactiveCommand<GameMetadata, Unit> SetGameVideoCommand
        {
            get => _mediaAssetManager.SetGameVideoCommand;
            set => _mediaAssetManager.SetGameVideoCommand = value;
        }

        public ReactiveCommand<Unit, Unit> CloseErrorCommand { get; set; }

        #endregion

        #region Events

        public event Action? BackRequested;
        public event EventHandler<GameMetadata>? FavoritesChanged;
        public event Action? PreviousPlatformRequested;
        public event Action? NextPlatformRequested;
        public event EventHandler<string>? PlatformImageChangeRequested;
        public event EventHandler<GameMetadata>? RequestExplicitScroll;
        public event EventHandler<bool>? RequestIdleDetectionChange;

        #endregion

        #region Constructor

        public GameListViewModel(Platform platform)
        {
            _platform = platform;

            _videoCoordinator = new VideoPlaybackCoordinator();
            _launchOrchestrator = new GameLaunchOrchestrator(_videoCoordinator);
            _collectionManager = new GameCollectionManager();

            _collectionManager.SelectedGameChanged += game =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    this.RaisePropertyChanged(nameof(SelectedGame));

                    if (game != null) _gameSelectionSubject.OnNext(game);
                });
            };

            _mediaAssetManager = new MediaAssetManager(_videoCoordinator);
            _emulatorConfigService = new EmulatorConfigService();
            _persistenceService = new MetadataPersistenceService();

            SetupEventHandlers();

            InitializeCommands();

            LoadGames(platform.Id);

            if (Games.Count > 0)
            {
                _collectionManager.LoadGenres();
                SetupPropertySubscriptions();
                BuildDisplayItems();

                if (SelectedGame != null) _gameSelectionSubject.OnNext(SelectedGame);
            }
        }

        private void SetupEventHandlers()
        {
            _videoCoordinator.VideoContainerVisibilityChanged += visible => Dispatcher.UIThread.Post(() => this.RaisePropertyChanged(nameof(IsVideoContainerVisible)));

            _launchOrchestrator.AppActivated += async () =>
            {
                if (ViewMode == GameViewMode.List) await _videoCoordinator.ResumeAsync(SelectedGame);
            };
            _launchOrchestrator.VideoContainerVisibilityRequested += visible => Dispatcher.UIThread.Post(() => IsVideoContainerVisible = visible);
            _launchOrchestrator.LaunchCompleted += () => Dispatcher.UIThread.Post(async () =>
            {
                await Task.Delay(400);
                IsLaunchingGame = false;
            });
            _launchOrchestrator.LaunchFailed += () => Dispatcher.UIThread.Post(() =>
            {
                IsLaunchingGame = false;
                TryResumeVideo();
            });                

            _launchOrchestrator.IdleDetectionEnabled += (isEnabled) => RequestIdleDetectionChange?.Invoke(this, isEnabled);

            _collectionManager.SelectedGameChanged += game => Dispatcher.UIThread.Post(() => this.RaisePropertyChanged(nameof(SelectedGame)));
            _collectionManager.SelectedGenreChanged += genre =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    this.RaisePropertyChanged(nameof(SelectedGenre));
                    BuildDisplayItems();
                });
            };
            _collectionManager.SearchTextChanged += text => Dispatcher.UIThread.Post(() =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    this.RaisePropertyChanged(nameof(SearchText));
                    BuildDisplayItems();
                });
            });
            _collectionManager.ShowingDeletedGamesChanged += value =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    BuildDisplayItems();
                    this.RaisePropertyChanged(nameof(IsShowingDeletedGames));
                });
            };
            _collectionManager.GamePropertyChanged += game => _persistenceService.MarkGameAsChanged(game);

            _mediaAssetManager.LogoImageChanged += OnMediaChanged;
            _mediaAssetManager.CoverImageChanged += OnMediaChanged;
            _mediaAssetManager.VideoChanged += OnMediaChanged;

            _mediaAssetManager.PlatformImageChanged += path =>
            {
                PlatformImageChangeRequested?.Invoke(this, path);
                TryResumeVideo();
            };

            _persistenceService.SaveRequested += () => SaveMetadata();
        }

        private void OnMediaChanged(GameMetadata game, string path) => TryResumeVideo();

        private void TryResumeVideo()
        {
            if (ViewMode == GameViewMode.List) _ = ResumeVideoAsync();
        }

        private void InitializeCommands()
        {
            RenameGameCommand = ReactiveCommand.Create<GameMetadata>(RenameGame);
            FinishRenameCommand = ReactiveCommand.Create<GameMetadata>(FinishRename);
            CloseErrorCommand = ReactiveCommand.Create(() => Dispatcher.UIThread.Post(() => IsErrorVisible = false));
        }

        private void SetupPropertySubscriptions()
        {
            _selectionSubscription?.Dispose();
            _selectionSubscription = null;
            
            if (ViewMode == GameViewMode.Grid) return;

            _selectionSubscription = _gameSelectionSubject
            .Throttle(TimeSpan.FromMilliseconds(300))
            .Where(_ => !IsLaunchingGame && !IsDownloadingMedia)
            .Where(_ => ViewMode == GameViewMode.List)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(game =>
            {
                if (ViewMode == GameViewMode.List && !IsLaunchingGame) _videoCoordinator.HandleSelectedGameChanged(game, 5);
            });
        }

        #endregion

        #region Game Loading

        public void LoadGames(string platformId) => _collectionManager.LoadGames(platformId);

        #endregion

        #region Navigation

        public void GoBack()
        {
            if (_currentSubFolder != null)
            {
                _videoCoordinator.ReleaseMedia();

                CurrentSubFolder = null;
                BuildDisplayItems();
            }
            else
            {
                _videoCoordinator.Stop();
                _selectionSubscription?.Dispose();

                if (_persistenceService.HasUnsavedChanges)
                    _ = Task.Run(() => _persistenceService.SaveNow());

                BackRequested?.Invoke();
            }
        }

        public void GoToPreviousPlatform()
        {
            _videoCoordinator.Stop();
            PreviousPlatformRequested?.Invoke();
        }

        public void GoToNextPlatform()
        {
            _videoCoordinator.Stop();
            NextPlatformRequested?.Invoke();
        }

        #endregion

        #region Game Commands

        public async Task LaunchGameAsync(GameMetadata game)
        {
            IsLaunchingGame = true;
            _videoCoordinator.Stop();

            await _launchOrchestrator.LaunchAsync(game);

            game.RefreshPlayHistory();
        }

        public static void RenameGame(GameMetadata game)
        {
            game.TempTitle = game.GetTitle();
            game.IsEditing = true;
        }

        public static void FinishRename(GameMetadata game)
        {
            game.Title = !string.IsNullOrEmpty(game.TempTitle) ? game.TempTitle.Trim() : null;
            game.IsEditing = false;
        }

        public async Task SetPlatformImage() => await _mediaAssetManager.SetPlatformImageAsync();

        private async Task LoadExternalMetadata(string initialDirectory, string fileDescription, string[] filePatterns, Func<string, string, CancellationToken, HashSet<GameMetadata>> updateFunc)
        {
            var cts = new CancellationTokenSource();

            try
            {
                _videoCoordinator.Stop();

                var fileType = new FilePickerFileType(fileDescription)
                {
                    Patterns = filePatterns
                };

                var path = await DialogHelper.OpenFileAsync(initialDirectory, fileType);

                if (!string.IsNullOrEmpty(path))
                {
                    var converter = PathConverterFactory.Create?.Invoke();
                    var realPath = converter?.FriendlyPathToRealPath(path);

                    await DialogService.Instance.ShowLoading("메타데이터를 불러오는 중...", cts);

                    HashSet<GameMetadata> changedGames;

                    try
                    {
                        changedGames = await Task.Run(() => updateFunc(Platform.Id, realPath, cts.Token), cts.Token);

                        foreach (var game in changedGames) _persistenceService.MarkGameAsChanged(game);
                    }
                    catch (OperationCanceledException)
                    {
                        await DialogService.Instance.HideLoading();
                        await Task.Delay(100);
                        await DialogService.Instance.ShowInfo("취소되었습니다.");

                        return;
                    }
                    finally
                    {
                        await DialogService.Instance.HideLoading();
                        await Task.Delay(100);
                    }

                    if (changedGames.Count > 0)
                        await DialogService.Instance.ShowSuccess($"총 {changedGames.Count}개의 게임 메타데이터가 업데이트되었습니다.");
                    else
                        await DialogService.Instance.ShowInfo("업데이트할 게임이 없습니다.");
                }
            }
            catch (Exception ex)
            {
                await DialogService.Instance.HideLoading();
                await Task.Delay(100);
                await DialogService.Instance.ShowError($"메타데이터를 불러오는 중 오류가 발생했습니다:\n{ex.Message}");
            }
            finally
            {
                cts.Dispose();

                if (ViewMode == GameViewMode.List) await _videoCoordinator.ResumeAsync(SelectedGame);
            }
        }

        public async Task LoadPegasusMetadata(string initialDirectory) => await LoadExternalMetadata(initialDirectory, "Pegasus Metadata Files", ["metadata.pegasus.txt"], _collectionManager.UpdateFromPegasusMetadata);

        public async Task LoadEsDeMetadata(string initialDirectory) => await LoadExternalMetadata(initialDirectory, "ES-DE Metadata Files", ["gamelist.xml"], _collectionManager.UpdateFromEsDeMetadata);

        public void SetLogoImage(GameMetadata game) => _mediaAssetManager.SetLogoImageCommand.Execute(game).Subscribe();

        public void SetCoverImage(GameMetadata game) => _mediaAssetManager.SetCoverImageCommand.Execute(game).Subscribe();

        public void SetGameVideo(GameMetadata game) => _mediaAssetManager.SetGameVideoCommand.Execute(game).Subscribe();

        #endregion

        #region Description Editing

        public static void FinishEditDescription(GameMetadata game)
        {
            game.Description = game.TempDescription?.Trim();
            game.IsEditingDescription = false;
        }

        public void SaveDescriptionChange() => _persistenceService.MarkAsChanged();

        #endregion

        #region Metadata Management

        private void SaveMetadata() => _persistenceService.SaveNow();

        public void RequestSave() => _persistenceService.MarkAsChanged();

        #endregion

        #region Favorites

        public void ToggleFavorite(GameMetadata game)
        {
            if (game == null) return;

            FavoritesManager.Toggle(game);
            FavoritesChanged?.Invoke(this, game);
        }

        public void OnFavoritesChanged(GameMetadata game) => FavoritesChanged?.Invoke(this, game);

        #endregion

        #region Emulator Management

        public List<EmulatorInfo> GetAvailableEmulators()
        {
            var selectedGamePlatformId = SelectedGame?.PlatformId;

            return EmulatorConfigService.GetAvailableEmulators(Platform.Id, selectedGamePlatformId);
        }

        public void SetEmulator(string emulatorId)
        {
            var selectedGamePlatformId = SelectedGame?.PlatformId;
            EmulatorConfigService.SetEmulator(Platform.Id, emulatorId, selectedGamePlatformId);
        }

        #endregion

        #region Video

        public void StopVideo() => _videoCoordinator.Stop();

        public async Task ResumeVideoAsync()
        {
            if (ViewMode == GameViewMode.List) await _videoCoordinator.ResumeAsync(SelectedGame);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            _selectionSubscription?.Dispose();
            SelectedGame = null;

            if (_persistenceService.HasUnsavedChanges)
                _ = Task.Run(() => _persistenceService.SaveNow());

            _videoCoordinator.Dispose();
            _collectionManager.Dispose();
            _persistenceService.Dispose();

            _ = Task.Run(() => _collectionManager.ClearCache());
        }

        #endregion

        public void IgnoreGameToggle()
        {
            _videoCoordinator.Stop();

            _collectionManager.IsShowingDeletedGames = !_collectionManager.IsShowingDeletedGames;

            Dispatcher.UIThread.Post(() =>
            {
                if (Games.Count > 0)
                {
                    SelectedGame = Games[0];
                    _gameSelectionSubject.OnNext(Games[0]);
                }
                else
                    SelectedGame = null;

                this.RaisePropertyChanged(nameof(IsShowingDeletedGames));                
            });
        }

        public void RefreshCurrentGamePlayHistory() => SelectedGame?.RefreshPlayHistory();

        public void ScrollToGame(GameMetadata game) => RequestExplicitScroll?.Invoke(this, game);

        public void EnableVideoPlayback()
        {
            if (ViewMode == GameViewMode.Grid) return;

            DisableVideoPlayback();

            if (ViewMode == GameViewMode.List)
            {
                SetupPropertySubscriptions();

                if (SelectedGame?.HasVideo == true)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (ViewMode == GameViewMode.List && !IsLaunchingGame) _videoCoordinator.HandleSelectedGameChanged(SelectedGame, 5);
                    }, DispatcherPriority.Background);
                }
            }
        }

        public void DisableVideoPlayback()
        {
            _selectionSubscription?.Dispose();
            _selectionSubscription = null;
            _videoCoordinator.Stop();
        }

        public async Task AddNativeAppAsync()
        {
            try
            {
                var focusSnapshot = FocusHelper.CreateSnapshot();

                var appProvider = AppProviderFactory.Create?.Invoke();

                if (appProvider == null)
                {
                    focusSnapshot.Restore();
                    return;
                }

                var apps = await appProvider.BrowseAppsAsync();

                if (apps == null || apps.Count == 0)
                {
                    focusSnapshot.Restore();
                    return;
                }

                var systemAppsPath = AppSettings.SystemAppsPath;
                var converter = PathConverterFactory.Create?.Invoke();
                var realSystemAppsPath = converter?.FriendlyPathToRealPath(systemAppsPath) ?? systemAppsPath;
                var platformPath = Path.Combine(realSystemAppsPath, appProvider.PlatformId);

                Directory.CreateDirectory(platformPath);

                foreach (var app in apps)
                {
                    var game = new GameMetadata
                    {
                        PlatformId = appProvider.PlatformId,
                        RomFile = app.Identifier,
                        Title = app.DisplayName,
                        EmulatorId = app.ActivityName
                    };

                    if (app.Icon != null)
                    {
                        var safeFileName = string.Join("_", app.DisplayName.Split(Path.GetInvalidFileNameChars()));
                        var mediaPath = Path.Combine(platformPath, "media", safeFileName);

                        Directory.CreateDirectory(mediaPath);

                        var logoPath = Path.Combine(mediaPath, "logo.png");
                        app.Icon.Save(logoPath);
                        game.LogoImagePath = PathHelper.ToRelativePath(logoPath);
                    }

                    game.SetBasePath(platformPath);
                    AllGamesManager.Instance.AddGame(game);
                    Games.Add(game);
                    _persistenceService.MarkGameAsChanged(game);
                }

                AllGamesManager.Instance.SavePlatformGames(appProvider.PlatformId);

                focusSnapshot.Restore();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AddNativeAppAsync error: {ex.Message}");
            }
        }

        public void MarkGameAsChanged(GameMetadata game) => _persistenceService.MarkGameAsChanged(game);

        #region Folder Navigation        

        public void BuildDisplayItems()
        {
            var items = new ObservableCollection<FolderItem>();

            if (_currentSubFolder == null)
            {
                if (Platform.Id == GameMetadataManager.HistoriesKey)
                {
                    var addedFolders = new HashSet<string>();

                    foreach (var game in Games)
                    {
                        if (!string.IsNullOrEmpty(game.SubFolder) && !addedFolders.Contains(game.SubFolder))
                        {
                            var folderGames = Games.Where(g => g.SubFolder == game.SubFolder).ToList();
                            items.Add(FolderItem.CreateFolder(game.SubFolder, folderGames.Count));
                            addedFolders.Add(game.SubFolder);
                        }
                        else if (string.IsNullOrEmpty(game.SubFolder))
                        {
                            items.Add(FolderItem.CreateGame(game));
                        }
                    }
                }
                else
                {
                    var folders = Games
                        .Where(g => !string.IsNullOrEmpty(g.SubFolder))
                        .GroupBy(g => g.SubFolder)
                        .OrderBy(g => g.Key);

                    foreach (var folder in folders)
                    {
                        items.Add(FolderItem.CreateFolder(folder.Key!, folder.Count()));
                    }

                    foreach (var game in Games.Where(g => string.IsNullOrEmpty(g.SubFolder)))
                    {
                        var item = FolderItem.CreateGame(game);
                        if (game == SelectedGame)
                            item.IsSelected = true;
                        items.Add(item);
                    }
                }
            }
            else
            {
                foreach (var game in Games.Where(g => g.SubFolder == _currentSubFolder))
                {
                    var item = FolderItem.CreateGame(game);
                    if (game == SelectedGame)
                        item.IsSelected = true;
                    items.Add(item);
                }
            }

            DisplayItems = items;
            SelectedItem = DisplayItems.FirstOrDefault(i => i.IsSelected);
            if (SelectedItem == null && DisplayItems.Count > 0)
                SelectedItem = DisplayItems[0];
        }

        public void EnterFolder(string subFolder)
        {
            CurrentSubFolder = subFolder;
            BuildDisplayItems();
        }

        public void OnItemTapped(FolderItem item)
        {
            if (item.IsFolder)
                EnterFolder(item.SubFolder!);
            else if (item.IsGame)
                SelectedItem = item;
        }

        #endregion
    }
}