using AssetRipper.GUI.Web.Documentation;
using AssetRipper.GUI.Web.Pages;
using AssetRipper.GUI.Web.Pages.Assets;
using AssetRipper.GUI.Web.Pages.Bundles;
using AssetRipper.GUI.Web.Pages.Collections;
using AssetRipper.GUI.Web.Pages.Resources;
using AssetRipper.GUI.Web.Pages.Scenes;
using AssetRipper.GUI.Web.Pages.Settings;
using AssetRipper.GUI.Web.Paths;
using AssetRipper.Import.Logging;
using AssetRipper.Web.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.OpenApi.Models;
using System.CommandLine;
using System.Diagnostics;

namespace AssetRipper.GUI.Web;

public static class WebApplicationLauncher
{
	private static class Defaults
	{
		public const int Port = 0;
		public const bool LaunchBrowser = true;
	}

	public static void Launch(string[] args)
	{
		RootCommand rootCommand = new() { Description = "AssetRipper" };

		Option<int> portOption = new Option<int>(
			name: "--port",
			description: "If nonzero, the application will attempt to host on this port, instead of finding a random unused port.",
			getDefaultValue: () => Defaults.Port);
		rootCommand.AddOption(portOption);

		Option<bool> launchBrowserOption = new Option<bool>(
			name: "--launch-browser",
			description: "If true, a browser window will be launched automatically.",
			getDefaultValue: () => Defaults.LaunchBrowser);
		rootCommand.AddOption(launchBrowserOption);

		Option<string[]> localWebFilesOption = new Option<string[]>(
			name: "--local-web-file",
			description: "Files provided with this option will replace online sources.",
			getDefaultValue: () => []);
		rootCommand.AddOption(localWebFilesOption);

		bool shouldRun = false;
		int port = Defaults.Port;
		bool launchBrowser = Defaults.LaunchBrowser;

		rootCommand.SetHandler((int portParsed, bool launchBrowserParsed, string[] localWebFilesParsed) =>
		{
			shouldRun = true;
			port = portParsed;
			launchBrowser = launchBrowserParsed;
			foreach (string localWebFile in localWebFilesParsed)
			{
				if (File.Exists(localWebFile))
				{
					string fileName = Path.GetFileName(localWebFile);
					string webPrefix = Path.GetExtension(fileName) switch
					{
						".css" => "/css/",
						".js" => "/js/",
						_ => "/"
					};
					StaticContentLoader.Cache.TryAdd(webPrefix + fileName, File.ReadAllBytes(localWebFile));
				}
				else
				{
					Console.WriteLine($"File '{localWebFile}' does not exist.");
				}
			}
		}, portOption, launchBrowserOption, localWebFilesOption);

		rootCommand.Invoke(args);

		if (shouldRun)
		{
			Launch(port, launchBrowser);
		}
	}

	public static void Launch(int port = Defaults.Port, bool launchBrowser = Defaults.LaunchBrowser)
	{
		WelcomeMessage.Print();

		Logger.Add(new FileLogger());
		Logger.LogSystemInformation("AssetRipper");
		Logger.Add(new ConsoleLogger());

		WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions()
		{
#if DEBUG
			EnvironmentName = Environments.Development,
#else
			EnvironmentName = Environments.Production,
#endif
		});

		builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

		builder.Services.AddTransient<ErrorHandlingMiddleware>(static (_) => new());
		builder.Services.ConfigureHttpJsonOptions(options =>
		{
			options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
			options.SerializerOptions.TypeInfoResolverChain.Insert(1, PathSerializerContext.Default);
			options.SerializerOptions.TypeInfoResolverChain.Insert(2, NullSerializerContext.Instance);
		});

		builder.Services.AddOpenApi(options =>
		{
			options.AddOperationTransformer(new ClearOperationTagsTransformer());
			options.AddOperationTransformer(new InsertionOperationTransformer());
			options.AddDocumentTransformer(new ClearDocumentTagsTransformer());
			options.AddDocumentTransformer(new SortDocumentPathsTransformer());
		});

		builder.Services.AddEndpointsApiExplorer();

		builder.Logging.ConfigureLoggingLevel();

		WebApplication app = builder.Build();

