using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using UltimateEnd.Services;

namespace UltimateEnd.Android.Models
{
    public class CommandConfig : ICommandConfig
    {
        [JsonPropertyName("emulators")]
        public Dictionary<string, IEmulatorCommand> EmulatorCommands { get; set; } = new();

        public void AddEmulator(IEmulatorCommand command)
        {
            EmulatorCommands.Add(command.Id, command);
        }

        [JsonPropertyName("defaultEmulators")]
        public Dictionary<string, string> DefaultEmulatorsMap { get; set; } = new();

        [JsonIgnore]
        public Dictionary<string, IEmulatorCommand> Emulators =>
            EmulatorCommands.ToDictionary(x => x.Key, x => (IEmulatorCommand)x.Value);

        [JsonIgnore]
        public Dictionary<string, string> DefaultEmulators => DefaultEmulatorsMap;
    }
}