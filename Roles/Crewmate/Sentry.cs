using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using UnityEngine;

namespace TownOfHost.Roles.Crewmate;

public sealed class Sentry : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Sentry),
            player => new Sentry(player),
            CustomRoles.Sentry,
            () => OptionAwakening.GetBool() ? RoleTypes.Crewmate : RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            40200,
            SetupOptionItem,
            "sty",
            "#4169e1",
            (6, 9),
            introSound: () => GetIntroSound(RoleTypes.Crewmate),
            Desc: () =>
            {
                return string.Format(GetString("SentryDesc"), OptionTime.GetFloat());
            }
        );

    public Sentry(PlayerControl player) : base(RoleInfo, player)
    {
        Cooldown = OptionCooldown.GetFloat();
        BasyoCoolDown = OptionBasyoCoolDown.GetFloat();

        markedPos = null;
        hasMarked = false;
        cooldownLeft = BasyoCoolDown;
        IsUsed = false;
        Original = null;
        UseCount = OptionUseCount.GetInt();
        Awakened = !OptionAwakening.GetBool();

        PetActionManager.Register(Player.PlayerId, OnPet);
    }
    static OptionItem OptionBasyoCoolDown;
    static OptionItem OptionCooldown;
    static OptionItem OptionTime;
    static OptionItem OptionUseCount;
    static OptionItem OptionAwakening;
    static OptionItem OptionTask;
    static float Cooldown;
    static float BasyoCoolDown;
    int UseCount;
    bool Awakened;
    enum OptionName
    {
        SetryBasyoCool,
        SentryCooldown,
    }

    static void SetupOptionItem()
    {
        OptionBasyoCoolDown = FloatOptionItem.Create(RoleInfo, 10, OptionName.SetryBasyoCool, OptionBaseCoolTime, 10f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.SentryCooldown, OptionBaseCoolTime, 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionTime = FloatOptionItem.Create(RoleInfo, 12, GeneralOption.Duration, OptionBaseCoolTime, 10f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionUseCount = IntegerOptionItem.Create(RoleInfo, 13, GeneralOption.OptionCount, new(1, 99, 1), 1, false)
            .SetValueFormat(OptionFormat.Times);
        OptionAwakening = BooleanOptionItem.Create(RoleInfo, 14, GeneralOption.AbilityAwakening, true, false);
        OptionTask = IntegerOptionItem.Create(RoleInfo, 15, GeneralOption.cantaskcount, new(1, 255, 1), 5, false)
            .SetValueFormat(OptionFormat.Pieces);
    }
    public override bool OnCompleteTask(uint taskid)
    {
        if (MyTaskState.HasCompletedEnoughCountOfTasks(OptionTask.GetInt()))
        {
            if (Awakened == false)
                if (!Utils.RoleSendList.Contains(Player.PlayerId))
                    Utils.RoleSendList.Add(Player.PlayerId);
            if (Player.IsAlive() && !Awakened)
                Player.RpcSetRoleDesync(RoleTypes.Engineer, Player.GetClientId(), SendOption.None);
            Awakened = true;
        }
        return true;
    }
    Vector2? markedPos;
    bool hasMarked;
    float cooldownLeft;
    bool IsUsed;
    Vector2? Original;
    void OnPet()
    {
        if (!Player.IsAlive()) return;
        if (!AmongUsClient.Instance.AmHost) return;
        if (cooldownLeft > 0f || UseCount <= 0 || !MyTaskState.HasCompletedEnoughCountOfTasks(OptionTask.GetInt())) return;

        if (!hasMarked)
        {
            markedPos = Player.transform.position;
            hasMarked = true;
            cooldownLeft = Cooldown;
            SendRpc();
            Player.MarkDirtySettings();
            Player.RpcResetAbilityCooldown(Sync: true);
            UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
            return;
        }

        if (markedPos.HasValue)
        {
            --UseCount;
            var capturedPos = markedPos.Value;
            cooldownLeft = BasyoCoolDown;
            SendRpc();
            Player.MarkDirtySettings();
            Player.RpcResetAbilityCooldown(Sync: true);
            var tmpSpeed = Main.AllPlayerSpeed[Player.PlayerId];
            Main.AllPlayerSpeed[Player.PlayerId] = Main.MinSpeed;
            Player.MarkDirtySettings();
            ReportDeadBodyPatch.CanReport[Player.PlayerId] = false;

            _ = new LateTask(() =>
            {
                if (!Player.IsAlive()) return;
                IsUsed = true;
                Original = Player.transform.position;
                SendRpc();
                Player.RpcSnapToDesync(Player, capturedPos);
                _ = new LateTask(() =>
                {
                    foreach (var p in PlayerCatch.AllPlayerControls)
                    {
                        if (p == Player) continue;
                        Player.RpcSnapToDesync(p, Original.Value);
                    }
                }, Main.LagTime, "Sentry BlockMove");
                UtilsGameLog.AddGameLog("Sentry",
                    $"{UtilsName.GetPlayerColor(Player)} がワープした → {capturedPos}");
                markedPos = null;
                _ = new LateTask(() =>
                {
                    Main.AllPlayerSpeed[Player.PlayerId] = tmpSpeed;
                    ReportDeadBodyPatch.CanReport[Player.PlayerId] = true;
                    Player.RpcSnapToForced(Original.Value);
                    Player.MarkDirtySettings();
                    IsUsed = false;
                    hasMarked = false;
                    Cooldown = BasyoCoolDown;
                    SendRpc();
                    if (UseCount <= 0 && Player.IsAlive())
                    {
                        Player.RpcSetRoleDesync(RoleTypes.Crewmate, Player.GetClientId(), SendOption.None);
                    }
                }, OptionTime.GetFloat(), "Sentry BlockMove");
            }, 0.5f, $"Sentry.Warp.{Player.PlayerId}", true);
        }
    }
    public override RoleTypes? AfterMeetingRole => !Awakened || UseCount <= 0 ? RoleTypes.Crewmate : RoleTypes.Engineer;
    public override CustomRoles Misidentify() => Awakened ? CustomRoles.NotAssigned : CustomRoles.Crewmate;
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive()) return;
        if (cooldownLeft <= 0f || !Player.Is(CustomRoles.Sentry)) return;

        float prev = cooldownLeft;
        cooldownLeft -= Time.fixedDeltaTime;
        if (cooldownLeft < 0f)
        {
            cooldownLeft = 0f;
            UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
        }
    }
    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = cooldownLeft > 0f ? cooldownLeft : 0.1f;
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }

    public override bool CanClickUseVentButton => false;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => false;

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";
        if (isForMeeting || IsUsed || cooldownLeft > 0f || UseCount <= 0 || !Awakened) return "";

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;

        if (!hasMarked)
            return $"{size}<color={color}>ペットを撫でてワープ先を設定</color>";
        return $"{size}<color={color}>ペットを撫でてワープ！</color>";
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(Awakened);
        sender.Writer.Write(hasMarked);
        sender.Writer.Write(UseCount);
        sender.Writer.Write(cooldownLeft);
        sender.Writer.Write(IsUsed);
        sender.Writer.Write(markedPos.HasValue);
        sender.Writer.Write(Original.HasValue);
        if (markedPos.HasValue)
        {
            sender.Writer.Write(markedPos.Value.x);
            sender.Writer.Write(markedPos.Value.y);
        }
        if (Original.HasValue)
        {
            sender.Writer.Write(Original.Value.x);
            sender.Writer.Write(Original.Value.y);
        }
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        Awakened = reader.ReadBoolean();
        hasMarked = reader.ReadBoolean();
        UseCount = reader.ReadInt32();
        cooldownLeft = reader.ReadSingle();
        IsUsed = reader.ReadBoolean();
        bool hasPos = reader.ReadBoolean();
        bool hasog = reader.ReadBoolean();
        markedPos = hasPos
            ? new Vector2(reader.ReadSingle(), reader.ReadSingle())
            : null;
        Original = hasog
            ? new Vector2(reader.ReadSingle(), reader.ReadSingle())
            : null;
    }
}