		// Configure the HTTP request pipeline.
#if !DEBUG
		app.UseMiddleware<ErrorHandlingMiddleware>();
#endif
		if (launchBrowser)
		{
			app.Lifetime.ApplicationStarted.Register(() =>
			{
				string? address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();
				if (address is not null)
				{
					OpenUrl(address);
				}
			});
		}

		app.MapOpenApi(DocumentationPaths.OpenApi);
		app.UseSwaggerUI(c =>
		{
			// Point to the static OpenAPI file
			c.SwaggerEndpoint(DocumentationPaths.OpenApi, "AssetRipper API");
		});

		//Static files
		app.MapStaticFile("/favicon.ico", "image/x-icon");
		app.MapStaticFile("/css/site.css", "text/css");
		app.MapStaticFile("/js/site.js", "text/javascript");
		app.MapStaticFile("/js/commands_page.js", "text/javascript");
		app.MapStaticFile("/js/mesh_preview.js", "text/javascript");

		//Normal Pages
		app.MapGet("/", (context) =>
		{
			context.Response.DisableCaching();
			return IndexPage.Instance.WriteToResponse(context.Response);
		})
			.WithSummary("The home page")
			.ProducesHtmlPage();
		app.MapGet("/Commands", CommandsPage.Instance.ToResult).ProducesHtmlPage();
		app.MapGet("/Privacy", PrivacyPage.Instance.ToResult).ProducesHtmlPage();
		app.MapGet("/Licenses", LicensesPage.Instance.ToResult).ProducesHtmlPage();

		app.MapGet("/ConfigurationFiles", (context) =>
		{
			context.Response.DisableCaching();
			return ConfigurationFilesPage.Instance.WriteToResponse(context.Response);
		}).ProducesHtmlPage();
		app.MapPost("/ConfigurationFiles/Singleton/Add", ConfigurationFilesPage.HandleSingletonAddPostRequest);
		app.MapPost("/ConfigurationFiles/Singleton/Remove", ConfigurationFilesPage.HandleSingletonRemovePostRequest);
		app.MapPost("/ConfigurationFiles/List/Add", ConfigurationFilesPage.HandleListAddPostRequest);
		app.MapPost("/ConfigurationFiles/List/Remove", ConfigurationFilesPage.HandleListRemovePostRequest);
		app.MapPost("/ConfigurationFiles/List/Replace", ConfigurationFilesPage.HandleListReplacePostRequest);

		app.MapGet("/Settings/Edit", (context) =>
		{
			context.Response.DisableCaching();
			return SettingsPage.Instance.WriteToResponse(context.Response);
		}).ProducesHtmlPage();
		app.MapPost("/Settings/Update", SettingsPage.HandlePostRequest);

		//Assets
		app.MapGet(AssetAPI.Urls.View, AssetAPI.GetView).ProducesHtmlPage();
		app.MapGet(AssetAPI.Urls.Image, AssetAPI.GetImageData)
			.Produces<byte[]>(contentType: "application/octet-stream")
			.WithAssetPathParameter()
			.WithImageExtensionParameter();
		app.MapGet(AssetAPI.Urls.Audio, AssetAPI.GetAudioData)
			.Produces<byte[]>(contentType: "application/octet-stream")
			.WithAssetPathParameter();
		app.MapGet(AssetAPI.Urls.Model, AssetAPI.GetModelData)
			.Produces<byte[]>(contentType: "application/octet-stream")
			.WithAssetPathParameter();
		app.MapGet(AssetAPI.Urls.Font, AssetAPI.GetFontData)
			.Produces<byte[]>(contentType: "application/octet-stream")
			.WithAssetPathParameter();
		app.MapGet(AssetAPI.Urls.Video, AssetAPI.GetVideoData)
			.Produces<byte[]>(contentType: "application/octet-stream")
			.WithAssetPathParameter();
		app.MapGet(AssetAPI.Urls.Json, AssetAPI.GetJson)
			.Produces<string>(contentType: "application/json")
			.WithAssetPathParameter();
		app.MapGet(AssetAPI.Urls.Yaml, AssetAPI.GetYaml)
			.Produces<string>(contentType: "text/yaml")
			.WithAssetPathParameter();
		app.MapGet(AssetAPI.Urls.Text, AssetAPI.GetText)
			.Produces<string>(contentType: "text/plain")
			.WithAssetPathParameter();
		app.MapGet(AssetAPI.Urls.Binary, AssetAPI.GetBinaryData)
			.Produces<byte[]>(contentType: "application/octet-stream")
			.WithAssetPathParameter();

