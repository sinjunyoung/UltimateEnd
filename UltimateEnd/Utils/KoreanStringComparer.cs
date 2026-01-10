using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace UltimateEnd.Utils
{
    public class KoreanStringComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return 1;
            if (y == null) return -1;

            string xt = TrimStart(x);
            string yt = TrimStart(y);

            return string.Compare(
                xt,
                yt,
                new CultureInfo("ko-KR"),
                CompareOptions.IgnoreCase
            );
        }

        private string TrimStart(string s)
        {
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c))
                    return s.Substring(s.IndexOf(c));
            }
            return s;
        }
        
        public static bool HasKorean(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;

            return s.Any(c =>
                (c >= 0xAC00 && c <= 0xD7A3) ||
                (c >= 0x1100 && c <= 0x11FF) ||
                (c >= 0x3130 && c <= 0x318F));
        }

    }
}