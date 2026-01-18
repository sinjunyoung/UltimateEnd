namespace UltimateEnd.Desktop.Models
{
    public class ButtonMapping
    {
        public int A { get; set; }

        public int B { get; set; }

        public int X { get; set; }

        public int Y { get; set; }

        public int LB { get; set; }

        public int RB { get; set; }

        public int Select { get; set; }

        public int Start { get; set; }

        public int LeftStick { get; set; }

        public int RightStick { get; set; }

        public static ButtonMapping XboxStyle() => new()
        {
            A = 0,
            B = 1,
            X = 2,
            Y = 3,
            LB = 4,
            RB = 5,
            Select = 6,
            Start = 7,
            LeftStick = 8,
            RightStick = 9
        };

        public static ButtonMapping PlayStationStyle() => new()
        {
            A = 1,      // Cross
            B = 2,      // Circle
            X = 0,      // Square
            Y = 3,      // Triangle
            LB = 4,     // L1
            RB = 5,     // R1
            Select = 8, // Share/Create
            Start = 9,  // Options
            LeftStick = 10,
            RightStick = 11
        };

        public static ButtonMapping SwitchStyle() => new()
        {
            A = 1,
            B = 0,
            X = 3,
            Y = 2,
            LB = 4,
            RB = 5,
            Select = 8,
            Start = 9,
            LeftStick = 10,
            RightStick = 11
        };
    }
}