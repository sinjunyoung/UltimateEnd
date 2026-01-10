using System.Collections.Generic;

namespace UltimateEnd.Android.Models
{
    public class ActivityInfo
    {
        public string Name { get; set; } = string.Empty;

        public bool IsLauncher { get; set; }

        public bool SupportsView { get; set; }

        public string DisplayText
        {
            get
            {
                var tags = new List<string>();
                if (IsLauncher) tags.Add("MAIN");
                if (SupportsView) tags.Add("VIEW");

                if (tags.Count > 0)
                {
                    var shortName = Name;
                    var lastDot = Name.LastIndexOf('.');
                    if (lastDot >= 0 && lastDot < Name.Length - 1)
                        shortName = Name.Substring(lastDot + 1);

                    return $"{shortName} [{string.Join(", ", tags)}]";
                }

                return Name;
            }
        }
    }
}