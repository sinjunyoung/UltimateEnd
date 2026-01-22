using UltimateEnd.Enums;
using UltimateEnd.Models;

namespace UltimateEnd.Utils
{
    public class HangulAutomata
    {
        private const int BASE = 0xAC00;
        private static readonly string CHO = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
        private static readonly string JUNG = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";
        private static readonly string JONG = "ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ";

        private int cho = -1, jung = -1, jong = -1;

        public HangulResult Input(string ch)
        {
            if (ch.Length != 1) return Reset(ch[0]);

            int c = CHO.IndexOf(ch[0]);
            int j = JUNG.IndexOf(ch[0]);

            if (c >= 0) return InputCho(c, ch[0]);
            if (j >= 0) return InputJung(j, ch[0]);

            return Reset(ch[0]);
        }

        private HangulResult InputCho(int c, char ch)
        {
            if (cho < 0)
            {
                cho = c;

                return new HangulResult { Action = HangulAction.Append, Char = ch };
            }

            if (jung < 0)
            {
                char prev = CHO[cho];
                cho = c;
                jung = -1;
                jong = -1;

                return new HangulResult { Action = HangulAction.Complete, Completed = prev, Char = ch };
            }

            if (jong < 0)
            {
                jong = JONG.IndexOf(ch);

                if (jong < 0)
                {
                    char prev = Compose();
                    cho = c;
                    jung = -1;
                    jong = -1;

                    return new HangulResult { Action = HangulAction.Complete, Completed = prev, Char = ch };
                }

                return new HangulResult { Action = HangulAction.Update, Char = Compose() };
            }

            char completed = Compose();

            cho = c;
            jung = -1;
            jong = -1;

            return new HangulResult { Action = HangulAction.Complete, Completed = completed, Char = ch };
        }

        private HangulResult InputJung(int j, char ch)
        {
            if (cho < 0) return Reset(ch);

            if (jung < 0)
            {
                jung = j;

                return new HangulResult { Action = HangulAction.Update, Char = Compose() };
            }

            if (jong >= 0)
            {
                char completed = ComposeWithoutJong();
                char jongChar = JONG[jong];

                cho = CHO.IndexOf(jongChar);
                jung = j;
                jong = -1;

                return new HangulResult
                {
                    Action = HangulAction.Complete,
                    Completed = completed,
                    Char = Compose()
                };
            }

            int combined = TryCombineJung(jung, j);

            if (combined >= 0)
            {
                jung = combined;

                return new HangulResult { Action = HangulAction.Update, Char = Compose() };
            }

            char prev = Compose();
            cho = -1;
            jung = -1;
            jong = -1;

            return new HangulResult { Action = HangulAction.Complete, Completed = prev, Char = ch };
        }

        private char ComposeWithoutJong()
        {
            if (cho < 0 || jung < 0) return '\0';

            int code = BASE + (cho * 21 * 28) + (jung * 28);

            return (char)code;
        }

        private static int TryCombineJung(int j1, int j2)
        {
            if (j1 == 8 && j2 == 0) return 9;
            if (j1 == 8 && j2 == 1) return 10;
            if (j1 == 8 && j2 == 20) return 11;
            if (j1 == 13 && j2 == 4) return 14;
            if (j1 == 13 && j2 == 5) return 15;
            if (j1 == 13 && j2 == 20) return 16;
            if (j1 == 18 && j2 == 20) return 19;

            return -1;
        }

        private char Compose()
        {
            if (cho < 0 || jung < 0) return '\0';
            int code = BASE + (cho * 21 * 28) + (jung * 28) + (jong < 0 ? 0 : jong + 1);
            return (char)code;
        }

        private HangulResult Reset(char ch)
        {
            cho = -1;
            jung = -1;
            jong = -1;

            return new HangulResult { Action = HangulAction.Append, Char = ch };
        }

        public void Reset()
        {
            cho = -1;
            jung = -1;
            jong = -1;
        }
    }
}