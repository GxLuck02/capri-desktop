# Mac Mainframe BRAIN + Keychain Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the Mac/Linux build (`cross/`) a Capri Mainframe window with the BRAIN section of Windows (NORMAL local Ollama / HIGHER NVIDIA / MAX paid key), and keep every API key in the macOS Keychain.

**Architecture:** A new `Keys` static class (Keychain via Security.framework P/Invoke on Mac, `600` file on Linux) replaces the plain `voice-key` file. `Pet` gains the tier logic ported from `app/Capri.cs` (`ChooseTier`, `ConnectText`, `ForgetText`, `ListCloud`) and owns all brain state. A new Avalonia `Mainframe` window is a thin view: it calls `Pet` methods and redraws itself from `Pet` state in `Render()`.

**Tech Stack:** C# / .NET 10, Avalonia 11.3, shared `app/Brain.cs` (`Cloud`, `Brain.CloudModels`, `Brain.DefaultCloudModel`), Security.framework + CoreFoundation via `DllImport`. Tests: the built-in `--smoke` run (`cross/Smoke.cs`), a step machine where each `Step(now)` branch returns `true` to advance, `false` to wait, and throws to fail.

Spec: `docs/superpowers/specs/2026-10-29-mac-mainframe-brain-design.md`.

## Commands

