using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

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

        internal static string ServerName = "llama-server";
        internal int Parent = Environment.ProcessId;
        internal string Key { get; private set; }

        [DllImport("libc", SetLastError = true)] private static extern int kill(int pid, int signal);

        // $1 Capri's PID, $2 PID file, $3 key file, then the server command. Nothing from a download is pasted in here.
        private const string Guard =
            "capri=$1; pids=$2; key=$3; shift 3\n" +
            "\"$@\" &\n" +
            "child=$!\n" +
            "echo $child > \"$pids\"\n" +
            "finish() { kill $child 2>/dev/null; wait $child 2>/dev/null; rm -f \"$pids\" \"$key\"; exit 0; }\n" +
            "trap finish TERM INT HUP\n" +
            "while kill -0 $capri 2>/dev/null && kill -0 $child 2>/dev/null; do sleep 2 & wait $!; done\n" +
            "finish\n";

        private string PidPath { get { return Path.Combine(root, "server.pid"); } }
        private string KeyPath { get { return Path.Combine(root, "server.key"); } }

        internal bool Running { get { lock (gate) return server != null && !server.HasExited; } }

        // Runs llama-server under a small sh guard that stops it within 2 s of Capri going away, however she goes.
        private Process Launch(ProcessStartInfo plain)
        {
            string secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            FileStreamOptions options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write, UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite };
            using (FileStream stream = new FileStream(KeyPath, options))
            {
                File.SetUnixFileMode(stream.SafeFileHandle, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                using (StreamWriter writer = new StreamWriter(stream)) writer.Write(secret);
            }
            Key = secret;
            ProcessStartInfo info = new ProcessStartInfo("/bin/sh");
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.WorkingDirectory = plain.WorkingDirectory;
            foreach (string part in new[] { "-c", Guard, "capri-guard", Parent.ToString(), PidPath, KeyPath, "/usr/bin/nice", "-n", "10", plain.FileName }) info.ArgumentList.Add(part);
            foreach (string part in Split(plain.Arguments)) info.ArgumentList.Add(part);
            info.ArgumentList.Add("--api-key-file");
            info.ArgumentList.Add(KeyPath);
            return Process.Start(info);
        }

        // StartInfo's argument string: spaces split it, double quotes group.
        private static List<string> Split(string line)
        {
            List<string> parts = new List<string>();
            StringBuilder part = new StringBuilder();
            bool quoted = false;
            bool any = false;
            foreach (char c in line)
            {
                if (c == '"') { quoted = !quoted; any = true; }
                else if (c == ' ' && !quoted) { if (any) parts.Add(part.ToString()); part.Clear(); any = false; }
                else { part.Append(c); any = true; }
            }
            if (any) parts.Add(part.ToString());
            return parts;
        }

        // Called with gate held. TERM lets the guard stop the server; SIGKILL is the fallback, then Sweep.
        private void StopMac()
        {
            Process guard = server;
            server = null;
            Key = null;
            if (guard != null)
            {
                try
                {
                    if (!guard.HasExited)
                    {
                        kill(guard.Id, 15);
                        if (!guard.WaitForExit(5000)) guard.Kill();
                    }
                }
                catch (InvalidOperationException) { }
                guard.Dispose();
            }
            Sweep();
        }

        // A server left behind by a crash: stop it if its PID still is llama-server, and forget its key.
        internal void Sweep()
        {
            try
            {
                if (File.Exists(PidPath))
                {
                    int pid;
                    if (int.TryParse(File.ReadAllText(PidPath).Trim(), out pid))
                    {
                        try
                        {
                            using (Process old = Process.GetProcessById(pid))
                                if (!old.HasExited && old.ProcessName == ServerName) { old.Kill(); old.WaitForExit(3000); }
                        }
                        catch (ArgumentException) { }
                        catch (InvalidOperationException) { }
                    }
                    File.Delete(PidPath);
                }
                if (File.Exists(KeyPath)) File.Delete(KeyPath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
