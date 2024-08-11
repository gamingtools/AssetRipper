using AssetRipper.Assets;
using AssetRipper.Export.Modules.Textures;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Export.UnityProjects.Configuration;
using AssetRipper.Import.Structure.Assembly.Serializable;
using AssetRipper.Mining.PredefinedAssets;
using AssetRipper.SourceGenerated.Classes.ClassID_114;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Unity.Extractor;

public class IndexEntry
{
    public JToken Metadata { get; set; }
    public JToken Data { get; set; }
}

internal class Program
{
    private static readonly HashSet<string> ClassNames = ["MonoBehaviour", "Texture2D"];
    private const string OutputFolder = @"F:\Fractured\Data";
    private const string AssetsPath = @"I:\Games\Fractured Online\app";
    //private const string AssetsPath = @"I:\Games\Fractured Online\app\UnityClient@Windows_Data\resources.assets";

    private static void Main()
    {
        var handler = new ExportHandler(new LibraryConfiguration { ExportRootPath = OutputFolder });
        Console.WriteLine("Loading assets");
        var gameData = handler.LoadAndProcess([AssetsPath]);
        var typeIndex = new SortedDictionary<string, List<IndexEntry>>();

        Console.WriteLine("Exporting assets");
        foreach (var collection in gameData.GameBundle.Collections)
        {
            foreach (var asset in collection.Assets)
            {
                if (!ClassNames.Contains(asset.Value.ClassName)) continue;
                //var shouldSerialize = asset.Value is IMonoBehaviour { Structure: SerializableStructure { Type.Name: not null } };
                //if (!shouldSerialize) continue;
                switch (asset.Value.ClassName)
                {
                    case "MonoBehaviour":
                        {
                            if (asset.Value is not IMonoBehaviour monoBehaviour) continue;
                            var assetJson = JsonExtractor.Export(asset.Value);
                            var fileName = Path.Combine(OutputFolder, asset.Value.ClassName, asset.Value.Collection.Name, asset.Value.PathID + ".json");
                            Directory.CreateDirectory(Path.GetDirectoryName(fileName)!);
                            File.WriteAllText(fileName, assetJson);
                            if (monoBehaviour.Structure is SerializableStructure structure)
                            {
                                var assetJObject = JObject.Parse(assetJson);
                                var mStructure = assetJObject["m_Structure"];
                                var metadata = assetJObject["Metadata"];
                                typeIndex.TryAdd(structure.Type.Name, new List<IndexEntry>());
                                typeIndex[structure.Type.Name].Add(new IndexEntry {Data = mStructure, Metadata =metadata });
                            }
                        }
                        break;

                    case "Texture2D":
                        break;
                        {
                            if (asset.Value is not ITexture2D texture2D) continue;
                            if (!TextureConverter.TryConvertToBitmap(texture2D, out var bitmap)) continue;

                            var fileName = Path.Combine(OutputFolder, asset.Value.ClassName, asset.Value.Collection.Name, asset.Value.PathID + ".png");
                            Directory.CreateDirectory(Path.GetDirectoryName(fileName)!);
                            using var file = new FileStream(fileName, FileMode.Create);
                            bitmap.SaveAsPng(file);
                            file.Flush();
                            file.Close();

                        }
                        break;
                }

            }
        }

        Directory.CreateDirectory(Path.Combine(OutputFolder, "Categorized"));

        foreach (var (typeName, values) in typeIndex)
        {
            File.WriteAllText(Path.Combine(OutputFolder,"Categorized", typeName + ".json"), JsonConvert.SerializeObject(values, Formatting.Indented));
        }
        Console.WriteLine("Done");
    }
}