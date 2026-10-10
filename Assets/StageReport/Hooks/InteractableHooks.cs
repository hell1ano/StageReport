using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace StageReport
{
    public static class InteractableHooks
    {
        public static void Init()
        {
            On.RoR2.PurchaseInteraction.PreStartClient += PurchaseInteraction_PreStartClient;
            On.RoR2.PurchaseInteraction.OnDisable += PurchaseInteraction_OnDisable;
            On.RoR2.ShrineChanceBehavior.AddShrineStack += ShrineChanceBehavior_AddShrineStack;

            On.RoR2.MultiShopController.Start += MultiShopController_Start;
            On.RoR2.MultiShopController.OnPurchase += MultiShopController_OnPurchase;

            On.RoR2.DroneVendorMultiShopController.Start += DroneVendorMultiShopController_Start; ;
            On.RoR2.DroneVendorMultiShopController.OnPurchase += DroneVendorMultiShopController_OnPurchase; ;

            On.RoR2.AccessCodesMissionController.OnStartServer += AccessCodesMissionController_OnStartServer; ;
            On.RoR2.AccessCodesMissionController.ActivateCode += AccessCodesMissionController_ActivateCode;

            On.RoR2.BarrelInteraction.Start += BarrelInteraction_Start;
            On.RoR2.BarrelInteraction.CoinDrop += BarrelInteraction_CoinDrop;
            On.RoR2.ScrapperController.PreStartClient += ScrapperController_PreStartClient;

            On.RoR2.CharacterBody.Start += CharacterBody_Start;
            On.RoR2.CharacterDeathBehavior.OnDeath += CharacterDeathBehavior_OnDeath;
            On.RoR2.ShopTerminalBehavior.UpdatePickupDisplayAndAnimations += ShopTerminalBehavior_UpdatePickupDisplayAndAnimations;
        }

        private static void AccessCodesMissionController_OnStartServer(On.RoR2.AccessCodesMissionController.orig_OnStartServer orig, AccessCodesMissionController self)
        {
            orig(self);
            Log.Debug("AccessCodesMissionController_OnStartServer " + self.gameObject.name);

            if (NetworkServer.active)
            {
                bool flag = self.ignoreSolusWingDeath || !Run.instance.GetEventFlag("SolusWingBeaten");
                if (self.RunHasRequiredExpansion() & flag)
                {
                    if (self.nodes.Length > 0)
                    {
                        InteractableDef interactableDef = InteractablesCollection.instance[InteractableType.AccessCodesNode];

                        Log.Debug(interactableDef.type + " found");

                        var trackedInteractable = new TrackedInteractable
                        {
                            netId = self.GetComponent<NetworkIdentity>().netId.Value,
                            type = interactableDef.type,
                            charges = interactableDef.charges
                        };

                        InteractableTracker.instance.trackedInteractables.Add(trackedInteractable);
                    }
                }
            }
        }

        private static void AccessCodesMissionController_ActivateCode(On.RoR2.AccessCodesMissionController.orig_ActivateCode orig, AccessCodesMissionController self)
        {
            orig(self);
            if (NetworkServer.active)
            {
                uint netId = self.GetComponent<NetworkIdentity>().netId.Value;
                SetCharges(netId, 0);
            }
        }

        private static void ShopTerminalBehavior_UpdatePickupDisplayAndAnimations(On.RoR2.ShopTerminalBehavior.orig_UpdatePickupDisplayAndAnimations orig, ShopTerminalBehavior self)
        {
            orig(self);
            UpdatePrinterItem(self);
        }

        public static void RefreshPrinterItems()
        {
            if (!NetworkServer.active || !InteractableTracker.instance)
            {
                return;
            }
            foreach (var terminal in UnityEngine.Object.FindObjectsOfType<ShopTerminalBehavior>())
            {
                UpdatePrinterItem(terminal);
            }
        }

        private static void UpdatePrinterItem(ShopTerminalBehavior self)
        {
            if (!NetworkServer.active || !InteractableTracker.instance)
            {
                return;
            }
            if (self.pickup.pickupIndex == PickupIndex.none)
            {
                return;
            }

            uint netId = self.GetComponent<NetworkIdentity>().netId.Value;
            int? index = FindInteractableIndex(netId);

            if (index == null)
            {
                return;
            }

            TrackedInteractable trackedInteractable = InteractableTracker.instance.trackedInteractables[index.Value];
            InteractableDef interactableDef = InteractablesCollection.instance[trackedInteractable.type];
            if (!interactableDef.unstackable)
            {
                return;
            }

            PickupDef pickupDef = PickupCatalog.GetPickupDef(self.pickup.pickupIndex);
            ItemDef itemDef = pickupDef != null ? ItemCatalog.GetItemDef(pickupDef.itemIndex) : null;
            if (itemDef == null)
            {
                return;
            }
            trackedInteractable.itemIndex = itemDef.itemIndex;


            InteractableTracker.instance.trackedInteractables[index.Value] = trackedInteractable;
        }

        private static void CharacterDeathBehavior_OnDeath(On.RoR2.CharacterDeathBehavior.orig_OnDeath orig, CharacterDeathBehavior self)
        {
            orig(self);
            if (NetworkServer.active)
            {
                uint netId = self.GetComponent<NetworkIdentity>().netId.Value;
                SetCharges(netId, 0);
            }
        }

        private static void CharacterBody_Start(On.RoR2.CharacterBody.orig_Start orig, CharacterBody self)
        {
            orig(self);
            bool found = TryRegistering("CharacterBody_Start", self, log: false);
            if (found)
            {
                OnDisableEvent disableEvent = self.gameObject.AddComponent<OnDisableEvent>();
                disableEvent.action = new UnityEvent();
                disableEvent.action.AddListener(() =>
                {
                    Log.Debug("CharacterBody_Disabled");
                    uint netId = self.GetComponent<NetworkIdentity>().netId.Value;
                    int? index = FindInteractableIndex(netId);

                    if (index != null)
                    {
                        Log.Debug("Removed");
                        InteractableTracker.instance.trackedInteractables.RemoveAt(index.Value);
                    }
                });
            }
        }

        private static void ScrapperController_PreStartClient(On.RoR2.ScrapperController.orig_PreStartClient orig, ScrapperController self)
        {
            orig(self);
            TryRegistering("ScrapperController_Start", self);
        }

        private static void BarrelInteraction_CoinDrop(On.RoR2.BarrelInteraction.orig_CoinDrop orig, BarrelInteraction self)
        {
            orig(self);

            Log.Debug("BarrelInteraction_Start " + self.gameObject.name);
            if (NetworkServer.active)
            {
                uint netId = self.GetComponent<NetworkIdentity>().netId.Value;
                SetCharges(netId, 0);
            }
        }

        private static void BarrelInteraction_Start(On.RoR2.BarrelInteraction.orig_Start orig, BarrelInteraction self)
        {
            orig(self);
            TryRegistering("BarrelInteraction_Start", self);
        }

        private static void MultiShopController_Start(On.RoR2.MultiShopController.orig_Start orig, MultiShopController self)
        {
            orig(self);
            TryRegistering("MultiShopController_Start", self);
        }

        private static void DroneVendorMultiShopController_Start(On.RoR2.DroneVendorMultiShopController.orig_Start orig, DroneVendorMultiShopController self)
        {
            orig(self);
            TryRegistering("DroneVendorMultiShopController_Start", self);
        }

        private static bool TryRegistering(string caller, NetworkBehaviour self, bool log = true)
        {
            if (log)
            {
                Log.Debug(caller + " " + self.gameObject.name);
            }
            if (NetworkServer.active)
            {
                InteractableDef interactableDef = InteractablesCollection.instance.GetByGameObjectName(self.gameObject.name);

                if (interactableDef == null)
                {
                    if (log)
                    {
                        Log.Debug("interactableDef not found");
                    }
                    return false;
                }

                Log.Debug(interactableDef.type + " found");

                var trackedInteractable = new TrackedInteractable
                {
                    netId = self.GetComponent<NetworkIdentity>().netId.Value,
                    type = interactableDef.type,
                    charges = interactableDef.charges,
                    itemIndex = ItemIndex.None
                };

                InteractableTracker.instance.trackedInteractables.Add(trackedInteractable);
                return true;
            }

            return false;
        }

        private static void MultiShopController_OnPurchase(On.RoR2.MultiShopController.orig_OnPurchase orig, MultiShopController self, CostTypeDef.PayCostContext payCostContext, CostTypeDef.PayCostResults payCostResult)
        {
            orig(self, payCostContext, payCostResult);

            Log.Debug("MultiShopController_OnPurchase " + self.gameObject.name);
            if (NetworkServer.active)
            {
                int charges = 0;
                foreach (GameObject obj in self.terminalGameObjects)
                {
                    if (obj.GetComponent<PurchaseInteraction>().Networkavailable)
                    {
                        charges++;
                    }
                }

                uint netId = self.GetComponent<NetworkIdentity>().netId.Value;
                SetCharges(netId, charges);
            }
        }


        private static void DroneVendorMultiShopController_OnPurchase(On.RoR2.DroneVendorMultiShopController.orig_OnPurchase orig, DroneVendorMultiShopController self, CostTypeDef.PayCostContext payCostContext, CostTypeDef.PayCostResults payCostResult)
        {
            orig(self, payCostContext, payCostResult);

            Log.Debug("DroneVendorMultiShopController_OnPurchase " + self.gameObject.name);
            if (NetworkServer.active)
            {
                int charges = 0;
                foreach (var obj in self._terminals)
                {
                    if (obj.NetworkcurrentPickup != UniquePickup.none)
                    {
                        charges++;
                    }
                }

                uint netId = self.GetComponent<NetworkIdentity>().netId.Value;
                SetCharges(netId, charges);
            }
        }

        private static void ShrineChanceBehavior_AddShrineStack(On.RoR2.ShrineChanceBehavior.orig_AddShrineStack orig, ShrineChanceBehavior self, Interactor activator)
        {
            orig(self, activator);

            Log.Debug("ShrineChanceBehavior_AddShrineStack " + self.gameObject.name);
            if (NetworkServer.active)
            {
                uint netId = self.GetComponent<NetworkIdentity>().netId.Value;
                SetCharges(netId, self.maxPurchaseCount - self.successfulPurchaseCount);
            }
        }

        private static void PurchaseInteraction_OnDisable(On.RoR2.PurchaseInteraction.orig_OnDisable orig, PurchaseInteraction self)
        {
            orig(self);

            Log.Debug("PurchaseInteraction_OnDisable" + self.gameObject.name);
            if (NetworkServer.active)
            {
                if (self.gameObject.name.Contains("(Clone)"))
                {
                    Log.Debug("Contains (Clone)");
                    return;
                }

                uint netId = self.GetComponent<NetworkIdentity>().netId.Value;
                int? index = FindInteractableIndex(netId);

                if (index != null)
                {
                    Log.Debug("Removed");
                    InteractableTracker.instance.trackedInteractables.RemoveAt(index.Value);
                }
            }
        }

        private static void PurchaseInteraction_PreStartClient(On.RoR2.PurchaseInteraction.orig_PreStartClient orig, PurchaseInteraction self)
        {
            orig(self);

            Log.Debug("PurchaseInteraction_PreStartClient " + self.gameObject.name);
            var iconDefinition = InteractablesCollection.instance.GetByGameObjectName(self.gameObject.name);
            if (iconDefinition != null && iconDefinition.type == InteractableType.CompulsionShrine)
            {
                Main.Instance.StartCoroutine(ShrineModelIcon.Capture(self.gameObject, iconDefinition));
            }
            if (NetworkServer.active)
            {
                InteractableDef interactableDef = InteractablesCollection.instance.GetByGameObjectName(self.gameObject.name);

                if (interactableDef == null)
                {
                    Log.Debug("interactableDef not found");
                    return;
                }

                Log.Debug(interactableDef.type + " found");

                var trackedInteractable = new TrackedInteractable
                {
                    netId = self.GetComponent<NetworkIdentity>().netId.Value,
                    type = interactableDef.type,
                    charges = interactableDef.charges,
                    itemIndex = ItemIndex.None
                };

                InteractableTracker.instance.trackedInteractables.Add(trackedInteractable);

                if (interactableDef.charges != 1 
                    && interactableDef.type != InteractableType.BloodShrine
                    && interactableDef.type != InteractableType.WoodShrine)
                {
                    Log.Debug("charges != 1");
                    return;
                }

                self.onPurchase.AddListener(interactor =>
                {
                    // Entries can be removed after registration; resolve the current index.
                    var index = FindInteractableIndex(trackedInteractable.netId);
                    if (index.HasValue)
                    {
                        var current = InteractableTracker.instance.trackedInteractables[index.Value];
                        SetCharges(current.netId, Mathf.Max(0, current.charges - 1));
                    }
                });
            }
        }

        public static int? FindInteractableIndex(uint netId)
        {
            if (!InteractableTracker.instance || InteractableTracker.instance.trackedInteractables == null)
            {
                return null;
            }

            return InteractableTracker.instance.trackedInteractables
                .Select((x, i) => (
                    netId: x.netId,
                    index: i
                ))
                .Where(x => x.netId == netId)
                .Select(x => (int?)x.index)
                .FirstOrDefault();
        }

        private static bool SetCharges(uint netId, int charges)
        {
            int? index = FindInteractableIndex(netId);

            if (index == null)
            {
                Log.Debug("index == null. netId = " + netId);
                return false;
            }

            var trackedInteractable = InteractableTracker.instance.trackedInteractables[index.Value];
            trackedInteractable.charges = charges;
            InteractableTracker.instance.trackedInteractables[index.Value] = trackedInteractable;

            return true;
        }
    }
}
