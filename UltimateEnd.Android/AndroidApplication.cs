using Android.App;
using Android.Content;
using Android.Runtime;
using System;

namespace UltimateEnd.Android
{
    [Application]
    public class AndroidApplication(IntPtr handle, JniHandleOwnership transfer) : global::Android.App.Application(handle, transfer)
    {
        public static Context? AppContext { get; private set; }

        public override void OnCreate()
        {
            base.OnCreate();
            AppContext = this.ApplicationContext;
        }
    }
}