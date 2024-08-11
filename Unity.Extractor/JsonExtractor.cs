using System.Web;
using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Metadata;
using AssetRipper.Export.PrimaryContent;
using AssetRipper.Import.Structure.Assembly.Serializable;
using AssetRipper.SourceGenerated.Classes.ClassID_114;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Unity.Extractor;

 internal class JsonWalker(AssetCollection collection) : DefaultJsonWalker
{
    public override void VisitPPtr<TAsset>(PPtr<TAsset> pptr)
    {
        var targetCollection = pptr.FileID >= 0 && pptr.FileID < collection.Dependencies.Count && pptr.PathID != 0
            ? collection.Dependencies[pptr.FileID]
            : null;

        if (targetCollection is null)
        {
            base.VisitPPtr(pptr);
        }
        else
        {
            Writer.Write("{ \"m_Collection\": \"");
            Writer.Write(HttpUtility.JavaScriptStringEncode(collection.Name));
            Writer.Write("\", \"m_PathID\": ");
            Writer.Write(pptr.PathID);
            Writer.Write(" }");
        }
    }
}

internal class JsonExtractor
{
    public static string Export(IUnityObjectBase asset)
    {
        var json = new JsonWalker(asset.Collection).SerializeStandard(asset);
        var jObject = JObject.Parse(json);

        string name = null;
        string ns = null;

        if (asset is IMonoBehaviour { Structure: SerializableStructure serializableStructure })
        {
            name = serializableStructure.Type.Name;
            ns = serializableStructure.Type.Namespace;
        }

        //if (name == null) return null;

        var meta = new Metadata
        {
            Name = asset.GetBestName(),
            Collection = asset.Collection.Name,
            PathId = asset.PathID,
            StructureName = name,
            StructureNamespace = ns
        };
        var metaObject = JObject.FromObject(meta);
        jObject.Add("Metadata", metaObject);
        return JsonConvert.SerializeObject(jObject, Formatting.Indented);
    }
}