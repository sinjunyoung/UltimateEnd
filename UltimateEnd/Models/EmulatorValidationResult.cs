using UltimateEnd.Enums;

namespace UltimateEnd.Models
{
    public class EmulatorValidationResult
    {
        public bool IsValid { get; set; }

        public EmulatorErrorType ErrorType { get; set; }

        public string? PlatformId { get; set; }

        public string? EmulatorId { get; set; }

        public string? EmulatorName { get; set; }

        public string? CoreName { get; set; }

        public string? MissingPath { get; set; }

        public string? ErrorMessage { get; set; }
    }
}