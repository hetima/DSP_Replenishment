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
using UnityEngine.EventSystems;

namespace ReplenishmentMod
{
    [BepInPlugin(__GUID__, __NAME__, "1.1.0")]
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
            Configs.configEnableSearchingInterstellarStations = Config.Bind<bool>(
                    "General",
                    "EnableSearchingInterstellarStations",
                    false,
                    "Whether or not to enable picking items from interstellar stations.").Value;
            Configs.configEnableRightClickOnReplicator = Config.Bind<bool>(
                   "General",
                   "EnableRightClickOnReplicator",
                   true,
                   "Enable right-click to replenish on Replicator window.").Value;
            Configs.configEnableUnlimitedAccessInNormalMode = Config.Bind<bool>(
                   "Cheat",
                   "EnableUnlimitedAccessInNormalMode",
                   false,
                   "Infinite access if not in stock in normal mode.").Value;
            new Harmony(__GUID__).PatchAll(typeof(Patch));
        }

        public static class Configs
        {
            public static bool configEnableOutgoingStorage = false;
            public static bool configEnableSearchingAllPlanets = false;
            public static bool configEnableSearchingInterstellarStations = false;
            public static bool configEnableRightClickOnReplicator = true;
            public static bool configEnableUnlimitedAccessInNormalMode = false;

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

        private static int GetFromFactoryStorages(PlanetFactory factory, int itemId, out int inc)
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

        private static int GetFromFactoryStations(PlanetFactory factory, int itemId, int count, out int inc)
        {
            inc = 0;
            if (factory == null || factory.factorySystem == null || factory.transport == null)
            {
                return 0;
            }
            int pick = count;
            int totalPicked = 0;
            foreach (StationComponent sc in factory.transport.stationPool)
            {
                if (sc == null || sc.entityId <= 0 || sc.isVeinCollector || sc.isCollector)
                {
                    continue;
                }
                if (sc.isStellar)
                {
                    int picked = pick;
                    int id = itemId;
                    sc.TakeItem(ref id, ref picked, out int inc2);
                    inc += inc2;
                    pick -= picked;
                    totalPicked += picked;
                    if (pick <= 0)
                    {
                        return totalPicked;
                    }
                }
            }
            return totalPicked;
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
            err = "Item not found";
            int picked = 0;
            int pick = StorageComponent.itemStackCount[itemId];
            Player mainPlayer = UIRoot.instance.uiGame.gameData.mainPlayer;
            int inc = 0;
            foreach (PlanetFactory factory in factories)
            {
                picked += GetFromFactoryStorages(factory, itemId, out int inc2);
                inc += inc2;
                pick -= picked;
                if (pick <= 0)
                {
                    break;
                }
                if (Configs.configEnableSearchingInterstellarStations)
                {
                    picked += GetFromFactoryStations(factory, itemId, pick, out int inc3);
                    inc += inc3;
                    pick -= picked;
                    if (pick <= 0)
                    {
                        break;
                    }
                }

            }
            if (picked > 0)
            {
                int upCount = mainPlayer.TryAddItemToPackage(itemId, picked, inc, false, 0, false);
                UIItemup.Up(itemId, upCount);
                return true;
            }
            return false;
        }

        public static bool DeliverFromBirthPlanet(int itemId, out string err)
        {
            int pick = StorageComponent.itemStackCount[itemId];
            Player mainPlayer = UIRoot.instance.uiGame.gameData.mainPlayer;
            PlanetFactory factory = BirthPlanetFactory();
            err = "Item not found";
            int picked = GetFromFactoryStorages(factory, itemId, out int inc);
            int inc2 = 0;
            if (Configs.configEnableSearchingInterstellarStations)
            {
                if (picked < pick)
                {
                    pick -= picked;
                    picked += GetFromFactoryStations(factory, itemId, pick, out inc2);
                }
            }
            if (picked > 0)
            {
                int upCount = mainPlayer.TryAddItemToPackage(itemId, picked, inc + inc2, false, 0, false);
                UIItemup.Up(itemId, upCount);
                return true;
            }
            return false;
        }

        public static bool DeliverFromVoid(int itemId, out string err)
        {
            err = "";
            Player mainPlayer = UIRoot.instance.uiGame.gameData.mainPlayer;
            int pick = StorageComponent.itemStackCount[itemId];

            int inc = 0;
            int upCount = mainPlayer.TryAddItemToPackage(itemId, pick, inc, false, 0, false);
            UIItemup.Up(itemId, upCount);
            return true;
        }

        public static bool DeliverFrom(int itemId, out string err)
        {
            if (!CheckInventoryCapacity())
            {
                err = "Inventory is full";
                return false;
            }

            if (GameMain.sandboxToolsEnabled)
            {
                return DeliverFromVoid(itemId, out err);
            }
            if (Configs.configEnableSearchingAllPlanets)
            {
                bool result = DeliverFromAllPlanets(itemId, out err);
                if (!result && Configs.configEnableUnlimitedAccessInNormalMode)
                {
                    return DeliverFromVoid(itemId, out err);
                }
                return result;
            }
            else
            {
                bool result = DeliverFromBirthPlanet(itemId, out err);
                if (!result && Configs.configEnableUnlimitedAccessInNormalMode)
                {
                    return DeliverFromVoid(itemId, out err);
                }
                return result;
            }
        }

        public static void DoDeliver(int itemId)
        {
            if (itemId < 12000 && itemId > 0)
            {
                if (DeliverFrom(itemId, out string err))
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

        private static void OnToolBtnRightClick(int obj)
        {
            UIBuildMenu buildMenu = UIRoot.instance.uiGame.buildMenu;
            if (buildMenu.childButtons.Length > obj)
            {
                UIButton uiBtn = buildMenu.childButtons[obj];
                if (uiBtn != null)
                {
                    int itemId = uiBtn.tips.itemId;
                    DoDeliver(itemId);
                }
            }
        }

        public static int ItemIdHintUnderMouse()
        {
            List<RaycastResult> targets = new List<RaycastResult>();
            PointerEventData pointer = new PointerEventData(EventSystem.current);
            pointer.position = Input.mousePosition;
            EventSystem.current.RaycastAll(pointer, targets);
            foreach (RaycastResult target in targets)
            {
                UIButton btn = target.gameObject.GetComponentInParent<UIButton>();
                if (btn?.tips != null && btn.tips.itemId > 0)
                {
                    return btn.tips.itemId;
                }

                UIReplicatorWindow repWin = target.gameObject.GetComponentInParent<UIReplicatorWindow>();
                if (repWin != null)
                {
                    int mouseRecipeIndex = AccessTools.FieldRefAccess<UIReplicatorWindow, int>(repWin, "mouseRecipeIndex");
                    RecipeProto[] recipeProtoArray = AccessTools.FieldRefAccess<UIReplicatorWindow, RecipeProto[]>(repWin, "recipeProtoArray");
                    if (mouseRecipeIndex < 0)
                    {
                        return 0;
                    }
                    RecipeProto recipeProto = recipeProtoArray[mouseRecipeIndex];
                    if (recipeProto != null)
                    {
                        return recipeProto.Results[0];
                    }
                    return 0;
                }

                UIStorageGrid grid = target.gameObject.GetComponentInParent<UIStorageGrid>();
                if (grid != null)
                {
                    StorageComponent storage = AccessTools.FieldRefAccess<UIStorageGrid, StorageComponent>(grid, "storage");
                    int mouseOnX = AccessTools.FieldRefAccess<UIStorageGrid, int>(grid, "mouseOnX");
                    int mouseOnY = AccessTools.FieldRefAccess<UIStorageGrid, int>(grid, "mouseOnY");
                    if (mouseOnX >= 0 && mouseOnY >= 0 && storage != null)
                    {
                        int num6 = mouseOnX + mouseOnY * grid.colCount;
                        return storage.grids[num6].itemId;
                    }
                    return 0;
                }

                UIProductEntry productEntry = target.gameObject.GetComponentInParent<UIProductEntry>();
                if (productEntry != null)
                {
                    if (productEntry.productionStatWindow.isProductionTab)
                    {
                        return productEntry.entryData?.itemId ?? 0;
                    }
                    return 0;
                }
            }
            return 0;
        }

        private static void OnReplicatorRightClick(int obj = 0)
        {
            if (! Configs.configEnableRightClickOnReplicator)
            {
                return;
            }
            int itemId = ItemIdHintUnderMouse();
            if (itemId > 0)
            {
                DoDeliver(itemId);
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
                    //transportation
                    protos[6, 10] = LDB.items.Select(5002); //物流船
                    protos[6, 9] = LDB.items.Select(5001); //物流ドローン
                    protos[6, 8] = LDB.items.Select(5003); //Logistics Bot
                    //storage
                    protos[4, 10] = LDB.items.Select(1210); //空間歪曲器
                    protos[4, 9] = LDB.items.Select(1803); //反物質燃料棒
                    protos[4, 8] = LDB.items.Select(1802); //重水素燃料棒
                    protos[4, 7] = LDB.items.Select(1801); //水素燃料棒

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

            [HarmonyPostfix, HarmonyPatch(typeof(UIReplicatorWindow), "OnRecipeMouseDown")]
            public static void UIReplicatorWindow_OnRecipeMouseDown_Postfix(UIReplicatorWindow __instance, BaseEventData evtData)
            {
                if (__instance != null)
                {
                    PointerEventData pointerEventData = evtData as PointerEventData;
                    if (pointerEventData != null && pointerEventData.button == PointerEventData.InputButton.Right)
                    {
                        Replenishment.OnReplicatorRightClick(0);
                    }
                }
            }

            [HarmonyPrefix, HarmonyPatch(typeof(UIReplicatorWindow), "_OnRegEvent")]
            public static void UIReplicatorWindow_OnRegEvent_Prefix(UIReplicatorWindow __instance)
            {
                List<UIButton> treeUpList = AccessTools.FieldRefAccess<UIReplicatorWindow, List<UIButton>>(__instance, "treeUpList");
                List<UIButton> treeDownList = AccessTools.FieldRefAccess<UIReplicatorWindow, List<UIButton>>(__instance, "treeDownList");

                foreach (UIButton uibutton in treeUpList)
                {
                    uibutton.onRightClick += Replenishment.OnReplicatorRightClick;
                }
                foreach (UIButton uibutton2 in treeDownList)
                {
                    uibutton2.onRightClick += Replenishment.OnReplicatorRightClick;
                }

                if (treeUpList.Count < 8)
                {
                    for (int i = treeUpList.Count; i < 8; i++)
                    {
                        UIButton uibutton5 = UnityEngine.Object.Instantiate<UIButton>(__instance.treeUpPrefab, __instance.treeUpPrefab.transform.parent);
                        uibutton5.onRightClick += Replenishment.OnReplicatorRightClick;
                        treeUpList.Add(uibutton5);
                    }
                }
                if (treeDownList.Count < 8)
                {
                    for (int i = treeDownList.Count; i < 8; i++)
                    {
                        UIButton uibutton2 = UnityEngine.Object.Instantiate<UIButton>(__instance.treeDownPrefab, __instance.treeDownPrefab.transform.parent);
                        uibutton2.onRightClick += Replenishment.OnReplicatorRightClick;
                        treeDownList.Add(uibutton2);
                    }
                }
            }

            [HarmonyPostfix, HarmonyPatch(typeof(UIReplicatorWindow), "_OnUnregEvent")]
            public static void UIReplicatorWindow_OnUnregEvent_Postfix(UIReplicatorWindow __instance)
            {
                List<UIButton> treeUpList = AccessTools.FieldRefAccess<UIReplicatorWindow, List<UIButton>>(__instance, "treeUpList");
                List<UIButton> treeDownList = AccessTools.FieldRefAccess<UIReplicatorWindow, List<UIButton>>(__instance, "treeDownList");

                foreach (UIButton uibutton in treeUpList)
                {
                    uibutton.onRightClick -= Replenishment.OnReplicatorRightClick;
                }
                foreach (UIButton uibutton2 in treeDownList)
                {
                    uibutton2.onRightClick -= Replenishment.OnReplicatorRightClick;
                }
            }
        }

    }
}
