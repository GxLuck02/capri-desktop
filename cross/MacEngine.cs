using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace CapriDesktop
{
    internal sealed partial class Machine
    {
        [DllImport("libc")] private static extern int sysctlbyname(string name, out long value, ref IntPtr size, IntPtr newValue, IntPtr newSize);

        private static Machine ReadUnix()
        {
            long ram = 0;
            IntPtr size = (IntPtr)8;
            if (!OperatingSystem.IsMacOS() || sysctlbyname("hw.memsize", out ram, ref size, IntPtr.Zero, IntPtr.Zero) != 0) ram = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            return Unified(ram);
        }

        // A Mac's GPU shares the RAM; macOS lets Metal use about two thirds of it, which is what Fit compares.
        internal static Machine Unified(long ram)
        {
            return new Machine { Ram = ram, Vram = ram / 3 * 2, Gpu = OperatingSystem.IsMacOS() ? "Apple GPU" : null };
        }
    }

    // The Mac half of Engine: the official llama.cpp build for Apple silicon, unpacked with care.
    internal sealed partial class Engine
    {
        internal const string Unsafe = "The engine download has files that reach outside its folder.";
        internal static string MacUrl = "https://github.com/ggml-org/llama.cpp/releases/download/" + Build + "/llama-" + Build + "-bin-macos-arm64.tar.gz";
        internal static string MacSha = "dbed55493d72bc1ba2babc8770d669c2289b5bc4ee9fb2a4dfc158d45b7d13fd";
        internal static long MacSize = 12191582;
        internal static string Mirror;

        internal static bool Supported { get { return OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64; } }

        internal bool HasServer { get { return File.Exists(Server); } }

        private static string Mirrored(string url) { return Mirror == null ? url : Mirror + url.Substring(url.LastIndexOf('/') + 1); }

        private void InstallMac(Pick pick, Action<string, long, long> progress)
        {
            if (!Supported) throw new IOException("The built-in brain needs a Mac with Apple silicon. Install Ollama instead.");
            Directory.CreateDirectory(root);
            long needed = (File.Exists(Server) ? 0 : MacSize * 4) + (File.Exists(ModelPath(pick)) ? 0 : pick.Size) + (256L << 20);
            if (new DriveInfo(Path.GetPathRoot(root)).AvailableFreeSpace < needed) throw new IOException("Storage too full: her brain needs " + Amount(needed) + " free.");
            if (!File.Exists(Server))
            {
                string tarball = Path.Combine(root, "engine-" + Build + ".tar.gz");
                Fetch(Mirrored(MacUrl), tarball, MacSize, MacSha, "the engine", progress, delegate { return Cancel; });
                string staging = Folder + ".new";
                try
                {
                    if (Directory.Exists(staging)) Directory.Delete(staging, true);
                    Unpack(tarball, staging);
                    string unpacked = Path.Combine(staging, "llama-" + Build);
                    string server = Path.Combine(unpacked, "llama-server");
                    if (!File.Exists(server)) throw new IOException("The engine download is missing llama-server.");
                    File.SetUnixFileMode(server, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                    if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
                    Directory.Move(unpacked, Folder);
                }
                finally
                {
                    if (Directory.Exists(staging)) Directory.Delete(staging, true);
                    File.Delete(tarball);
                }
            }
            if (!File.Exists(ModelPath(pick)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ModelPath(pick)));
                Fetch(Mirrored(pick.Url), ModelPath(pick), pick.Size, pick.Sha256, pick.Name, progress, delegate { return Cancel; });
            }
        }

        // Only folders, files, and links to a file beside them; nothing may land outside "into".
        private static void Unpack(string tarball, string into)
        {
            string inside = Path.GetFullPath(into) + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(into);
            using (FileStream file = File.OpenRead(tarball))
            using (GZipStream zip = new GZipStream(file, CompressionMode.Decompress))
            using (TarReader tar = new TarReader(zip))
            {
                TarEntry entry;
                while ((entry = tar.GetNextEntry()) != null)
                {
                    string target = Path.GetFullPath(Path.Combine(into, entry.Name));
                    if (Path.IsPathRooted(entry.Name) || !(target + Path.DirectorySeparatorChar).StartsWith(inside, StringComparison.Ordinal)) throw new IOException(Unsafe);
                    if (entry.EntryType == TarEntryType.Directory) Directory.CreateDirectory(target);
                    else if (entry.EntryType == TarEntryType.RegularFile || entry.EntryType == TarEntryType.V7RegularFile)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        entry.ExtractToFile(target, true);
                    }
                    else if (entry.EntryType == TarEntryType.SymbolicLink)
                    {
                        string link = entry.LinkName ?? "";
                        if (link.Length == 0 || link.IndexOf('/') >= 0 || link == "." || link == "..") throw new IOException(Unsafe);
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        File.CreateSymbolicLink(target, link);
                    }
                    else throw new IOException(Unsafe);
                }
            }
        }
    }
}
