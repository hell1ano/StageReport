using RoR2;
using RoR2.UI;
using RoR2.UI.SkinControllers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace StageReport
{
    public class StageReportPanel : MonoBehaviour
    {
        public string labelPrefabKey;
        public string interactableIconPrefabKey;

        public GameObject interactablePanel;
        public GameObject printerItemIconPrefab;

        public InteractablesCollection interactablesCollection;

        public string stageLabelText;
        public Vector3 stageLabelScale;
        public Vector2 stageLabelPivot;
        public Color stageLabelColor;
        public int stageLabelFontSize;

        public Texture2D numberRamp;
        public Color noChargesColor;

        public float interactableShowInitialDelay;
        public float interactableShowDelay;

        public static void Show(IList<TrackedInteractable> trackedInteractables)
        {
            var container = (GameObject.Find("HUDSimple(Clone)") ?? GameObject.Find("RiskUI(Clone)")).transform
                .Find("MainContainer")
                .Find("MainUIArea")
                .Find("SpringCanvas");

            StageReportPanel stageReportPanel = Instantiate(ContentProvider.stageReportPanelPrefab, container).GetComponent<StageReportPanel>();
            stageReportPanel.Render(trackedInteractables);

            if (!Application.isEditor && ModConfig.revealInteractableOnStageEnd.Value)
            {
                var chestScannePrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Scanner/ChestScanner.prefab").WaitForCompletion();
                var chestScanner = Instantiate(chestScannePrefab, Camera.main.transform);
                var revealer = chestScanner.GetComponent<ChestRevealer>();
                revealer.radius = float.MaxValue;
                revealer.pulseTravelSpeed *= 2;

                OnDestroyCallback.AddCallback(chestScanner, _ => ChestRevealerHooks.reportOpened = false);
                ChestRevealerHooks.reportOpened = true;
            }
        }

        public static void Toggle(IList<TrackedInteractable> trackedInteractables)
        {
            GameObject currentPanel = (GameObject.Find("HUDSimple(Clone)") ?? GameObject.Find("RiskUI(Clone)")).transform
                .Find("MainContainer")
                .Find("MainUIArea")
                .Find("SpringCanvas")
                .Find("StageReportPanel(Clone)")
                ?.gameObject;

            if (currentPanel != null)
            {
                Destroy(currentPanel);
            }
            else
            {
                Show(trackedInteractables);
            }
        }

        public void Render(IList<TrackedInteractable> trackedInteractables)
        {
            GameObject labelPrefab = Addressables.LoadAssetAsync<GameObject>(labelPrefabKey).WaitForCompletion();
            
            var stageReportLabel = Instantiate(labelPrefab, transform);

            RectTransform labelRect = (RectTransform)stageReportLabel.transform;
            labelRect.localScale = stageLabelScale;
            labelRect.pivot = stageLabelPivot;
            labelRect.SetAsFirstSibling();

            HGTextMeshProUGUI textMesh = stageReportLabel.GetComponent<HGTextMeshProUGUI>();
            textMesh.text = stageLabelText;
            textMesh.color = stageLabelColor;
            textMesh.fontSize = stageLabelFontSize;

            StartCoroutine(RenderInteractables(textMesh, trackedInteractables));
        }

        private IEnumerator RenderInteractables(HGTextMeshProUGUI textMesh, IList<TrackedInteractable> trackedInteractables)
        {
            yield return new WaitForSeconds(interactableShowInitialDelay);

            var interactableGroups = trackedInteractables
                .Where(x => Application.isEditor || ModConfig.visibleInteractables[x.type].Value)
                .Select(x => (
                    value: x,
                    def: InteractablesCollection.instance[x.type]
                ))
                .GroupBy(x => (
                    x.value.type,
                    x.def.unstackable
                        ? Guid.NewGuid()
                        : Guid.Empty
                ))
                .Select(kvp => (
                    type: kvp.Key.type,
                    itemIndex: kvp.First().value.itemIndex,
                    charges: kvp.Select(x => x.value.charges).Sum(),
                    count: kvp.Count(),
                    def: kvp.First().def
                ))
                .OrderBy(x => InteractablesCollection.instance.GetOrder(x.type))
                .ToList();

            float currentScore = 0;

            float total = interactableGroups
                .Where(x => x.def.charges > 0)
                .Select(x => x.def.score * x.count)
                .Sum();

            foreach (var interactableGroup in interactableGroups)
            {
                var interactableIcon = CreateRecapIcon(textMesh);

                RawImage rawImage = interactableIcon.GetComponent<RawImage>();
                rawImage.texture = interactableGroup.def.Texture;

                var stackText = interactableIcon.transform.GetChild(0);
                HGTextMeshProUGUI stackLabel = stackText.GetComponent<HGTextMeshProUGUI>();
                stackText.gameObject.SetActive(true);

                if (interactableGroup.def.unstackable)
                {
                    stackLabel.text = "";
                    GameObject itemIcon = Instantiate(printerItemIconPrefab, interactableIcon.transform);
                    if (!Application.isEditor)
                    {
                        var itemDef = ItemCatalog.GetItemDef(interactableGroup.itemIndex);
                        if (itemDef != null)
                        {
                            itemIcon.GetComponent<RawImage>().texture = itemDef.pickupIconTexture;
                        }
                    }
                }
                else if (interactableGroup.def.charges == 0)
                {
                    stackLabel.text = interactableGroup.count.ToString();
                    stackLabel.color = noChargesColor;
                }
                else
                {
                    float interactablePercent = 1 - interactableGroup.charges / ((float)interactableGroup.def.charges * interactableGroup.count);
                    stackLabel.color = numberRamp.GetPixelBilinear(interactablePercent, 0);

                    currentScore += interactableGroup.def.score * (interactableGroup.count - (interactableGroup.charges / (float)interactableGroup.def.charges));
                    stackLabel.text = $"{interactableGroup.count - interactableGroup.charges / (float)interactableGroup.def.charges:0.##}/{interactableGroup.count}";
                }

                TooltipProvider tooltipProvider = interactableIcon.GetComponent<TooltipProvider>();
                tooltipProvider.titleToken = interactableGroup.def.nameToken;

                float percent = total > 0
                    ? 100 * Mathf.Clamp01(currentScore / total)
                    : 100;
                textMesh.text = $"{stageLabelText} - {Mathf.CeilToInt(percent)}%";
                yield return new WaitForSeconds(interactableShowDelay);
            }
        }

        private GameObject CreateRecapIcon(HGTextMeshProUGUI heading)
        {
            // Own this simple UI instead of relying on the game's ItemIcon hierarchy.
            var icon = new GameObject("StageRecapIcon", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(RawImage), typeof(TooltipProvider));
            icon.transform.SetParent(interactablePanel.transform, false);
            icon.GetComponent<RectTransform>().sizeDelta = new Vector2(64, 64);

            var counter = new GameObject("Counter", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(HGTextMeshProUGUI));
            counter.transform.SetParent(icon.transform, false);
            var counterRect = counter.GetComponent<RectTransform>();
            counterRect.anchorMin = Vector2.zero;
            counterRect.anchorMax = Vector2.one;
            counterRect.offsetMin = new Vector2(0, -8);
            counterRect.offsetMax = new Vector2(8, 0);

            var label = counter.GetComponent<HGTextMeshProUGUI>();
            label.font = heading.font;
            label.fontSharedMaterial = heading.fontSharedMaterial;
            label.fontSize = 20;
            label.alignment = TMPro.TextAlignmentOptions.BottomRight;
            label.enableWordWrapping = false;
            label.overflowMode = TMPro.TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return icon;
        }
    }
}
