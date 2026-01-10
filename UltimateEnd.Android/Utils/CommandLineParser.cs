using Android.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UltimateEnd.Android.Utils
{
    public static class CommandLineParser
    {
        public static List<string> SplitCommandLine(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
                return new List<string>();

            var result = new List<string>();
            var current = new StringBuilder();
            bool inDoubleQuotes = false;
            bool inSingleQuotes = false;
            bool escapeNext = false;

            foreach (char c in commandLine)
            {
                if (escapeNext)
                {
                    current.Append(c);
                    escapeNext = false;
                    continue;
                }

                if (c == '\\')
                {
                    escapeNext = true;
                    continue;
                }

                if (c == '"' && !inSingleQuotes)
                    inDoubleQuotes = !inDoubleQuotes;
                else if (c == '\'' && !inDoubleQuotes)
                    inSingleQuotes = !inSingleQuotes;
                else if (char.IsWhiteSpace(c) && !inDoubleQuotes && !inSingleQuotes)
                {
                    if (current.Length > 0)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                    current.Append(c);
            }

            if (current.Length > 0)
                result.Add(current.ToString());

            result.RemoveAll(arg =>
                arg.Equals("am", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("start", StringComparison.OrdinalIgnoreCase));

            return result;
        }

        public static Intent ParseIntentCommand(List<string> args, Func<string, string> tokenReplacer = null!)
        {
            if (args == null || args.Count == 0)
                throw new ArgumentException("No intent arguments provided");

            var intent = new Intent();
            string dataUri = null;
            string mimeType = null;
            int i = 0;

            while (i < args.Count)
            {
                var opt = args[i++];

                switch (opt.ToLowerInvariant())
                {
                    case "-a":
                        if (i < args.Count)
                            intent.SetAction(args[i++]);
                        break;

                    case "-d":
                        if (i < args.Count)
                            dataUri = ApplyTokens(args[i++], tokenReplacer);
                        break;

                    case "-t":
                        if (i < args.Count)
                            mimeType = args[i++];
                        break;

                    case "-i":
                        if (i < args.Count)
                        {
                            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.Q)
                                intent.SetIdentifier(args[i++]);
                            else
                                i++;
                        }
                        break;

                    case "-c":
                        if (i < args.Count)
                            intent.AddCategory(args[i++]);
                        break;

                    case "-n":
                        if (i < args.Count)
                        {
                            var componentStr = args[i++];
                            var cn = ComponentName.UnflattenFromString(componentStr);
                            if (cn != null)
                                intent.SetComponent(cn);
                        }
                        break;

                    case "-p":
                        if (i < args.Count)
                            intent.SetPackage(args[i++]);
                        break;

                    case "-f":
                        if (i < args.Count && int.TryParse(args[i++], out int flags))
                            intent.SetFlags((ActivityFlags)flags);
                        break;

                    case "-e":
                    case "--es":
                        if (i + 1 < args.Count)
                        {
                            var key = args[i++];
                            var value = ApplyTokens(args[i++], tokenReplacer);
                            intent.PutExtra(key, value);
                        }
                        break;

                    case "--esn":
                        if (i < args.Count)
                        {
                            var key = args[i++];
                            intent.PutExtra(key, (string)null);
                        }
                        break;

                    case "--ei":
                        if (i + 1 < args.Count)
                        {
                            var key = args[i++];
                            if (int.TryParse(args[i++], out int intVal))
                                intent.PutExtra(key, intVal);
                        }
                        break;

                    case "--eia":
                        if (i + 1 < args.Count)
                        {
                            var key = args[i++];
                            var values = args[i++].Split(',');
                            var intArray = values.Select(v => int.TryParse(v.Trim(), out int x) ? x : 0).ToArray();
                            intent.PutExtra(key, intArray);
                        }
                        break;

                    case "--eial":
                        if (i + 1 < args.Count)
                        {
                            var key = args[i++];
                            var values = args[i++].Split(',');
                            var intList = new List<Java.Lang.Integer>();
                            foreach (var v in values)
                            {
                                if (int.TryParse(v.Trim(), out int x))
                                    intList.Add(new Java.Lang.Integer(x));
                            }
                            intent.PutIntegerArrayListExtra(key, intList);
                        }
                        break;

                    case "--el":
                        if (i + 1 < args.Count)
                        {
                            var key = args[i++];
                            if (long.TryParse(args[i++], out long longVal))
                                intent.PutExtra(key, longVal);
                        }
                        break;

                    case "--ela":
                        if (i + 1 < args.Count)
                        {
                            var key = args[i++];
                            var values = args[i++].Split(',');
                            var longArray = values.Select(v => long.TryParse(v.Trim(), out long x) ? x : 0L).ToArray();
                            intent.PutExtra(key, longArray);
                        }
                        break;

                    case "--elal":
                        if (i + 1 < args.Count)
                        {
                            var key = args[i++];
                            var values = args[i++].Split(',');
                            var longList = values.Select(v => long.TryParse(v.Trim(), out long x) ? x : 0L).ToList();
                            intent.PutExtra(key, longList.ToArray());
                        }
                        break;

                    case "--ef":
                        if (i + 1 < args.Count)
                        {
                            var key = args[i++];
                            if (float.TryParse(args[i++], out float floatVal))
                                intent.PutExtra(key, floatVal);
                        }
                        break;

                    case "--efa":
                        if (i + 1 < args.Count)
                        {
                            var key = args[i++];
                            var values = args[i++].Split(',');
                            var floatArray = values.Select(v => float.TryParse(v.Trim(), out float x) ? x : 0f).ToArray();
                            intent.PutExtra(key, floatArray);
                        }
                        break;

                    case "--efal":
                        if (i + 1 < args.Count)
                        {
                            var key = args[i++];
                            var values = args[i++].Split(',');
                            var floatList = values.Select(v => float.TryParse(v.Trim(), out float x) ? x : 0f).ToList();
                            intent.PutExtra(key, floatList.ToArray());
                        }
                        break;

                    case "--ez":
                        if (i + 1 < args.Count)
                        {
                            var key = args[i++];
                            var value = args[i++].ToLowerInvariant();
                            bool boolVal = ParseBoolean(value);
                            intent.PutExtra(key, boolVal);
                        }
                        break;

                    case "--esa":
                        if (i + 1 < args.Count)
                        {
                            var key = args[i++];
                            var value = ApplyTokens(args[i++], tokenReplacer);
                            var stringArray = SplitEscapedComma(value);
                            intent.PutExtra(key, stringArray);
                        }
                        break;

                    case "--esal":
                        if (i + 1 < args.Count)
                        {
                            var key = args[i++];
                            var value = ApplyTokens(args[i++], tokenReplacer);
                            var stringArray = SplitEscapedComma(value);
                            intent.PutStringArrayListExtra(key, new List<string>(stringArray));
                        }
                        break;

                    case "--eu":
                        if (i + 1 < args.Count)
                        {
                            var key = args[i++];
                            var uriStr = ApplyTokens(args[i++], tokenReplacer);
                            var uri = global::Android.Net.Uri.Parse(uriStr);
                            intent.PutExtra(key, uri);
                        }
                        break;

                    case "--ecn":
                        if (i + 1 < args.Count)
                        {
                            var key = args[i++];
                            var cn = ComponentName.UnflattenFromString(args[i++]);
                            if (cn != null)
                                intent.PutExtra(key, cn);
                        }
                        break;

                    case "--grant-read-uri-permission":
                        intent.AddFlags(ActivityFlags.GrantReadUriPermission);
                        break;

                    case "--grant-write-uri-permission":
                        intent.AddFlags(ActivityFlags.GrantWriteUriPermission);
                        break;

                    case "--grant-persistable-uri-permission":
                        intent.AddFlags(ActivityFlags.GrantPersistableUriPermission);
                        break;

                    case "--grant-prefix-uri-permission":
                        intent.AddFlags(ActivityFlags.GrantPrefixUriPermission);
                        break;

                    case "--activity-brought-to-front":
                        intent.AddFlags(ActivityFlags.BroughtToFront);
                        break;

                    case "--activity-clear-top":
                        intent.AddFlags(ActivityFlags.ClearTop);
                        break;

                    case "--activity-clear-task":
                        intent.AddFlags(ActivityFlags.ClearTask);
                        break;

                    case "--activity-clear-when-task-reset":
                        intent.AddFlags(ActivityFlags.ClearWhenTaskReset);
                        break;

                    case "--activity-exclude-from-recents":
                        intent.AddFlags(ActivityFlags.ExcludeFromRecents);
                        break;

                    case "--activity-launched-from-history":
                        intent.AddFlags(ActivityFlags.LaunchedFromHistory);
                        break;

                    case "--activity-multiple-task":
                        intent.AddFlags(ActivityFlags.MultipleTask);
                        break;

                    case "--activity-no-animation":
                        intent.AddFlags(ActivityFlags.NoAnimation);
                        break;

                    case "--activity-no-history":
                        intent.AddFlags(ActivityFlags.NoHistory);
                        break;

                    case "--activity-no-user-action":
                        intent.AddFlags(ActivityFlags.NoUserAction);
                        break;

                    case "--activity-previous-is-top":
                        intent.AddFlags(ActivityFlags.PreviousIsTop);
                        break;

                    case "--activity-reorder-to-front":
                        intent.AddFlags(ActivityFlags.ReorderToFront);
                        break;

                    case "--activity-reset-task-if-needed":
                        intent.AddFlags(ActivityFlags.ResetTaskIfNeeded);
                        break;

                    case "--activity-single-top":
                        intent.AddFlags(ActivityFlags.SingleTop);
                        break;

                    case "--activity-new-task":
                        intent.AddFlags(ActivityFlags.NewTask);
                        break;

                    case "--activity-task-on-home":
                        intent.AddFlags(ActivityFlags.TaskOnHome);
                        break;

                    case "--activity-match-external":
                        if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.P)
                            intent.AddFlags(ActivityFlags.MatchExternal);
                        break;

                    case "--exclude-stopped-packages":
                        intent.AddFlags(ActivityFlags.ExcludeStoppedPackages);
                        break;

                    case "--include-stopped-packages":
                        intent.AddFlags(ActivityFlags.IncludeStoppedPackages);
                        break;

                    case "--debug-log-resolution":
                        intent.AddFlags(ActivityFlags.DebugLogResolution);
                        break;

                    case "--receiver-registered-only":
                        intent.AddFlags(ActivityFlags.ReceiverRegisteredOnly);
                        break;

                    case "--receiver-replace-pending":
                        intent.AddFlags(ActivityFlags.ReceiverReplacePending);
                        break;

                    case "--receiver-foreground":
                        intent.AddFlags(ActivityFlags.ReceiverForeground);
                        break;

                    case "--receiver-no-abort":
                        intent.AddFlags(ActivityFlags.ReceiverNoAbort);
                        break;

                    case "-D": // 대문자 D (디버그)
                    case "-N": // 대문자 N
                    case "-W": // 대문자 W (wait)
                    case "-S": // 대문자 S (strict mode)
                    case "--streaming":
                    case "--track-allocation":
                    case "--task-overlay":
                    case "--lock-task":
                    case "--allow-background-activity-starts":
                        break;

                    case "-P": // 대문자 P (profiler file)
                    case "-R": // 대문자 R (repeat)
                    case "--start-profiler":
                    case "--sampling":
                    case "--attach-agent":
                    case "--attach-agent-bind":
                    case "--user":
                    case "--receiver-permission":
                    case "--display":
                    case "--windowingmode":
                    case "--activitytype":
                    case "--task":
                        if (i < args.Count) i++;
                        break;

                    default:
                        if (opt.StartsWith("--"))
                            System.Diagnostics.Debug.WriteLine($"Warning: Unknown option '{opt}'");
                        break;
                }
            }

            if (!string.IsNullOrEmpty(dataUri) || !string.IsNullOrEmpty(mimeType))
            {
                if (!string.IsNullOrEmpty(dataUri))
                {
                    var uri = global::Android.Net.Uri.Parse(dataUri);
                    intent.SetDataAndType(uri, mimeType ?? "*/*");
                }
                else
                    intent.SetType(mimeType);
            }

            return intent;
        }

        private static bool ParseBoolean(string value)
        {
            if (value == "true" || value == "t")
                return true;

            if (value == "false" || value == "f")
                return false;

            if (int.TryParse(value, out int intVal))
                return intVal != 0;

            throw new ArgumentException($"Invalid boolean value: {value}");
        }

        private static string[] SplitEscapedComma(string value)
        {
            var result = new List<string>();
            var current = new StringBuilder();

            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\\' && i + 1 < value.Length && value[i + 1] == ',')
                {
                    current.Append(',');
                    i++;
                }
                else if (value[i] == ',')
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                    current.Append(value[i]);
            }

            if (current.Length > 0)
                result.Add(current.ToString());

            return result.ToArray();
        }

        private static string ApplyTokens(string value, Func<string, string> tokenReplacer)
        {
            return tokenReplacer?.Invoke(value) ?? value;
        }

        public static string FlagToString(ActivityFlags flag)
        {
            return flag switch
            {
                ActivityFlags.ClearTask => "--activity-clear-task",
                ActivityFlags.ClearTop => "--activity-clear-top",
                ActivityFlags.NoHistory => "--activity-no-history",
                ActivityFlags.SingleTop => "--activity-single-top",
                ActivityFlags.NewTask => "--activity-new-task",
                ActivityFlags.MultipleTask => "--activity-multiple-task",
                ActivityFlags.BroughtToFront => "--activity-brought-to-front",
                ActivityFlags.ResetTaskIfNeeded => "--activity-reset-task-if-needed",
                ActivityFlags.ExcludeFromRecents => "--activity-exclude-from-recents",
                ActivityFlags.ClearWhenTaskReset => "--activity-clear-when-task-reset",
                ActivityFlags.NoAnimation => "--activity-no-animation",
                ActivityFlags.ReorderToFront => "--activity-reorder-to-front",
                ActivityFlags.GrantReadUriPermission => "--grant-read-uri-permission",
                ActivityFlags.GrantWriteUriPermission => "--grant-write-uri-permission",
                _ => string.Empty
            };
        }

        public static ActivityFlags ParseFlag(string flag)
        {
            if (string.IsNullOrWhiteSpace(flag))
                return 0;

            return flag.Trim().ToLowerInvariant() switch
            {
                "--activity-clear-task" => ActivityFlags.ClearTask,
                "--activity-clear-top" => ActivityFlags.ClearTop,
                "--activity-no-history" => ActivityFlags.NoHistory,
                "--activity-single-top" => ActivityFlags.SingleTop,
                "--activity-new-task" => ActivityFlags.NewTask,
                "--activity-multiple-task" => ActivityFlags.MultipleTask,
                "--activity-brought-to-front" => ActivityFlags.BroughtToFront,
                "--activity-reset-task-if-needed" => ActivityFlags.ResetTaskIfNeeded,
                "--activity-exclude-from-recents" => ActivityFlags.ExcludeFromRecents,
                "--activity-clear-when-task-reset" => ActivityFlags.ClearWhenTaskReset,
                "--activity-no-animation" => ActivityFlags.NoAnimation,
                "--activity-reorder-to-front" => ActivityFlags.ReorderToFront,
                "--grant-read-uri-permission" => ActivityFlags.GrantReadUriPermission,
                "--grant-write-uri-permission" => ActivityFlags.GrantWriteUriPermission,
                _ => 0
            };
        }

        public static string ExtractPackageName(string launchCommand)
        {
            if (string.IsNullOrWhiteSpace(launchCommand))
                return null;

            var args = SplitCommandLine(launchCommand);

            for (int i = 0; i < args.Count; i++)
            {
                if (args[i].Equals("-n", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
                {
                    var component = args[i + 1];
                    var slashIndex = component.IndexOf('/');
                    return slashIndex > 0 ? component.Substring(0, slashIndex) : component;
                }
                else if (args[i].Equals("-p", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
                    return args[i + 1];
            }

            return null;
        }
        public static ComponentName ExtractComponentName(string launchCommand)
        {
            if (string.IsNullOrWhiteSpace(launchCommand))
                return null;

            var args = SplitCommandLine(launchCommand);

            for (int i = 0; i < args.Count; i++)
                if (args[i].Equals("-n", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
                    return ComponentName.UnflattenFromString(args[i + 1]);

            return null;
        }
    }
}