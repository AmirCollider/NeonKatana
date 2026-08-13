using System.Collections.Generic;
using System.Text;

namespace NeonKatana
{
    /// <summary>
    /// Every word the game says, in code.
    ///
    /// <para>
    /// <b>Keyed by the Persian that is already in the scene.</b> There is no key to invent, no
    /// component to put on each label and no asset to keep in step — a label keeps the Persian it
    /// was built with, and that Persian is the lookup. <see cref="TextTranslator"/> reads it once,
    /// remembers it, and swaps the words whenever the language changes.
    /// </para>
    /// <para>
    /// To add a line: put the Persian on the left and the two translations beside it. Anything not
    /// listed here is left alone rather than blanked, so a line nobody has translated yet simply
    /// stays in Persian — and <see cref="TextTranslator"/> prints it to the Console already written
    /// out as a row you can paste in here.
    /// </para>
    /// <para>
    /// <b>Whitespace does not have to match.</b> Line breaks, double spaces and the invisible
    /// direction marks that creep into a right-to-left text field are all collapsed before the
    /// lookup, on both sides. Typing a paragraph on one line here still matches the same paragraph
    /// broken over three lines in the label, which is the difference between this working and this
    /// being a puzzle every time somebody edits a label.
    /// </para>
    /// </summary>
    public static class GameText
    {
        /// <summary>One line, in all three languages.</summary>
        public readonly struct Line
        {
            public readonly string English;
            public readonly string Japanese;

            public Line(string english, string japanese)
            {
                English = english;
                Japanese = japanese;
            }
        }

        // ==========================================
        // The whole of the game's text.
        //
        // Persian first, then English, then Japanese.
        // Whitespace in the Persian is free - it is
        // normalized before it becomes a key - so a long
        // paragraph can be written on one line here.
        // ==========================================
        static readonly string[,] Written =
        {
            // --- Main menu ---
            { "شروع بازی", "Start Game", "ゲーム開始" },
            { "مدیریت حساب", "Account", "アカウント" },
            { "خروج از بازی", "Quit Game", "ゲーム終了" },
            { "با زدن این وارد اکانتت شو!", "Tap here to sign in!", "ここをタップしてサインイン！" },

            // --- Account screen ---
            { "برگشت به منو", "Back to Menu", "メニューに戻る" },
            { "سایتمون!", "Our Website!", "ウェブサイト！" },
            {
                "سلام دوست من! با رفتن به وبسایتمون میتونی نام کاربری یا عکس پروفایلت رو تغیر بدی! "
                    + "همینطور میتونی به جدول امتیازات بازی دسترسی داشته باشی!",

                "Hello friend!\n\nHead over to our website to change your username or your profile "
                    + "picture — and to see where you stand on the leaderboard!",

                "こんにちは！\n\nウェブサイトでユーザー名やプロフィール画像を変更できます。"
                    + "ランキングも確認できます！"
            },

            // --- Pause menu, and the game-over screen ---
            //
            // The two screens share their words, which is why there are fewer rows here than there
            // are labels: "شروع مجدد" is the restart button in both, and "بازگشت به منو" is the
            // way out of both. A row is a line of text, not a button.
            { "ادامه ی بازی", "Resume", "再開" },
            { "شروع مجدد", "Restart", "リスタート" },
            { "بازگشت به منو", "Main Menu", "メニューへ" },
            { "شما باختید!", "You Lost!", "ゲームオーバー！" },
        };

        static readonly Dictionary<string, Line> Lines = Build();

        static Dictionary<string, Line> Build()
        {
            var lines = new Dictionary<string, Line>(Written.GetLength(0));

            for (int row = 0; row < Written.GetLength(0); row++)
            {
                string key = Normalize(Written[row, 0]);
                if (key.Length == 0) continue;

                lines[key] = new Line(Written[row, 1], Written[row, 2]);
            }

            return lines;
        }

        /// <summary>
        /// What <paramref name="persian"/> says in <paramref name="language"/>. Comes back
        /// unchanged when the line is Persian already, or when nobody has translated it yet —
        /// which is deliberate: an untranslated line should read oddly, not vanish.
        /// </summary>
        public static string In(string persian, GameLanguage language)
        {
            if (string.IsNullOrEmpty(persian) || language == GameLanguage.Persian) return persian;

            if (!Lines.TryGetValue(Normalize(persian), out Line line)) return persian;

            string translated = language == GameLanguage.Japanese ? line.Japanese : line.English;

            return string.IsNullOrEmpty(translated) ? persian : translated;
        }

        /// <summary>Whether this line has been written down here at all.</summary>
        public static bool Knows(string persian) =>
            !string.IsNullOrEmpty(persian) && Lines.ContainsKey(Normalize(persian));

        /// <summary>
        /// The form a line is looked up by: every run of whitespace becomes one space, and the
        /// invisible direction marks are dropped.
        /// <para>
        /// A right-to-left text field collects U+200E and U+200F without anybody typing them, and
        /// a label edited in the Inspector comes back with Windows line endings. Matching on the
        /// raw string means a line stops being found because somebody pressed Enter in it, and
        /// nothing about that looks like a whitespace problem from the outside.
        /// </para>
        /// </summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var built = new StringBuilder(text.Length);
            bool lastWasSpace = false;

            foreach (char c in text)
            {
                // Left-to-right and right-to-left marks, and the zero-width space.
                if (c == '‎' || c == '‏' || c == '​' || c == '﻿') continue;

                if (char.IsWhiteSpace(c))
                {
                    if (!lastWasSpace && built.Length > 0) built.Append(' ');
                    lastWasSpace = true;
                    continue;
                }

                built.Append(c);
                lastWasSpace = false;
            }

            return built.ToString().TrimEnd();
        }
    }
}
