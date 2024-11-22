using AssetRipper.Assets;
using AssetRipper.Assets.Metadata;
using AssetRipper.Export.Modules.Textures;
using AssetRipper.Export.PrimaryContent;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Export.UnityProjects.Configuration;
using AssetRipper.Export.UnityProjects.Miscellaneous;
using AssetRipper.Import.Structure.Assembly.Serializable;
using AssetRipper.Mining.PredefinedAssets;
using AssetRipper.Processing;
using AssetRipper.Processing.Textures;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Classes.ClassID_114;
using AssetRipper.SourceGenerated.Classes.ClassID_213;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Classes.ClassID_4;
using AssetRipper.SourceGenerated.Classes.ClassID_49;
using AssetRipper.SourceGenerated.Subclasses.Rectf;
using AssetRipper.SourceGenerated.Subclasses.SpriteData;
using AssetRipper.SourceGenerated.Subclasses.Vector2f;
using AssetRipper.SourceGenerated.Subclasses.Vector4f;
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
    private static readonly HashSet<string> ClassNames = ["MonoBehaviour", "Texture2D", "TextAsset", "GameObject", "Transform", "Sprite"];
    //private const string OutputFolder = @"F:\StarsReach\Extracted";
    //private const string AssetsPath = @"I:\SteamLibrary\steamapps\common\StarsReach";
    private const string OutputFolder = @"F:\Fractured\Data";
    private const string AssetsPath = @"I:\Games\Fractured Online\app\UnityClient@Windows_Data";
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
                    case "Transform":
                        {
                            var assetJson = JsonExtractor.Export(asset.Value);
                            var fileName = Path.Combine(OutputFolder, asset.Value.ClassName, collection.Name, asset.Value.PathID + ".json");
                            Directory.CreateDirectory(Path.GetDirectoryName(fileName)!);
                            File.WriteAllText(fileName, assetJson);

                        }
                        break;
                    case "GameObject":
                        {
                            var assetJson = JsonExtractor.Export(asset.Value);
                            var fileName = Path.Combine(OutputFolder, asset.Value.ClassName, collection.Name, asset.Value.PathID + ".json");
                            Directory.CreateDirectory(Path.GetDirectoryName(fileName)!);
                            File.WriteAllText(fileName, assetJson);


                        }
                        break;
                    case "MonoBehaviour":
                        {
                            if (asset.Value is not IMonoBehaviour monoBehaviour) continue;
                            var assetJson = JsonExtractor.Export(asset.Value);
                            var fileName = Path.Combine(OutputFolder, asset.Value.ClassName, collection.Name, asset.Value.PathID + ".json");
                            Directory.CreateDirectory(Path.GetDirectoryName(fileName)!);
                            File.WriteAllText(fileName, assetJson);
                            if (monoBehaviour.Structure is SerializableStructure structure)
                            {
                                var assetJObject = JObject.Parse(assetJson);
                                var metadata = assetJObject["Metadata"];
                                assetJObject.Remove("Metadata");
                                typeIndex.TryAdd(structure.Type.Name, new List<IndexEntry>());
                                typeIndex[structure.Type.Name].Add(new IndexEntry { Data = assetJObject, Metadata = metadata });
                            }
                        }
                        break;
                    case "TextAsset":
                        {
                            if (asset.Value is not ITextAsset textAsset) continue;
                            var bytes = textAsset.Script_C49.Data.ToArray();
                            var fileName = Path.Combine(OutputFolder, asset.Value.ClassName, asset.Value.GetBestName() + ".file");
                            Directory.CreateDirectory(Path.GetDirectoryName(fileName)!);
                            File.WriteAllBytes(fileName, bytes);
                            break;
                        }

                    case "Sprite":
                        {
                            if (asset.Value is not ISprite sprite) continue;
                            if (sprite.MainAsset is not SpriteInformationObject spriteInformation) continue;
                            var spriteData = new SpriteTextureInformation
                            {
                                Texture = new AssetLink(spriteInformation.Texture.AssetInfo),
                                TextureName = spriteInformation.Texture.Name,
                                Sprites = spriteInformation.Sprites.Select(s => new SpriteAtlasData
                                {
                                    Name = s.Key.Name,
                                    Border = s.Key.Border,
                                    Offset = s.Key.Offset,
                                    Pivot = s.Key.Pivot,
                                    Rect = s.Key.Rect

                                }).ToList()
                            };
                            var fileName = Path.Combine(OutputFolder, asset.Value.ClassName, collection.Name, asset.Value.PathID + ".json");
                            Directory.CreateDirectory(Path.GetDirectoryName(fileName)!);
                            File.WriteAllText(fileName, JsonConvert.SerializeObject(spriteData, Formatting.Indented));
                            break;

                        }

                    case "Texture2D":
                        {
                            if (asset.Value is not ITexture2D texture2D) continue;
                            if (!TextureConverter.TryConvertToBitmap(texture2D, out var bitmap)) continue;

                            var fileName = Path.Combine(OutputFolder, asset.Value.ClassName, collection.Name, asset.Value.PathID + ".png");
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
            File.WriteAllText(Path.Combine(OutputFolder, "Categorized", typeName + ".json"), JsonConvert.SerializeObject(values, Formatting.Indented));
        }
        Console.WriteLine("Done");
    }
}

public class SpriteTextureInformation
{
    public string TextureName { get; set; }
    public AssetLink Texture { get; set; }
    public List<SpriteAtlasData> Sprites { get; set; }
}

public class SpriteAtlasData
{
    public string Name { get; set; }
    public Vector4f Border { get; set; }
    public Vector2f Offset { get; set; }
    public Vector2f Pivot { get; set; }
    public Rectf Rect { get; set; }
}

public class AssetLink(AssetInfo assetInfo)
{
    [JsonProperty("m_Collection")] public string Collection { get; set; } = assetInfo.Collection.Name;
    [JsonProperty("m_PathID")] public long PathId { get; set; } = assetInfo.PathID;
}