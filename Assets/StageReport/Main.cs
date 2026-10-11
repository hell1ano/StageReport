using BepInEx;
using BepInEx.Configuration;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using RoR2;
using RoR2.ContentManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

[assembly: HG.Reflection.SearchableAttribute.OptIn]
namespace StageReport
{
    [BepInDependency("com.rune580.riskofoptions")]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class Main : BaseUnityPlugin
    {
        public static Main Instance { get; private set; }
        public const string PluginGUID = "Lawlzee.StageRecap";
        public const string PluginAuthor = "Lawlzee";
        public const string PluginName = "StageRecap";
        public const string PluginVersion = "1.4.0";

        public void Awake()
        {
            Instance = this;
            Log.Init(Logger);

            var texture = LoadTexture("icon.png");
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0, 0));
            ModSettingsManager.SetModIcon(sprite);
            ModSettingsManager.SetModDescription("At the end of each stage, StageRecap shows all the interactables on the stage and how many you’ve collected. This helps players improve at full looting stages.");

            RoR2Application.onLoad += () =>
            {
                R2API.LanguageAPI.Add("STAGERECAP_HOLLOW_CHEST_FREE_NAME", "Concept Reliquary");
                R2API.LanguageAPI.Add("STAGERECAP_COMPULSION_SHRINE_NAME", "Shrine of Compulsion");
                ModConfig.Init(Config);
            };

            RunHooks.Init();
            InteractableHooks.Init();
            //ChestRevealerHooks.Init();

            ContentManager.collectContentPackProviders += GiveToRoR2OurContentPackProviders;
        }

        private Texture2D LoadTexture(string name)
        {
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Info.Location), name)));
            return texture;
        }

        private void GiveToRoR2OurContentPackProviders(ContentManager.AddContentPackProviderDelegate addContentPackProvider)
        {
            addContentPackProvider(new ContentProvider());
        }
    }
}
