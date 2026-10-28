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
            Content = new Border { Background = new SolidColorBrush(Look.Panel), BorderBrush = new SolidColorBrush(Look.Red), BorderThickness = new Thickness(1), Child = page };
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
            // NVIDIA model names run long, so the name shortens to keep Refresh on the window.
            models.Content = new TextBlock { Text = chosen ?? "Pick a model", MaxWidth = 220, TextTrimming = TextTrimming.CharacterEllipsis };
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
