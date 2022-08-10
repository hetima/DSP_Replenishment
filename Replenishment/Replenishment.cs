using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ReplenishmentMod
{
    [BepInPlugin(__GUID__, __NAME__, "1.0.6")]
    public class Replenishment : BaseUnityPlugin
    {
        public const string __NAME__ = "Replenishment";
        public const string __GUID__ = "com.hetima.dsp." + __NAME__;

        new internal static ManualLogSource Logger;
        void Awake()
        {
            Logger = base.Logger;
            //Logger.LogInfo("Awake");

            Configs.configEnableOutgoingStorage = Config.Bind<bool>(
                    "General",
                    "EnableOutgoingStorage",
                    false,
                    "Whether or not to enable picking items from storages with an outgoing sorter attached.").Value;
            Configs.configEnableSearchingAllPlanets = Config.Bind<bool>(
                    "General",
                    "EnableSearchingAllPlanets",
                    false,
                    "Whether or not to enable picking items from storages on any planets.").Value;
            new Harmony(__GUID__).PatchAll(typeof(Patch));
        }

        public static class Configs
        {
            public static bool configEnableOutgoingStorage = false;
            public static bool configEnableSearchingAllPlanets = false;
        }

        public static PlanetFactory BirthPlanetFactory()
        {
            GameData game = UIRoot.instance.uiGame.gameData;
            PlanetFactory[] factories = game.factories;
            int birthPlanetId = game.galaxy.birthPlanetId;
            for (int i = 0; i < game.factoryCount; i++)
            {
                if (factories[i].planetId == birthPlanetId)
                {
                    return factories[i];
                }
            }
            return null;
        }

        private static int GetFromFactory(PlanetFactory factory, int itemId, out int inc)
        {
            inc = 0;
            if (factory == null)
            {
                return 0;
            }
            int pick = StorageComponent.itemStackCount[itemId];
            int picked = 0;            
            FactoryStorage fs = factory.factoryStorage;
            for (int i = 1; i < fs.storageCursor; i++)
            {
                StorageComponent sc = fs.storagePool[i];
                if (sc == null || sc.entityId <= 0)
                {
                    continue;
                }
                int stock = sc.GetItemCount(itemId);
                if (stock <= 0)
                {
                    continue;
                }

                if (!Configs.configEnableOutgoingStorage)
                {
                    //搬出ソーターが付いているものは除外
                    bool isOutput = false;
                    for (int j = 0; j <= 11; j++)
                    {
                        int otherObjId;
                        int otherSlot;
                        factory.ReadObjectConn(sc.entityId, j, out isOutput, out otherObjId, out otherSlot);
                        if (isOutput)
                        {
                            break;
                        }
                    }
                    if (isOutput)
                    {
                        continue;
                    }
                }
                picked += sc.TakeItem(itemId, pick, out int inc2);
                inc += inc2;
                pick -= picked;
                if (pick <= 0)
                {
                    return picked;
                }
            }
            return picked;
        }

        private static bool CheckInventoryCapacity()
        {
            Player mainPlayer = UIRoot.instance.uiGame.gameData.mainPlayer;
            if (mainPlayer != null)
            {
                for (int i = mainPlayer.package.size - 1; i >= 0; i--)
                {
                    if (mainPlayer.package.grids[i].itemId == 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool DeliverFromAllPlanets(int itemId, out string err)
        {
            GameData game = UIRoot.instance.uiGame.gameData;
            PlanetFactory[] factories = game.factories;
            if (!CheckInventoryCapacity())
            {
                err = "Inventory is full";
                return false;
            }
            err = "Item not found";
            int picked = 0;
            int pick = StorageComponent.itemStackCount[itemId];
            Player mainPlayer = UIRoot.instance.uiGame.gameData.mainPlayer;
            int inc = 0;
            foreach (PlanetFactory factory in factories)
            {
                picked += GetFromFactory(factory, itemId, out int inc2);
                inc += inc2;
                pick -= picked;
                if (pick <= 0)
                {
                    break;
                }
            }
            if (picked > 0)
            {
                int upCount = mainPlayer.TryAddItemToPackage(itemId, picked, inc, false, 0);
                UIItemup.Up(itemId, upCount);
                return true;
            }
            return false;
        }

        public static bool DeliverFromBirthPlanet(int itemId, out string err)
        {
            if (!CheckInventoryCapacity())
            {
                err = "Inventory is full";
                return false;
            }

            Player mainPlayer = UIRoot.instance.uiGame.gameData.mainPlayer;
            PlanetFactory factory = BirthPlanetFactory();
            err = "Item not found";
            int picked = GetFromFactory(factory, itemId, out int inc);
            if (picked > 0)
            {
                int upCount = mainPlayer.TryAddItemToPackage(itemId, picked, inc, false, 0);
                UIItemup.Up(itemId, upCount);
                return true;
            }
            return false;
        }

        public static bool DeliverFromVoid(int itemId, out string err)
        {
            if (!CheckInventoryCapacity())
            {
                err = "Inventory is full";
                return false;
            }

            err = "";
            Player mainPlayer = UIRoot.instance.uiGame.gameData.mainPlayer;
            int pick = StorageComponent.itemStackCount[itemId];

            int inc = 0;
            int upCount = mainPlayer.TryAddItemToPackage(itemId, pick, inc, false, 0);
            UIItemup.Up(itemId, upCount);
            return true;
        }

        public static bool DeliverFrom(int itemId, out string err)
        {
            if (GameMain.sandboxToolsEnabled)
            {
                return DeliverFromVoid(itemId, out err);
            }
            if (Configs.configEnableSearchingAllPlanets)
            {
                return DeliverFromAllPlanets(itemId, out err);
            }
            else
            {
                return DeliverFromBirthPlanet(itemId, out err);
            }
        }

        private static void OnToolBtnRightClick(int obj)
        {
            UIBuildMenu buildMenu = UIRoot.instance.uiGame.buildMenu;
            if (buildMenu.childButtons.Length > obj)
            {
                UIButton uiBtn = buildMenu.childButtons[obj];
                if (uiBtn != null)
                {
                    int itemId = uiBtn.tips.itemId;
                    if (itemId < 12000 && itemId > 0)
                    {
                        string err;
                        if (DeliverFrom(itemId, out err))
                        {
                            VFAudio.Create("transfer-item", null, Vector3.zero, true, 0);
                            //UIRoot.instance.uiGame.ShutAllFunctionWindow();
                            //UIRoot.instance.uiGame.OpenPlayerInventory();
                        }
                        else
                        {
                            VFAudio.Create("ui-error", null, Vector3.zero, true, 5);
                            UIRealtimeTip.Popup(err, false, 0);
                        }
                    }
                }
            }
        }

        static class Patch
        {
            internal static bool _initialized = false;

            [HarmonyPostfix, HarmonyPatch(typeof(UIBuildMenu), "StaticLoad")]
            public static void UIBuildMenu_StaticLoad_Postfix()
            {
                if (!_initialized)
                {
                    //ItemProto[,] protos 0は無視 1から
                    ref ItemProto[,] protos = ref AccessTools.StaticFieldRefAccess<ItemProto[,]>(typeof(UIBuildMenu), "protos");
                    protos[6, 10] = LDB.items.Select(5002); //物流船
                    protos[6, 9] = LDB.items.Select(5001); //物流ドローン
                    protos[7, 10] = LDB.items.Select(1210); //空間歪曲器
                    protos[7, 9] = LDB.items.Select(1803); //反物質燃料棒
                    protos[7, 8] = LDB.items.Select(1802); //重水素燃料棒
                    protos[7, 7] = LDB.items.Select(1801); //水素燃料棒
                    //protos[8, 10] = LDB.items.Select(1503); //小型輸送ロケット
                    //protos[8, 9] = LDB.items.Select(1501); //ソーラーセイル

                    protos[1, 10] = LDB.items.Select(2207); //蓄電器(満充電)

                    _initialized = true;
                }

            }

            [HarmonyPostfix, HarmonyPatch(typeof(UIBuildMenu), "_OnRegEvent")]
            public static void UIBuildMenu_OnRegEvent_Postfix(UIBuildMenu __instance)
            {
                for (int i = 0; i < __instance.childButtons.Length; i++)
                {
                    if (__instance.childButtons[i] != null)
                    {
                        __instance.childButtons[i].onRightClick += OnToolBtnRightClick;
                        __instance.childButtons[i].data = i;
                    }
                }
            }

            [HarmonyPostfix, HarmonyPatch(typeof(UIBuildMenu), "_OnUnregEvent")]
            public static void UIBuildMenu_OnUnregEvent_Postfix(UIBuildMenu __instance)
            {
                for (int i = 0; i < __instance.childButtons.Length; i++)
                {
                    if (__instance.childButtons[i] != null)
                    {
                        __instance.childButtons[i].onRightClick -= OnToolBtnRightClick;
                    }
                }
            }
        }

    }
}
