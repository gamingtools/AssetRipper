using AssetRipper.Core.Parser.Asset;
using AssetRipper.Core.Parser.Files.SerializedFiles;
using AssetRipper.Core.Project.Collections;
using System;
using System.Collections.Generic;
using Object = AssetRipper.Core.Classes.Object.Object;

namespace AssetRipper.Core.Project.Exporters
{
	public interface IAssetExporter
	{
		bool IsHandle(UnityObjectBase asset);

		bool Export(IExportContainer container, Object asset, string path);
		void Export(IExportContainer container, Object asset, string path, Action<IExportContainer, Object, string> callback);
		bool Export(IExportContainer container, IEnumerable<Object> assets, string path);
		void Export(IExportContainer container, IEnumerable<Object> assets, string path, Action<IExportContainer, Object, string> callback);

		IExportCollection CreateCollection(VirtualSerializedFile virtualFile, Object asset);
		AssetType ToExportType(UnityObjectBase asset);
		bool ToUnknownExportType(ClassIDType classID, out AssetType assetType);
	}
}
