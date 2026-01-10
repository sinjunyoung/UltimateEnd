using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using Android.Util;
using AndroidX.DocumentFile.Provider;
using System;
using System.Linq;

namespace UltimateEnd.Android.Services
{
    public class SafPermissionManager
    {
        private const int REQUEST_CODE_FOLDER = 1001;
        private static Action<global::Android.Net.Uri>? _pendingCallback;
        private static bool _isRequesting = false;

        public static void EnsureFolderPermission(string folderPath, Action<global::Android.Net.Uri> onPermissionGranted, Action? onPermissionDenied = null)
        {
            if (_isRequesting)
                return;

            var existingUri = FindExistingPermission(folderPath);
            if (existingUri != null)
            {
                onPermissionGranted?.Invoke(existingUri);
                return;
            }

            RequestFolderPermission(folderPath, onPermissionGranted, onPermissionDenied);
        }

        private static global::Android.Net.Uri? FindExistingPermission(string folderPath)
        {
            try
            {
                var persistedUris = MainActivity.Instance.ContentResolver.PersistedUriPermissions;

                Log.Debug("SafPermissionManager", $"Looking for permission: [{folderPath}]");
                Log.Debug("SafPermissionManager", $"Total persisted URIs: {persistedUris.Count}");

                foreach (var permission in persistedUris)
                {
                    if (permission.IsReadPermission)
                    {
                        var uriString = permission.Uri.ToString();
                        var permissionPath = ExtractFolderPath(uriString);

                        Log.Debug("SafPermissionManager", $"Checking URI: {uriString}");
                        Log.Debug("SafPermissionManager", $"Extracted path: [{permissionPath}]");
                        Log.Debug("SafPermissionManager", $"Match? {permissionPath == folderPath}");

                        if (permissionPath == folderPath)
                        {
                            Log.Debug("SafPermissionManager", "Found matching permission!");
                            return permission.Uri;
                        }
                    }
                }

                Log.Debug("SafPermissionManager", "No matching permission found");
            }
            catch (Exception ex)
            {
                Log.Error("SafPermissionManager", $"Error: {ex.Message}");
            }

            return null;
        }

