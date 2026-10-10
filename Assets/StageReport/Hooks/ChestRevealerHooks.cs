using RoR2;
using RoR2.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace StageReport
{
    public static class ChestRevealerHooks
    {
        public static bool reportOpened;
        private static bool _inRevealingContext;

        public static void Init()
        {
            On.RoR2.ChestRevealer.RevealedObject.OnEnable += RevealedObject_OnEnable;
            On.RoR2.UI.PingIndicator.GetInteractableIcon += PingIndicator_GetInteractableIcon;
        }

        private static void RevealedObject_OnEnable(On.RoR2.ChestRevealer.RevealedObject.orig_OnEnable orig, RoR2.ChestRevealer.RevealedObject self)
        {
            Log.Debug("RevealedObject_OnEnable " + self.name);
            try
            {
                _inRevealingContext = reportOpened;
                orig(self);
            }
            finally
            {
                _inRevealingContext = false;
            }
        }

        private static UnityEngine.Sprite PingIndicator_GetInteractableIcon(On.RoR2.UI.PingIndicator.orig_GetInteractableIcon orig, UnityEngine.GameObject gameObject)
        {
            Log.Debug("PingIndicator_GetInteractableIcon " + gameObject.name);

            if (!_inRevealingContext)
            {
                Log.Debug("_inRevealingContext: false");
                return orig(gameObject);
            }

            var networkIdentity = gameObject.GetComponent<NetworkIdentity>();

            if (networkIdentity == null)
            {
                Log.Debug("networkIdentity == null");
                return orig(gameObject);
            }

            int? index = InteractableHooks.FindInteractableIndex(networkIdentity.netId.Value);

            if (index == null)
            {
                Log.Debug("index == null");
                return orig(gameObject);
            }

            var interactableType = InteractableTracker.instance.trackedInteractables[index.Value].type;
            var interactableDef = InteractablesCollection.instance[interactableType];

            return Sprite.Create(interactableDef.Texture, new Rect(0, 0, interactableDef.Texture.width, interactableDef.Texture.height), new Vector2(0.5f, 0.5f));
        }
    }
}
