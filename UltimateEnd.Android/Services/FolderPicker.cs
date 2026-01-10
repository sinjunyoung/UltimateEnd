using Android.App;
using Android.Content;
using Android.OS;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UltimateEnd.Services;

namespace UltimateEnd.Android.Services
{
    public class FolderPicker : IFolderPicker
    {
        public Task<string?> PickFolderAsync(string title, string? defaultPath = null)
        {
            var tcs = new TaskCompletionSource<string?>();

            MainActivity.Instance?.RunOnUiThread(() =>
            {
                if (!string.IsNullOrEmpty(defaultPath) && Directory.Exists(defaultPath))
                {
                    ShowFolderBrowser(defaultPath, title, tcs);
                }
                else
                {
                    ShowStorageSelector(title, tcs);
                }
            });

            return tcs.Task;
        }

        private void ShowStorageSelector(string title, TaskCompletionSource<string?> tcs)
        {
            try
            {
                var storages = new List<string>();
                var displayNames = new List<string>();

                var internalPath = Environment.ExternalStorageDirectory?.AbsolutePath;
                if (internalPath != null && Directory.Exists(internalPath))
                {
                    storages.Add(internalPath);
                    displayNames.Add("📱 내부 저장소");
                }

                if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
                {
                    var externalDirs = MainActivity.Instance?.GetExternalFilesDirs(null);
                    if (externalDirs != null)
                    {
                        foreach (var dir in externalDirs)
                        {
                            if (dir != null && dir.AbsolutePath.Contains("/Android/data/"))
                            {
                                var parts = dir.AbsolutePath.Split(["/Android/"], System.StringSplitOptions.None);
                                if (parts.Length > 0)
                                {
                                    var storagePath = parts[0];
                                    if (!storages.Contains(storagePath))
                                    {
                                        storages.Add(storagePath);

                                        var storageManager = MainActivity.Instance?.GetSystemService(Context.StorageService) as global::Android.OS.Storage.StorageManager;
                                        var volumeName = storageManager?.GetStorageVolume(new Java.IO.File(storagePath))?.GetDescription(MainActivity.Instance)
                                            ?? "SD 카드";

                                        displayNames.Add($"💾 {volumeName}");
                                    }
                                }
                            }
                        }
                    }
                }

                if (storages.Count == 0)
                {
                    var storageDirs = Directory.GetDirectories("/storage/")
                        .Where(d =>
                        {
                            var name = Path.GetFileName(d);
                            return name != "self" && !name.StartsWith('.') && name != "emulated";
                        })
                        .ToArray();

                    foreach (var dir in storageDirs)
                    {
                        try
                        {
                            if (Directory.Exists(dir) && Directory.GetDirectories(dir).Length > 0)
                            {
                                storages.Add(dir);
                                displayNames.Add($"💾 저장소 ({Path.GetFileName(dir)})");
                            }
                        }
                        catch { }
                    }

                    var emulated0 = "/storage/emulated/0";
                    if (Directory.Exists(emulated0))
                    {
                        storages.Insert(0, emulated0);
                        displayNames.Insert(0, "📱 내부 저장소");
                    }
                }

                if (storages.Count == 0)
                {
                    ShowError("사용 가능한 저장소를 찾을 수 없습니다.", tcs);
                    return;
                }

                var builder = new AlertDialog.Builder(MainActivity.Instance);
                builder.SetTitle(title);
                builder.SetItems(displayNames.ToArray(), (s, e) =>
                {
                    var index = ((DialogClickEventArgs)e).Which;
                    ShowFolderBrowser(storages[index], title, tcs);
                });
                builder.SetNegativeButton("취소", (s, e) => tcs.TrySetResult(null));
                builder.Show();
            }
            catch (System.Exception ex)
            {
                ShowError("저장소를 불러올 수 없습니다: " + ex.Message, tcs);
            }
        }

        private void ShowFolderBrowser(string currentPath, string title, TaskCompletionSource<string?> tcs)
        {
            try
            {
                var directories = Directory.GetDirectories(currentPath)
                    .Select(Path.GetFileName)
                    .Where(d => !d.StartsWith('.'))
                    .OrderBy(d => d)
                    .ToArray();

                var builder = new AlertDialog.Builder(MainActivity.Instance);
                builder.SetTitle($"{title}\n📁 {currentPath}");

                builder.SetPositiveButton("✅ 이 폴더 선택", (s, e) =>
                {
                    tcs.TrySetResult(currentPath);
                });

                if (!currentPath.StartsWith("/storage/emulated/0") || currentPath != "/storage/emulated/0")
                {
                    if (currentPath.Length > "/storage/emulated/0".Length &&
                        currentPath.Length > "/storage/".Length + 16)
                    {
                        builder.SetNeutralButton("⬆️ 상위", (s, e) =>
                        {
                            var parentPath = Path.GetDirectoryName(currentPath);
                            if (parentPath != null)
                                ShowFolderBrowser(parentPath, title, tcs);
                        });
                    }
                }

                builder.SetNegativeButton("❌ 취소", (s, e) => tcs.TrySetResult(null));

                builder.SetItems(directories!, (s, e) =>
                {
                    var selectedFolder = Path.Combine(currentPath, directories[e.Which]);
                    ShowFolderBrowser(selectedFolder, title, tcs);
                });

                builder.Show();
            }
            catch (System.Exception ex)
            {
                ShowError($"폴더 접근 실패: {ex.Message}", tcs);
            }
        }

        private static void ShowError(string message, TaskCompletionSource<string?> tcs)
        {
            var builder = new AlertDialog.Builder(MainActivity.Instance);
            builder.SetTitle("⚠️ 오류");
            builder.SetMessage(message);
            builder.SetPositiveButton("확인", (s, e) => tcs.TrySetResult(null));
            builder.Show();
        }
    }
}