        private static void RequestFolderPermission(string folderPath, Action<global::Android.Net.Uri> onGranted, Action onDenied)
        {
            if (_isRequesting) return;

            _isRequesting = true;
            _pendingCallback = onGranted;

            MainActivity.Instance.FolderPickerResult = null;

            MainActivity.Instance.RunOnUiThread(() =>
            {
                var dialog = new AlertDialog.Builder(MainActivity.Instance)
                    .SetTitle("폴더 접근 권한 필요")
                    .SetMessage($"다음 화면에서 반드시 '{folderPath}' 폴더를 선택해주세요.\n\n※ 정확한 폴더를 선택하지 않으면 에뮬레이터가 실행되지 않습니다.")
                    .SetPositiveButton("권한 부여", (s, e) =>
                    {
                        MainActivity.Instance.FolderPickerResult = (uri) =>
                        {
                            if (uri != null)
                                _pendingCallback?.Invoke(uri);
                            _pendingCallback = null;
                            _isRequesting = false;
                            MainActivity.Instance.FolderPickerResult = null;
                        };

                        var intent = new Intent(Intent.ActionOpenDocumentTree);
                        intent.AddFlags(
                            ActivityFlags.GrantReadUriPermission |
                            ActivityFlags.GrantWriteUriPermission |
                            ActivityFlags.GrantPersistableUriPermission);

                        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                        {
                            try
                            {
                                global::Android.Net.Uri? initialUri = null;

                                var romsUri = FindExistingPermission("ROMs");
                                if (romsUri != null && Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
                                {
                                    var romsDoc = DocumentFile.FromTreeUri(MainActivity.Instance, romsUri);
                                    var targetDoc = FindDocumentByPath(romsDoc, folderPath.Replace("ROMs/", string.Empty));
                                    if (targetDoc != null)
                                    {
                                        initialUri = targetDoc.Uri;
                                        Log.Debug("SafPermissionManager", $"Using DocumentFile URI: {initialUri}");
                                    }
                                    else
                                    {
                                        initialUri = BuildInitialUri(folderPath);
                                        Log.Debug("SafPermissionManager", $"DocumentFile not found, using fallback: {initialUri}");
                                    }
                                }
                                else
                                {
                                    initialUri = BuildInitialUri(folderPath);
                                    Log.Debug("SafPermissionManager", $"No ROMs permission, using fallback: {initialUri}");
                                }

                                if (initialUri != null)
                                {
                                    intent.PutExtra(DocumentsContract.ExtraInitialUri, initialUri);

                                    if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
                                        intent.PutExtra("android.provider.extra.INITIAL_URI", initialUri);
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error("SafPermissionManager", $"Error setting initial URI: {ex.Message}");
                            }
                        }

                        MainActivity.Instance.StartActivityForResult(intent, REQUEST_CODE_FOLDER);
                    })
                    .SetNegativeButton("취소", (s, e) =>
                    {
                        onDenied?.Invoke();
                        _pendingCallback = null;
                        _isRequesting = false;
                    })
                    .SetOnDismissListener(new DialogDismissListener(() =>
                    {
                        if (_isRequesting && _pendingCallback == null)
                            _isRequesting = false;
                    }))
                    .SetCancelable(false)
                    .Create();

                dialog.Show();
            });
        }

        private static DocumentFile FindDocumentByPath(DocumentFile root, string path)
        {
            var parts = path.Split('/');
            var current = root;

            foreach (var part in parts)
            {
                current = current.FindFile(part);
                if (current == null) return null;
            }

            return current;
        }

        private static global::Android.Net.Uri? BuildInitialUri(string folderPath)
        {
            try
            {
                var persistedUris = MainActivity.Instance.ContentResolver.PersistedUriPermissions;
                string storageId = "primary";

                if (persistedUris.Any())
                {
                    var firstUri = persistedUris.First().Uri.ToString();
                    storageId = ExtractStorageId(firstUri);
                    Log.Debug("SafPermissionManager", $"Extracted storage ID: {storageId}");
                }

                var treeId = $"{storageId}:{folderPath}";
                var encodedTreeId = global::Android.Net.Uri.Encode(treeId);
                var initialUriString = $"content://com.android.externalstorage.documents/tree/{encodedTreeId}";

                Log.Debug("SafPermissionManager", $"Built initial URI: {initialUriString}");

                return global::Android.Net.Uri.Parse(initialUriString);
            }
            catch (Exception ex)
            {
                Log.Error("SafPermissionManager", $"BuildInitialUri error: {ex.Message}");
                return null;
            }
        }

        public static string ExtractFolderPath(string uriString)
        {
            try
            {
                string docId;
                if (uriString.Contains("/document/"))
                {
                    var idx = uriString.LastIndexOf("/document/") + 10;
                    docId = uriString.Substring(idx);
                }
                else if (uriString.Contains("/tree/"))
                {
                    var idx = uriString.LastIndexOf("/tree/") + 6;
                    docId = uriString.Substring(idx);
                }
                else
                    return null;

                docId = global::Android.Net.Uri.Decode(docId);

                if (docId.Contains(":"))
                    return docId.Substring(docId.IndexOf(':') + 1);
            }
            catch { }

            return null;
        }

        private static string ExtractStorageId(string uriString)
        {
            try
            {
                string docId;
                if (uriString.Contains("/tree/"))
                {
                    var idx = uriString.LastIndexOf("/tree/") + 6;
                    docId = uriString.Substring(idx);
                }
                else
                    return "primary";

                docId = global::Android.Net.Uri.Decode(docId);

                if (docId.Contains(":"))
                    return docId.Substring(0, docId.IndexOf(':'));
            }
            catch { }

            return "primary";
        }

        class DialogDismissListener : Java.Lang.Object, IDialogInterfaceOnDismissListener
        {
            private readonly Action _onDismiss;

            public DialogDismissListener(Action onDismiss)
            {
                _onDismiss = onDismiss;
            }

            public void OnDismiss(IDialogInterface? dialog) => _onDismiss?.Invoke();
        }
    }    
}