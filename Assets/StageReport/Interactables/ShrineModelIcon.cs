using System;
using System.Collections;
using UnityEngine;

namespace StageReport
{
    public static class ShrineModelIcon
    {
        private static bool pending;

        public static IEnumerator Capture(GameObject source, InteractableDef definition)
        {
            if (pending || definition.texture != null)
            {
                yield break;
            }
            pending = true;
            // Let the spawned shrine initialize its model before copying it.
            yield return null;
            try
            {
                if (source != null)
                {
                    definition.SetRuntimeTexture(Render(source));
                }
            }
            catch (Exception exception)
            {
                Log.Error("Could not render the Compulsion shrine icon: " + exception);
            }
            finally
            {
                pending = false;
            }
        }

        private static Texture2D Render(GameObject source)
        {
            var preview = new GameObject("StageRecapShrinePreview");
            preview.hideFlags = HideFlags.HideAndDontSave;
            RenderTexture target = null;
            var previousTarget = RenderTexture.active;
            try
            {
                // Copy only static model geometry, never the shrine's gameplay components.
                var hasBounds = false;
                var bounds = new Bounds();
                foreach (var renderer in source.GetComponentsInChildren<MeshRenderer>())
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (!renderer.enabled || filter == null || filter.sharedMesh == null)
                    {
                        continue;
                    }
                    var part = new GameObject("ModelPart", typeof(MeshFilter), typeof(MeshRenderer));
                    part.layer = 31;
                    part.transform.SetParent(preview.transform, false);
                    part.transform.position = renderer.transform.position - source.transform.position;
                    part.transform.rotation = renderer.transform.rotation;
                    part.transform.localScale = renderer.transform.lossyScale;
                    part.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    var copy = part.GetComponent<MeshRenderer>();
                    copy.sharedMaterials = renderer.sharedMaterials;
                    if (!hasBounds)
                    {
                        bounds = copy.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(copy.bounds);
                    }
                }
                if (!hasBounds)
                {
                    throw new InvalidOperationException("The shrine has no visible static mesh.");
                }

                // Keep the temporary model far away from the playing field.
                var offset = new Vector3(10000, 10000, 10000);
                preview.transform.position = offset;
                var center = bounds.center + offset;
                var radius = Mathf.Max(bounds.extents.magnitude, 0.1f);
                var cameraObject = new GameObject("PreviewCamera", typeof(Camera));
                cameraObject.transform.SetParent(preview.transform, false);
                var camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = radius * 1.1f;
                camera.aspect = 1;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.cullingMask = 1 << 31;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = radius * 8;
                camera.transform.rotation = Quaternion.Euler(12, 35, 0);
                camera.transform.position = center - camera.transform.forward * radius * 4;

                var lightObject = new GameObject("PreviewLight", typeof(Light));
                lightObject.transform.SetParent(preview.transform, false);
                var light = lightObject.GetComponent<Light>();
                light.type = LightType.Directional;
                light.cullingMask = 1 << 31;
                light.intensity = 1.5f;
                light.transform.rotation = Quaternion.Euler(35, -30, 0);

                target = RenderTexture.GetTemporary(128, 128, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                texture.name = "StageRecapCompulsionShrine";
                texture.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                texture.Apply();
                camera.targetTexture = null;
                return texture;
            }
            finally
            {
                RenderTexture.active = previousTarget;
                preview.SetActive(false);
                if (target != null)
                {
                    RenderTexture.ReleaseTemporary(target);
                }
                UnityEngine.Object.Destroy(preview);
            }
        }
    }
}
