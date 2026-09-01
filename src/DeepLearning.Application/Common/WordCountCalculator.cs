using System.Text;

namespace DeepLearning.Application.Common
{
    /// <summary>
    /// Derives the <c>word_count</c> for a question from its source passage. Manual import
    /// (<c>ImportUserQuestionCommandHandler</c>) always recomputes this instead of trusting a
    /// caller-supplied value, so an admin entering a question by hand never has to count words.
    ///
    /// Counting rule: every CJK ideograph / kana counts as one word (there are no spaces to split
    /// on), and every maximal run of letters-or-digits in any other script counts as one word
    /// (so "state-of-the-art" is 4, "Rule 34" is 2). Whitespace and punctuation only separate
    /// words. This is exact for the current NAATI CT 英译中 source text (English) and stays
    /// sensible if a 中译英 direction with Chinese source text is added later (design doc §9).
    /// </summary>
    public static class WordCountCalculator
    {
        public static int Count(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            var count = 0;
            var inWord = false;

            foreach (var rune in text.EnumerateRunes())
            {
                if (IsIdeographicOrKana(rune))
                {
                    count++;
                    inWord = false;
                }
                else if (Rune.IsLetterOrDigit(rune))
                {
                    if (!inWord)
                    {
                        count++;
                        inWord = true;
                    }
                }
                else
                {
                    inWord = false;
                }
            }

            return count;
        }

        private static bool IsIdeographicOrKana(Rune rune)
        {
            var value = rune.Value;

            // CJK Unified Ideographs + Ext. A, CJK Compatibility Ideographs, and the two
            // Supplementary Ideographic Plane blocks (Ext. B..F). Hiragana + Katakana are
            // included so Japanese source text degrades gracefully rather than counting a
            // whole sentence as zero words.
            return (value >= 0x3040 && value <= 0x30FF)   // Hiragana + Katakana
                || (value >= 0x3400 && value <= 0x4DBF)   // CJK Ext. A
                || (value >= 0x4E00 && value <= 0x9FFF)   // CJK Unified Ideographs
                || (value >= 0xF900 && value <= 0xFAFF)   // CJK Compatibility Ideographs
                || (value >= 0x20000 && value <= 0x2FA1F); // CJK Ext. B..F + Compatibility Supplement
        }
    }
}
