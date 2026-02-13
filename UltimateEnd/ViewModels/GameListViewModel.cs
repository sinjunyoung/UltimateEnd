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
    public class GameListViewModel : ViewModelBase, IAsyncDisposable
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
        private readonly Stack<int> _scrollPositionStack = new();

        private string _searchText = string.Empty;
        private bool _simpleGameListMode = false;

        private double _gridTitleFontSize = 16;

        private readonly Random _random = new();

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
                if (_collectionManager.SelectedGame == value) return;

                _collectionManager.SelectedGame = value;

                if (value != null)
                {
                    var matchingItem = DisplayItems.FirstOrDefault(i => i.IsGame && i.Game == value);
                    if (matchingItem != null && SelectedItem != matchingItem)
                    {
                        SelectedItem = matchingItem;
                    }
                    _gameSelectionSubject.OnNext(value);
                }

                this.RaisePropertyChanged();
            }
        }

        public GameMetadata? ContextMenuTargetGame
        {
            get => _contextMenuTargetGame;
            set => this.RaiseAndSetIfChanged(ref _contextMenuTargetGame, value);
        }

        public object? MediaPlayer => _videoCoordinator.MediaPlayer;

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

        public string SearchText
        {
            get => _searchText;
            set => this.RaiseAndSetIfChanged(ref _searchText, value);
        }


        public bool SimpleGameListMode
        {
            get => _simpleGameListMode;
            set
            {
                this.RaiseAndSetIfChanged(ref _simpleGameListMode, value);
                var settings = SettingsService.LoadSettings();
                settings.SimpleGameListMode = value;
                SettingsService.SaveSettingsQuiet(settings);
            }
        }

        public double GridTitleFontSize
        {
            get => _gridTitleFontSize;
            set => this.RaiseAndSetIfChanged(ref _gridTitleFontSize, value);
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

        public GameListViewModel(Platform platform, GameMetadata? initialGame = null)
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

            if (GameMetadataManager.IsSpecialPlatform(platform.Id))
            {
                LoadGames(platform.Id);
                FinalizeGameListInitialization(initialGame);
            }
            else
            {
                if (!AllGamesManager.Instance.IsPlatformLoaded(platform.Id))
                    _ = InitializeGamesAsync(platform.Id, initialGame);
                else
                {
                    LoadGames(platform.Id);
                    FinalizeGameListInitialization(initialGame);
                }
            }
        }

        private void FinalizeGameListInitialization(GameMetadata? initialGame)
        {
            if (Games.Count == 0) return;

            _collectionManager.LoadGenres();

            var settings = SettingsService.LoadSettings();
            _simpleGameListMode = settings.SimpleGameListMode;
            this.RaisePropertyChanged(nameof(SimpleGameListMode));

            SetupPropertySubscriptions();
            BuildDisplayItems();

            if (initialGame != null)
                SelectInitialGame(initialGame);
            else if (SelectedGame != null)
                _gameSelectionSubject.OnNext(SelectedGame);
        }

        private void SelectInitialGame(GameMetadata initialGame)
        {
            var targetGame = Games.FirstOrDefault(g => g.RomFile == initialGame.RomFile);
            
            if (targetGame != null)
            {
                _collectionManager.SelectedGame = targetGame;

                var targetItem = DisplayItems.FirstOrDefault(i => i.IsGame && i.Game == targetGame);

                if (targetItem != null) SelectedItem = targetItem;

                _gameSelectionSubject.OnNext(targetGame);
            }
        }

        private async Task InitializeGamesAsync(string platformId, GameMetadata? initialGame = null)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                _videoCoordinator.IsVideoContainerVisible = false;
                await DialogService.Instance.ShowLoading("게임 목록 로딩중...");
            });

            await Task.Run(() => AllGamesManager.Instance.EnsurePlatformLoaded(platformId));

            await Dispatcher.UIThread.InvokeAsync(async () => await DialogService.Instance.HideLoading());

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                LoadGames(platformId);
                FinalizeGameListInitialization(initialGame);

                if (ViewMode == GameViewMode.List) _videoCoordinator.IsVideoContainerVisible = true;
            });

            if (Games.Count > 0 && SelectedGame != null)
            {
                await Task.Delay(50);
                await Dispatcher.UIThread.InvokeAsync(() =>  RequestExplicitScroll?.Invoke(this, SelectedGame), DispatcherPriority.Background);
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
            _launchOrchestrator.LaunchCompleted += () => Dispatcher.UIThread.Post(() =>
            {
                Dispatcher.UIThread.Post(() => RequestExplicitScroll?.Invoke(this, SelectedGame), DispatcherPriority.Background);
                IsLaunchingGame = false;
            });
            _launchOrchestrator.LaunchFailed += () => Dispatcher.UIThread.Post(() =>
            {
                Dispatcher.UIThread.Post(() => RequestExplicitScroll?.Invoke(this, SelectedGame), DispatcherPriority.Background);
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
                BuildDisplayItems();
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
                if (ViewMode == GameViewMode.List && !IsLaunchingGame) _videoCoordinator.HandleSelectedGameChanged(game);
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

                if (_scrollPositionStack.Count > 0)
                {
                    int savedIndex = _scrollPositionStack.Pop();
                    if (savedIndex >= 0 && savedIndex < DisplayItems.Count)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            SelectedItem = DisplayItems[savedIndex];
                            if (SelectedItem?.IsGame == true && SelectedItem.Game != null)
                                RequestExplicitScroll?.Invoke(this, SelectedItem.Game);
                        }, DispatcherPriority.Background);
                    }
                }
            }
            else
            {
                _scrollPositionStack.Clear();
                _videoCoordinator.Stop();
                _selectionSubscription?.Dispose();

                if (_persistenceService.HasUnsavedChanges)
                {
                    try
                    {
                        var saveTask = Task.Run(() => _persistenceService.SaveNowAsync());
                        saveTask.Wait(TimeSpan.FromSeconds(3));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error saving on GoBack: {ex.Message}");
                    }
                }

                BackRequested?.Invoke();
            }
        }

        public void GoToPreviousPlatform()
        {
            _scrollPositionStack.Clear();
            _videoCoordinator.Stop();
            PreviousPlatformRequested?.Invoke();
        }

        public void GoToNextPlatform()
        {
            _scrollPositionStack.Clear();
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

        private async void SaveMetadata() => await _persistenceService.SaveNowAsync();

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

        public void ForceStopVideo()
        {
            _selectionSubscription?.Dispose();
            _selectionSubscription = null;

            _videoCoordinator.ForceStop();

            IsVideoContainerVisible = false;
        }

        public async Task ResumeVideoAsync()
        {
            if (ViewMode == GameViewMode.List) await _videoCoordinator.ResumeAsync(SelectedGame);
        }

        #endregion

        #region IDisposable

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _selectionSubscription?.Dispose();
                _selectionSubscription = null;

                if (_persistenceService.HasUnsavedChanges)
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    try
                    {
                        await _persistenceService.SaveNowAsync()
                            .WaitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.WriteLine("Save cancelled due to timeout");
                    }
                }

                using var clearCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try
                {
                    await Task.Run(() => _collectionManager.ClearCache(), clearCts.Token);
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("Cache clear cancelled due to timeout");
                }

                SelectedGame = null;

                _videoCoordinator.Dispose();
                _collectionManager.Dispose();
                _persistenceService.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in DisposeAsync: {ex.Message}");
            }
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
                        if (ViewMode == GameViewMode.List && !IsLaunchingGame) _videoCoordinator.HandleSelectedGameChanged(SelectedGame);
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
                var appProvider = AppProviderFactory.Create?.Invoke();

                if (appProvider == null) return;

                var app = await appProvider.BrowseAppsAsync();

                if (app == null)
                {
                    if (SelectedGame != null)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            RequestExplicitScroll?.Invoke(this, SelectedGame);
                        }, DispatcherPriority.Background);
                    }

                    return;
                }

                var systemAppsPath = AppSettings.SystemAppsPath;
                var converter = PathConverterFactory.Create?.Invoke();
                var realSystemAppsPath = converter?.FriendlyPathToRealPath(systemAppsPath) ?? systemAppsPath;

                foreach(var g in Games)
                {
                    if (g.PlatformId == appProvider.PlatformId && g.RomFile == app.Identifier)
                    {
                        this._videoCoordinator.IsVideoContainerVisible = false;
                        await DialogService.Instance.ShowInfo("이미 추가된 앱입니다.");
                        this._videoCoordinator.IsVideoContainerVisible = true;

                        if (SelectedGame != null)
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                RequestExplicitScroll?.Invoke(this, SelectedGame);
                            }, DispatcherPriority.Background);
                        }

                        return;
                    }
                }

                var platformPath = Path.Combine(realSystemAppsPath, appProvider.PlatformId);

                Directory.CreateDirectory(platformPath);

                var game = new GameMetadata
                {
                    PlatformId = appProvider.PlatformId,
                    RomFile = app.Identifier,
                    Title = app.DisplayName,
                    EmulatorId = app.ActivityName
                };

                game.SetBasePath(platformPath);

                if (app.Icon != null)
                {
                    var safeFileName = string.Join("_", app.DisplayName.Split(Path.GetInvalidFileNameChars()));
                    var mediaPath = Path.Combine(platformPath, "media", safeFileName);

                    Directory.CreateDirectory(mediaPath);

                    var logoPath = Path.Combine(mediaPath, "logo.png");
                    app.Icon.Save(logoPath);

                    var friendlyPath = converter?.RealPathToFriendlyPath(logoPath) ?? logoPath;
                    var relativePath = PathHelper.ToRelativePath(friendlyPath);

                    game.CoverImagePath = relativePath;
                    game.LogoImagePath = relativePath;
                }

                AllGamesManager.Instance.AddGame(game);

                await Dispatcher.UIThread.InvokeAsync(() => Games.Add(game));

                _persistenceService.MarkGameAsChanged(game);

                AllGamesManager.Instance.SavePlatformGames(appProvider.PlatformId);
                
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _collectionManager.LoadGenres();
                    
                    BuildDisplayItems();

                    if (Games.Count > 0)
                    {
                        var lastGame = Games[^1];
                        SelectedGame = lastGame;

                        var newItem = DisplayItems.FirstOrDefault(i => i.IsGame && i.Game == lastGame);

                        if (newItem != null) SelectedItem = newItem;

                        RequestExplicitScroll?.Invoke(this, lastGame);
                    }
                });
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
            var newItems = new List<FolderItem>();

            if (_currentSubFolder == null)
            {
                var folderGroups = Games
                    .Where(g => !string.IsNullOrEmpty(g.SubFolder))
                    .GroupBy(g => g.SubFolder)
                    .OrderBy(g => g.Key)
                    .ToList();

                foreach (var folder in folderGroups)
                    newItems.Add(FolderItem.CreateFolder(folder.Key!, folder.Count()));

                foreach (var game in Games.Where(g => string.IsNullOrEmpty(g.SubFolder)))
                    newItems.Add(FolderItem.CreateGame(game));
            }
            else
            {
                foreach (var game in Games.Where(g => g.SubFolder == _currentSubFolder))
                    newItems.Add(FolderItem.CreateGame(game));
            }

            if (DisplayItems.Count == newItems.Count && DisplayItems.SequenceEqual(newItems, new FolderItemComparer()))
                return;

            DisplayItems.Clear();
            foreach (var item in newItems)
                DisplayItems.Add(item);

            SelectedItem = DisplayItems.FirstOrDefault(i => i.IsSelected) ?? DisplayItems.FirstOrDefault();
        }

        public void EnterFolder(string subFolder)
        {
            if (SelectedItem != null)
            {
                int currentIndex = DisplayItems.IndexOf(SelectedItem);
                _scrollPositionStack.Push(currentIndex);
            }
            else
                _scrollPositionStack.Push(0);

            CurrentSubFolder = subFolder;
            BuildDisplayItems();
        }

        public void OnItemTapped(FolderItem item)
        {
            if (item.IsFolder) EnterFolder(item.SubFolder!);
            else if (item.IsGame) SelectedItem = item;
        }

        #endregion

        public void CommitSearch() => _collectionManager.CommittedSearchText = SearchText;

        public async Task LaunchRandomGame()
        {
            if (this.Games.Count == 0)
            {
                await DialogService.Instance.ShowMessage("알림", "실행 가능한 게임이 없습니다.", MessageType.Warning);
                return;
            }

            var favorites = this.Games.Where(g => g.IsFavorite).ToList();
            var nonFavorites = this.Games.Where(g => !g.IsFavorite).ToList();

            GameMetadata selectedGame;

            if (favorites.Count > 0 && nonFavorites.Count > 0)
            {
                if (_random.NextDouble() < 0.2)
                    selectedGame = favorites[_random.Next(favorites.Count)];
                else
                    selectedGame = nonFavorites[_random.Next(nonFavorites.Count)];
            }
            else if (favorites.Count > 0)
                selectedGame = favorites[_random.Next(favorites.Count)];
            else if (nonFavorites.Count > 0)
                selectedGame = nonFavorites[_random.Next(nonFavorites.Count)];
            else
            {
                await DialogService.Instance.ShowMessage("알림", "실행 가능한 게임이 없습니다.", MessageType.Warning);
                return;
            }
            
            this.SelectedGame = selectedGame;
            await this.LaunchGameAsync(this.SelectedGame);            
        }

        private class FolderItemComparer : IEqualityComparer<FolderItem>
        {
            public bool Equals(FolderItem? x, FolderItem? y)
            {
                if (x == null || y == null) return x == y;
                if (x.IsFolder != y.IsFolder) return false;
                if (x.IsFolder) return x.SubFolder == y.SubFolder;

                return x.Game == y.Game;
            }

            public int GetHashCode(FolderItem obj)
            {
                return obj.IsFolder ? obj.SubFolder?.GetHashCode() ?? 0 : obj.Game?.GetHashCode() ?? 0;
            }
        }
    }
}