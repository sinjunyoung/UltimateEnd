using Android.Content;
using Android.Util;
using AndroidX.Media3.UI;
using System;

namespace UltimateEnd.Android.Controls
{
    public class CustomPlayerView : PlayerView
    {
        private const int SURFACE_TYPE_TEXTURE_VIEW = 2;
        private static bool _isReflectionCached = false;
        private static Java.Lang.Reflect.Field? _surfaceTypeField;

        public CustomPlayerView(Context context) : base(context, null) => InitializeView(context);

        public CustomPlayerView(Context context, IAttributeSet attrs) : base(context, attrs) => InitializeView(context);

        protected CustomPlayerView(IntPtr javaReference, global::Android.Runtime.JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }

        private void InitializeView(Context context)
        {
            try
            {
                this.UseController = false;

                if (!_isReflectionCached)
                    CacheSurfaceTypeField();

                if (_surfaceTypeField != null)
                    _surfaceTypeField.SetInt(this, SURFACE_TYPE_TEXTURE_VIEW);
                else
                    SetSurfaceTypeDirectly();
            }
            catch { }
        }

        private static void CacheSurfaceTypeField()
        {
            try
            {
                _surfaceTypeField = Java.Lang.Class.FromType(typeof(PlayerView))
                    .GetDeclaredField("surfaceType");

                if (_surfaceTypeField != null)
                    _surfaceTypeField.Accessible = true;

                _isReflectionCached = true;
            }
            catch
            {
                _isReflectionCached = true;
            }
        }

        private void SetSurfaceTypeDirectly()
        {
            try
            {
                var setShutterBackgroundColorMethod = Java.Lang.Class.FromType(typeof(PlayerView))
                    .GetMethod("setShutterBackgroundColor", Java.Lang.Integer.Type);

                this.SetWillNotDraw(false);
            }
            catch { }
        }
    }
}