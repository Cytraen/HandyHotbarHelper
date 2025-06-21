using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using HandyHotbarHelper.Windows;

namespace HandyHotbarHelper;

public unsafe class Plugin : IDalamudPlugin
{
	private const string CommandName = "/hhh";
	private readonly WindowSystem _windowSystem;
	private readonly ActionMenuWindow _actionMenuWindow;

	public Dictionary<uint, uint> ActionAdjustmentCache { get; private set; } = [];
	public Dictionary<uint, List<uint>> ActionAdjustmentSets { get; private set; } = [];

	public Plugin(IDalamudPluginInterface pluginInterface)
	{
		pluginInterface.Create<Services>();

		_windowSystem = new WindowSystem("HandyHotbarHelper");
		_actionMenuWindow = new ActionMenuWindow(this);
		_windowSystem.AddWindow(_actionMenuWindow);

		Services.PluginInterface.UiBuilder.Draw += DrawUi;
		Services.PluginInterface.UiBuilder.OpenMainUi += OpenMainUi;

		Services.CommandManager.AddHandler(
			CommandName,
			new CommandInfo(OnCommand) { HelpMessage = "Opens the game's Actions & Traits window." }
		);

		Services.AddonLifecycle.RegisterListener(
			AddonEvent.PreFinalize,
			"ActionMenu",
			PreFinalizeActionMenu
		);

		Services.AddonLifecycle.RegisterListener(
			AddonEvent.PostSetup,
			"ActionMenu",
			PostSetupActionMenu
		);

		Services.ClientState.ClassJobChanged += OnClassJobChanged;
		Services.ClientState.LevelChanged += OnLevelChanged;
		Services.Framework.Update += OnFrameworkUpdate;

		if (AgentActionMenu.Instance() != null && AgentActionMenu.Instance()->IsAddonShown())
		{
			OpenMainWindow();
		}
	}

	private void OpenMainWindow()
	{
		UpdateActionsLists();
		_actionMenuWindow.IsOpen = true;
	}

	private void OnLevelChanged(uint classJobId, uint level)
	{
		var agent = AgentActionMenu.Instance();
		if (agent != null && agent->IsAddonShown())
		{
			UpdateActionsLists();
		}
	}

	private void OnClassJobChanged(uint classJobId)
	{
		var agent = AgentActionMenu.Instance();
		if (agent != null && agent->IsAddonShown())
		{
			UpdateActionsLists();
		}
	}

	private void OnFrameworkUpdate(IFramework framework)
	{
		var atkMgr = RaptureAtkUnitManager.Instance();
		if (atkMgr == null)
			return;

		var addon = atkMgr->GetAddonByName("ActionMenu");
		if (addon == null)
			return;

		_actionMenuWindow.AddonPosition = new Vector2(addon->X, addon->Y);

		_actionMenuWindow.AddonSize = new Vector2(
			addon->GetScaledWidth(true),
			addon->GetScaledHeight(true)
		);

		_actionMenuWindow.SizeConstraints = new Window.WindowSizeConstraints
		{
			MinimumSize = _actionMenuWindow.SizeConstraints?.MinimumSize ?? Vector2.Zero,
			MaximumSize = new Vector2(float.MaxValue, addon->GetScaledHeight(true)),
		};
	}

	private void UpdateActionsLists()
	{
		Services.Framework.RunOnTick(ActuallyUpdate);
		return;

		void ActuallyUpdate()
		{
			var atkMgr = RaptureAtkUnitManager.Instance();
			var playerState = PlayerState.Instance();
			if (
				atkMgr == null
				|| playerState == null
				|| Services.ClientState.LocalPlayer?.ClassJob.ValueNullable?.ExpArrayIndex
					is not { } expArrayIndex
			)
			{
				return;
			}

			var addon = atkMgr->GetAddonByName("ActionMenu");
			if (addon == null)
			{
				return;
			}

			var agent = AgentActionMenu.Instance();
			var am = ActionManager.Instance();

			if (agent == null || am == null)
				return;

			var equivalentActions = new Dictionary<uint, List<uint>>();
			var actionAdjustmentCache = new Dictionary<uint, uint>();
			var allActionData = agent
				->ClassJobActionList
				//.UnionBy(agent->GeneralList, x => x.ActionId)
				.UnionBy(agent->GatheringRoleActionList, x => x.ActionId)
				.UnionBy(agent->CombatRoleActionList, x => x.ActionId)
				//.UnionBy(agent->DutyActionList, x => x.ActionId)
				.Where(x => x.IsSlotable && x.Level <= playerState->ClassJobLevels[expArrayIndex])
				.ToList();

			foreach (var action in allActionData)
			{
				var adjusted = am->GetAdjustedActionId(action.ActionId);
				actionAdjustmentCache[action.ActionId] = adjusted;

				if (!equivalentActions.TryGetValue(adjusted, out var value))
				{
					value = [];
					equivalentActions.Add(adjusted, value);
				}

				value.Add(action.ActionId);
			}

			ActionAdjustmentSets = equivalentActions;
			ActionAdjustmentCache = actionAdjustmentCache;
		}
	}

	private void PostSetupActionMenu(AddonEvent type, AddonArgs args)
	{
		OpenMainWindow();
	}

	private void PreFinalizeActionMenu(AddonEvent type, AddonArgs args)
	{
		_actionMenuWindow.IsOpen = false;
	}

	private void DrawUi() => _windowSystem.Draw();

	private static void OnCommand(string command, string args)
	{
		OpenMainUi();
	}

	private static void OpenMainUi()
	{
		var agent = AgentActionMenu.Instance();
		if (agent == null)
		{
			return;
		}

		agent->Show();
	}

	public void Dispose()
	{
		Services.AddonLifecycle.UnregisterListener(PostSetupActionMenu, PreFinalizeActionMenu);

		Services.CommandManager.RemoveHandler(CommandName);

		Services.PluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
		Services.PluginInterface.UiBuilder.Draw -= DrawUi;

		_windowSystem.RemoveAllWindows();
	}
}
