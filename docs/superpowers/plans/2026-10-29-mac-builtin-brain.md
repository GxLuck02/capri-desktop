# Mac Built-in Brain Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** On an Apple-silicon Mac, "Set up her brain" downloads the official llama.cpp engine and the right model, verifies them, and runs `llama-server` on demand behind a guard (no orphan) and a per-launch secret key.

**Architecture:** `app/Engine.cs` (shared with Windows) becomes `partial` and gets tiny `#if CROSS` hooks; the Mac half lives in a new `cross/MacEngine.cs` (RAM via sysctl, tar.gz download + safe unpack, guarded launch, key file, stop, sweep). `app/Brain.cs` signs built-in requests with the key (`#if CROSS`). `Pet` owns one `Engine` (`BuiltIn`), picks Ollama → built-in → nothing, and runs the setup; `Mainframe` shows Set up / progress / Cancel.

**Tech Stack:** C# / .NET 10, Avalonia 11.3, `System.Formats.Tar`, `GZipStream`, `RandomNumberGenerator`, libc P/Invoke (`sysctlbyname`, `kill`), `/bin/sh` guard script. Tests: the `--smoke` step machine in `cross/Smoke.cs`, plus a new `--fake-brain` mode that impersonates `llama-server`.

Spec: `docs/superpowers/specs/2026-10-29-mac-builtin-brain-design.md`.

## Rules for this codebase

