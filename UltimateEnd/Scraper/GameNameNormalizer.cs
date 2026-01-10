using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UltimateEnd.Scraper
{
    public static class GameNameNormalizer
    {
        // 파일명 앞/뒤의 모든 괄호 메타 제거: (K), (J), (한), (Japan), [En] 등
        private static readonly Regex LeadingTrailingBracketRegex =
            new Regex(
                @"^(?:\s*[\[\(][^\]\)]*[\]\)])+|(?:[\[\(][^\]\)]*[\]\)]\s*)+$",
                RegexOptions.Compiled
            );

        // v / ver / _ver 등장 시점부터 끝까지 전부 제거
        // 단, 로마숫자 뒤의 v는 제외 (Final Fantasy V 같은 경우)
        private static readonly Regex VersionAndAfterRegex =
            new Regex(
                @"(?i)(?<=\s)(?:ver|v)[\d\.]+.*$|(?<=_)(?:ver|v)\b.*$",
                RegexOptions.Compiled
            );

        // 로마 숫자 목록 (게임 시리즈 넘버링용)
        private static readonly HashSet<string> RomanNumerals = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X",
            "XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII", "XIX", "XX"
        };

        public static string Normalize(string gameName)
        {
            if (string.IsNullOrWhiteSpace(gameName))
                return string.Empty;

            string result = gameName;

            // [Step 1] 파일 확장자 처리 (마지막 4자리 강제 제거)
            if (result.Length > 4)
                result = result.Substring(0, result.Length - 4);

            // 1. 앞/뒤 괄호 메타 제거
            result = LeadingTrailingBracketRegex.Replace(result, string.Empty);

            // 2. v / ver 이후 제거 (단, 로마숫자 V는 보호)
            result = VersionAndAfterRegex.Replace(result, string.Empty);

            // 3. 공백 정리
            return result.Trim();
        }
    }
}