- Build: `dotnet build cross/Capri.Cross.csproj -c Release` → expect `0 Erreur(s)` / `0 Error(s)`.
- Smoke (opens real windows on screen for ~2 min, don't touch the mouse):
  ```bash
  S=/tmp/capri-smoke; rm -rf $S; cross/bin/Release/net10.0/Capri --smoke $S; echo "exit $?"; cat $S/smoke-test.txt
  ```
  Expect `exit 0` and no `FAIL` line. A failing run ends with `FAIL <message>` and `exit 1`.

## Files

- Create `cross/Keys.cs` — `Keys` (save/read/forget/migrate API keys) and `Keychain` (Security.framework P/Invoke).
- Create `cross/Mainframe.cs` — the Mainframe window, BRAIN section only.
- Modify `cross/Pet.cs` — `Settings` fields, voice key through `Keys`, tier logic, Brain menu, `Capri Mainframe` menu entry.
- Modify `cross/Smoke.cs` — fake brain server, new steps 22-30, voice steps read `Keys` instead of the file.

Conventions in this codebase: no XAML, controls built in code; `delegate { }` handlers; no `var`; terse comments only where a reason isn't obvious; shared `app/*.cs` files are not modified.

---

### Task 1: `Keys` — Keychain on Mac, private file on Linux, voice key moves in

**Files:**
- Create: `cross/Keys.cs`
- Modify: `cross/Pet.cs` (constructor ~l.240, `KeyFile`/`ReadVoiceKey`/`WriteVoiceKey` ~l.1348-1381)
- Modify: `cross/Smoke.cs` (constructor ~l.47, steps 18, 19, 21, `Tick` timeout l.62)

- [ ] **Step 1: Write the failing test**

In `cross/Smoke.cs`, constructor, after `Brain.MakePersonality();` add (clears what a crashed run left in the `capri-smoke` Keychain service):

```csharp
            foreach (string name in new[] { "nvidia", "paid", "smoke" }) Keys.Forget(name);
            pet.ForgetVoice();
```

In `Tick`, change the overall timeout `if (now - started > 150)` to `if (now - started > 200)`.

Step 18: replace `File.Exists(Path.Combine(folder, "voice-key"))` with `Keys.Read("voice") != null`.

Step 19: replace

```csharp
                string keyPath = Path.Combine(folder, "voice-key");
```
with
```csharp
                string keyPath = Path.Combine(folder, "voice-key");
                string savedKey = Keys.Read("voice");
```
replace
```csharp
                bool shut = OperatingSystem.IsWindows() || File.GetUnixFileMode(keyPath) == (UnixFileMode.UserRead | UnixFileMode.UserWrite);
```
with
```csharp
                bool shut = !OperatingSystem.IsLinux() || File.GetUnixFileMode(keyPath) == (UnixFileMode.UserRead | UnixFileMode.UserWrite);
```
and in the big `if`, replace `!File.Exists(keyPath) || File.ReadAllText(keyPath) != "good-key-1234" || !shut` with `savedKey != "good-key-1234" || !shut || (OperatingSystem.IsMacOS() && File.Exists(keyPath))`. Change its PASS text to:

```csharp
                checks.Add("PASS voice key: Settings > Voice asks for the key in a hidden box, explains and drops a rejected key, keeps a good one in " + (OperatingSystem.IsMacOS() ? "Keychain" : "a private file") + " and picks their own voice first");
```

Step 21: replace `File.Exists(Path.Combine(folder, "voice-key"))` with `Keys.Read("voice") != null`, change `and Remove key deletes the file` to `and Remove key deletes it`, and replace the end of the step

```csharp
                fakeVoice.Stop();
                Send(shelf, "quit");
                Finish(null);
                return true;
            }
```
with
```csharp
                fakeVoice.Stop();
                return true;
            }
            if (step == 22)
            {
                Keys.Save("smoke", "abc-1234");
                string kept = Keys.Read("smoke");
                bool shut = !OperatingSystem.IsLinux() || File.GetUnixFileMode(Path.Combine(folder, "smoke-key")) == (UnixFileMode.UserRead | UnixFileMode.UserWrite);
                Keys.Forget("smoke");
                if (kept != "abc-1234" || !shut || Keys.Read("smoke") != null || Keys.Problem != null) throw new Exception("Keys didn't save, read back and forget a key: " + kept + " | " + shut + " | " + Keys.Problem);
                if (OperatingSystem.IsMacOS())
                {
                    string old = Path.Combine(folder, "voice-key");
                    File.WriteAllText(old, "old-key-9876");
                    Keys.Broken = true;
                    bool refused = false;
                    try { Keys.Migrate(); }
                    catch (Exception) { refused = true; }
                    Keys.Broken = false;
                    if (!refused || !File.Exists(old)) throw new Exception("A failed move into Keychain lost the old voice-key file.");
                    Keys.Migrate();
                    string moved = Keys.Read("voice");
                    Keys.Forget("voice");
                    if (File.Exists(old) || moved != "old-key-9876") throw new Exception("The old voice-key file didn't move into Keychain: " + moved);
                }
                checks.Add("PASS keys: API keys save, read back and are forgotten " + (OperatingSystem.IsMacOS() ? "in Keychain, and the old voice-key file moves in and is deleted only once it's safely there" : "in a file only they can read"));
                Send(shelf, "quit");
                Finish(null);
                return true;
            }
```

- [ ] **Step 2: Run the build to verify it fails**

Run: `dotnet build cross/Capri.Cross.csproj -c Release`
Expected: FAIL with `error CS0103: The name 'Keys' does not exist in the current context`.

- [ ] **Step 3: Write `cross/Keys.cs`**

```csharp
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CapriDesktop
{
    // API keys: the macOS Keychain on a Mac, a file only its owner can read on Linux.
    internal static class Keys
    {
        internal static string Folder;
        internal static string Service = "capri";
        internal static string Problem;
        internal static bool Broken;

        internal static string Read(string name)
        {
            Problem = null;
            try { return OperatingSystem.IsMacOS() ? Keychain.Find(Service, name) : ReadFile(name); }
            catch (Exception error) { Problem = error.Message; return null; }
        }

        internal static void Save(string name, string key)
        {
            if (Broken) throw new IOException("Keychain is locked.");
            if (OperatingSystem.IsMacOS()) Keychain.Put(Service, name, key);
            else WriteFile(name, key);
        }

        internal static void Forget(string name)
        {
            if (OperatingSystem.IsMacOS()) { Keychain.Remove(Service, name); return; }
            string path = FilePath(name);
            if (File.Exists(path)) File.Delete(path);
        }

        // The Mac used to keep the ElevenLabs key in a plain voice-key file. On Linux that file is still where it lives.
        internal static void Migrate()
        {
            if (!OperatingSystem.IsMacOS()) return;
            string path = Path.Combine(Folder, "voice-key");
            if (!File.Exists(path)) return;
            string key = File.ReadAllText(path).Trim();
            if (key.Length > 0) Save("voice", key);
            File.Delete(path);
        }

        private static string FilePath(string name) { return Path.Combine(Folder, name + "-key"); }

        private static string ReadFile(string name)
        {
            string path = FilePath(name);
            if (!File.Exists(path)) return null;
            string key = File.ReadAllText(path).Trim();
            return key.Length == 0 ? null : key;
        }

        private static void WriteFile(string name, string key)
        {
            Directory.CreateDirectory(Folder);
            FileStreamOptions options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write, UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite };
            using (FileStream stream = new FileStream(FilePath(name), options))
            {
                File.SetUnixFileMode(stream.SafeFileHandle, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                using (StreamWriter writer = new StreamWriter(stream)) writer.Write(key);
            }
        }
    }

    // Generic passwords through Security.framework's SecItem calls, so no key ever passes through a command line.
    internal static class Keychain
    {
        private const string SecurityPath = "/System/Library/Frameworks/Security.framework/Security";
        private const string CorePath = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        private const int NotFound = -25300;
        private const uint Utf8 = 0x08000100;
        private static readonly IntPtr security = NativeLibrary.Load(SecurityPath);
        private static readonly IntPtr core = NativeLibrary.Load(CorePath);

        [DllImport(SecurityPath)] private static extern int SecItemAdd(IntPtr attributes, IntPtr result);
        [DllImport(SecurityPath)] private static extern int SecItemCopyMatching(IntPtr query, out IntPtr result);
        [DllImport(SecurityPath)] private static extern int SecItemUpdate(IntPtr query, IntPtr changes);
        [DllImport(SecurityPath)] private static extern int SecItemDelete(IntPtr query);
        [DllImport(SecurityPath)] private static extern IntPtr SecCopyErrorMessageString(int status, IntPtr reserved);
        [DllImport(CorePath)] private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string text, uint encoding);
        [DllImport(CorePath)] private static extern nint CFStringGetLength(IntPtr text);
        [DllImport(CorePath)] [return: MarshalAs(UnmanagedType.I1)] private static extern bool CFStringGetCString(IntPtr text, byte[] buffer, nint size, uint encoding);
        [DllImport(CorePath)] private static extern IntPtr CFDataCreate(IntPtr allocator, byte[] bytes, nint length);
        [DllImport(CorePath)] private static extern nint CFDataGetLength(IntPtr data);
        [DllImport(CorePath)] private static extern IntPtr CFDataGetBytePtr(IntPtr data);
        [DllImport(CorePath)] private static extern IntPtr CFDictionaryCreate(IntPtr allocator, IntPtr[] keys, IntPtr[] values, nint count, IntPtr keyCallBacks, IntPtr valueCallBacks);
        [DllImport(CorePath)] private static extern void CFRelease(IntPtr item);

        internal static string Find(string service, string account)
        {
            IntPtr name = Text(service), who = Text(account), query = IntPtr.Zero, found = IntPtr.Zero;
            try
            {
                query = Dictionary(Sec("kSecClass"), Sec("kSecClassGenericPassword"), Sec("kSecAttrService"), name, Sec("kSecAttrAccount"), who, Sec("kSecReturnData"), Constant(core, "kCFBooleanTrue"), Sec("kSecMatchLimit"), Sec("kSecMatchLimitOne"));
                int status = SecItemCopyMatching(query, out found);
                if (status == NotFound) return null;
                Check(status);
                byte[] bytes = new byte[(int)CFDataGetLength(found)];
                Marshal.Copy(CFDataGetBytePtr(found), bytes, 0, bytes.Length);
                string key = Encoding.UTF8.GetString(bytes).Trim();
                return key.Length == 0 ? null : key;
            }
            finally { Release(name, who, query, found); }
        }

        internal static void Put(string service, string account, string key)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(key);
            IntPtr name = Text(service), who = Text(account), data = CFDataCreate(IntPtr.Zero, bytes, bytes.Length), query = IntPtr.Zero, changes = IntPtr.Zero, adding = IntPtr.Zero;
            try
            {
                query = Dictionary(Sec("kSecClass"), Sec("kSecClassGenericPassword"), Sec("kSecAttrService"), name, Sec("kSecAttrAccount"), who);
                changes = Dictionary(Sec("kSecValueData"), data);
                int status = SecItemUpdate(query, changes);
                if (status == NotFound)
                {
                    adding = Dictionary(Sec("kSecClass"), Sec("kSecClassGenericPassword"), Sec("kSecAttrService"), name, Sec("kSecAttrAccount"), who, Sec("kSecValueData"), data);
                    status = SecItemAdd(adding, IntPtr.Zero);
                }
                Check(status);
            }
            finally { Release(name, who, data, query, changes, adding); }
        }

        internal static void Remove(string service, string account)
        {
            IntPtr name = Text(service), who = Text(account), query = IntPtr.Zero;
            try
            {
                query = Dictionary(Sec("kSecClass"), Sec("kSecClassGenericPassword"), Sec("kSecAttrService"), name, Sec("kSecAttrAccount"), who);
                int status = SecItemDelete(query);
                if (status != NotFound) Check(status);
            }
            finally { Release(name, who, query); }
        }

        private static IntPtr Sec(string name) { return Constant(security, name); }

        private static IntPtr Constant(IntPtr library, string name) { return Marshal.ReadIntPtr(NativeLibrary.GetExport(library, name)); }

        private static IntPtr Text(string text) { return CFStringCreateWithCString(IntPtr.Zero, text, Utf8); }

        // Keys and values alternate: key, value, key, value...
        private static IntPtr Dictionary(params IntPtr[] pairs)
        {
            IntPtr[] keys = new IntPtr[pairs.Length / 2], values = new IntPtr[pairs.Length / 2];
            for (int i = 0; i < keys.Length; i++) { keys[i] = pairs[i * 2]; values[i] = pairs[i * 2 + 1]; }
            return CFDictionaryCreate(IntPtr.Zero, keys, values, keys.Length, NativeLibrary.GetExport(core, "kCFTypeDictionaryKeyCallBacks"), NativeLibrary.GetExport(core, "kCFTypeDictionaryValueCallBacks"));
        }

        private static void Check(int status)
        {
            if (status == 0) return;
            IntPtr said = SecCopyErrorMessageString(status, IntPtr.Zero);
            string message = "Keychain error " + status;
            if (said != IntPtr.Zero)
            {
                byte[] buffer = new byte[(int)CFStringGetLength(said) * 4 + 1];
                if (CFStringGetCString(said, buffer, buffer.Length, Utf8)) message = Encoding.UTF8.GetString(buffer).TrimEnd('\0');
                CFRelease(said);
            }
            throw new IOException(message);
        }

        private static void Release(params IntPtr[] items) { foreach (IntPtr item in items) if (item != IntPtr.Zero) CFRelease(item); }
    }
}
```

- [ ] **Step 4: Route the voice key through `Keys` in `cross/Pet.cs`**

In the constructor, replace `voice.Key = ReadVoiceKey();` with:

```csharp
            Keys.Folder = Program.Smoke ?? folder;
            if (Program.Smoke != null) Keys.Service = "capri-smoke";
            try { Keys.Migrate(); }
            catch (Exception error) { Brain.Log(Path.Combine(Keys.Folder, "voice-key"), -1, "Couldn't move the voice key into Keychain: " + error.Message); }
            voice.Key = Keys.Read("voice");
```

Delete `KeyFile` and `ReadVoiceKey()` entirely, and replace the whole `WriteVoiceKey` method with:

```csharp
        private void WriteVoiceKey(string key)
        {
            try
            {
                if (key == null) Keys.Forget("voice");
                else Keys.Save("voice", key);
            }
            catch (Exception error) { Brain.Log(Keys.Folder, -1, "Couldn't save the voice key: " + error.Message); }
        }
```

- [ ] **Step 5: Build, then run the smoke test**

Run: `dotnet build cross/Capri.Cross.csproj -c Release` → `0 Erreur(s)`.
Run the smoke command. Expected: `exit 0`, the last PASS line is `PASS keys: API keys save, read back and are forgotten in Keychain, and the old voice-key file moves in...`, no `FAIL`.

- [ ] **Step 6: Commit**

```bash
git add cross/Keys.cs cross/Pet.cs cross/Smoke.cs
git commit -m "the Mac keeps her API keys in Keychain, and moves an old voice-key file in"
```

---

### Task 2: Brain tiers in `Pet` (local, NVIDIA, paid key)

**Files:**
- Modify: `cross/Pet.cs` (`Settings` l.21-38, usings, constants ~l.124, constructor, `Menu()` ~l.849-923, `RefreshBrains`/`ShowBrains`/`PickBrain`/`FindBrain` ~l.1043-1265, `Save` ~l.1570)
- Modify: `cross/Smoke.cs` (fields, `FakeVoice`/`Serve` refactor, steps 22-26)

- [ ] **Step 1: Write the failing test**

In `cross/Smoke.cs`, add a field next to `fakeVoice`:

```csharp
        private TcpListener fakeBrain;
```

Pull the accept loop and header reading out of `FakeVoice`/`Serve` so the brain server can share them. Replace the whole `FakeVoice()` method with:

```csharp
        private string FakeVoice()
        {
            fakeVoice = Listen(Serve);
            return "http://127.0.0.1:" + ((IPEndPoint)fakeVoice.LocalEndpoint).Port;
        }

        // Stands in for NVIDIA's /v1/models and Ollama's /api/tags.
        private string FakeBrain()
        {
            fakeBrain = Listen(ServeBrain);
            return "http://127.0.0.1:" + ((IPEndPoint)fakeBrain.LocalEndpoint).Port;
        }

        private static TcpListener Listen(Action<NetworkStream> serve)
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Thread loop = new Thread(delegate()
            {
                while (true)
                {
                    TcpClient client;
                    try { client = listener.AcceptTcpClient(); }
                    catch (Exception) { return; }
                    try { using (client) serve(client.GetStream()); }
                    catch (Exception) { }
                }
            });
            loop.IsBackground = true;
            loop.Start();
            return listener;
        }

        // Reads up to the blank line after the headers; whatever came after it stays in read.
        private static int ReadHead(NetworkStream stream, List<byte> read)
        {
            byte[] chunk = new byte[4096];
            int end = -1;
            while (end < 0)
            {
                int got = stream.Read(chunk, 0, chunk.Length);
                if (got <= 0) return -1;
                for (int i = 0; i < got; i++) read.Add(chunk[i]);
                for (int i = 3; i < read.Count && end < 0; i++) if (read[i - 3] == 13 && read[i - 2] == 10 && read[i - 1] == 13 && read[i] == 10) end = i + 1;
            }
            return end;
        }

        private static void Answer(NetworkStream stream, int status, string type, byte[] reply)
        {
            byte[] top = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " " + (status == 200 ? "OK" : "Error") + "\r\nContent-Type: " + type + "\r\nContent-Length: " + reply.Length + "\r\nConnection: close\r\n\r\n");
            stream.Write(top, 0, top.Length);
            stream.Write(reply, 0, reply.Length);
            stream.Flush();
        }

        private void ServeBrain(NetworkStream stream)
        {
            List<byte> read = new List<byte>();
            int end = ReadHead(stream, read);
            if (end < 0) return;
            string[] lines = Encoding.ASCII.GetString(read.ToArray(), 0, end).Split(new[] { "\r\n" }, StringSplitOptions.None);
            string path = lines[0].Split(' ')[1];
            string key = "";
            foreach (string line in lines) if (line.StartsWith("Authorization: Bearer ", StringComparison.OrdinalIgnoreCase)) key = line.Substring(22).Trim();
            if (path.StartsWith("/api/tags", StringComparison.Ordinal)) Answer(stream, 200, "application/json", Encoding.UTF8.GetBytes("{\"models\":[{\"name\":\"gemma4:e2b\"},{\"name\":\"qwen3:4b\"}]}"));
            else if (path.StartsWith("/v1/models", StringComparison.Ordinal) && key == "nvapi-good-1234") Answer(stream, 200, "application/json", Encoding.UTF8.GetBytes("{\"data\":[{\"id\":\"openai/gpt-oss-20b\"},{\"id\":\"nvidia/nemotron-3.5-super-vl-120b-a12b\"}]}"));
            else if (path.StartsWith("/v1/models", StringComparison.Ordinal)) Answer(stream, 401, "application/json", Encoding.UTF8.GetBytes("{\"error\":\"Invalid key\"}"));
            else Answer(stream, 404, "application/json", Encoding.UTF8.GetBytes("{}"));
        }
```

In `Serve`, replace its first lines

```csharp
            List<byte> read = new List<byte>();
            byte[] chunk = new byte[4096];
            int end = -1;
            while (end < 0)
            {
                int got = stream.Read(chunk, 0, chunk.Length);
                if (got <= 0) return;
                for (int i = 0; i < got; i++) read.Add(chunk[i]);
                for (int i = 3; i < read.Count && end < 0; i++) if (read[i - 3] == 13 && read[i - 2] == 10 && read[i - 1] == 13 && read[i] == 10) end = i + 1;
            }
```
with
```csharp
            List<byte> read = new List<byte>();
            byte[] chunk = new byte[4096];
            int end = ReadHead(stream, read);
            if (end < 0) return;
```
and replace its last four lines (`byte[] top = ...` through `stream.Flush();`) with `Answer(stream, status, type, reply);`.

At the end of step 22, replace

```csharp
                Send(shelf, "quit");
                Finish(null);
                return true;
            }
```
with
```csharp
                Brain.Nvidia = FakeBrain();
                Brain.Ollama = Brain.Nvidia;
                pet.RefreshBrains();
                return true;
            }
            if (step == 23)
            {
                if (pet.OllamaModels == null) return false;
                if (string.Join(" | ", pet.OllamaModels) != "gemma4:e2b | qwen3:4b") throw new Exception("NORMAL didn't list her Ollama models: " + string.Join(" | ", pet.OllamaModels));
                pet.ChooseTier(1);
                if (pet.Saved.Tier != 1 || pet.Brain.Remote != null) throw new Exception("HIGHER with no key didn't stay on her local brain.");
                pet.ConnectText("hello");
                if (pet.BrainProblem != "That isn't an NVIDIA, OpenAI, Claude or DeepSeek key.") throw new Exception("A key in no known format wasn't refused: " + pet.BrainProblem);
                pet.ConnectText("nvapi-bad-0000");
                return true;
            }
            if (step == 24)
            {
                if (pet.BrainProblem == "Checking key...") return false;
                if (pet.BrainProblem != "Key rejected. Press Forget, then paste it again." || pet.Brain.Remote != null || Keys.Read("nvidia") != null) throw new Exception("A rejected NVIDIA key was kept or not explained: " + pet.BrainProblem);
                pet.ConnectText("nvapi-good-1234");
                return true;
            }
            if (step == 25)
            {
                const string Nemotron = "nvidia/nemotron-3.5-super-vl-120b-a12b";
                if (pet.Brain.Remote == null || pet.CloudModels == null)
                {
                    if (pet.BrainProblem != "Checking key...") throw new Exception("A good NVIDIA key didn't connect: " + pet.BrainProblem);
                    return false;
                }
                string json = pet.SettingsJson;
                if (pet.Saved.Tier != 1 || pet.Brain.Remote.Model != Nemotron || pet.Brain.Model != Nemotron || pet.Saved.NvidiaModel != Nemotron || Keys.Read("nvidia") != "nvapi-good-1234") throw new Exception("A good NVIDIA key didn't become her brain: " + pet.Saved.Tier + " | " + pet.Brain.Model + " | " + pet.Saved.NvidiaModel);
                if (json.IndexOf("nvapi", StringComparison.Ordinal) >= 0 || json.IndexOf("\"Tier\":1", StringComparison.Ordinal) < 0) throw new Exception("cross.json holds the key, or not the tier: " + json);
                pet.ShowBrains(pet.OllamaModels);
                if (string.Join(" | ", pet.CloudModels) != Nemotron + " | openai/gpt-oss-20b" || pet.BrainChoices != 2) throw new Exception("The Brain menu didn't list the NVIDIA models: " + string.Join(" | ", pet.CloudModels));
                pet.PickModel("openai/gpt-oss-20b");
                if (pet.Brain.Model != "openai/gpt-oss-20b" || pet.Saved.NvidiaModel != "openai/gpt-oss-20b") throw new Exception("Picking an NVIDIA model didn't stick.");
                checks.Add("PASS brain tiers: a key in no known format and a rejected key are explained and dropped, a good NVIDIA key goes in Keychain, picks its best model and never lands in cross.json");
                pet.ForgetText();
                if (pet.Saved.Tier != 0 || pet.Brain.Remote != null || Keys.Read("nvidia") != null || pet.Saved.NvidiaModel != null || pet.CloudModels != null) throw new Exception("Forget didn't drop the NVIDIA key and go back to NORMAL.");
                pet.ShowBrains(pet.OllamaModels);
                if (pet.BrainChoices != 2) throw new Exception("Back on NORMAL, the Brain menu didn't list Ollama models again.");
                checks.Add("PASS forget: Forget removes the key from Keychain and she goes back to her local brain");
                fakeBrain.Stop();
                Send(shelf, "quit");
                Finish(null);
                return true;
            }
```

- [ ] **Step 2: Run the build to verify it fails**

Run: `dotnet build cross/Capri.Cross.csproj -c Release`
Expected: FAIL with `error CS1061: 'Pet' does not contain a definition for 'OllamaModels'` (and similar for `ChooseTier`, `ConnectText`, `BrainProblem`, `CloudModels`, `SettingsJson`, `PickModel`, `ForgetText`, `Settings.Tier`).

- [ ] **Step 3: `Settings` fields and constants**

In `cross/Pet.cs`, add `using System.Text;` after `using System.Threading;`. In `Settings`, after `[DataMember] internal int Typing;` add:

```csharp
        [DataMember] internal int Tier;
        [DataMember] internal string NvidiaModel;
        [DataMember] internal string PaidModel;
```

Replace the `Offline` constant with:

```csharp
        internal const string Offline = "My brain's offline. Install Ollama from ollama.com, then run: ollama pull gemma4:e2b, or open Capri Mainframe for a free NVIDIA key.";
        internal const string OllamaPage = "https://ollama.com/download";
        internal const string FreeKeyPage = "https://build.nvidia.com/settings/api-keys";
        internal const string KeychainLocked = "Couldn't read your key from Keychain.";
        private const string BrainHeard = "(brain key)";
```

Next to `private MenuItem brainMenu;` add:

```csharp
        private string[] ollamaModels;
        private List<string> cloudModels;
        internal string BrainProblem;
```

- [ ] **Step 4: Load the tier's cloud brain at startup**

In the constructor, right after `voice.Key = Keys.Read("voice");` add:

```csharp
            Saved.Tier = Math.Max(0, Math.Min(2, Saved.Tier));
            Brain.Remote = TierCloud();
            if (Brain.Remote != null) Brain.Model = Brain.Remote.Model;
```

- [ ] **Step 5: Tier logic (ported from `app/Capri.cs` l.3712-3830)**

Add these methods to `Pet`, just before `internal void FindBrain()`:

```csharp
        internal string[] OllamaModels { get { return ollamaModels; } }
        internal List<string> CloudModels { get { return cloudModels; } }

        internal string SettingsJson
        {
            get
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    new DataContractJsonSerializer(typeof(Settings)).WriteObject(stream, Saved);
                    return Encoding.UTF8.GetString(stream.ToArray());
                }
            }
        }

        private Cloud TierCloud()
        {
            if (Saved.Tier == 0) return null;
            string key = Keys.Read(Saved.Tier == 1 ? "nvidia" : "paid");
            if (key == null)
            {
                if (Keys.Problem != null) BrainProblem = KeychainLocked;
                return null;
            }
            return new Cloud(key, Saved.Tier == 1 ? Saved.NvidiaModel : Saved.PaidModel);
        }

        internal void ChooseTier(int tier)
        {
            tier = Math.Max(0, Math.Min(2, tier));
            if (tier == Saved.Tier) return;
            Brain.Cancel();
            ++asking;
            Saved.Tier = tier;
            Save();
            BrainProblem = null;
            cloudModels = null;
            Brain.Remote = TierCloud();
            Brain.Model = Brain.Remote == null ? null : Brain.Remote.Model;
            Brain.Forget();
            FindBrain();
            ShowState();
        }

        internal void ConnectText(string key)
        {
            key = (key ?? "").Trim();
            if (Cloud.KindOf(key) == null) { ShowProblem("That isn't an NVIDIA, OpenAI, Claude or DeepSeek key."); return; }
            ListCloud(key, true);
        }

        internal void ForgetText()
        {
            string name = Saved.Tier == 1 ? "nvidia" : "paid";
            try { Keys.Forget(name); }
            catch (Exception error) { Brain.Log(Keys.Folder, -1, "Couldn't forget the " + name + " key: " + error.Message); ShowProblem("Couldn't remove your key from Keychain."); return; }
            Brain.Cancel();
            ++asking;
            if (Saved.Tier == 1) Saved.NvidiaModel = null;
            else Saved.PaidModel = null;
            Brain.Remote = null;
            Brain.Model = null;
            Brain.Forget();
            cloudModels = null;
            BrainProblem = null;
            Saved.Tier = 0;
            Save();
            FindBrain();
            ShowState();
        }

        internal void PickModel(string model)
        {
            Cloud remote = Brain.Remote;
            if (remote == null) { PickBrain(model); return; }
            if (string.IsNullOrEmpty(model) || model == remote.Model) return;
            Brain.Cancel();
            ++asking;
            remote.Model = model;
            Brain.Model = model;
            Brain.Forget();
            Keep(remote);
            ShowState();
        }

        private void Keep(Cloud remote)
        {
            if (remote.Kind == "nvidia") Saved.NvidiaModel = remote.Model;
            else Saved.PaidModel = remote.Model;
            Save();
        }

        private void ListCloud(string key, bool connecting)
        {
            Cloud probe = new Cloud(key, null);
            if (connecting) ShowProblem("Checking key...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<string> found = null;
                string problem = null;
                try { found = Brain.CloudModels(key); }
                catch (BrainException error) { problem = error.Message; }
                catch (Exception error)
                {
                    Brain.Log("https://" + probe.Company.ToLowerInvariant() + "/models", -1, error.Message);
                    Cloud now = Brain.Remote;
                    problem = now != null && now.Key == key && now.Model != null ? probe.Company + " is slow. Using " + now.Model + "." : "Couldn't reach " + probe.Company + ". Check your internet.";
                }
                Dispatcher.UIThread.Post(delegate
                {
                    if (problem != null)
                    {
                        if (connecting || Brain.Remote != null) ShowProblem(Brain.Brief(problem) ?? (problem.Length > 60 ? problem.Substring(0, 57) + "..." : problem));
                        return;
                    }
                    if (connecting)
                    {
                        Cloud remote = new Cloud(key, Brain.DefaultCloudModel(probe.Kind, found));
                        if (remote.Model == null) { ShowProblem("That key can't use any chat models."); return; }
                        try { Keys.Save(remote.Kind == "nvidia" ? "nvidia" : "paid", key); }
                        catch (Exception error) { Brain.Log(Keys.Folder, -1, "Couldn't save the brain key: " + error.Message); ShowProblem("Couldn't save your key in Keychain."); return; }
                        Brain.Cancel();
                        ++asking;
                        Brain.Remote = remote;
                        Brain.Forget();
                        Saved.Tier = remote.Kind == "nvidia" ? 1 : 2;
                        Keep(remote);
                    }
                    Cloud current = Brain.Remote;
                    if (current == null || current.Key != key) return;
                    BrainProblem = null;
                    cloudModels = found;
                    if (current.Model == null || !found.Contains(current.Model))
                    {
                        current.Model = Brain.DefaultCloudModel(current.Kind, found) ?? current.Model;
                        Keep(current);
                    }
                    Brain.Model = current.Model;
                    ShowBrains(ollamaModels);
                    if (connecting) Reply(++asking, BrainHeard, "Brain online. I'm thinking with " + current.Name + " " + current.Model + " now.");
                });
            });
        }

        private void ShowProblem(string problem)
        {
            BrainProblem = problem;
            ShowState();
        }

        private void ShowState() { }
```

(`ShowState` is empty until Task 3 gives it a window to redraw.)

- [ ] **Step 6: Brain menu and `FindBrain` follow the tier**

Make `RefreshBrains` `internal` and give it a cloud branch at the top:

```csharp
        internal void RefreshBrains()
        {
            Cloud remote = Brain.Remote;
            if (remote != null) { ShowBrains(ollamaModels); ListCloud(remote.Key, false); return; }
            ThreadPool.QueueUserWorkItem(delegate
```
(the rest of the method is unchanged).

Replace `ShowBrains` with:

```csharp
        internal void ShowBrains(string[] found)
        {
            ollamaModels = found;
            brainMenu.Items.Clear();
            Cloud remote = Brain.Remote;
            if (remote != null)
            {
                if (cloudModels == null) brainMenu.Items.Add(new MenuItem { Header = "Loading " + remote.Name + " models...", IsEnabled = false });
                else
                {
                    int shown = 0;
                    foreach (string name in cloudModels)
                    {
                        if (shown++ >= 30) break;
                        string model = name;
                        MenuItem pick = new MenuItem { Header = model, ToggleType = MenuItemToggleType.Radio, GroupName = "brain", IsChecked = model == remote.Model };
                        pick.Click += delegate { PickModel(model); };
                        brainMenu.Items.Add(pick);
                    }
                }
            }
            else if (found == null || found.Length == 0) brainMenu.Items.Add(new MenuItem { Header = "No models yet. Install Ollama, then pull one.", IsEnabled = false });
            else
            {
                foreach (string name in found)
                {
                    string model = name;
                    MenuItem pick = new MenuItem { Header = model, ToggleType = MenuItemToggleType.Radio, GroupName = "brain", IsChecked = model == Brain.Model };
                    pick.Click += delegate { PickModel(model); };
                    brainMenu.Items.Add(pick);
                }
            }
            ShowState();
        }
```

At the end of `PickBrain` (after `Save();`) add `ShowState();`.

In `FindBrain`, add at the very top:

```csharp
            Cloud remote = Brain.Remote;
            if (remote != null) { ListCloud(remote.Key, false); return; }
```
and change `if (Brain.Model != null || model == null) return;` to `if (Brain.Remote != null || Brain.Model != null || model == null) return;`.

- [ ] **Step 7: Build, then run the smoke test**

Run: `dotnet build cross/Capri.Cross.csproj -c Release` → `0 Erreur(s)`.
Run the smoke command. Expected: `exit 0`, new lines `PASS brain tiers: ...` and `PASS forget: ...`, no `FAIL`.

- [ ] **Step 8: Commit**

```bash
git add cross/Pet.cs cross/Smoke.cs
git commit -m "the Mac gets Windows' three brain tiers: her local brain, a free NVIDIA key or a paid key"
```

---

### Task 3: The Capri Mainframe window (BRAIN section)

**Files:**
- Create: `cross/Mainframe.cs`
- Modify: `cross/Pet.cs` (`Menu()`, `ShowState`, new `OpenMainframe`/`Frame`)
- Modify: `cross/Smoke.cs` (steps 25-29)

- [ ] **Step 1: Write the failing test**

At the end of step 25 in `cross/Smoke.cs`, replace

```csharp
                fakeBrain.Stop();
                Send(shelf, "quit");
                Finish(null);
                return true;
            }
```
with
```csharp
                pet.OpenMainframe();
                return true;
            }
            if (step == 26)
            {
                Mainframe frame = pet.Frame;
                if (frame == null || !frame.IsVisible) throw new Exception("Capri Mainframe didn't open.");
                if (pet.OllamaModels == null || !frame.TiersFit) return false;
                if (frame.Choices != "gemma4:e2b | qwen3:4b" || frame.OllamaShown) throw new Exception("NORMAL didn't list her Ollama models: " + frame.Choices + " | " + frame.TextStatus);
                frame.Choose("qwen3:4b");
                if (pet.Brain.Model != "qwen3:4b" || pet.Saved.Model != "qwen3:4b" || !frame.TextStatus.StartsWith("tier 0 | Ollama · qwen3:4b | Running on this", StringComparison.Ordinal)) throw new Exception("Picking an Ollama model in the Mainframe didn't stick: " + frame.TextStatus);
                Snap(frame.Content as Control, "mainframe.png");
                frame.PickTier(1);
                if (pet.Saved.Tier != 1 || !frame.TextStatus.EndsWith("Paste your free NVIDIA key | Connect", StringComparison.Ordinal)) throw new Exception("HIGHER didn't ask for an NVIDIA key: " + frame.TextStatus);
                frame.Type("nvapi-good-1234");
                frame.PressConnect();
                return true;
            }
            if (step == 27)
            {
                Mainframe frame = pet.Frame;
                if (pet.Brain.Remote == null || pet.CloudModels == null)
                {
                    if (pet.BrainProblem != "Checking key...") throw new Exception("Connect in the Mainframe didn't connect: " + pet.BrainProblem);
                    return false;
                }
                if (!frame.TextStatus.EndsWith("NVIDIA key saved, ends 1234 | Forget", StringComparison.Ordinal) || frame.Choices != "nvidia/nemotron-3.5-super-vl-120b-a12b | openai/gpt-oss-20b") throw new Exception("The Mainframe didn't show the saved NVIDIA key and its models: " + frame.TextStatus + " | " + frame.Choices);
                Snap(frame.Content as Control, "mainframe-nvidia.png");
                frame.PressConnect();
                if (pet.Saved.Tier != 0 || Keys.Read("nvidia") != null || !frame.TextStatus.StartsWith("tier 0 |", StringComparison.Ordinal) || !frame.TextStatus.EndsWith("no key row", StringComparison.Ordinal)) throw new Exception("Forget in the Mainframe didn't go back to NORMAL: " + frame.TextStatus);
                Brain.Ollama = "http://127.0.0.1:1";
                pet.RefreshBrains();
                return true;
            }
            if (step == 28)
            {
                Mainframe frame = pet.Frame;
                if (pet.OllamaModels != null) return false;
                if (!frame.OllamaShown || frame.TextStatus.IndexOf("ollama pull gemma4:e2b", StringComparison.Ordinal) < 0) throw new Exception("With no Ollama, the Mainframe didn't say how to get a brain: " + frame.TextStatus);
                checks.Add("PASS mainframe: Capri Mainframe opens from her menu, lists her Ollama models on NORMAL, connects and forgets an NVIDIA key on HIGHER, and says how to get Ollama when it's missing");
                frame.Close();
                fakeBrain.Stop();
                Send(shelf, "quit");
                Finish(null);
                return true;
            }
```

- [ ] **Step 2: Run the build to verify it fails**

Run: `dotnet build cross/Capri.Cross.csproj -c Release`
Expected: FAIL with `error CS0246: The type or namespace name 'Mainframe' could not be found`.

- [ ] **Step 3: Write `cross/Mainframe.cs`**

```csharp
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;

namespace CapriDesktop
{
    // Capri Mainframe on Mac and Linux. For now only BRAIN: Windows' three tiers. It holds no state of its own:
    // it calls Pet, and Render() redraws it from Pet.
    internal sealed class Mainframe : Window
    {
        private static readonly string[,] Tiers = { { "NORMAL", "Free · local model" }, { "HIGHER", "Free · NVIDIA API" }, { "MAX INTELLIGENCE", "ChatGPT or Claude API" } };
        private readonly Pet pet;
        private readonly StackPanel page = new StackPanel { Margin = new Thickness(24, 20, 24, 24), Spacing = 8 };
        private readonly TextBlock brainName = Line(13, Look.Ink, true);
        private readonly TextBlock brainNote = Line(11, Look.Dim, false);
        private readonly TextBlock tierNote = Line(10.5, Look.Dim, false);
        private readonly Button models = Make("");
        private readonly Button refresh = Make("Refresh");
        private readonly MenuFlyout menu = new MenuFlyout();
        private readonly RadioButton[] rungs = new RadioButton[3];
        private readonly Button freeKey = Make("Free key");
        private readonly TextBox keyBox = new TextBox { PasswordChar = '•', Width = 230, FontFamily = Look.Mono };
        private readonly Button connect = Make("Connect");
        private readonly Button ollama = Make("Get Ollama");
        private readonly List<Panel> rows = new List<Panel>();
        private StackPanel keyRow;
        private bool syncing;
        private bool saved;
        private int tier;

        internal Mainframe(Pet pet)
        {
            this.pet = pet;
            Title = "Capri Mainframe";
            Width = 384;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            Background = new SolidColorBrush(Look.Panel);
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            page.Children.Add(new TextBlock { Text = "MAINFRAME", FontFamily = Look.Mono, FontSize = 20, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Look.Red) });
            page.Children.Add(new TextBlock { Text = "BRAIN", FontFamily = Look.Mono, FontSize = 12, Foreground = new SolidColorBrush(Look.Cyan), Margin = new Thickness(0, 8, 0, 0) });
            page.Children.Add(brainName);
            page.Children.Add(brainNote);
            models.Flyout = menu;
            refresh.Click += delegate { pet.RefreshBrains(); };
            page.Children.Add(Row(models, refresh));
            page.Children.Add(tierNote);
            for (int i = 0; i < rungs.Length; i++)
            {
                int picked = i;
                RadioButton rung = new RadioButton { GroupName = "tier", Content = Tiers[i, 0] + "  " + Tiers[i, 1], FontFamily = Look.Mono, Foreground = new SolidColorBrush(Look.Ink) };
                rung.IsCheckedChanged += delegate { if (!syncing && rung.IsChecked == true) pet.ChooseTier(picked); };
                rungs[i] = rung;
            }
            freeKey.Click += delegate { Pet.Browse(Pet.FreeKeyPage); };
            page.Children.Add(Row(rungs[0]));
            page.Children.Add(Row(rungs[1], freeKey));
            page.Children.Add(Row(rungs[2]));
            connect.Click += delegate { Connect(); };
            keyBox.TextChanged += delegate { Relabel(); };
            keyBox.AddHandler(KeyDownEvent, delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; Connect(); } }, RoutingStrategies.Tunnel);
            keyRow = Row(keyBox, connect);
            page.Children.Add(keyRow);
            ollama.Click += delegate { Pet.Browse(Pet.OllamaPage); };
            page.Children.Add(ollama);
            Content = new Border { BorderBrush = new SolidColorBrush(Look.Red), BorderThickness = new Thickness(1), Child = page };
            Render();
        }

        private static TextBlock Line(double size, Color colour, bool bold)
        {
            return new TextBlock { FontFamily = Look.Mono, FontSize = size, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal, Foreground = new SolidColorBrush(colour), TextWrapping = TextWrapping.Wrap };
        }

        private static Button Make(string text)
        {
            return new Button { Content = text, FontFamily = Look.Mono, Foreground = new SolidColorBrush(Look.Ink), BorderBrush = new SolidColorBrush(Look.Edge), BorderThickness = new Thickness(1) };
        }

        private StackPanel Row(params Control[] parts)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            foreach (Control part in parts) row.Children.Add(part);
            rows.Add(row);
            return row;
        }

        internal void Render()
        {
            tier = pet.Saved.Tier;
            Cloud remote = pet.Brain.Remote;
            string[] local = pet.OllamaModels;
            List<string> choices = new List<string>();
            string chosen = null;
            syncing = true;
            for (int i = 0; i < rungs.Length; i++) rungs[i].IsChecked = i == tier;
            syncing = false;
            if (remote != null)
            {
                brainName.Text = remote.Name + " · " + (remote.Model ?? "...");
                brainNote.Text = "Using your " + remote.Company + " key";
                if (pet.CloudModels != null) choices.AddRange(pet.CloudModels);
                chosen = remote.Model;
            }
            else if (local != null && local.Length > 0)
            {
                chosen = pet.Brain.Model ?? local[0];
                brainName.Text = "Ollama · " + chosen;
                brainNote.Text = "Running on this " + (OperatingSystem.IsMacOS() ? "Mac" : "computer");
                choices.AddRange(local);
            }
            else
            {
                brainName.Text = "No brain yet";
                brainNote.Text = "Install Ollama, then run: ollama pull gemma4:e2b";
            }
            if (pet.BrainProblem != null) brainNote.Text = pet.BrainProblem;
            menu.Items.Clear();
            for (int i = 0; i < choices.Count && i < 30; i++)
            {
                string model = choices[i];
                MenuItem pick = new MenuItem { Header = model, ToggleType = MenuItemToggleType.Radio, GroupName = "mainframe-brain", IsChecked = model == chosen };
                pick.Click += delegate { pet.PickModel(model); };
                menu.Items.Add(pick);
            }
            models.Content = chosen ?? "Pick a model";
            models.IsVisible = choices.Count > 0;
            ollama.IsVisible = remote == null && (local == null || local.Length == 0);
            tierNote.Text = tier == 1 ? "Most NVIDIA models can't see your screen." : "Pick one. Only one brain runs at a time.";
            keyRow.IsVisible = tier > 0;
            saved = remote != null;
            keyBox.Watermark = saved ? (tier == 1 ? "NVIDIA key saved, ends " : "Key saved, ends ") + Voice.Tail(remote.Key) : tier == 1 ? "Paste your free NVIDIA key" : "Paste a ChatGPT or Claude key";
            Relabel();
        }

        private void Relabel()
        {
            bool forget = saved && string.IsNullOrWhiteSpace(keyBox.Text);
            connect.Content = forget ? "Forget" : "Connect";
            connect.Foreground = new SolidColorBrush(forget ? Look.Red : Look.Ink);
        }

        private void Connect()
        {
            string typed = (keyBox.Text ?? "").Trim();
            if (typed.Length > 0) { keyBox.Text = ""; pet.ConnectText(typed); }
            else if (saved) pet.ForgetText();
            else keyBox.Focus();
        }

        internal void PickTier(int pick) { rungs[pick].IsChecked = true; }
        internal void Type(string key) { keyBox.Text = key; }
        internal void PressConnect() { Connect(); }
        internal void Choose(string model) { pet.PickModel(model); }
        internal bool OllamaShown { get { return ollama.IsVisible; } }

        internal string Choices
        {
            get
            {
                List<string> shown = new List<string>();
                foreach (object item in menu.Items) { MenuItem entry = item as MenuItem; if (entry != null) shown.Add(entry.Header as string); }
                return string.Join(" | ", shown);
            }
        }

        internal string TextStatus { get { return "tier " + tier + " | " + brainName.Text + " | " + brainNote.Text + " | " + (keyRow.IsVisible ? keyBox.Watermark + " | " + connect.Content : "no key row"); } }

        // Every row fits across the window and the window fits on the screen.
        internal bool TiersFit
        {
            get
            {
                double room = page.Bounds.Width;
                if (room <= 0) return false;
                foreach (Panel row in rows)
                {
                    if (!row.IsVisible) continue;
                    row.Measure(Size.Infinity);
                    bool fits = row.DesiredSize.Width <= room + 0.5;
                    row.InvalidateMeasure();
                    if (!fits) return false;
                }
                Screen screen = Screens.ScreenFromWindow(this);
                return screen == null || Bounds.Height * DesktopScaling <= screen.WorkingArea.Height;
            }
        }
    }
}
```

- [ ] **Step 4: Open it from `Pet`**

In `cross/Pet.cs`, next to `private string[] ollamaModels;` add `private Mainframe mainframe;`. Replace `private void ShowState() { }` with:

```csharp
        private void ShowState() { if (mainframe != null) mainframe.Render(); }

        internal Mainframe Frame { get { return mainframe; } }

        internal void OpenMainframe()
        {
            if (mainframe == null)
            {
                mainframe = new Mainframe(this);
                mainframe.Closed += delegate { mainframe = null; };
                mainframe.Show();
            }
            else mainframe.Activate();
            RefreshBrains();
        }
```

In `Menu()`, after the `brainMenu` lines (`brainMenu.Items.Add(new MenuItem { Header = "Looking for models...", IsEnabled = false });`) add:

```csharp
            MenuItem mainframeItem = new MenuItem { Header = "Capri Mainframe" };
            mainframeItem.Click += delegate { OpenMainframe(); };
```
and after `menu.Items.Add(brainMenu);` add `menu.Items.Add(mainframeItem);`.

- [ ] **Step 5: Build, then run the smoke test**

Run: `dotnet build cross/Capri.Cross.csproj -c Release` → `0 Erreur(s)`.
Run the smoke command. Expected: `exit 0`, new line `PASS mainframe: ...`, no `FAIL`. Open `/tmp/capri-smoke/mainframe.png` and `/tmp/capri-smoke/mainframe-nvidia.png` and check the look (dark panel, red MAINFRAME title, red border, three tiers, nothing cut off).

- [ ] **Step 6: Commit**

```bash
git add cross/Mainframe.cs cross/Pet.cs cross/Smoke.cs
git commit -m "Capri Mainframe opens on Mac and Linux with her BRAIN tiers"
```

---

### Task 4: Manual check on the M2, then wrap up

**Files:**
- Modify: `docs/mac-parity-todo.md`

- [ ] **Step 1: Run Capri for real**

```bash
dotnet build cross/Capri.Cross.csproj -c Release && cross/bin/Release/net10.0/Capri
```

Check, in order (the user does this):
1. Right-click her → **Capri Mainframe** opens.
2. NORMAL with Ollama running and `gemma4:e2b` pulled (`brew install ollama && ollama pull gemma4:e2b`): the model shows, she answers in the chat.
3. HIGHER → **Free key** opens the NVIDIA page → paste the `nvapi-...` key → Connect. macOS may ask for Keychain access: allow it. She says "Brain online...". She answers in the chat.
4. Quit and relaunch Capri: she's still on HIGHER with the same model (key read back from Keychain). Note: an unsigned build that was rebuilt may ask for Keychain access again; that's expected.
5. **Keychain Access** app → search `capri`: one `nvidia` entry. `~/.config/Capri/cross.json` has `"Tier":1` and no `nvapi`.
6. Forget → back on NORMAL, the `capri`/`nvidia` entry is gone.
7. Settings > Voice: paste the ElevenLabs key → she speaks; a `capri`/`voice` entry appears in Keychain Access, no `voice-key` file in `~/.config/Capri/`.

- [ ] **Step 2: Update the roadmap**

In `docs/mac-parity-todo.md`, tick `Étape 1` (`- [x]`), move `← EN COURS` to `Étape 2`, and replace the "Étape 1 — où on en est" section with a short "Étape 1 — fait" note (branch `mac-step1-mainframe`, what landed, anything left over from the manual check).

- [ ] **Step 3: Commit**

```bash
git add docs/mac-parity-todo.md
git commit -m "docs: step 1 of the Mac parity roadmap is done"
```