		//Bundles
		app.MapGet(BundleAPI.Urls.View, BundleAPI.GetView).ProducesHtmlPage();

		//Collections
		app.MapGet(CollectionAPI.Urls.View, CollectionAPI.GetView).ProducesHtmlPage();
		app.MapGet(CollectionAPI.Urls.Count, CollectionAPI.GetCount)
			.WithSummary("Get the number of elements in the collection.")
			.Produces<int>();

		//Resources
		app.MapGet(ResourceAPI.Urls.View, ResourceAPI.GetView).ProducesHtmlPage();
		app.MapGet(ResourceAPI.Urls.Data, ResourceAPI.GetData)
			.Produces<byte[]>(contentType: "application/octet-stream");

		//Scenes
		app.MapGet(SceneAPI.Urls.View, SceneAPI.GetView).ProducesHtmlPage();

		app.MapPost("/Localization", (context) =>
		{
			context.Response.DisableCaching();
			if (context.Request.Query.TryGetValue("code", out StringValues code))
			{
				string? language = code;
				if (language is not null && LocalizationLoader.LanguageNameDictionary.ContainsKey(language))
				{
					Localization.LoadLanguage(language);
				}
			}
			return Results.Redirect("/").ExecuteAsync(context);
		})
			.WithQueryStringParameter("Code", "Language code", true)
			.Produces(StatusCodes.Status302Found);

		//Commands
		app.MapPost("/Export/UnityProject", Commands.HandleCommand<Commands.ExportUnityProject>)
			.WithQueryStringParameter("Path")
			.Produces(StatusCodes.Status302Found);
		app.MapPost("/Export/PrimaryContent", Commands.HandleCommand<Commands.ExportPrimaryContent>)
			.WithQueryStringParameter("Path")
			.Produces(StatusCodes.Status302Found);
		app.MapPost("/LoadFile", Commands.HandleCommand<Commands.LoadFile>)
			.WithQueryStringParameter("Path")
			.Produces(StatusCodes.Status302Found);
		app.MapPost("/LoadFolder", Commands.HandleCommand<Commands.LoadFolder>)
			.WithQueryStringParameter("Path")
			.Produces(StatusCodes.Status302Found);
		app.MapPost("/Reset", Commands.HandleCommand<Commands.Reset>)
			.WithQueryStringParameter("Path")
			.Produces(StatusCodes.Status302Found);

		//Dialogs
		app.MapGet("/Dialogs/SaveFile", Dialogs.SaveFile.HandleGetRequest).Produces<string>();
		app.MapGet("/Dialogs/OpenFolder", Dialogs.OpenFolder.HandleGetRequest).Produces<string>();
		app.MapGet("/Dialogs/OpenFolders", Dialogs.OpenFolders.HandleGetRequest).Produces<string>();
		app.MapGet("/Dialogs/OpenFile", Dialogs.OpenFile.HandleGetRequest).Produces<string>();
		app.MapGet("/Dialogs/OpenFiles", Dialogs.OpenFiles.HandleGetRequest).Produces<string>();

		//File API
		app.MapGet("/IO/File/Exists", (context) =>
		{
			context.Response.DisableCaching();
			if (context.Request.Query.TryGetValue("Path", out StringValues path))
			{
				bool exists = File.Exists(path);
				return Results.Json(exists, AppJsonSerializerContext.Default.Boolean).ExecuteAsync(context);
			}
			else
			{
				return Results.BadRequest().ExecuteAsync(context);
			}
		})
			.Produces<bool>()
			.WithQueryStringParameter("Path", required: true);

		app.MapGet("/IO/Directory/Exists", (context) =>
		{
			context.Response.DisableCaching();
			if (context.Request.Query.TryGetValue("Path", out StringValues path))
			{
				bool exists = Directory.Exists(path);
				return Results.Json(exists, AppJsonSerializerContext.Default.Boolean).ExecuteAsync(context);
			}
			else
			{
				return Results.BadRequest().ExecuteAsync(context);
			}
		})
			.Produces<bool>()
			.WithQueryStringParameter("Path", required: true);

