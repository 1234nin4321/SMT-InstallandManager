using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace SMTDecorator
{
    // Bringing pictures in from the player's PC: Windows' own Open dialog, or the Import folder for anyone it doesn't
    // work for. Big pictures are scaled down and saved as JPEG (PNG when they have see-through parts) before they're
    // kept, so they travel quickly.
    static class ImageFiles
    {
        // ---- Importing ----

        // Reads a picture file and keeps it in the picture store. Returns its name, or null with the reason.
        public static string Import(string path, out string error)
        {
            error = null;
            byte[] original;
            try
            {
                original = File.ReadAllBytes(path);
            }
            catch (Exception e)
            {
                error = "Could not read the file: " + e.Message;
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(original))
                {
                    error = "That isn't a PNG or JPEG picture.";
                    return null;
                }
                int max = DecoratorPlugin.MaxImageSize.Value;
                var scaled = texture.width > max || texture.height > max ? Scale(texture, max) : texture;
                try
                {
                    var bytes = HasTransparency(scaled) ? scaled.EncodeToPNG() : scaled.EncodeToJPG(88);
                    return Store.SaveImage(bytes);
                }
                finally
                {
                    if (scaled != texture) UnityEngine.Object.Destroy(scaled);
                }
            }
            catch (Exception e)
            {
                error = "Could not import the picture: " + e.Message;
                return null;
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        // Scaled on the CPU, smoothing over the pixels each new pixel covers
        static Texture2D Scale(Texture2D source, int max)
        {
            float factor = (float)max / Mathf.Max(source.width, source.height);
            int width = Mathf.Max(1, Mathf.RoundToInt(source.width * factor));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height * factor));
            var pixels = source.GetPixels32();
            var scaled = new Color32[width * height];
            int sw = source.width, sh = source.height;
            for (int y = 0; y < height; y++)
            {
                int y0 = y * sh / height, y1 = Math.Max(y0 + 1, (y + 1) * sh / height);
                for (int x = 0; x < width; x++)
                {
                    int x0 = x * sw / width, x1 = Math.Max(x0 + 1, (x + 1) * sw / width);
                    int r = 0, g = 0, b = 0, a = 0, n = 0;
                    for (int sy = y0; sy < y1; sy++)
                        for (int sx = x0; sx < x1; sx++)
                        {
                            var p = pixels[sy * sw + sx];
                            r += p.r; g += p.g; b += p.b; a += p.a; n++;
                        }
                    scaled[y * width + x] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), (byte)(a / n));
                }
            }
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.SetPixels32(scaled);
            result.Apply();
            return result;
        }

        static readonly Dictionary<int, bool> transparency = new Dictionary<int, bool>();

        // Whether any part of the picture is see-through
        public static bool HasTransparency(Texture2D texture)
        {
            if (texture.format != TextureFormat.RGBA32 && texture.format != TextureFormat.ARGB32) return false;
            int id = texture.GetInstanceID();
            if (transparency.TryGetValue(id, out var known)) return known;
            bool found = false;
            try
            {
                foreach (var pixel in texture.GetPixels32())
                    if (pixel.a < 250)
                    {
                        found = true;
                        break;
                    }
            }
            catch (Exception)
            {
                // Not readable: treat it as solid
            }
            transparency[id] = found;
            return found;
        }

        // Pictures waiting in the Import folder
        public static List<string> ImportFolderFiles()
        {
            var files = new List<string>();
            foreach (var pattern in new[] { "*.png", "*.jpg", "*.jpeg" })
                files.AddRange(Directory.GetFiles(Store.ImportFolder, pattern));
            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        // ---- Windows' Open dialog ----

        // The file picked in the dialog, handed over from the dialog's thread
        static string picked;
        static volatile bool dialogOpen;
        static volatile bool dialogDone;

        public static bool DialogOpen => dialogOpen;
        public static bool CanUseDialog => Application.platform == RuntimePlatform.WindowsPlayer;

        // Opens the dialog without stopping the game; the choice turns up in TakePicked
        public static void OpenDialog()
        {
            if (dialogOpen || !CanUseDialog) return;
            dialogOpen = true;
            dialogDone = false;
            var owner = GetActiveWindow();
            var thread = new Thread(() =>
            {
                try
                {
                    picked = ShowDialog(owner);
                }
                catch (Exception e)
                {
                    picked = null;
                    DecoratorPlugin.Log.LogWarning($"The Open dialog failed: {e.Message}");
                }
                dialogDone = true;
                dialogOpen = false;
            });
            thread.IsBackground = true;
            try
            {
                thread.SetApartmentState(ApartmentState.STA);
            }
            catch (Exception)
            {
                // Not every runtime lets the apartment be chosen; the dialog usually works anyway
            }
            thread.Start();
        }

        // The file picked since the last call, if the dialog has closed (null when it was cancelled)
        public static bool TakePicked(out string path)
        {
            path = null;
            if (!dialogDone) return false;
            dialogDone = false;
            path = picked;
            picked = null;
            return true;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        class OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public string lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool GetOpenFileNameW([In, Out] OpenFileName ofn);

        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();

        const int OFN_NOCHANGEDIR = 0x8;
        const int OFN_PATHMUSTEXIST = 0x800;
        const int OFN_FILEMUSTEXIST = 0x1000;
        const int OFN_EXPLORER = 0x80000;
        const int MaxPath = 4096;

        static string ShowDialog(IntPtr owner)
        {
            var buffer = Marshal.AllocHGlobal(MaxPath * 2);
            try
            {
                Marshal.Copy(new byte[MaxPath * 2], 0, buffer, MaxPath * 2);
                var ofn = new OpenFileName
                {
                    lStructSize = Marshal.SizeOf(typeof(OpenFileName)),
                    hwndOwner = owner,
                    lpstrFilter = "Pictures (*.png, *.jpg)\0*.png;*.jpg;*.jpeg\0\0",
                    nFilterIndex = 1,
                    lpstrFile = buffer,
                    nMaxFile = MaxPath,
                    lpstrInitialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    lpstrTitle = "Pick a picture for the wall",
                    Flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR,
                };
                if (!GetOpenFileNameW(ofn)) return null;
                var path = Marshal.PtrToStringUni(buffer);
                return string.IsNullOrEmpty(path) ? null : path;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
