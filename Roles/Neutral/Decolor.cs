using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmongUs.GameOptions;
using Hazel;
using Rewired.Utils.Classes.Data;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Madmate;
using UnityEngine;
using static Il2CppSystem.Globalization.CultureInfo;
using static UnityEngine.GraphicsBuffer;
namespace TownOfHost.Roles.Neutral;

public sealed class Decolor : RoleBase, IKiller, IUsePhantomButton
{
    enum DeColorstatus
    {
        Normal,
        Waiting
    }

    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Decolor),
            player => new Decolor(player),
            CustomRoles.Decolor,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Neutral,
            58200,
            SetupOptionItem,
            "DC",
            "#C9CDD2",
            //(3, 2),
            (0, 0),
            true,
            introSound: () => GetIntroSound(RoleTypes.Shapeshifter),
            from: From.UchuAddon
        );
    public Decolor(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
        DecoloredPlayers.Clear();
        DecolorWaitPlayers.Clear();
        IDecolored.Clear();
        InstanceWaitPlayers.Clear();
        UsedPhantom = false;
        shape = null;
        Modosita = false;
    }
    public override void Add()
    {
        IDecolored.Clear();
        InstanceWaitPlayers.Clear();
        Modosita = false;
        status = DeColorstatus.Normal;
    }
    public bool CanKill { get; private set; } = false;
    public static HashSet<byte> DecoloredPlayers = new();
    static HashSet<byte> DecolorWaitPlayers = new();
    //ディカラー個人の脱色待ちプレイヤー
    HashSet<byte> InstanceWaitPlayers = new();
    //ディカラー個人の脱色したプレイヤー
    HashSet<byte> IDecolored = new();
    static HashSet<byte> NotifyDecolored = new(14);
    public static OptionItem OptionCooldown;
    private static OptionItem OptionSelfCool;
    private static OptionItem OptionCanVent;
    private static OptionItem Sokuji;
    DeColorstatus status;
    bool UsedPhantom;
    public bool IsPhantomRole => !UsedPhantom;
    static PlayerControl shape = null;

    [Attributes.GameModuleInitializer]
    public static void Init()
    {
        DecoloredPlayers.Clear();
        DecolorWaitPlayers.Clear();
        shape = null;
    }

    enum OptionName
    {
        DecolorSelfCool,
        DecolorSokujiWin
    }
    private static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 1);
        OverrideKilldistance.Create(RoleInfo, 10);
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 11, GeneralOption.Cooldown, OptionBaseCoolTime, 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionSelfCool = FloatOptionItem.Create(RoleInfo, 12, OptionName.DecolorSelfCool, OptionBaseCoolTime, 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 13, GeneralOption.CanVent, true, false);
        Sokuji = BooleanOptionItem.Create(RoleInfo, 14, OptionName.DecolorSokujiWin, false, false);
    }

    public bool CanUseKillButton() => true;
    public bool CanUseImpostorVentButton() => OptionCanVent.GetBool();
    public bool CanUseSabotageButton() => false;
    public float CalculateKillCooldown() => OptionCooldown.GetFloat();
    bool Modosita;
    public void OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = true;
        if (UsedPhantom) return;

        UsedPhantom = true;

        DecolorWaitPlayers.Add(Player.PlayerId);
        InstanceWaitPlayers.Add(Player.PlayerId);

        SendRPC();
        //ぱりーんが鬱陶しいので
        if (OptionCooldown.GetFloat() != 0f)
        {
            Player.SetKillCooldown(OptionCooldown.GetFloat());
        }
    }
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        var target = info.AttemptTarget;
        info.DoKill = false;
        if (InstanceWaitPlayers.Contains(target.PlayerId) || DecoloredPlayers.Contains(target.PlayerId) || DecolorWaitPlayers.Contains(target.PlayerId)) return;
        DecolorWaitPlayers.Add(target.PlayerId);
        InstanceWaitPlayers.Add(target.PlayerId);
        //ぱりーんが鬱陶しいので
        if (OptionCooldown.GetFloat() != 0f)
        {
            Player.SetKillCooldown(OptionCooldown.GetFloat());
            _ = new LateTask(() =>
            {
                Player.RpcResetAbilityCooldown();
            }, Main.LagTime, "", true);
        }
        bool CanWin = Sokuji.GetBool();
        status = DeColorstatus.Waiting;
        foreach (var p in PlayerCatch.AllAlivePlayerControls)
        {
            var id = p.PlayerId;
            if (!DecoloredPlayers.Contains(id) && !DecolorWaitPlayers.Contains(id))
            {
                status = DeColorstatus.Normal;
                CanWin = false;
            }
        }
        SendRPC();
        if (CanWin && Sokuji.GetBool())
        {
            Win();
        }
    }
    void Win()
    {
        var CanWin = true;
        foreach (var p in PlayerCatch.AllAlivePlayerControls)
        {
            var id = p.PlayerId;
            if (Sokuji.GetBool())
            {
                if (!DecolorWaitPlayers.Contains(id) && !DecoloredPlayers.Contains(id))
                {
                    CanWin = false;
                }
            }
            else
            {
                if (!DecoloredPlayers.Contains(id))
                {
                    CanWin = false;
                }
            }
        }
        if (!CanWin) return;
        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Decolor, byte.MaxValue, false))
        {
            CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Decolor);
            CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
        }
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = UsedPhantom ? 999f : OptionSelfCool.GetFloat();
        opt.SetVision(false);
    }
    public bool OverrideKillButtonText(out string text)
    {
        text = GetString("DecolorText");
        return true;
    }
    public override string GetAbilityButtonText()
    {
        var text = GetString("DecolorSelfText");
        return text;
    }
    void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(UsedPhantom);
        sender.Writer.Write(Modosita);

        byte[] data = DecolorWaitPlayers.ToArray();
        sender.Writer.Write(data.Length);
        sender.Writer.Write(data);

        byte[] data2 = DecoloredPlayers.ToArray();
        sender.Writer.Write(data2.Length);
        sender.Writer.Write(data2);

        byte[] data3 = IDecolored.ToArray();
        sender.Writer.Write(data3.Length);
        sender.Writer.Write(data3);

        byte[] data4 = InstanceWaitPlayers.ToArray();
        sender.Writer.Write(data4.Length);
        sender.Writer.Write(data4);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        UsedPhantom = reader.ReadBoolean();
        Modosita = reader.ReadBoolean();

        int count = reader.ReadInt32();
        byte[] data = reader.ReadBytes(count);

        DecolorWaitPlayers = new HashSet<byte>(data);

        int count2 = reader.ReadInt32();
        byte[] data2 = reader.ReadBytes(count2);

        DecoloredPlayers = new HashSet<byte>(data2);

        int count3 = reader.ReadInt32();
        byte[] data3 = reader.ReadBytes(count3);

        IDecolored = new HashSet<byte>(data3);

        int c4 = reader.ReadInt32();
        byte[] d4 = reader.ReadBytes(c4);

        InstanceWaitPlayers = new HashSet<byte>(d4);
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!Player.IsAlive() && !Modosita)
        {
            Modosu();
        }
    }

    public override void AfterMeetingTasks()
    {
        if (!Player.IsAlive() && !Modosita)
        {
            Modosu();
        }
        if (status is DeColorstatus.Waiting)
        {
            Win();
        }
        DecolorPlayer();
    }
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        foreach (var wait in DecolorWaitPlayers)
        {
            if (!DecoloredPlayers.Contains(wait))
            {
                DecoloredPlayers.Add(wait);
                NotifyDecolored.Add(wait);
            }
        }
        DecolorWaitPlayers.Clear();
        InstanceWaitPlayers.Clear();
        if (!Player.IsAlive() && !Modosita)
        {
            Modosu();
        }
        SendRPC();
    }
    public static string SendMessage()
    {
        if (NotifyDecolored.Count == 0) return "";

        var names = NotifyDecolored
            .Distinct()
            .Select(PlayerCatch.GetPlayerById)
            .Where(p => p != null)
            .Select(p => UtilsName.GetPlayerColor(p, true))
            .ToList();

        NotifyDecolored.Clear(); // 次回会議まで重複表示しない
        if (names.Count == 0) return "";

        var text = string.Join("、", names);
        text = string.Format(GetString("DecolorMettingText"), text);
        return $"<size=80%>{text}</size>";
    }
    void Modosu()
    {
        Modosita = true;
        foreach (var id in IDecolored)
        {
            DecoloredPlayers.Remove(id);
            var pl = PlayerCatch.GetPlayerById(id);
            if (!pl.IsAlive())
            {
                continue;
            }
            if (shape.PlayerId == id)
            {
                Camouflage.RpcSetSkin(pl);
            }
            else
            {
                pl.RpcShapeshift(pl, false);
                var sender = CustomRpcSender.Create("CamouflagerShape");
                sender.AutoStartRpc(pl.NetId, RpcCalls.Shapeshift)
                    .Write(pl)
                    .Write(false)
                    .EndRpc();
                sender.EndMessage();
                sender.SendMessage();
            }
        }
    }
    public static void Color(PlayerControl player)
    {
        if (player == null || player.Data == null) return;
        if (DecoloredPlayers == null || !CustomRoles.Decolor.IsPresent()) return;

        var id = player.PlayerId;
        DecoloredPlayers.Remove(id);
        if (!player.IsAlive())
        {
            return;
        }
        if (shape.PlayerId == id)
        {
            var os = shape;
            shape = PlayerCatch.AllAlivePlayerControls
                .FirstOrDefault(x => x != os);
            RpcChColor(shape, 7, true);
            RpcHideSkinAndPet(shape);
            DecolorPlayer();
            Camouflage.RpcSetSkin(player);
        }
        else
        {
            player.RpcShapeshift(player, false);
            var sender = CustomRpcSender.Create("CamouflagerShape");
            sender.AutoStartRpc(player.NetId, RpcCalls.Shapeshift)
                .Write(player)
                .Write(false)
                .EndRpc();
            sender.EndMessage();
            sender.SendMessage();
        }
    }
    public static void DecolorPlayer(PlayerControl shapes = null)
    {
        shape = shapes;

        foreach (var decolor in DecoloredPlayers)
        {
            var pl = PlayerCatch.GetPlayerById(decolor);
            if (!pl.IsAlive())
            {
                DecoloredPlayers.Remove(decolor);
                continue;
            }
            /*pl.SetNamePlate("");
            CustomRpcSender.Create(name: $"RpcSetNamePlate({pl.Data.PlayerName})").AutoStartRpc(pl.NetId, (byte)RpcCalls.SetNamePlateStr)
                .Write("")
                .Write(pl.GetNextRpcSequenceId(RpcCalls.SetNamePlateStr))
                .EndRpc();*/
            if (shape == null)
            {
                RpcChColor(pl, 7, true);
                RpcHideSkinAndPet(pl);
                shape = pl;
            }
            else
            {
                pl.RpcShapeshift(shape, false);
                var sender = CustomRpcSender.Create("CamouflagerShape");
                sender.AutoStartRpc(pl.NetId, RpcCalls.Shapeshift)
                    .Write(shape)
                    .Write(false)
                    .EndRpc();
                sender.EndMessage();
                sender.SendMessage();
            }
        }
    }
    static void RpcChColor(PlayerControl pc, byte Color, bool NonSkin = false)
    {
        if (Options.firstturnmeeting && MeetingStates.FirstMeeting) return;
        if (GameStates.IsLobby) return;

        pc.SetColor(Color);
        var sender = CustomRpcSender.Create("DeChengeColor", SendOption.Reliable);
        sender.StartMessage();

        sender.StartRpc(pc.NetId, RpcCalls.SetColor)
        .Write(pc.NetId)
        .Write(Color)
        .EndRpc();
        sender.EndMessage();
        sender.SendMessage();
    }
    public static void RpcHideSkinAndPet(PlayerControl pc)
    {
        if (Options.firstturnmeeting && MeetingStates.FirstMeeting) return;
        if (GameStates.IsLobby) return;

        pc.SetSkin("", pc.CurrentOutfit.ColorId);
        pc.SetHat("", pc.CurrentOutfit.ColorId);
        pc.SetVisor("", pc.CurrentOutfit.ColorId);
        pc.SetPet("", pc.CurrentOutfit.ColorId);

        var sender = CustomRpcSender.Create("DeHideSkinAndPet", SendOption.Reliable);
        sender.StartMessage();
        sender.StartRpc(pc.NetId, RpcCalls.SetHatStr)
        .Write("")
        .Write(pc.GetNextRpcSequenceId(RpcCalls.SetHatStr))
        .EndRpc();

        sender.StartRpc(pc.NetId, RpcCalls.SetSkinStr)
        .Write("")
        .Write(pc.GetNextRpcSequenceId(RpcCalls.SetSkinStr))
        .EndRpc();

        sender.StartRpc(pc.NetId, RpcCalls.SetVisorStr)
        .Write("")
        .Write(pc.GetNextRpcSequenceId(RpcCalls.SetVisorStr))
        .EndRpc();

        sender.StartRpc(pc.NetId, RpcCalls.SetPetStr)
        .Write("")
        .Write(pc.GetNextRpcSequenceId(RpcCalls.SetPetStr))
        .EndRpc();

        sender.EndMessage();
        sender.SendMessage();
    }

    public static string GetMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (DecoloredPlayers.Contains(seen.PlayerId) && (GameStates.CalledMeeting || GameStates.IsMeeting))
        {
            return "<#C9CDD2>×</color>";
        }
        return "";
    }
    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;

        if (!Is(seer)) return "";
        var mark = "";

        if (InstanceWaitPlayers.Contains(seen.PlayerId))
        {
            mark = "<#C9CDD2>★</color>";
        }
        return mark;
    }

    public bool OverrideKillButton(out string text)
    {
        text = "Decolor_Kill";
        return true;
    }
    public override bool OverrideAbilityButton(out string text)
    {
        text = "Decolor_Ability";
        return true;
    }
}
