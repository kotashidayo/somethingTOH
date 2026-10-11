using AmongUs.GameOptions;
using Hazel;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Crewmate;

public sealed class Sweetgifter : RoleBase, IKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Sweetgifter),
            player => new Sweetgifter(player),
            CustomRoles.Sweetgifter,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            40300,
            SetupOptionItem,
            "sgf",
            "#FF6600",
            (7, 6)
        );
    public Sweetgifter(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        count = OptionCount.GetInt();
        cooldown = OptionCooldown.GetInt();
        IsGiveMode = false;
        NowKillCool = OptionCooldown.GetFloat();
        OkasiPlayers.Clear();
        OkasiRooms.Clear();
    }

    private static OptionItem OptionCount;
    private static OptionItem OptionCooldown;
    static int cooldown;
    int count;
    bool IsGiveMode;
    float NowKillCool;
    enum OptionName
    {

    }

    private static void SetupOptionItem()
    {
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, OptionBaseCoolTime, 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCount = IntegerOptionItem.Create(RoleInfo, 11, GeneralOption.OptionCount, new(0, 99, 1), 2, false)
            .SetValueFormat(OptionFormat.Times).SetZeroNotation(OptionZeroNotation.Infinity);
    }
    public bool CanUseSabotageButton() => false;
    public float CalculateKillCooldown() => NowKillCool;
    public bool CanUseImpostorVentButton() => true;
    public override bool CanUseAbilityButton() => !Player.IsModClient() && !IsGiveMode;
    public bool CanUseKillButton() => IsGiveMode;
    public bool IsKiller => false;
    List<byte> OkasiPlayers = new();
    static List<PlainShipRoom> OkasiRooms = new();
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        info.DoKill = false;
        var target = info.AttemptTarget;
        if (OkasiPlayers.Contains(target.PlayerId)) return;

        OkasiPlayers.Add(target.PlayerId);
        --count;
        NowKillCool = OptionCooldown.GetFloat();
        Player.SetKillCooldown(NowKillCool);
        if (count <= 0)
        {
            ModeSwitching(RoleTypes.Crewmate);
        }
    }
    public override void OnDead(PlayerControl player)
    {
        if (OkasiPlayers.Contains(player.PlayerId))
        {
            Utils.KillFlash(Player);
            OkasiPlayers.Remove(player.PlayerId);
        }
    }
    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        if (OkasiPlayers == null) return "";
        if (!Is(seer)) return "";

        if (OkasiPlayers.Contains(seen.PlayerId))
        {
            return $"<{RoleInfo.RoleColorCode}>★</color>";
        }
        return "";
    }
    public override bool OnEnterVent(PlayerPhysics physics, int ventId)
    {
        if (count <= 0 && !IsGiveMode) return false;

        ModeSwitching();
        return false;
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (NowKillCool <= 0f)
        {
            NowKillCool = 0f;
            return;
        }
        NowKillCool -= Time.fixedDeltaTime;
    }
    void SetRoleForSweetgifterClient(PlayerControl target, RoleTypes role, int clientId)
    {
        if (target == null || !target.IsAlive()) return;

        if (target == PlayerControl.LocalPlayer && Is(PlayerControl.LocalPlayer))
        {
            RoleManager.Instance.SetRole(target, role);
        }
        target.RpcSetRoleDesync(role, clientId);
    }
    void ModeSwitching(RoleTypes type = RoleTypes.CrewmateGhost)
    {
        if (type == RoleTypes.CrewmateGhost)
        {
            IsGiveMode = !IsGiveMode;
            if (IsGiveMode)
            {
                SetRoleForSweetgifterClient(Player, RoleTypes.Impostor, Player.GetClientId());
                Player.SetKillCooldown(NowKillCool);
            }
            else
            {
                SetRoleForSweetgifterClient(Player, RoleTypes.Engineer, Player.GetClientId());
            }
        }
        else
        {
            switch (type)
            {
                case RoleTypes.Crewmate:
                    IsGiveMode = false;
                    type = RoleTypes.Engineer;
                    break;

                case RoleTypes.Impostor:
                    IsGiveMode = true;
                    break;
            }
            SetRoleForSweetgifterClient(Player, type, Player.GetClientId());
            if (type is RoleTypes.Impostor)
            {
                Player.SetKillCooldown(NowKillCool);
            }
        }
        SendRPC();
    }
    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(count);
        sender.Writer.Write(IsGiveMode);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        count = reader.ReadInt32();
        IsGiveMode = reader.ReadBoolean();
    }
    public override bool CanVentMoving(PlayerPhysics physics, int ventId) => false;
    public override bool CanClickUseVentButton => count > 0;
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = 0.1f;
        AURoleOptions.EngineerInVentMaxTime = 1;
    }
    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        var text = $"({count})";
        return count > 0 ? Utils.ColorString(Color.yellow, text) : Utils.ColorString(Color.gray, text);
    }
}
