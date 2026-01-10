using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using UltimateEnd.Services;

namespace UltimateEnd.Desktop.Models
{
    public class CommandConfig : ICommandConfig
    {
        [JsonPropertyName("emulators")]
        public Dictionary<string, IEmulatorCommand> EmulatorCommands { get; set; } = [];        

        [JsonPropertyName("defaultEmulators")]
        public Dictionary<string, string> DefaultEmulatorsMap { get; set; } = [];

        [JsonIgnore]
        public Dictionary<string, IEmulatorCommand> Emulators => EmulatorCommands.ToDictionary(x => x.Key, x => (IEmulatorCommand)x.Value);

        [JsonIgnore]
        public Dictionary<string, string> DefaultEmulators => DefaultEmulatorsMap;

        public void AddEmulator(IEmulatorCommand command)
        {
            EmulatorCommands.Add(command.Id, command);
        }
    }
}