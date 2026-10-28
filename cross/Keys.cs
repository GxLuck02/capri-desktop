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