		app.MapGet("/IO/Directory/Empty", (context) =>
		{
			context.Response.DisableCaching();
			if (context.Request.Query.TryGetValue("Path", out StringValues stringValues))
			{
				string? path = stringValues;
				bool empty = !Directory.Exists(path) || !Directory.EnumerateFileSystemEntries(path).Any();
				return Results.Json(empty, AppJsonSerializerContext.Default.Boolean).ExecuteAsync(context);
			}
			else
			{
				return Results.BadRequest().ExecuteAsync(context);
			}
		})
			.Produces<bool>()
			.WithQueryStringParameter("Path", required: true);

		app.Run();
	}

	private static ILoggingBuilder ConfigureLoggingLevel(this ILoggingBuilder builder)
	{
		builder.Services.Add(ServiceDescriptor.Singleton<IConfigureOptions<LoggerFilterOptions>>(
			new LifetimeOrWarnConfigureOptions()));
		return builder;
	}

	private sealed class LifetimeOrWarnConfigureOptions : ConfigureOptions<LoggerFilterOptions>
	{
		public LifetimeOrWarnConfigureOptions() : base(AddRule)
		{
		}

		private static void AddRule(LoggerFilterOptions options)
		{
			options.Rules.Add(new LoggerFilterRule(null, null, LogLevel.Information, static (provider, category, logLevel) =>
			{
				return category is "Microsoft.Hosting.Lifetime" || logLevel >= LogLevel.Warning;
			}));
		}
	}

	private static void OpenUrl(string url)
	{
		try
		{
			if (OperatingSystem.IsWindows())
			{
				Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
			}
			else if (OperatingSystem.IsLinux())
			{
				Process.Start("xdg-open", url);
			}
			else if (OperatingSystem.IsMacOS())
			{
				Process.Start("open", url);
			}
		}
		catch (Exception ex)
		{
			Logger.Error($"Failed to launch web browser for: {url}", ex);
		}
	}

	private static RouteHandlerBuilder MapGet(this IEndpointRouteBuilder endpoints, [StringSyntax("Route")] string pattern, RequestDelegate requestDelegate)
	{
		// RouteHandlerBuilder is always returned from this method.
		// We cast it, so we can access Produces<T> and similar methods.
		RouteHandlerBuilder mapped = (RouteHandlerBuilder)EndpointRouteBuilderExtensions.MapGet(endpoints, pattern, requestDelegate);

		// We need to add MethodInfo to the metadata, so that it will be used in the api explorer.
		// https://github.com/dotnet/aspnetcore/issues/44005#issuecomment-1248717069
		// https://github.com/dotnet/aspnetcore/issues/44970
		return mapped.WithMetadata(requestDelegate.Method);
	}

	private static RouteHandlerBuilder MapPost(this IEndpointRouteBuilder endpoints, [StringSyntax("Route")] string pattern, RequestDelegate requestDelegate)
	{
		// RouteHandlerBuilder is always returned from this method.
		// We cast it, so we can access Produces<T> and similar methods.
		RouteHandlerBuilder mapped = (RouteHandlerBuilder)EndpointRouteBuilderExtensions.MapPost(endpoints, pattern, requestDelegate);

		// We need to add MethodInfo to the metadata, so that it will be used in the api explorer.
		// https://github.com/dotnet/aspnetcore/issues/44005#issuecomment-1248717069
		// https://github.com/dotnet/aspnetcore/issues/44970
		return mapped.WithMetadata(requestDelegate.Method);
	}

	private static RouteHandlerBuilder MapGet(this IEndpointRouteBuilder endpoints, [StringSyntax("Route")] string pattern, Func<IResult> handler)
	{
		return endpoints.MapGet(pattern, (context) =>
		{
			IResult result = handler.Invoke();
			return result.ExecuteAsync(context);
		});
	}

	private static RouteHandlerBuilder MapStaticFile(this IEndpointRouteBuilder endpoints, [StringSyntax("Route")] string path, string contentType)
	{
		return endpoints.MapGet(path, async (context) =>
		{
			string fileName = Path.GetFileName(path);
			byte[] data = await StaticContentLoader.Load(path);
			await Results.Bytes(data, contentType, fileName).ExecuteAsync(context);
		}).Produces<byte[]>(StatusCodes.Status200OK, contentType);
	}
}
