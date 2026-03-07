using SPTarkov.Server.Core.Models.Spt.Mod;

namespace Amonya
{
    public record ModMetadata : AbstractModMetadata
    {
        public override string ModGuid { get; init; } = "com.szonszczyk.amonya";
        public override string Name { get; init; } = "Amonya";
        public override string Author { get; init; } = "Szonszczyk";
        public override List<string>? Contributors { get; init; }
        public override SemanticVersioning.Version Version { get; init; } = new("2.0.4");
        public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.4");
        public override List<string>? Incompatibilities { get; init; } = [];
        public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; } = [];
        public override string? Url { get; init; } = "https://github.com/Szonszczyk/Amonya-csharp"; // TODO
        public override bool? IsBundleMod { get; init; } = false;
        public override string? License { get; init; } = "MIT";
    }
}