using Android.Content;
using System;
using System.IO;
using System.Threading.Tasks;
using UltimateEnd.Android.Models;
using UltimateEnd.Android.Utils;
using UltimateEnd.Models;

namespace UltimateEnd.Android.Services
{
    public class IntentBuilder(Context context)
    {
        private readonly Context _context = context ?? throw new ArgumentNullException(nameof(context));
        private readonly UriPermissionManager _permissionManager = new(context);

        public async Task<Intent> BuildAsync(Command command, string romPath)
        {
            if (string.IsNullOrWhiteSpace(command.LaunchCommand)) throw new InvalidOperationException("LaunchCommand가 비어있습니다.");

            var context = PrepareTokenContext(command, romPath);
            var args = CommandLineParser.SplitCommandLine(command.LaunchCommand);
            var intent = CommandLineParser.ParseIntentCommand(args, token => TemplateVariableManager.ReplaceTokens(token, context));

            if (intent.Data != null && intent.Data.Scheme == "content") await _permissionManager.EnsurePermissionAsync(intent);

            return intent;
        }

        private TokenContext PrepareTokenContext(Command command, string romPath)
        {
            string romName = Path.GetFileNameWithoutExtension(romPath);
            string directory = Path.GetDirectoryName(romPath) ?? string.Empty;
            var file = new Java.IO.File(romPath);

            string safUriString;
            try
            {
                var fileProviderType = Type.GetType("AndroidX.Core.Content.FileProvider, Xamarin.AndroidX.Core");
                var getUriMethod = fileProviderType?.GetMethod("GetUriForFile", [typeof(Context), typeof(string), typeof(Java.IO.File)]);
                var safUri = getUriMethod?.Invoke(null, [_context, "com.yamesoft.ultimateend.fileprovider", file]) as global::Android.Net.Uri;
                safUriString = safUri?.ToString() ?? string.Empty;
            }
            catch
            {
                safUriString = global::Android.Net.Uri.FromFile(file).ToString();
            }

            var fileUri = global::Android.Net.Uri.FromFile(file).ToString();

            return new TokenContext
            {
                RomPath = romPath,
                RomDir = directory,
                RomName = romName,
                CoreName = command.CoreName ?? string.Empty,
                SafUriRomPath = safUriString,
                FileUriRomPath = fileUri
            };
        }
    }
}