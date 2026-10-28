using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;

namespace CapriDesktop
{
    internal static class Look
    {
        internal static readonly Color Panel = Color.FromRgb(9, 11, 15);
        internal static readonly Color Cyan = Color.FromRgb(58, 214, 235);
        internal static readonly Color Red = Color.FromRgb(232, 36, 64);
        internal static readonly Color Neon = Color.FromRgb(56, 176, 255);
        internal static readonly Color Ink = Color.FromRgb(232, 236, 240);
        internal static readonly Color Dim = Color.FromRgb(122, 128, 138);
        internal static readonly Color Edge = Color.FromRgb(70, 73, 82);
        internal static readonly Color Pill = Color.FromArgb(244, 20, 21, 25);
        internal static readonly FontFamily Mono = new FontFamily("avares://Capri/Assets#Share Tech Mono");
        internal static readonly Typeface Regular = new Typeface(Mono);
        internal static readonly Typeface Italic = new Typeface(Mono, FontStyle.Italic);
        internal static readonly Typeface Bold = new Typeface(Mono, FontStyle.Normal, FontWeight.Bold);

        internal static FormattedText Text(string text, Typeface face, double size, Color colour)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, new SolidColorBrush(colour));
        }

        internal static double CharWidth(double size)
        {
            return Text(new string('M', 20), Regular, size, Ink).WidthIncludingTrailingWhitespace / 20;
        }

        internal static Geometry Plate(Rect box, double cut, double tipX, double tipY, double tail, bool both = false)
        {
            double right = both ? cut : 0;
            List<Point> points = new List<Point>();
            double near = Math.Max(box.Left + cut + 2, tipX - tail * 0.8);
            double far = Math.Min(box.Right - right - 2, tipX + tail * 0.2);
            points.Add(new Point(box.Left + cut, box.Top));
            if (tipY < box.Top) { points.Add(new Point(near, box.Top)); points.Add(new Point(tipX, tipY)); points.Add(new Point(far, box.Top)); }
            points.Add(new Point(box.Right - right, box.Top));
            if (both) points.Add(new Point(box.Right, box.Top + cut));
            if (both) points.Add(new Point(box.Right, box.Bottom - cut));
            points.Add(new Point(box.Right - right, box.Bottom));
            if (tipY > box.Bottom) { points.Add(new Point(far, box.Bottom)); points.Add(new Point(tipX, tipY)); points.Add(new Point(near, box.Bottom)); }
            points.Add(new Point(box.Left + cut, box.Bottom));
            points.Add(new Point(box.Left, box.Bottom - cut));
            points.Add(new Point(box.Left, box.Top + cut));
            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(points[0], true);
                for (int i = 1; i < points.Count; i++) context.LineTo(points[i]);
                context.EndFigure(true);
            }
            return geometry;
        }

        // How many columns a character takes in her mono bubbles: Chinese, Japanese and Korean characters and full-width forms
        // take two, Thai vowel and tone marks (they sit above or below a letter) none, everything else one. The same as on Windows.
        internal static int Cells(char c)
        {
            if (c == 0x0E31 || (c >= 0x0E34 && c <= 0x0E3A) || (c >= 0x0E47 && c <= 0x0E4E)) return 0;
            return (c >= 0x1100 && c <= 0x115F) || (c >= 0x2E80 && c <= 0x303E) || (c >= 0x3041 && c <= 0x33FF) || (c >= 0x3400 && c <= 0x4DBF) || (c >= 0x4E00 && c <= 0x9FFF)
                || (c >= 0xA000 && c <= 0xA4CF) || (c >= 0xA960 && c <= 0xA97F) || (c >= 0xAC00 && c <= 0xD7A3) || (c >= 0xF900 && c <= 0xFAFF) || (c >= 0xFE10 && c <= 0xFE19)
                || (c >= 0xFE30 && c <= 0xFE6F) || (c >= 0xFF00 && c <= 0xFF60) || (c >= 0xFFE0 && c <= 0xFFE6) ? 2 : 1;
        }

        internal static int Cells(string text)
        {
            if (Indic(text)) return (int)Math.Ceiling(Span(text) - 0.01);
            int cells = 0;
            foreach (char c in text ?? "") cells += Cells(c);
            return cells;
        }

        // True when the text has an Indian script in it, Devanagari to Malayalam, or Ethiopic, which Amharic is written in.
        internal static bool Indic(string text)
        {
            foreach (char c in text ?? "") if ((c >= 0x0900 && c <= 0x0DFF) || (c >= 0x1200 && c <= 0x139F)) return true;
            return false;
        }

        // Indian scripts join letters and marks into one shape, and Tamil, Kannada, Malayalam and Ethiopic draw wider than her
        // columns, so their words are measured in her bubble's font instead of counted, the same as on Windows.
        private static readonly Dictionary<string, double> measured = new Dictionary<string, double>();

        private static double Span(string text)
        {
            double cells = 0;
            string[] words = text.Split(' ');
            foreach (string word in words)
            {
                if (!Indic(word)) { foreach (char c in word) cells += Cells(c); continue; }
                lock (measured)
                {
                    double width;
                    if (!measured.TryGetValue(word, out width))
                    {
                        // A shade over, like Windows; a font that can't measure falls back to a column a letter.
                        try { width = Text(word, Regular, 14, Ink).WidthIncludingTrailingWhitespace / CharWidth(14) * 1.03; }
                        catch (Exception) { width = word.Length; }
                        if (measured.Count > 4000) measured.Clear();
                        measured[word] = width;
                    }
                    cells += width;
                }
            }
            return cells + words.Length - 1;
        }

        // True when the first letter is Arabic, Urdu, Persian or Hebrew, so the line reads right to left. Digits and
        // punctuation don't decide it.
        internal static bool RightToLeft(string text)
        {
            foreach (char c in text ?? "")
            {
                if ((c >= 0x0590 && c <= 0x08FF) || (c >= 0xFB1D && c <= 0xFDFF) || (c >= 0xFE70 && c <= 0xFEFE)) return true;
                if (char.IsLetter(c)) return false;
            }
            return false;
        }

        // The longest start of the text that fits in that many columns.
        internal static string Cut(string text, int columns)
        {
            text = text ?? "";
            if (Indic(text))
            {
                // Whole letters with their marks, measured.
                int fits = 0;
                int[] starts = StringInfo.ParseCombiningCharacters(text);
                for (int k = 1; k <= starts.Length; k++)
                {
                    int end = k < starts.Length ? starts[k] : text.Length;
                    if (Cells(text.Substring(0, end)) > columns) break;
                    fits = end;
                }
                return text.Substring(0, fits);
            }
            int cells = 0, length = 0;
            while (length < text.Length && cells + Cells(text[length]) <= columns) cells += Cells(text[length++]);
            return text.Substring(0, length);
        }

        // True when the text has Thai in it.
        internal static bool Thai(string text)
        {
            foreach (char c in text ?? "") if (c >= 0x0E01 && c <= 0x0E5B) return true;
            return false;
        }

        // Thai writes no spaces between words, and Mac and Linux have no word breaker she can call, so a Thai line breaks
        // between letters: never before a vowel or tone mark, or a vowel that follows its letter, and never after a vowel
        // written before its letter. Windows breaks between words.
        internal static bool[] Letters(string word)
        {
            bool[] breaks = new bool[word.Length];
            for (int i = 1; i < word.Length; i++)
                breaks[i] = Cells(word[i]) > 0 && "ฯะาำๅๆ".IndexOf(word[i]) < 0 && (word[i - 1] < 0x0E40 || word[i - 1] > 0x0E44);
            return breaks;
        }

        // The longest start of a word with Thai in it that ends where a line may break and fits in that many columns.
        internal static string Fill(string word, int columns)
        {
            bool[] breaks = Letters(word);
            int cells = 0, end = 0;
            for (int i = 0; i < word.Length; i++)
            {
                if (breaks[i]) end = i;
                cells += Cells(word[i]);
                if (cells > columns) return word.Substring(0, end);
            }
            return word;
        }

        // Lines by columns, so Chinese, Japanese and Korean fit the bubble too. They have no spaces to break at, so a run of
        // them fills the rest of a line and carries on below, the way they're written. Thai does the same. Other words only
        // break when they're longer than a whole line.
        internal static List<string> Wrap(string text, int columns)
        {
            List<string> lines = new List<string>();
            string line = "";
            foreach (string piece in (text ?? "").Split(' '))
            {
                string word = piece;
                bool thai = Thai(word);
                if (word.Length > 0 && (Cells(word[0]) == 2 || thai) && line.Length > 0 && Cells(line) + 1 + Cells(word) > columns)
                {
                    string start = thai ? Fill(word, columns - Cells(line) - 1) : Kept(word, Cut(word, columns - Cells(line) - 1));
                    if (start.Length > 0) { line += " " + start; word = word.Substring(start.Length); }
                    lines.Add(line);
                    line = "";
                }
                while (Cells(word) > columns)
                {
                    if (line.Length > 0) { lines.Add(line); line = ""; }
                    string cut = thai ? Fill(word, columns) : "";
                    if (cut.Length == 0) cut = Kept(word, Cut(word, columns));
                    if (cut.Length == 0) cut = word.Substring(0, 1);
                    lines.Add(cut);
                    word = word.Substring(cut.Length);
                }
                if (line.Length == 0) line = word;
                else if (Cells(line) + 1 + Cells(word) <= columns) line += " " + word;
                else { lines.Add(line); line = word; }
            }
            if (line.Length > 0 || lines.Count == 0) lines.Add(line);
            return lines;
        }

        // Chinese and Japanese never start a line with closing punctuation, so the character before it moves down with it.
        private const string Closing = "，。、！？：；）」』】〕〉》｝’”…,.!?:;)";

        private static string Kept(string word, string cut)
        {
            while (cut.Length > 1 && cut.Length < word.Length && Closing.IndexOf(word[cut.Length]) >= 0) cut = cut.Substring(0, cut.Length - 1);
            return cut;
        }

        // How much of each line shows once `revealed` characters of the text are out. A break between words used up spaces,
        // which count as revealed too; a word or a run of Chinese broken across lines used none.
        internal static int[] Takes(List<string> lines, string text, int revealed)
        {
            int[] takes = new int[lines.Count];
            int at = 0;
            for (int i = 0; i < lines.Count && revealed > 0; i++)
            {
                takes[i] = Math.Min(lines[i].Length, revealed);
                revealed -= takes[i];
                at += lines[i].Length;
                while (at < text.Length && text[at] == ' ') { at++; revealed--; }
            }
            return takes;
        }

        internal static void Overlay(Window window, string title)
        {
            window.Title = title;
            window.SystemDecorations = SystemDecorations.None;
            window.Background = Brushes.Transparent;
            window.TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            window.Topmost = true;
            window.ShowInTaskbar = false;
            window.CanResize = false;
            window.ShowActivated = false;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
        }
    }
}
