using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using UnityEngine;

namespace TownOfHost.Roles.Crewmate;

public sealed class Jizo : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Jizo),
            player => new Jizo(player),
            CustomRoles.Jizo,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            38800,
            SetupOptionItem,
            "jz",
            "#a9a9a9",
            (6, 5)
        );

    public Jizo(PlayerControl player)
        : base(RoleInfo, player)
    {
        Cooldown = OptionCooldown.GetFloat();

        UseCount = OptionUseCount.GetInt();
        IsUsed = false;
        Duration = OptionDuration.GetFloat();
        KilledRoom = null;
        UsedRoom = null;
        JizocooldownLeft = OptionCooldown.GetFloat(); //一応オプションから直接取得しておく

        Killed = false;
        Detectioned = false;

        CustomRoleManager.OnEnterVentOthers.Add(OnEnterVentOthers);
        vented = false;

        sendCount = 0;

        BomDetectioned = false;
        BomKilled = false;
        BomKilledRoom = null;
        BomKiller = null;
        bomber = null;

        Notifyname = OptionNotifyName.GetBool();
    }

    public static OptionItem OptionCooldown;
    public static OptionItem OptionUseCount;
    public static OptionItem OptionDuration;
    public static OptionItem OptionKill;
    public static OptionItem OptionVent;
    public static OptionItem OptionNotifyName;
    public SystemTypes? UsedRoom = null;

    float Duration;
    bool IsUsed;
    float Cooldown;
    float JizocooldownLeft;
    int UseCount;
    Vector2 pos = Vector2.zero;

    public bool BomKilled;
    public SystemTypes? BomKilledRoom = null;
    PlayerControl BomKiller;
    public bool BomDetectioned;

    public bool Killed;
    public SystemTypes? KilledRoom = null;
    public SystemTypes? VentedRoom = null;
    PlayerControl Killer;
    public PlayerControl bomber;
    bool Detectioned;
    bool vented;
    PlayerControl VentUser;
    int sendCount;

    bool Notifyname;
    public override void Add()
    {
        JizocooldownLeft = OptionCooldown.GetFloat(); //一応オプションから直接取得しておく
        PetActionManager.Register(Player.PlayerId, PetUsed);
        UsedRoom = null;
        KilledRoom = null;
        Killed = false;
        Detectioned = false;
        BomDetectioned = false;
        BomKilled = false;
        BomKilledRoom = null;
        BomKiller = null;
        bomber = null;
        sendCount = 0;
        RpcJizo(null);
    }
    public override void OnSpawn(bool initialState = false)
    {
        JizocooldownLeft = OptionCooldown.GetFloat(); //一応オプションから直接取得しておく

        Player.RpcResetAbilityCooldown(Sync: true);
        Killed = false;

        BomDetectioned = false;
        BomKilled = false;
        BomKilledRoom = null;
        BomKiller = null;
        bomber = null;
        RpcJizo(null);
    }
    enum OptionName
    {
        JizoDuration,
        JizoDetectionKill,
        JizoDetectionVent,
        JizoNotifyName
    }

    static void SetupOptionItem()
    {
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, OptionBaseCoolTime, 30, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionDuration = FloatOptionItem.Create(RoleInfo, 11, OptionName.JizoDuration, new(1f, 30f, 1f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionUseCount = IntegerOptionItem.Create(RoleInfo, 12, GeneralOption.OptionCount, new(1, 10, 1), 1, false)
            .SetValueFormat(OptionFormat.Times);
        OptionKill = BooleanOptionItem.Create(RoleInfo, 13, OptionName.JizoDetectionKill, true, false);
        OptionVent = BooleanOptionItem.Create(RoleInfo, 14, OptionName.JizoDetectionVent, true, false);
        OptionNotifyName = BooleanOptionItem.Create(RoleInfo, 15, OptionName.JizoNotifyName, true, false);
    }

    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = JizocooldownLeft > 0f ? JizocooldownLeft : 0.1f;
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }

    public override bool CanClickUseVentButton => false;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => false;

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!Player.IsAlive()) return;
        if (!AmongUsClient.Instance.AmHost) return;

        if (IsUsed)
        {
            Player.RpcSnapToForced(pos);
        }

        if (JizocooldownLeft > 0f)
        {
            JizocooldownLeft -= Time.fixedDeltaTime;
            if (JizocooldownLeft < 0f) JizocooldownLeft = 0f;
        }
    }

    void PetUsed()
    {
        if (!Player.IsAlive() || IsUsed || UseCount <= 0 || !AmongUsClient.Instance.AmHost || JizocooldownLeft > 0f)
        {
            return;
        }

        var room = Player.GetPlainShipRoom();
        if (room == null) return; //部屋の外では使用不可

        Logger.Info("注視開始", "Jizo");
        IsUsed = true;
        pos = Player.transform.position;
        Player.RpcSnapToForced(pos);

        JizocooldownLeft = Duration;
        --UseCount;
        Player.RpcResetAbilityCooldown(Sync: true);

        RpcJizo(room.RoomId); //IsUsed/UsedRoom/クールダウン/使用回数をまとめて送信

        _ = new LateTask(() =>
        {
            IsUsed = false;
            JizocooldownLeft = Cooldown;
            Logger.Info("注視終了", "Jizo");

            RpcJizo(null);
        }, Duration, "Jizo_Use", true);
    }

    /// <summary>
    /// UsedRoomを更新して全クライアントへ同期する
    /// </summary>
    private void RpcJizo(SystemTypes? roomType)
    {
        UsedRoom = roomType;
        SendRPC();
    }
    /// <summary>
    /// 同期が必要な値をまとめて送信する
    /// ReceiveRPCの読み込み順と必ず一致させること
    /// </summary>
    void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(JizocooldownLeft);
        sender.Writer.Write(UseCount);
        sender.Writer.Write(IsUsed);
        sender.Writer.Write((byte?)UsedRoom ?? byte.MaxValue);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        JizocooldownLeft = reader.ReadSingle();
        UseCount = reader.ReadInt32();
        IsUsed = reader.ReadBoolean();
        var roomId = reader.ReadByte();

        UsedRoom = roomId == byte.MaxValue ? null : (SystemTypes)roomId;
    }

    /// <summary>
    /// 地蔵が注視中で同じ部屋にいるかをチェックする
    /// </summary>
    public static void Checkroom(PlainShipRoom room, PlayerControl Player)
    {
        foreach (var jizo in PlayerCatch.AllAlivePlayerControls)
        {
            if (jizo.GetRoleClass() is not Jizo j) continue;
            if (room == null) continue;
            if (room.RoomId != j.UsedRoom) continue;
            if (j.Detectioned || j.BomDetectioned) continue;
            if (j.Killer != null || j.BomKiller != null) return;
            if (!OptionKill.GetBool()) return;
            j.Detectioned = true;
            j.Killed = true;
            j.KilledRoom = room.RoomId;
            j.Killer = Player;
        }
    }
    /// <summary>
    /// 地蔵が注視中で同じ部屋にいるかをチェックする(ボマー専用)
    /// </summary>
    public static void BomCheckroom(PlainShipRoom room, PlayerControl Player)
    {
        foreach (var jizo in PlayerCatch.AllAlivePlayerControls)
        {
            if (jizo.GetRoleClass() is not Jizo j) continue;
            if (room == null) continue;
            if (room.RoomId != j.UsedRoom) continue;
            if (j.BomKiller != null) continue;
            if (!OptionKill.GetBool()) continue;
            j.BomDetectioned = true;
            j.BomKilledRoom = room.RoomId;
            j.BomKiller = Player;
            j.bomber = Player;
        }
    }

    public void BomClear()
    {
        BomDetectioned = false;
        BomKilled = false;
        BomKilledRoom = null;
        BomKiller = null;
    }

    public override string GetProgressText(bool comms = false, bool gamelog = false)
    {
        var progress = Utils.ColorString(UseCount > 0 ? Color.green : Color.gray, $"({UseCount})");
        return progress;
    }
    public override void AfterMeetingTasks()
    {
        JizocooldownLeft = Cooldown;
        IsUsed = false;
        Player.RpcResetAbilityCooldown(Sync: true);
        KilledRoom = null;
        Killed = false;
        Detectioned = false;
        sendCount = 0;
        BomDetectioned = false;
        BomKilled = false;
        BomKilledRoom = null;
        BomKiller = null;
        RpcJizo(null); //UsedRoom=nullにして全値を同期
    }

    public override void OnReportDeadBody(PlayerControl _, NetworkedPlayerInfo __)
    {
        if (!Detectioned && !BomDetectioned)
        {
            return;
        }
        if (Killed)
        {
            Utils.SendMessage(string.Format(GetString("JizoKillText"), GetString($"{KilledRoom.Value}")), Player.PlayerId);
            if (Notifyname)
            {
                Utils.SendMessage(string.Format(GetString("JizoKillerText2"), UtilsName.GetPlayerColor(Player, true), Player), Killer.PlayerId);
            }
            else
            {
                Utils.SendMessage(string.Format(GetString("JizoKillerText")), Killer.PlayerId);
            }
            ++sendCount;
        }
        if (vented && VentUser != null)
        {
            Utils.SendMessage(string.Format(GetString("JizoVentText"), GetString($"{VentedRoom.Value}")), Player.PlayerId);
            if (Notifyname)
            {
                Utils.SendMessage(string.Format(GetString("JizoVenterText2"), UtilsName.GetPlayerColor(Player, true), Player), VentUser.PlayerId);
            }
            else
            {
                Utils.SendMessage(string.Format(GetString("JizoVenterText")), VentUser.PlayerId);
            }
            ++sendCount;
        }
        if ((BomKilled || BomDetectioned) && BomKiller != null && BomKilledRoom.HasValue)
        {
            Utils.SendMessage(string.Format(GetString("JizoKillText"), GetString($"{BomKilledRoom.Value}")), Player.PlayerId);
            if (Notifyname)
            {
                Utils.SendMessage(string.Format(GetString("JizoKillerText2"), UtilsName.GetPlayerColor(Player, true), Player), BomKiller.PlayerId);
            }
            else
            {
                Utils.SendMessage(string.Format(GetString("JizoKillerText")), BomKiller.PlayerId);
            }
            ++sendCount;
        }

        KilledRoom = null;

        Killed = false;
        Killer = null;

        vented = false;
        VentedRoom = null;
        VentUser = null;

        Detectioned = false;
        sendCount = 0;

        BomDetectioned = false;
        BomKilled = false;
        BomKilledRoom = null;
        BomKiller = null;

        RpcJizo(null); //UsedRoom=nullをクライアントにも反映
    }
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;

        if (!IsUsed || UsedRoom == null)
        {
            return "";
        }
        return $"{UsedRoom.Value}で注視使用中</size>";
    }
    public static bool OnEnterVentOthers(PlayerPhysics physics, int ventId)
    {
        if (!OptionVent.GetBool()) return true;
        var user = physics.myPlayer;
        var userRoom = user.GetPlainShipRoom();
        if (userRoom == null) return true;
        foreach (var jizo in PlayerCatch.AllAlivePlayerControls)
        {
            if (jizo.GetRoleClass() is not Jizo j) continue;
            if (userRoom.RoomId == j.UsedRoom)
            {
                j.vented = true;
                j.VentUser = user;
                j.Detectioned = true;
                j.VentedRoom = j.UsedRoom;
            }
        }
        return true;
    }
    public override string GetAbilityButtonText() => "注視";
    public override bool OverrideAbilityButton(out string text)
    {
        text = "Jizo_Ability";
        return true;
    }
}