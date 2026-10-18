using AmongUs.GameOptions;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Crewmate;

public sealed class ToiletFan : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(ToiletFan),
            player => new ToiletFan(player),
            CustomRoles.ToiletFan,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            12500,
            SetupOptionItem,
            "to",
            "#735134",
            (9, 3),
            introSound: () => GetIntroSound(RoleTypes.Crewmate),
            assignInfo: new RoleAssignInfo(CustomRoles.ToiletFan, CustomRoleTypes.Crewmate)
            {
                // トイレファンをエアシップ限定にするオプションがONのときのみエアシップで初期配役可能にする
                //　ファングル・ミラ・スケルドは多分何もあけれないからどっちにしろアサインしない

                IsInitiallyAssignableCallBack = () => (OptionOnlyAirship?.GetBool() ?? false) ? Main.NormalOptions.MapId is 4 : Main.NormalOptions.MapId is 4 or 3
            },
            from: From.SuperNewRoles
        );
    public ToiletFan(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        Cooldown = OptionCooldown.GetFloat();
        flug = 0;
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = Cooldown;
        AURoleOptions.EngineerInVentMaxTime = 1;
    }
    public override void Add()
    {
        if (Main.NormalOptions.MapId != 4 && OptionOnlyAirship.GetBool())
        {
            Player.RpcSetCustomRole(CustomRoles.Bakery);
        }
        PetActionManager.Register(Player.PlayerId, Open);
    }
    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
    }
    private static OptionItem OptionCooldown;
    public static OptionItem OptionOnlyAirship;
    public static OptionItem OptionTask;
    public static OptionItem TaskCount;
    enum OptionName
    {
        OnlyAirship,
        ToiletFanAllTaskIsToilet,
        ToiletFanTaskCount
    }
    private static float Cooldown;
    int flug;
    private static void SetupOptionItem()
    {
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, new(1f, 30f, 1f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionOnlyAirship = BooleanOptionItem.Create(RoleInfo, 11, OptionName.OnlyAirship, false, false);
        OptionTask = BooleanOptionItem.Create(RoleInfo, 12, OptionName.ToiletFanAllTaskIsToilet, false, false, OptionOnlyAirship)
            .SetEnabled(() => Event.April || Event.Special).SetInfo(Translator.GetString("AprilfoolOnly"));
        TaskCount = IntegerOptionItem.Create(RoleInfo, 13, OptionName.ToiletFanTaskCount, new(1, 255, 1), 15, false, OptionTask);
    }
    public static (bool, int, int, int) TaskData => (false, 0, 0, TaskCount.GetInt());
    public override bool CanClickUseVentButton => false;
    /*public override bool OnEnterVent(PlayerPhysics physics, int ventId)
    {
        Open();
        return false;
    }*/
    void Open()
    {
        //可哀想やから除染あけれるようにはしてあげる。
        flug = Main.NormalOptions.MapId is 4 ? 1 : 2;
        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, 79);
        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, 80);
        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, 81);
        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Doors, 82);
        Player.RpcResetAbilityCooldown();
    }
    public override string GetAbilityButtonText() => GetString("ToiletFanAbility");
    public override bool OverrideAbilityButton(out string text)
    {
        text = "ToiletFan_Ability";
        return true;
    }
    public override void CheckWinner(GameOverReason reason)
    {
        switch (flug)
        {
            case 1:
                Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[0]);
                break;
            case 2:
                Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[1]);
                break;
            default: break;
        }
    }
    public static System.Collections.Generic.Dictionary<int, Achievement> achievements = new();
    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var n1 = new Achievement(RoleInfo, 0, 1, 0, 0);
        var n2 = new Achievement(RoleInfo, 1, 1, 0, 0);
        achievements.Add(0, n1);
        achievements.Add(1, n2);
    }
}