using System.Threading.Tasks;

namespace UltimateEnd.Android.Utils
{
    public static class AndroidDialogHelper
    {
        public static Task ShowToastAsync(string message, bool longDuration = false)
        {
            try
            {
                var activity = MainActivity.Instance;
                activity?.RunOnUiThread(() =>
                {
                    var duration = longDuration
                        ? global::Android.Widget.ToastLength.Long
                        : global::Android.Widget.ToastLength.Short;

                    global::Android.Widget.Toast.MakeText(
                        activity,
                        message,
                        duration
                    )?.Show();
                });
            }
            catch { }

            return Task.CompletedTask;
        }

        public static async Task<bool> ShowErrorAndAskRetryAsync(string message, string title = "오류")
        {
            try
            {
                var activity = MainActivity.Instance;
                if (activity == null) return false;

                var tcs = new TaskCompletionSource<bool>();

                await activity.RunOnUiThreadAsync(() =>
                {
                    var builder = new global::Android.App.AlertDialog.Builder(activity);
                    builder.SetTitle(title);
                    builder.SetMessage(message);
                    builder.SetPositiveButton("재시도", (s, e) => tcs.TrySetResult(true));
                    builder.SetNegativeButton("취소", (s, e) => tcs.TrySetResult(false));
                    builder.SetCancelable(false);
                    builder.Show();
                });

                return await tcs.Task;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> ShowConfirmDialogAsync(
            string message,
            string title = "확인",
            string positiveButton = "확인",
            string negativeButton = "취소")
        {
            try
            {
                var activity = MainActivity.Instance;
                if (activity == null) return false;

                var tcs = new TaskCompletionSource<bool>();

                await activity.RunOnUiThreadAsync(() =>
                {
                    var builder = new global::Android.App.AlertDialog.Builder(activity);
                    builder.SetTitle(title);
                    builder.SetMessage(message);
                    builder.SetPositiveButton(positiveButton, (s, e) => tcs.TrySetResult(true));
                    builder.SetNegativeButton(negativeButton, (s, e) => tcs.TrySetResult(false));
                    builder.SetCancelable(false);
                    builder.Show();
                });

                return await tcs.Task;
            }
            catch
            {
                return false;
            }
        }

        public static async Task ShowInfoDialogAsync(string message, string title = "알림")
        {
            try
            {
                var activity = MainActivity.Instance;
                if (activity == null) return;

                var tcs = new TaskCompletionSource<bool>();

                await activity.RunOnUiThreadAsync(() =>
                {
                    var builder = new global::Android.App.AlertDialog.Builder(activity);
                    builder.SetTitle(title);
                    builder.SetMessage(message);
                    builder.SetPositiveButton("확인", (s, e) => tcs.TrySetResult(true));
                    builder.SetCancelable(false);
                    builder.Show();
                });

                await tcs.Task;
            }
            catch { }
        }
    }
}