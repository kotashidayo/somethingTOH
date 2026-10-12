using System.Collections.Generic;
using UnityEngine;
using TownOfHost.Roles.Core;
using static TownOfHost.Options;

namespace TownOfHost.Roles.AddOns.Common
{
    public static class Medicalrecord
    {
        private static readonly int Id = 74300;
        private static Color RoleColor = UtilsRoleText.GetRoleColor(CustomRoles.Medicalrecord);
        public static string SubRoleMark = Utils.ColorString(RoleColor, "Ｍ");
        public static List<byte> playerIdList = new();
        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.Addons, CustomRoles.Medicalrecord);
            AddOnsAssignData.Create(Id + 10, CustomRoles.Medicalrecord, true, true, true, true);
        }
        public static void Init()
        {
            playerIdList = new();
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
        }
    }
}