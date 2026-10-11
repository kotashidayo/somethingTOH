using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using MS.Internal.Xml.XPath;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Crewmate;

public struct OkasiState
{
    public int Count { get; set; }
    public int Turn { get; set; }

    public OkasiState(int count, int turn)
    {
        Count = count;
        Turn = turn;
    }
}

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
        IsGiveMode = false;
        Kaisyu = OptionKaisyuCount.GetInt();
        NowKillCool = OptionCooldown.GetFloat();
        OkasiPlayers.Clear();
        OkasiRooms.Clear();
        PetActionManager.Register(Player.PlayerId, OnPet);
    }
    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
    }
    private static OptionItem OptionCount;
    private static OptionItem OptionCooldown;
    private static OptionItem OptionKaisyuCount;
    int count;
    bool IsGiveMode;
    float NowKillCool;
    int Kaisyu;
    enum OptionName
    {
        SweetgifterGiveCount,
        SweetgifterKaisyuCount,
    }

    private static void SetupOptionItem()
    {
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, OptionBaseCoolTime, 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCount = IntegerOptionItem.Create(RoleInfo, 11, OptionName.SweetgifterGiveCount, new(0, 14, 1), 2, false)
            .SetValueFormat(OptionFormat.Players);
        OptionKaisyuCount = IntegerOptionItem.Create(RoleInfo, 12, OptionName.SweetgifterKaisyuCount, new(0, 14, 1), 3, false)
            .SetValueFormat(OptionFormat.Times);
    }
    public bool CanUseSabotageButton() => false;
    public float CalculateKillCooldown() => NowKillCool;
    public bool CanUseImpostorVentButton() => true;
    public override bool CanUseAbilityButton() => !Player.IsModClient() && !IsGiveMode;
    public bool CanUseKillButton() => IsGiveMode;
    public bool IsKiller => false;
    List<byte> OkasiPlayers = new();
    static Dictionary<SystemTypes, List<OkasiState>> OkasiRooms = new();
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
    void OnPet()
    {
        var pr = Player.GetPlainShipRoom().RoomId;
        if (Player.GetPlainShipRoom() is null) return;
        Utils.SendMessage($"[Debug] {pr} にはお菓子データが登録されていません。");

        if (OkasiRooms.TryGetValue(pr, out var current))
        {
            count = count + current.Sum(okasi => okasi.Count);

            if (IsGiveMode)
            {
                Player.SetKillCooldown(NowKillCool);
            }
            else
            {
                Player.RpcProtectedMurderPlayer();
            }

            OkasiRooms.Remove(pr);
        }
    }

    public override void AfterMeetingTasks()
    {
        foreach (var room in OkasiRooms.Keys.ToList())
        {
            var okasiList = OkasiRooms[room];

            var updatedList = new List<OkasiState>();

            foreach (var current in okasiList)
            {
                var nextTurn = current.Turn + 1;

                if (nextTurn < 2)
                {
                    updatedList.Add(new OkasiState(current.Count, nextTurn));
                }
            }

            if (updatedList.Count == 0)
            {
                OkasiRooms.Remove(room);
            }
            else
            {
                OkasiRooms[room] = updatedList;
            }
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
        foreach (var id in OkasiPlayers.ToList())
        {
            var target = PlayerCatch.GetPlayerById(id);
            if (target == null || target.IsAlive()) continue;

            OkasiPlayers.Remove(id);
            var room = target.GetPlainShipRoom();
            if (room == null) continue;

            Utils.KillFlash(Player);
            if (!OkasiRooms.ContainsKey(room.RoomId))
                OkasiRooms[room.RoomId] = new List<OkasiState>();
            OkasiRooms[room.RoomId].Add(new OkasiState(1, 0));
        }

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
        sender.Writer.Write(Kaisyu);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        count = reader.ReadInt32();
        IsGiveMode = reader.ReadBoolean();
        Kaisyu = reader.ReadInt32();
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
