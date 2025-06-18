using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace HandyHotbarHelper;

public class Services
{
	[PluginService]
	public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

	[PluginService]
	public static IClientState ClientState { get; private set; } = null!;

	[PluginService]
	public static ICommandManager CommandManager { get; private set; } = null!;

	[PluginService]
	public static IDataManager DataManager { get; private set; } = null!;

	[PluginService]
	public static IPluginLog PluginLog { get; private set; } = null!;

	[PluginService]
	public static IAddonLifecycle AddonLifecycle { get; private set; } = null!;

	[PluginService]
	public static IFramework Framework { get; private set; } = null!;

	[PluginService]
	public static ITextureProvider TextureProvider { get; private set; } = null!;
}
