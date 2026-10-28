using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SkiaSharp;

namespace CapriDesktop
{
    // "Check my screen" on Mac and Linux: a screenshot from the system's own tool, shrunk to a JPEG her brain can look at.
    internal static class Eyes
    {
        // A whole-screen picture's file bytes, or null when no tool here could take one. The smoke test hands in its own.
        internal static Func<byte[]> Shoot = Screenshot;

        internal static string Missing
        {
            get
            {
                return OperatingSystem.IsMacOS()
                    ? "You couldn't take a screenshot. Tell them to allow Capri under System Settings > Privacy & Security > Screen Recording, then open you again."
                    : "You couldn't take a screenshot. Tell them to install grim (Wayland), scrot (X11) or gnome-screenshot (GNOME), then ask again.";
            }
        }

        // The tools that can take a screenshot here, best first, with "{0}" where the file to write goes.
        internal static List<string[]> Tools(bool mac, string session, string desktop)
        {
            List<string[]> tools = new List<string[]>();
            if (mac)
            {
                tools.Add(new[] { "screencapture", "-x", "-t", "jpg", "{0}" });
                return tools;
            }
            string where = (desktop ?? "").ToLowerInvariant();
            string[] spectacle = { "spectacle", "-b", "-n", "-f", "-o", "{0}" };
            string[] gnome = { "gnome-screenshot", "-f", "{0}" };
            if (string.Equals(session, "wayland", StringComparison.OrdinalIgnoreCase))
            {
                if (where.Contains("kde")) tools.Add(spectacle);
                if (where.Contains("gnome")) tools.Add(gnome);
                tools.Add(new[] { "grim", "{0}" });
                if (!where.Contains("kde")) tools.Add(spectacle);
                if (!where.Contains("gnome")) tools.Add(gnome);
                return tools;
            }
            tools.Add(new[] { "import", "-window", "root", "{0}" });
            tools.Add(new[] { "scrot", "{0}" });
            tools.Add(new[] { "maim", "{0}" });
            tools.Add(gnome);
            tools.Add(spectacle);
            tools.Add(new[] { "xfce4-screenshooter", "-f", "-s", "{0}" });
            return tools;
        }

        private static byte[] Screenshot()
        {
            bool mac = OperatingSystem.IsMacOS();
            foreach (string[] tool in Tools(mac, Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")))
            {
                string path = Path.Combine(Path.GetTempPath(), "capri-screen-" + Guid.NewGuid().ToString("N") + (mac ? ".jpg" : ".png"));
                try
                {
                    if (!Take(tool, path)) continue;
                    byte[] shot = File.ReadAllBytes(path);
                    if (shot.Length > 0) return shot;
                }
                catch (Exception) { }
                finally
                {
                    try { File.Delete(path); }
                    catch (Exception) { }
                }
            }
            return null;
        }

        // A tool that isn't installed throws when it starts, so the next one gets a turn.
        private static bool Take(string[] tool, string path)
        {
            ProcessStartInfo start = new ProcessStartInfo(tool[0]) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            for (int i = 1; i < tool.Length; i++) start.ArgumentList.Add(tool[i] == "{0}" ? path : tool[i]);
            try
            {
                using (Process process = Process.Start(start))
                {
                    if (process == null) return false;
                    process.StandardOutput.ReadToEndAsync();
                    process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(10000))
                    {
                        try { process.Kill(); }
                        catch (Exception) { }
                        return false;
                    }
                    return process.ExitCode == 0 && File.Exists(path);
                }
            }
            catch (Exception) { return false; }
        }

        // The picture as a JPEG at most this wide, the size her brain looks at on Windows too. Null if it can't be read.
        internal static byte[] Jpeg(byte[] picture, int widest)
        {
            if (picture == null || picture.Length == 0) return null;
            // Skia throws, rather than giving back null, for bytes it can't read.
            SKBitmap read;
            try { read = SKBitmap.Decode(picture); }
            catch (Exception) { return null; }
            using (SKBitmap original = read)
            {
                if (original == null || original.Width <= 0 || original.Height <= 0) return null;
                SKBitmap scaled = original;
                if (original.Width > widest)
                {
                    scaled = original.Resize(new SKImageInfo(widest, Math.Max(1, (int)((long)original.Height * widest / original.Width))), SKFilterQuality.Medium);
                    if (scaled == null) return null;
                }
                try
                {
                    using (SKImage image = SKImage.FromBitmap(scaled))
                    using (SKData data = image == null ? null : image.Encode(SKEncodedImageFormat.Jpeg, 80))
                        return data == null ? null : data.ToArray();
                }
                finally
                {
                    if (scaled != original) scaled.Dispose();
                }
            }
        }
    }
}
