using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using AssetRipper.Assets;
using AssetRipper.Export.PrimaryContent;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Export.UnityProjects.Configuration;
using AssetRipper.Import.Configuration;
using AssetRipper.Import.Structure.Assembly.Serializable;
using AssetRipper.Primitives;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Classes.ClassID_114;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Unity.Extractor;

internal class Program
{
    private static readonly HashSet<string> ClassNames = ["GameObject", "MonoBehaviour"];
    private const string OutputFolder = @"F:\Fractured\Data";
    private const string AssetsPath = @"I:\Games\Fractured Online\app";
    private const string ResourcesPath = @"I:\Games\Fractured Online\app\UnityClient@Windows_Data\resources.assets";
    static void Main(string[] args)
    {
        var jsonExtractor = new JsonConverter();
        //var classNames = new HashSet<string>();
        //var paths = new Dictionary<long, IUnityAssetBase>();
        var handler = new ExportHandler(new LibraryConfiguration{ExportRootPath = OutputFolder});
        Console.WriteLine("Loading assets");
        var gameData = handler.LoadAndProcess([AssetsPath]);

        Console.WriteLine("Exporting assets");
        foreach (var collection in gameData.GameBundle.Collections)
        {
            foreach (var asset in collection.Assets)
            {
                if(ClassNames.Contains(asset.Value.ClassName)) continue;
                var assetJson = jsonExtractor.Export(asset.Value);
                var fileName = Path.Combine(OutputFolder, asset.Value.Collection.Name, asset.Value.PathID + ".json");
                Directory.CreateDirectory(Path.GetDirectoryName(fileName)!) ;
                File.WriteAllText(fileName, assetJson);
                //classNames.Add(asset.Value.ClassName);
                //paths.Add(asset.Value.PathID, asset.Value);
            }
        }
        Console.WriteLine("Done");

        //var json = JsonSerializer.Serialize(classNames.OrderBy(c => c));
        //File.WriteAllText(Path.Combine(OutputFolder, "classNames.json"), json);
    }
}

public class Metadata
{
    public string Collection { get; set; }
    public Dictionary<int, string> Files { get; set; }
    public long PathId { get; set; }
    public string Name { get; set; }
    public string Namespace { get; set; }
}

public class JsonConverter : IContentExtractor
{
    public bool TryCreateCollection(IUnityObjectBase asset, [NotNullWhen(true)] out ExportCollectionBase? exportCollection)
    {
        exportCollection = new JsonExportCollection(this, asset);
        return true;
    }

    public string Export(IUnityObjectBase asset)
    {
        var json = new DefaultJsonWalker().SerializeStandard(asset);
        var jObject = JObject.Parse(json);

        var dependencies = asset.Collection.Dependencies.Select((d, i) => new { d, i }).ToDictionary(r => r.i, r => r.d?.Name ?? string.Empty);

        string name = null;
        string ns = null;

        if (asset is IMonoBehaviour { Structure: SerializableStructure serializableStructure })
        {
            name = serializableStructure.Type.Name;
            ns = serializableStructure.Type.Namespace;
        }

        var meta = new Metadata
        {
            Collection = asset.Collection.Name,
            Files = dependencies,
            PathId = asset.PathID,
            Name = name,
            Namespace = ns
        };
        var metaObject = JObject.FromObject(meta);
        jObject.Add("Metadata", metaObject);
        return JsonConvert.SerializeObject(jObject,Formatting.Indented);
    }

    private sealed class JsonExportCollection(IContentExtractor contentExtractor, IUnityObjectBase asset)
        : SingleExportCollection<IUnityObjectBase>(contentExtractor, asset)
    {
        protected override string ExportExtension => "json";
    }
}