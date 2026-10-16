using AmongUs.GameOptions;
using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Neutral;

public sealed class Romeo : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Romeo),
            player => new Romeo(player),
            CustomRoles.Romeo,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            77100,
            SetupOptionItem,
            "re",
            "#9E3047",
            (1, 1),
            assignInfo: new RoleAssignInfo(CustomRoles.Romeo, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1),
                AssignUnitRoles = [CustomRoles.Romeo, CustomRoles.Juliet],
                IsInitiallyAssignableCallBack = () => false
            },
            introSound: () => GetIntroSound(RoleTypes.Phantom),
            combination: CombinationRoles.RomeoJuliet,
            tab: TabGroup.Combinations
        );
    public Romeo(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        JulietAlive = true;
    }
    public static bool JulietAlive;
    enum OptionName
    {

    }

    private static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 1);
    }

    public override void AfterMeetingTasks()
    {
        Juliet.RomeoAlive = Player.IsAlive();
    }
    public override void OnMurderPlayerAsTarget(MurderInfo info)
    {
        if (AddOns.Common.Amnesia.CheckAbilityreturn(Player) || JulietAlive) return;

        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Romeo, byte.MaxValue, hantrole: CustomRoles.Romeo))
        {
            CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Juliet);
            CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Romeo);
        }
    }
    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (AddOns.Common.Amnesia.CheckAbilityreturn(Player) || JulietAlive) return;
        if (!AmongUsClient.Instance.AmHost) return;

        if (!DecidedWinner)
        {
            if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return;

            if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Romeo, byte.MaxValue, hantrole: CustomRoles.Romeo))
            {
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Juliet);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Romeo);
            }
        }
    }
}