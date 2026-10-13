using R2API;
using RoR2;
using RoR2.ContentManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace StageReport
{
    public class ContentProvider : IContentPackProvider
    {
        public string identifier => Main.PluginGUID + "." + nameof(ContentProvider);

        public static ContentPack ContentPack = new ContentPack();

        public static GameObject interactableTrackerPrefab;
        public static GameObject stageReportPanelPrefab;

        public ContentProvider()
        {
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            ContentPack.identifier = identifier;

            var assetsFolderFullPath = System.IO.Path.GetDirectoryName(typeof(ContentProvider).Assembly.Location);
            string assetDirectory = assetsFolderFullPath;

            AssetBundle stageReportBundle = null;
            yield return LoadAssetBundle(
                System.IO.Path.Combine(assetsFolderFullPath, "stageRecap"),
                args.progressReceiver,
                (assetBundle) => stageReportBundle = assetBundle);

            yield return LoadAllAssetsAsync(stageReportBundle, args.progressReceiver, (Action<InteractablesCollection[]>)((assets) =>
            {
                assets.First().Init(stageReportBundle);
            }));
            
            yield return LoadAllAssetsAsync(stageReportBundle, args.progressReceiver, (Action<GameObject[]>)((assets) =>
            {
                interactableTrackerPrefab = assets.First(a => a.name == "InteractableTracker");
                stageReportPanelPrefab = assets.First(a => a.name == "StageReportPanel");
                //rampPrefab = assets.First(a => a.name == "Ramp");
            
                foreach (var asset in assets)
                {
                    // UI prefabs are local objects; only network prefabs need registration.
                    if (asset.GetComponent<NetworkIdentity>() != null)
                    {
                        ClientScene.RegisterPrefab(asset);
                    }
                }
            }));
        }

        private IEnumerator LoadAssetBundle(string assetBundleFullPath, IProgress<float> progress, Action<AssetBundle> onAssetBundleLoaded)
        {
            var assetBundleCreateRequest = AssetBundle.LoadFromFileAsync(assetBundleFullPath);
            while (!assetBundleCreateRequest.isDone)
            {
                progress.Report(assetBundleCreateRequest.progress);
                yield return null;
            }

            onAssetBundleLoaded(assetBundleCreateRequest.assetBundle);

            yield break;
        }

        private static IEnumerator LoadAllAssetsAsync<T>(AssetBundle assetBundle, IProgress<float> progress, Action<T[]> onAssetsLoaded) where T : UnityEngine.Object
        {
            var sceneDefsRequest = assetBundle.LoadAllAssetsAsync<T>();
            while (!sceneDefsRequest.isDone)
            {
                progress.Report(sceneDefsRequest.progress);
                yield return null;
            }

            onAssetsLoaded(sceneDefsRequest.allAssets.Cast<T>().ToArray());

            yield break;
        }

        public IEnumerator GenerateContentPackAsync(GetContentPackAsyncArgs args)
        {
            ContentPack.Copy(ContentPack, args.output);

            args.ReportProgress(1f);
            yield break;
        }

        public IEnumerator FinalizeAsync(FinalizeAsyncArgs args)
        {
            args.ReportProgress(1f);
            yield break;
        }
    }
}
