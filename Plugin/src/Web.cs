using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace SMTModBrowser
{
    /// <summary>Coroutine helpers around UnityWebRequest.</summary>
    static class Web
    {
        const string UserAgent = ModBrowserPlugin.Name + "/" + ModBrowserPlugin.Version;

        public static IEnumerator GetText(string url, Action<string> done, Action<string> failed)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("User-Agent", UserAgent);
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success) done(request.downloadHandler.text);
                else failed(request.error);
            }
        }

        public static IEnumerator GetBytes(string url, Action<float> progress, Action<byte[]> done, Action<string> failed)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("User-Agent", UserAgent);
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    progress(request.downloadProgress);
                    yield return null;
                }
                if (request.result == UnityWebRequest.Result.Success) done(request.downloadHandler.data);
                else failed(request.error);
            }
        }

        public static IEnumerator GetTexture(string url, Action<Texture2D> done)
        {
            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                request.SetRequestHeader("User-Agent", UserAgent);
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    var texture = DownloadHandlerTexture.GetContent(request);
                    texture.hideFlags = HideFlags.HideAndDontSave;
                    done(texture);
                }
            }
        }
    }
}
