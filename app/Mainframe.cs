using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed class Mainframe : Form, IMessageFilter
    {
        private static readonly Color Field = Color.FromArgb(17, 21, 28);
        private static readonly Color Hover = Color.FromArgb(26, 32, 42);
        private static readonly Color Pressed = Color.FromArgb(34, 42, 55);
        private static readonly Color Edge = Color.FromArgb(52, 60, 74);
        private static readonly Color Track = Color.FromArgb(30, 36, 46);
        private const int Wide = 380;
        private const int Tall = 642;
        private static readonly int[] Gaps = { 10, 15, 30, 60, 90, 120, 180, 300, 600 };
        private static readonly string[] GapNames = { "10 s", "15 s", "30 s", "1 min", "1.5 min", "2 min", "3 min", "5 min", "10 min" };
        private const int SetupHeight = 118;
        private const int PageTall = 804;
        // Settings opens a second column to the right, so every setting shows at once.
        private const int Column = 380;
        private const int Far = 30000;
        private static readonly int[][] PageBars = { new[] { 0, 58, 206 }, new[] { 0, 220, 484 }, new[] { 0, 494, 650 }, new[] { Column, 58, 146 }, new[] { Column, 162, 250 }, new[] { Column, 266, 460 }, new[] { Column, 482, 652 }, new[] { 0, 674, 768 } };
        private static readonly string[] PerchNames = Array.ConvertAll(Companion.PerchRates, rate => rate + " fps");
        // Her voice's pitch in semitones, and her speed.
        private static readonly string[] PitchNames = { "-6", "-5", "-4", "-3", "-2", "-1", "Normal", "+1", "+2", "+3", "+4", "+5", "+6" };
        private static readonly int[] Paces = { 70, 80, 90, 100, 110, 120, 130, 140, 150 };
        private static readonly string[] PaceNames = { "0.7x", "0.8x", "0.9x", "Normal", "1.1x", "1.2x", "1.3x", "1.4x", "1.5x" };
        // Her robot and glitch, with small steps at the subtle end.
        private static readonly int[] Amounts = { 0, 5, 10, 15, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
        private static readonly string[] AmountNames = { "Off", "5%", "10%", "15%", "20%", "30%", "40%", "50%", "60%", "70%", "80%", "90%", "100%" };
        private static readonly int[] Volumes = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 125, 150 };
        private static readonly string[] VolumeNames = { "Mute", "10%", "20%", "30%", "40%", "50%", "60%", "70%", "80%", "90%", "100%", "125%", "150%" };
        private static readonly int[] SizeSteps = { 140, 170, 200, 240, 280, 320, 380, 440, 500, 560 };
        private static readonly string[] SizeNames = { "Tiny", "Small", "Small+", "Medium", "Medium+", "Large", "Large+", "Huge", "Huge+", "Giant" };
        private static readonly int[] Sleeps = { 0, 60, 120, 300, 600, 1800 };
        private static readonly string[] SleepNames = { "Never", "1 min", "2 min", "5 min", "10 min", "30 min" };
        private static readonly string[] PopNames = { "Rarely", "Sometimes", "Often" };
        private static readonly string[] BubbleNames = { "Quick", "Normal", "Long" };
        private static readonly string[] SoundNames = { "Off", "10%", "20%", "30%", "40%", "50%", "60%", "70%", "80%", "90%", "100%" };
        private static readonly string[] WanderNames = { "Off", "Rarely", "Sometimes", "Often", "Restless" };
        private static readonly string[] Lengths = { "Short", "Medium", "Long" };
        private static readonly int[] Recalls = { 8, 16, 32 };
        private const int VoiceHeight = 300;
        // While her free voice or ears download, a row with the brain setup's dotted bar opens here and pushes the rest down.
        private const int InstallAt = 443;
        private const int InstallRow = 40;
        // CLOUD VOICE's row: its service, then ElevenLabs' key or the other service's panel. Unless ElevenLabs is picked with a
        // key saved, one row under it folds away and the rest moves up; under the other rows, this one folds away instead.
        private const int KeyAt = 310;
        private const int KeyFold = 36;
        private float voiceFilled = -1;
        private readonly Label installNote;
        private readonly Companion capri;
        private readonly Label brainName;
        private readonly Label brainNote;
        private readonly Key models;
        private readonly ContextMenuStrip modelMenu;
        private readonly Key refresh;
        private readonly Label[] steps = new Label[3];
        private readonly string[] stepText = new string[3];
        private readonly Key setup;
        private readonly Label hint;
        private readonly Label tierNote;
        private readonly Rung[] rungs = new Rung[3];
        private int tier;
        private readonly Entry nickname;
        private readonly Label personalityNote;
        private readonly Key showHide;
        private readonly Key perch;
        private readonly Slider sizeSlider;
        private readonly Label sizeName;
        private readonly Toggle checkIns;
        private readonly Toggle onTop;
        private readonly Toggle startup;
        private readonly Toggle discord;
        private readonly Toggle cursor;
        private readonly Toggle bypass;
        private readonly Key talkKeys;
        private readonly Key muteKeys;
        private readonly Key typeKeys;
        private readonly Key hideKeys;
        private readonly Key quietKeys;
        private readonly Key confirmKeys;
        private readonly Key cancelKeys;
        // The keys pressed so far while one of her keys is being picked: -1 until a real key comes, then its modifiers.
        private int pendingMods = -1;
        private int pendingVk;
        private int heldMods;
        private readonly Toggle watch;
        private readonly Toggle caughtUp;
        private readonly Toggle stayOnModel;
        private readonly Toggle remembering;
        private readonly Label voiceHead;
        // OFF, PREBUILT LOCAL (her own voices), CUSTOM LOCAL (a voice model they set up on their PC) and CLOUD VOICE.
        private readonly Rung[] voiceRungs = new Rung[4];
        private int voiceRow = 1;
        private bool off;
        private bool eleven;
        private bool wantEleven;
        // Fish Audio or OpenAI, picked under CLOUD VOICE before it's set up: the row and its menu stay up until another row is picked.
        private string wantCloud;
        private const string Eleven = Companion.ElevenLabs;
        // Fish Audio comes first: its free tier needs no credits.
        private static readonly string[] Clouds = { "Fish Audio", Eleven, "OpenAI" };
        private string cloudName = Clouds[0];
        private readonly Key cloudPick;
        private readonly ContextMenuStrip cloudMenu;
        private readonly Key cloudVoice;
        private readonly Key customVoice;
        private readonly Key setUpModel;
        private int arrangedFold = -1;
        private readonly Key freeVoices;
        private readonly ContextMenuStrip freeMenu;
        private readonly Key freeSetup;
        private readonly Key herVoice;
        private bool freeReady;
        private bool yoursShown;
        private ServerSheet sheet;
        private readonly Slider gap;
        private readonly Label gapName;
        private readonly Entry keyEntry;
        private readonly Key connect;
        private readonly Entry voiceEntry;
        private readonly ContextMenuStrip voiceMenu;
        private readonly Key speechModels;
        private readonly ContextMenuStrip speechModelMenu;
        private readonly Label voiceNote;
        private bool speechFix;
        private readonly Label keysNote;
        private readonly Label blackwallNote;
        private readonly Label updateNote;
        private readonly Key keyHelp;
        private readonly Label shortcut;
        private bool shortcutReady;
        private bool muteReady = true;
        private int capturing;
        private string talkName = "Ctrl + Alt + V";
        private string muteName = "Ctrl + Alt + N";
        private string typeName = "Ctrl + Alt + T";
        private string hideName = "Ctrl + Alt + H";
        private string quietName = "Ctrl + Alt + P";
        private bool typeReady = true;
        private bool hideReady = true;
        private bool quietReady = true;
        private readonly Label muteShortcut;
        private readonly Key mics;
        private readonly ContextMenuStrip micMenu;
        private bool keySaved;
        private readonly Entry textEntry;
        private readonly Key textConnect;
        private readonly Key freeKey;
        private bool textSaved;
        private readonly List<KeyValuePair<Control, int>> lower = new List<KeyValuePair<Control, int>>();
        private string[] lastInstalled;
        private float filled;
        private bool expanded = true;
        private bool loaded;
        private bool syncing;
        internal bool Quitting;
        private readonly Key more;
        private readonly Key minimize;
        private readonly Key close;
        private readonly Label build;
        private readonly List<KeyValuePair<Control, int>> mainLeft = new List<KeyValuePair<Control, int>>();
        private readonly List<KeyValuePair<Control, int>> pageLeft = new List<KeyValuePair<Control, int>>();
        private readonly Dictionary<Control, bool> stops = new Dictionary<Control, bool>();
        private bool paged;
        // On a screen too short for Settings, the whole page, header too, scrolls a row per turn of the wheel.
        private const int ScrollRow = 36;
        private readonly List<Control> scrolled = new List<Control>();
        private readonly Dictionary<Control, int> tops = new Dictionary<Control, int>();
        private int pageView = PageTall;
        private int pageScroll;
        private int wheel;
        private readonly Rim topRim = new Rim();
        private readonly Rim bottomRim = new Rim();
        // Smoke tests pretend the screen's working area is this many px tall, before scaling; 0 uses the real one.
        internal int ShortScreen;
        private readonly Key languages;
        private readonly ContextMenuStrip languageMenu;
        private readonly Slider volume;
        private readonly Label volumeName;
        private readonly Slider pitchSlider;
        private readonly Label pitchName;
        private readonly Slider paceSlider;
        private readonly Label paceName;
        private readonly Slider robotSlider;
        private readonly Label robotName;
        private readonly Slider glitchSlider;
        private readonly Label glitchName;
        private readonly Slider sounds;
        private readonly Slider sleepAfter;
        private readonly Slider bubble;
        private readonly Label bubbleName;
        private readonly Toggle intro;
        private readonly Toggle typing;
        private readonly Toggle hdGames;
        private readonly Toggle miniPlayer;
        private readonly Label sleepName;
        private readonly Label soundsName;
        private readonly Slider wander;
        private readonly Label wanderName;
        private readonly Slider pops;
        private readonly Label popsName;
        private readonly Toggle dance;
        private readonly Slider replies;
        private readonly Label repliesName;
        private readonly Slider recall;
        private readonly Label recallName;
        private readonly Entry custom;
        private readonly Label customNote;
        private readonly Slider perchFps;
        private readonly Label perchFpsName;
        private readonly Label perchNote;

        internal Mainframe(Companion owner, Icon icon, string version)
        {
            capri = owner;
            Text = "Capri Mainframe";
            Icon = icon;
            ShowInTaskbar = true;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            Font = Look.Mono(9f, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Look.Panel;
            ForeColor = Look.Ink;
            DoubleBuffered = true;
            ClientSize = new Size(Wide, Tall + SetupHeight + KeyRow);
            Caption("MAINFRAME", 26, 20, Look.Mono(13f, FontStyle.Bold, GraphicsUnit.Point), Look.Ink);
            build = Caption("v" + version, 200, 20, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim);
            build.AutoSize = false;
            build.TextAlign = ContentAlignment.MiddleRight;
            build.Bounds = new Rectangle(230, 20, 62, 22);
            minimize = Make("", 300, 20, 26, 22, delegate { WindowState = FormWindowState.Minimized; });
            minimize.Glyph = 1;
            minimize.AccessibleName = "Minimize";
            close = Make("", 330, 20, 26, 22, delegate { Close(); });
            close.Glyph = 2;
            close.AccessibleName = "Close";
            more = Make("Settings", 146, 20, 84, 22, delegate { ShowSettings(!paged); });
            more.Accent = Look.Cyan;
            more.Loud = true;
            int header = Controls.Count;
            for (int i = 0; i < header; i++) Scrolls(Controls[i]);
            Caption("BRAIN >", 28, 58, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow);
            brainName = Caption("", 28, 78, Look.Mono(10.5f, FontStyle.Bold, GraphicsUnit.Point), Look.Ink);
            brainNote = Caption("", 28, 100, Font, Look.Dim);
            modelMenu = Dropdown();
            models = Make("", 28, 134, 244, 28, delegate { modelMenu.Show(models, new Point(0, models.Height)); });
            models.Chevron = true;
            refresh = Make("Refresh", 280, 134, 76, 28, delegate { capri.FindBrain(); });
            for (int i = 0; i < 3; i++) steps[i] = Caption("", 28, 138 + i * 22, Font, Look.Dim);
            setup = Make("Set up her brain", 28, 226, 170, 28, delegate { if (capri.SettingUp) capri.CancelSetup(); else capri.SetUpBrain(); });
            setup.Accent = Look.Cyan;
            setup.Loud = true;
            hint = Caption("Have Ollama? Open it, then press Refresh.", 28, 264, Look.Mono(9f, FontStyle.Italic, GraphicsUnit.Point), Look.Dim);
            tierNote = Caption("Pick one. Only one brain runs at a time.", 28, 168, Look.Mono(8.5f, FontStyle.Italic, GraphicsUnit.Point), Look.Dim);
            string[,] tiers = { { "NORMAL", "Free · local model" }, { "HIGHER", "Free · NVIDIA API" }, { "MAX INTELLIGENCE", "ChatGPT or Claude API" } };
            for (int i = 0; i < rungs.Length; i++)
            {
                int picked = i;
                Rung rung = new Rung();
                rung.Text = tiers[i, 0];
                rung.Hint = tiers[i, 1];
                rung.Font = Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point);
                rung.HintFont = Font;
                rung.AccessibleName = tiers[i, 0] + ", " + tiers[i, 1];
                rung.Bounds = new Rectangle(28, 184 + 24 * i, i == 1 ? 232 : 328, 24);
                rung.CheckedChanged += delegate { if (!syncing && rungs[picked].Checked) capri.ChooseTier(picked); };
                Controls.Add(rung);
                rungs[i] = rung;
            }
            rungs[0].Flag = "Start with this!";
            rungs[0].FlagFont = Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point);
            freeKey = Make("Free key", 266, 209, 90, 22, delegate { capri.OpenFreeKeyPage(); });
            textEntry = Input(28, 262, 244);
            textEntry.Box.UseSystemPasswordChar = true;
            textEntry.Box.AccessibleName = "NVIDIA API key";
            textEntry.Cue = "Paste your free NVIDIA key";
            textConnect = Make("Connect", 280, 262, 76, 28, delegate { ConnectText(); });
            textEntry.Box.TextChanged += delegate { RelabelText(); };
            textEntry.Box.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ConnectText(); } };
            voiceHead = Lower(Caption("VOICE >", 28, 184, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow));
            shortcut = Caption("Ctrl + Alt + V to talk", 156, 182, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim);
            shortcut.AutoSize = false;
            shortcut.TextAlign = ContentAlignment.MiddleRight;
            shortcut.Bounds = new Rectangle(156, 182, 200, 20);
            shortcut.Cursor = Cursors.Hand;
            // Keys are picked in Settings, where Confirm and Cancel sit beside them.
            shortcut.Click += delegate { ShowSettings(true); PickKeys(0); };
            Lower(shortcut);
            string[,] voiceTiers = { { "OFF", "Silent · text only" }, { "PREBUILT LOCAL", "Her voices · free" }, { "CUSTOM LOCAL", "Your own model" }, { "CLOUD VOICE", "Needs a key" } };
            for (int i = 0; i < voiceRungs.Length; i++)
            {
                int row = i;
                Rung rung = new Rung();
                // A set of its own: a self-checking radio button would untick the BRAIN tiers too.
                rung.AutoCheck = false;
                rung.TabStop = true;
                rung.Text = voiceTiers[i, 0];
                rung.Hint = voiceTiers[i, 1];
                rung.Font = Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point);
                rung.HintFont = Font;
                rung.AccessibleName = voiceTiers[i, 0] + ", " + voiceTiers[i, 1];
                rung.Bounds = new Rectangle(28, 208 + 24 * i, i >= 2 ? 246 : 328, 24);
                rung.Click += delegate { if (syncing) return; PickRow(row); };
                Controls.Add(rung);
                voiceRungs[i] = Lower(rung);
            }
            setUpModel = Lower(Make("Set up", 276, 257, 80, 22, delegate { OpenServer(false, null); }));
            setUpModel.AccessibleName = "Set up your own voice model";
            keyHelp = Lower(Make("Setup guide", 276, 281, 80, 22, delegate { CloudGuide(); }));
            cloudMenu = Dropdown();
            foreach (string name in Clouds)
            {
                string each = name;
                cloudMenu.Items.Add(Choice(each, false, delegate { PickCloud(each); }));
            }
            cloudPick = Lower(Make(Clouds[0], 28, KeyAt, 100, 28, delegate { cloudMenu.Show(cloudPick, new Point(0, cloudPick.Height)); }));
            cloudPick.Chevron = true;
            cloudPick.AccessibleName = "Cloud voice service";
            keyEntry = Lower(Input(136, KeyAt, 136));
            keyEntry.Box.UseSystemPasswordChar = true;
            keyEntry.Box.AccessibleName = "ElevenLabs API key";
            connect = Lower(Make("Connect", 280, KeyAt, 76, 28, delegate { Connect(); }));
            keyEntry.Box.TextChanged += delegate { Relabel(); };
            keyEntry.Box.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Connect(); } };
            // Fish Audio and OpenAI keep their key, voice and model in the voice model panel.
            cloudVoice = Lower(Make("", 136, KeyAt, 220, 28, delegate { OpenServer(true, CloudApi(cloudName)); }));
            voiceMenu = Dropdown();
            voiceEntry = Lower(Input(28, 346, 196));
            voiceEntry.Chevron = true;
            voiceEntry.Cue = "Voice ID";
            voiceEntry.Box.AccessibleName = "ElevenLabs voice ID";
            voiceEntry.Drop += delegate { voiceMenu.Show(voiceEntry, new Point(0, voiceEntry.Height)); };
            voiceEntry.Box.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; capri.ChooseVoice(voiceEntry.Box.Text); } };
            voiceEntry.Box.Leave += delegate { capri.ChooseVoice(voiceEntry.Box.Text); };
            speechModelMenu = Dropdown();
            speechModels = Lower(Make("Default model", 232, 346, 124, 28, delegate { speechModelMenu.Show(speechModels, new Point(0, speechModels.Height)); }));
            speechModels.Chevron = true;
            freeMenu = Dropdown();
            freeVoices = Lower(Make("Free voice: Her own", 28, 346, 196, 28, delegate { freeMenu.Show(freeVoices, new Point(0, freeVoices.Height)); }));
            freeVoices.Chevron = true;
            freeVoices.Visible = false;
            // CUSTOM LOCAL's model in use. Its panel offers only what runs on their PC.
            customVoice = Lower(Make("Voice model", 28, 346, 328, 28, delegate { OpenServer(false, null); }));
            customVoice.Chevron = true;
            customVoice.AccessibleName = "Your voice model";
            customVoice.Visible = false;
            freeSetup = Lower(Make("Get free voice", 232, 346, 124, 28, delegate { if (capri.SettingUpVoice) capri.CancelFreeVoice(); else capri.SetUpFreeVoice(); }));
            freeSetup.Accent = Look.Cyan;
            freeSetup.Loud = true;
            freeSetup.Visible = false;
            // One click from their voice back to hers, in the same spot as Get free voice, which only shows before her free voice is in.
            herVoice = Lower(Make("Her voice", 232, 346, 124, 28, delegate { capri.UseHerVoice(); }));
            herVoice.AccessibleName = "Back to her own HD voice";
            herVoice.Visible = false;
            micMenu = Dropdown();
            mics = Lower(Make("Mic: default", 28, 382, 244, 28, delegate { ShowMics(); }));
            mics.Chevron = true;
            mics.AccessibleName = "Microphone";
            Lower(Make("Test", 280, 382, 76, 28, delegate { capri.TestVoice(); }));
            muteShortcut = Lower(Caption("Ctrl + Alt + N mutes mic", 28, 417, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim));
            muteShortcut.Cursor = Cursors.Hand;
            muteShortcut.Click += delegate { ShowSettings(true); PickKeys(1); };
            Deactivate += delegate { CancelKeys(); };
            voiceNote = Lower(Caption("", 28, InstallAt, Font, Look.Dim));
            voiceNote.Click += delegate { if (speechFix) capri.OpenSpeechSettings(); };
            installNote = Caption("", 28, InstallAt + 2, Font, Look.Ink);
            installNote.Visible = false;
            int v = VoiceHeight;
            Lower(Caption("CAPRI >", 28, 184 + v, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow));
            showHide = Lower(Make("Hide Capri", 28, 206 + v, 104, 28, delegate { capri.ToggleHidden(); }));
            Lower(Make("Bring here", 140, 206 + v, 104, 28, delegate { capri.BringHere(); }));
            perch = Lower(Make("Ride cursor", 252, 206 + v, 104, 28, delegate { capri.TogglePerch(); }));
            Lower(Make("Check for updates", 28, 250 + v, 150, 28, delegate { capri.CheckUpdates(); }));
            Key help = Lower(Make("Help", 186, 250 + v, 62, 28, delegate { capri.OpenHelp(); }));
            help.AccessibleName = "Help: ask Capri on the website";
            Key quit = Lower(Make("Quit Capri", 256, 250 + v, 100, 28, delegate { capri.Quit(); }));
            quit.Accent = Look.Red;
            updateNote = Lower(Caption("", 28, 288 + v, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim));
            for (int i = header; i < Controls.Count; i++) mainLeft.Add(new KeyValuePair<Control, int>(Controls[i], Controls[i].Left));
            int first = Controls.Count;
            Caption("YOU >", 28, 58, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow);
            personalityNote = Caption("", 84, 59, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim);
            Caption("Language", 28, 86, Font, Look.Dim);
            languageMenu = Dropdown();
            for (int i = 0; i < Brain.Languages.GetLength(0); i++)
            {
                string picked = Brain.Languages[i, 1];
                ToolStripMenuItem item = Choice(Brain.Languages[i, 0], false, delegate { capri.SetLanguage(picked); Sync(); });
                item.Tag = picked;
                languageMenu.Items.Add(item);
            }
            languages = Make("English", 128, 80, 228, 28, delegate { languageMenu.Show(languages, new Point(0, languages.Height)); });
            languages.Chevron = true;
            languages.AccessibleName = "Her language";
            // A label of its own, so the question still shows once a name is saved in the box.
            Caption("What should she call you?", 28, 118, Font, Look.Dim);
            Make("Get to know me", 236, 113, 120, 22, delegate { capri.GetToKnow(); }).AccessibleName = "Get to know me: she asks what to call you, your pronouns and what you're into, and keeps them in me.md";
            nickname = Input(28, 140, 244);
            nickname.Cue = "Your name or a nickname";
            nickname.Box.AccessibleName = "What should she call you?";
            nickname.Box.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; UseNickname(); } };
            Make("Save", 280, 140, 76, 28, delegate { UseNickname(); });
            Make("Open her notes", 28, 176, 120, 28, delegate { capri.OpenNotes(); }).AccessibleName = "Open her notes: me.md, her notes about you";
            Make("Edit personality", 156, 176, 130, 28, delegate { capri.OpenPersonality(); }).AccessibleName = "Edit her personality: personality.md";
            Make("Reset", 294, 176, 62, 28, delegate { capri.ResetPersonality(); }).AccessibleName = "Reset her personality to her default";
            Caption("VOICE >", 28, 220, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow);
            Label styleNote = Caption("Every voice, ElevenLabs too", 120, 218, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim);
            styleNote.AutoSize = false;
            styleNote.TextAlign = ContentAlignment.MiddleRight;
            styleNote.Bounds = new Rectangle(120, 218, 236, 20);
            Caption("Volume", 28, 249, Font, Look.Dim);
            volume = PageSlider(VolumeNames, 242, "Her voice volume");
            volumeName = Caption("100%", 292, 249, Font, Look.Ink);
            volume.Changed += delegate { if (syncing) return; volumeName.Text = VolumeNames[volume.Value]; capri.SetVolume(Volumes[volume.Value]); };
            // Her voice's sliders change every voice she has as they move, and she says a line once they're let go.
            Caption("Pitch", 28, 285, Font, Look.Dim);
            pitchSlider = PageSlider(PitchNames, 278, "Her voice's pitch");
            pitchName = Caption("Normal", 292, 285, Font, Look.Ink);
            pitchSlider.Changed += delegate { if (syncing) return; pitchName.Text = PitchNames[pitchSlider.Value]; capri.SetVoicePitch(pitchSlider.Value - VoiceFx.MostPitch); };
            pitchSlider.Released += delegate { if (!syncing) capri.HearVoiceStyle(); };
            Caption("Speed", 28, 321, Font, Look.Dim);
            paceSlider = PageSlider(PaceNames, 314, "Her voice's speed");
            paceName = Caption("Normal", 292, 321, Font, Look.Ink);
            paceSlider.Changed += delegate { if (syncing) return; paceName.Text = PaceNames[paceSlider.Value]; capri.SetVoiceSpeed(Paces[paceSlider.Value]); };
            paceSlider.Released += delegate { if (!syncing) capri.HearVoiceStyle(); };
            Caption("Robot", 28, 357, Font, Look.Dim);
            robotSlider = PageSlider(AmountNames, 350, "How robotic her voice sounds");
            robotName = Caption("Off", 292, 357, Font, Look.Ink);
            robotSlider.Changed += delegate { if (syncing) return; robotName.Text = AmountName(robotSlider.Value); capri.SetVoiceRobot(Amounts[robotSlider.Value]); };
            robotSlider.Released += delegate { if (!syncing) capri.HearVoiceStyle(); };
            Caption("Glitch", 28, 393, Font, Look.Dim);
            glitchSlider = PageSlider(AmountNames, 386, "How much her voice glitches");
            glitchName = Caption("Off", 292, 393, Font, Look.Ink);
            glitchSlider.Changed += delegate { if (syncing) return; glitchName.Text = AmountName(glitchSlider.Value); capri.SetVoiceGlitch(Amounts[glitchSlider.Value]); };
            glitchSlider.Released += delegate { if (!syncing) capri.HearVoiceStyle(); };
            Caption("Sounds", 28, 429, Font, Look.Dim);
            sounds = PageSlider(SoundNames, 422, "Sound effects volume");
            soundsName = Caption("60%", 292, 429, Font, Look.Ink);
            sounds.Changed += delegate { if (syncing) return; soundsName.Text = SoundNames[sounds.Value]; capri.SetSoundLevel(sounds.Value * 10); };
            typing = Check("Typing sound", 28, 458, 328, delegate { if (!syncing) capri.ToggleTyping(); });
            typing.Hint = "When she types";
            typing.HintFont = Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point);
            typing.AccessibleName = "Typing sound. A soft typing sound while her text replies type out";
            // Shares Typing sound's row, and only while her HD voice is on (Lachy, 29 Oct). Off out of the box.
            hdGames = Check("Free voice in games", 186, 458, 170, delegate { if (!syncing) capri.ToggleHdGameRest(); });
            hdGames.AccessibleName = "Free voice in games: while a game or video is full screen, her free voice talks instead of her HD voice, so the game gets the graphics card";
            hdGames.Visible = false;
            Caption("MOVES >", 28, 494, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow);
            // Her mini player shows what's playing while music plays, with her perched on it. It sits in MOVES' heading row,
            // like Stay on my model beside BRAIN, since Free voice in games takes the rest of Typing sound's row while HD is on.
            miniPlayer = Check("Mini player", 186, 492, 170, delegate { if (!syncing) capri.TogglePlayer(); });
            miniPlayer.AccessibleName = "Mini player. It shows what's playing while music plays, and she sits on it";
            Caption("Wander", 28, 523, Font, Look.Dim);
            wander = PageSlider(WanderNames, 516, "How much she wanders");
            wanderName = Caption("Off", 292, 523, Font, Look.Ink);
            wander.Changed += delegate { if (syncing) return; wanderName.Text = WanderNames[wander.Value]; capri.SetWanderLevel(wander.Value); };
            Caption("Sleep", 28, 559, Font, Look.Dim);
            sleepAfter = PageSlider(SleepNames, 552, "How long until she falls asleep");
            sleepName = Caption("2 min", 292, 559, Font, Look.Ink);
            sleepAfter.Changed += delegate { if (syncing) return; sleepName.Text = SleepNames[sleepAfter.Value]; capri.SetSleepAfter(Sleeps[sleepAfter.Value]); };
            // How often Check in on me brings her over to say hi.
            Caption("Pop-ins", 28, 595, Font, Look.Dim);
            pops = PageSlider(PopNames, 588, "How often she pops in to say hi");
            popsName = Caption("Sometimes", 292, 595, Font, Look.Ink);
            pops.Changed += delegate { if (syncing) return; popsName.Text = PopNames[pops.Value]; capri.SetPopPace(pops.Value); };
            dance = Check("Dance to music", 28, 624, 150, delegate { if (!syncing) capri.ToggleDance(); });
            intro = Check("Glitch in at startup", 186, 624, 170, delegate { if (!syncing) capri.ToggleIntro(); });
            const int c = Column;
            Caption("CAPRI >", 28 + c, 58, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow);
            Caption("Size", 28 + c, 87, Font, Look.Dim);
            sizeSlider = PageSlider(SizeNames, 80, "Her size", c);
            sizeSlider.Changed += delegate { if (syncing) return; sizeName.Text = SizeNames[sizeSlider.Value]; capri.SetCharacterSize(SizeSteps[sizeSlider.Value]); };
            sizeName = Caption("Medium", 292 + c, 87, Font, Look.Ink);
            checkIns = Check("Check in on me", 28 + c, 116, 150, delegate { if (!syncing) capri.ToggleCheckIns(); });
            onTop = Check("Always on top", 186 + c, 116, 170, delegate { if (!syncing) capri.ToggleOnTop(); });
            Caption("WATCH >", 28 + c, 162, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow);
            watch = Check("Watch my screen", 28 + c, 186, 150, delegate { if (!syncing) capri.ToggleWatch(); });
            watch.AccessibleName = "Watch my screen. She comments now and then";
            caughtUp = Check("Keep her caught up", 186 + c, 186, 170, delegate { if (!syncing) capri.ToggleCaughtUp(); });
            caughtUp.AccessibleName = "Keep her caught up on my screen. Glances she stays quiet about still go in her memory, which uses a little more of your API";
            Caption("Every", 28 + c, 223, Font, Look.Dim);
            gap = PageSlider(GapNames, 216, "Time between glances", c);
            gap.Changed += delegate { if (syncing) return; gapName.Text = GapNames[gap.Value]; capri.SetGlanceGap(Gaps[gap.Value]); };
            gapName = Caption("2 min", 292 + c, 223, Font, Look.Ink);
            Caption("BRAIN >", 28 + c, 266, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow);
            stayOnModel = Check("Stay on my model", 186 + c, 264, 170, delegate { if (!syncing) capri.ToggleStayOnModel(); });
            stayOnModel.AccessibleName = "Stay on my model. When an NVIDIA model gets stuck she says so instead of switching to another one";
            Caption("Replies", 28 + c, 295, Font, Look.Dim);
            replies = PageSlider(Lengths, 288, "Reply length", c);
            repliesName = Caption("Short", 292 + c, 295, Font, Look.Ink);
            replies.Changed += delegate { if (syncing) return; repliesName.Text = Lengths[replies.Value]; capri.SetWordiness(replies.Value); };
            Caption("Memory", 28 + c, 331, Font, Look.Dim);
            recall = PageSlider(Lengths, 324, "How much of the chat she keeps", c);
            recallName = Caption("Short", 292 + c, 331, Font, Look.Ink);
            recall.Changed += delegate { if (syncing) return; recallName.Text = Lengths[recall.Value]; capri.SetRecall(Recalls[recall.Value]); };
            Caption("Bubble", 28 + c, 367, Font, Look.Dim);
            bubble = PageSlider(BubbleNames, 360, "How long her replies stay up", c);
            bubbleName = Caption("Normal", 292 + c, 367, Font, Look.Ink);
            bubble.Changed += delegate { if (syncing) return; bubbleName.Text = BubbleNames[bubble.Value]; capri.SetBubbleTime(bubble.Value); };
            custom = Input(28 + c, 398, 244);
            custom.Cue = "Custom model name";
            custom.Box.AccessibleName = "Custom model name";
            custom.Box.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; UseCustom(); } };
            Make("Use", 280 + c, 398, 76, 28, delegate { UseCustom(); });
            customNote = Caption("Any Ollama model you've pulled, or a model id on your key.", 28 + c, 434, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim);
            customNote.MaximumSize = new Size(328, 0);
            Caption("HABITS >", 28 + c, 482, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow);
            startup = Check("Start with Windows", 28 + c, 506, 150, delegate { if (!syncing) capri.ToggleStartup(); });
            discord = Check("Show on Discord", 186 + c, 506, 170, delegate { if (!syncing) capri.ToggleDiscord(); });
            cursor = Check("Use her cursor", 28 + c, 538, 150, delegate { if (!syncing) capri.ToggleCursor(); });
            // Blackwall mode was "Bypass all permissions" until 0.7.4; its label says what it does.
            bypass = Check("Blackwall mode", 186 + c, 538, 170, delegate { if (!syncing) capri.ToggleBypass(); });
            bypass.AccessibleName = "Blackwall mode. She won't ask twice when you ask her to do something";
            blackwallNote = Caption("She won't ask twice when you ask her to do something.", 28 + c, 562, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim);
            blackwallNote.AutoSize = false;
            blackwallNote.TextAlign = ContentAlignment.MiddleRight;
            blackwallNote.Bounds = new Rectangle(28 + c, 562, 328, 18);
            // Frames a second while she sits on a window. Her sway takes 2 s at every fps; past 30 she draws in-betweens.
            Caption("Perch", 28 + c, 597, Font, Look.Dim);
            perchFps = PageSlider(PerchNames, 590, "Her perch frames per second", c);
            perchFpsName = Caption("30 fps", 292 + c, 597, Font, Look.Ink);
            perchFps.Changed += delegate { if (syncing) return; capri.SetPerchFps(Companion.PerchRates[perchFps.Value]); ShowPerch(); };
            perchNote = Caption("", 120 + c, 480, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim);
            perchNote.AutoSize = false;
            perchNote.TextAlign = ContentAlignment.MiddleRight;
            perchNote.Bounds = new Rectangle(120 + c, 480, 236, 20);
            remembering = Check("Remember our chats", 28 + c, 626, 150, delegate { if (!syncing) capri.ToggleRemembering(); });
            remembering.AccessibleName = "Remember our chats. She keeps daily transcripts and memories.md on this PC, never uploaded";
            Make("Open her memories", 186 + c, 626, 170, 22, delegate { capri.OpenMemories(); }).AccessibleName = "Open her memories: memories.md and her chat transcripts, kept on this PC";
            Caption("KEYS >", 28, 678, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow);
            talkKeys = Make("Talk: Ctrl + Alt + V", 96, 672, 250, 28, delegate { PickKeys(0); });
            talkKeys.AccessibleName = "Her talk key. Click, press new keys, then Confirm";
            muteKeys = Make("Mute mic: Ctrl + Alt + N", 358, 672, 250, 28, delegate { PickKeys(1); });
            muteKeys.AccessibleName = "Her mute key. Click, press new keys, then Confirm";
            typeKeys = Make("Type: Ctrl + Alt + T", 96, 706, 250, 28, delegate { PickKeys(2); });
            typeKeys.AccessibleName = "Her typing key. Click, press new keys, then Confirm";
            hideKeys = Make("Hide/show: Ctrl + Alt + H", 358, 706, 250, 28, delegate { PickKeys(3); });
            hideKeys.AccessibleName = "Her hide key. Click, press new keys, then Confirm";
            quietKeys = Make("Quiet: Ctrl + Alt + P", 96, 740, 250, 28, delegate { PickKeys(4); });
            quietKeys.AccessibleName = "Her quiet key: she only talks when you talk to her until you press it again. Click, press new keys, then Confirm";
            // Only a click ends picking a key, so Enter, Esc and Tab can be her keys too.
            confirmKeys = Make("Confirm", 620, 672, 116, 28, delegate { ConfirmKeys(); });
            confirmKeys.Accent = Look.Cyan;
            confirmKeys.Loud = true;
            confirmKeys.AccessibleName = "Confirm her new keys";
            cancelKeys = Make("Cancel", 620, 706, 116, 28, delegate { CancelKeys(); });
            cancelKeys.AccessibleName = "Cancel and keep her old keys";
            confirmKeys.Visible = cancelKeys.Visible = false;
            keysNote = Caption("", 96, 776, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim);
            keysNote.MaximumSize = new Size(512, 0);
            for (int i = first; i < Controls.Count; i++)
            {
                Control control = Controls[i];
                pageLeft.Add(new KeyValuePair<Control, int>(control, control.Left));
                Scrolls(control);
                control.Left += Far;
                Reachable(control, false);
                // Clicking into anything else while a key is being picked keeps the old key, so typing in a box is never swallowed.
                control.Enter += delegate { if (capturing != 0 && control != confirmKeys && control != cancelKeys && control != PickedKey) CancelKeys(); };
            }
            Controls.Add(topRim);
            Controls.Add(bottomRim);
            topRim.BringToFront();
            bottomRim.BringToFront();
            Searching();
            Sync();
            Application.AddMessageFilter(this);
        }

        private Slider PageSlider(string[] names, int y, string name, int x = 0)
        {
            Slider slider = new Slider(names);
            slider.Bounds = new Rectangle(96 + x, y, 188, 30);
            slider.AccessibleName = name;
            Controls.Add(slider);
            return slider;
        }

        private void UseNickname()
        {
            capri.SetNickname(nickname.Box.Text);
            nickname.Box.Text = capri.Nickname;
        }

        internal string NicknameShown { get { return nickname.Box.Text; } }

        internal void TypeNickname(string typed) { nickname.Box.Text = typed; UseNickname(); }

        private void UseCustom()
        {
            customNote.Text = capri.ChooseCustomModel(custom.Box.Text, lastInstalled);
        }

        internal bool SettingsShown { get { return paged; } }

        internal string SizeStatus { get { return sizeName.Text; } }

        internal bool TypingShown { get { return typing.Checked; } }

        internal string HdGamesStatus { get { return hdGames.Visible ? (hdGames.Checked ? "on" : "off") : "-"; } }

        // Free voice in games shows beside Typing sound only while her HD voice is on; without it, Typing sound has the row.
        private void ShowHdGames()
        {
            bool was = syncing, hd = capri.HdVoiceOn;
            syncing = true;
            hdGames.Visible = hd;
            hdGames.Checked = capri.HdGameRest;
            typing.Width = Scaled(hd ? 150 : 328);
            typing.Hint = hd ? null : "When she types";
            typing.Invalidate();
            syncing = was;
        }

        internal bool PlayerShown { get { return miniPlayer.Checked; } }

        internal void FlipPlayer() { miniPlayer.Checked = !miniPlayer.Checked; }

        internal string HabitsStatus { get { return startup.Checked ? "startup" : "-"; } }

        internal bool DiscordShown { get { return discord.Checked; } }

        internal string JobsStatus { get { return (cursor.Checked ? "cursor" : "-") + " | " + (bypass.Checked ? "bypass" : "-"); } }

        internal string BlackwallStatus { get { return bypass.Text + " | " + blackwallNote.Text; } }

        internal string SettingsStatus { get { return languages.Text + " | " + volumeName.Text + " | " + soundsName.Text + " | " + wanderName.Text + " | " + sleepName.Text + " | " + (dance.Checked ? "dance" : "-") + " | " + (intro.Checked ? "intro" : "-") + " | " + repliesName.Text + " | " + recallName.Text + " | " + bubbleName.Text; } }

        internal void ShowSettings(bool on)
        {
            if (!on) CancelKeys();
            paged = on;
            more.Text = on ? "Back" : "Settings";
            more.Invalidate();
            foreach (KeyValuePair<Control, int> entry in mainLeft) { entry.Key.Left = Scaled(entry.Value) + (on ? Far : 0); Reachable(entry.Key, !on); }
            foreach (KeyValuePair<Control, int> entry in pageLeft) { entry.Key.Left = Scaled(entry.Value) + (on ? 0 : Far); Reachable(entry.Key, on); }
            int edge = on ? Column : 0;
            build.Left = Scaled(230 + edge);
            minimize.Left = Scaled(300 + edge);
            close.Left = Scaled(330 + edge);
            if (Hidden(ActiveControl)) more.Focus();
            if (on) Sync();
            wheel = 0;
            Arrange(expanded);
            Rectangle area = Screen.FromControl(this).WorkingArea;
            if (on && Right > area.Right) Left = Math.Max(area.Left, area.Right - Width);
            // She opens centred on the shorter main page, so the taller Settings page can hang off the bottom.
            if (on && Bottom > area.Bottom) Top = Math.Max(area.Top, area.Bottom - Height);
        }

        // A header or settings control moves with the page, and Tab landing on it scrolls it into view.
        private void Scrolls(Control control)
        {
            scrolled.Add(control);
            control.Enter += delegate { Reveal(control); };
        }

        // Scrolls just far enough to show all of a control Tab reached. A click leaves the page where it is.
        private void Reveal(Control control)
        {
            if (!paged || pageView >= PageTall || Control.MouseButtons != MouseButtons.None) return;
            float s = DeviceScale();
            int edge = Scaled(8);
            // Near the top it goes all the way up, so the header shows too.
            int up = pageScroll - (int)Math.Ceiling((edge - control.Top) / s);
            if (control.Top < edge) ScrollPage(up < 2 * ScrollRow ? 0 : up);
            else if (control.Bottom > ClientSize.Height - edge) ScrollPage(pageScroll + (int)Math.Ceiling((control.Bottom - ClientSize.Height + edge) / s));
        }

        // Moves the page and its header up by `to` px, as far as the page runs past the screen. Off Settings it's always 0.
        private void ScrollPage(int to)
        {
            int next = paged ? Math.Max(0, Math.Min(PageTall - pageView, to)) : 0;
            if (next == pageScroll) return;
            if (pageScroll == 0) foreach (Control control in scrolled) tops[control] = control.Top;
            pageScroll = next;
            int shift = Scaled(next);
            // Every control moves at once, with no half-scrolled frame in between.
            Native.SendMessage(Handle, 0x000B, IntPtr.Zero, IntPtr.Zero);
            try { foreach (Control control in scrolled) control.Top = tops[control] - shift; }
            finally { Native.SendMessage(Handle, 0x000B, new IntPtr(1), IntPtr.Zero); }
            Invalidate(true);
        }

        // How much of the settings page the screen's working area has room for, before scaling.
        private int Fit()
        {
            int room = ShortScreen > 0 ? ShortScreen : (int)(Screen.FromControl(this).WorkingArea.Height / DeviceScale());
            return Math.Max(4 * ScrollRow, Math.Min(PageTall, room));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!paged || pageView >= PageTall) return;
            wheel += e.Delta;
            int rows = wheel / 120;
            wheel -= rows * 120;
            if (rows != 0) ScrollPage(pageScroll - rows * ScrollRow);
        }

        private void Reachable(Control control, bool on)
        {
            if (!stops.ContainsKey(control)) stops[control] = control.TabStop;
            control.TabStop = on && stops[control];
            foreach (Control child in control.Controls) Reachable(child, on);
        }

        private bool Hidden(Control control)
        {
            List<KeyValuePair<Control, int>> hidden = paged ? mainLeft : pageLeft;
            for (Control top = control; top != null && top != this; top = top.Parent)
                foreach (KeyValuePair<Control, int> entry in hidden) if (entry.Key == top) return true;
            return false;
        }

        internal string TabWalk()
        {
            Control from = more;
            int reached = 0;
            for (int i = 0; i < 80; i++)
            {
                if (!SelectNextControl(from, true, true, true, true)) break;
                Control at = ActiveControl;
                if (at == null || at == from || at == more) break;
                if (Hidden(at)) return "Tab reached a hidden " + (at.AccessibleName ?? at.Text);
                reached++;
                from = at;
            }
            more.Focus();
            return reached == 0 ? "Tab reached nothing" : null;
        }

        private T Lower<T>(T control) where T : Control
        {
            lower.Add(new KeyValuePair<Control, int>(control, control.Top));
            return control;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams value = base.CreateParams;
                value.Style = (value.Style | 0x00020000 | 0x00080000) & ~0x00010000;
                return value;
            }
        }

        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            if (message.Msg == 0x0084 && message.Result == new IntPtr(1)) message.Result = new IntPtr(2);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            loaded = true;
            Arrange(expanded);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            Shape();
            Invalidate(true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float s = DeviceScale();
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Backdrop(graphics, this, s);
            Frame(graphics, s);
            if (paged)
            {
                using (Brush red = new SolidBrush(Look.Red))
                    foreach (int[] bar in PageBars) graphics.FillRectangle(red, (13 + bar[0]) * s, (bar[1] - pageScroll) * s, 3 * s, (bar[2] - bar[1]) * s);
                if (pageView >= PageTall) return;
                // A thin thumb on her right edge shows how much of the page is on screen, and where.
                float rail = Height - 36 * s;
                float thumb = rail * pageView / PageTall;
                float at = 18 * s + (rail - thumb) * pageScroll / (PageTall - pageView);
                using (Brush track = new SolidBrush(Track)) graphics.FillRectangle(track, Width - 9 * s, 18 * s, 3 * s, rail);
                using (Brush lit = new SolidBrush(Look.Cyan)) graphics.FillRectangle(lit, Width - 9 * s, at, 3 * s, thumb);
                return;
            }
            int shift = (expanded ? SetupHeight : 0) + KeyRow;
            int below = voiceFilled >= 0 ? InstallRow : 0;
            int fold = arrangedFold;
            using (Brush red = new SolidBrush(Look.Red))
            {
                graphics.FillRectangle(red, 13 * s, 58 * s, 3 * s, ((expanded ? 280 : 162) + KeyRow - 58) * s);
                graphics.FillRectangle(red, 13 * s, (184 + shift) * s, 3 * s, (InstallAt + 29 - 184 + below - fold) * s);
                graphics.FillRectangle(red, 13 * s, (184 + VoiceHeight + shift + below - fold) * s, 3 * s, (234 - 184) * s);
            }
            if (expanded) Dots(graphics, s, 210, filled);
            if (below > 0) Dots(graphics, s, InstallAt + 24 + shift - fold, voiceFilled);
        }

        // Her cyan frame, which the rims draw again over settings scrolled under her edges.
        private void Frame(Graphics graphics, float s)
        {
            using (GraphicsPath edge = Look.Plate(new RectangleF(s, s, Width - 2 * s - 1, Height - 2 * s - 1), 12 * s, -1, Height / 2f, 0, true))
            using (Pen cyan = new Pen(Look.Cyan, 2 * s))
                graphics.DrawPath(cyan, edge);
        }

        // The dotted bar her brain setup and her voice downloads share.
        private static void Dots(Graphics graphics, float s, int y, float share)
        {
            float pitch = 6 * s;
            int count = (int)(328 / 6f);
            int lit = (int)Math.Round(count * share);
            using (Brush on = new SolidBrush(Look.Cyan))
            using (Brush off = new SolidBrush(Track))
                for (int i = 0; i < count; i++) graphics.FillRectangle(i < lit ? on : off, 28 * s + i * pitch, y * s, pitch - 2 * s, 6 * s);
        }

        private static void Backdrop(Graphics graphics, Control control, float s)
        {
            graphics.Clear(Look.Panel);
            float pitch = 3 * s;
            float first = control is Form ? 0 : (pitch - control.Top % pitch) % pitch;
            using (Pen scan = new Pen(Color.FromArgb(9, 255, 255, 255), Math.Max(1, s)))
                for (float row = first; row < control.Height; row += pitch) graphics.DrawLine(scan, 0, row, control.Width, row);
        }

        private void Shape()
        {
            float s = DeviceScale();
            Region old = Region;
            using (GraphicsPath plate = Look.Plate(new RectangleF(0, 0, Width, Height), 12 * s, -1, Height / 2f, 0, true)) Region = new Region(plate);
            if (old != null) old.Dispose();
        }

        private float DeviceScale() { return CurrentAutoScaleDimensions.Width / 96f; }

        // The scale of whichever of her windows a control is on: the Mainframe, or their voice server's panel.
        private static float ScaleOf(Control control)
        {
            Form form = control.FindForm();
            return form == null ? 1f : form.CurrentAutoScaleDimensions.Width / 96f;
        }

        private int Scaled(int value) { return loaded ? (int)Math.Round(value * DeviceScale()) : value; }

        private void Arrange(bool setupShown)
        {
            expanded = setupShown;
            int top = setupShown ? SetupHeight : 0, shift = top + KeyRow;
            tierNote.Top = Scaled(168 + top);
            for (int i = 0; i < rungs.Length; i++) rungs[i].Top = Scaled(184 + 24 * i + top);
            freeKey.Top = Scaled(209 + top);
            textEntry.Top = Scaled(262 + top);
            textConnect.Top = textEntry.Top;
            textEntry.Visible = textConnect.Visible = tier > 0;
            int below = voiceFilled >= 0 ? InstallRow : 0;
            int fold = arrangedFold = Fold;
            foreach (KeyValuePair<Control, int> entry in lower) entry.Key.Top = Scaled(entry.Value + shift + (entry.Value >= InstallAt ? below : 0) - (entry.Value > KeyAt ? fold : 0));
            installNote.Top = Scaled(InstallAt + 2 + shift - fold);
            installNote.Visible = below > 0;
            foreach (Label step in steps) step.Visible = setupShown;
            setup.Visible = setupShown;
            refresh.Top = Scaled(setupShown ? 226 : 134);
            brainNote.MaximumSize = new Size(Scaled(328), 0);
            voiceNote.MaximumSize = new Size(Scaled(328), 0);
            updateNote.MaximumSize = new Size(Scaled(328), 0);
            customNote.MaximumSize = new Size(Scaled(328), 0);
            if (keysNote != null) keysNote.MaximumSize = new Size(Scaled(512), 0);
            if (paged) pageView = Fit();
            ClientSize = new Size(Scaled(paged ? Wide + Column : Wide), Scaled(paged ? pageView : Tall + shift + below - fold));
            ScrollPage(pageScroll);
            bool scrolls = paged && pageView < PageTall;
            topRim.Visible = bottomRim.Visible = scrolls;
            if (scrolls)
            {
                topRim.Bounds = new Rectangle(Scaled(14), 0, ClientSize.Width - Scaled(28), Scaled(3));
                bottomRim.Bounds = new Rectangle(Scaled(14), ClientSize.Height - Scaled(3), ClientSize.Width - Scaled(28), Scaled(3));
            }
            Invalidate(true);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!Quitting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; WindowState = FormWindowState.Minimized; }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { Application.RemoveMessageFilter(this); modelMenu.Dispose(); voiceMenu.Dispose(); speechModelMenu.Dispose(); micMenu.Dispose(); languageMenu.Dispose(); freeMenu.Dispose(); }
            base.Dispose(disposing);
        }

        internal void Restore()
        {
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        internal void Sync()
        {
            syncing = true;
            showHide.Text = capri.IsHidden ? "Show Capri" : "Hide Capri";
            perch.Text = capri.Riding ? "Hop off" : "Ride cursor";
            int near = 0;
            for (int i = 0; i < SizeSteps.Length; i++) if (Math.Abs(SizeSteps[i] - capri.CharacterSize) < Math.Abs(SizeSteps[near] - capri.CharacterSize)) near = i;
            sizeSlider.Value = near;
            sizeName.Text = SizeNames[near];
            checkIns.Checked = capri.CheckIns;
            onTop.Checked = capri.OnTop;
            startup.Checked = capri.StartsWithWindows;
            discord.Checked = capri.OnDiscord;
            cursor.Checked = capri.UsesCursor;
            bypass.Checked = capri.Bypassing;
            watch.Checked = capri.Watching;
            caughtUp.Checked = capri.CaughtUp;
            stayOnModel.Checked = capri.StayOnModel;
            remembering.Checked = capri.Remembers;
            VoiceTier();
            int chosen = Array.IndexOf(Gaps, capri.GlanceGap);
            gap.Value = chosen < 0 ? 5 : chosen;
            gapName.Text = GapNames[gap.Value];
            if (!nickname.Box.Focused) nickname.Box.Text = capri.Nickname;
            string language = capri.Language;
            foreach (ToolStripMenuItem item in languageMenu.Items)
            {
                item.Checked = (string)item.Tag == language;
                if (item.Checked) languages.Text = item.Text;
            }
            languages.Invalidate();
            int loud = 0;
            for (int i = 0; i < Volumes.Length; i++) if (Math.Abs(Volumes[i] - capri.Volume) < Math.Abs(Volumes[loud] - capri.Volume)) loud = i;
            volume.Value = loud;
            volumeName.Text = VolumeNames[loud];
            pitchSlider.Value = capri.VoicePitch + VoiceFx.MostPitch;
            pitchName.Text = PitchNames[pitchSlider.Value];
            int pace = 0;
            for (int i = 0; i < Paces.Length; i++) if (Math.Abs(Paces[i] - capri.VoiceSpeed) < Math.Abs(Paces[pace] - capri.VoiceSpeed)) pace = i;
            paceSlider.Value = pace;
            paceName.Text = PaceNames[pace];
            sounds.Value = Math.Max(0, Math.Min(10, (capri.SoundLevel + 5) / 10));
            soundsName.Text = SoundNames[sounds.Value];
            sleepAfter.Value = Math.Max(0, Array.IndexOf(Sleeps, capri.SleepAfter));
            sleepName.Text = SleepNames[sleepAfter.Value];
            wander.Value = capri.WanderLevel;
            wanderName.Text = WanderNames[wander.Value];
            pops.Value = Math.Max(0, Math.Min(PopNames.Length - 1, capri.PopPace));
            popsName.Text = PopNames[pops.Value];
            dance.Checked = capri.Dancing;
            bubble.Value = capri.BubbleTime;
            bubbleName.Text = BubbleNames[bubble.Value];
            intro.Checked = capri.Intro;
            typing.Checked = capri.TypingSound;
            ShowHdGames();
            miniPlayer.Checked = capri.PlayerOn;
            replies.Value = capri.Wordiness;
            repliesName.Text = Lengths[replies.Value];
            recall.Value = Math.Max(0, Array.IndexOf(Recalls, capri.Recall));
            recallName.Text = Lengths[recall.Value];
            ShowPersonality();
            perchFps.Value = Math.Max(0, Array.IndexOf(Companion.PerchRates, capri.PerchFps));
            ShowPerch();
            syncing = false;
        }

        // Says which personality she's using, and why it's her default when personality.md is missing, empty or can't be read.
        internal void ShowPersonality()
        {
            string note = Brain.PersonalityNote;
            personalityNote.Text = note;
            personalityNote.ForeColor = note == Brain.HerDefault || note == Brain.YourEdit ? Look.Dim : note == Brain.LongNote ? Look.Yellow : Look.Red;
        }

        internal string PersonalityShown { get { return personalityNote.Text; } }

        internal Color PersonalityColour { get { return personalityNote.ForeColor; } }

        // Back from editing personality.md in Notepad, the note catches up.
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            ShowPersonality();
        }

        // Past 30 fps she draws her perch more often, so the HABITS line says it costs more.
        private void ShowPerch()
        {
            perchFpsName.Text = PerchNames[perchFps.Value];
            perchNote.Text = Companion.PerchRates[perchFps.Value] > 30 ? "Perch over 30 fps uses more CPU" : "";
        }

        internal string PerchStatus { get { return perchFpsName.Text + " | " + (perchNote.Text.Length > 0 ? perchNote.Text : "-"); } }

        internal void SlidePerch(int steps) { Press(perchFps, steps); }

        internal string PopsShown { get { return popsName.Text; } }

        internal void SlidePops(int steps) { Press(pops, steps); }

        // The HABITS row and KEYS fit inside the settings page.
        internal bool PerchFits { get { Rectangle page = PageArea; return page.Contains(perchFpsName.Bounds) && page.Contains(perchFps.Bounds) && page.Contains(hideKeys.Bounds) && page.Contains(quietKeys.Bounds); } }

        internal string Status { get { return brainName.Text + " | " + brainNote.Text; } }

        internal bool WatchShown { get { return watch.Checked; } }

        internal bool CaughtUpShown { get { return caughtUp.Checked; } }

        internal void FlipCaughtUp() { caughtUp.Checked = !caughtUp.Checked; }

        internal bool StayShown { get { return stayOnModel.Checked; } }

        internal void FlipStay() { stayOnModel.Checked = !stayOnModel.Checked; }

        internal bool RemembersShown { get { return remembering.Checked; } }

        internal void FlipRemembering() { remembering.Checked = !remembering.Checked; }

        internal bool ElevenShown { get { return voiceRow == 3 && cloudName == Eleven && voiceRungs[3].Checked && !voiceRungs[0].Checked && !voiceRungs[1].Checked && !voiceRungs[2].Checked; } }

        // The VOICE row picked, what its two rows show and how far the rest folds up, for the smoke test.
        internal string VoiceTierStatus
        {
            get
            {
                string[] names = { "OFF", "PREBUILT LOCAL", "CUSTOM LOCAL", "CLOUD VOICE" };
                bool elevenLabs = voiceRow == 3 && cloudName == Eleven;
                return names[voiceRow] + " | " + (voiceRow != 3 ? "-" : elevenLabs ? "key" : cloudName) + " | " + (voiceRow == 1 ? "free voice" : voiceRow == 2 ? "voice model" : elevenLabs && keySaved ? "voice id" : "-") + " | fold " + arrangedFold;
            }
        }

        internal string Overlap()
        {
            for (int i = 0; i < Controls.Count; i++)
                for (int j = i + 1; j < Controls.Count; j++)
                {
                    Control a = Controls[i], b = Controls[j];
                    if (!a.Visible || !b.Visible || (a is Label && a.Text.Length == 0) || (b is Label && b.Text.Length == 0)) continue;
                    if (a.Bounds.IntersectsWith(b.Bounds)) return a.Text + " / " + b.Text;
                }
            return null;
        }

        // A settings control that pokes out past the page's edge, or null.
        internal string Outside()
        {
            Rectangle page = PageArea;
            foreach (KeyValuePair<Control, int> entry in pageLeft)
            {
                Control control = entry.Key;
                if (!control.Visible || (control is Label && control.Text.Length == 0)) continue;
                if (!page.Contains(control.Bounds)) return control.Text.Length > 0 ? control.Text : control.AccessibleName;
            }
            return null;
        }

        // The whole settings page, scrolled or not, in the window's own px.
        private Rectangle PageArea { get { return paged ? new Rectangle(0, -Scaled(pageScroll), ClientSize.Width, Math.Max(ClientSize.Height, Scaled(PageTall))) : ClientRectangle; } }

        // Settings on a short screen: how much shows (or whole) and whether the rims hold her frame together.
        internal string ScrollStatus { get { return (paged && pageView < PageTall ? pageView + " tall" : "whole") + " | " + (topRim.Visible && bottomRim.Visible ? "rims" : "-"); } }

        internal int Scrolled { get { return pageScroll; } }

        internal int ScrollMost { get { return Math.Max(0, PageTall - pageView); } }

        internal int BackTop { get { return more.Top; } }

        internal bool HideKeyShows { get { return ClientRectangle.Contains(hideKeys.Bounds) && ClientRectangle.Contains(quietKeys.Bounds); } }

        internal int Px(int value) { return Scaled(value); }

        // DrawToBitmap paints child controls bottom to top, so a screenshot draws the rims again over what's under them, as the screen does.
        internal void DrawRims(Bitmap panel)
        {
            foreach (Rim rim in new[] { topRim, bottomRim })
            {
                if (!rim.Visible) continue;
                using (Bitmap strip = new Bitmap(rim.Width, rim.Height))
                using (Graphics graphics = Graphics.FromImage(panel))
                {
                    rim.DrawToBitmap(strip, new Rectangle(Point.Empty, rim.Size));
                    graphics.DrawImageUnscaled(strip, rim.Location);
                }
            }
        }

        // Turns the mouse wheel over the perch slider, so it reaches the page the way a real wheel does.
        internal void Wheel(int notches)
        {
            Point at = perchFps.PointToScreen(new Point(perchFps.Width / 2, perchFps.Height / 2));
            for (int i = 0; i < Math.Abs(notches); i++)
                Native.SendMessage(perchFps.Handle, 0x020A, new IntPtr((notches > 0 ? 120 : -120) << 16), new IntPtr((at.Y << 16) | (at.X & 0xFFFF)));
        }

        // Tabs through Settings from Back and names the first control that gets focus without all of it showing, or null.
        internal string TabShows()
        {
            Control from = more;
            bool keys = false;
            for (int i = 0; i < 160; i++)
            {
                if (!SelectNextControl(from, true, true, true, true)) break;
                Control at = ActiveControl;
                if (at == null || at == from || at == more) break;
                Control whole = at;
                while (whole.Parent != this) whole = whole.Parent;
                if (!ClientRectangle.Contains(whole.Bounds)) return "Tab reached " + (whole.AccessibleName ?? whole.Text) + " half off the screen";
                keys = keys || whole == quietKeys;
                from = at;
            }
            more.Focus();
            return keys ? null : "Tab never reached the quiet key";
        }

        internal string VoiceStyleStatus { get { return pitchName.Text + " | " + paceName.Text + " | " + robotName.Text + " | " + glitchName.Text; } }

        // Drags one of her voice's sliders and lets go there, as they would: 0 pitch, 1 speed, 2 glitch, 3 robot.
        internal void SlideVoice(int which, int index) { (which == 0 ? pitchSlider : which == 1 ? paceSlider : which == 2 ? glitchSlider : robotSlider).Slide(index); }

        internal string GapShown { get { return gapName.Text; } }

        internal void SlideGap(int steps) { Press(gap, steps); }

        // Arrow keys on a slider, the way they'd press them.
        private static void Press(Slider slider, int steps) { slider.Focus(); for (int i = 0; i < Math.Abs(steps); i++) { KeyEventArgs press = new KeyEventArgs(steps > 0 ? Keys.Right : Keys.Left); typeof(Control).GetMethod("OnKeyDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(slider, new object[] { press }); } }

        internal void Searching()
        {
            brainName.Text = "Looking for a brain...";
            brainNote.Text = "";
            models.Visible = false;
            hint.Visible = false;
            Arrange(false);
        }

        internal void ShowOllama(string[] installed, string chosen)
        {
            brainName.Text = "Ollama · " + chosen;
            brainNote.Text = "Running on this PC";
            models.Text = chosen;
            models.Visible = true;
            hint.Visible = false;
            lastInstalled = installed;
            modelMenu.Items.Clear();
            foreach (string name in installed)
            {
                string pick = name;
                ToolStripMenuItem item = new ToolStripMenuItem(name, null, delegate { capri.ChooseModel(pick); ShowOllama(lastInstalled, pick); });
                item.Checked = name == chosen;
                modelMenu.Items.Add(item);
            }
            Arrange(false);
        }

        internal void ShowCloud(Cloud remote, List<string> found)
        {
            brainName.Text = remote.Name + " · " + remote.Model;
            brainNote.Text = "Using your " + remote.Company + " key";
            models.Text = remote.Model;
            models.Visible = true;
            hint.Visible = false;
            modelMenu.Items.Clear();
            foreach (string name in found ?? new List<string> { remote.Model })
            {
                string pick = name;
                modelMenu.Items.Add(Choice(Brain.Shown(remote.Kind, name), name == remote.Model, delegate { capri.ChooseModel(pick); ShowCloud(remote, found); }));
            }
            Arrange(false);
        }

        private int KeyRow { get { return tier > 0 ? 128 : 94; } }

        internal void ShowTiers(int pick, string nvidiaTail, string paidTail)
        {
            tier = Math.Max(0, Math.Min(rungs.Length - 1, pick));
            syncing = true;
            for (int i = 0; i < rungs.Length; i++) rungs[i].Checked = i == tier;
            syncing = false;
            string tail = tier == 1 ? nvidiaTail : tier == 2 ? paidTail : null;
            textSaved = tail != null;
            textEntry.Box.AccessibleName = tier == 1 ? "NVIDIA API key" : "OpenAI, Anthropic or DeepSeek API key";
            tierNote.Text = tier == 1 ? "Most NVIDIA models can't see your screen." : "Pick one. Only one brain runs at a time.";
            textEntry.Cue = textSaved ? (tier == 1 ? "NVIDIA key saved, ends " : "Key saved, ends ") + tail : tier == 1 ? "Paste your free NVIDIA key" : "Paste a ChatGPT or Claude key";
            RelabelText();
            Arrange(expanded);
        }

        internal bool TiersFit
        {
            get
            {
                Rectangle last = rungs[rungs.Length - 1].Bounds;
                bool row = !textEntry.Visible || (textEntry.Top >= last.Bottom && textConnect.Right <= ClientSize.Width);
                int end = textEntry.Visible ? textEntry.Bottom : last.Bottom;
                return rungs[0].Top >= tierNote.Bottom && rungs[1].Top >= rungs[0].Bottom && rungs[2].Top >= rungs[1].Bottom && freeKey.Left >= rungs[1].Right && freeKey.Top >= rungs[1].Top && freeKey.Bottom <= rungs[1].Bottom && row && end <= voiceHead.Top && rungs[0].Right <= ClientSize.Width;
            }
        }

        internal string TextStatus { get { return "tier " + tier + " | " + brainName.Text + " | " + brainNote.Text + " | " + (textEntry.Visible ? textEntry.Cue + " | " + textConnect.Text : "no key row"); } }

        private void ConnectText()
        {
            string typed = textEntry.Box.Text.Trim();
            if (typed.Length > 0) { textEntry.Box.Text = ""; capri.ConnectText(typed); }
            else if (textSaved) capri.ForgetText();
            else textEntry.Box.Focus();
        }

        private void RelabelText()
        {
            bool forget = textSaved && textEntry.Box.Text.Trim().Length == 0;
            textConnect.Text = forget ? "Forget" : "Connect";
            textConnect.Accent = forget ? Look.Red : Look.Ink;
            textConnect.Invalidate();
        }

        internal void ShowLocal(Pick pick)
        {
            brainName.Text = "Built-in · " + pick.Name;
            brainNote.Text = "Running on this PC";
            models.Visible = false;
            hint.Visible = false;
            Arrange(false);
        }

        internal void ShowSetup(Machine machine, Pick pick, bool viaOllama, string problem)
        {
            brainName.Text = problem == null ? "No brain yet" : "Setup stopped";
            brainNote.Text = problem ?? "Best fit for this PC: " + pick.Name;
            models.Visible = false;
            Step(0, "✓", machine.Describe(), Look.Ink);
            Step(1, "2", "Download " + pick.Name + " (" + pick.Download + ")" + (viaOllama ? " with Ollama" : ""), Look.Dim);
            Step(2, "3", "Wake her up", Look.Dim);
            filled = 0;
            setup.Text = problem == null ? "Set up her brain" : "Try again";
            setup.Enabled = true;
            hint.Visible = !viaOllama;
            Arrange(true);
        }

        internal void ShowProgress(int step, string text, long done, long total)
        {
            Step(1, step > 1 ? "✓" : "2", stepText[1], step == 1 ? Look.Cyan : Look.Ink);
            Step(2, "3", stepText[2], step == 2 ? Look.Cyan : Look.Dim);
            brainName.Text = step == 1 ? "Setting up her brain" : "Waking her up";
            brainNote.Text = total > 0 ? text + " · " + Engine.Amount(done) + " of " + Engine.Amount(total) : text;
            filled = total > 0 ? Math.Min(1f, done / (float)total) : step == 2 ? 1f : 0f;
            setup.Text = step == 1 ? "Cancel" : "Starting...";
            setup.Enabled = step == 1;
            hint.Visible = false;
            if (!expanded) Arrange(true);
            else Invalidate(new Rectangle(0, Scaled(204), Width, Scaled(18)));
        }

        internal void ShowProblem(string problem) { brainNote.Text = problem; }

        internal void ShowInstall(string text, long done, long total)
        {
            bool opening = voiceFilled < 0;
            installNote.Text = total > 0 ? text + " · " + Engine.Amount(done) + " of " + Engine.Amount(total) : text + "...";
            voiceFilled = total > 0 ? Math.Min(1f, done / (float)total) : 0f;
            if (opening) Arrange(expanded);
            else Invalidate(new Rectangle(0, Scaled(InstallAt + 20 + (expanded ? SetupHeight : 0) + KeyRow - arrangedFold), Width, Scaled(14)));
        }

        internal void HideInstall()
        {
            if (voiceFilled < 0) return;
            voiceFilled = -1;
            installNote.Text = "";
            Arrange(expanded);
        }

        internal string InstallShown { get { return voiceFilled < 0 ? null : installNote.Text + " | " + (int)Math.Round(voiceFilled * 100) + "%"; } }

        internal int CapriRowTop { get { return showHide.Top; } }

        // OFF folds both rows under the VOICE rows away. PREBUILT LOCAL shows her voice picker and CUSTOM LOCAL their model, in the
        // second row. CLOUD VOICE shows its service in the first, with ElevenLabs' key box, then its Voice ID and model once a key is saved.
        private int Fold { get { return off ? 2 * KeyFold : voiceRow == 3 && cloudName == Eleven && keySaved ? 0 : KeyFold; } }

        private void VoiceTier()
        {
            off = capri.Silent;
            eleven = keySaved ? capri.Speaking : wantEleven;
            bool served = !off && !eleven && capri.ServerPicked, inCloud = served && capri.ServerInCloud;
            if (eleven) cloudName = Eleven;
            else if (inCloud) cloudName = capri.ServerCloudName;
            else if (wantCloud != null) cloudName = wantCloud;
            else cloudName = capri.CloudChoice;
            voiceRow = off ? 0 : eleven || inCloud || wantCloud != null ? 3 : served ? 2 : 1;
            for (int i = 0; i < voiceRungs.Length; i++) voiceRungs[i].Checked = i == voiceRow;
            bool cloud = voiceRow == 3, elevenLabs = cloud && cloudName == Eleven;
            cloudPick.Visible = cloud;
            cloudPick.Text = cloudName;
            foreach (ToolStripMenuItem item in cloudMenu.Items) item.Checked = item.Text == cloudName;
            keyEntry.Visible = elevenLabs;
            connect.Visible = elevenLabs;
            cloudVoice.Visible = cloud && !elevenLabs;
            cloudVoice.Text = inCloud ? "Voice, key and model..." : "Set up " + cloudName + "...";
            voiceEntry.Visible = elevenLabs && keySaved;
            speechModels.Visible = elevenLabs && keySaved;
            freeVoices.Visible = voiceRow == 1;
            customVoice.Visible = voiceRow == 2;
            // A model file runs on her free voice's engine, and her free voice fills in for a server that's down.
            freeSetup.Visible = (voiceRow == 1 || voiceRow == 2) && !freeReady;
            customVoice.Width = Scaled(freeReady ? 328 : 196);
            herVoice.Visible = voiceRow == 1 && freeReady && yoursShown;
            cloudPick.Invalidate();
            cloudVoice.Invalidate();
            if (Fold != arrangedFold) Arrange(expanded);
            ShowEffects();
        }

        private void PickRow(int row)
        {
            if (row == 0) PickSilent();
            else if (row == 1) { wantEleven = false; wantCloud = null; capri.UsePrebuilt(); }
            else if (row == 2) PickModel(false, null);
            else PickCloud(cloudName);
        }

        // ElevenLabs keeps its key, Voice ID and model in the Mainframe; Fish Audio and OpenAI open the voice model panel.
        private void PickCloud(string name)
        {
            cloudName = name;
            if (name == Eleven) PickVoice(true);
            else PickModel(true, CloudApi(name));
        }

        // One they set up before comes back at once. Otherwise the voice model panel opens with this row's choices, and a cloud
        // service stays picked under CLOUD VOICE, so its menu can still switch to ElevenLabs if they close the panel.
        private void PickModel(bool cloud, string api)
        {
            if (capri.HasServer(cloud, api))
            {
                wantEleven = false;
                wantCloud = null;
                capri.UseSavedServer(cloud);
            }
            else
            {
                wantCloud = cloud ? cloudName : null;
                OpenServer(cloud, api);
                VoiceTier();
            }
        }

        private static string CloudApi(string name) { return name == Clouds[0] ? VoiceServer.Fish : name == Clouds[2] ? VoiceServer.OpenAi : null; }

        // ElevenLabs' setup guide, or the voice model panel, whose note says where Fish Audio's and OpenAI's keys come from.
        private void CloudGuide()
        {
            if (cloudName == Eleven) capri.OpenKeyPage();
            else OpenServer(true, CloudApi(cloudName));
        }

        // Her HD voice and their voice server keep their own robot and glitch, so those sliders show the ones for the voice she speaks with.
        private void ShowEffects()
        {
            bool was = syncing;
            syncing = true;
            robotSlider.Value = Nearest(capri.VoiceRobot);
            robotName.Text = AmountName(robotSlider.Value);
            glitchSlider.Value = Nearest(capri.VoiceGlitch);
            glitchName.Text = AmountName(glitchSlider.Value);
            syncing = was;
        }

        private static int Nearest(int amount)
        {
            int near = 0;
            for (int i = 0; i < Amounts.Length; i++) if (Math.Abs(Amounts[i] - amount) < Math.Abs(Amounts[near] - amount)) near = i;
            return near;
        }

        private string AmountName(int step) { return AmountNames[step] + (capri.SpeaksHd ? " · HD" : capri.SpeaksServer ? " · server" : ""); }

        internal void PickVoice(bool paid)
        {
            wantEleven = paid;
            wantCloud = null;
            capri.ChooseVoiceTier(paid);
            if (paid && !keySaved) keyEntry.Box.Focus();
        }

        internal void PickSilent()
        {
            wantCloud = null;
            capri.Silence();
        }

        internal void ShowVoice(string tail, string voiceId, string model, VoiceListing listing)
        {
            keySaved = tail != null;
            // Short, to fit beside CLOUD VOICE's menu, which already says ElevenLabs.
            keyEntry.Cue = keySaved ? "Saved, ends " + tail : "Paste API key";
            Relabel();
            VoiceTier();
            if (!voiceEntry.Box.Focused || voiceEntry.Box.Text.Trim().Length == 0) voiceEntry.Box.Text = voiceId;
            voiceMenu.Items.Clear();
            voiceEntry.Chevron = listing != null && listing.Voices.Count > 0;
            speechModelMenu.Items.Clear();
            speechModelMenu.Items.Add(Choice("Default model", string.IsNullOrEmpty(model), delegate { capri.ChooseVoiceModel(null); }));
            speechModels.Text = string.IsNullOrEmpty(model) ? "Default model" : model;
            if (listing != null)
            {
                foreach (VoiceEntry entry in listing.Voices)
                {
                    string id = entry.Id;
                    voiceMenu.Items.Add(Choice(entry.Name ?? id, id == voiceId, delegate { voiceEntry.Box.Text = id; capri.ChooseVoice(id); }));
                }
                foreach (VoiceModel option in listing.Models)
                {
                    string id = option.Id;
                    string name = option.Name ?? id;
                    if (id == model) speechModels.Text = name.StartsWith("Eleven ", StringComparison.OrdinalIgnoreCase) ? name.Substring(7) : name;
                    speechModelMenu.Items.Add(Choice(name, id == model, delegate { capri.ChooseVoiceModel(id); }));
                }
            }
            speechModels.Invalidate();
        }

        // `yours`: their clip is what her HD voice clones. `kept`: one is saved but she's on her own. `served`: the name of the voice
        // server she speaks through, like Fish Audio, when it's picked.
        internal void ShowFree(bool ready, string chosen, bool busy, bool hdFits, bool yours, bool kept, string served = null)
        {
            bool hd = chosen == QwenVoice.Style;
            ShowHdGames();
            freeReady = ready;
            yoursShown = hd && yours;
            freeVoices.Text = "Free voice: " + (yoursShown ? "Your voice" : served ?? chosen);
            customVoice.Text = served != null ? "Voice model: " + served : "Pick a voice model...";
            freeSetup.Text = busy ? "Cancel" : "Get free voice";
            freeSetup.Visible = (voiceRow == 1 || voiceRow == 2) && !ready;
            herVoice.Visible = voiceRow == 1 && ready && yoursShown;
            freeVoices.Width = Scaled(ready && !yoursShown ? 328 : 196);
            customVoice.Width = Scaled(ready ? 328 : 196);
            freeMenu.Items.Clear();
            foreach (FreeStyle style in LocalVoice.Styles)
            {
                string name = style.Name;
                freeMenu.Items.Add(Choice(name, name == chosen, delegate { capri.ChooseVoiceStyle(name); }));
            }
            freeMenu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem own = Choice(!hdFits ? "HD · needs a 6 GB graphics card" : yours || kept ? "HD · her own voice" :"HD · graphics card, " + Engine.Amount(QwenVoice.PackSize), hd && !yours, delegate { if (yours) capri.UseHerVoice(); else capri.ChooseVoiceStyle(QwenVoice.Style); });
            own.Enabled = hdFits;
            freeMenu.Items.Add(own);
            // Only her HD voice clones from a clip; her other styles are set voices.
            if (yours || kept)
            {
                ToolStripMenuItem theirs = Choice("Your voice (HD)", yoursShown, delegate { if (yours) capri.ChooseVoiceStyle(QwenVoice.Style); else capri.UseKeptVoice(); });
                theirs.Enabled = hdFits;
                freeMenu.Items.Add(theirs);
            }
            ToolStripMenuItem pick = Choice(yours || kept ? "Pick a new clip of you..." : "Your voice (HD) · pick a clip of you...", false, delegate { PickYourVoice(); });
            pick.Enabled = hdFits;
            freeMenu.Items.Add(pick);
            freeVoices.Invalidate();
            customVoice.Invalidate();
            freeSetup.Invalidate();
            herVoice.Invalidate();
            ShowEffects();
        }

        // 5 to 30 seconds of them talking, as a WAV, MP3 or M4A.
        private void PickYourVoice()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Pick 5 to 30 seconds of you talking";
                dialog.Filter = "Voice clips (*.wav;*.mp3;*.m4a)|*.wav;*.mp3;*.m4a|All files (*.*)|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK) capri.UseYourVoice(dialog.FileName);
            }
        }

        internal string YourVoiceStatus
        {
            get
            {
                List<string> items = new List<string>();
                foreach (ToolStripItem item in freeMenu.Items)
                {
                    ToolStripMenuItem choice = item as ToolStripMenuItem;
                    if (choice != null && (choice.Text.StartsWith("HD ", StringComparison.Ordinal) || choice.Text.IndexOf("you", StringComparison.OrdinalIgnoreCase) >= 0)) items.Add(choice.Text + (choice.Checked ? " (picked)" : ""));
                }
                return freeVoices.Text + " | " + (herVoice.Visible ? herVoice.Text : "-") + " | " + string.Join(", ", items.ToArray());
            }
        }

        internal void ClickHerVoice() { typeof(Control).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(herVoice, new object[] { EventArgs.Empty }); }

        // Clicks the free voice menu's item that starts with this text, as they would. False if it isn't there or can't be clicked.
        internal bool PickFree(string text)
        {
            foreach (ToolStripItem item in freeMenu.Items)
                if (item is ToolStripMenuItem && item.Enabled && item.Text.StartsWith(text, StringComparison.Ordinal)) { item.PerformClick(); return true; }
            return false;
        }

        internal string FreeStatus { get { return (freeVoices.Visible ? freeVoices.Text : "-") + " | " + (freeSetup.Visible ? freeSetup.Text : "-"); } }

        internal string HdChoice
        {
            get
            {
                foreach (ToolStripItem item in freeMenu.Items)
                {
                    ToolStripMenuItem choice = item as ToolStripMenuItem;
                    if (choice != null && choice.Text.StartsWith(QwenVoice.Style + " ", StringComparison.Ordinal)) return choice.Text + (choice.Enabled ? "" : " (off)") + (choice.Checked ? " (picked)" : "");
                }
                return "-";
            }
        }

        // CUSTOM LOCAL's voice model, or CLOUD VOICE's Fish Audio or OpenAI: its address, model, voice and key, in a panel of its own
        // over the Mainframe with only that row's choices.
        private void OpenServer(bool cloud, string api)
        {
            if (sheet == null || sheet.IsDisposed) sheet = new ServerSheet(this);
            sheet.Fill(cloud, api);
            if (!sheet.Visible) sheet.Show(this);
            sheet.Activate();
        }

        // The VOICE rows and CLOUD VOICE's service menu as they'd click them, and CUSTOM LOCAL's Set up, for the smoke test.
        internal void PickVoiceRow(int row) { PickRow(row); }

        internal void PickCloudVoice(string name) { PickCloud(name); }

        internal void SetUpCustom() { OpenServer(false, null); }

        internal void SetUpCloud() { OpenServer(true, CloudApi(cloudName)); }

        internal string ServerChoices { get { return sheet.Choices; } }

        internal string CloudPicked { get { return cloudName; } }

        internal string ModelChoices
        {
            get
            {
                List<string> names = new List<string>();
                foreach (ToolStripMenuItem item in modelMenu.Items) names.Add(item.Text + (item.Checked ? " (picked)" : ""));
                return string.Join(", ", names);
            }
        }

        internal string CloudMenu
        {
            get
            {
                List<string> names = new List<string>();
                foreach (ToolStripItem item in cloudMenu.Items) names.Add(item.Text);
                return string.Join(", ", names);
            }
        }

        internal string CloudShown { get { return (cloudPick.Visible ? cloudPick.Text : "-") + " | " + (cloudVoice.Visible ? cloudVoice.Text : "-") + " | " + (customVoice.Visible ? customVoice.Text : "-"); } }

        // Their voice server's panel for the smoke test: what it shows, typing into it, its choices and buttons, and a picture of it.
        internal string ServerShown { get { return sheet == null || sheet.IsDisposed || !sheet.Visible ? "-" : sheet.Showing; } }

        internal void TypeServer(string address, string name, string voice, string secret) { sheet.Type(address, name, voice, secret); }

        internal void PickService(string name) { sheet.Choose(name); }

        internal void TakeModel(string path) { sheet.Take(path); }

        internal string ServerFields { get { return sheet.Fields; } }

        internal void DropFishVoices() { sheet.DropVoices(); }

        internal string FishVoicesShown { get { return sheet.Voices; } }

        internal void PickFishVoice(string name) { sheet.PickVoice(name); }

        internal void MakeFishVoice(string clip) { sheet.MakeVoice(clip); }

        internal void TakeVoiceClip(string clip) { sheet.TakeClip(clip); }

        internal void PressServer(string button) { sheet.Press(button); }

        internal Form ServerPanel { get { return sheet; } }

        // The free voice menu as it drops down under its button, and where, for the smoke test's picture of it.
        internal Bitmap FreeMenuPicture()
        {
            freeMenu.Show(freeVoices, new Point(0, freeVoices.Height));
            try
            {
                Bitmap picture = new Bitmap(freeMenu.Width, freeMenu.Height);
                freeMenu.DrawToBitmap(picture, new Rectangle(Point.Empty, freeMenu.Size));
                return picture;
            }
            finally { freeMenu.Close(); }
        }

        internal Point FreeMenuAt { get { return new Point(freeVoices.Left, freeVoices.Bottom); } }

        internal string FreeMenuShown
        {
            get
            {
                List<string> items = new List<string>();
                foreach (ToolStripItem item in freeMenu.Items)
                {
                    ToolStripMenuItem choice = item as ToolStripMenuItem;
                    items.Add(choice == null ? "-" : choice.Text + (choice.Enabled ? "" : " (off)") + (choice.Checked ? " (picked)" : ""));
                }
                return string.Join(", ", items.ToArray());
            }
        }

        internal void ShowShortcut(bool talk, bool mute)
        {
            shortcutReady = talk;
            muteReady = mute;
            KeyLabels();
        }

        internal void ShowKeys(bool type, bool hide, bool quiet)
        {
            typeReady = type;
            hideReady = hide;
            quietReady = quiet;
            KeyLabels();
        }

        internal void Hotkeys(string talk, string mute, string type, string hide, string quiet)
        {
            talkName = talk;
            muteName = mute;
            typeName = type;
            hideName = hide;
            quietName = quiet;
            KeyLabels();
        }

        // Which of her keys to change: 0 talk, 1 mute mic, 2 type to her, 3 hide or show her, 4 quiet.
        // Opening another key while one is being picked drops what was pressed, so the old key stays.
        internal void PickKeys(int slot)
        {
            capturing = slot + 1;
            pendingMods = -1;
            heldMods = 0;
            if (keysNote != null) keysNote.Text = "";
            KeyLabels();
        }

        internal void TakeKeys(Keys keys) { TakeKeys(keys, false); }

        // Every press shows in her key's button until Confirm or Cancel is clicked. The latest key with what was held wins.
        internal void TakeKeys(Keys keys, bool win)
        {
            if (capturing == 0) return;
            Keys key = keys & Keys.KeyCode;
            int mods = Companion.HotMods(keys, win || key == Keys.LWin || key == Keys.RWin);
            if (key == Keys.ControlKey || key == Keys.LControlKey || key == Keys.RControlKey || key == Keys.Menu || key == Keys.LMenu || key == Keys.RMenu || key == Keys.ShiftKey || key == Keys.LShiftKey || key == Keys.RShiftKey || key == Keys.LWin || key == Keys.RWin)
            {
                // A modifier on its own only shows until a key joins it, and never counts as her key.
                if (pendingMods < 0) { heldMods = mods; KeyLabels(); }
                return;
            }
            if (!Companion.Bindable(mods, (int)key)) return;
            pendingMods = mods;
            pendingVk = (int)key;
            KeyLabels();
            string name = Companion.KeyName(pendingMods, pendingVk);
            KeysNote(Companion.Swallows(pendingMods, pendingVk) ? name + " won't type in other apps while she runs. Confirm to keep it."
                : Companion.ByMouse(pendingVk) ? name + " still does its own job in other apps too. Confirm to keep it." : "Confirm to keep " + name + ", or Cancel.", false);
        }

        internal void ConfirmKeys()
        {
            if (capturing == 0 || pendingMods < 0) return;
            int slot = capturing - 1, mods = pendingMods, vk = pendingVk;
            string problem = capri.SetHotkey(slot, mods, vk);
            // A key that's taken or already hers keeps the picking open, so another can be pressed straight away.
            if (problem != null) { KeysNote(problem, true); return; }
            capturing = 0;
            pendingMods = -1;
            KeyLabels();
            KeysNote("Saved. " + (slot == 0 ? talkName + " starts voice chat." : slot == 1 ? muteName + " mutes your mic." : slot == 2 ? typeName + " opens her typing box." : slot == 3 ? hideName + " hides or shows her." : quietName + " keeps her quiet, and lets her talk again.") + (Companion.Swallows(mods, vk) ? " It won't type in other apps while she runs." : Companion.ByMouse(vk) ? " It still does its own job in other apps too." : ""), false);
        }

        internal void CancelKeys()
        {
            if (capturing == 0) return;
            capturing = 0;
            pendingMods = -1;
            if (keysNote != null) keysNote.Text = "";
            KeyLabels();
        }

        internal bool ConfirmReady { get { return confirmKeys.Visible && confirmKeys.Enabled; } }

        private Control PickedKey { get { return capturing == 1 ? talkKeys : capturing == 2 ? muteKeys : capturing == 3 ? typeKeys : capturing == 4 ? hideKeys : capturing == 5 ? quietKeys : null; } }

        private static bool WinHeld() { return (Native.GetKeyState(0x5B) & 0x8000) != 0 || (Native.GetKeyState(0x5C) & 0x8000) != 0; }

        // A key change answers where it was made: under the KEYS row in Settings, or under VOICE on the main page.
        private void KeysNote(string text, bool problem)
        {
            if (!paged) { VoiceNote(text, problem); return; }
            keysNote.Text = text;
            keysNote.ForeColor = problem ? Look.Red : Look.Dim;
        }

        internal string KeysNoteText { get { return keysNote.Text; } }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (capturing == 0) return base.ProcessCmdKey(ref msg, keyData);
            TakeKeys(keyData, WinHeld());
            return true;
        }

        internal bool Picking { get { return capturing != 0; } }

        // While a key is being picked, middle click or a side mouse button anywhere on the Mainframe is picked like a key press.
        public bool PreFilterMessage(ref Message m)
        {
            if (capturing == 0 || (m.Msg != 0x0207 && m.Msg != 0x0209 && m.Msg != 0x020B && m.Msg != 0x020D)) return false;
            Control over = Control.FromChildHandle(m.HWnd);
            if (over == null || over.FindForm() != this) return false;
            Keys button = m.Msg == 0x0207 || m.Msg == 0x0209 ? Keys.MButton : ((m.WParam.ToInt64() >> 16) & 0xFFFF) == 2 ? Keys.XButton2 : Keys.XButton1;
            TakeKeys(button | ModifierKeys, WinHeld());
            return true;
        }

        private const string Waiting = "Press new keys, then Confirm";

        private void KeyLabels()
        {
            shortcut.Text = capturing == 1 ? Waiting : talkName + (shortcutReady ? " to talk" : " is taken");
            muteShortcut.Text = capturing == 2 ? Waiting : muteName + (muteReady ? " mutes mic" : " is taken");
            if (cancelKeys == null) return;
            talkKeys.Text = capturing == 1 ? PickingText("Talk: ") : "Talk: " + talkName + (shortcutReady ? "" : " (taken)");
            muteKeys.Text = capturing == 2 ? PickingText("Mute mic: ") : "Mute mic: " + muteName + (muteReady ? "" : " (taken)");
            typeKeys.Text = capturing == 3 ? PickingText("Type: ") : "Type: " + typeName + (typeReady ? "" : " (taken)");
            hideKeys.Text = capturing == 4 ? PickingText("Hide/show: ") : "Hide/show: " + hideName + (hideReady ? "" : " (taken)");
            quietKeys.Text = capturing == 5 ? PickingText("Quiet: ") : "Quiet: " + quietName + (quietReady ? "" : " (taken)");
            confirmKeys.Visible = cancelKeys.Visible = capturing != 0;
            confirmKeys.Enabled = pendingMods >= 0;
            confirmKeys.Invalidate();
        }

        // What the key being picked shows: the keys pressed so far, the modifiers held so far, or what to do.
        private string PickingText(string job)
        {
            if (pendingMods >= 0) return job + Companion.KeyName(pendingMods, pendingVk);
            if (heldMods != 0) return job + Companion.ModNames(heldMods) + "...";
            return Waiting;
        }

        internal string TierNote { get { return tierNote.Text; } }
        internal string KeyStatus { get { return shortcut.Text + " | " + muteShortcut.Text; } }
        internal string KeysShown { get { return talkKeys.Text + " | " + muteKeys.Text + " | " + typeKeys.Text + " | " + hideKeys.Text; } }
        internal string QuietKeyShown { get { return quietKeys.Text; } }

        internal void ShowMic(string name)
        {
            mics.Text = "Mic: " + (name ?? "default");
            mics.Invalidate();
        }

        private void ShowMics()
        {
            string chosen = capri.MicId;
            List<Mic> found = Ears.List();
            micMenu.Items.Clear();
            micMenu.Items.Add(Choice("Windows default", string.IsNullOrEmpty(chosen), delegate { capri.ChooseMic(null); }));
            if (found.Count > 0) micMenu.Items.Add(new ToolStripSeparator());
            foreach (Mic mic in found)
            {
                string id = mic.Id;
                micMenu.Items.Add(Choice(mic.Name, id == chosen, delegate { capri.ChooseMic(id); }));
            }
            micMenu.Show(mics, new Point(0, mics.Height));
        }

        internal string MicStatus { get { return "mic | " + mics.Text + " | " + (keyHelp.Visible ? "help" : "-") + " | " + (shortcut.Visible ? (shortcutReady ? "keys" : "taken") : "-") + " | " + (muteShortcut.Visible ? (muteReady ? "mute" : "taken") : "-"); } }

        // Windows' Online speech recognition switch is hard to find (Baguettboy couldn't), so a note that asks for it opens its page.
        internal void VoiceNote(string text, bool problem)
        {
            speechFix = text != null && text.IndexOf("Online speech recognition", StringComparison.Ordinal) >= 0;
            voiceNote.Text = speechFix ? text + OpenSpeech : text;
            voiceNote.ForeColor = problem ? Look.Red : Look.Dim;
            voiceNote.Cursor = speechFix ? Cursors.Hand : Cursors.Default;
        }

        internal const string OpenSpeech = " Click to open it.";
        internal bool SpeechFixShown { get { return speechFix && voiceNote.Cursor == Cursors.Hand && voiceNote.Text.EndsWith(OpenSpeech, StringComparison.Ordinal); } }

        internal string VoiceNoteText { get { return voiceNote.Text; } }

        internal void UpdateNote(string text, bool problem)
        {
            updateNote.Text = text;
            updateNote.ForeColor = problem ? Look.Red : Look.Dim;
        }

        internal string UpdateStatus { get { return updateNote.Text; } }

        internal string VoiceStatus { get { return voiceNote.Text + " | " + voiceEntry.Box.Text + " | " + speechModels.Text + " | " + connect.Text; } }
        private void Connect()
        {
            string typed = keyEntry.Box.Text.Trim();
            if (typed.Length > 0) { keyEntry.Box.Text = ""; capri.ConnectVoice(typed); }
            else if (keySaved) capri.ForgetVoice();
            else keyEntry.Box.Focus();
        }

        private void Relabel()
        {
            bool forget = keySaved && keyEntry.Box.Text.Trim().Length == 0;
            connect.Text = forget ? "Forget" : "Connect";
            connect.Accent = forget ? Look.Red : Look.Ink;
            connect.Invalidate();
        }

        private static ToolStripMenuItem Choice(string text, bool chosen, EventHandler click)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text, null, click);
            item.Checked = chosen;
            return item;
        }

        private ContextMenuStrip Dropdown()
        {
            ContextMenuStrip strip = new ContextMenuStrip();
            strip.Renderer = new ToolStripProfessionalRenderer(new CapriMenuColors());
            strip.BackColor = Look.Panel;
            strip.ForeColor = Look.Ink;
            strip.Font = Font;
            strip.ShowImageMargin = false;
            strip.ShowCheckMargin = true;
            return strip;
        }

        private Entry Input(int x, int y, int width)
        {
            Entry entry = new Entry();
            entry.Bounds = new Rectangle(x, y, width, 28);
            Controls.Add(entry);
            return entry;
        }

        private void Step(int index, string mark, string text, Color colour)
        {
            stepText[index] = text;
            steps[index].Text = mark + "  " + text;
            steps[index].ForeColor = colour;
        }

        private Label Caption(string text, int x, int y, Font font, Color colour)
        {
            Label label = new Note();
            label.Text = text;
            label.Font = font;
            label.ForeColor = colour;
            label.BackColor = Look.Panel;
            label.AutoSize = true;
            label.Location = new Point(x, y);
            Controls.Add(label);
            return label;
        }

        private Key Make(string text, int x, int y, int width, int height, EventHandler click)
        {
            Key key = new Key();
            key.Text = text;
            key.Bounds = new Rectangle(x, y, width, height);
            key.Click += click;
            Controls.Add(key);
            return key;
        }

        private Toggle Check(string text, int x, int y, int width, EventHandler changed)
        {
            Toggle box = new Toggle();
            box.Text = text;
            box.Bounds = new Rectangle(x, y, width, 22);
            box.CheckedChanged += changed;
            Controls.Add(box);
            return box;
        }

        private sealed class Note : Label
        {
            internal Note() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); }

            protected override void OnPaintBackground(PaintEventArgs e) { Backdrop(e.Graphics, this, ScaleOf(this)); }

            protected override void WndProc(ref Message message)
            {
                if (message.Msg == 0x0084) { message.Result = new IntPtr(-1); return; }
                base.WndProc(ref message);
            }
        }

        // A strip along her top or bottom edge that keeps her frame whole over settings scrolled under it.
        private sealed class Rim : Label
        {
            internal Rim()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
                AutoSize = false;
                BackColor = Look.Panel;
                Visible = false;
            }

            protected override void OnPaintBackground(PaintEventArgs e) { Backdrop(e.Graphics, this, ((Mainframe)Parent).DeviceScale()); }

            protected override void OnPaint(PaintEventArgs e)
            {
                Mainframe owner = (Mainframe)Parent;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.TranslateTransform(-Left, -Top);
                owner.Frame(e.Graphics, owner.DeviceScale());
            }

            protected override void WndProc(ref Message message)
            {
                if (message.Msg == 0x0084) { message.Result = new IntPtr(-1); return; }
                base.WndProc(ref message);
            }
        }

        private sealed class Key : Button
        {
            internal Color Accent = Look.Ink;
            internal bool Loud;
            internal bool Picked;
            internal bool Chevron;
            internal int Glyph;
            private bool hot;
            private bool down;

            internal Key()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
                BackColor = Look.Panel;
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }

            protected override void OnMouseLeave(EventArgs e) { hot = false; down = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }

            protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                float s = ScaleOf(this);
                Graphics graphics = e.Graphics;
                Backdrop(graphics, this, s);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                bool lit = hot || Picked || (Focused && ShowFocusCues);
                Color hotColour = Accent == Look.Ink ? Look.Cyan : Accent;
                if (Glyph == 0 || hot)
                {
                    using (GraphicsPath plate = Look.Plate(new RectangleF(0.7f * s, 0.7f * s, Width - 1.4f * s - 1, Height - 1.4f * s - 1), 6 * s, -1, Height / 2f, 0, true))
                    {
                        using (Brush fill = new SolidBrush(down ? Pressed : hot ? Hover : Field)) graphics.FillPath(fill, plate);
                        if (Glyph == 0)
                        {
                            Color edge = !Enabled ? Field : lit ? hotColour : Loud ? Color.FromArgb(170, Accent) : Edge;
                            using (Pen pen = new Pen(edge, 1.4f * s)) graphics.DrawPath(pen, plate);
                        }
                    }
                }
                Color ink = !Enabled ? Look.Dim : Picked ? Look.Cyan : Accent;
                float x = Width / 2f;
                float y = Height / 2f;
                if (Glyph != 0)
                {
                    using (Pen pen = new Pen(hot ? (Glyph == 2 ? Look.Red : Look.Cyan) : Look.Dim, 1.5f * s))
                    {
                        if (Glyph == 1) graphics.DrawLine(pen, x - 5 * s, y, x + 5 * s, y);
                        else
                        {
                            graphics.DrawLine(pen, x - 4.5f * s, y - 4.5f * s, x + 4.5f * s, y + 4.5f * s);
                            graphics.DrawLine(pen, x - 4.5f * s, y + 4.5f * s, x + 4.5f * s, y - 4.5f * s);
                        }
                    }
                    return;
                }
                if (Chevron)
                {
                    float left = Width - 18 * s;
                    using (Brush mark = new SolidBrush(Look.Dim)) graphics.FillPolygon(mark, new[] { new PointF(left, y - 2 * s), new PointF(left + 8 * s, y - 2 * s), new PointF(left + 4 * s, y + 3 * s) });
                }
                Rectangle area = Chevron ? new Rectangle((int)(10 * s), 0, Width - (int)(30 * s), Height) : ClientRectangle;
                TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | (Chevron ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter);
                TextRenderer.DrawText(graphics, Text, Font, area, ink, flags);
            }
        }

        private sealed class Entry : Control
        {
            internal readonly TextBox Box = new TextBox();
            internal event EventHandler Drop;
            private bool chevron;
            private string cue = "";
            private bool hot;

            internal Entry()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = Look.Panel;
                Cursor = Cursors.IBeam;
                Box.BorderStyle = BorderStyle.None;
                Box.BackColor = Field;
                Box.ForeColor = Look.Ink;
                Box.GotFocus += delegate { Invalidate(); };
                Box.LostFocus += delegate { Invalidate(); };
                Box.HandleCreated += delegate { ShowCue(); };
                Controls.Add(Box);
            }

            internal string Cue { get { return cue; } set { cue = value ?? ""; ShowCue(); } }

            internal bool Chevron { get { return chevron; } set { chevron = value; PerformLayout(); Invalidate(); } }

            private float Unit() { return ScaleOf(this); }

            private bool OnChevron(int x) { return Chevron && x >= Width - 28 * Unit(); }

            private void ShowCue() { if (Box.IsHandleCreated) Native.SendMessage(Box.Handle, 0x1501, new IntPtr(1), cue); }

            protected override void OnLayout(LayoutEventArgs e)
            {
                base.OnLayout(e);
                float s = Unit();
                int left = (int)(10 * s);
                int right = (int)((Chevron ? 28 : 10) * s);
                int height = Box.PreferredHeight;
                Box.SetBounds(left, (Height - height) / 2, Math.Max(1, Width - left - right), height);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                bool over = OnChevron(e.X);
                Cursor = over ? Cursors.Hand : Cursors.IBeam;
                if (over != hot) { hot = over; Invalidate(); }
                base.OnMouseMove(e);
            }

            protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    if (!OnChevron(e.X)) Box.Focus();
                    else if (Drop != null) Drop(this, EventArgs.Empty);
                }
                base.OnMouseDown(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                float s = Unit();
                Graphics graphics = e.Graphics;
                Backdrop(graphics, this, s);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath plate = Look.Plate(new RectangleF(0.7f * s, 0.7f * s, Width - 1.4f * s - 1, Height - 1.4f * s - 1), 6 * s, -1, Height / 2f, 0, true))
                {
                    using (Brush fill = new SolidBrush(Field)) graphics.FillPath(fill, plate);
                    using (Pen pen = new Pen(Box.Focused ? Look.Cyan : Edge, 1.4f * s)) graphics.DrawPath(pen, plate);
                }
                if (!Chevron) return;
                float left = Width - 18 * s;
                float y = Height / 2f;
                using (Brush mark = new SolidBrush(hot ? Look.Cyan : Look.Dim)) graphics.FillPolygon(mark, new[] { new PointF(left, y - 2 * s), new PointF(left + 8 * s, y - 2 * s), new PointF(left + 4 * s, y + 3 * s) });
            }
        }

        // A voice model for CUSTOM LOCAL or CLOUD VOICE: a ready-made choice like Fish Audio, or their own server. Its address, an
        // optional model, voice and key, Test, and Use this server.
        private sealed class ServerSheet : Form
        {
            private readonly Mainframe owner;
            private readonly Label title;
            private readonly Label hint;
            private readonly Key service;
            private readonly ContextMenuStrip services;
            private readonly ContextMenuStrip voices;
            private readonly Label place;
            private readonly Entry url;
            private readonly Label modelName;
            private readonly Entry model;
            private readonly Label voiceName;
            private readonly Entry speaker;
            private readonly Label keyName;
            private readonly Entry key;
            private readonly Key forget;
            private readonly Key browse;
            private readonly Key test;
            private readonly Label note;
            private VoiceServer.Service picked = VoiceServer.Services[0];
            // The server their saved key belongs to, and its last characters. The key only shows as kept for that server.
            private string savedUrl;
            private string savedApi;
            private string savedTail;
            private bool addressShown = true;

            internal ServerSheet(Mainframe owner)
            {
                this.owner = owner;
                Text = VoiceServer.Menu;
                Icon = owner.Icon;
                ShowInTaskbar = false;
                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual;
                AutoScaleMode = AutoScaleMode.Dpi;
                AutoScaleDimensions = new SizeF(96, 96);
                Font = owner.Font;
                BackColor = Look.Panel;
                ForeColor = Look.Ink;
                DoubleBuffered = true;
                ClientSize = new Size(Wide, 420);
                title = Caption("", 28, 22, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow);
                Key shut = Make("", 330, 20, 26, 22, delegate { Close(); });
                shut.Glyph = 2;
                shut.AccessibleName = "Close";
                hint = Caption("", 28, 48, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim);
                Caption("Voice from", 28, 112, Font, Look.Dim);
                services = owner.Dropdown();
                service = Make(picked.Name, 28, 132, 328, 28, delegate { services.Show(service, new Point(0, service.Height)); });
                service.Chevron = true;
                service.AccessibleName = "Voice from";
                place = Caption("Address", 28, 168, Font, Look.Dim);
                url = Input(28, 188, 328, VoiceServer.Address, "Voice server address, or a voice model's folder");
                url.Box.TextChanged += delegate { Kept(); };
                modelName = Caption("Model", 28, 224, Font, Look.Dim);
                model = Input(28, 244, 160, "Optional", "Model name, optional");
                voiceName = Caption("Voice", 196, 224, Font, Look.Dim);
                speaker = Input(196, 244, 160, "Optional", "Voice name or ID, optional");
                // Fish Audio's cloud lists their own voices under the Voice box's arrow. A model file's arrow picks a clip for Pocket TTS.
                voices = owner.Dropdown();
                speaker.Drop += delegate { if (picked.Api == VoiceServer.Files) PickClip(); else ListVoices(); };
                keyName = Caption("Key", 28, 280, Font, Look.Dim);
                key = Input(28, 300, 244, "Optional", "Key. It goes only to this server, as a Bearer key");
                key.Box.UseSystemPasswordChar = true;
                forget = Make("Forget", 280, 300, 76, 28, delegate { owner.capri.ForgetServerKey(savedUrl, savedApi); savedTail = null; Kept(); Say("Key forgotten.", false); });
                forget.Accent = Look.Red;
                // A model file has no key; Browse takes its place.
                browse = Make("Browse...", 28, 300, 150, 28, delegate { Browse(); });
                browse.Visible = false;
                test = Make("Test", 28, 344, 84, 28, delegate { Try(); });
                Key use = Make("Use this server", 120, 344, 150, 28, delegate { Use(); });
                use.Accent = Look.Cyan;
                use.Loud = true;
                AcceptButton = use;
                CancelButton = Make("Cancel", 278, 344, 78, 28, delegate { Close(); });
                note = Caption("", 28, 384, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim);
            }

            // CUSTOM LOCAL's choices or CLOUD VOICE's, with what they set up for that row, or else the service asked for ready to go.
            // A saved key shows only its last four characters.
            internal void Fill(bool cloud, string wanted)
            {
                string api, address, name, voice, tail;
                bool saved = owner.capri.SavedServer(cloud, out api, out address, out name, out voice, out tail);
                Text = cloud ? "Cloud voice" : "Custom local voice";
                title.Text = (cloud ? "CLOUD VOICE" : "CUSTOM LOCAL") + " >";
                ShowAddress(!cloud);
                for (int i = services.Items.Count - 1; i >= 0; i--) services.Items[i].Dispose();
                foreach (VoiceServer.Service choice in VoiceServer.Services)
                {
                    VoiceServer.Service each = choice;
                    if (Cloudy(each) == cloud) services.Items.Add(Choice(each.Name, false, delegate { Pick(each, true); }));
                }
                savedUrl = saved ? address : null;
                savedApi = api;
                savedTail = tail;
                key.Box.Text = "";
                if (saved && (wanted == null || api == wanted))
                {
                    url.Box.Text = address ?? "";
                    model.Box.Text = name ?? "";
                    speaker.Box.Text = voice ?? "";
                    Pick(VoiceServer.Find(api, address), false);
                }
                else Pick(First(cloud, wanted), true);
                Say("", false);
            }

            private static bool Cloudy(VoiceServer.Service choice) { return choice.Url != null && VoiceServer.InCloud(choice.Url, choice.Api); }

            // Fish Audio's and OpenAI's own servers never change address, so CLOUD VOICE hides the Address row and moves the rest
            // up into its place (Lachy, 29 Oct). The address is still filled in underneath. The row is 56 px at 96 DPI, scaled
            // like the boxes in it.
            private void ShowAddress(bool shown)
            {
                if (shown == addressShown) return;
                addressShown = shown;
                int step = (int)Math.Round(56f * url.Height / 28) * (shown ? 1 : -1);
                place.Visible = url.Visible = shown;
                foreach (Control control in Controls) if (control != place && control != url && control.Top >= place.Top) control.Top += step;
                ClientSize = new Size(ClientSize.Width, ClientSize.Height + step);
            }

            // The row's first choice, or in the cloud the one with that API.
            private static VoiceServer.Service First(bool cloud, string wanted)
            {
                foreach (VoiceServer.Service choice in VoiceServer.Services)
                    if (Cloudy(choice) == cloud && (wanted == null || choice.Api == wanted)) return choice;
                return VoiceServer.Services[0];
            }

            // A ready-made choice fills in its address, model and voice. The last one, any server, keeps what's typed. A model file
            // asks for its folder, a Kokoro model's language and a speaker number instead, and has no key.
            private void Pick(VoiceServer.Service choice, bool fill)
            {
                bool file = choice.Api == VoiceServer.Files;
                // A key typed for one service never goes to the next one they pick.
                bool dropped = choice != picked && key.Box.Text.Trim().Length > 0;
                if (choice != picked) key.Box.Text = "";
                picked = choice;
                service.Text = choice.Name;
                hint.Text = choice.Hint;
                place.Text = file ? "Model folder" : "Address";
                url.Cue = file ? "Browse to a voice model" : VoiceServer.Address;
                modelName.Text = file ? "Language" : "Model";
                model.Cue = file ? "Kokoro only, like es" : "Optional";
                voiceName.Text = file ? "Speaker or clip" : "Voice";
                keyName.Visible = key.Visible = !file;
                browse.Visible = file;
                speaker.Cue = choice.VoiceCue;
                speaker.Chevron = file || (choice.Api == VoiceServer.Fish && choice.Url == VoiceServer.FishCloud);
                if (fill && choice.Url != null)
                {
                    url.Box.Text = choice.Url;
                    model.Box.Text = choice.Model;
                    speaker.Box.Text = choice.Speaker;
                }
                foreach (ToolStripItem item in services.Items) ((ToolStripMenuItem)item).Checked = item.Text == choice.Name;
                Kept();
                Say(!dropped ? "" : file ? "Key cleared: a model file needs none." : "Key cleared: paste the key for " + choice.Name + ".", false);
            }

            internal void Choose(string name)
            {
                foreach (ToolStripItem item in services.Items) if (item.Text == name) { item.PerformClick(); return; }
                throw new InvalidOperationException("The voice model panel has no " + name + " to pick.");
            }

            // Their own Fish Audio voices, listed with the key they typed or the one saved, to pick from.
            private void ListVoices()
            {
                Say("Getting your Fish Audio voices...", false);
                owner.capri.FishVoices(url.Box.Text, key.Box.Text, delegate(List<KeyValuePair<string, string>> found, string problem)
                {
                    if (IsDisposed) return;
                    if (problem != null) { Say(problem, true); return; }
                    voices.Items.Clear();
                    foreach (KeyValuePair<string, string> each in found)
                    {
                        string id = each.Key;
                        voices.Items.Add(Choice(each.Value, VoiceServer.FishVoice(speaker.Box.Text) == id, delegate { speaker.Box.Text = id; }));
                    }
                    if (voices.Items.Count > 0) voices.Items.Add(new ToolStripSeparator());
                    voices.Items.Add(Choice(NewFishVoice, false, delegate { MakeVoice(); }));
                    Say("", false);
                    voices.Show(speaker, new Point(0, speaker.Height));
                });
            }

            internal const string NewFishVoice = "New voice from a clip...";

            private void MakeVoice()
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = "Pick 10 to 30 seconds of someone talking, with their OK";
                    dialog.Filter = "Voice clips (*.wav;*.mp3;*.m4a;*.flac)|*.wav;*.mp3;*.m4a;*.flac|All files (*.*)|*.*";
                    if (dialog.ShowDialog(this) == DialogResult.OK) MakeVoice(dialog.FileName);
                }
            }

            // A new private Fish Audio voice from the clip; its ID goes in the Voice box.
            internal void MakeVoice(string clip)
            {
                Say("Making your Fish Audio voice...", false);
                owner.capri.MakeFishVoice(url.Box.Text, key.Box.Text, clip, delegate(string id, string problem)
                {
                    if (IsDisposed) return;
                    if (problem != null) { Say(problem, true); return; }
                    speaker.Box.Text = id;
                    Say("Made your Fish Audio voice. Press Test to hear it.", false);
                });
            }

            // Their Fish Audio voices' names in its Voice menu, for the smoke test.
            internal string Voices
            {
                get
                {
                    List<string> names = new List<string>();
                    foreach (ToolStripItem item in voices.Items) names.Add(item is ToolStripMenuItem ? item.Text + (((ToolStripMenuItem)item).Checked ? " (picked)" : "") : "-");
                    return string.Join(", ", names.ToArray());
                }
            }

            internal void PickVoice(string name)
            {
                foreach (ToolStripItem item in voices.Items) if (item.Text == name) { item.PerformClick(); return; }
                throw new InvalidOperationException("Fish Audio's voice menu has no " + name + ".");
            }

            internal void DropVoices() { ListVoices(); }

            // The clip whose voice a Pocket TTS model copies.
            private void PickClip()
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = "Pick a clip of the voice to copy, with their OK";
                    dialog.Filter = "Voice clips (*.wav;*.mp3;*.m4a;*.flac)|*.wav;*.mp3;*.m4a;*.flac|All files (*.*)|*.*";
                    if (dialog.ShowDialog(this) == DialogResult.OK) TakeClip(dialog.FileName);
                }
            }

            internal void TakeClip(string clip)
            {
                speaker.Box.Text = clip;
                Say("Pocket TTS models copy this clip's voice. Press Test to hear it.", false);
            }

            private void Browse()
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = "Pick a voice model: its .tar.bz2, or the .onnx in its folder";
                    dialog.Filter = "Voice models (*.tar.bz2;*.zip;*.onnx)|*.tar.bz2;*.zip;*.onnx|All files (*.*)|*.*";
                    if (dialog.ShowDialog(this) == DialogResult.OK) Take(dialog.FileName);
                }
            }

            // An .onnx is used where it is. An archive is unpacked into her models folder first, off the UI thread.
            internal void Take(string path)
            {
                if (path.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase))
                {
                    url.Box.Text = System.IO.Path.GetDirectoryName(path);
                    Say(ModelFile.Kind(url.Box.Text) == null ? ModelFile.Missing : "", ModelFile.Kind(url.Box.Text) == null);
                    return;
                }
                browse.Enabled = false;
                Say("Unpacking " + System.IO.Path.GetFileName(path) + "...", false);
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    string folder = null, problem = null;
                    try { folder = ModelFile.Unpack(path); }
                    catch (VoiceException error) { problem = error.Message; }
                    catch (Exception error) { problem = Voice.Short("Couldn't unpack that: " + error.Message); }
                    try
                    {
                        BeginInvoke(new MethodInvoker(delegate
                        {
                            browse.Enabled = true;
                            if (folder != null) url.Box.Text = folder;
                            Say(folder != null ? "Unpacked. Press Test to hear it." : problem, folder == null);
                        }));
                    }
                    catch (InvalidOperationException) { }
                });
            }

            private void Kept()
            {
                bool kept = savedTail != null && savedUrl != null && VoiceServer.Same(savedUrl, savedApi, url.Box.Text, picked.Api);
                key.Cue = kept ? "Key saved, ends " + savedTail : picked.KeyCue;
                forget.Visible = kept;
            }

            private void Say(string text, bool problem)
            {
                note.Text = text;
                note.ForeColor = problem ? Look.Red : Look.Dim;
            }

            private void Try()
            {
                test.Enabled = false;
                Say("Asking your server...", false);
                owner.capri.TestVoiceServer(url.Box.Text, model.Box.Text, speaker.Box.Text, key.Box.Text, picked.Api, delegate(string said, bool problem)
                {
                    if (IsDisposed) return;
                    test.Enabled = true;
                    Say(said, problem);
                });
            }

            private void Use()
            {
                owner.wantEleven = false;
                owner.wantCloud = null;
                owner.capri.UseVoiceServer(url.Box.Text, model.Box.Text, speaker.Box.Text, key.Box.Text, picked.Api);
                Close();
            }

            internal string Showing { get { return picked.Name + " | " + url.Box.Text + " | " + model.Box.Text + " | " + speaker.Box.Text + " | " + key.Cue + " | " + (forget.Visible ? "Forget" : "-") + " | " + note.Text; } }

            internal string Fields { get { return (addressShown ? place.Text : "no address") + " | " + modelName.Text + " | " + voiceName.Text + " | " + (key.Visible ? "Key" : "no key") + " | " + (browse.Visible ? browse.Text : "-"); } }

            // Its title and the choices in its Voice from menu, for the smoke test.
            internal string Choices
            {
                get
                {
                    List<string> names = new List<string>();
                    foreach (ToolStripItem item in services.Items) names.Add(item.Text);
                    return title.Text + " | " + string.Join(", ", names.ToArray());
                }
            }

            internal void Type(string address, string name, string voice, string secret)
            {
                url.Box.Text = address;
                model.Box.Text = name;
                speaker.Box.Text = voice;
                key.Box.Text = secret;
            }

            // Clicks a button as they would, even while the Mainframe is minimized.
            internal void Press(string button)
            {
                foreach (Control control in Controls)
                    if (control is Key && control.Text == button && control.Visible && control.Enabled) { typeof(Control).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(control, new object[] { EventArgs.Empty }); return; }
                throw new InvalidOperationException("The voice server panel has no " + button + " button to press.");
            }

            protected override void OnShown(EventArgs e)
            {
                base.OnShown(e);
                url.Box.Focus();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    services.Dispose();
                    voices.Dispose();
                }
                base.Dispose(disposing);
            }

            // Wrapped notes and a spot over the Mainframe, once Windows has scaled the panel to the screen.
            protected override void OnLoad(EventArgs e)
            {
                base.OnLoad(e);
                float s = ScaleOf(this);
                foreach (Control control in Controls) if (control is Label) control.MaximumSize = new Size((int)Math.Round(328 * s), 0);
                Rectangle area = Screen.FromControl(owner).WorkingArea;
                int x = owner.Left + (owner.Width - Width) / 2, y = owner.Top + (int)(48 * s);
                Location = new Point(Math.Max(area.Left, Math.Min(area.Right - Width, x)), Math.Max(area.Top, Math.Min(area.Bottom - Height, y)));
            }

            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);
                float s = ScaleOf(this);
                Region old = Region;
                using (GraphicsPath plate = Look.Plate(new RectangleF(0, 0, Width, Height), 12 * s, -1, Height / 2f, 0, true)) Region = new Region(plate);
                if (old != null) old.Dispose();
                Invalidate(true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                float s = ScaleOf(this);
                Graphics graphics = e.Graphics;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Backdrop(graphics, this, s);
                using (Brush red = new SolidBrush(Look.Red)) graphics.FillRectangle(red, 13 * s, 22 * s, 3 * s, 350 * s);
                using (GraphicsPath edge = Look.Plate(new RectangleF(s, s, Width - 2 * s - 1, Height - 2 * s - 1), 12 * s, -1, Height / 2f, 0, true))
                using (Pen cyan = new Pen(Look.Cyan, 2 * s))
                    graphics.DrawPath(cyan, edge);
            }

            // Dragged by any spot that isn't a control, like the Mainframe.
            protected override void WndProc(ref Message message)
            {
                base.WndProc(ref message);
                if (message.Msg == 0x0084 && message.Result == new IntPtr(1)) message.Result = new IntPtr(2);
            }

            private Label Caption(string text, int x, int y, Font font, Color colour)
            {
                Label label = new Note();
                label.Text = text;
                label.Font = font;
                label.ForeColor = colour;
                label.BackColor = Look.Panel;
                label.AutoSize = true;
                label.Location = new Point(x, y);
                Controls.Add(label);
                return label;
            }

            private Entry Input(int x, int y, int width, string cue, string name)
            {
                Entry entry = new Entry();
                entry.Bounds = new Rectangle(x, y, width, 28);
                entry.Cue = cue;
                entry.Box.AccessibleName = name;
                Controls.Add(entry);
                return entry;
            }

            private Key Make(string text, int x, int y, int width, int height, EventHandler click)
            {
                Key made = new Key();
                made.Text = text;
                made.Bounds = new Rectangle(x, y, width, height);
                made.Click += click;
                Controls.Add(made);
                return made;
            }
        }

        private sealed class Slider : Control
        {
            private readonly string[] labels;
            private int value;
            private bool hot;
            private bool sliding;
            private int settled;
            internal event EventHandler Changed;
            // Once the knob or an arrow key is let go somewhere new.
            internal event EventHandler Released;

            internal Slider(string[] labels)
            {
                this.labels = labels;
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Selectable, true);
                BackColor = Look.Panel;
                Cursor = Cursors.Hand;
                TabStop = true;
                AccessibleRole = AccessibleRole.Slider;
            }

            internal int Value
            {
                get { return value; }
                set { Place(value); settled = this.value; }
            }

            private void Place(int wanted)
            {
                int next = Math.Max(0, Math.Min(labels.Length - 1, wanted));
                if (next == value) return;
                value = next;
                AccessibleDescription = labels[next];
                Invalidate();
            }

            private void Pick(int next)
            {
                int before = value;
                Place(next);
                if (value != before && Changed != null) Changed(this, EventArgs.Empty);
            }

            private void Settle()
            {
                if (value == settled) return;
                settled = value;
                if (Released != null) Released(this, EventArgs.Empty);
            }

            internal void Slide(int next) { Pick(next); Settle(); }

            private float Unit() { return ((Mainframe)Parent).DeviceScale(); }

            private int At(int x)
            {
                float s = Unit();
                float start = 8 * s;
                float span = Width - 16 * s;
                return (int)Math.Round((x - start) / span * (labels.Length - 1));
            }

            protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }

            protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnMouseDown(MouseEventArgs e) { Focus(); sliding = true; Pick(At(e.X)); base.OnMouseDown(e); }

            protected override void OnMouseMove(MouseEventArgs e) { if (sliding) Pick(At(e.X)); base.OnMouseMove(e); }

            protected override void OnMouseUp(MouseEventArgs e) { bool was = sliding; sliding = false; if (was) Settle(); base.OnMouseUp(e); }

            protected override bool IsInputKey(Keys key) { return key == Keys.Left || key == Keys.Right || base.IsInputKey(key); }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Left) Pick(value - 1);
                else if (e.KeyCode == Keys.Right) Pick(value + 1);
                base.OnKeyDown(e);
            }

            protected override void OnKeyUp(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right) Settle();
                base.OnKeyUp(e);
            }

            protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }

            protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                float s = Unit();
                Graphics graphics = e.Graphics;
                Backdrop(graphics, this, s);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                float start = 8 * s;
                float span = Width - 16 * s;
                float middle = Height / 2f;
                float thumb = start + span * value / (labels.Length - 1);
                using (Brush track = new SolidBrush(Track)) graphics.FillRectangle(track, start, middle - 2 * s, span, 4 * s);
                using (Brush lit = new SolidBrush(Look.Cyan)) graphics.FillRectangle(lit, start, middle - 2 * s, thumb - start, 4 * s);
                using (Brush tick = new SolidBrush(Edge))
                    for (int i = 0; i < labels.Length; i++) graphics.FillRectangle(tick, start + span * i / (labels.Length - 1) - 0.5f * s, middle + 5 * s, 1.5f * s, 4 * s);
                RectangleF knob = new RectangleF(thumb - 5 * s, middle - 8 * s, 10 * s, 16 * s);
                using (Brush fill = new SolidBrush(Field)) graphics.FillRectangle(fill, knob);
                using (Pen edge = new Pen(hot || Focused || sliding ? Look.Cyan : Look.Ink, 1.6f * s)) graphics.DrawRectangle(edge, knob.X, knob.Y, knob.Width, knob.Height);
            }
        }

        private sealed class Rung : RadioButton
        {
            private bool hot;
            internal string Hint;
            internal Font HintFont;
            internal string Flag;
            internal Font FlagFont;

            internal Rung()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
                BackColor = Look.Panel;
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }

            protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }

            // A radio button clicks itself when focus arrives by arrow key or from code. The VOICE pair picks on a real click or Space only.
            protected override void OnEnter(EventArgs e) { if (AutoCheck) base.OnEnter(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                float s = ((Mainframe)Parent).DeviceScale();
                Graphics graphics = e.Graphics;
                Backdrop(graphics, this, s);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                float side = 13 * s;
                RectangleF dot = new RectangleF(s, (Height - side) / 2, side, side);
                using (Brush fill = new SolidBrush(Field)) graphics.FillEllipse(fill, dot);
                using (Pen edge = new Pen(hot || (Focused && ShowFocusCues) ? Look.Cyan : Edge, 1.4f * s)) graphics.DrawEllipse(edge, dot);
                if (Checked) using (Brush mark = new SolidBrush(Look.Cyan)) graphics.FillEllipse(mark, dot.X + 3.5f * s, dot.Y + 3.5f * s, dot.Width - 7 * s, dot.Height - 7 * s);
                int left = (int)(dot.Right + 9 * s);
                TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
                TextRenderer.DrawText(graphics, Text, Font, new Rectangle(left, 0, Width - left, Height), Checked ? Look.Cyan : Look.Ink, flags);
                int after = left + TextRenderer.MeasureText(graphics, Text, Font, Size.Empty, flags).Width + (int)(10 * s);
                if (Hint != null) TextRenderer.DrawText(graphics, Hint, HintFont ?? Font, new Rectangle(after, 0, Math.Max(0, Width - after), Height), Look.Dim, flags);
                if (Flag != null) TextRenderer.DrawText(graphics, Flag, FlagFont ?? Font, ClientRectangle, Look.Yellow, TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
        }

        private sealed class Toggle : CheckBox
        {
            private bool hot;
            internal string Hint;
            internal Font HintFont;

            internal Toggle()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
                BackColor = Look.Panel;
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }

            protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                float s = ((Mainframe)Parent).DeviceScale();
                Graphics graphics = e.Graphics;
                Backdrop(graphics, this, s);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                float side = 13 * s;
                RectangleF box = new RectangleF(s, (Height - side) / 2, side, side);
                using (Brush fill = new SolidBrush(Field)) graphics.FillRectangle(fill, box);
                using (Pen edge = new Pen(hot || (Focused && ShowFocusCues) ? Look.Cyan : Edge, 1.4f * s)) graphics.DrawRectangle(edge, box.X, box.Y, box.Width, box.Height);
                if (Checked) using (Brush mark = new SolidBrush(Look.Cyan)) graphics.FillRectangle(mark, box.X + 3.5f * s, box.Y + 3.5f * s, box.Width - 7 * s, box.Height - 7 * s);
                int left = (int)(box.Right + 9 * s);
                TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
                TextRenderer.DrawText(graphics, Text, Font, new Rectangle(left, 0, Width - left, Height), Look.Ink, flags);
                if (Hint == null) return;
                int after = left + TextRenderer.MeasureText(graphics, Text, Font, Size.Empty, flags).Width + (int)(14 * s);
                TextRenderer.DrawText(graphics, Hint, HintFont ?? Font, new Rectangle(after, 0, Math.Max(0, Width - after), Height), Look.Dim, flags);
            }
        }
    }
}
