using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace CapriDesktop
{
    internal sealed class Smoke
    {
        private readonly Pet pet;
        private readonly string folder;
        private readonly List<string> checks = new List<string>();
        private readonly List<Process> shelves = new List<Process>();
        private readonly List<short[]> sounds = new List<short[]>();
        private int step;
        private double started = -1;
        private double stepStart = -1;
        private double settled = -1;
        private double coverAt = -1;
        private double hideAt = -1;
        private bool finished;
        private Process shelf;
        private Process cover;
        private PixelRect shelfAsked;
        private PixelRect coverAsked;
        private Spot shelfSpot;
        private PixelPoint hipBefore;
        private int dx;
        private int dy;
        private int arrivalsBefore;
        private double outFrom = -1;
        private TcpListener fakeVoice;
        private TcpListener fakeBrain;
        private TcpListener hub;
        private readonly Dictionary<string, byte[]> hubFiles = new Dictionary<string, byte[]>();
        private readonly List<string> hubAsked = new List<string>();
        private Engine bench;
        private Pick testPick;
        private readonly List<string> freeHeard = new List<string>();
        private byte[] voiceTone;
        private int voicedBefore;
        private Process sleeper;
        private int guardedPid;
        private double killedAt;
        private readonly List<string> voiceAsked = new List<string>();
        private readonly List<byte[]> spoken = new List<byte[]>();

        private bool blinked;
        private bool slept;
        private int napStage;
        private int moveStage;
        private PixelPoint moveTo;
        private Screen moveFrom;
        private readonly List<string> moveLines = new List<string>();
        private double noticedBefore = -1;
        private bool poppedAgain;
        private bool hushGlance;
        private int attention;
        private string attentionAsk, attentionSulk;
        private bool attentionAsked, attentionSulked;
        private double emptyAt;
        private Func<byte[]> peekWas;
        private readonly List<string> glanceAsks = new List<string>();
        private const string GlanceSaid = "SAW: a racing game, a red car in first place. SAY: First place? Don't choke now.";

        private static byte[] FakeScreen(SkiaSharp.SKColor color)
        {
            using (SkiaSharp.SKBitmap screen = new SkiaSharp.SKBitmap(320, 180))
            {
                screen.Erase(color);
                using (SkiaSharp.SKImage image = SkiaSharp.SKImage.FromBitmap(screen))
                using (SkiaSharp.SKData png = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100)) return png.ToArray();
            }
        }

        internal Smoke(Pet pet, string folder)
        {
            this.pet = pet;
            this.folder = folder;
            Hands.PleaseRoll = delegate { return 1; };
            Directory.CreateDirectory(folder);
            if (File.Exists(Path.Combine(folder, "me.md"))) File.Delete(Path.Combine(folder, "me.md"));
            if (File.Exists(pet.LoginFile)) File.Delete(pet.LoginFile);
            foreach (string name in new[] { "nvidia", "paid", "smoke" }) Keys.Forget(name);
            pet.ForgetVoice();
            foreach (string old in new[] { "brain", "voice", "t", "tv" }) if (Directory.Exists(Path.Combine(folder, old))) Directory.Delete(Path.Combine(folder, old), true);
        }

        internal void Tick(double now)
        {
            if (finished) return;
            if (started < 0) started = now;
            if (stepStart < 0) stepStart = now;
            if (now - started > 420) { Finish("Timed out at step " + step + "."); return; }
            if (now - stepStart > 15) { checks.Add("INFO " + Seen()); Finish("Step " + step + " took longer than 15 seconds."); return; }
            try
            {
                if (Step(now))
                {
                    step++;
                    stepStart = now;
                }
            }
            catch (Exception error) { Finish(error.Message); }
        }

        private int Px(double dip) { return (int)Math.Round(dip * pet.DesktopScaling); }

        private PixelRect Work()
        {
            Screen screen = pet.Screens.Primary ?? pet.ScreenHere();
            if (screen == null) throw new Exception("No screen was found.");
            return screen.WorkingArea;
        }

        private bool Sitting()
        {
            Ledge seat = pet.Seat;
            return seat != null && pet.Hip.Y == seat.Y;
        }

        private bool Step(double now)
        {
            if (step == 0)
            {
                foreach (Clip clip in pet.Clips) if (!clip.Even || clip.Strip.PixelSize.Height != 384) throw new Exception("A sprite strip doesn't split into even 384 px cells.");
                checks.Add("PASS sprites: all " + pet.Clips.Count + " clips, standing and perched, load from the app and split into even cells");
                foreach (string clipName in new[] { "morning", "late", "miss" })
                {
                    byte[] hello = Pet.Hello(clipName);
                    if (hello == null || Voice.Seconds(hello) < 1 || Voice.Seconds(hello) > 5) throw new Exception("The " + clipName + " startup recording is missing or the wrong length.");
                }
                if (Brain.Greeting(8) != "Good morning, choom." || Brain.Greeting(23) != "Isn't it late, choom?" || Brain.Greeting(15) != "Miss me, choom?" || Brain.GreetingClip(15) != "miss") throw new Exception("Startup greetings came at the wrong hours.");
                checks.Add("PASS startup voice: her giggle greetings for the morning, late at night and the rest of the day are packed in the app");
                if (pet.Arrivals != 1) throw new Exception("She didn't glitch in when she started.");
                checks.Add("PASS glitch in: she glitches onto the screen when she starts, then drops");
                if (pet.ActualTransparencyLevel == WindowTransparencyLevel.Transparent) checks.Add("PASS her window is see-through");
                else checks.Add("LIMIT her window isn't see-through here (" + pet.ActualTransparencyLevel + "); on Linux that needs a compositor");
                checks.Add("INFO window list: " + pet.Desk.Name);
                return true;
            }
            if (step == 1)
            {
                if (pet.Falling || pet.Arriving) return false;
                if (!Sitting()) throw new Exception("She didn't sit down after falling in.");
                checks.Add("PASS falling: she drops in from the top of the screen and sits on " + (pet.Seat.Floor ? "the bottom of the work area" : "the top of a window below her"));
                Snap(pet.Content as Control, "pet.png");
                pet.Grab();
                if (pet.Seat != null || pet.Falling || !pet.Dragging) throw new Exception("Grabbing her didn't lift her off her seat.");
                PixelRect area = Work();
                pet.Move(pet.Seated(area.X + area.Width / 2, area.Bottom - Px(100)));
                if (!pet.Drop()) throw new Exception("Dropping her just above the floor didn't make her fall.");
                return true;
            }
            if (step == 2)
            {
                if (pet.Falling) return false;
                if (!Sitting()) throw new Exception("A dropped Capri didn't sit down again.");
                checks.Add("PASS drag and drop: grabbing her lifts her off, dropping her near a ledge makes her fall and sit on it");
                PixelPoint hip = pet.Hip;
                pet.Resize(320);
                if (pet.Hip != hip) throw new Exception("Resizing moved her off her seat: " + hip + " to " + pet.Hip + ".");
                pet.Resize(240);
                if (pet.Hip != hip) throw new Exception("Resizing back moved her off her seat.");
                checks.Add("PASS size: changing her size keeps her sitting on the same spot");
                Point? petSpot = pet.BodySpot();
                if (petSpot == null) throw new Exception("Couldn't find her body to pet.");
                PixelPoint petLeft = pet.PointToScreen(new Point(petSpot.Value.X - 12, petSpot.Value.Y));
                PixelPoint petRight = pet.PointToScreen(new Point(petSpot.Value.X + 12, petSpot.Value.Y));
                int petsBefore = pet.Pets;
                for (int i = 0; i < 6; i++) pet.Stroke(petLeft);
                pet.Stroke(petRight);
                pet.Stroke(petLeft);
                bool earlyPet = pet.Pets != petsBefore;
                for (int i = 0; i < 4; i++) pet.Stroke(i % 2 == 0 ? petRight : petLeft);
                bool smiled = pet.Pets == petsBefore + 1 && pet.Smiling && pet.Talk.Quiet;
                pet.PetLine(pet.Pets);
                bool spoke = Array.IndexOf(Pet.PetLines, pet.Talk.Bubble.Sheet.Reply) >= 0;
                for (int i = 0; i < 8; i++) pet.Stroke(i % 2 == 0 ? petRight : petLeft);
                bool cooled = pet.Pets == petsBefore + 1;
                pet.Talk.Dismiss();
                pet.ForgetPets();
                if (earlyPet || !smiled || !spoke || !cooled) throw new Exception("Petting her went wrong: early " + earlyPet + ", smiled " + smiled + ", spoke " + spoke + ", cooled " + cooled);
                checks.Add("PASS petting: rubbing the cursor back and forth over her makes her smile and say a short line, not too easily and not again for 20 s");
                pet.SetWander(3);
                pet.SetLanguage("French");
                pet.SetReplies(1);
                pet.SetRecall(16);
                pet.SetSounds(30);
                pet.Dancing = true;
                if (pet.Saved.Wander != 3 || pet.Saved.Language != "French" || Brain.Language != "French" || Brain.Wordiness != 1 || Brain.Recall != 16 || pet.Saved.Sounds != 30 || Sfx.Level != 30 || pet.Picked != "Wander: Often | Français | Medium | Medium | Quiet | Dance to music | Dance") throw new Exception("Menu settings didn't stick: " + pet.Picked);
                pet.SetDanceToMusic(false);
                if (!pet.Saved.NoDance || pet.Picked.Contains("Dance to music")) throw new Exception("Dance to music didn't turn off.");
                pet.SetDanceToMusic(true);
                pet.SetWander(0);
                pet.SetLanguage("English");
                pet.SetReplies(0);
                pet.SetRecall(8);
                pet.SetSounds(60);
                pet.Dancing = false;
                if (Brain.Language != null || pet.Saved.Language != null || pet.Picked != "Stay put | English | Short | Short | Normal | Dance to music") throw new Exception("Menu settings didn't go back: " + pet.Picked);
                bool checkInsNew = pet.CheckIns && pet.CheckInsShown && pet.PopPace == 1 && pet.PopsPicked == "Sometimes";
                pet.SetPopPace(2);
                bool popsOften = pet.Saved.PopPace == 2 && pet.PopsPicked == "Often";
                double popSoonest = double.MaxValue, popLatest = 0;
                for (int i = 0; i < 200; i++)
                {
                    double gap = pet.PopGap();
                    popSoonest = Math.Min(popSoonest, gap);
                    popLatest = Math.Max(popLatest, gap);
                }
                pet.SetPopPace(0);
                double rareGap = pet.PopGap();
                bool popsRarely = pet.Saved.PopPace == 0 && pet.PopsPicked == "Rarely" && rareGap >= 1200 && rareGap <= 2400;
                pet.SetCheckIns(false);
                bool checkInsOff = !pet.CheckIns && pet.Saved.NoCheckIns && !pet.CheckInsShown;
                pet.SetCheckIns(true);
                pet.SetPopPace(1);
                if (!checkInsNew || !popsOften || popSoonest < 180 || popLatest > 480 || !popsRarely || !checkInsOff || !pet.CheckIns || pet.PopsPicked != "Sometimes") throw new Exception("Check in on me settings went wrong: new " + checkInsNew + ", often " + popsOften + " " + popSoonest.ToString("0") + "-" + popLatest.ToString("0") + " s, rarely " + popsRarely + ", off " + checkInsOff);
                checks.Add("PASS Check in on me settings: on by default at Pop-ins Sometimes, Often waits 3 to 8 minutes and Rarely 20 to 40, and turning it off sticks");
                pet.Talk.Dismiss();
                bool freeBefore = pet.Free;
                pet.ToggleShush();
                string hushed = pet.Talk.Reply;
                bool quieted = pet.Shushed && pet.QuietShown && !pet.Free && hushed == "Going quiet. Right-click me > Quiet when you want me back.";
                pet.ToggleShush();
                bool unhushed = !pet.Shushed && !pet.QuietShown && pet.Talk.Reply == "I'm back, choom.";
                pet.Talk.Dismiss();
                if (!quieted || !unhushed || pet.Free != freeBefore) throw new Exception("Quiet went wrong: quiet " + quieted + " (" + hushed + "), back " + unhushed + ", free before " + freeBefore + " and after " + pet.Free);
                checks.Add("PASS quiet: right-click > Quiet stops her pop-ins, check-ins, cheers, jokes and glances until it's turned off, and she says so both times");
                pet.SetBubbleTime(2);
                bool bubbleLong = pet.BubblePicked == "Long" && Talk.Hold(100, pet.BubbleTime) == 18 && Talk.Hold(1000, pet.BubbleTime) == 28;
                pet.SetBubbleTime(0);
                bool bubbleQuick = pet.BubblePicked == "Quick" && Talk.Hold(100, pet.BubbleTime) == 4.5;
                pet.SetBubbleTime(1);
                if (!bubbleLong || !bubbleQuick || pet.BubblePicked != "Normal" || Talk.Hold(100, pet.BubbleTime) != 9) throw new Exception("Bubble went wrong: long " + bubbleLong + ", quick " + bubbleQuick + ", picked " + pet.BubblePicked);
                checks.Add("PASS bubble: Settings > Bubble keeps her replies up half as long on Quick and twice as long on Long, like Windows");
                int loudnessWas = Voice.Loudness;
                pet.SetVolume(50);
                short[] volumeHalved = Voice.Louder(new short[] { 1000, -2000 });
                bool volumeHalf = pet.VolumePicked == "50%" && volumeHalved[0] == 500 && volumeHalved[1] == -1000;
                pet.SetVolume(150);
                short[] volumeRaised = Voice.Louder(new short[] { 1000, 30000 });
                bool volumeLoud = pet.VolumePicked == "150%" && volumeRaised[0] == 1500 && volumeRaised[1] == short.MaxValue;
                pet.SetVolume(0);
                bool volumeMuted = pet.VolumePicked == "Mute" && Voice.Louder(new short[] { 1000 })[0] == 0;
                pet.SetVolume(loudnessWas);
                if (!volumeHalf || !volumeLoud || !volumeMuted) throw new Exception("Volume went wrong: 50% " + volumeHalf + ", 150% " + volumeLoud + ", mute " + volumeMuted);
                checks.Add("PASS volume: Settings > Volume sets her voice from Mute to 150%, like Windows");
                short[] tonePcm = new short[12000];
                for (int i = 0; i < tonePcm.Length; i++) tonePcm[i] = (short)(Math.Sin(2 * Math.PI * 220 * i / 24000.0) * 8000);
                byte[] toneWav = Voice.Wave(tonePcm, 24000);
                bool stylePlainBefore = pet.StyleShown == "Normal | Normal | Off | Off" && VoiceFx.Styled(toneWav, "Hey choom.") == toneWav;
                pet.SetPitch(3);
                pet.SetPace(120);
                pet.SetRobot(30);
                pet.SetGlitch(40);
                string styleSet = pet.StyleShown;
                byte[] toneStyled = VoiceFx.Styled(toneWav, "Hey choom.");
                bool styleChanged = styleSet == "+3 | 1.2x | 30% | 40%" && VoiceFx.Pitch == 3 && VoiceFx.Speed == 120 && VoiceFx.Robot == 30 && VoiceFx.Glitch == 40 && toneStyled != toneWav && Voice.Seconds(toneStyled) > 0.2;
                pet.SetPitch(0);
                pet.SetPace(100);
                pet.SetRobot(0);
                pet.SetGlitch(0);
                bool stylePlain = pet.StyleShown == "Normal | Normal | Off | Off" && VoiceFx.Styled(toneWav, "Hey choom.") == toneWav;
                if (!stylePlainBefore || !styleChanged || !stylePlain) throw new Exception("Voice style went wrong: plain before " + stylePlainBefore + ", set " + styleSet + " changed " + styleChanged + ", plain after " + stylePlain);
                checks.Add("PASS voice style: Settings > Voice style sets her pitch, speed, robot and glitch like Windows, her ElevenLabs replies get them, and at Normal and Off her voice is untouched");
                // Her voice layers, like Windows: the shared checks of the sound, then Voice style's Layers menus, her ElevenLabs
                // replies getting them and the settings saving and loading.
                string layersFound, layersWrong = VoiceFx.LayersCheck(out layersFound);
                VoiceFx.Layering layersWas = VoiceFx.Layers;
                string layersNew = pet.LayersShown;
                bool layersPlain = VoiceFx.Styled(toneWav, "Hey choom.") == toneWav;
                pet.SetLayers(VoiceFx.Layers.Turned(true));
                string layersOn = pet.LayersShown;
                int layersRate;
                short[] layersSaid = Voice.Samples(VoiceFx.Styled(toneWav, "Hey choom."), out layersRate);
                pet.SetLayers(VoiceFx.Layers.With(1, 3, 40).With(2, -4, 100));
                string layersSet = pet.LayersShown;
                System.Runtime.Serialization.Json.DataContractJsonSerializer layersJson = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(Settings));
                Settings layersLoaded;
                using (MemoryStream layersStream = new MemoryStream())
                {
                    layersJson.WriteObject(layersStream, pet.Saved);
                    layersStream.Position = 0;
                    layersLoaded = (Settings)layersJson.ReadObject(layersStream);
                }
                Settings layersOld = (Settings)layersJson.ReadObject(new MemoryStream(Encoding.UTF8.GetBytes("{\"Pitch\":2,\"Glitch\":10}")));
                pet.SetLayers(layersWas);
                string layersBack = pet.LayersShown;
                const string layersPlainShown = "Off | +2, 25 ms | -2.5, 25 ms";
                if (layersWrong != null || layersNew != layersPlainShown || !layersPlain || layersOn != "On | +2, 25 ms | -2.5, 25 ms" || layersRate != 24000 || layersSaid.Length != tonePcm.Length + 600
                    || layersSet != "On | +3, 40 ms | -4, 100 ms" || Pet.LayersOf(layersLoaded).Shown != "On | +3, 40 ms | -4, 100 ms" || Pet.LayersOf(layersOld).Shown != layersPlainShown || Pet.LayersOf(new Settings()).Shown != layersPlainShown || layersBack != layersPlainShown || VoiceFx.Styled(toneWav, "Hey choom.") != toneWav)
                    throw new Exception("Voice layers went wrong: " + layersWrong + " / new " + layersNew + ", on " + layersOn + " (" + layersSaid.Length + " samples from " + tonePcm.Length + "), set " + layersSet + ", loaded " + Pet.LayersOf(layersLoaded).Shown + ", older " + Pet.LayersOf(layersOld).Shown + ", back " + layersBack);
                checks.Add("PASS voice layers: Settings > Voice style > Layers turns on two copies of her under her, like Windows, with Layer 1 and Layer 2's pitch and delay in their own menus; " + layersFound + "; her ElevenLabs replies get them and come back 25 ms longer for their tails; off out of the box and in settings from before them, and off her voice is byte for byte as it was; the layers save and load as set");
                bool loginNew = !pet.StartsAtLogin;
                pet.SetLogin(true);
                string loginText = File.Exists(pet.LoginFile) ? File.ReadAllText(pet.LoginFile) : "";
                bool loginOn = pet.StartsAtLogin && pet.LoginShown && loginText == Limbs.LoginEntry(Environment.ProcessPath, OperatingSystem.IsMacOS());
                File.SetLastWriteTimeUtc(pet.LoginFile, new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                pet.SetLogin(true);
                bool loginKept = File.GetLastWriteTimeUtc(pet.LoginFile).Year == 2001;
                pet.SetLogin(false);
                bool loginOff = !File.Exists(pet.LoginFile) && !pet.LoginShown;
                string linuxLogin = Limbs.LoginEntry("/home/me/My Apps/Capri", false), macLogin = Limbs.LoginEntry("/Applications/Capri.app/Contents/MacOS/Capri", true), bareLogin = Limbs.LoginEntry("/Users/me/capri/Capri", true);
                bool macRight = macLogin.Contains("\t\t<string>/usr/bin/open</string>\n\t\t<string>-g</string>\n\t\t<string>-b</string>\n\t\t<string>" + Limbs.MacBundle + "</string>\n") && !macLogin.Contains("/Applications/") && macLogin.Contains("<key>RunAtLoad</key>") && macLogin.Contains("<string>Interactive</string>") && macLogin.Contains("<key>AssociatedBundleIdentifiers</key>\n\t<array>\n\t\t<string>" + Limbs.MacBundle + "</string>") && bareLogin.Contains("\t\t<string>/Users/me/capri/Capri</string>\n") && !bareLogin.Contains("AssociatedBundleIdentifiers");
                if (!loginNew || !loginOn || !loginKept || !loginOff || !linuxLogin.Contains("\nExec=\"/home/me/My Apps/Capri\"\n") || !macRight) throw new Exception("Start at login went wrong: new " + loginNew + ", on " + loginOn + ", kept " + loginKept + ", off " + loginOff + ", mac " + macRight + " | " + loginText);
                checks.Add("PASS start at login: off until ticked, then an autostart entry on Linux that starts this copy, or a LaunchAgent on Mac that opens her app by its id wherever it's kept; left alone when unchanged, gone again when unticked");
                bool introNew = pet.IntroShown && !pet.Saved.NoIntro;
                pet.SetIntro(false);
                PixelPoint introHip = pet.Hip;
                int introArrivals = pet.Arrivals;
                pet.Reappear();
                bool introOff = pet.Saved.NoIntro && !pet.IntroShown && pet.Arrivals == introArrivals && !pet.Falling && Sitting() && pet.Hip == introHip;
                pet.SetIntro(true);
                if (!introNew || !introOff || pet.Saved.NoIntro || !pet.IntroShown) throw new Exception("Glitch in at startup went wrong: new " + introNew + ", off " + introOff + " (" + introHip + " to " + pet.Hip + ")");
                checks.Add("PASS glitch in at startup: ticked by default; unticked, she starts back on the spot she sat on, with no glitch or sound, like Windows");
                bool onTopNew = pet.OnTop && pet.OnTopShown && pet.Topmost && pet.Talk.Bubble.Topmost && !pet.Raised;
                pet.SetOnTop(false);
                bool onTopOff = !pet.OnTop && !pet.OnTopShown && pet.Saved.NotOnTop && !pet.Topmost && !pet.Talk.Bubble.Topmost && !pet.Talk.Typer.Topmost && !pet.Talk.Pill.Topmost;
                pet.Raise(pet.Now + 6);
                pet.CheckRaised(pet.Now);
                bool raisedUp = pet.Raised && pet.Topmost && pet.Talk.Bubble.Topmost;
                pet.Tap();
                pet.CheckRaised(pet.Now);
                bool loweredByTap = !pet.Raised && !pet.Topmost && !pet.Talk.Bubble.Topmost;
                pet.SetOnTop(true);
                pet.Raise(pet.Now + 6);
                bool onTopBack = pet.OnTop && pet.OnTopShown && pet.Topmost && pet.Talk.Pill.Topmost && !pet.Raised && !pet.Saved.NotOnTop;
                if (!onTopNew || !onTopOff || !raisedUp || !loweredByTap || !onTopBack) throw new Exception("Always on top went wrong: new " + onTopNew + ", off " + onTopOff + ", raised " + raisedUp + ", lowered " + loweredByTap + ", back " + onTopBack);
                checks.Add("PASS always on top: on by default; off, she and her bubble, typing box and pill are ordinary windows, and a pop-in brings her to the front until a click, like Windows");
                string homeFirst = pet.Cheer(true, false, 100, true), homeStill = pet.Cheer(true, false, 115, true);
                pet.Cheer(false, false, 130, true);
                string homeSoon = pet.Cheer(true, false, 145, true), homeBusy = pet.Cheer(true, true, 160, false);
                pet.Cheer(true, false, 175, true);
                string homeSong = pet.Cheer(true, true, 190, true);
                pet.Cheer(false, false, 200, true);
                string homeLater = pet.Cheer(true, false, 200 + Pet.HomeGap, true);
                pet.ForgetCheers();
                if (Array.IndexOf(Pet.HomeGameLines, homeFirst) < 0 || homeStill != null || homeSoon != null || homeBusy != null || Array.IndexOf(Pet.HomeSongLines, homeSong) < 0 || Array.IndexOf(Pet.HomeGameLines, homeLater) < 0) throw new Exception("Her Night City cheers came at the wrong times: " + homeFirst + " | " + homeStill + " | " + homeSoon + " | " + homeBusy + " | " + homeSong + " | " + homeLater);
                checks.Add("PASS Night City cheer: she cheers once when Cyberpunk 2077 starts or a Night City song comes on, not while she's busy, and not again for two hours");
                string lagEarly = pet.Lagging(0.95, 10, true) ?? pet.Lagging(0.95, 25, true) ?? pet.Lagging(0.95, 40, true), lagLine = pet.Lagging(0.97, 55, true);
                string lagSoon = null, lagBusy = null, lagCooled = null, lagLater = null;
                for (int i = 0; i < 4; i++) lagSoon = lagSoon ?? pet.Lagging(0.99, 70 + i * 15, true);
                for (int i = 0; i < 4; i++) lagBusy = lagBusy ?? pet.Lagging(0.99, 2000 + i * 15, false);
                for (int i = 0; i < 4; i++) lagCooled = lagCooled ?? pet.Lagging(i == 0 ? 0.4 : 0.99, 2100 + i * 15, true);
                for (int i = 0; i < 4; i++) lagLater = lagLater ?? pet.Lagging(0.99, 2200 + i * 15, true);
                pet.ForgetLag();
                long cpuIdle, cpuTotal;
                bool cpuRead = Limbs.CpuTimes(out cpuIdle, out cpuTotal) && cpuIdle >= 0 && cpuIdle <= cpuTotal;
                if (lagEarly != null || Array.IndexOf(Pet.LagLines, lagLine) < 0 || lagSoon != null || lagBusy != null || lagCooled != null || Array.IndexOf(Pet.LagLines, lagLater) < 0 || !cpuRead) throw new Exception("Her lag joke came at the wrong times, or the CPU didn't read: " + lagEarly + " | " + lagLine + " | " + lagSoon + " | " + lagBusy + " | " + lagCooled + " | " + lagLater + " | " + cpuRead + " " + cpuIdle + "/" + cpuTotal);
                checks.Add("PASS lag joke: she jokes once the CPU has been over 90% busy for about a minute while she's free, and not again for half an hour; this computer's CPU time reads");
                Func<string, string[], string> runWas = Music.Run;
                List<string> ran = new List<string>();
                string status = "Playing";
                string apple = "playing\n";
                try
                {
                    Music.Run = delegate(string file, string[] args)
                    {
                        string line = file + " " + string.Join(" ", args);
                        ran.Add(line);
                        if (line.Contains("ListNames")) return "method return\n   array [\n      string \"org.freedesktop.DBus\"\n      string \"org.mpris.MediaPlayer2.chromium.instance4021\"\n      string \"org.mpris.MediaPlayer2.spotify\"\n   ]\n";
                        if (line.Contains("PlaybackStatus")) return "method return\n   variant       string \"" + status + "\"\n";
                        if (file == "pgrep") return line.EndsWith(" Spotify", StringComparison.Ordinal) ? "4242\n" : null;
                        if (file == "osascript") return line.Contains("\"Spotify\"") ? apple : "";
                        return null;
                    };
                    bool linuxOn = Music.Linux();
                    bool macOn = Music.Mac();
                    status = "Paused";
                    apple = "paused\n";
                    bool linuxOff = Music.Linux();
                    bool macOff = Music.Mac();
                    string asked = string.Join(" / ", ran);
                    if (!linuxOn || !macOn || linuxOff || macOff || asked.Contains("chromium") || !asked.Contains("--dest=org.mpris.MediaPlayer2.spotify") || !asked.Contains("pgrep -x Music") || asked.Contains("tell application \"Music\"") || !asked.Contains("tell application \"Spotify\" to get player state")) throw new Exception("Music detection read players wrong: " + asked);
                }
                finally { Music.Run = runWas; }
                checks.Add("PASS music: Linux reads music players over D-Bus (not browsers), Mac asks Spotify and Music only when they're already running");
                List<string> songsAsked = new List<string>();
                try
                {
                    Music.Run = delegate(string file, string[] args)
                    {
                        string line = file + " " + string.Join(" ", args);
                        songsAsked.Add(line);
                        if (line.Contains("ListNames")) return "method return\n   array [\n      string \"org.freedesktop.DBus\"\n      string \"org.mpris.MediaPlayer2.chromium.instance4021\"\n      string \"org.mpris.MediaPlayer2.spotify\"\n   ]\n";
                        if (line.Contains("PlaybackStatus")) return "method return\n   variant       string \"" + (line.Contains("chromium") ? "Playing" : "Paused") + "\"\n";
                        if (line.Contains("Metadata")) return "method return\n   variant       array [\n         dict entry(\n            string \"xesam:artist\"\n            variant                array [\n                  string \"CD PROJEKT RED\"\n               ]\n         )\n         dict entry(\n            string \"xesam:title\"\n            variant                string \"SAMURAI - Never Fade Away (Official Audio)\"\n         )\n      ]\n";
                        if (file == "pgrep") return line.Contains("cyberpunk") || line.EndsWith(" Music", StringComparison.Ordinal) ? "777\n" : null;
                        if (file == "osascript") return line.Contains("\"Music\"") ? "Rosa Walton - I Really Want to Stay at Your House\n" : "";
                        return null;
                    };
                    List<string> linuxSongs = Music.LinuxSongs();
                    List<string> macSongs = Music.MacSongs();
                    bool cyberpunk = Limbs.CyberpunkRunning();
                    string asked = string.Join(" / ", songsAsked);
                    if (linuxSongs.Count != 1 || linuxSongs[0] != "CD PROJEKT RED - SAMURAI - Never Fade Away (Official Audio)" || !Hands.NightCity(linuxSongs[0]) || macSongs.Count != 1 || !Hands.NightCity(macSongs[0]) || !cyberpunk || asked.Contains("tell application \"Spotify\"") || asked.Contains("spotify /org/mpris/MediaPlayer2 org.freedesktop.DBus.Properties.Get string:org.mpris.MediaPlayer2.Player string:Metadata")) throw new Exception("Night City songs or the game were read wrong: " + string.Join(" | ", linuxSongs) + " / " + string.Join(" | ", macSongs) + " / " + asked);
                }
                finally { Music.Run = runWas; }
                checks.Add("PASS Night City songs: Linux reads every playing player's title, browser tabs too, Mac asks Spotify and Music only when they're open, and a running Cyberpunk 2077 is found");
                Action<string, string[]> launchWas = Limbs.Launch;
                int patienceWas = Limbs.Patience;
                int settleWas = Limbs.Settle;
                Func<List<InstalledApp>> catalogWas = Hands.Catalog;
                List<string> did = new List<string>();
                bool noWindowTool = false, pactlWorks = false;
                string tempApps = Path.Combine(folder, "apps");
                try
                {
                    Limbs.Patience = 2;
                    Limbs.Settle = 0;
                    Limbs.Launch = delegate(string file, string[] args) { did.Add("launch " + file + " " + string.Join(" ", args)); };
                    Music.Run = delegate(string file, string[] args)
                    {
                        string line = file + " " + string.Join(" ", args);
                        did.Add(line);
                        if (file == "pgrep") return line.EndsWith(" Spotify", StringComparison.Ordinal) ? "4242\n" : null;
                        if (line.Contains("ListNames")) return "   array [\n      string \"org.mpris.MediaPlayer2.firefox.instance_1_9\"\n      string \"org.mpris.MediaPlayer2.spotify\"\n   ]\n";
                        if (line.Contains("PlaybackStatus")) return "   variant       string \"Playing\"\n";
                        if (line.Contains("Metadata")) return "   variant       array [\n         dict entry(\n            string \"xesam:artist\"\n            variant                array [\n                  string \"Chase Atlantic\"\n               ]\n         )\n         dict entry(\n            string \"xesam:title\"\n            variant                string \"Consume\"\n         )\n      ]\n";
                        if (file == "osascript" && line.Contains("current track")) return "Petite Meller - In Love With Myself\n";
                        if (file == "pactl" && pactlWorks && line.Contains("get-sink-volume")) return "Volume: front-left: 26214 /  40% / -23.88 dB,   front-right: 26214 /  40% / -23.88 dB\n        balance 0.00\n";
                        if (file == "pactl" && pactlWorks && line.Contains("get-sink-mute")) return "Mute: yes\n";
                        if (file == "pactl") return pactlWorks ? "" : null;
                        if (file == "sh" && line.Contains("command -v ")) return line.EndsWith(" spectacle", StringComparison.Ordinal) || line.EndsWith(" plasma-systemmonitor", StringComparison.Ordinal) ? "/usr/bin/found\n" : null;
                        if (line == "amixer sget Master") return "Simple mixer control 'Master',0\n  Front Left: Playback 26214 [40%] [-23.88dB] [on]\n";
                        if (file == "osascript" && line.EndsWith("-e output volume of (get volume settings)", StringComparison.Ordinal)) return "40\n";
                        if (file == "osascript" && line.EndsWith("-e output muted of (get volume settings)", StringComparison.Ordinal)) return "false\n";
                        if (file == "osascript" && line.Contains("background only is false")) return "Finder\tcom.apple.finder\nDiscord\tcom.hnc.Discord\nCapri\tmissing value\n";
                        if (file == "osascript" && line.Contains("exists application process")) return "false\n";
                        if (file == "wmctrl" && line == "wmctrl -lpx") return noWindowTool ? null : "0x01e00003 -1 0      N/A                    N/A Desktop\n0x03c00007  0 99999999 discord.discord  host #general | Discord\n0x04200001  0 99999998 Navigator.firefox  host YouTube — Mozilla Firefox\n";
                        if (file == "wmctrl" && line == "wmctrl -l") return "0x04200001  0 host YouTube — Mozilla Firefox\n";
                        return "";
                    };
                    Directory.CreateDirectory(Path.Combine(tempApps, "Spotify.app"));
                    File.WriteAllLines(Path.Combine(tempApps, "spotify.desktop"), new[] { "[Desktop Entry]", "Type=Application", "Name=Spotify", "Exec=spotify %U" });
                    File.WriteAllLines(Path.Combine(tempApps, "helper.desktop"), new[] { "[Desktop Entry]", "Type=Application", "Name=Helper", "Exec=helper", "NoDisplay=true" });
                    List<InstalledApp> linuxApps = Limbs.LinuxApps(new[] { tempApps });
                    List<InstalledApp> macApps = Limbs.MacApps(new[] { tempApps });
                    if (linuxApps.Count != 1 || linuxApps[0].Name != "Spotify" || linuxApps[0].Id != "exec:spotify" || macApps.Count != 1 || macApps[0].Id != Path.Combine(tempApps, "Spotify.app")) throw new Exception("Installed apps were read wrong.");
                    // Steam games come from Steam's own library too (here the Flatpak install), without its runtimes.
                    string steamHome = Path.Combine(folder, "home");
                    string steamRoot = Path.Combine(steamHome, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam");
                    Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps"));
                    File.WriteAllText(Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"), "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"" + steamRoot + "\"\n\t}\n}\n");
                    File.WriteAllText(Path.Combine(steamRoot, "steamapps", "appmanifest_1145360.acf"), "\"AppState\"\n{\n\t\"appid\"\t\t\"1145360\"\n\t\"name\"\t\t\"Hades\"\n\t\"StateFlags\"\t\t\"4\"\n}\n");
                    File.WriteAllText(Path.Combine(steamRoot, "steamapps", "appmanifest_1628350.acf"), "\"AppState\"\n{\n\t\"appid\"\t\t\"1628350\"\n\t\"name\"\t\t\"Steam Linux Runtime 3.0 (sniper)\"\n\t\"StateFlags\"\t\t\"4\"\n}\n");
                    List<InstalledApp> steamGames = Hands.SteamApps(Limbs.SteamRoot(steamHome, false));
                    if (Limbs.SteamRoot(steamHome, false) != steamRoot || Limbs.SteamRoot(steamHome, true) != null || steamGames.Count != 1 || steamGames[0].Name != "Hades" || steamGames[0].Id != "steam://rungameid/1145360")
                        throw new Exception("Steam's library was read wrong: " + steamGames.Count);
                    Hands.Catalog = delegate { return Hands.WithSteam(Limbs.LinuxApps(new[] { tempApps }), Hands.SteamApps(Limbs.SteamRoot(steamHome, false))); };
                    Hands.Forget();
                    string hades = Hands.Do("open_app", new ToolArguments { Name = "hades" });
                    if (hades != "Opened Hades." || !did.Contains((OperatingSystem.IsMacOS() ? "launch open " : "launch xdg-open ") + "steam://rungameid/1145360")) throw new Exception("A Steam game didn't open: " + hades + " / " + string.Join(" / ", did));
                    Hands.Catalog = delegate { return Limbs.LinuxApps(new[] { tempApps }); };
                    Hands.Forget();
                    string opened = Hands.Do("open_app", new ToolArguments { Name = "spotify" });
                    string searched = Hands.Do("open_website", new ToolArguments { Site = "youtube", Search = "lofi beats" });
                    string skipped = Hands.Do("media", new ToolArguments { Action = "next" });
                    string snipped = Hands.Do("shortcut", new ToolArguments { Name = "screenshot" });
                    string clipped = Hands.Do("shortcut", new ToolArguments { Name = "clip_that" });
                    Limbs.Key(0xB0, true);
                    Limbs.Key(0xAF, true);
                    Limbs.Key(0xAD, false);
                    Limbs.Open("spotify:track:2iyaHQlYrR8Eyr4wOmOT5D", true);
                    Limbs.Open("spotify:track:2iyaHQlYrR8Eyr4wOmOT5D", false);
                    string macSong = Limbs.Song(true);
                    string linuxSong = Limbs.Song(false);
                    string log = string.Join(" / ", did);
                    if (opened != "Opened Spotify." || !log.Contains("launch /bin/sh -c spotify") || searched != "Searched youtube.com for lofi beats." || !log.Contains((OperatingSystem.IsMacOS() ? "launch open " : "launch xdg-open ") + "https://www.youtube.com/results?search_query=lofi%20beats")) throw new Exception("Opening apps or sites went wrong: " + opened + " / " + searched + " / " + log);
                    if (skipped != "Skipped to the next track." || !log.Contains(OperatingSystem.IsMacOS() ? "osascript -e tell application \"Spotify\" to next track" : "--dest=org.mpris.MediaPlayer2.spotify /org/mpris/MediaPlayer2 org.mpris.MediaPlayer2.Player.Next") || !snipped.StartsWith("Opened the screenshot picker.", StringComparison.Ordinal) || !clipped.Contains("only work on Windows")) throw new Exception("Media keys or shortcuts went wrong: " + skipped + " / " + snipped + " / " + clipped + " / " + log);
                    if (!log.Contains("osascript -e tell application \"Spotify\" to next track") || !log.Contains("+ 2)") || !log.Contains("pactl set-sink-mute @DEFAULT_SINK@ toggle / amixer -q sset Master toggle")) throw new Exception("Mac or Linux media keys went wrong: " + log);
                    if (!log.Contains("play track \"spotify:track:2iyaHQlYrR8Eyr4wOmOT5D\"") || !log.Contains("OpenUri string:spotify:track:2iyaHQlYrR8Eyr4wOmOT5D") || log.Contains("launch open spotify:") || log.Contains("launch xdg-open spotify:")) throw new Exception("Spotify tracks didn't play directly: " + log);
                    if (macSong != "Petite Meller - In Love With Myself" || linuxSong != "Chase Atlantic - Consume") throw new Exception("Now playing was read wrong: " + macSong + " / " + linuxSong);
                    // Closing, minimizing and switching apps: System Events on Mac, wmctrl on Linux, and her own window never counts.
                    did.Clear();
                    List<OpenWindow> macWindows = Limbs.Windows(true);
                    OpenWindow macDiscord = macWindows.Find(delegate(OpenWindow shown) { return shown.Program == "Discord"; });
                    bool macSteered = macDiscord != null && Limbs.Steer(macDiscord, "switch_to", true) && Limbs.Steer(macDiscord, "close", true) && Limbs.Steer(macDiscord, "minimize", true);
                    List<OpenWindow> linuxWindows = Limbs.Windows(false);
                    OpenWindow linuxDiscord = linuxWindows.Find(delegate(OpenWindow shown) { return shown.Program == "discord"; });
                    bool linuxSteered = linuxDiscord != null && linuxWindows.Count == 2 && linuxWindows[1].Program == "firefox" && linuxWindows[1].Title == "YouTube — Mozilla Firefox"
                        && Limbs.Steer(linuxDiscord, "close", false) && Limbs.Steer(linuxWindows[1], "minimize", false) && Limbs.Steer(linuxWindows[1], "switch_to", false);
                    string closedDiscord = Hands.Do("app_window", new ToolArguments { Name = "discord", Action = "close" });
                    // Without wmctrl, what's playing just sees no windows, and her window tool says what to install.
                    noWindowTool = true;
                    bool noneListed = Limbs.Windows(false).Count == 0;
                    string noWmctrl = Hands.Unlisted() ?? "";
                    noWindowTool = false;
                    Limbs.Windows(false);
                    bool listedAgain = Hands.Unlisted() == null;
                    string windowLog = string.Join(" / ", did);
                    if (!macSteered || macWindows.Count != 2 || macWindows.Exists(delegate(OpenWindow shown) { return shown.Program == "Capri"; }) || !windowLog.Contains("tell application id \"com.hnc.Discord\" to activate") || !windowLog.Contains("tell application id \"com.hnc.Discord\" to quit")
                        || !windowLog.Contains("set visible of application process \"Discord\" to false") || !linuxSteered || !windowLog.Contains("wmctrl -i -c 0x03c00007") || !windowLog.Contains("xdotool windowminimize 0x04200001") || !windowLog.Contains("wmctrl -i -a 0x04200001")
                        || closedDiscord != "Closed discord." || !noWmctrl.Contains("needs wmctrl") || !noneListed || !listedAgain || Hands.Presses)
                        throw new Exception("Closing and switching apps went wrong: " + closedDiscord + " / " + noWmctrl + " / " + windowLog);
                    // Shortcuts with a tool of their own: the screenshot picker, the task manager and, on Linux, show desktop.
                    did.Clear();
                    string linuxShot = Limbs.Shortcut("screenshot", false), linuxMonitor = Limbs.Shortcut("task_manager", false), linuxDesktop = Limbs.Shortcut("show_desktop", false);
                    string macShot = Limbs.Shortcut("screenshot", true), macMonitor = Limbs.Shortcut("task_manager", true);
                    string shortcutLog = string.Join(" / ", did);
                    if (linuxShot != "Opened the screenshot picker. Drag over what to grab." || linuxMonitor != "Opened the system monitor." || linuxDesktop != "Showed the desktop." || macMonitor != "Opened Activity Monitor."
                        || !macShot.EndsWith("it goes to your clipboard.", StringComparison.Ordinal) || Limbs.Shortcut("show_desktop", true) != null || Limbs.Shortcut("clip_that", false) != null
                        || !shortcutLog.Contains("launch spectacle -r") || !shortcutLog.Contains("launch plasma-systemmonitor") || !shortcutLog.Contains("wmctrl -k on") || !shortcutLog.Contains("launch screencapture -i -c") || !shortcutLog.Contains("launch open -a Activity Monitor") || shortcutLog.Contains("launch flameshot"))
                        throw new Exception("Mac or Linux shortcuts went wrong: " + shortcutLog);
                    // An exact volume and the mute state: osascript on Mac, pactl on Linux, and amixer when pactl is missing.
                    did.Clear();
                    pactlWorks = true;
                    double? pulseLevel = Limbs.Level(false);
                    bool? pulseMuted = Limbs.Muted(false);
                    bool pulseSet = Limbs.SetLevel(0.4, false) && Limbs.Mute(false, false);
                    pactlWorks = false;
                    double? alsaLevel = Limbs.Level(false);
                    bool? alsaMuted = Limbs.Muted(false);
                    double? macLevel = Limbs.Level(true);
                    bool? macMuted = Limbs.Muted(true);
                    bool macSet = Limbs.SetLevel(0.55, true) && Limbs.Mute(true, true);
                    string setLine = Hands.Do("media", new ToolArguments { Action = "volume_set", Level = "40" });
                    string volumeLog = string.Join(" / ", did);
                    if (pulseLevel != 0.4 || pulseMuted != true || !pulseSet || alsaLevel != 0.4 || alsaMuted != false || macLevel != 0.4 || macMuted != false || !macSet || setLine != "Set the volume to 40%."
                        || !volumeLog.Contains("pactl set-sink-volume @DEFAULT_SINK@ 40%") || !volumeLog.Contains("pactl set-sink-mute @DEFAULT_SINK@ 0") || !volumeLog.Contains("osascript -e set volume output volume 55") || !volumeLog.Contains("osascript -e set volume output muted true"))
                        throw new Exception("Setting the volume went wrong on Mac or Linux: " + setLine + " / " + volumeLog);
                    // Her power tool: a lock happens at once, a shutdown waits and cancel stops it. Only the pretend runner sees any of it.
                    did.Clear();
                    string lockedLine = Hands.Do("power", new ToolArguments { Action = "lock" });
                    bool poweredNow = Limbs.Power("lock", 0, false) && Limbs.Power("sleep", 0, true) && Limbs.Power("sign_out", 0, true);
                    bool waiting = Limbs.Power("shut_down", 600, false);
                    bool cancelled = Limbs.Power("cancel", 0, false), nothingLeft = !Limbs.Power("cancel", 0, false);
                    string powerLog = string.Join(" / ", did);
                    if (lockedLine != "Locked your PC." || !poweredNow || !waiting || !cancelled || !nothingLeft || !powerLog.Contains("loginctl lock-session") || !powerLog.Contains("pmset sleepnow") || !powerLog.Contains("«event aevtrlgo»") || powerLog.Contains("poweroff")
                        || Limbs.PowerCommands("sign_out", false)[0][0] != "gnome-session-quit" || Limbs.PowerCommands("restart", true)[0][2].IndexOf("restart", StringComparison.Ordinal) < 0 || Limbs.PowerCommands("dance", false) != null)
                        throw new Exception("Her power tool went wrong on Mac or Linux: " + lockedLine + " / " + powerLog);
                }
                finally
                {
                    Limbs.Power("cancel", 0, false);
                    Music.Run = runWas;
                    Limbs.Launch = launchWas;
                    Limbs.Patience = patienceWas;
                    Limbs.Settle = settleWas;
                    Hands.Catalog = catalogWas;
                    Hands.Forget();
                }
                checks.Add("PASS hands: she opens apps (.app folders, .desktop files) and sites, skips and plays Spotify tracks, reads what's playing, turns or sets the volume and reads the mute state on Mac and Linux, closes, minimizes and switches to apps (System Events on Mac, wmctrl on Linux, which says when it's missing), and shuts down, restarts, sleeps, locks and signs out, with cancel stopping a wait; a screenshot, the task manager and show desktop use their own tools, and key-press shortcuts say they're Windows-only");
                SecurityChecks();
                pet.ShowBrains(new[] { "gemma4:e4b", "qwen3:8b" });
                int brains = pet.BrainChoices;
                pet.Brain.Remember("what's up?", "Not much, choom.");
                int chatBeforePick = pet.Brain.History.Count;
                pet.PickBrain("qwen3:8b");
                int chatAfterPick = pet.Brain.History.Count;
                bool picked = pet.Brain.Model == "qwen3:8b" && pet.Saved.Model == "qwen3:8b";
                pet.Brain.Forget();
                pet.ShowBrains(new string[0]);
                string noModels = pet.FirstBrainChoice;
                pet.ShowBrains(null);
                if (brains != 2 || !picked || pet.BrainChoices != 1) throw new Exception("The Brain menu didn't list or pick models.");
                if (noModels != Pet.PullChoice || pet.FirstBrainChoice != Pet.NoOllama) throw new Exception("The Brain menu didn't offer her brain download: " + noModels + " / " + pet.FirstBrainChoice);
                if (chatBeforePick < 2 || chatAfterPick != chatBeforePick) throw new Exception("Picking another model wiped her chat: " + chatBeforePick + " then " + chatAfterPick);
                checks.Add("PASS settings menu: wander, dance, language, reply length, chat memory and the brain picker change her and show which is picked, picking another model keeps the chat, and with Ollama open but empty Brain offers to download gemma4:e4b");
                // A cloud key instead of Ollama: asked for in a hidden box, a key that isn't one is explained, a good one lists its models under Brain, and Forget goes back to Ollama.
                pet.ShowBrains(new[] { "qwen3:8b" });
                string ollamaMenu = pet.BrainMenu;
                pet.GetFreeKey();
                string freeKey = pet.Talk.Reply;
                pet.AskBrainKey();
                bool keyAsked = pet.Talk.Typing && pet.Talk.Typer.Secret && pet.Talk.Reply != null && pet.Talk.Reply.IndexOf("API key", StringComparison.Ordinal) >= 0;
                pet.Talk.Submit("not-a-key");
                string notKey = pet.Talk.Reply;
                bool keyHidden = !pet.Talk.Typer.Secret && !File.Exists(Path.Combine(folder, "voice-key"));
                pet.UseCloud("nvapi-smoke-1234", "meta/llama-3.3-70b-instruct", new List<string> { "meta/llama-3.3-70b-instruct", "qwen/qwen3-235b-a22b" });
                pet.ShowBrains(new[] { "qwen3:8b" });
                string cloudMenu = pet.BrainMenu;
                bool cloudDetails = pet.ErrorDetails().IndexOf(" | brain: NVIDIA meta/llama-3.3-70b-instruct | ", StringComparison.Ordinal) > 0;
                pet.PickCloud("qwen/qwen3-235b-a22b");
                bool cloudPicked = pet.Brain.Remote != null && pet.Brain.Remote.Model == "qwen/qwen3-235b-a22b" && pet.Brain.Model == "qwen/qwen3-235b-a22b" && pet.Saved.NvidiaModel == "qwen/qwen3-235b-a22b";
                // A stuck NVIDIA model hands over to the next one for that message, and their pick stays saved.
                pet.UseCloud("nvapi-smoke-1234", "nvidia/nemotron-3.5-super-vl-120b-a12b", new List<string> { "nvidia/nemotron-3.5-super-vl-120b-a12b", "nvidia/nemotron-3.5-bolt-e4b" });
                bool hopped = Brain.Stalled(new BrainException(Brain.Down, true)) && !Brain.Stalled(new BrainException("Something else.", true)) && pet.Hop(pet.Brain.Remote) && pet.Brain.Model == "nvidia/nemotron-3.5-bolt-e4b" && pet.Brain.Remote.Model == "nvidia/nemotron-3.5-bolt-e4b" && pet.Saved.NvidiaModel == "nvidia/nemotron-3.5-super-vl-120b-a12b";
                bool hopsEnd = !pet.Hop(pet.Brain.Remote);
                pet.SetStay(true);
                pet.UseCloud("nvapi-smoke-1234", "nvidia/nemotron-3.5-super-vl-120b-a12b", new List<string> { "nvidia/nemotron-3.5-super-vl-120b-a12b", "nvidia/nemotron-3.5-bolt-e4b", "openai/gpt-oss-20b" });
                pet.ShowBrains(null);
                bool stayed = !pet.Hop(pet.Brain.Remote) && pet.Brain.Model == "nvidia/nemotron-3.5-super-vl-120b-a12b" && pet.BrainMenu.IndexOf(" | *Stay on my model | ", StringComparison.Ordinal) > 0;
                // Nemotron Super shows as recommended.
                string nvidiaMenu = pet.BrainMenu;
                pet.SetStay(false);
                hopsEnd = hopsEnd && stayed;
                pet.ForgetBrainKey();
                bool cloudGone = pet.Brain.Remote == null && pet.Saved.NvidiaModel == null && pet.Saved.Tier == 0 && Keys.Read("nvidia") == null;
                pet.Brain.Model = "qwen3:8b";
                pet.Talk.Dismiss();
                if (ollamaMenu != "*qwen3:8b | - | Get a free NVIDIA key | " + Pet.KeyChoice) throw new Exception("The Brain menu didn't offer a cloud key: " + ollamaMenu);
                if (freeKey != Pet.FreeKeySteps || !keyAsked || notKey != Pet.NotAKey || !keyHidden) throw new Exception("The key choices didn't say where to paste, ask in a hidden box or explain a bad key: " + freeKey + " | " + keyAsked + " | " + notKey + " | " + keyHidden);
                if (cloudMenu != "NVIDIA key ending 1234 (off) | *meta/llama-3.3-70b-instruct | qwen/qwen3-235b-a22b | - | Stay on my model | Forget the key and use Ollama" || !cloudPicked || !cloudGone || !cloudDetails || !hopped || !hopsEnd
                    || nvidiaMenu != "NVIDIA key ending 1234 (off) | *nvidia/nemotron-3.5-super-vl-120b-a12b · recommended | nvidia/nemotron-3.5-bolt-e4b | openai/gpt-oss-20b | - | *Stay on my model | Forget the key and use Ollama")
                    throw new Exception("The cloud key's Brain menu went wrong: " + cloudMenu + " | " + cloudPicked + " | " + cloudGone + " | " + cloudDetails + " | " + hopped + " | " + hopsEnd + " | " + nvidiaMenu);
                checks.Add("PASS cloud brain: Brain offers a free NVIDIA key or any API key in a hidden box, explains one that isn't a key, lists a key's models to pick from with Nemotron Super and Nemotron's reasoning model marked recommended, and Forget goes back to Ollama");
                // Updates: a newer finished release puts Get Capri X in her menu and she mentions it once; older releases, drafts and odd tags don't.
                pet.Saved.UpdateSeen = null;
                pet.ShowUpdate("{\"tag_name\":\"v99.1.0\",\"draft\":false,\"prerelease\":false,\"html_url\":\"https://github.com/lachydotmcg/capri-desktop/releases/tag/v99.1.0\",\"assets\":[{\"name\":\"Capri-linux-x64.tar.gz\"}]}");
                string updateChoice = pet.UpdateChoice;
                string updateNews = pet.UpdateNews;
                string updatePage = pet.UpdatePage;
                pet.UpdateNews = null;
                pet.ShowUpdate("{\"tag_name\":\"v99.1.0\",\"draft\":false,\"prerelease\":false}");
                string updateAgain = pet.UpdateNews;
                string updateKept = pet.UpdateChoice;
                pet.ShowUpdate("{\"tag_name\":\"v99.2.0\",\"draft\":true}");
                string draftChoice = pet.UpdateChoice;
                pet.ShowUpdate("{\"tag_name\":\"v99.3.0\",\"html_url\":\"https://example.com/capri\"}");
                string oddPage = pet.UpdatePage;
                pet.ShowUpdate("{\"tag_name\":\"v0.0.1\"}");
                string olderChoice = pet.UpdateChoice;
                pet.ShowUpdate("not json");
                bool oddTags = Pet.Newer("nightly", new Version(0, 7, 5)) == null && Pet.Newer("v0.7.5", new Version(0, 7, 5)) == null && Pet.Newer("0.7.10", new Version(0, 7, 9)) == "0.7.10" && Pet.Newer("v1.0", new Version(0, 7, 5)) == "1.0.0" && Pet.Newer("v0.7.6-beta", new Version(0, 7, 5)) == null;
                pet.UpdateNews = null;
                pet.Saved.UpdateSeen = null;
                pet.Save();
                if (updateChoice != "Get Capri 99.1.0" || updateNews != "Capri 99.1.0 is out. Right-click me > Get Capri 99.1.0." || updatePage != "https://github.com/lachydotmcg/capri-desktop/releases/tag/v99.1.0") throw new Exception("A newer release wasn't offered: " + updateChoice + " | " + updateNews + " | " + updatePage);
                if (updateAgain != null || updateKept != "Get Capri 99.1.0" || draftChoice != null || oddPage != "https://github.com/lachydotmcg/capri-desktop/releases/latest" || olderChoice != null || pet.UpdateChoice != null || !oddTags) throw new Exception("The update notice went wrong: " + updateAgain + " | " + updateKept + " | " + draftChoice + " | " + oddPage + " | " + olderChoice + " | " + pet.UpdateChoice + " | " + oddTags);
                checks.Add("PASS updates: a newer GitHub release puts Get Capri X in her right-click menu and she says so once; older releases, drafts, odd tags and other sites don't");
                // Check my screen: the right screenshot tool for the desktop, shrunk to a JPEG, and a plain answer when no tool works.
                bool toolsRight = Eyes.Tools(true, null, null)[0][0] == "screencapture" && Eyes.Tools(false, "x11", "XFCE")[0][0] == "import" && Eyes.Tools(false, "wayland", "KDE")[0][0] == "spectacle"
                    && Eyes.Tools(false, "wayland", "ubuntu:GNOME")[0][0] == "gnome-screenshot" && Eyes.Tools(false, "wayland", "Hyprland")[0][0] == "grim" && Eyes.Tools(false, "wayland", "Hyprland").Count == 3;
                byte[] wideShot;
                using (SkiaSharp.SKBitmap wide = new SkiaSharp.SKBitmap(2000, 1000))
                {
                    wide.Erase(new SkiaSharp.SKColor(30, 200, 120));
                    using (SkiaSharp.SKImage image = SkiaSharp.SKImage.FromBitmap(wide))
                    using (SkiaSharp.SKData png = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100)) wideShot = png.ToArray();
                }
                byte[] smallShot = Eyes.Jpeg(wideShot, 1280);
                bool shrunk = smallShot != null && smallShot.Length > 3 && smallShot[0] == 0xFF && smallShot[1] == 0xD8;
                if (shrunk) using (SkiaSharp.SKBitmap back = SkiaSharp.SKBitmap.Decode(smallShot)) shrunk = back != null && back.Width == 1280 && back.Height == 640;
                Func<byte[]> shootWas = Eyes.Shoot;
                Eyes.Shoot = delegate { return null; };
                string noShot = Hands.Do("look_at_screen", new ToolArguments());
                Eyes.Shoot = shootWas;
                if (!toolsRight || !shrunk || noShot != Eyes.Missing || Eyes.Jpeg(new byte[] { 1, 2, 3 }, 1280) != null) throw new Exception("Check my screen went wrong: " + toolsRight + " | " + shrunk + " | " + noShot);
                checks.Add("PASS check my screen: the right screenshot tool for Mac, X11, KDE, GNOME or wlroots desktops, shrunk to a 1280-wide JPEG for her brain, and a plain answer when no tool works");
                // Her Discord status: the switch, plain states, and Discord's socket found by name in its temp folder.
                string ipcFolder = Path.Combine(folder, "ipc");
                Directory.CreateDirectory(ipcFolder);
                File.WriteAllText(Path.Combine(ipcFolder, "discord-ipc-3"), "");
                string tmpWas = Environment.GetEnvironmentVariable("TMPDIR");
                List<string> ipcNames;
                Environment.SetEnvironmentVariable("TMPDIR", ipcFolder);
                try { ipcNames = Presence.Names(); }
                finally { Environment.SetEnvironmentVariable("TMPDIR", tmpWas); }
                pet.SetDiscord(false);
                bool discordOff = pet.Saved.NoDiscord && !pet.DiscordShown;
                pet.SetDiscord(true);
                bool discordOn = !pet.Saved.NoDiscord && pet.DiscordShown;
                if (!ipcNames.Contains(Path.Combine(ipcFolder, "discord-ipc-3")) || !discordOff || !discordOn || Array.IndexOf(new[] { "Hanging out", "Chatting", "Dancing to music", "Taking a break" }, pet.Doing) < 0)
                    throw new Exception("Her Discord status went wrong: " + string.Join(", ", ipcNames) + " / off " + discordOff + " / on " + discordOn + " / " + pet.Doing);
                checks.Add("PASS Discord status: Settings > Show on Discord turns it off and on, she says what she's doing in plain words, and she finds Discord's socket in its temp folder");
                string details = pet.ErrorDetails();
                if (!details.StartsWith("Capri v", StringComparison.Ordinal) || details.IndexOf(" | brain: ", StringComparison.Ordinal) < 0 || details.IndexOf(" | voice: ", StringComparison.Ordinal) < 0)
                    throw new Exception("Her error details were wrong: " + details);
                checks.Add("PASS Help > Copy error details: her version, the system, her brain and voice, then her error log");
                pet.AskNickname();
                bool askedName = pet.Talk.Typing && pet.Talk.Reply != null && pet.Talk.Reply.StartsWith("What should I call you?", StringComparison.Ordinal);
                pet.Talk.Submit("  Lachy!!  ");
                bool namedRight = !pet.Talk.Typing && Brain.Nickname == "Lachy" && pet.Saved.Nickname == "Lachy" && pet.Talk.Reply == "Got it, Lachy." && Brain.Character.IndexOf("call them: Lachy.", StringComparison.Ordinal) >= 0;
                pet.SetNickname("shithead");
                bool rudeName = Brain.Nickname == "shithead" && pet.Saved.Nickname == "shithead" && pet.Talk.Reply == "Got it, shithead.";
                pet.SetNickname("choom");
                bool clearedName = Brain.Nickname == null && pet.Saved.Nickname == null && pet.Talk.Reply == "Back to choom, then." && Brain.Character.IndexOf("set a nickname", StringComparison.Ordinal) < 0;
                if (!askedName || !namedRight || !rudeName || !clearedName) throw new Exception("The nickname went wrong: asked " + askedName + ", named " + namedRight + ", rude " + rudeName + ", cleared " + clearedName);
                checks.Add("PASS nickname: Settings > What should she call you? asks in her typing box, saves a cleaned-up name she says back, rude ones too since it's their call, and choom goes back to choom");
                pet.Hear("remember I love Hollow Knight");
                string saved = pet.Talk.Reply;
                string kept = Brain.Notes();
                pet.Hear("forget about hollow knight");
                string forgot = pet.Talk.Reply;
                string notesPath = pet.OpenNotes();
                if (saved != "Saved to my notes: They love Hollow Knight." || kept != "- They love Hollow Knight" || forgot != "Forgot my note about hollow knight." || Brain.Notes() != "" || notesPath == null || !File.ReadAllText(notesPath).StartsWith("# What Capri remembers", StringComparison.Ordinal)) throw new Exception("Her notes didn't work: " + saved + " / " + kept + " / " + forgot);
                checks.Add("PASS notes: remember and forget save to me.md here too, and Open her notes makes the file");
                pet.GetToKnow();
                bool meetAsked = pet.GettingToKnow && pet.Talk.Typing && pet.Talk.Reply == Meeting.AskName;
                pet.Talk.Submit("hey");
                bool meetNudged = pet.Talk.Typing && pet.Talk.Reply == Meeting.AskCalled;
                pet.Talk.Submit("call me Lachy!!");
                bool meetNamed = Brain.Nickname == "Lachy" && pet.Saved.Nickname == "Lachy" && pet.Talk.Typing && pet.Talk.Reply == "Nice to meet you, Lachy. " + Meeting.AskPronouns;
                pet.Talk.Submit("she");
                bool meetPronouns = pet.Talk.Typing && pet.Talk.Reply == "Got it. " + Meeting.AskInto;
                pet.Talk.Submit("I'm into Cyberpunk, Roblox and drawing");
                string meetNotes = Brain.Notes();
                bool meetKept = !pet.GettingToKnow && meetNotes == "- Name: Lachy\n- Pronouns: she/her\n- Into: Cyberpunk, Roblox and drawing" && pet.Talk.Reply == "Love that, Lachy. It's all in my notes now, me.md. " + Meeting.Again && Brain.Remembered().IndexOf("match words like girl, boy or choom", StringComparison.Ordinal) >= 0;
                pet.GetToKnow();
                pet.Talk.Submit("skip");
                bool meetSkipped = pet.Talk.Reply == Meeting.AskPronouns;
                pet.Talk.Submit("they/them");
                pet.Talk.Submit("stop");
                bool meetStopped = !pet.GettingToKnow && Brain.Notes() == "- Name: Lachy\n- Pronouns: they/them\n- Into: Cyberpunk, Roblox and drawing" && Brain.Nickname == "Lachy" && pet.Talk.Reply == "No worries. What you told me is in my notes, me.md. " + Meeting.Again;
                pet.GetToKnow();
                pet.Talk.Submit("remember I'm learning guitar");
                bool meetHandedOn = !pet.GettingToKnow && pet.Talk.Reply == "Saved to my notes: They're learning guitar.";
                Brain.Unnote("everything");
                pet.SetNickname("choom");
                pet.Talk.Dismiss();
                if (!meetAsked || !meetNudged || !meetNamed || !meetPronouns || !meetKept || !meetSkipped || !meetStopped || !meetHandedOn)
                    throw new Exception("Getting to know them went wrong: asked " + meetAsked + ", nudged " + meetNudged + ", named " + meetNamed + ", pronouns " + meetPronouns + ", kept " + meetKept + " (" + meetNotes.Replace("\n", " | ") + "), skipped " + meetSkipped + ", stopped " + meetStopped + ", handed on " + meetHandedOn);
                checks.Add("PASS getting to know you: Settings > Get to know me asks what to call them, their pronouns and what they're into in her typing box, writes them to me.md and calls them by their name; skip, stop and asking again work, and something for her to do ends it and is heard as usual");
                string today = Path.Combine(Brain.TranscriptPath, DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + ".md");
                Brain.Transcribe("You", "we finally beat the smoke boss");
                bool transcribed = File.Exists(today) && File.ReadAllText(today).Contains("You:** we finally beat the smoke boss");
                bool rememberedOn = pet.Remembers && Brain.Keeping && !pet.Saved.NoMemories;
                pet.SetRemembering(false);
                Brain.Transcribe("You", "this line stays out");
                bool stoppedWriting = !File.ReadAllText(today).Contains("this line stays out") && !pet.Remembers && pet.Saved.NoMemories && !Brain.Keeping;
                pet.SetRemembering(true);
                string memoriesPath = pet.OpenMemories();
                string recalledBoss = Brain.Recalled("remember when we beat the smoke boss?");
                // Talking about it again digs it up unasked, unless it's already in her chat; small talk digs up nothing.
                string dugBoss = Brain.Recalled("the smoke boss is back for a rematch");
                string dugShown = Brain.Recalled("the smoke boss is back for a rematch", new[] { "we finally beat the smoke boss" });
                string dugSmall = Brain.Recalled("how's it going?");
                if (!transcribed || !rememberedOn || !stoppedWriting || !pet.Remembers || !Brain.Keeping || memoriesPath != Path.Combine(folder, "memories.md") || !File.Exists(memoriesPath) || !Directory.Exists(Brain.TranscriptPath) || recalledBoss.IndexOf("You: we finally beat the smoke boss", StringComparison.Ordinal) < 0
                    || dugBoss.IndexOf("earlier moments that may connect", StringComparison.Ordinal) < 0 || dugBoss.IndexOf("You: we finally beat the smoke boss", StringComparison.Ordinal) < 0 || dugShown.IndexOf("we finally beat the smoke boss", StringComparison.Ordinal) >= 0 || dugSmall != "")
                    throw new Exception("Remember our chats went wrong: " + transcribed + " | " + rememberedOn + " | " + stoppedWriting + " | " + recalledBoss + " | " + dugBoss + " | " + dugShown + " | " + dugSmall);
                checks.Add("PASS remember our chats: on by default here too, a chat line goes in today's transcript, \"remember when\" finds it, talking about it again digs it up unasked unless it's already in her chat, small talk digs up nothing, off writes nothing, and Open her memories makes memories.md");
                bool personaFixed = Brain.Persona == Brain.DefaultPersonality + "\n" + Brain.Safety + "\n" + Brain.Machinery && Brain.Safety.IndexOf("findahelpline.com", StringComparison.Ordinal) >= 0 && Brain.DefaultPersonality.IndexOf("helpline", StringComparison.Ordinal) < 0 && Brain.Character.StartsWith(Brain.Persona, StringComparison.Ordinal);
                if (!personaFixed) throw new Exception("Her personality or safety lines aren't fixed in code: " + Brain.Persona.Length + " characters");
                checks.Add("PASS her personality: fixed in code with her safety lines right after it and her reply rules last, with no personality.md to edit");
                List<short[]> typedHeard = new List<short[]>();
                Sfx.TypeOut = delegate(short[] typed) { typedHeard.Add(typed); };
                Sfx.Untyped = delegate { };
                pet.Hear("remember I love Silksong");
                int typedOn = typedHeard.Count;
                double typedSeconds = typedOn == 1 ? (double)typedHeard[0].Length / Sfx.Rate : 0;
                double typedWanted = pet.Talk.Reply.Length / Talk.LettersPerSecond + 0.05;
                pet.SetTyping(false);
                bool typingOff = !pet.TypingSound && !Sfx.Typing;
                pet.Hear("forget about silksong");
                int typedOff = typedHeard.Count;
                pet.SetTyping(true);
                pet.AutoTyping();
                Sfx.TypeOut = null;
                Sfx.Untyped = null;
                if (typedOn != 1 || Math.Abs(typedSeconds - typedWanted) > 0.02 || !typingOff || typedOff != 1 || typedHeard.Count != 2 || !pet.TypingSound || Brain.Notes() != "" || !Sfx.Typing) throw new Exception("Typing sound went wrong: " + typedOn + " | " + typedSeconds + " | " + typingOff + " | " + typedOff + " | " + typedHeard.Count);
                checks.Add("PASS typing sound: a text-only reply plays soft typing ticks, Settings > Sounds > Typing sound turns them off");
                pet.Talk.Dismiss();
                pet.Think = null;
                pet.Brain.Model = null;
                pet.Hear("hello?");
                if (pet.Talk.Reply != Pet.Offline) throw new Exception("No brain didn't explain how to get one: " + pet.Talk.Reply);
                checks.Add("PASS no brain: she says to install Ollama and pull a model");
                pet.Talk.Dismiss();
                pet.Hear("dance for me");
                bool brainlessDance = pet.Dancing && pet.Talk.Reply == "Dancing. Tell me to stop when you've had enough.";
                pet.Talk.Dismiss();
                pet.Hear("stop dancing");
                bool brainlessStop = !pet.Dancing && pet.Talk.Reply == "Stopped dancing.";
                pet.Talk.Dismiss();
                if (!brainlessDance || !brainlessStop) throw new Exception("Her dance needed a brain: " + brainlessDance + ", " + brainlessStop + ", " + pet.Talk.Reply);
                checks.Add("PASS her own moves without a brain: \"dance for me\" and \"stop dancing\" work with no brain set up, like her nap and \"teleport here\"");
                pet.Think = delegate(string words) { return words == "hey" ? "Just vibing, choom." : "wrong words"; };
                pet.Talk.Pill.Press(1);
                if (!pet.Talk.Typing) throw new Exception("The pencil didn't open the typing box.");
                pet.Talk.Submit("hey");
                return true;
            }
            if (step == 3)
            {
                if (!pet.Talk.Done) return false;
                if (settled < 0) settled = now;
                if (now - settled < 0.3) return false;
                if (pet.Talk.Shown != "Just vibing, choom.") throw new Exception("Her reply showed wrong: " + pet.Talk.Shown);
                Bubble bubble = pet.Talk.Bubble;
                if (bubble.Position.Y + (int)Math.Round(bubble.Height * bubble.DesktopScaling) > pet.Head.Y + 2) throw new Exception("Her bubble covered her head.");
                if (!pet.Talk.Pill.IsVisible) throw new Exception("The pencil and mic pill didn't show under her while she talked.");
                Snap(pet.Talk.Face, "talk.png");
                Snap(pet.Talk.Typer.Content as Control, "typer.png");
                Snap(pet.Talk.Pill.Content as Control, "pill.png");
                Scene("desktop.png", new Window[] { pet, bubble, pet.Talk.Pill }, new PixelRect[0]);
                string pillLook = PillFace.Check(Path.Combine(folder, "pill-states.png"));
                if (pillLook != null) throw new Exception(pillLook);
                if (!pet.Talk.Pill.GlitchesIn(now)) throw new Exception("The pill didn't glitch in when it showed, or a hover glitched it again.");
                checks.Add("PASS hover pill: black cut-corner plates with a pink-to-cyan neon edge, TYPE and TALK and a segmented wave like Windows, the mic level and a red MUTED drawn the same, crisp at 100%, 150% and 200% with the same size and hit areas, and a glitch only when it shows");
                checks.Add("PASS chat: the typing box sends a line to her brain, the reply types out in a bubble above her head");
                // Chinese takes two columns a character and wraps without spaces, a long link breaks across lines, and every
                // letter still shows once she's done typing.
                string hanziReply = "你好，朋友！今天过得怎么样？";
                string wrapText = Sheet.Speaker + hanziReply + hanziReply + hanziReply + hanziReply + " https://example.com/a/very/long/link/that/needs/more/than/one/line ok";
                List<string> wrapped = Look.Wrap(wrapText, Sheet.Columns);
                int[] wrapTakes = Look.Takes(wrapped, wrapText, wrapText.Length);
                int wrapShown = 0;
                bool wrapFits = true;
                for (int wrapAt = 0; wrapAt < wrapped.Count; wrapAt++)
                {
                    wrapShown += wrapTakes[wrapAt];
                    if (Look.Cells(wrapped[wrapAt]) > Sheet.Columns || (wrapAt > 0 && wrapped[wrapAt].Length > 0 && "，！？".IndexOf(wrapped[wrapAt][0]) >= 0)) wrapFits = false;
                }
                if (!wrapFits || wrapShown != string.Join("", wrapped).Length || string.Join("", wrapped).Replace(" ", "") != wrapText.Replace(" ", "") || wrapped.Count < 5
                    || string.Join("|", Look.Wrap("CAPRI: hey choom, long day?", 12)) != "CAPRI: hey|choom, long|day?" || !Look.RightToLeft("مرحبا 2077") || !Look.RightToLeft("2077 مرحبا") || Look.RightToLeft("Hey مرحبا"))
                    throw new Exception("Her bubble wrapped wrong: " + string.Join(" | ", wrapped));
                string thaiText = "CAPRI: เฮ้ เพื่อน วันนี้เหนื่อยไหม อยากให้ฉันเปิดเพลงเบาๆ ให้ฟังระหว่างทำงานหรือเปล่า บอกมาได้เลยนะ";
                List<string> thaiLines = Look.Wrap(thaiText, 16);
                bool thaiFits = thaiLines.TrueForAll(delegate(string thaiRow) { return Look.Cells(thaiRow) <= 16 && thaiRow.Length > 0 && Look.Cells(thaiRow[0]) > 0 && "ะาำๅๆ".IndexOf(thaiRow[0]) < 0; });
                bool[] thaiLetters = Look.Letters("เปิดเพลง");
                if (!thaiFits || string.Join("", thaiLines).Replace(" ", "") != thaiText.Replace(" ", "") || Look.Cells("วันนี้อากาศดีมาก") != 12 || !Look.Thai("hey วันนี้") || Look.Thai("hey choom")
                    || thaiLetters[1] || thaiLetters[2] || !thaiLetters[3] || !thaiLetters[4] || thaiLetters[5])
                    throw new Exception("Thai wrapped wrong: " + string.Join(" | ", thaiLines));
                bool[] laoLetters = Look.Letters("ມື້ນີ້ເຈົ້າເຮັດຫຍັງຢູ່"), burmeseLetters = Look.Letters("မင်္ဂလာပါ");
                string joinedCut = "";
                for (int n = 1; n <= 12; n++)
                    foreach (string joined in new[] { "ថ្ងៃនេះ", "क्षमा करो", "မင်္ဂလာပါ" })
                    {
                        string start = Look.Cut(joined, n);
                        if (start.EndsWith("្") || start.EndsWith("्") || start.EndsWith("္") || start == "မ") joinedCut += start + " (" + n + ") ";
                    }
                if (!laoLetters[3] || !laoLetters[6] || laoLetters[7] || laoLetters[10] || !laoLetters[11] || laoLetters[12] || joinedCut.Length > 0
                    || burmeseLetters[1] || burmeseLetters[4] || !burmeseLetters[5] || burmeseLetters[6] || !burmeseLetters[7] || burmeseLetters[8] || !Look.Unspaced("မင်္ဂလာပါ"))
                    throw new Exception("A Lao vowel, a Burmese syllable or a joined letter came apart: " + laoLetters[7] + " " + laoLetters[10] + " " + laoLetters[12] + " " + burmeseLetters[1] + " " + burmeseLetters[4] + " / " + joinedCut);
                checks.Add("PASS bubble lines: Chinese, Japanese and Korean take two columns and wrap without spaces, never starting a line with closing punctuation, a long link breaks across lines without losing letters, Arabic, Urdu, Persian and Hebrew lines read right to left, Thai, Lao and Burmese break between letters, never before a vowel or tone mark, after a vowel written first or inside a Burmese syllable, and a virama or Khmer coeng keeps its letters together");
                pet.Talk.Dismiss();
                PixelRect area = Work();
                shelfAsked = new PixelRect(area.X + area.Width / 2 - Px(350), area.Y + area.Height / 2 - Px(140), Px(700), Px(360));
                shelf = Shelf(shelfAsked);
                return true;
            }
            if (step == 4)
            {
                shelfSpot = Near(shelfAsked, true);
                if (shelfSpot == null) return false;
                PixelPoint letAt = pet.Hip;
                pet.Grab();
                pet.Let(true);
                Ledge here = pet.Seat;
                if (here == null || !here.Free || pet.Hip.Y != here.Y || Math.Abs(pet.Hip.X - letAt.X) > Px(60) || pet.SitChoice != "Stand up") throw new Exception("Shift + drop didn't sit her where she was let go: seat " + (here == null ? "none" : here.Left + "-" + here.Right + " at " + here.Y + (here.Free ? " free" : "")) + ", hip " + pet.Hip + " from " + letAt + ", menu " + pet.SitChoice);
                checks.Add("PASS Shift + drop: she sits right where she's let go, and her menu says Stand up");
                pet.StandUp();
                if (pet.SitChoice != "Sit here") throw new Exception("Her menu still said Stand up after she stood.");
                pet.Move(pet.Seated(shelfSpot.Bounds.X + Px(220), shelfSpot.Bounds.Y - Px(12)));
                if (!pet.Drop()) throw new Exception("She couldn't reach the test shelf 12 px under her.");
                return true;
            }
            if (step == 5)
            {
                if (pet.Falling) return false;
                Ledge seat = pet.Seat;
                if (seat == null || seat.Window != shelfSpot.Window || pet.Hip.Y != shelfSpot.Bounds.Y) throw new Exception("She didn't land on the test shelf's top edge.");
                if (pet.Showing != pet.PerchGlitch) throw new Exception("Sitting on the shelf didn't glitch her on.");
                checks.Add("PASS sitting: dropped over a window, she lands on its top edge and glitches into her seat");
                Scene("shelf.png", new Window[] { pet }, new[] { shelfSpot.Bounds });
                hipBefore = pet.Hip;
                dx = Px(120);
                dy = Px(60);
                Send(shelf, "move " + (shelfAsked.X + dx) + " " + (shelfAsked.Y + dy));
                return true;
            }
            if (step == 6)
            {
                Ledge seat = pet.Seat;
                if (seat == null) throw new Exception("She stood up when the shelf moved.");
                if (seat.Y != shelfSpot.Bounds.Y + dy) return false;
                if (pet.Hip.Y != seat.Y || pet.Hip.X != hipBefore.X + dx) throw new Exception("She didn't move with the shelf: hip " + pet.Hip + ", expected " + new PixelPoint(hipBefore.X + dx, seat.Y) + ".");
                checks.Add("PASS following: when the window moves, she moves with it and stays seated");
                coverAsked = new PixelRect(pet.Hip.X - Px(130), seat.Y - Px(60), Px(260), Px(200));
                cover = Shelf(coverAsked);
                return true;
            }
            if (step == 7)
            {
                if (coverAt < 0)
                {
                    if (Near(coverAsked, false) == null) return false;
                    coverAt = now;
                }
                if (pet.Seat != null) return false;
                double waited = now - coverAt;
                if (waited < 0.45 || waited > 2) throw new Exception("She stood up " + waited.ToString("0.00") + " s after a window covered her seat; it should be about 0.7 s.");
                if (pet.Falling) throw new Exception("Standing up made her fall.");
                checks.Add("PASS covered: a window over her seat makes her stand up after " + waited.ToString("0.0") + " s");
                Send(cover, "quit");
                return true;
            }
            if (step == 8)
            {
                if (!slept)
                {
                    if (!pet.Upright) return false;
                    pet.Talk.Dismiss();
                    pet.SetSleepAfter(0);
                    pet.Doze(100000);
                    bool never = !pet.Asleep && pet.SleepPicked == "Never";
                    pet.SetSleepAfter(120);
                    pet.Doze(119);
                    bool early = pet.Asleep;
                    pet.Doze(130);
                    bool dozed = pet.Asleep && pet.Doing == "Asleep";
                    pet.Doze(140);
                    bool stayed = pet.Asleep;
                    pet.Doze(0.5);
                    bool woke = pet.Waking;
                    if (!never || early || !dozed || !stayed || !woke || pet.SleepPicked != "2 min") throw new Exception("Sleep went wrong: never " + never + ", early " + early + ", dozed " + dozed + ", stayed " + stayed + ", woke " + woke + ", picked " + pet.SleepPicked);
                    checks.Add("PASS sleep: standing, she dozes off after Fall asleep after without the mouse or keyboard (2 min here), shows Asleep on Discord, wakes with her boot glitch when they're back, and Never keeps her up");
                    slept = true;
                }
                if (!Napped()) return false;
                if (!Moved()) return false;
                if (!cover.HasExited) return false;
                shelfSpot = Near(new PixelRect(shelfAsked.X + dx, shelfAsked.Y + dy, shelfAsked.Width, shelfAsked.Height), true);
                if (shelfSpot == null) return false;
                pet.Move(pet.Seated(shelfSpot.Bounds.X + Px(220), shelfSpot.Bounds.Y - Px(12)));
                if (!pet.Drop()) throw new Exception("She couldn't get back onto the shelf.");
                return true;
            }
            if (step == 9)
            {
                if (pet.Falling) return false;
                if (pet.Seat == null || pet.Seat.Window != shelfSpot.Window) throw new Exception("She didn't sit back on the shelf.");
                Send(shelf, "hide");
                hideAt = now;
                return true;
            }
            if (step == 10)
            {
                if (pet.Seat != null) return false;
                double waited = now - hideAt;
                if (waited > 1.5) throw new Exception("She took " + waited.ToString("0.00") + " s to stand up after her window hid.");
                checks.Add("PASS window gone: when her window hides, she stands up after " + waited.ToString("0.0") + " s");
                Send(shelf, "show");
                return true;
            }
            if (step == 11)
            {
                shelfSpot = Near(new PixelRect(shelfAsked.X + dx, shelfAsked.Y + dy, shelfAsked.Width, shelfAsked.Height), true);
                if (shelfSpot == null) return false;
                pet.StandUp();
                pet.Move(pet.Seated(shelfSpot.Bounds.X + Px(200), shelfSpot.Bounds.Y - Px(12)));
                if (!pet.Drop()) throw new Exception("She couldn't get back onto the shown shelf.");
                return true;
            }
            if (step == 12)
            {
                if (pet.Falling) return false;
                if (pet.Seat == null || pet.Seat.Window != shelfSpot.Window) throw new Exception("She didn't sit on the shelf before walking.");
                hipBefore = pet.Hip;
                if (!pet.Stroll() || !pet.Walking) throw new Exception("She couldn't start walking along the shelf.");
                if (pet.Hip.Y != shelfSpot.Bounds.Y) throw new Exception("Standing up to walk lifted her off the shelf: hip " + pet.Hip + ".");
                return true;
            }
            if (step == 13)
            {
                if (pet.Walking)
                {
                    if (pet.Hip.Y != pet.Seat.Y) throw new Exception("Her feet left the shelf while walking.");
                    return false;
                }
                if (!Sitting() || pet.Seat.Window != shelfSpot.Window || pet.Hip.X == hipBefore.X) throw new Exception("She didn't walk and sit back down on the shelf.");
                checks.Add("PASS walking: she gets up, walks along her window with her feet on it, and sits back down " + Math.Abs(pet.Hip.X - hipBefore.X) + " px away");
                hipBefore = pet.Hip;
                if (!pet.Blink() || !pet.Leaving) throw new Exception("She couldn't teleport along the shelf.");
                return true;
            }
            if (step == 14)
            {
                if (pet.Leaving || pet.Arriving || pet.Falling) return false;
                if (!blinked)
                {
                    if (pet.Walking || pet.Seat == null || pet.Seat.Window != shelfSpot.Window || pet.Hip.Y != shelfSpot.Bounds.Y || pet.Hip.X == hipBefore.X) throw new Exception("She didn't teleport to a new spot on the same shelf: hip " + pet.Hip + ".");
                    checks.Add("PASS teleporting: wander glitches her out and back in " + Math.Abs(pet.Hip.X - hipBefore.X) + " px along her window instead of walking");
                    blinked = true;
                    if (!pet.Wander() || !pet.Leaving) throw new Exception("She had nowhere to hop to.");
                    return false;
                }
                if (pet.Seat == null || (pet.Seat.Window == shelfSpot.Window && pet.Seat.Y == shelfSpot.Bounds.Y)) throw new Exception("She didn't glitch over to a new spot.");
                checks.Add("PASS hopping: wander glitches her out and back in on " + (pet.Seat.Floor ? "the bottom of the screen" : "another window"));
                pet.HearMusic(true);
                return true;
            }
            if (step == 15)
            {
                if (pet.Showing != pet.SeatDance) return false;
                Snap(pet.Content as Control, "dance.png");
                pet.HearMusic(false);
                checks.Add("PASS dancing: she dances in her seat while music plays");
                outFrom = now;
                Sfx.Out = sounds.Add;
                pet.HideAway();
                return true;
            }
            if (step == 16)
            {
                if (pet.IsVisible) return false;
                if (now - outFrom < 0.6) throw new Exception("Her glitch-out ended after " + (now - outFrom).ToString("0.00") + " s.");
                if (!pet.Away || sounds.Count != 1 || sounds[0].Length != Sfx.Make(Cue.Leave, 60).Length) throw new Exception("Her glitch-out didn't play its sound once: " + sounds.Count);
                checks.Add("PASS glitch out: Hide her plays her glitch-out and its sound for " + (now - outFrom).ToString("0.0") + " s, then her window goes");
                pet.ToggleShush();
                bool hushedHidden = pet.Shushed && pet.Talk.Reply == null && !pet.Talk.Bubble.IsVisible && !pet.Talk.Pill.IsVisible;
                pet.ToggleShush();
                if (!hushedHidden || pet.Shushed || !pet.Talk.Quiet || pet.Talk.Bubble.IsVisible) throw new Exception("Quiet from the tray while she was hidden left a bubble up: " + pet.Talk.Reply);
                checks.Add("PASS hidden: Quiet from the tray while she's hidden puts up no bubble with nobody under it, and she's still free to pop in");
                arrivalsBefore = pet.Arrivals;
                pet.ComeBack();
                if (!pet.IsVisible || pet.Away || !pet.Arriving || pet.Arrivals != arrivalsBefore + 1) throw new Exception("Show her didn't glitch her back in.");
                return true;
            }
            if (step == 17)
            {
                if (pet.Arriving || pet.Falling) return false;
                if (!Sitting()) throw new Exception("After Show her she didn't sit down again.");
                if (sounds.Count != 2 || sounds[1].Length != Sfx.Make(Cue.Arrive, 60).Length) throw new Exception("Show her didn't play the arrive sound: " + sounds.Count);
                checks.Add("PASS show: Show her glitches her back in with its sound and she drops onto a seat");
                Voice.Host = FakeVoice();
                Voice.Playback = delegate(byte[] wav) { lock (spoken) spoken.Add(wav); };
                Voice.Silence = delegate { };
                pet.Talk.Dismiss();
                if (pet.VoiceStatus != "no key | no voice | quiet") throw new Exception("She started with a voice: " + pet.VoiceStatus);
                pet.AskVoiceKey();
                if (!pet.Talk.Typing || !pet.Talk.Typer.Secret || pet.Talk.Reply == null || pet.Talk.Reply.IndexOf("ElevenLabs API key", StringComparison.Ordinal) < 0) throw new Exception("Add ElevenLabs key didn't ask for the key in a hidden box.");
                pet.Talk.Submit("bad-key");
                return true;
            }
            if (step == 18)
            {
                if (pet.Talk.Reply == null) return false;
                if (pet.Talk.Reply != Voice.RejectedNew || pet.VoiceStatus != "no key | no voice | quiet" || Keys.Read("voice") != null || pet.Talk.Typer.Secret) throw new Exception("A rejected ElevenLabs key wasn't explained, or was kept: " + pet.Talk.Reply + " | " + pet.VoiceStatus);
                pet.AskVoiceKey();
                pet.Talk.Submit("good-key-1234");
                return true;
            }
            if (step == 19)
            {
                if (pet.Talk.Reply == null) return false;
                string keyPath = Path.Combine(folder, "voice-key");
                string savedKey = Keys.Read("voice");
                string[] asked;
                lock (voiceAsked) asked = voiceAsked.ToArray();
                int voiced;
                lock (spoken) voiced = spoken.Count;
                bool shut = !OperatingSystem.IsLinux() || File.GetUnixFileMode(keyPath) == (UnixFileMode.UserRead | UnixFileMode.UserWrite);
                string last = asked.Length == 0 ? "" : asked[asked.Length - 1];
                if (pet.Talk.Reply != "Key saved. I'll talk as Capri Test. Change it under Settings > Voice." || savedKey != "good-key-1234" || !shut || (OperatingSystem.IsMacOS() && File.Exists(keyPath)) || pet.VoiceStatus != "key 1234 | Capri Test | speaking" || voiced != 1 || !last.StartsWith("POST /v1/text-to-speech/v1?", StringComparison.Ordinal) || last.IndexOf(" good-key-1234 ", StringComparison.Ordinal) < 0) throw new Exception("Saving a good ElevenLabs key went wrong: " + pet.Talk.Reply + " | " + pet.VoiceStatus + " | " + voiced + " | " + string.Join(" / ", asked));
                string voiceMenu = pet.VoiceMenu;
                if (voiceMenu != "*Use ElevenLabs | - | *Capri Test | Adam | - | New key... | Remove key ending 1234") throw new Exception("The Voice menu listed the wrong things: " + voiceMenu);
                checks.Add("PASS voice key: Settings > Voice asks for the key in a hidden box, explains and drops a rejected key, keeps a good one in " + (OperatingSystem.IsMacOS() ? "Keychain" : "a private file") + " and picks their own voice first");
                if (pet.TypingSound) throw new Exception("Typing sound stayed on while her ElevenLabs voice works.");
                pet.Think = delegate(string words) { return "Preem question, choom."; };
                pet.Hear("talk to me");
                return true;
            }
            if (step == 20)
            {
                if (pet.Talk.Reply == null) return false;
                string[] asked;
                lock (voiceAsked) asked = voiceAsked.ToArray();
                int voiced;
                lock (spoken) voiced = spoken.Count;
                string last = asked.Length == 0 ? "" : asked[asked.Length - 1];
                if (pet.Talk.Reply != "Preem question, choom." || voiced != 2 || last.IndexOf("question", StringComparison.Ordinal) < 0 || Math.Abs(pet.Talk.Pace - 22) > 1.5) throw new Exception("Her reply wasn't spoken at the voice's pace: " + pet.Talk.Reply + " | " + voiced + " | " + pet.Talk.Pace + " | " + last);
                pet.SetSpeak(false);
                pet.Hear("again");
                return true;
            }
            if (step == 21)
            {
                if (pet.Talk.Reply == null) return false;
                int voiced;
                lock (spoken) voiced = spoken.Count;
                if (voiced != 2 || pet.VoiceStatus != "key 1234 | Capri Test | quiet" || !pet.TypingSound) throw new Exception("Speak replies off still spoke, or typing stayed off: " + voiced + " | " + pet.VoiceStatus);
                pet.ForgetVoice();
                if (Keys.Read("voice") != null || pet.VoiceStatus != "no key | v1 | quiet" || pet.VoiceMenu != "Add ElevenLabs key...") throw new Exception("Remove key didn't remove it: " + pet.VoiceStatus + " | " + pet.VoiceMenu);
                checks.Add("PASS voice: replies are spoken through ElevenLabs and type out at the voice's pace, Use ElevenLabs off keeps her quiet, and Remove key deletes it");
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
                if (string.Join(" | ", pet.CloudModels) != Nemotron + " | openai/gpt-oss-20b" || pet.BrainChoices != 3) throw new Exception("The Brain menu didn't list the NVIDIA models: " + string.Join(" | ", pet.CloudModels));
                pet.PickModel("openai/gpt-oss-20b");
                if (pet.Brain.Model != "openai/gpt-oss-20b" || pet.Saved.NvidiaModel != "openai/gpt-oss-20b") throw new Exception("Picking an NVIDIA model didn't stick.");
                checks.Add("PASS brain tiers: a key in no known format and a rejected key are explained and dropped, a good NVIDIA key goes in Keychain, picks its best model and never lands in cross.json");
                pet.ForgetText();
                if (pet.Saved.Tier != 0 || pet.Brain.Remote != null || Keys.Read("nvidia") != null || pet.Saved.NvidiaModel != null || pet.CloudModels != null) throw new Exception("Forget didn't drop the NVIDIA key and go back to NORMAL.");
                pet.ShowBrains(pet.OllamaModels);
                if (pet.BrainChoices != 2) throw new Exception("Back on NORMAL, the Brain menu didn't list Ollama models again.");
                checks.Add("PASS forget: Forget removes the key from Keychain and she goes back to her local brain");
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
                if (!frame.TiersFit) return false;
                Snap(frame.Content as Control, "mainframe-nvidia.png");
                Brain.Ollama = "http://127.0.0.1:1";
                frame.PressConnect();
                if (pet.Saved.Tier != 0 || Keys.Read("nvidia") != null || !frame.TextStatus.StartsWith("tier 0 |", StringComparison.Ordinal) || !frame.TextStatus.EndsWith("no key row", StringComparison.Ordinal)) throw new Exception("Forget in the Mainframe didn't go back to NORMAL: " + frame.TextStatus);
                return true;
            }
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
                    pet.Talk.Dismiss();
                    pet.Hear("still there?");
                    pet.HideAway();
                    step = 40;
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
                if (pet.Talk.Reply != CapriDesktop.FakeBrain.Line) return false;
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
                pet.Talk.Dismiss();
                pet.Hear("still there?");
                pet.HideAway();
                return true;
            }
            if (step == 41)
            {
                if (pet.IsVisible) return false;
                if (!pet.Talk.Quiet) throw new Exception("A reply that came in after Hide her still showed a bubble: " + pet.Talk.Reply);
                arrivalsBefore = pet.Arrivals;
                pet.PopIn();
                string hello = pet.Talk.Reply;
                if (!pet.IsVisible || pet.Away || !pet.Arriving || pet.Arrivals != arrivalsBefore + 1 || Array.IndexOf(Pet.PopLines, hello) < 0 || pet.PopOut < 0) throw new Exception("She didn't pop in from hiding with a hello: " + hello + " | away " + pet.Away + " | out " + pet.PopOut);
                pet.Talk.Dismiss();
                pet.CheckPopIn(pet.PopOut + 0.1);
                if (!pet.Away || !pet.Leaving) throw new Exception("After popping in from hiding she didn't hide again.");
                return true;
            }
            if (step == 42)
            {
                if (!poppedAgain)
                {
                    if (pet.IsVisible) return false;
                    checks.Add("PASS pop-ins: with Check in on me on she glitches in to say hi even when hidden, then hides again");
                    pet.PopIn();
                    pet.Talk.Dismiss();
                    poppedAgain = true;
                    return false;
                }
                if (pet.Arriving || pet.Falling || pet.Seat == null) return false;
                pet.Tap();
                pet.CheckPopIn(pet.PopOut + 0.1);
                if (pet.Away || pet.Leaving || pet.PopOut >= 0) throw new Exception("A quick click didn't keep her out after she popped in from hiding: away " + pet.Away + " | out " + pet.PopOut);
                checks.Add("PASS a quick click while she's popped in from hiding keeps her out");
                return true;
            }
            if (step == 43)
            {
                if (pet.Arriving || pet.Falling) return false;
                pet.CheckNudge(pet.Now);
                noticedBefore = pet.Noticed;
                // Stay put: she says hi from where she is.
                PixelPoint stayed = pet.Hip;
                pet.PopIn();
                string hi = pet.Talk.Reply;
                if (pet.Leaving || pet.Away || pet.Hip != stayed || Array.IndexOf(Pet.PopLines, hi) < 0) throw new Exception("With Stay put she didn't say hi from where she is: " + hi + " | leaving " + pet.Leaving);
                pet.Talk.Dismiss();
                checks.Add("PASS pop-ins with Stay put: she says hi from where she is instead of moving");
                pet.SetWander(1);
                pet.PopIn();
                pet.SetWander(0);
                if (!pet.Leaving || pet.Away) throw new Exception("She didn't glitch out to pop in somewhere else.");
                return true;
            }
            if (step == 44 && attention == 1)
            {
                if (pet.Talk.Reply == null) return false;
                attentionSulk = pet.Talk.Reply;
                attentionSulked = Array.IndexOf(Pet.SnubbedLines, attentionSulk) >= 0 && pet.NudgeOut < 0 && pet.NudgeWait == Pet.NudgeGap * 2;
                pet.Talk.Dismiss();
                pet.Idle = delegate { return 30; };
                pet.Nudge(pet.Now, null);
                pet.Talk.Dismiss();
                pet.CheckNudge(pet.NudgeOut + 0.1);
                emptyAt = now;
                attention = 2;
                return false;
            }
            if (step == 44 && attention == 2)
            {
                if (now - emptyAt < 0.5) return false;
                bool alone = pet.Talk.Quiet && pet.NudgeOut < 0 && pet.NudgeWait == Pet.NudgeGap * 4;
                pet.Idle = delegate { return 0; };
                pet.Nudge(pet.Now, null);
                pet.Talk.Dismiss();
                pet.Grab();
                pet.CheckNudge(pet.Now);
                string glad = pet.Talk.Reply;
                pet.Drop();
                bool welcomed = Array.IndexOf(Pet.NoticedLines, glad) >= 0 && pet.NudgeOut < 0 && pet.NudgeWait == Pet.NudgeGap;
                pet.ForgetNudges();
                if (!attentionAsked || !attentionSulked || !alone || !welcomed) throw new Exception("Asking for attention went wrong: " + attentionAsk + " | " + attentionSulk + " | alone " + alone + " | " + glad + " | wait " + pet.NudgeWait);
                checks.Add("PASS attention: she asks for attention, sulks and waits twice as long when ignored, only sulks out loud when someone's at the computer, and picking her up gets a happy line and the usual 2 hours");
                peekWas = Eyes.Peek;
                Eyes.Peek = delegate { return FakeScreen(new SkiaSharp.SKColor(200, 30, 40)); };
                pet.Seeing = delegate(byte[] shot, string question)
                {
                    lock (glanceAsks) glanceAsks.Add(question);
                    return GlanceSaid;
                };
                pet.SetWatch(true);
                if (!pet.WatchShown || pet.GapShown != "2 min" || pet.GlanceGap != 120) throw new Exception("Watch my screen didn't turn on at 2 min: " + pet.WatchShown + " | " + pet.GapShown);
                pet.Talk.Dismiss();
                pet.Glance(pet.Now);
                return true;
            }
            if (step == 44)
            {
                if (pet.Leaving) return false;
                string hello = pet.Talk.Reply;
                // Read before Check in on me is turned off and on, which starts her wait again.
                double noticedAfter = pet.Noticed;
                pet.SetCheckIns(false);
                pet.Talk.Dismiss();
                pet.CheckPopIn(pet.Now + 60);
                pet.SetCheckIns(true);
                if (!pet.IsVisible || pet.Away || pet.PopOut >= 0 || Array.IndexOf(Pet.PopLines, hello) < 0) throw new Exception("Popping in while she was out went wrong: " + hello + " | away " + pet.Away + " | out " + pet.PopOut);
                if (noticedBefore < 0 || noticedAfter != noticedBefore) throw new Exception("A pop-in restarted her 2-hour wait to ask for attention: " + noticedBefore + " then " + noticedAfter);
                checks.Add("PASS pop-ins while she's out: she glitches over to a new spot at the top of the screen, says hi and stays out, and her 2-hour wait to ask for attention keeps counting");
                // Someone's at the computer, so her sulk is said; it comes once the idle time is read.
                pet.Idle = delegate { return 0; };
                pet.ForgetNudges();
                pet.Talk.Dismiss();
                pet.Nudge(pet.Now, null);
                attentionAsk = pet.Talk.Reply;
                attentionAsked = Array.IndexOf(Pet.NudgeLines, attentionAsk) >= 0 && pet.NudgeOut > pet.Now;
                pet.Talk.Dismiss();
                pet.CheckNudge(pet.NudgeOut + 0.1);
                attention = 1;
                return false;
            }
            if (step == 45)
            {
                if (pet.Glancing) return false;
                string glanceLine = pet.Talk.Reply;
                int asks;
                lock (glanceAsks) asks = glanceAsks.Count;
                if (glanceLine != Glances.Reply(GlanceSaid) || asks != 1 || glanceAsks[0] != Glances.Line) throw new Exception("Her first glance went wrong: " + glanceLine + " | " + asks);
                pet.Talk.Dismiss();
                pet.Glance(pet.Now);
                return true;
            }
            if (step == 46)
            {
                if (pet.Glancing) return false;
                int asks;
                lock (glanceAsks) asks = glanceAsks.Count;
                if (asks != 1 || pet.Talk.Reply != null) throw new Exception("She looked again at a screen that hadn't changed: " + asks);
                Eyes.Peek = delegate { return FakeScreen(new SkiaSharp.SKColor(20, 60, 220)); };
                pet.Glance(pet.Now);
                return true;
            }
            if (step == 47)
            {
                if (pet.Glancing) return false;
                int asks;
                lock (glanceAsks) asks = glanceAsks.Count;
                bool repeatDropped = asks == 2 && pet.Talk.Reply == null && glanceAsks[1].IndexOf(Glances.Reply(GlanceSaid), StringComparison.Ordinal) >= 0;
                Eyes.Peek = delegate { return null; };
                pet.Glance(pet.Now);
                if (!repeatDropped) throw new Exception("A changed screen with the same line wasn't dropped as a repeat: " + asks + " | " + pet.Talk.Reply);
                return true;
            }
            if (step == 48 && hushGlance)
            {
                if (pet.Glancing) return false;
                bool hushedQuiet = pet.Talk.Quiet;
                pet.ToggleShush();
                pet.Talk.Dismiss();
                Eyes.Peek = peekWas;
                pet.Seeing = null;
                pet.SetWatch(false);
                if (!hushedQuiet || pet.Shushed || pet.WatchShown) throw new Exception("A glance that came back after Quiet went on was said: " + pet.Talk.Reply);
                checks.Add("PASS quiet glance: a glance still out when Quiet goes on is dropped, like Windows");
                Finish(null);
                return true;
            }
            if (step == 48)
            {
                if (pet.Glancing) return false;
                string told = pet.Talk.Reply;
                List<string[]> gnome = Eyes.QuietTools(false, "wayland", "ubuntu:GNOME"), x11 = Eyes.QuietTools(false, "x11", "XFCE"), kde = Eyes.QuietTools(false, "wayland", "KDE");
                bool quietRight = gnome.Count == 1 && gnome[0][0] == "grim" && x11.Count == 4 && x11[0][0] == "import" && Array.IndexOf(x11[0], "-silent") == 1 && kde[0][0] == "spectacle" && Eyes.QuietTools(true, null, null)[0][0] == "screencapture";
                if (told != Eyes.Unwatchable || !quietRight) throw new Exception("Watching without a quiet screenshot tool went wrong: " + told + " | " + quietRight);
                checks.Add("PASS watch my screen: she glances, comments on what she sees, skips a screen that hasn't changed and a line she already said, and says once when this desktop can't take quiet screenshots");
                // A new screen with a new line, but Quiet goes on while she's looking.
                pet.Talk.Dismiss();
                Eyes.Peek = delegate { return FakeScreen(new SkiaSharp.SKColor(240, 200, 20)); };
                pet.Seeing = delegate(byte[] shot, string question) { return "SAW: a yellow spreadsheet. SAY: Spreadsheets at this hour, choom?"; };
                pet.Glance(pet.Now);
                pet.ToggleShush();
                pet.Talk.Dismiss();
                hushGlance = true;
                return false;
            }
            return false;
        }


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

        // Security on Mac and Linux, like Windows' SECURITY window: each preset ticks its toggles, a flip makes it Custom and is
        // saved, settings from before it get Trusted, and a toggle that's off holds her tool call so she asks first and does it
        // on their yes. Mac and Linux have the first five toggles; her mouse and keyboard jobs and "Hey Capri" are Windows-only.
        private void SecurityChecks()
        {
            Func<string> bits = delegate { StringBuilder all = new StringBuilder(); for (int i = 0; i < Security.Count; i++) all.Append(Security.On(i) ? '1' : '0'); return all.ToString(); };
            Func<int, int, string> menu = delegate(int preset, int mask)
            {
                List<string> want = new List<string> { "Security: " + Security.Presets[preset] };
                for (int i = 0; i <= Security.Custom; i++) want.Add((i == preset ? "*" : "") + Security.Presets[i] + (i == Security.Custom ? " (off)" : ""));
                want.Add("-");
                for (int i = 0; i <= Security.Power; i++) want.Add(((mask & (1 << i)) != 0 ? "*" : "") + Security.Names[i] + " (" + Security.Lines[i] + ")");
                return string.Join(" | ", want);
            };
            string[] presetBits = { "00000000000", "11100000000", "11111110000", "11111111111" };
            List<string> wrong = new List<string>();
            string first = pet.SecurityShown;
            bool startedTrusted = Security.Preset == Security.Trusted && first == menu(Security.Trusted, 0x7F);
            for (int preset = Security.Lockdown; preset <= Security.Blackwall; preset++)
            {
                pet.ClickSecurity(preset, -1);
                string shown = pet.SecurityShown;
                if (bits() != presetBits[preset] || Security.Preset != preset || pet.Saved.Permissions != presetBits[preset].Substring(0, 10) || shown != menu(preset, Security.MaskOf(preset)))
                    wrong.Add(Security.Presets[preset] + " " + bits() + " saved " + pet.Saved.Permissions + " shows " + shown);
            }
            pet.ClickSecurity(Security.Trusted, -1);
            pet.ClickSecurity(-1, Security.Open);
            string customShown = pet.SecurityShown;
            bool custom = Security.Preset == Security.Custom && pet.Saved.Permissions == "0111111000" && customShown == menu(Security.Custom, 0x7E);
            // The smoke test never writes cross.json, so this is what Save would write.
            MemoryStream savedJson = new MemoryStream();
            new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(Settings)).WriteObject(savedJson, pet.Saved);
            bool written = Encoding.UTF8.GetString(savedJson.ToArray()).Contains("\"Permissions\":\"0111111000\"");
            Security.Load(pet.Saved.Permissions);
            bool reloaded = Security.Preset == Security.Custom && !Security.On(Security.Open) && Security.On(Security.Media);
            pet.ClickSecurity(-1, Security.Open);
            bool back = Security.Preset == Security.Trusted && pet.Saved.Permissions == "1111111000" && pet.SecurityShown == menu(Security.Trusted, 0x7F);
            bool windowsOnly = customShown.IndexOf("mouse and keyboard", StringComparison.Ordinal) < 0 && customShown.IndexOf("Hey Capri", StringComparison.Ordinal) < 0 && customShown.IndexOf("passwords", StringComparison.Ordinal) < 0 && customShown.IndexOf("Pay, buy", StringComparison.Ordinal) < 0;
            bool lines = Security.PresetLine(Security.Trusted) == "She does what you ask without asking first." && Security.PresetLine(Security.Blackwall) == "Everything is on: she never asks before doing what you ask." && Security.PresetLine(Security.Guarded) == "She opens apps and sites, plays music and presses shortcuts, and asks first for the rest.";
            Settings older = (Settings)new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(Settings)).ReadObject(new MemoryStream(Encoding.UTF8.GetBytes("{\"Model\":\"qwen3:8b\",\"Watch\":true}")));
            Security.Load(older.Permissions);
            bool olderTrusted = older.Permissions == null && Security.Preset == Security.Trusted;
            Security.Load("not toggles");
            bool unreadable = Security.Preset == Security.Trusted;
            Security.Load(pet.Saved.Permissions);
            if (wrong.Count > 0 || !startedTrusted || !custom || !written || !reloaded || !back || !windowsOnly || !lines || !olderTrusted || !unreadable)
                throw new Exception("Security's menu went wrong: " + string.Join(" / ", wrong) + " / first " + first + " / custom " + customShown + " / " + written + " " + reloaded + " " + back + " " + windowsOnly + " " + lines + " " + olderTrusted + " " + unreadable);
            checks.Add("PASS Security menu: Settings > Security starts on Trusted; Lockdown, Guarded, Trusted and Blackwall tick the toggles under them (the five Mac and Linux have, each with its plain line), a toggle click makes it Custom and is saved for next time, ticking it again is Trusted, and settings from before it, or unreadable ones, get Trusted");

            // A toggle that's off holds the call and she asks first; a yes runs it once, a no leaves it and anything else drops it.
            List<string> did = new List<string>();
            Action<string> runWas = Hands.Run;
            Action<byte> pressWas = Hands.Press;
            Func<List<InstalledApp>> catalogWas = Hands.Catalog;
            Func<List<OpenWindow>> windowsWas = Hands.Windows;
            Func<OpenWindow, string, bool> steerWas = Hands.Steer;
            Func<string, int, bool> powerWas = Hands.Power;
            Func<string, string> insteadWas = Hands.Instead;
            string[] tools = { "open_app", "media", "shortcut", "app_window", "power" };
            ToolArguments[] calls = { new ToolArguments { Name = "spotify" }, new ToolArguments { Action = "volume_up" }, new ToolArguments { Name = "screenshot" }, new ToolArguments { Name = "discord", Action = "switch_to" }, new ToolArguments { Action = "lock" } };
            string[] questions = { "Open spotify? Say yes and I will.", "Turn the volume up? Say yes and I will.", "Take a screenshot? Say yes and I will.", "Switch to discord? Say yes and I will.", "Lock your PC? Say yes and I will." };
            string[] ran = { "run exec:spotify", "key AF", "instead screenshot", "steer switch_to discord", "power lock" };
            List<string> gates = new List<string>();
            string declined, dropped, mixedAsk, mixedRan, mouseFree, lockdownNote, trustedPrompt, openPrompt, textChat;
            try
            {
                Hands.Run = delegate(string id) { did.Add("run " + id); };
                Hands.Press = delegate(byte key) { did.Add("key " + key.ToString("X2")); };
                Hands.Catalog = delegate { return new List<InstalledApp> { new InstalledApp("Spotify", "exec:spotify") }; };
                Hands.Forget();
                OpenWindow discord = new OpenWindow(new IntPtr(9), "#general | Discord", "discord");
                Hands.Windows = delegate { return new List<OpenWindow> { discord }; };
                Hands.Steer = delegate(OpenWindow window, string action) { did.Add("steer " + action + " " + window.Program); return true; };
                Hands.Power = delegate(string action, int seconds) { did.Add("power " + action); return true; };
                Hands.Instead = delegate(string name) { did.Add("instead " + name); return "Opened the screenshot picker."; };
                for (int i = 0; i <= Security.Power; i++)
                {
                    Security.Pick(Security.Trusted);
                    Security.Set(i, false);
                    did.Clear();
                    string held = pet.Brain.Act(tools[i], calls[i]);
                    string early = string.Join(",", did), question = Security.Question;
                    string yes = Hands.Resume("yes");
                    string after = string.Join(",", did);
                    if (!Security.IsHold(held) || early.Length > 0 || question != questions[i] || yes == null || Security.IsHold(yes) || after.IndexOf(ran[i], StringComparison.Ordinal) < 0 || Security.Holding != null)
                        gates.Add(Security.Names[i] + ": " + held + " / ran early " + early + " / asked " + question + " / yes " + yes + " / ran " + after);
                }
                Security.Pick(Security.Trusted);
                Security.Set(Security.Open, false);
                did.Clear();
                pet.Brain.Act("open_app", calls[0]);
                declined = Hands.Resume("nah") + " [" + string.Join(",", did) + "]";
                pet.Brain.Act("open_app", calls[0]);
                dropped = (Hands.Resume("what's the weather like?") ?? "null") + " / " + (Security.Holding ?? "nothing held") + " [" + string.Join(",", did) + "]";
                // Two held and one that ran: what ran, then one question for both, and one yes runs both.
                Security.Pick(Security.Trusted);
                Security.Set(Security.Media, false);
                Security.Set(Security.Power, false);
                did.Clear();
                List<string> results = new List<string> { pet.Brain.Act("open_app", calls[0]), pet.Brain.Act("media", calls[1]), pet.Brain.Act("power", calls[4]) };
                mixedAsk = Security.Reply(results);
                mixedRan = Hands.Resume("yeah") + " [" + string.Join(",", did) + "]";
                // Text chat: her question goes out word for word with no second brain call, and their next message's yes does it.
                Security.Pick(Security.Trusted);
                Security.Set(Security.Media, false);
                did.Clear();
                Brain talker = new Brain();
                talker.Model = "smoke-test-model";
                talker.Act = pet.Brain.Act;
                string talkAsked = talker.Ask("turn the volume up"), talkEarly = string.Join(",", did);
                textChat = talkAsked + " / " + talkEarly + " / " + talker.Ask("yes") + " [" + string.Join(",", did) + "]";
                // Mac and Linux don't have her mouse and keyboard jobs yet, so even Lockdown never holds one, what's playing never
                // asks, and her prompt names only the toggles this computer has.
                Security.Pick(Security.Lockdown);
                ToolArguments job = new ToolArguments { Task = "message Sam hi on Discord" };
                mouseFree = (Security.Gate("use_computer", job) ?? "free") + " / " + Security.Asks("use_computer", job) + " / " + (Security.Gate("media", new ToolArguments { Action = "now_playing" }) ?? "free");
                lockdownNote = Security.Note;
                Security.Pick(Security.Trusted);
                trustedPrompt = Hands.Prompt;
                Security.Set(Security.Open, false);
                openPrompt = Hands.Prompt;
            }
            finally
            {
                Hands.Run = runWas;
                Hands.Press = pressWas;
                Hands.Catalog = catalogWas;
                Hands.Forget();
                Hands.Windows = windowsWas;
                Hands.Steer = steerWas;
                Hands.Power = powerWas;
                Hands.Instead = insteadWas;
                Security.Drop();
                Security.Pick(Security.Trusted);
            }
            if (gates.Count > 0) throw new Exception("A Security toggle didn't ask first or run on a yes: " + string.Join(" // ", gates));
            if (declined != Security.Left + " []" || dropped != "null / nothing held []") throw new Exception("A no or a new message after her Security question went wrong: " + declined + " / " + dropped);
            if (mixedAsk != "Opened Spotify. Turn the volume up and lock your PC? Say yes and I will." || mixedRan.IndexOf("key AF", StringComparison.Ordinal) < 0 || mixedRan.IndexOf("power lock", StringComparison.Ordinal) < 0) throw new Exception("Two held calls weren't one question and one yes: " + mixedAsk + " / " + mixedRan);
            if (!textChat.StartsWith("Turn the volume up? Say yes and I will. /  / Turned the volume up.", StringComparison.Ordinal) || textChat.IndexOf("key AF", StringComparison.Ordinal) < 0) throw new Exception("Text chat didn't ask first or run on a yes: " + textChat);
            if (mouseFree != "free / False / free" || lockdownNote.IndexOf("switch to or close apps and tabs or shut down, restart, sleep, lock or sign out of their PC. Still call the tool as soon as they ask", StringComparison.Ordinal) < 0 || lockdownNote.IndexOf("mouse and keyboard", StringComparison.Ordinal) >= 0
                || trustedPrompt.IndexOf("SECURITY", StringComparison.Ordinal) >= 0 || openPrompt.IndexOf("Call the tool.\nThey've set her SECURITY settings to ask first before you open apps, games or sites. Still call the tool as soon as they ask", StringComparison.Ordinal) < 0)
                throw new Exception("Security asked about something Mac and Linux don't have, or her prompt didn't follow it: " + mouseFree + " / " + lockdownNote);
            checks.Add("PASS Security gates: with each of the five toggles off she holds the call and asks (\"Open spotify? Say yes and I will.\", \"Lock your PC? Say yes and I will.\"), nothing runs until a yes, a yes runs it once through her own hands, a no leaves it alone and a new message drops it; two held calls are one question and one yes; in text chat her question goes out word for word and the next message's yes does it; mouse and keyboard jobs, which Mac and Linux don't have yet, never ask; and her brain is told to still call a tool that's off and let her ask");
        }

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
        internal static int ReadHead(NetworkStream stream, List<byte> read)
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

        internal static void Answer(NetworkStream stream, int status, string type, byte[] reply)
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

        private static string Sha(byte[] data) { return Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant(); }

        private void Serve(NetworkStream stream)
        {
            List<byte> read = new List<byte>();
            byte[] chunk = new byte[4096];
            int end = ReadHead(stream, read);
            if (end < 0) return;
            string head = Encoding.ASCII.GetString(read.ToArray(), 0, end);
            string[] lines = head.Split(new[] { "\r\n" }, StringSplitOptions.None);
            string[] first = lines[0].Split(' ');
            string key = "";
            int length = 0;
            foreach (string line in lines)
            {
                int colon = line.IndexOf(':');
                if (colon < 0) continue;
                string name = line.Substring(0, colon).Trim().ToLowerInvariant();
                string value = line.Substring(colon + 1).Trim();
                if (name == "xi-api-key") key = value;
                if (name == "content-length") length = int.Parse(value);
            }
            while (read.Count - end < length)
            {
                int got = stream.Read(chunk, 0, chunk.Length);
                if (got <= 0) break;
                for (int i = 0; i < got; i++) read.Add(chunk[i]);
            }
            string body = Encoding.UTF8.GetString(read.ToArray(), end, read.Count - end);
            lock (voiceAsked) voiceAsked.Add(first[0] + " " + first[1] + " " + key + " " + body);
            int status = 200;
            string type = "application/json";
            byte[] reply;
            if (key == "bad-key") { status = 401; reply = Encoding.UTF8.GetBytes("{\"detail\":{\"status\":\"invalid_api_key\",\"message\":\"Invalid API key\"}}"); }
            else if (first[1].StartsWith("/v2/voices", StringComparison.Ordinal)) reply = Encoding.UTF8.GetBytes("{\"voices\":[{\"voice_id\":\"p1\",\"name\":\"Adam\",\"category\":\"premade\"},{\"voice_id\":\"v1\",\"name\":\"Capri Test\",\"category\":\"cloned\"}],\"has_more\":false}");
            else if (first[1].StartsWith("/v1/models", StringComparison.Ordinal)) reply = Encoding.UTF8.GetBytes("[]");
            else if (first[1].StartsWith("/v1/text-to-speech/", StringComparison.Ordinal))
            {
                short[] tone = new short[24000];
                for (int i = 0; i < tone.Length; i++) tone[i] = (short)(Math.Sin(i * 0.05) * 3000);
                reply = Voice.Wave(tone, 24000);
                type = "audio/wav";
            }
            else { status = 404; reply = Encoding.UTF8.GetBytes("{}"); }
            Answer(stream, status, type, reply);
        }

        // A nap they ask for, in stages so she can finish waking between them: she waits out her line, then sleeps through
        // the mouse and keyboard until a click, her menu's Wake up, "wake up" or talking to her.
        private bool Napped()
        {
            if (napStage >= 4) return true;
            if (!pet.Upright) return false;
            if (napStage == 0)
            {
                string line = Hands.Do("nap", new ToolArguments { Action = "sleep" });
                pet.Talk.Answer("(nap)", "Night night.");
                pet.CheckNap();
                bool waited = pet.Upright && pet.NapChoice == "Wake up";
                pet.Talk.Dismiss();
                pet.CheckNap();
                bool dozed = pet.Asleep && pet.Napping && pet.Doing == "Asleep";
                pet.Doze(0.5);
                bool held = pet.Asleep && pet.Napping;
                pet.Tap();
                bool tapped = pet.Waking && !pet.Napping;
                if (line != "Going to sleep. A click on me wakes me up." || !waited || !dozed || !held || !tapped) throw new Exception("Her nap went wrong: waited " + waited + ", dozed " + dozed + ", held " + held + ", tapped " + tapped + ", said " + line);
                napStage = 1;
                return false;
            }
            if (napStage == 1)
            {
                pet.Nap(true);
                pet.CheckNap();
                bool dozed = pet.Asleep && pet.NapChoice == "Wake up";
                string woke = Hands.Do("nap", new ToolArguments { Action = "wake" });
                if (!dozed || woke != "Woke up." || !pet.Waking || pet.NapChoice != "Go to sleep") throw new Exception("Her menu's nap or \"wake up\" went wrong: dozed " + dozed + ", said " + woke);
                napStage = 2;
                return false;
            }
            if (napStage == 2)
            {
                string already = Hands.Do("nap", new ToolArguments { Action = "wake" });
                Hands.Do("nap", new ToolArguments { Action = "sleep" });
                pet.CheckNap();
                if (already != "Already awake." || !pet.Asleep) throw new Exception("Waking an awake Capri or her next nap went wrong: " + already);
                pet.Talk.Answer("(nap)", "Hm?");
                napStage = 3;
                return false;
            }
            pet.CheckNap();
            bool talkWoke = !pet.Asleep && !pet.Napping;
            pet.Talk.Dismiss();
            if (!talkWoke) throw new Exception("Talking to her didn't wake her from a nap.");
            checks.Add("PASS nap on demand: \"go to sleep\" waits for her line, then she sleeps through the mouse and keyboard, Asleep on Discord, until a click, her menu's Wake up, \"wake up\" or talking to her");
            napStage = 4;
            return true;
        }

        private string Self(string tool, string what, string which = null)
        {
            string line = Hands.Do(tool, tool == "animate" ? new ToolArguments { Name = what } : new ToolArguments { Action = what, Screen = which });
            moveLines.Add(line);
            return line;
        }

        // Her own moves and animations, in stages so her glitches can play out: "teleport here" puts her hip on the mouse, she
        // sits, stands, hangs and lets go where she is, goes to another screen when there is one, a hide waits for her line, and
        // her animations play by name.
        private bool Moved()
        {
            if (moveStage >= 5) return true;
            if (moveStage == 0)
            {
                if (!pet.Upright) return false;
                PixelRect work = Work();
                moveTo = new PixelPoint(work.X + work.Width / 2, work.Y + work.Height / 2);
                pet.MouseAt = delegate { return moveTo; };
                if (Self("move_yourself", "come_here") != "Teleporting to your mouse." || !pet.Leaving) throw new Exception("\"Teleport here\" didn't glitch her out.");
                moveStage = 1;
                return false;
            }
            if (moveStage == 1)
            {
                if (pet.Leaving) return false;
                if (pet.Hip != moveTo || !pet.Arriving) throw new Exception("\"Teleport here\" didn't glitch her in on the mouse: " + pet.Hip + " for " + moveTo);
                Self("move_yourself", "sit");
                bool sat = pet.Seat != null && pet.Seat.Free && pet.Hip == moveTo;
                Self("move_yourself", "sit");
                Self("move_yourself", "stand_up");
                bool stood = pet.Seat == null && pet.Playing == "glitch";
                Self("move_yourself", "hang");
                bool hung = pet.Hanging && pet.Playing == "dangle" && pet.HangChoice == "Let go";
                Self("move_yourself", "stand_up");
                bool letGo = !pet.Hanging;
                Self("move_yourself", "ride_cursor");
                Self("move_yourself", "move_mainframe", "other");
                Self("animate", "glitch");
                bool glitched = pet.Playing == "glitch";
                Self("animate", "grin");
                bool grinned = pet.Playing == "grin";
                Self("animate", "blink");
                bool blinked = pet.Playing == "idle" && pet.Blinking;
                Self("animate", "dance");
                bool danced = pet.Dancing && pet.Doing != "Dancing to music";
                Self("animate", "stop_dancing");
                Self("animate", "stop_dancing");
                bool stopped = !pet.Dancing;
                Self("animate", "idle");
                if (!sat || !stood || !hung || !letGo || !glitched || !grinned || !blinked || !danced || !stopped || pet.Playing != "idle")
                    throw new Exception("Her moves or animations went wrong: sat " + sat + ", stood " + stood + ", hung " + hung + ", let go " + letGo + ", glitched " + glitched + ", grinned " + grinned + ", blinked " + blinked + ", danced " + danced + ", stopped " + stopped + " / " + string.Join(" / ", moveLines));
                moveFrom = pet.ScreenHere();
                string other = Self("move_yourself", "other_screen");
                if (pet.Screens.All.Count < 2)
                {
                    if (other != "There's no other screen to go to.") throw new Exception("With one screen, going to another said: " + other);
                    moveStage = 3;
                    return false;
                }
                if (!other.StartsWith("Moving to ", StringComparison.Ordinal) || !pet.Leaving) throw new Exception("Going to another screen didn't glitch her out: " + other);
                moveStage = 2;
                return false;
            }
            if (moveStage == 2)
            {
                if (pet.Leaving) return false;
                if (pet.ScreenHere().Bounds == moveFrom.Bounds) throw new Exception("She didn't land on another screen.");
                moveStage = 3;
                return false;
            }
            if (moveStage == 3)
            {
                Self("move_yourself", "hide");
                pet.Talk.Answer("(hide)", "Hiding.");
                pet.CheckHide();
                bool waited = !pet.Away;
                pet.Talk.Dismiss();
                pet.CheckHide();
                bool hid = pet.Away;
                Self("move_yourself", "show");
                bool back = !pet.Away;
                Self("move_yourself", "show");
                if (!waited || !hid || !back) throw new Exception("Her hide went wrong: waited " + waited + ", hid " + hid + ", back " + back);
                moveStage = 4;
                return false;
            }
            if (pet.Leaving || pet.Arriving || pet.Falling) return false;
            pet.MouseAt = null;
            string said = string.Join(" / ", moveLines);
            string wanted = "Teleporting to your mouse. / Sitting right here. / Already sitting. / Standing up. / Hanging right here. / Let go. / Riding your cursor is only on Windows so far. / I can't move my Mainframe by myself here yet. Drag its window where you want it."
                + " / Glitched. / Grinning. / Blinked. / Dancing. Tell me to stop when you've had enough. / Stopped dancing. / I wasn't dancing. / Back to my idle. / " + moveLines[15] + " / Hiding. Pick Show her in my tray menu to bring me back. / Back. / I'm right here.";
            if (said != wanted) throw new Exception("Her moves said the wrong things: " + said);
            checks.Add("PASS she moves herself: \"teleport here\" glitches her hip onto the mouse, she sits, stands, hangs and lets go where she is, goes to another screen " + (pet.Screens.All.Count > 1 ? "and lands there" : "only when there is one (one here)") + ", a hide waits for her line and \"show yourself\" brings her back, riding the cursor and the Mainframe say they're Windows-only so far, and glitch, grin, blink, idle and a dance until \"stop dancing\" play by name");
            moveStage = 5;
            return true;
        }

        private Process Shelf(PixelRect rect)
        {
            ProcessStartInfo start = new ProcessStartInfo(Environment.ProcessPath, "--shelf " + rect.X + " " + rect.Y + " " + (int)Math.Round(rect.Width / pet.DesktopScaling) + " " + (int)Math.Round(rect.Height / pet.DesktopScaling));
            start.UseShellExecute = false;
            start.RedirectStandardInput = true;
            Process process = Process.Start(start);
            if (process == null) throw new Exception("The test shelf window didn't start.");
            shelves.Add(process);
            return process;
        }

        private static void Send(Process process, string line)
        {
            if (process != null && !process.HasExited) process.StandardInput.WriteLine(line);
        }

        private string Seen()
        {
            List<string> parts = new List<string>();
            foreach (Spot spot in pet.Windows()) parts.Add(spot.Window + " " + spot.Bounds + (spot.Seat ? " seat" : ""));
            return "window list " + pet.Desk.Name + (pet.DeskTrouble == null ? "" : " after " + pet.DeskTrouble) + ", scale " + pet.Desk.Scale.ToString("0.###") + ", shelf asked " + shelfAsked + " " + (shelf == null ? "not started" : shelf.HasExited ? "exited " + shelf.ExitCode : "running") + ", saw " + (parts.Count == 0 ? "no windows" : string.Join("; ", parts)) + ", " + pet.Desk.Debug();
        }

        private Spot Near(PixelRect asked, bool seat)
        {
            Spot best = null;
            int bestFar = int.MaxValue;
            foreach (Spot spot in pet.Windows())
            {
                if (seat && !spot.Seat) continue;
                int far = Math.Abs(spot.Bounds.X - asked.X) + Math.Abs(spot.Bounds.Y - asked.Y) + Math.Abs(spot.Bounds.Width - asked.Width);
                if (far < Px(120) && far < bestFar) { best = spot; bestFar = far; }
            }
            return best;
        }

        private void Snap(Control control, string name)
        {
            if (control == null) return;
            PixelSize size = new PixelSize(Math.Max(1, (int)Math.Ceiling(control.Bounds.Width)), Math.Max(1, (int)Math.Ceiling(control.Bounds.Height)));
            using (RenderTargetBitmap shot = new RenderTargetBitmap(size, new Vector(96, 96)))
            {
                shot.Render(control);
                shot.Save(Path.Combine(folder, name));
            }
        }

        private void Scene(string name, Window[] windows, PixelRect[] boxes)
        {
            int left = int.MaxValue;
            int top = int.MaxValue;
            int right = int.MinValue;
            int bottom = int.MinValue;
            double scale = pet.DesktopScaling;
            foreach (Window window in windows)
            {
                left = Math.Min(left, window.Position.X);
                top = Math.Min(top, window.Position.Y);
                right = Math.Max(right, window.Position.X + (int)Math.Ceiling(window.Bounds.Width * scale));
                bottom = Math.Max(bottom, window.Position.Y + (int)Math.Ceiling(window.Bounds.Height * scale));
            }
            foreach (PixelRect box in boxes)
            {
                left = Math.Min(left, box.X);
                top = Math.Min(top, box.Y);
                right = Math.Max(right, box.Right);
                bottom = Math.Max(bottom, box.Bottom);
            }
            double pad = 40;
            PixelSize size = new PixelSize((int)Math.Ceiling((right - left) / scale + pad * 2), (int)Math.Ceiling((bottom - top) / scale + pad * 2));
            using (RenderTargetBitmap scene = new RenderTargetBitmap(size, new Vector(96, 96)))
            {
                using (DrawingContext context = scene.CreateDrawingContext())
                {
                    context.FillRectangle(new SolidColorBrush(Color.FromRgb(238, 240, 243)), new Rect(0, 0, size.Width, size.Height));
                    foreach (PixelRect box in boxes)
                    {
                        Rect place = new Rect(pad + (box.X - left) / scale, pad + (box.Y - top) / scale, box.Width / scale, box.Height / scale);
                        context.FillRectangle(new SolidColorBrush(Color.FromRgb(200, 204, 210)), place);
                        context.FillRectangle(new SolidColorBrush(Color.FromRgb(120, 126, 136)), new Rect(place.X, place.Y, place.Width, Math.Min(32, place.Height)));
                    }
                    foreach (Window window in windows)
                    {
                        Control face = window.Content as Control;
                        if (face == null || !window.IsVisible) continue;
                        PixelSize part = new PixelSize(Math.Max(1, (int)Math.Ceiling(face.Bounds.Width)), Math.Max(1, (int)Math.Ceiling(face.Bounds.Height)));
                        RenderTargetBitmap shot = new RenderTargetBitmap(part, new Vector(96, 96));
                        shot.Render(face);
                        context.DrawImage(shot, new Rect(pad + (window.Position.X - left) / scale, pad + (window.Position.Y - top) / scale, part.Width, part.Height));
                    }
                }
                scene.Save(Path.Combine(folder, name));
            }
        }

        private void Finish(string failure)
        {
            if (finished) return;
            finished = true;
            foreach (Process process in shelves)
            {
                try
                {
                    if (process.HasExited) continue;
                    process.StandardInput.WriteLine("quit");
                    if (!process.WaitForExit(3000)) process.Kill();
                }
                catch (Exception) { }
            }
            List<string> lines = new List<string>(checks);
            lines.Insert(0, "Capri cross-platform smoke test on " + System.Runtime.InteropServices.RuntimeInformation.OSDescription);
            if (failure != null) lines.Add("FAIL " + failure);
            File.WriteAllLines(Path.Combine(folder, "smoke-test.txt"), lines);
            pet.Finish(failure == null ? 0 : 1);
        }
    }

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
}
