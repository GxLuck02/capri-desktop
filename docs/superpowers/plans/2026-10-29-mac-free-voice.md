# Mac Free Voice (VOICE section) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** On an Apple-silicon Mac, Capri speaks with Windows' free Kokoro voice (sherpa-onnx), installed in one click, and the Mainframe gets Windows' VOICE section (OFF / FREE VOICE / ELEVENLABS).

**Architecture:** `LocalVoice` in `app/Voice.cs` (shared with Windows) is enabled under `CROSS` and made `partial`, with `#if CROSS` hooks in `Install`, `Complete` and `Load`; its Mac half lives in a new `cross/MacVoice.cs` (download of Windows' voice pack + sherpa-onnx macOS libs, safe unzip, bz2 via `/usr/bin/tar`, dylib loading through a `DllImport` resolver). `Pet` gets a voice tier, the free-voice speaking path and the setup; `Mainframe` gets the VOICE section.

**Tech Stack:** C# / .NET 10, Avalonia 11.3, `System.IO.Compression.ZipArchive`, `/usr/bin/tar` and `/usr/bin/bzip2` (no shell), `NativeLibrary`, sherpa-onnx 1.13.8 C API. Tests: the `--smoke` step machine in `cross/Smoke.cs` with the fake download hub from step 2.

Spec: `docs/superpowers/specs/2026-10-29-mac-free-voice-design.md`.

## Rules for this codebase

- **`app/*.cs` is also compiled on Windows by .NET Framework 4.8 `csc.exe` (C# 5).** Changes there go inside `#if CROSS` / `#else` / `#endif`, except adding `partial` and moving a `using` that Windows already had. Nothing modern outside `#if CROSS`.
- `cross/` is .NET 10. Style: no `var`, `delegate { }` handlers, terse comments, match surrounding code.
- Build: `dotnet build cross/Capri.Cross.csproj -c Release` → `0 Erreur(s)`.
- Smoke opens real windows: **only the user runs it**. Implementers: build only.

## Files

- Modify `app/Voice.cs` — enable `FreeStyle`/`LocalVoice` under CROSS, `partial`, hooks.
- Create `cross/MacVoice.cs` — `LocalVoice` Mac half.
- Modify `cross/MacEngine.cs` — `Mirrored` becomes `internal`.
- Modify `cross/Pet.cs` — voice tier, free voice speaking, setup, unload after 15 min.
- Modify `cross/Mainframe.cs` — VOICE section.
- Modify `cross/Smoke.cs` — cleanup, fake voice files, steps 33-40.

Decision vs spec: the sherpa-onnx archive genuinely contains an extra `libsherpa-onnx-cxx-api.dylib`, so an extra file is **not** refused; only links and paths leaving the folder are. Spec test 2 is read that way.

---

### Task 1: Install her free voice on a Mac (download, safe unpack, dylib loading)

**Files:**
- Modify: `app/Voice.cs`
- Create: `cross/MacVoice.cs`
- Modify: `cross/MacEngine.cs`
- Modify: `cross/Smoke.cs`

- [ ] **Step 1: Write the failing test**

In `cross/Smoke.cs`:

In the constructor, after `pet.ForgetVoice();`, add (left-overs of an earlier run would fake an installed brain or voice):

```csharp
            foreach (string old in new[] { "brain", "voice", "t", "tv" }) if (Directory.Exists(Path.Combine(folder, old))) Directory.Delete(Path.Combine(folder, old), true);
```

In `Tick`, change `if (now - started > 240)` to `if (now - started > 300)`.

Pull the entry loop of `Tarball` into a helper so a bz2 builder can share it. Replace the whole `Tarball` method with:

```csharp
        // Each entry is { "dir" | "file" | "link", name, file text or link target }.
        private static byte[] Tarball(params string[][] entries)
        {
            using (MemoryStream raw = new MemoryStream())
            {
                using (GZipStream zip = new GZipStream(raw, CompressionLevel.Fastest, true))
                using (TarWriter tar = new TarWriter(zip, TarEntryFormat.Pax, false))
                    Write(tar, entries);
                return raw.ToArray();
            }
        }

        // Same entries, bzip2-compressed the way sherpa-onnx ships its libraries.
        private byte[] Bzipped(string name, params string[][] entries)
        {
            string path = Path.Combine(folder, name + ".tar");
            using (FileStream file = File.Create(path))
            using (TarWriter tar = new TarWriter(file, TarEntryFormat.Pax, false))
                Write(tar, entries);
            ProcessStartInfo info = new ProcessStartInfo("/usr/bin/bzip2");
            info.UseShellExecute = false;
            info.ArgumentList.Add("-f");
            info.ArgumentList.Add(path);
            using (Process zip = Process.Start(info)) zip.WaitForExit();
            byte[] made = File.ReadAllBytes(path + ".bz2");
            File.Delete(path + ".bz2");
            return made;
        }

        private static void Write(TarWriter tar, string[][] entries)
        {
            foreach (string[] entry in entries)
            {
                PaxTarEntry item = new PaxTarEntry(entry[0] == "dir" ? TarEntryType.Directory : entry[0] == "link" ? TarEntryType.SymbolicLink : TarEntryType.RegularFile, entry[1]);
                item.Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
                if (entry[0] == "link") item.LinkName = entry[2];
                if (entry[0] == "file") item.DataStream = new MemoryStream(Encoding.UTF8.GetBytes(entry[2]));
                tar.WriteEntry(item);
            }
        }

        private static byte[] Zipped(string[] names, byte[][] bodies)
        {
            using (MemoryStream raw = new MemoryStream())
            {
                using (ZipArchive zip = new ZipArchive(raw, ZipArchiveMode.Create, true))
                    for (int i = 0; i < names.Length; i++)
                        using (Stream entry = zip.CreateEntry(names[i]).Open()) entry.Write(bodies[i], 0, bodies[i].Length);
                return raw.ToArray();
            }
        }
```

In step 33, replace its ending

```csharp
                fakeBrain.Stop();
                pet.Frame.Close();
                hub.Stop();
                Send(shelf, "quit");
                Finish(null);
                return true;
            }
```
with
```csharp
                fakeBrain.Stop();
                return true;
            }
            if (step == 34)
            {
                byte[] model = new byte[9 << 20];
                new Random(11).NextBytes(model);
                string[] names = { "model.onnx", "voices.bin", "tokens.txt", "lexicon-us-en.txt", "lexicon-gb-en.txt", "espeak-ng-data/phontab", "onnxruntime.dll", "sherpa-onnx-c-api.dll", "licenses/README.txt" };
                byte[][] bodies = new byte[names.Length][];
                bodies[0] = model;
                for (int i = 1; i < names.Length; i++) bodies[i] = Encoding.UTF8.GetBytes(names[i]);
                byte[] pack = Zipped(names, bodies);
                byte[] evilPack = Zipped(new[] { "../evil" }, new[] { Encoding.UTF8.GetBytes("x") });
                string top = "sherpa-onnx-v1.13.8-osx-arm64-shared-lib/";
                byte[] libs = Bzipped("libs-good", new[] { "dir", top, "" }, new[] { "dir", top + "lib/", "" }, new[] { "file", top + "lib/libsherpa-onnx-c-api.dylib", "c" }, new[] { "file", top + "lib/libsherpa-onnx-cxx-api.dylib", "cxx" }, new[] { "file", top + "lib/libonnxruntime.dylib", "ort" });
                byte[] linkLibs = Bzipped("libs-link", new[] { "file", top + "lib/libsherpa-onnx-c-api.dylib", "c" }, new[] { "file", top + "lib/libonnxruntime.dylib", "ort" }, new[] { "link", top + "lib/escape", "/etc/passwd" });
                byte[] dotLibs = Bzipped("libs-dot", new[] { "file", "../evil", "x" }, new[] { "file", top + "lib/libsherpa-onnx-c-api.dylib", "c" }, new[] { "file", top + "lib/libonnxruntime.dylib", "ort" });
                string packName = Path.GetFileName(LocalVoice.PackUrl);
                string libsName = Path.GetFileName(LocalVoice.LibsUrl);
                string voiceRoot = Path.Combine(folder, "tv", "voice");
                string installed = Path.Combine(voiceRoot, LocalVoice.Pack);
                LocalVoice trial = new LocalVoice(Path.Combine(folder, "tv"));
                Action<byte[], byte[]> serve = delegate(byte[] p, byte[] l)
                {
                    lock (hubFiles) { hubFiles[packName] = p; hubFiles[libsName] = l; }
                    LocalVoice.PackSha = Sha(p);
                    LocalVoice.PackSize = p.Length;
                    LocalVoice.LibsSha = Sha(l);
                    LocalVoice.LibsSize = l.Length;
                };
                foreach (byte[][] bad in new[] { new[] { evilPack, libs }, new[] { pack, linkLibs }, new[] { pack, dotLibs } })
                {
                    serve(bad[0], bad[1]);
                    bool refused = false;
                    try { trial.Install(delegate { }); }
                    catch (IOException) { refused = true; }
                    if (!refused || trial.Installed || Directory.Exists(installed) || File.Exists(Path.Combine(voiceRoot, "evil")) || File.Exists(Path.Combine(folder, "tv", "evil"))) throw new Exception("A voice download reaching outside its folder wasn't refused.");
                }
                serve(pack, libs);
                bool cancelled = false;
                try { trial.Install(delegate(string text, long done, long total) { if (done < total) trial.Cancel = true; }); }
                catch (OperationCanceledException) { cancelled = true; }
                string part = Path.Combine(voiceRoot, LocalVoice.Pack + ".zip.part");
                long kept = File.Exists(part) ? new FileInfo(part).Length : 0;
                if (!cancelled || kept < (4L << 20) || kept >= pack.Length) throw new Exception("Cancel didn't stop her voice download and keep what came down: " + cancelled + " | " + kept);
                trial.Cancel = false;
                trial.Install(delegate { });
                bool resumed = false;
                lock (hubAsked) foreach (string asked in hubAsked) if (asked.StartsWith(packName + " from ", StringComparison.Ordinal)) resumed = true;
                string lib = Path.Combine(installed, "lib");
                bool clean = Directory.GetFiles(installed, "*.dll", SearchOption.AllDirectories).Length == 0 && Directory.GetFiles(lib).Length == 2 && File.Exists(Path.Combine(lib, "libsherpa-onnx-c-api.dylib")) && File.Exists(Path.Combine(lib, "libonnxruntime.dylib")) && File.Exists(Path.Combine(installed, "licenses", "README.txt"));
                bool tidy = !File.Exists(installed + ".zip") && !File.Exists(installed + "-libs.tar.bz2") && !Directory.Exists(installed + ".new") && !Directory.Exists(installed + ".libs");
                if (!trial.Installed || !resumed || !clean || !tidy) throw new Exception("Her free voice didn't install cleanly: " + trial.Installed + " | " + resumed + " | " + clean + " | " + tidy);
                checks.Add("PASS free voice install: Windows' voice pack and sherpa-onnx's Mac libraries download, pass their checksums and unpack without the Windows DLLs, archives reaching outside their folder or carrying links are refused, and Cancel picks up where it left off");
                pet.Frame.Close();
                hub.Stop();
                Send(shelf, "quit");
                Finish(null);
                return true;
            }
```

- [ ] **Step 2: Build to verify it fails**

Run: `dotnet build cross/Capri.Cross.csproj -c Release`
Expected: FAIL — `The type or namespace name 'LocalVoice' could not be found` (it is `#if !CROSS` today).

- [ ] **Step 3: Enable `LocalVoice` on Mac (`app/Voice.cs`)**

At the top, replace

```csharp
using System.IO;
#if !CROSS
using System.IO.Compression;
using System.Media;
#endif
```
with
```csharp
using System.IO;
using System.IO.Compression;
#if !CROSS
using System.Media;
#endif
```

Delete the line `#if !CROSS` directly above `internal sealed class FreeStyle`, and the matching `#endif` that is the second-to-last line of the file (just before the namespace's closing `}`).

Change `internal sealed class LocalVoice : IDisposable` to `internal sealed partial class LocalVoice : IDisposable`.

In `Install`, make the first lines:

```csharp
        internal void Install(Action<string, long, long> progress)
        {
#if CROSS
            if (!OperatingSystem.IsWindows()) { InstallMac(progress); return; }
#endif
            if (IntPtr.Size != 8) throw new IOException("Her free voice needs 64-bit Windows.");
```

Replace

```csharp
        private static bool Complete(string where) { return VoicePack.Complete(where); }
```
with
```csharp
#if CROSS
        private static bool Complete(string where) { return OperatingSystem.IsWindows() ? VoicePack.Complete(where) : CompleteMac(where); }
#else
        private static bool Complete(string where) { return VoicePack.Complete(where); }
#endif
```

In `Load`, make the first lines:

```csharp
        internal static void Load(string where)
        {
#if CROSS
            if (!OperatingSystem.IsWindows()) { LoadMac(where); return; }
#endif
            lock (loading)
```

- [ ] **Step 4: Make `Engine.Mirrored` reachable (`cross/MacEngine.cs`)**

Change `private static string Mirrored(string url)` to `internal static string Mirrored(string url)`.

- [ ] **Step 5: Create `cross/MacVoice.cs`**

```csharp
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
```

Note: `new FileInfo(path).LinkTarget` is non-null for a link to a file or a folder alike.

- [ ] **Step 6: Build**

Run: `dotnet build cross/Capri.Cross.csproj -c Release` → `0 Erreur(s)`. Smoke: deferred to the user.

- [ ] **Step 7: Commit**

```bash
git add app/Voice.cs cross/MacVoice.cs cross/MacEngine.cs cross/Smoke.cs
git commit -m "a Mac downloads her free Kokoro voice and sherpa-onnx's Mac libraries, checks them and unpacks only what stays in its folder"
```

---

### Task 2: VOICE tiers in Pet and the Mainframe

**Files:**
- Modify: `cross/Pet.cs`
- Modify: `cross/Mainframe.cs`
- Modify: `cross/Smoke.cs`

- [ ] **Step 1: Write the failing test**

In `cross/Smoke.cs`, add fields next to `private Pick testPick;`:

```csharp
        private readonly List<string> freeHeard = new List<string>();
        private byte[] voiceTone;
        private int voicedBefore;
```

At the end of step 34, replace

```csharp
                pet.Frame.Close();
                hub.Stop();
                Send(shelf, "quit");
                Finish(null);
                return true;
            }
```
(the one after the `PASS free voice install` line) with
```csharp
                if (Pet.FirstVoiceTier(true, true) != 0 || Pet.FirstVoiceTier(false, true) != 2 || Pet.FirstVoiceTier(false, false) != 1) throw new Exception("Her first voice tier is wrong.");
                Mainframe frame = pet.Frame;
                short[] tone = new short[24000];
                for (int i = 0; i < tone.Length; i++) tone[i] = (short)(Math.Sin(i * 0.05) * 3000);
                voiceTone = Voice.Wave(tone, 24000);
                pet.FreeSpeak = delegate(string words) { lock (freeHeard) freeHeard.Add(words + "|" + pet.FreeStyleName); return voiceTone; };
                frame.PickVoiceTier(1);
                if (pet.Saved.VoiceTier != 1 || pet.FreeSpeaks || !frame.VoiceSetupShown || frame.VoiceStatus.IndexOf("Her free voice · ", StringComparison.Ordinal) < 0) throw new Exception("FREE VOICE without her pack didn't offer Get free voice: " + frame.VoiceStatus);
                lock (spoken) voicedBefore = spoken.Count;
                pet.Think = delegate(string words) { return "Text only for now, choom."; };
                pet.Hear("say something");
                return true;
            }
            if (step == 35)
            {
                if (pet.Talk.Reply != "Text only for now, choom.") return false;
                lock (spoken) if (spoken.Count != voicedBefore) throw new Exception("FREE VOICE without her pack still played sound.");
                pet.Frame.PressGetVoice();
                if (!pet.SettingUpVoice) throw new Exception("Get free voice didn't start.");
                return true;
            }
            if (step == 36)
            {
                Mainframe frame = pet.Frame;
                if (pet.SettingUpVoice || pet.Talk.Reply != "Hey choom, this is my free voice.") { if (!pet.SettingUpVoice && pet.FreeProblem != null) throw new Exception("Get free voice failed: " + pet.FreeProblem); return false; }
                if (!frame.TiersFit) return false;
                string last;
                lock (freeHeard) last = freeHeard.Count == 0 ? "" : freeHeard[freeHeard.Count - 1];
                int voiced;
                lock (spoken) voiced = spoken.Count;
                if (!pet.FreeSpeaks || frame.VoiceSetupShown || voiced != voicedBefore + 1 || !last.EndsWith("|Her own", StringComparison.Ordinal) || last.IndexOf("free voice", StringComparison.Ordinal) < 0) throw new Exception("Her free voice didn't speak its test line: " + last + " | " + voiced + " | " + frame.VoiceStatus);
                Snap(frame.Content as Control, "mainframe-voice.png");
                frame.PickStyle("Sharp");
                if (pet.FreeStyleName != "Sharp" || pet.Saved.FreeStyle != "Sharp" || frame.VoiceStatus.IndexOf("Free voice: Sharp", StringComparison.Ordinal) < 0) throw new Exception("Picking the Sharp style didn't stick: " + frame.VoiceStatus);
                pet.Think = delegate(string words) { return "Sharp check, choom."; };
                pet.Hear("again");
                return true;
            }
            if (step == 37)
            {
                Mainframe frame = pet.Frame;
                if (pet.Talk.Reply != "Sharp check, choom.") return false;
                string last;
                lock (freeHeard) last = freeHeard[freeHeard.Count - 1];
                int voiced;
                lock (spoken) voiced = spoken.Count;
                if (voiced != voicedBefore + 2 || !last.EndsWith("|Sharp", StringComparison.Ordinal)) throw new Exception("Her reply wasn't spoken in the Sharp style: " + last + " | " + voiced);
                checks.Add("PASS free voice: FREE VOICE without her pack types and offers Get free voice, the setup installs it and she says her test line, replies are spoken with her mouth moving, and the style picked is the one heard");
                pet.SetLanguage("French");
                frame.Render();
                if (pet.FreeSpeaks || frame.VoiceStatus.IndexOf("Free voice speaks English only.", StringComparison.Ordinal) < 0) throw new Exception("In French, the Mainframe didn't say the free voice is English only: " + frame.VoiceStatus);
                pet.Think = delegate(string words) { return "Bonjour, choom."; };
                pet.Hear("en francais");
                return true;
            }
            if (step == 38)
            {
                Mainframe frame = pet.Frame;
                if (pet.Talk.Reply != "Bonjour, choom.") return false;
                lock (spoken) if (spoken.Count != voicedBefore + 2) throw new Exception("Her free voice read a French reply.");
                pet.SetLanguage("English");
                frame.PickVoiceTier(0);
                pet.Think = delegate(string words) { return "Quiet now, choom."; };
                pet.Hear("off");
                return true;
            }
            if (step == 39)
            {
                Mainframe frame = pet.Frame;
                if (pet.Talk.Reply != "Quiet now, choom.") return false;
                lock (spoken) if (spoken.Count != voicedBefore + 2 || pet.Saved.VoiceTier != 0) throw new Exception("OFF still spoke.");
                frame.PickVoiceTier(2);
                if (!frame.VoiceStatus.EndsWith("Paste your ElevenLabs key | Connect", StringComparison.Ordinal)) throw new Exception("ELEVENLABS didn't ask for a key: " + frame.VoiceStatus);
                Voice.Host = FakeVoice();
                frame.TypeVoiceKey("good-key-1234");
                frame.PressVoiceConnect();
                return true;
            }
            if (step == 40)
            {
                Mainframe frame = pet.Frame;
                if (!pet.Speaks) return false;
                if (Keys.Read("voice") != "good-key-1234" || pet.Saved.VoiceTier != 2 || !frame.VoiceStatus.EndsWith("Key saved, ends 1234 | Forget", StringComparison.Ordinal)) throw new Exception("The ElevenLabs key from the Mainframe didn't land in Keychain: " + frame.VoiceStatus);
                checks.Add("PASS voice tiers: a non-English reply and OFF stay silent, and ELEVENLABS takes its key in the Mainframe into Keychain");
                pet.ForgetVoice();
                fakeVoice.Stop();
                frame.Close();
                hub.Stop();
                Send(shelf, "quit");
                Finish(null);
                return true;
            }
```

- [ ] **Step 2: Build to verify it fails**

Run: `dotnet build cross/Capri.Cross.csproj -c Release`
Expected: FAIL — `'Pet' does not contain a definition for 'FirstVoiceTier'` (and `FreeSpeak`, `FreeStyleName`, `FreeSpeaks`, `SettingUpVoice`, `FreeProblem`, `Settings.VoiceTier`, `Settings.FreeStyle`; `'Mainframe'` lacks `PickVoiceTier`, `VoiceSetupShown`, `VoiceStatus`, `PressGetVoice`, `PickStyle`, `TypeVoiceKey`, `PressVoiceConnect`).

- [ ] **Step 3: Pet — settings, fields, startup (`cross/Pet.cs`)**

In `Settings`, after `[DataMember] internal string PaidModel;` add:

```csharp
        [DataMember] internal int? VoiceTier;
        [DataMember] internal string FreeStyle;
```

Next to `private Engine builtIn;` add:

```csharp
        private LocalVoice kokoro;
        private double freeUsed;
        private const string FreeHeard = "(free voice)";
        internal Func<string, byte[]> FreeSpeak;
        internal bool SettingUpVoice;
        internal bool VoiceLoading;
        internal string VoiceSetupText;
        internal string FreeProblem;
```

In the constructor, right after `voice.VoiceId = string.IsNullOrEmpty(Saved.VoiceId) ? null : Saved.VoiceId;` add:

```csharp
            if (Saved.VoiceTier == null) Saved.VoiceTier = FirstVoiceTier(Saved.Silent, voice.Key != null);
            Saved.VoiceTier = Math.Max(0, Math.Min(2, Saved.VoiceTier.Value));
            kokoro = new LocalVoice(Program.Smoke ?? folder);
            kokoro.Style = LocalVoice.Find(Saved.FreeStyle);
```

In `Tick()`, right after `double now = Now;` add:

```csharp
            if (freeUsed > 0 && now - freeUsed > 900) { freeUsed = 0; ThreadPool.QueueUserWorkItem(delegate { kokoro.Dispose(); }); }
```

- [ ] **Step 4: Pet — tiers, speaking, setup**

Replace `internal bool Speaks { get { return voice.Ready && !Saved.Silent; } }` with:

```csharp
        internal bool Speaks { get { return voice.Ready && Saved.VoiceTier == 2; } }

        internal static int FirstVoiceTier(bool silent, bool key) { return silent ? 0 : key ? 2 : 1; }

        internal bool English { get { return Brain.LanguageCode(Saved.Language) == "en"; } }

        internal bool FreeInstalled { get { return kokoro.Installed; } }

        // Her free voice only knows English, so other languages stay in text.
        internal bool FreeSpeaks { get { return Saved.VoiceTier == 1 && English && kokoro.Installed; } }

        internal string FreeStyleName { get { return kokoro.Style.Name; } }

        internal string VoiceKeyTail { get { return voice.Key == null ? null : Voice.Tail(voice.Key); } }

        private byte[] SpeakFree(string words)
        {
            freeUsed = Now;
            return kokoro.Speak(words);
        }

        internal void ChooseVoiceTier(int tier)
        {
            tier = Math.Max(0, Math.Min(2, tier));
            if (tier == Saved.VoiceTier) return;
            Saved.VoiceTier = tier;
            Saved.Silent = tier == 0;
            if (tier == 0) voice.Stop();
            Save();
            ShowState();
        }

        internal void ChooseFreeStyle(string name)
        {
            kokoro.Style = LocalVoice.Find(name);
            Saved.FreeStyle = kokoro.Style.Name;
            Save();
            ShowState();
        }

        // Ported from Windows' SetUpFreeVoice.
        internal void SetUpFreeVoice()
        {
            if (SettingUpVoice || !LocalVoice.Supported) return;
            SettingUpVoice = true;
            VoiceLoading = false;
            kokoro.Cancel = false;
            FreeProblem = null;
            VoiceSetupText = "Starting download";
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
                    string label = text + (total > 0 && done < total ? " · " + Engine.Amount(done) + " / " + Engine.Amount(total) : "");
                    Dispatcher.UIThread.Post(delegate { if (SettingUpVoice) { VoiceSetupText = label; ShowState(); } });
                };
                try
                {
                    kokoro.Install(progress);
                    Dispatcher.UIThread.Post(delegate { VoiceLoading = true; VoiceSetupText = "Loading her free voice"; ShowState(); });
                    if (FreeSpeak == null) kokoro.Warm();
                    Dispatcher.UIThread.Post(delegate
                    {
                        SettingUpVoice = false;
                        VoiceLoading = false;
                        VoiceSetupText = null;
                        ShowState();
                        if (Saved.VoiceTier == 1) Reply(++asking, FreeHeard, "Hey choom, this is my free voice.");
                    });
                }
                catch (OperationCanceledException) { Dispatcher.UIThread.Post(delegate { SettingUpVoice = false; VoiceLoading = false; VoiceSetupText = null; FreeProblem = "Cancelled. It picks up where it left off."; ShowState(); }); }
                catch (Exception error)
                {
                    string problem = Voice.Short(error.Message);
                    Dispatcher.UIThread.Post(delegate { SettingUpVoice = false; VoiceLoading = false; VoiceSetupText = null; FreeProblem = problem; ShowState(); });
                }
            });
        }

        internal void CancelFreeVoice() { kokoro.Cancel = true; }
```

Replace the whole `Reply` method with:

```csharp
        private void Reply(int ticket, string heard, string said)
        {
            bool paid = Speaks;
            Func<string, byte[]> speak = paid ? new Func<string, byte[]>(voice.Speak) : FreeSpeaks ? FreeSpeak ?? new Func<string, byte[]>(SpeakFree) : null;
            if (speak == null || Voice.Spoken(said).Length == 0) { Talk.Answer(heard, said); TypeAlong(said, ticket); return; }
            string words = paid ? said : Voice.Spoken(said);
            Talk.Thinking(heard);
            bool[] shown = { false };
            DispatcherTimer.RunOnce(delegate
            {
                if (ticket != asking || shown[0]) return;
                shown[0] = true;
                Talk.Answer(heard, said);
                TypeAlong(said, ticket);
            }, TimeSpan.FromMilliseconds(VoiceWait));
            ThreadPool.QueueUserWorkItem(delegate
            {
                byte[] wav = null;
                string problem = null;
                try { wav = speak(words); }
                catch (VoiceException error) { problem = paid ? Mac(error.Message) : error.Message; }
                catch (Exception error) { problem = Voice.Short((paid ? "Voice glitched: " : "Her free voice glitched: ") + error.Message); }
                Dispatcher.UIThread.Post(delegate
                {
                    if (paid) voiceProblem = problem;
                    else { FreeProblem = problem; ShowState(); }
                    if (ticket != asking || shown[0]) return;
                    shown[0] = true;
                    double seconds = wav == null ? 0 : Voice.Seconds(wav);
                    Talk.Answer(heard, said, seconds);
                    if (seconds >= 0.2) voice.Play(wav);
                    else TypeAlong(said, ticket);
                });
            });
        }
```

Replace `internal bool VoiceWorking { get { return Speaks && voiceProblem == null; } }` with:

```csharp
        internal bool VoiceWorking { get { return (Speaks && voiceProblem == null) || (FreeSpeaks && FreeProblem == null); } }
```

Replace the body of `SetSpeak` with:

```csharp
        internal void SetSpeak(bool on)
        {
            Saved.VoiceTier = on ? 2 : 0;
            Saved.Silent = !on;
            if (!on) voice.Stop();
            Save();
            ShowState();
        }
```

In `SaveVoiceKey`, replace `Saved.Silent = false;` with:

```csharp
                    Saved.Silent = false;
                    Saved.VoiceTier = 2;
```
and right after the `Save();` that follows it add `ShowState();`.

In `RefreshVoice`, the "Use ElevenLabs" toggle must follow the tier, not the old `Silent` flag. Replace

```csharp
            speak.Click += delegate { SetSpeak(Saved.Silent); };
```
with
```csharp
            speak.Click += delegate { SetSpeak(!Speaks); };
```

Replace `ForgetVoice` with:

```csharp
        internal void ForgetVoice()
        {
            voice.Stop();
            voice.Key = null;
            voices = null;
            voiceProblem = null;
            WriteVoiceKey(null);
            if (Saved.VoiceTier == 2) Saved.VoiceTier = 1;
            ShowState();
        }
```

- [ ] **Step 5: Mainframe — VOICE section (`cross/Mainframe.cs`)**

Add fields next to `private readonly Button ollama`:

```csharp
        private static readonly string[,] VoiceTiers = { { "OFF", "Silent · text only" }, { "FREE VOICE", "Free · runs on your Mac" }, { "ELEVENLABS", "Sounds amazing · paid" } };
        private readonly RadioButton[] voiceRungs = new RadioButton[3];
        private readonly Button styles = Make("");
        private readonly MenuFlyout styleMenu = new MenuFlyout();
        private readonly Button getVoice = Make("Get free voice");
        private readonly Button cancelVoice = Make("Cancel");
        private readonly TextBlock voiceNote = Line(10.5, Look.Dim, false);
        private readonly TextBox elevenBox = new TextBox { PasswordChar = '•', Width = 230, FontFamily = Look.Mono };
        private readonly Button elevenConnect = Make("Connect");
        private StackPanel styleRow;
        private StackPanel voiceSetupRow;
        private StackPanel elevenRow;
        private bool elevenSaved;
        private int voiceTier;
```

In the constructor, right after `page.Children.Add(ollama);` add:

```csharp
            page.Children.Add(new TextBlock { Text = "VOICE", FontFamily = Look.Mono, FontSize = 12, Foreground = new SolidColorBrush(Look.Cyan), Margin = new Thickness(0, 12, 0, 0) });
            for (int i = 0; i < voiceRungs.Length; i++)
            {
                int picked = i;
                // A group of its own, so picking a voice never unticks a BRAIN tier.
                RadioButton rung = new RadioButton { GroupName = "voice", Content = VoiceTiers[i, 0] + "  " + VoiceTiers[i, 1], FontFamily = Look.Mono, Foreground = new SolidColorBrush(Look.Ink) };
                rung.IsCheckedChanged += delegate { if (!syncing && rung.IsChecked == true) pet.ChooseVoiceTier(picked); };
                voiceRungs[i] = rung;
                page.Children.Add(Row(rung));
            }
            styles.Flyout = styleMenu;
            styleRow = Row(styles);
            page.Children.Add(styleRow);
            getVoice.Click += delegate { pet.SetUpFreeVoice(); };
            cancelVoice.Click += delegate { pet.CancelFreeVoice(); };
            voiceSetupRow = Row(getVoice, cancelVoice);
            page.Children.Add(voiceSetupRow);
            page.Children.Add(voiceNote);
            elevenConnect.Click += delegate { ConnectEleven(); };
            elevenBox.TextChanged += delegate { RelabelEleven(); };
            elevenBox.AddHandler(KeyDownEvent, delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; ConnectEleven(); } }, RoutingStrategies.Tunnel);
            elevenRow = Row(elevenBox, elevenConnect);
            page.Children.Add(elevenRow);
```

At the end of `Render()` (after its last line, `Relabel();`) add `RenderVoice();`, and add these members:

```csharp
        private void RenderVoice()
        {
            voiceTier = pet.Saved.VoiceTier ?? 1;
            syncing = true;
            for (int i = 0; i < voiceRungs.Length; i++) voiceRungs[i].IsChecked = i == voiceTier;
            syncing = false;
            bool free = voiceTier == 1;
            bool installed = pet.FreeInstalled;
            styleRow.IsVisible = free && installed;
            styles.Content = "Free voice: " + pet.FreeStyleName;
            styleMenu.Items.Clear();
            foreach (FreeStyle style in LocalVoice.Styles)
            {
                string name = style.Name;
                MenuItem pick = new MenuItem { Header = name, ToggleType = MenuItemToggleType.Radio, GroupName = "free-style", IsChecked = name == pet.FreeStyleName };
                pick.Click += delegate { pet.ChooseFreeStyle(name); };
                styleMenu.Items.Add(pick);
            }
            voiceSetupRow.IsVisible = free && LocalVoice.Supported && (pet.SettingUpVoice || !installed);
            getVoice.IsVisible = !pet.SettingUpVoice;
            cancelVoice.IsVisible = pet.SettingUpVoice && !pet.VoiceLoading;
            string note = "";
            if (free && !LocalVoice.Supported) note = "Free voice needs a Mac with Apple silicon.";
            else if (free && pet.SettingUpVoice) note = pet.VoiceSetupText ?? "";
            else if (free && pet.FreeProblem != null) note = pet.FreeProblem;
            else if (free && !installed) note = "Her free voice · " + Engine.Amount(LocalVoice.PackSize + LocalVoice.LibsSize);
            else if (free && !pet.English) note = "Free voice speaks English only.";
            voiceNote.Text = note;
            voiceNote.IsVisible = note.Length > 0;
            elevenRow.IsVisible = voiceTier == 2;
            elevenSaved = pet.VoiceKeyTail != null;
            elevenBox.Watermark = elevenSaved ? "Key saved, ends " + pet.VoiceKeyTail : "Paste your ElevenLabs key";
            RelabelEleven();
        }

        private void RelabelEleven()
        {
            bool forget = elevenSaved && string.IsNullOrWhiteSpace(elevenBox.Text);
            elevenConnect.Content = forget ? "Forget" : "Connect";
            elevenConnect.Foreground = new SolidColorBrush(forget ? Look.Red : Look.Ink);
        }

        private void ConnectEleven()
        {
            string typed = (elevenBox.Text ?? "").Trim();
            if (typed.Length > 0) { elevenBox.Text = ""; pet.SaveVoiceKey(typed); }
            else if (elevenSaved) pet.ForgetVoice();
            else elevenBox.Focus();
        }

        internal void PickVoiceTier(int pick) { voiceRungs[pick].IsChecked = true; }
        internal void PickStyle(string name) { pet.ChooseFreeStyle(name); }
        internal void PressGetVoice() { pet.SetUpFreeVoice(); }
        internal void TypeVoiceKey(string key) { elevenBox.Text = key; }
        internal void PressVoiceConnect() { ConnectEleven(); }
        internal bool VoiceSetupShown { get { return voiceSetupRow.IsVisible && getVoice.IsVisible; } }
        internal string VoiceStatus { get { return "voice " + voiceTier + " | " + (styleRow.IsVisible ? styles.Content : "-") + " | " + voiceNote.Text + " | " + (elevenRow.IsVisible ? elevenBox.Watermark + " | " + elevenConnect.Content : "no key row"); } }
```

- [ ] **Step 6: Build**

Run: `dotnet build cross/Capri.Cross.csproj -c Release` → `0 Erreur(s)`. Smoke: deferred to the user.

- [ ] **Step 7: Commit**

```bash
git add cross/Pet.cs cross/Mainframe.cs cross/Smoke.cs
git commit -m "the Mainframe gets Windows' VOICE section: OFF, her free voice with its styles, or an ElevenLabs key"
```

---

### Task 3: Smoke run, manual check on the M2, wrap up

- [ ] **Step 1: The user runs the smoke test**

```
! cd ~/Documents/projects/capri/capri-desktop && cross/bin/Release/net10.0/Capri --smoke /tmp/capri-smoke; echo "exit $?"; grep -cE "^PASS" /tmp/capri-smoke/smoke-test.txt; grep -E "FAIL|free voice|voice tiers" /tmp/capri-smoke/smoke-test.txt
```
Expected: `exit 0`, new PASS lines `free voice install`, `free voice`, `voice tiers`. Look at `/tmp/capri-smoke/mainframe-voice.png`.

- [ ] **Step 2: Manual check (the user)**

1. Launch Capri, Mainframe → VOICE: FREE VOICE is picked, with **Get free voice** · ~325 MB.
2. Press it: progress, then "Loading her free voice", then "Hey choom, this is my free voice." spoken.
3. Chat: she speaks; time from the reply to her voice.
4. Try the 4 styles (Her own, Bright, Sly, Sharp).
5. Activity Monitor: Capri's memory while speaking.
6. OFF → silent; FREE VOICE again → she speaks.

- [ ] **Step 3: Update `docs/mac-parity-todo.md`** (step 3 done, step 4 next) and commit.
