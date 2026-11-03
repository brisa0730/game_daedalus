using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace Orcapafea.Daedalus.Editor
{
    /// <summary>
    /// Writes the Share zip the class gallery accepts (Documents/GDD/platforms.md § Share zip; checked by
    /// Deploy/gallery/src/share_zip.rs): daedalus.json, screenshot.png and the four Brotli build files, at the zip's root.
    /// </summary>
    internal static class ShareZip
    {
        /// <summary>The build's output folder name. Unity names the build files after it, and the gallery only accepts
        /// <c>[A-Za-z0-9_-]{1,64}</c>, so it is fixed rather than taken from the title.</summary>
        internal const string BuildName = "game";

        /// <summary>The gallery's <c>max_metadata_bytes</c> (Deploy/gallery/src/config.rs).</summary>
        const int MaxMetadataBytes = 4096;

        static readonly string[] BuildFiles =
        {
            BuildName + ".loader.js",
            BuildName + ".data.br",
            BuildName + ".framework.js.br",
            BuildName + ".wasm.br",
        };

        [Serializable]
        sealed class Metadata
        {
            public int format = 1;
            public string title;
        }

        /// <summary>True when the build folder holds all four files the zip needs.</summary>
        internal static bool HasBuildFiles(string buildDir)
        {
            foreach (string file in BuildFiles)
            {
                if (!File.Exists(Path.Combine(buildDir, "Build", file)))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Writes the zip to <paramref name="zipPath"/> and returns its size. When it would be over
        /// <paramref name="maxBytes"/>, nothing at <paramref name="zipPath"/> changes (an earlier good zip stays) and the
        /// oversize length is returned for the caller's message.
        /// </summary>
        internal static long Write(string zipPath, string cleanTitle, byte[] screenshotPng, string buildDir, long maxBytes)
        {
            byte[] metadata = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(new Metadata { title = cleanTitle }));
            if (metadata.Length > MaxMetadataBytes)
                throw new ArgumentException($"daedalus.json is {metadata.Length} bytes, over {MaxMetadataBytes}");

            string partial = zipPath + ".partial";
            try
            {
                using (var stream = new FileStream(partial, FileMode.Create, FileAccess.Write))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    // Entry names are spelled with '/', never Path.Combine: the gallery rejects a backslash. The Brotli files and
                    // the PNG are already compressed; NoCompression keeps Mono from spending time on them (it still writes
                    // them as level-0 Deflate, which the gallery accepts).
                    Add(zip, "daedalus.json", metadata, CompressionLevel.Optimal);
                    Add(zip, "screenshot.png", screenshotPng, CompressionLevel.NoCompression);
                    Add(zip, "Build/" + BuildFiles[0], ReadBuildFile(buildDir, BuildFiles[0]), CompressionLevel.Optimal);
                    for (int i = 1; i < BuildFiles.Length; i++)
                        Add(zip, "Build/" + BuildFiles[i], ReadBuildFile(buildDir, BuildFiles[i]), CompressionLevel.NoCompression);
                }

                long length = new FileInfo(partial).Length;
                if (length > maxBytes)
                {
                    File.Delete(partial);
                    return length;
                }
                // Replace swaps in one step, so a failure can't lose the earlier zip.
                if (File.Exists(zipPath))
                    File.Replace(partial, zipPath, null);
                else
                    File.Move(partial, zipPath);
                return length;
            }
            catch
            {
                if (File.Exists(partial))
                    File.Delete(partial);
                throw;
            }
        }

        static byte[] ReadBuildFile(string buildDir, string file)
        {
            return File.ReadAllBytes(Path.Combine(buildDir, "Build", file));
        }

        static void Add(ZipArchive zip, string name, byte[] bytes, CompressionLevel level)
        {
            using (Stream entry = zip.CreateEntry(name, level).Open())
                entry.Write(bytes, 0, bytes.Length);
        }
    }
}
