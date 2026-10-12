using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Epic.OnlineServices.RTC;
using Hazel;
using MS.Internal.Xml.XPath;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Crewmate;

public class OkasiState
{
    public int Count { get; set; }
    public int Turn { get; set; }
    public bool Okasi { get; set; }
    public PlayerControl Player;
    public OkasiState(int count, int turn, bool okasi, PlayerControl player)
    {
        Count = count;
        Turn = turn;
        Okasi = okasi;
        Player = player;
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
        Notify.Clear();
        Notifytaisyo.Clear();
    }
    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
    }
    private static OptionItem OptionCount;
    private static OptionItem OptionCooldown;
    private static OptionItem OptionKaisyuCount;
    private static OptionItem OptionKillFlash;
    private static OptionItem OptionNotifytaisyo;
    private static OptionItem OptionNotifyKiller;
    private static OptionItem OptionKaisyuSonoturn;
    int count;
    bool IsGiveMode;
    float NowKillCool;
    int Kaisyu;
    enum OptionName
    {
        SweetgifterGiveCount,
        SweetgifterKaisyuCount,
        SweetgifterKillFlash,
        SweetgifterNotifyTaisyo,
        SweetgifterNotifyKiller,
        SweetgifterCanKaisyu
    }

    private static void SetupOptionItem()
    {
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, OptionBaseCoolTime, 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCount = IntegerOptionItem.Create(RoleInfo, 11, OptionName.SweetgifterGiveCount, new(0, 14, 1), 2, false)
            .SetValueFormat(OptionFormat.Players);
        OptionKaisyuCount = IntegerOptionItem.Create(RoleInfo, 12, OptionName.SweetgifterKaisyuCount, new(0, 14, 1), 3, false)
            .SetValueFormat(OptionFormat.Times);
        OptionKillFlash = BooleanOptionItem.Create(RoleInfo, 13, OptionName.SweetgifterKillFlash, true, false);
        OptionNotifytaisyo = BooleanOptionItem.Create(RoleInfo, 14, OptionName.SweetgifterNotifyTaisyo, true, false);
        OptionNotifyKiller = BooleanOptionItem.Create(RoleInfo, 15, OptionName.SweetgifterNotifyKiller, true, false);
        OptionKaisyuSonoturn = BooleanOptionItem.Create(RoleInfo, 16, OptionName.SweetgifterCanKaisyu, true, false);
    }
    public bool CanUseSabotageButton() => false;
    public float CalculateKillCooldown() => NowKillCool;
    public bool CanUseImpostorVentButton() => true;
    public override bool CanUseAbilityButton() => !Player.IsModClient() && !IsGiveMode;
    public bool CanUseKillButton() => IsGiveMode;
    public bool IsKiller => false;
    HashSet<byte> OkasiPlayers = new();
    HashSet<byte> Notifytaisyo = new();
    Dictionary<SystemTypes, Dictionary<PlayerControl, bool>> Notify = new();
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
        bool Enable = OptionNotifytaisyo.GetBool() && OptionNotifyKiller.GetBool() ? target.GetRoleClass() is IKiller && !target.Is(CustomRoleTypes.Crewmate) : OptionNotifytaisyo.GetBool();
        if (OptionNotifytaisyo.GetBool())
        {
            if (target.GetRoleClass() is IKiller killer && killer.IsKiller)
            {
                Notifytaisyo.Add(target.PlayerId);
            }
            else if (target.GetRoleClass() is not IKiller && OptionNotifytaisyo.GetBool())
            {
                Notifytaisyo.Add(target.PlayerId);
            }
        }
        if (count <= 0)
        {
            ModeSwitching(RoleTypes.Crewmate);
        }
        else
        {
            SendRPC();
        }
    }
    void OnPet()
    {
        var room = Player.GetPlainShipRoom();
        if (room is null) return;
        var pr = room.RoomId;

        if (OkasiRooms.TryGetValue(pr, out var current) && Kaisyu > 0)
        {
            if (!Notify.TryGetValue(pr, out var roomNotify))
            {
                roomNotify = new Dictionary<PlayerControl, bool>(); 
                Notify[pr] = roomNotify;
            }
            foreach (var kaisyu in current)
            {
                if (Kaisyu <= 0) break;
                if (kaisyu.Turn == 1 || OptionKaisyuSonoturn.GetBool())
                {
                    if (!roomNotify.ContainsKey(kaisyu.Player))
                    {
                        roomNotify.Add(kaisyu.Player, kaisyu.Okasi);
                        --Kaisyu;
                        ++count;
                    }
                }
            }

            if (IsGiveMode)
            {
                Player.SetKillCooldown(NowKillCool);
            }
            else
            {
                Player.RpcProtectedMurderPlayer();
            }

            OkasiRooms.Remove(pr);
            SendRPC();
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
                    updatedList.Add(new OkasiState(current.Count, nextTurn, current.Okasi, current.Player));
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
        seen ??= seer;

        if (OkasiPlayers == null) return "";
        if (!Is(seer)) return "";

        if (OkasiPlayers.Contains(seen.PlayerId))
        {
            return $"<{RoleInfo.RoleColorCode}>★</color>";
        }
        return "";
    }
    public override void OnStartMeeting()
    {
        if (Notify.Count is 0)
        {
            Logger.Info("からっぽ！","SweetGifter");
        }
        foreach (var room in Notify.Keys)
        {
            foreach (var okasi in Notify[room])
            { 
                if (okasi.Value)
                {
                    _ = new LateTask(() => Utils.SendMessage(string.Format(GetString("SweetgifterNotifyKillOkasi"), UtilsName.GetPlayerColor(okasi.Key), GetString($"{room}"), Player.PlayerId)),
                    1f, "SendPhantom", true);
                }
                else
                {
                    _ = new LateTask(() => Utils.SendMessage(string.Format(GetString("SweetgifterNotifyKillNotOkasi"), UtilsName.GetPlayerColor(okasi.Key), GetString($"{room}"), Player.PlayerId)),
                    1f, "SendPhantom", true);
                }
            }
        }
        Notifytaisyo.Add(Player.PlayerId);
        foreach (var p in Notifytaisyo)
        {
            _ = new LateTask(() => Utils.SendMessage(string.Format(GetString("SweetgifterNotifytxt")), p, GetString("SweetgifterNotifyTitle")),
                    1f, "SendPhantom", true);
        }
        Notifytaisyo.Clear();
        Notify.Clear();
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
    public void Otosu(PlayerControl target, PlayerControl killer)
    {
        if (GameStates.IsMeeting || GameStates.CalledMeeting) return;
        OkasiPlayers.Remove(target.PlayerId);
        SendRPC();

        var room = target.GetPlainShipRoom();
        if (room == null) return;

        if (OptionKillFlash.GetBool())
        {
            Utils.KillFlash(Player);
        }
        if (!OkasiRooms.ContainsKey(room.RoomId))
            OkasiRooms[room.RoomId] = new List<OkasiState>();
        OkasiRooms[room.RoomId].Add(new OkasiState(1, 0, OkasiPlayers.Contains(killer.PlayerId), target));
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
        sender.Writer.Write(OkasiPlayers.Count);
        foreach (var id in OkasiPlayers)
        {
            sender.Writer.Write(id);
        }
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        count = reader.ReadInt32();
        IsGiveMode = reader.ReadBoolean();
        Kaisyu = reader.ReadInt32();
        OkasiPlayers.Clear();
        var n = reader.ReadInt32();
        for (int i = 0; i < n; i++)
        {
            OkasiPlayers.Add(reader.ReadByte());
        }
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
        var kc = Kaisyu < 0 ? 0 : Kaisyu;
        var text = $"({count})({kc})";
        return count > 0 ? Utils.ColorString(Color.yellow, text) : Utils.ColorString(Color.gray, text);
    }
}