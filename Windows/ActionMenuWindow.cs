using System.Numerics;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace HandyHotbarHelper.Windows;

public class ActionMenuWindow : Window
{
	private readonly Plugin _plugin;
	public Vector2 AddonPosition;
	public Vector2 AddonSize;
	private List<ActionData> MissingActions { get; set; } = [];

	public ActionMenuWindow(Plugin plugin)
		: base("Handy Hotbar Helper")
	{
		_plugin = plugin;
		Size = Vector2.Zero;
		SizeConstraints = new WindowSizeConstraints();
		ShowCloseButton = false;
		ForceMainWindow = true;
		Flags =
			ImGuiWindowFlags.NoMove
			| ImGuiWindowFlags.AlwaysAutoResize
			| ImGuiWindowFlags.NoCollapse
			| ImGuiWindowFlags.NoResize
			| ImGuiWindowFlags.NoFocusOnAppearing;
	}

	public override unsafe void Update()
	{
		var atkMgr = RaptureAtkUnitManager.Instance();
		if (atkMgr == null)
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
		var hotbarModule = RaptureHotbarModule.Instance();

		if (agent == null || am == null || hotbarModule == null)
			return;

		var hotbars =
			(hotbarModule->CrossHotbarFlags & CrossHotbarFlags.Active) == 0
				? hotbarModule->StandardHotbars
				: hotbarModule->CrossHotbars;

		var hotbarActionIds = new List<uint>();

		foreach (var hotbar in hotbars)
		{
			foreach (var slot in hotbar.Slots)
			{
				if (
					slot
					is not {
						IsEmpty: false,
						OriginalApparentSlotType: RaptureHotbarModule.HotbarSlotType.Action,
						OriginalApparentActionId: not 0
					}
				)
				{
					continue;
				}

				hotbarActionIds.Add(slot.OriginalApparentActionId);
			}
		}

		var order = new List<uint>();
		var currentTab = *(uint*)((nint)agent + 0x48);

		var actionList = currentTab switch
		{
			0 => agent->ClassJobActionList,
			2 => agent->GatheringRoleActionList,
			8 => agent->CombatRoleActionList,
			_ => [],
		};

		for (var i = 0; i < actionList.Count; i++)
		{
			var atkValuesIndex = 16 + i * 8;
			var actionAtkValue = addon->AtkValuesSpan[atkValuesIndex];
			if (actionAtkValue.Type != ValueType.UInt)
				break;
			if ((addon->AtkValuesSpan[atkValuesIndex + 4].UInt & 0x100) != 0)
				continue;

			order.Add(actionAtkValue.UInt);
		}

		var missingActions = order
			.Join(actionList, x => x, y => y.ActionId, (_, y) => y)
			.Where(x =>
				x.IsSlotable
				&& _plugin.ActionAdjustmentCache.TryGetValue(x.ActionId, out var adjusted)
				&& !_plugin.ActionAdjustmentSets[adjusted].Any(y => hotbarActionIds.Contains(y))
			)
			.DistinctBy(x => x.ActionId)
			.ToList();

		MissingActions = missingActions;
	}

	public override bool DrawConditions()
	{
		return base.DrawConditions() && MissingActions.Count != 0;
	}

	public override void PreDraw()
	{
		base.PreDraw();

		SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(
				ImGui.CalcTextSize(WindowName).X + 50 * ImGuiHelpers.GlobalScale,
				0
			),
			MaximumSize = SizeConstraints!.Value.MaximumSize,
		};
	}

	public override unsafe void Draw()
	{
		var atkMgr = RaptureAtkUnitManager.Instance();
		if (atkMgr == null)
			return;

		var agent = AgentActionMenu.Instance();
		if (agent == null)
			return;

		var addon = atkMgr->GetAddonByName("ActionMenu");
		if (addon == null)
			return;

		for (var i = 0; i < MissingActions.Count; i++)
		{
			if (addon->Param == 41 && i == 0)
			{
				ImGui.TextUnformatted("Role Actions");
			}
			else if (agent->CompactView)
			{
				if (
					MissingActions.FirstOrDefault(x => x.ActionCategoryId == 3).ActionId
					== MissingActions[i].ActionId
				)
				{
					ImGui.TextUnformatted("Weaponskills");
				}
				else if (
					MissingActions.FirstOrDefault(x => x.ActionCategoryId == 2).ActionId
					== MissingActions[i].ActionId
				)
				{
					ImGui.TextUnformatted("Spells");
				}
				else if (
					MissingActions.FirstOrDefault(x => x.ActionCategoryId == 4).ActionId
					== MissingActions[i].ActionId
				)
				{
					ImGui.TextUnformatted("Abilities");
				}
			}

			var tex = Services.TextureProvider.GetFromGameIcon(
				new GameIconLookup(MissingActions[i].IconId)
			);
			var wrap = tex.GetWrapOrEmpty();
			var str = MissingActions[i].DisplayString.ToString().Trim();
			var scaledIconSize = wrap.Size / 2.0f;
			ImGui.Image(wrap.ImGuiHandle, scaledIconSize);
			ImGui.SameLine();
			ImGui.SetCursorPosY(
				ImGui.GetCursorPosY()
					+ (
						scaledIconSize.Y
						- ImGui.GetStyle().FramePadding.Y / 2
						- ImGui.CalcTextSize(str).Y
					) / 2
			);

			ImGui.TextUnformatted(str);

			if (MissingActions.Count > i + 1)
			{
				ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 5 * ImGuiHelpers.GlobalScale);
			}
		}

		EndOfDraw();
	}

	private void EndOfDraw()
	{
		var windowWidth = ImGui.GetWindowWidth();
		var mainViewPortSize = ImGui.GetMainViewport().Size;
		if (windowWidth + AddonPosition.X + AddonSize.X > mainViewPortSize.X)
		{
			Position = AddonPosition with { X = AddonPosition.X - windowWidth };
		}
		else
		{
			Position = AddonPosition with { X = AddonPosition.X + AddonSize.X };
		}
	}

	public override void OnClose()
	{
		base.OnClose();
		MissingActions.Clear();
		_plugin.ActionAdjustmentCache.Clear();
		_plugin.ActionAdjustmentSets.Clear();
	}
}