- **`app/*.cs` is also compiled on Windows by the .NET Framework 4.8 `csc.exe` (C# 5).** Any change there must be inside `#if CROSS` / `#else` / `#endif`, except adding the `partial` keyword. No `OperatingSystem`, `=>`, `$""`, `?.` outside `#if CROSS`.
- `cross/` is .NET 10 (any C#). Style: no `var`, `delegate { }` handlers, terse comments, match surrounding code.
- Build: `dotnet build cross/Capri.Cross.csproj -c Release` → `0 Erreur(s)` (French locale).
- Smoke (opens real windows ~2 min; **only the user runs it**, via `! cross/bin/Release/net10.0/Capri --smoke /tmp/capri-smoke; echo "exit $?"; cat /tmp/capri-smoke/smoke-test.txt`). Implementers: build only.

## Files

- Modify `app/Engine.cs` — `partial`, `#if CROSS` hooks in `Machine.Read`, `Server`, `Install`, `Ensure`, `Stop`.
- Create `cross/MacEngine.cs` — `partial class Machine` (Mac RAM) and `partial class Engine` (Mac install, unpack, launch, stop, sweep).
- Modify `app/Brain.cs` — `Complete` signs built-in requests (`#if CROSS`).
- Modify `cross/Program.cs` — `--fake-brain` mode.
- Modify `cross/Smoke.cs` — `FakeBrain`, fake download hub, tarball builder, steps 28-35.
- Modify `cross/Pet.cs` — `BuiltIn` engine, brain choice, setup, stops.
- Modify `cross/Mainframe.cs` — Set up her brain / progress / Cancel / Built-in status.

Decision vs spec: the eyes (mmproj) are **not** downloaded in step 2 (they'd cost 557 MB and ~0.6 GB of RAM for nothing until step 6). The offer shows model + engine size.

---

### Task 1: Mac engine install — RAM, download, safe unpack, resume

**Files:**
- Modify: `app/Engine.cs`
- Create: `cross/MacEngine.cs`
- Modify: `cross/Smoke.cs`

- [ ] **Step 1: Write the failing test**

In `cross/Smoke.cs`:

Add usings at the top (keep the existing ones):

```csharp
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
```

Add fields next to `private TcpListener fakeBrain;`:

```csharp
        private TcpListener hub;
        private readonly Dictionary<string, byte[]> hubFiles = new Dictionary<string, byte[]>();
        private readonly List<string> hubAsked = new List<string>();
        private Engine bench;
        private Pick testPick;
```

In `Tick`, change `if (now - started > 200)` to `if (now - started > 240)`.

Replace the whole `if (step == 28)` block with:

```csharp
            if (step == 28)
            {
                Mainframe frame = pet.Frame;
                if (pet.OllamaModels != null) return false;
                bool told = Engine.Supported ? frame.SetupShown && frame.TextStatus.IndexOf("Set up her brain", StringComparison.Ordinal) >= 0 : frame.OllamaShown && frame.TextStatus.IndexOf("ollama pull gemma4:e2b", StringComparison.Ordinal) >= 0;
                if (!told) throw new Exception("With no Ollama, the Mainframe didn't say how to get a brain: " + frame.TextStatus);
                checks.Add("PASS mainframe: Capri Mainframe opens from her menu, lists her Ollama models on NORMAL, connects and forgets an NVIDIA key on HIGHER, and says how to get a brain when Ollama is missing");
                fakeBrain.Stop();
                return true;
            }
            if (step == 29)
            {
                if (!Engine.Supported)
                {
                    checks.Add("INFO built-in brain: only on a Mac with Apple silicon, skipped");
                    pet.Frame.Close();
                    Send(shelf, "quit");
                    Finish(null);
                    return true;
                }
                if (Engine.Fit(Machine.Unified(8L << 30)) != Engine.Picks[1] || Engine.Fit(Machine.Unified(16L << 30)) != Engine.Picks[0] || Engine.Fit(Machine.Unified(4L << 30)) != Engine.Picks[2]) throw new Exception("The Mac brain pick is wrong for 4, 8 or 16 GB of RAM.");
                long ram = Machine.Read().Ram;
                if (ram < (4L << 30)) throw new Exception("Couldn't read the Mac's RAM: " + ram);
                string script = "#!/bin/sh\nexec '" + Environment.ProcessPath + "' --fake-brain \"$@\"\n";
                string top = "llama-" + Engine.Build + "/";
                byte[] good = Tarball(new[] { "dir", top, "" }, new[] { "file", top + "llama-server", script }, new[] { "file", top + "libggml.0.27.0.dylib", "lib" }, new[] { "link", top + "libggml.0.dylib", "libggml.0.27.0.dylib" });
                byte[] evil = Tarball(new[] { "file", top + "../../evil", "x" }, new[] { "file", top + "llama-server", script });
                byte[] sneaky = Tarball(new[] { "link", top + "escape", "../../evil" }, new[] { "file", top + "llama-server", script });
                byte[] model = new byte[9 << 20];
                new Random(7).NextBytes(model);
                testPick = new Pick("Test Brain", "test:1b", "capri/test", "main", "test-brain.gguf", Sha(model), model.Length);
                string tarName = Path.GetFileName(Engine.MacUrl);
                lock (hubFiles) hubFiles["test-brain.gguf"] = model;
                hub = Listen(ServeHub);
                Engine.Mirror = "http://127.0.0.1:" + ((IPEndPoint)hub.LocalEndpoint).Port + "/";
                string brainDir = Path.Combine(folder, "t", "brain");
                if (Directory.Exists(Path.Combine(folder, "t"))) Directory.Delete(Path.Combine(folder, "t"), true);
                bench = new Engine(Path.Combine(folder, "t"));
                foreach (byte[] bad in new[] { evil, sneaky })
                {
                    lock (hubFiles) hubFiles[tarName] = bad;
                    Engine.MacSha = Sha(bad);
                    Engine.MacSize = bad.Length;
                    string why = null;
                    try { bench.Install(testPick, delegate { }); }
                    catch (IOException error) { why = error.Message; }
                    if (why != Engine.Unsafe || File.Exists(Path.Combine(brainDir, "evil")) || Directory.Exists(Path.Combine(brainDir, "engine-" + Engine.Build)) || Directory.Exists(Path.Combine(brainDir, "engine-" + Engine.Build + ".new"))) throw new Exception("An engine archive reaching outside its folder wasn't refused: " + why);
                }
                lock (hubFiles) hubFiles[tarName] = good;
                Engine.MacSha = Sha(good);
                Engine.MacSize = good.Length;
                string modelPath = Path.Combine(brainDir, "models", "test-brain.gguf");
                Pick wrong = new Pick("Test Brain", "test:1b", "capri/test", "main", "test-brain.gguf", new string('0', 64), model.Length);
                string bad2 = null;
                try { bench.Install(wrong, delegate { }); }
                catch (IOException error) { bad2 = error.Message; }
                if (bad2 != "Test Brain failed its checksum. Try again." || File.Exists(modelPath) || File.Exists(modelPath + ".part")) throw new Exception("A model with the wrong checksum wasn't refused and deleted: " + bad2);
                string server = Path.Combine(brainDir, "engine-" + Engine.Build, "llama-server");
                string link = Path.Combine(brainDir, "engine-" + Engine.Build, "libggml.0.dylib");
                if (!File.Exists(server) || (File.GetUnixFileMode(server) & UnixFileMode.UserExecute) == 0 || new FileInfo(link).LinkTarget != "libggml.0.27.0.dylib") throw new Exception("The engine wasn't unpacked with its runnable server and library links.");
                bool cancelled = false;
                try { bench.Install(testPick, delegate(string text, long done, long total) { if (done < total) bench.Cancel = true; }); }
                catch (OperationCanceledException) { cancelled = true; }
                long kept = File.Exists(modelPath + ".part") ? new FileInfo(modelPath + ".part").Length : 0;
                if (!cancelled || kept < (4L << 20) || kept >= model.Length) throw new Exception("Cancel didn't stop the download and keep what came down: " + cancelled + " | " + kept);
                bench.Cancel = false;
                bench.Install(testPick, delegate { });
                bool resumed = false;
                lock (hubAsked) foreach (string asked in hubAsked) if (asked.StartsWith("test-brain.gguf from ", StringComparison.Ordinal)) resumed = true;
                if (!File.Exists(modelPath) || Engine.Hash(modelPath) != testPick.Sha256 || !resumed) throw new Exception("The download didn't pick up where it left off.");
                checks.Add("PASS brain install: a Mac picks Gemma 4 E2B for 8 GB, the engine and model download, pass their checksums and unpack with links, archives reaching outside their folder and a bad checksum are refused, and Cancel picks up where it left off");
                pet.Frame.Close();
                hub.Stop();
                Send(shelf, "quit");
                Finish(null);
                return true;
            }
```

Add these helpers to the `Smoke` class (next to `ServeBrain`):

```csharp
        // Stands in for GitHub and Hugging Face: serves hubFiles by name and honours "Range: bytes=N-".
        private void ServeHub(NetworkStream stream)
        {
            List<byte> read = new List<byte>();
            int end = ReadHead(stream, read);
            if (end < 0) return;
            string[] lines = Encoding.ASCII.GetString(read.ToArray(), 0, end).Split(new[] { "\r\n" }, StringSplitOptions.None);
            string name = lines[0].Split(' ')[1].TrimStart('/');
            long from = 0;
            foreach (string line in lines) if (line.StartsWith("Range: bytes=", StringComparison.OrdinalIgnoreCase)) from = long.Parse(line.Substring(13).TrimEnd('-'));
            lock (hubAsked) hubAsked.Add(name + (from > 0 ? " from " + from : ""));
            byte[] file;
            lock (hubFiles) hubFiles.TryGetValue(name, out file);
            if (file == null) { Answer(stream, 404, "text/plain", new byte[0]); return; }
            byte[] part = new byte[file.Length - from];
            Array.Copy(file, from, part, 0, part.Length);
            Answer(stream, from > 0 ? 206 : 200, "application/octet-stream", part);
        }

        // Each entry is { "dir" | "file" | "link", name, file text or link target }.
        private static byte[] Tarball(params string[][] entries)
        {
            using (MemoryStream raw = new MemoryStream())
            {
                using (GZipStream zip = new GZipStream(raw, CompressionLevel.Fastest, true))
                using (TarWriter tar = new TarWriter(zip, TarEntryFormat.Pax, false))
                    foreach (string[] entry in entries)
                    {
                        PaxTarEntry item = new PaxTarEntry(entry[0] == "dir" ? TarEntryType.Directory : entry[0] == "link" ? TarEntryType.SymbolicLink : TarEntryType.RegularFile, entry[1]);
                        item.Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
                        if (entry[0] == "link") item.LinkName = entry[2];
                        if (entry[0] == "file") item.DataStream = new MemoryStream(Encoding.UTF8.GetBytes(entry[2]));
                        tar.WriteEntry(item);
                    }
                return raw.ToArray();
            }
        }

        private static string Sha(byte[] data) { return Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant(); }
```

- [ ] **Step 2: Build to verify it fails**

Run: `dotnet build cross/Capri.Cross.csproj -c Release`
Expected: FAIL — `'Engine' does not contain a definition for 'Supported'` / `'Machine' does not contain a definition for 'Unified'` / `'Mainframe' does not contain a definition for 'SetupShown'` (and `Mirror`, `MacUrl`, `MacSha`, `MacSize`, `Unsafe`).

`SetupShown` is added in Task 3. To keep this task buildable, add this temporary stub to `cross/Mainframe.cs` next to `OllamaShown` now; Task 3 replaces it:

```csharp
        internal bool SetupShown { get { return false; } }
```

- [ ] **Step 3: Make `Engine` and `Machine` partial, hook `Read`, `Server` and `Install` (`app/Engine.cs`)**

Change `internal sealed class Machine` to `internal sealed partial class Machine` and `internal sealed class Engine` to `internal sealed partial class Engine`.

In `Machine.Read()`, make the first lines:

```csharp
        internal static Machine Read()
        {
#if CROSS
            if (!OperatingSystem.IsWindows()) return ReadUnix();
#endif
            Machine machine = new Machine();
```

Replace

```csharp
        private string Server { get { return Path.Combine(Folder, "llama-server.exe"); } }
```
with
```csharp
#if CROSS
        private string Server { get { return Path.Combine(Folder, OperatingSystem.IsWindows() ? "llama-server.exe" : "llama-server"); } }
#else
        private string Server { get { return Path.Combine(Folder, "llama-server.exe"); } }
#endif
```

In `Install`, make the first lines:

```csharp
        internal void Install(Pick pick, Action<string, long, long> progress)
        {
#if CROSS
            if (!OperatingSystem.IsWindows()) { InstallMac(pick, progress); return; }
#endif
            Directory.CreateDirectory(root);
```

- [ ] **Step 4: Create `cross/MacEngine.cs` (install half)**

```csharp
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
```

Note: `entry.Name` for the top folder is `llama-b11669/`, whose full path ends with a separator; the `target + separator` comparison accepts it. An entry equal to `into` itself is accepted (harmless).

- [ ] **Step 5: Build**

Run: `dotnet build cross/Capri.Cross.csproj -c Release` → `0 Erreur(s)`. Smoke: deferred to the user.

- [ ] **Step 6: Commit**

```bash
git add app/Engine.cs cross/MacEngine.cs cross/Mainframe.cs cross/Smoke.cs
git commit -m "a Mac downloads llama.cpp's own engine and her model, checks them and unpacks only what stays in its folder"
```

---

### Task 2: Guarded `llama-server` — secret key, no orphan, sweep, fake brain

**Files:**
- Modify: `app/Engine.cs` (`Ensure`, `Stop`)
- Modify: `cross/MacEngine.cs`
- Modify: `app/Brain.cs` (`Complete`)
- Modify: `cross/Program.cs`
- Modify: `cross/Smoke.cs`

- [ ] **Step 1: Write the failing test**

In `cross/Smoke.cs`, make `ReadHead` and `Answer` `internal static` instead of `private static` (FakeBrain reuses them).

Add fields next to `private Pick testPick;`:

```csharp
        private Process sleeper;
        private int guardedPid;
        private double killedAt;
```

At the end of step 29, replace

```csharp
                pet.Frame.Close();
                hub.Stop();
                Send(shelf, "quit");
                Finish(null);
                return true;
            }
```
(the one after the `PASS brain install` line) with
```csharp
                Engine.ServerName = Path.GetFileNameWithoutExtension(Environment.ProcessPath);
                bench.Ensure(testPick);
                string pids = Path.Combine(brainDir, "server.pid");
                string keyFile = Path.Combine(brainDir, "server.key");
                if (!bench.Running || bench.Key == null || bench.Key.Length != 64 || File.GetUnixFileMode(keyFile) != (UnixFileMode.UserRead | UnixFileMode.UserWrite) || !File.Exists(pids)) throw new Exception("Her brain didn't start with a private secret key and a PID file.");
                string url = bench.Address + "/v1/chat/completions";
                if (Post(url, null) != 401 || Post(url, "wrong") != 401 || Post(url, bench.Key) != 200) throw new Exception("Her brain answered without its secret key, or refused it.");
                int first = int.Parse(File.ReadAllText(pids).Trim());
                bench.Stop();
                if (bench.Running || bench.Key != null || File.Exists(keyFile) || File.Exists(pids) || Alive(first)) throw new Exception("Stopping her brain left it running or left its key behind.");
                sleeper = Process.Start("/bin/sleep", "60");
                bench.Parent = sleeper.Id;
                bench.Ensure(testPick);
                guardedPid = int.Parse(File.ReadAllText(pids).Trim());
                sleeper.Kill();
                sleeper.WaitForExit();
                killedAt = now;
                return true;
            }
            if (step == 30)
            {
                string brainDir = Path.Combine(folder, "t", "brain");
                string pids = Path.Combine(brainDir, "server.pid");
                if (Alive(guardedPid) || File.Exists(pids))
                {
                    if (now - killedAt > 3.5) throw new Exception("Her brain kept running " + (now - killedAt).ToString("0.0") + " s after Capri was gone.");
                    return false;
                }
                checks.Add("PASS brain guard: her brain needs its per-launch secret key, Stop removes the key, and when Capri dies her brain goes " + (now - killedAt).ToString("0.0") + " s later");
                bench.Parent = Environment.ProcessId;
                bench.Stop();
                Process stray = Process.Start(Environment.ProcessPath, "--fake-brain --port 0");
                File.WriteAllText(pids, stray.Id.ToString());
                bench.Sweep();
                if (!stray.WaitForExit(3000) || File.Exists(pids)) throw new Exception("A brain left over from a crash wasn't stopped at start.");
                checks.Add("PASS brain sweep: a brain left over from a crash is stopped when Capri starts");
                pet.Frame.Close();
                hub.Stop();
                Send(shelf, "quit");
                Finish(null);
                return true;
            }
```

Add helpers to `Smoke`:

```csharp
        private static bool Alive(int pid)
        {
            try { using (Process process = Process.GetProcessById(pid)) return !process.HasExited; }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
        }

        private static int Post(string url, string key)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Proxy = null;
            request.Timeout = 5000;
            if (key != null) request.Headers["Authorization"] = "Bearer " + key;
            byte[] body = Encoding.UTF8.GetBytes("{\"messages\":[]}");
            request.ContentLength = body.Length;
            using (Stream sent = request.GetRequestStream()) sent.Write(body, 0, body.Length);
            try { using (HttpWebResponse response = (HttpWebResponse)request.GetResponse()) return (int)response.StatusCode; }
            catch (WebException error)
            {
                HttpWebResponse response = error.Response as HttpWebResponse;
                if (response == null) throw;
                using (response) return (int)response.StatusCode;
            }
        }
```

Add this class at the end of `cross/Smoke.cs`, inside the namespace:

```csharp
    // Plays llama-server for the smoke test: /health, and chat that needs the --api-key-file key.
    internal static class FakeBrain
    {
        internal const string Line = "Built-in brain here, choom.";

        internal static int Run(string[] args)
        {
            int port = 0;
            string key = null;
            for (int i = 1; i + 1 < args.Length; i++)
            {
                if (args[i] == "--port") port = int.Parse(args[i + 1]);
                if (args[i] == "--api-key-file") key = File.ReadAllText(args[i + 1]).Trim();
            }
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            while (true)
            {
                using (TcpClient client = listener.AcceptTcpClient())
                {
                    try { Serve(client.GetStream(), key); }
                    catch (Exception) { }
                }
            }
        }

        private static void Serve(NetworkStream stream, string key)
        {
            List<byte> read = new List<byte>();
            int end = Smoke.ReadHead(stream, read);
            if (end < 0) return;
            string[] lines = Encoding.ASCII.GetString(read.ToArray(), 0, end).Split(new[] { "\r\n" }, StringSplitOptions.None);
            string path = lines[0].Split(' ')[1];
            string given = null;
            int length = 0;
            foreach (string line in lines)
            {
                if (line.StartsWith("Authorization: Bearer ", StringComparison.OrdinalIgnoreCase)) given = line.Substring(22).Trim();
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) length = int.Parse(line.Substring(15).Trim());
            }
            byte[] chunk = new byte[4096];
            while (read.Count - end < length)
            {
                int got = stream.Read(chunk, 0, chunk.Length);
                if (got <= 0) break;
                for (int i = 0; i < got; i++) read.Add(chunk[i]);
            }
            if (path == "/health") Smoke.Answer(stream, 200, "application/json", Encoding.UTF8.GetBytes("{\"status\":\"ok\"}"));
            else if (path.StartsWith("/v1/chat/completions", StringComparison.Ordinal) && (key == null || given != key)) Smoke.Answer(stream, 401, "application/json", Encoding.UTF8.GetBytes("{\"error\":{\"message\":\"Invalid API Key\"}}"));
            else if (path.StartsWith("/v1/chat/completions", StringComparison.Ordinal)) Smoke.Answer(stream, 200, "application/json", Encoding.UTF8.GetBytes("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"" + Line + "\"},\"finish_reason\":\"stop\"}]}"));
            else Smoke.Answer(stream, 404, "application/json", Encoding.UTF8.GetBytes("{}"));
        }
    }
```

In `cross/Program.cs` `Main`, as the very first line:

```csharp
            if (args.Length >= 1 && args[0] == "--fake-brain") return FakeBrain.Run(args);
```

- [ ] **Step 2: Build to verify it fails**

Run: `dotnet build cross/Capri.Cross.csproj -c Release`
Expected: FAIL — `'Engine' does not contain a definition for 'ServerName'` (and `Running`, `Key`, `Parent`, `Sweep`).

- [ ] **Step 3: Hook `Ensure` and `Stop` (`app/Engine.cs`)**

In `Ensure`, replace

```csharp
                server = Process.Start(StartInfo(pick, eyes, serverCpu, port, window));
                Lower(server);
                Bind(server);
```
with
```csharp
#if CROSS
                if (OperatingSystem.IsWindows())
                {
                    server = Process.Start(StartInfo(pick, eyes, serverCpu, port, window));
                    Lower(server);
                    Bind(server);
                }
                else server = Launch(StartInfo(pick, eyes, serverCpu, port, window));
#else
                server = Process.Start(StartInfo(pick, eyes, serverCpu, port, window));
                Lower(server);
                Bind(server);
#endif
```

In `Stop`, replace

```csharp
                if (server == null) return;
                try { if (!server.HasExited)
```
with
```csharp
#if CROSS
                if (!OperatingSystem.IsWindows()) { StopMac(); return; }
#endif
                if (server == null) return;
                try { if (!server.HasExited)
```

- [ ] **Step 4: Add the launch half to `cross/MacEngine.cs`**

Add usings `System.Collections.Generic`, `System.Diagnostics`, `System.Security.Cryptography`, `System.Text` to the file. Add to the `Engine` partial class:

```csharp
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
```

- [ ] **Step 5: Sign built-in requests (`app/Brain.cs`, `Complete`)**

In `Complete`, right after the line `CompletionRequest asking = new CompletionRequest { ... };` add:

```csharp
            Action<HttpWebRequest> sign = null;
#if CROSS
            Engine local = Local;
            string secret = local != null && address == local.Address ? local.Key : null;
            if (secret != null) sign = delegate(HttpWebRequest request) { request.Headers["Authorization"] = "Bearer " + secret; };
#endif
```

and in the two calls inside `Complete` only, change `Send<CompletionReply>(address + "/v1/chat/completions", asking, 180000, listed, null, parts, ticket)` to `Send<CompletionReply>(address + "/v1/chat/completions", asking, 180000, listed, sign, parts, ticket)`.

- [ ] **Step 6: Build**

Run: `dotnet build cross/Capri.Cross.csproj -c Release` → `0 Erreur(s)`. Smoke: deferred to the user.

- [ ] **Step 7: Commit**

```bash
git add app/Engine.cs app/Brain.cs cross/MacEngine.cs cross/Program.cs cross/Smoke.cs
git commit -m "her Mac brain runs under a guard that stops it when Capri goes, and only answers Capri's secret key"
```

---

### Task 3: Set up her brain in Pet and the Mainframe

**Files:**
- Modify: `cross/Pet.cs`
- Modify: `cross/Mainframe.cs`
- Modify: `cross/Smoke.cs`

- [ ] **Step 1: Write the failing test**

In `cross/Smoke.cs`, at the end of step 30 replace

```csharp
                pet.Frame.Close();
                hub.Stop();
                Send(shelf, "quit");
                Finish(null);
                return true;
            }
```
(the one after the `PASS brain sweep` line) with
```csharp
                Mainframe frame = pet.Frame;
                pet.SetupPick = testPick;
                frame.Render();
                if (!frame.SetupShown || frame.SetupStatus != "Test Brain · " + Engine.Amount(testPick.Size + Engine.MacSize)) throw new Exception("NORMAL with no brain didn't offer Set up her brain with its size: " + frame.SetupStatus + " | " + frame.TextStatus);
                frame.PressSetUp();
                if (!pet.SettingUp) throw new Exception("Set up her brain didn't start.");
                return true;
            }
            if (step == 31)
            {
                Mainframe frame = pet.Frame;
                if (pet.SettingUp) return false;
                if (pet.BrainProblem != null || pet.Brain.Local == null || pet.Brain.Model != "Test Brain" || !pet.BuiltIn.Running || !frame.TextStatus.StartsWith("tier 0 | Built-in · Test Brain | Running on this Mac", StringComparison.Ordinal) || frame.SetupShown) throw new Exception("Set up her brain didn't leave her on the built-in brain: " + pet.BrainProblem + " | " + frame.TextStatus);
                Snap(frame.Content as Control, "mainframe-builtin.png");
                pet.Think = null;
                pet.Hear("hi there");
                return true;
            }
            if (step == 32)
            {
                if (pet.Talk.Reply != FakeBrain.Line) return false;
                checks.Add("PASS set up her brain: NORMAL offers it with its size, it installs and starts her built-in brain, and she answers through it");
                pet.ChooseTier(1);
                if (pet.BuiltIn.Running || pet.Brain.Local != null) throw new Exception("HIGHER left her built-in brain running.");
                pet.ChooseTier(0);
                pet.UseBuiltIn(testPick);
                Brain.Ollama = FakeBrain();
                pet.FindBrain();
                return true;
            }
            if (step == 33)
            {
                if (pet.Brain.Local != null || pet.Brain.Model == null) return false;
                if (pet.Brain.Model != "qwen3:4b" || pet.BuiltIn.Running) throw new Exception("With Ollama models, she didn't move off her built-in brain: " + pet.Brain.Model);
                checks.Add("PASS brain choice: HIGHER stops her built-in brain, and Ollama models come first like on Windows");
                fakeBrain.Stop();
                pet.Frame.Close();
                hub.Stop();
                Send(shelf, "quit");
                Finish(null);
                return true;
            }
```

- [ ] **Step 2: Build to verify it fails**

Run: `dotnet build cross/Capri.Cross.csproj -c Release`
Expected: FAIL — `'Pet' does not contain a definition for 'SetupPick'` (and `SettingUp`, `BuiltIn`, `UseBuiltIn`; `'Mainframe'` lacks `SetupStatus`, `PressSetUp`).

- [ ] **Step 3: Pet — engine, offer, setup (`cross/Pet.cs`)**

Replace the `Offline` constant with:

```csharp
        internal static readonly string Offline = Engine.Supported ? "My brain's offline. Open Capri Mainframe and press Set up her brain, or press Free key there for a free NVIDIA key." : "My brain's offline. Install Ollama from ollama.com, then run: ollama pull gemma4:e2b, or open Capri Mainframe for a free NVIDIA key.";
```

Next to `private Mainframe mainframe;` add:

```csharp
        private Engine builtIn;
        internal Pick SetupPick;
        internal bool SettingUp;
        internal string SetupText;
```

In the constructor, just before `Saved.Tier = Math.Max(0, Math.Min(2, Saved.Tier));` add:

```csharp
            builtIn = new Engine(Program.Smoke ?? folder);
            if (Engine.Supported) builtIn.Sweep();
```

Add these members just before `internal void FindBrain()`:

```csharp
        internal Engine BuiltIn { get { return builtIn; } }

        internal bool CanSetUp { get { return Engine.Supported || ollamaModels != null; } }

        private Pick Offer { get { return SetupPick ?? Engine.Fit(Machine.Read()); } }

        internal string OfferText
        {
            get
            {
                Pick pick = Offer;
                bool engine = ollamaModels == null && Engine.Supported && !builtIn.HasServer;
                return pick.Name + " · " + Engine.Amount(pick.Size + (engine ? Engine.MacSize : 0));
            }
        }

        internal void UseBuiltIn(Pick pick)
        {
            Brain.Cancel();
            Brain.LocalPick = pick;
            Brain.Local = builtIn;
            Brain.Model = pick.Name;
            Brain.Forget();
        }

        private void DropBuiltIn()
        {
            if (Brain.Local != null) { Brain.Local = null; Brain.Model = null; }
            builtIn.Stop();
        }

        // Ported from Windows' SetUpBrain: Ollama pulls it if it's running, otherwise the built-in engine installs it.
        internal void SetUpBrain()
        {
            if (SettingUp || !CanSetUp) return;
            SettingUp = true;
            builtIn.Cancel = false;
            Pick pick = Offer;
            bool viaOllama = ollamaModels != null;
            SetupText = "Starting download";
            BrainProblem = null;
            ShowState();
            Stopwatch since = Stopwatch.StartNew();
            long shownAt = -1000;
            ThreadPool.QueueUserWorkItem(delegate
            {
                Action<string, long, long> progress = delegate(string text, long done, long total)
                {
                    long now = since.ElapsedMilliseconds;
                    if (now - Interlocked.Read(ref shownAt) < 200 && done < total) return;
                    Interlocked.Exchange(ref shownAt, now);
                    string label = (viaOllama ? "Downloading " + pick.Name : text) + (total > 0 ? " · " + Engine.Amount(done) + " / " + Engine.Amount(total) : "");
                    Dispatcher.UIThread.Post(delegate { if (SettingUp) { SetupText = label; ShowState(); } });
                };
                try
                {
                    if (viaOllama) Brain.Pull(pick.Ollama, progress, delegate { return builtIn.Cancel; });
                    else builtIn.Install(pick, progress);
                    Dispatcher.UIThread.Post(delegate { SetupText = "Loading " + pick.Name; ShowState(); });
                    if (!viaOllama) builtIn.Ensure(pick);
                    Dispatcher.UIThread.Post(delegate
                    {
                        SettingUp = false;
                        SetupText = null;
                        if (Brain.Remote != null) { builtIn.Stop(); ShowState(); return; }
                        if (viaOllama) { DropBuiltIn(); Saved.Model = pick.Ollama; Save(); FindBrain(); }
                        else UseBuiltIn(pick);
                        ShowState();
                        Reply(++asking, BrainHeard, "Brain online. I'm thinking with " + pick.Name + " on this " + (OperatingSystem.IsMacOS() ? "Mac" : "computer") + " now.");
                    });
                }
                catch (OperationCanceledException) { Dispatcher.UIThread.Post(delegate { SettingUp = false; SetupText = null; ShowProblem("Cancelled. It picks up where it left off."); }); }
                catch (Exception error)
                {
                    string problem = Brain.Brief(error.Message) ?? (error.Message.Length > 70 ? error.Message.Substring(0, 67) + "..." : error.Message);
                    Dispatcher.UIThread.Post(delegate { SettingUp = false; SetupText = null; ShowProblem(problem); });
                }
            });
        }

        internal void CancelSetup() { builtIn.Cancel = true; }
```

- [ ] **Step 4: Pet — brain choice and stops**

Replace the body of `FindBrain` with:

```csharp
        internal void FindBrain()
        {
            Cloud remote = Brain.Remote;
            if (remote != null) { ListCloud(remote.Key, false); return; }
            string saved = Saved.Model;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string model = null;
                try { model = Brain.PickModel(Brain.InstalledModels(), saved, null); }
                catch (Exception) { }
                Pick local = model == null && Engine.Supported ? builtIn.Installed() : null;
                Dispatcher.UIThread.Post(delegate
                {
                    if (Brain.Remote != null || SettingUp) return;
                    // Ollama's models come first, like on Windows.
                    if (model != null && Brain.Local != null) DropBuiltIn();
                    if (Brain.Model != null) return;
                    if (model != null)
                    {
                        Brain.Model = model;
                        Saved.Model = model;
                        Save();
                    }
                    else if (local != null) UseBuiltIn(local);
                    else return;
                    ShowState();
                });
            });
        }
```

In `PickBrain`, after the first line (`if (string.IsNullOrEmpty(model) || model == Brain.Model) return;`) add:

```csharp
            if (Brain.Local != null) DropBuiltIn();
```

In `ChooseTier`, right after `Brain.Remote = TierCloud();` add `DropBuiltIn();` — then the next existing line (`Brain.Model = Brain.Remote == null ? null : Brain.Remote.Model;`) still sets the model.

In `ListCloud`, right after `Brain.Remote = remote;` add `DropBuiltIn();`.

In `Quit`, inside the `GlitchOut` callback, before `Save();` add `builtIn.Stop();`. In `Finish`, before `frames.Stop();` add `builtIn.Stop();`.

- [ ] **Step 5: Mainframe — Set up her brain row (`cross/Mainframe.cs`)**

Delete the temporary `SetupShown` stub from Task 1. Add fields next to `ollama`:

```csharp
        private readonly Button setUp = Make("Set up her brain");
        private readonly Button cancel = Make("Cancel");
        private readonly TextBlock setupNote = Line(10.5, Look.Dim, false);
        private StackPanel setupRow;
```

In the constructor, just before `ollama.Click += ...`, add:

```csharp
            setUp.Click += delegate { pet.SetUpBrain(); };
            cancel.Click += delegate { pet.CancelSetup(); };
            setupRow = Row(setUp, cancel);
            page.Children.Add(setupRow);
            page.Children.Add(setupNote);
```

In `Render`, replace

```csharp
            else
            {
                brainName.Text = "No brain yet";
                brainNote.Text = "Install Ollama, then run: ollama pull gemma4:e2b";
            }
```
with
```csharp
            else if (pet.Brain.Local != null)
            {
                brainName.Text = "Built-in · " + pet.Brain.Model;
                brainNote.Text = "Running on this " + (OperatingSystem.IsMacOS() ? "Mac" : "computer");
            }
            else
            {
                brainName.Text = "No brain yet";
                brainNote.Text = Engine.Supported ? "Press Set up her brain, or install Ollama." : "Install Ollama, then run: ollama pull gemma4:e2b";
            }
```

and replace

```csharp
            ollama.IsVisible = remote == null && (local == null || local.Length == 0);
```
with
```csharp
            bool empty = remote == null && (local == null || local.Length == 0) && pet.Brain.Local == null;
            setupRow.IsVisible = pet.SettingUp || (empty && pet.CanSetUp);
            setUp.IsVisible = !pet.SettingUp;
            cancel.IsVisible = pet.SettingUp;
            setupNote.IsVisible = setupRow.IsVisible;
            setupNote.Text = !setupRow.IsVisible ? "" : pet.SettingUp ? pet.SetupText : pet.OfferText;
            ollama.IsVisible = empty && !pet.SettingUp && !Engine.Supported;
```

Add accessors next to `OllamaShown`:

```csharp
        internal bool SetupShown { get { return setupRow.IsVisible && setUp.IsVisible; } }
        internal string SetupStatus { get { return setupNote.Text; } }
        internal void PressSetUp() { pet.SetUpBrain(); }
```

- [ ] **Step 6: Build**

Run: `dotnet build cross/Capri.Cross.csproj -c Release` → `0 Erreur(s)`. Smoke: deferred to the user.

- [ ] **Step 7: Commit**

```bash
git add cross/Pet.cs cross/Mainframe.cs cross/Smoke.cs
git commit -m "Set up her brain on a Mac: one click gives her a built-in brain, and Ollama still comes first"
```

---

### Task 4: Smoke run, manual check on the M2, wrap up

- [ ] **Step 1: The user runs the smoke test**

```
! cd ~/Documents/projects/capri/capri-desktop && cross/bin/Release/net10.0/Capri --smoke /tmp/capri-smoke; echo "exit $?"; grep -E "FAIL|brain|mainframe" /tmp/capri-smoke/smoke-test.txt
```
Expected: `exit 0`, PASS lines `brain install`, `brain guard`, `brain sweep`, `set up her brain`, `brain choice`. Look at `/tmp/capri-smoke/mainframe-builtin.png`.

- [ ] **Step 2: Manual check (the user)**

1. `brew services stop ollama`, launch Capri, Mainframe → NORMAL shows **Set up her brain · Gemma 4 E2B · 3.1 GB**.
2. Press it: progress moves; press Cancel midway → "Cancelled…"; press again → resumes.
3. "Brain online…", then chat: time the first reply and a later one.
4. Activity Monitor: one `llama-server`. Quit Capri → it's gone.
5. Relaunch, chat, then `! pkill -9 -f "net10.0/Capri"` → `llama-server` gone within ~2 s.
6. `brew services start ollama`, relaunch → she's on Ollama again (Ollama first).

- [ ] **Step 3: Update `docs/mac-parity-todo.md`** (step 2 done, step 3 next) and commit.
