using System;
using System.Collections;
using System.IO;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

/// <summary>
/// Saves a PNG into the phone's photo gallery. On Android (minSdk 29) this goes through
/// MediaStore into Pictures/AbhyasAR, which needs no storage permission. Elsewhere (the Editor,
/// desktop builds) it writes to the user's Pictures folder so the flow can still be tested.
/// </summary>
public static class GallerySaver
{
    private const string Album = "AbhyasAR";
    private const string WritePermission = "android.permission.WRITE_EXTERNAL_STORAGE";

    /// <summary>
    /// Asks for gallery access when the OS requires it. Android 10+ (API 29+) lets an app add its own
    /// images through MediaStore with no permission, so the prompt only appears on the older
    /// versions that need WRITE_EXTERNAL_STORAGE. Calls back with whether saving may proceed.
    /// </summary>
    public static IEnumerator EnsurePermission(Action<bool> done)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        int sdk;
        using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
        {
            sdk = version.GetStatic<int>("SDK_INT");
        }
        if (sdk <= 29 && !Permission.HasUserAuthorizedPermission(WritePermission))
        {
            Permission.RequestUserPermission(WritePermission);
            // The dialog pauses the app; wait for focus to return, then a beat for the result.
            yield return new WaitForSecondsRealtime(0.3f);
            float timeout = 60f;
            while (!Application.isFocused && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.3f);
            done?.Invoke(Permission.HasUserAuthorizedPermission(WritePermission));
            yield break;
        }
#endif
        done?.Invoke(true);
        yield break;
    }

    /// <summary>Returns true on success; <paramref name="location"/> is where it was saved, or the error.</summary>
    public static bool SavePng(byte[] png, string fileName, out string location)
    {
        location = string.Empty;
        if (png == null || png.Length == 0)
        {
            location = "empty image";
            return false;
        }

        try
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return SaveAndroid(png, fileName, out location);
#else
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), Album);
            if (string.IsNullOrEmpty(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)))
            {
                dir = Path.Combine(Application.persistentDataPath, Album);
            }
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName);
            File.WriteAllBytes(path, png);
            location = path;
            return true;
#endif
        }
        catch (Exception ex)
        {
            Debug.LogError($"GallerySaver: could not save '{fileName}' — {ex}");
            location = ex.Message;
            return false;
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static bool SaveAndroid(byte[] png, string fileName, out string location)
    {
        location = string.Empty;
        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
        using (var values = new AndroidJavaObject("android.content.ContentValues"))
        using (var media = new AndroidJavaClass("android.provider.MediaStore$Images$Media"))
        {
            values.Call("put", "_display_name", fileName);
            values.Call("put", "mime_type", "image/png");
            values.Call("put", "relative_path", "Pictures/" + Album);

            using (var collection = media.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"))
            using (var uri = resolver.Call<AndroidJavaObject>("insert", collection, values))
            {
                if (uri == null)
                {
                    location = "MediaStore refused the new image";
                    return false;
                }
                using (var stream = resolver.Call<AndroidJavaObject>("openOutputStream", uri))
                {
                    stream.Call("write", (sbyte[])(Array)png);
                    stream.Call("flush");
                    stream.Call("close");
                }
                location = "Pictures/" + Album + "/" + fileName;
                return true;
            }
        }
    }
#endif
}
