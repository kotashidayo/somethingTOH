using System.Collections.Generic;
using AmongUs.GameOptions;

using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Crewmate;

public sealed class Blinder : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Blinder),
            player => new Blinder(player),
            CustomRoles.Blinder,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            40400,
            SetupOptionItem,
            "bld",
            "#883fd1",
            (5, 7),
            from: From.TownOfHost_Y
        );
    public Blinder(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        BlindedPlayers.Clear();
    }
    public override void Add()
    {
        blindPlayer = null;
    }
    public override void OnDestroy()
    {
        if (blindPlayer == null) return;

        BlindedPlayers.Remove(blindPlayer.PlayerId);
        blindPlayer.MarkDirtySettings();

        blindPlayer = null;
    }
    enum OptionName
    {
        BlinderVision
    }
    static OptionItem OptVision;
    public static HashSet<byte> BlindedPlayers = new();
    PlayerControl blindPlayer;
    private static void SetupOptionItem()
    {
        OptVision = FloatOptionItem.Create(RoleInfo, 12, OptionName.BlinderVision, new(0.0f, 3.0f, 0.05f), 0.5f, false)
            .SetValueFormat(OptionFormat.Multiplier);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetFloat(FloatOptionNames.CrewLightMod, OptVision.GetFloat());
    }
    public static void ApplyGameOptionsByOther(byte id, IGameOptions opt)
    {
        if (!CustomRoles.Blinder.IsPresent()) return;

        if (BlindedPlayers.Contains(id))
        {
            opt.SetFloat(FloatOptionNames.CrewLightMod, OptVision.GetFloat());
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, OptVision.GetFloat());
            opt.SetVision(false);
        }
    }
    public override void OnMurderPlayerAsTarget(MurderInfo info)
    {
        var killer = info.AttemptKiller;
        SetVision(killer.PlayerId);
    }
    void SetVision(byte id)
    {
        if (PlayerCatch.GetPlayerById(id) == null || blindPlayer != null) return;
        blindPlayer = PlayerCatch.GetPlayerById(id);
        BlindedPlayers.Add(id);
    }
}