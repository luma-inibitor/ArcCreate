using System;
using UnityEngine;

namespace ArcCreate
{
    /// <summary>
    /// On Android, vendor game services (Samsung's Game Booster among them) decide whether a package is
    /// a game from the category its installer set, not from the manifest. Store installs get that from
    /// the store. A build installed from source can only get it by being its own installer
    /// (<c>adb install -i &lt;package&gt; …</c>) and then setting the category itself, which is what
    /// this does. Anywhere else the call is refused and nothing changes.
    /// </summary>
    public static class AndroidGameCategory
    {
        private const int CategoryGame = 0; // android.content.pm.ApplicationInfo.CATEGORY_GAME

        public static void Apply()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject packageManager = activity.Call<AndroidJavaObject>("getPackageManager"))
                {
                    packageManager.Call("setApplicationCategoryHint", Application.identifier, CategoryGame);
                }
            }
            catch (Exception)
            {
                // Not our own installer: the category stays whatever the installer chose.
            }
#endif
        }
    }
}
