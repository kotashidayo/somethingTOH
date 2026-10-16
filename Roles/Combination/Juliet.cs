using AmongUs.GameOptions;
using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Neutral;

public sealed class Juliet : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Juliet),
            player => new Juliet(player),
            CustomRoles.Juliet,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            77200,
            null,
            "jli",
            "#D4AF37",
            (1, 1),
            assignInfo: new RoleAssignInfo(CustomRoles.Juliet, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(1, 1, 1),
                AssignUnitRoles = [CustomRoles.Romeo, CustomRoles.Juliet],
                IsInitiallyAssignableCallBack = () => false
            },
            combination: CombinationRoles.RomeoJuliet,
            tab: TabGroup.Combinations,
            introSound: () => GetIntroSound(RoleTypes.Phantom)
        );
    public Juliet(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {

    }
    public static bool RomeoAlive;
    public override void AfterMeetingTasks()
    {
        Romeo.JulietAlive = Player.IsAlive();
    }
    public override void OnMurderPlayerAsTarget(MurderInfo info)
    {
        if (AddOns.Common.Amnesia.CheckAbilityreturn(Player) || RomeoAlive) return;

        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Romeo, byte.MaxValue, hantrole: CustomRoles.Romeo))
        {
            CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Romeo);
            CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Juliet);
        }
    }
    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (AddOns.Common.Amnesia.CheckAbilityreturn(Player) || RomeoAlive) return;
        if (!AmongUsClient.Instance.AmHost) return;

        if (!DecidedWinner)
        {
            if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return;

            if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Romeo, byte.MaxValue, hantrole: CustomRoles.Romeo))
            {
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Romeo);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Juliet);
            }
        }
    }
}