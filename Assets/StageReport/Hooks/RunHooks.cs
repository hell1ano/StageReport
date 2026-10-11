using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine;
using UnityEngine.Networking;

namespace StageReport
{
    public static class RunHooks
    {
        public static void Init()
        {
            On.RoR2.PreGameController.Awake += PreGameController_Awake;
            On.RoR2.Run.AdvanceStage += Run_AdvanceStage;
            On.RoR2.SceneExitController.Begin += SceneExitController_Begin;
        }

        private static void Run_AdvanceStage(On.RoR2.Run.orig_AdvanceStage orig, RoR2.Run self, RoR2.SceneDef nextScene)
        {
            if (NetworkServer.active)
            {
                InteractableTracker.instance.trackedInteractables.Clear();
            }
            orig(self, nextScene);
        }

        private static void PreGameController_Awake(On.RoR2.PreGameController.orig_Awake orig, PreGameController self)
        {
            orig(self);

            if (NetworkServer.active)
            {
                var interactableTracker = UnityEngine.Object.Instantiate(ContentProvider.interactableTrackerPrefab);
                //interactableTracker.GetComponent<RunConfig>().InitHostConfig(self.runSeed);
                NetworkServer.Spawn(interactableTracker);
            }
        }

        private static void SceneExitController_Begin(On.RoR2.SceneExitController.orig_Begin orig, SceneExitController self)
        {
            if (self.exitState == SceneExitController.ExitState.Idle)
            {
                if (NetworkServer.active && InteractableTracker.instance)
                {
                    InteractableHooks.RefreshPrinterItems();
                    InteractableHooks.RefreshWakeAltars();
                    foreach (var entry in InteractableTracker.instance.trackedInteractables)
                    {
                        var visible = ModConfig.visibleInteractables.TryGetValue(entry.type, out var setting) && setting.Value;
                        Log.Debug($"Recap entry: {entry.type}, netId={entry.netId}, remaining={entry.charges}, item={entry.itemIndex}, visible={visible}");
                    }
                }
                InteractableTracker.instance.RpcShowRecap();
            }
            orig(self);
        }
    }
}
