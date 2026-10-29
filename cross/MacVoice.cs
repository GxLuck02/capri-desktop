using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CapriDesktop
{
    // The Mac half of her free voice: Windows' voice pack for the model, sherpa-onnx's own Mac build for the code.
    internal sealed partial class LocalVoice
    {
        internal const string Unsafe = "The voice download has files that reach outside its folder.";
        internal static string LibsUrl = "https://github.com/k2-fsa/sherpa-onnx/releases/download/v1.13.8/sherpa-onnx-v1.13.8-osx-arm64-shared-lib.tar.bz2";
        internal static string LibsSha = "ae77050cdae565496059d96f5ab33d77b397a0864282a4e4a26e3b3b3effb948";
        internal static long LibsSize = 8773198;
        private static readonly string[] Libraries = { "libsherpa-onnx-c-api.dylib", "libonnxruntime.dylib" };
        private static string library;
        private static bool resolving;

        internal static bool Supported { get { return Engine.Supported; } }

        private static bool CompleteMac(string where)
        {
            foreach (string name in VoicePack.Needed) if (!name.EndsWith(".dll", StringComparison.Ordinal) && !File.Exists(Path.Combine(where, name))) return false;
            foreach (string name in Libraries) if (!File.Exists(Path.Combine(where, "lib", name))) return false;
            return true;
        }

        private void InstallMac(Action<string, long, long> progress)
        {
            if (!Supported) throw new IOException("Free voice needs a Mac with Apple silicon.");
            string root = Path.GetDirectoryName(folder);
            Directory.CreateDirectory(root);
            long needed = PackSize * 2 + LibsSize * 4 + (64L << 20);
            if (new DriveInfo(Path.GetPathRoot(root)).AvailableFreeSpace < needed) throw new IOException("Her free voice needs " + Engine.Amount(needed) + " free.");
            string zip = folder + ".zip";
            string libs = folder + "-libs.tar.bz2";
            if (!File.Exists(zip)) Engine.Fetch(Engine.Mirrored(PackUrl), zip, PackSize, PackSha, "her free voice", progress, delegate { return Cancel; });
            if (!File.Exists(libs)) Engine.Fetch(Engine.Mirrored(LibsUrl), libs, LibsSize, LibsSha, "her voice engine", progress, delegate { return Cancel; });
            progress("Unpacking her free voice", PackSize, PackSize);
            string staging = folder + ".new";
            string loose = folder + ".libs";
            try
            {
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
                if (Directory.Exists(loose)) Directory.Delete(loose, true);
                Unzip(zip, staging);
                Untar(libs, loose, Path.Combine(staging, "lib"));
                if (!CompleteMac(staging)) throw new IOException("The voice download is missing files.");
                lock (gate)
                {
                    Close();
                    if (Directory.Exists(folder)) Directory.Delete(folder, true);
                    Directory.Move(staging, folder);
                }
            }
            catch (Exception)
            {
                File.Delete(zip);
                File.Delete(libs);
                throw;
            }
            finally
            {
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
                if (Directory.Exists(loose)) Directory.Delete(loose, true);
            }
            File.Delete(zip);
            File.Delete(libs);
        }

        // The model and its data only: Windows' DLLs stay in the zip, and nothing may land outside "into".
        private static void Unzip(string zip, string into)
        {
            string inside = Path.GetFullPath(into) + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(into);
            using (ZipArchive archive = ZipFile.OpenRead(zip))
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string target = Path.GetFullPath(Path.Combine(into, entry.FullName));
                    if (Path.IsPathRooted(entry.FullName) || !(target + Path.DirectorySeparatorChar).StartsWith(inside, StringComparison.Ordinal)) throw new IOException(Unsafe);
                    if (entry.FullName.EndsWith("/", StringComparison.Ordinal)) { Directory.CreateDirectory(target); continue; }
                    if (entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    entry.ExtractToFile(target, true);
                }
        }

        // .NET can't read bzip2, so macOS's tar unpacks it (it refuses ".." and absolute paths), then only plain files may come out.
        private static void Untar(string tarball, string loose, string lib)
        {
            Directory.CreateDirectory(loose);
            ProcessStartInfo info = new ProcessStartInfo("/usr/bin/tar");
            info.UseShellExecute = false;
            info.RedirectStandardError = true;
            foreach (string part in new[] { "-xjf", tarball, "-C", loose }) info.ArgumentList.Add(part);
            using (Process tar = Process.Start(info))
            {
                tar.StandardError.ReadToEnd();
                tar.WaitForExit();
                if (tar.ExitCode != 0) throw new IOException("The voice engine download wouldn't unpack.");
            }
            foreach (string path in Directory.EnumerateFileSystemEntries(loose, "*", SearchOption.AllDirectories))
            {
                FileSystemInfo item = new FileInfo(path);
                if (item.LinkTarget != null) throw new IOException(Unsafe);
            }
            Directory.CreateDirectory(lib);
            foreach (string name in Libraries)
            {
                string[] found = Directory.GetFiles(loose, name, SearchOption.AllDirectories);
                if (found.Length != 1) throw new IOException("The voice engine download is missing " + name + ".");
                File.Copy(found[0], Path.Combine(lib, name));
            }
        }

        // Windows names the library sherpa-onnx-c-api.dll; on a Mac that name resolves to the dylib in her pack.
        private static void LoadMac(string where)
        {
            lock (loading)
            {
                string path = Path.Combine(where, "lib", "libsherpa-onnx-c-api.dylib");
                library = path;
                if (!resolving)
                {
                    NativeLibrary.SetDllImportResolver(typeof(LocalVoice).Assembly, delegate(string name, Assembly assembly, DllImportSearchPath? search) { return name == "sherpa-onnx-c-api.dll" && library != null ? NativeLibrary.Load(library) : IntPtr.Zero; });
                    resolving = true;
                }
                if (loaded) return;
                try { NativeLibrary.Load(path); }
                catch (DllNotFoundException) { throw new VoiceException("Her free voice wouldn't start. Press Get free voice again."); }
                catch (BadImageFormatException) { throw new VoiceException("Her free voice wouldn't start. Press Get free voice again."); }
                loaded = true;
            }
        }
    }
}
