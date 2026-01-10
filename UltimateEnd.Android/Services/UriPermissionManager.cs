using Android.Content;
using System.Threading.Tasks;

namespace UltimateEnd.Android.Services
{
    public class UriPermissionManager(Context context)
    {
        private readonly Context _context = context;

        public Task EnsurePermissionAsync(Intent intent)
        {
            if (intent.Data == null)
                return Task.CompletedTask;

            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);

            var targetPackage = intent.Component?.PackageName;

            if (!string.IsNullOrEmpty(targetPackage))
            {
                _context.GrantUriPermission(
                    targetPackage,
                    intent.Data,
                    ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
            }

            return Task.CompletedTask;
        }

        public void GrantPermission(string packageName, global::Android.Net.Uri uri)
        {
            if (string.IsNullOrEmpty(packageName) || uri == null)
                return;

            _context.GrantUriPermission(
                packageName,
                uri,
                ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
        }
    }
}