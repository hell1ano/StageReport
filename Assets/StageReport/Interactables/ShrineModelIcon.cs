using System;
using System.Collections;
using System.Linq;
using RoR2;
using UnityEngine;

namespace StageReport
{
    public static class ShrineModelIcon
    {
        private static readonly System.Collections.Generic.HashSet<InteractableType> pending = new System.Collections.Generic.HashSet<InteractableType>();

        public static IEnumerator Capture(GameObject source, InteractableDef definition)
        {
            if (pending.Contains(definition.type) || definition.texture != null)
            {
                yield break;
            }
            pending.Add(definition.type);
            try
            {
                // Models may be assigned later or referenced outside the network object's hierarchy.
                Exception lastFailure = null;
                for (int attempt = 0; attempt < 20 && source != null; attempt++)
                {
                    yield return new WaitForSeconds(0.1f);
                    Texture2D icon = null;
                    bool fatalFailure = false;
                    try
                    {
                        icon = Render(source);
                    }
                    catch (InvalidOperationException exception)
                    {
                        lastFailure = exception;
                    }
                    catch (Exception exception)
                    {
                        Log.Error("Could not render shrine icon for " + source.name + ": " + exception);
                        fatalFailure = true;
                    }
                    if (fatalFailure) yield break;
                    if (icon != null)
                    {
                        definition.SetRuntimeTexture(icon);
                        Log.Debug("Shrine icon captured: " + source.name);
                        yield break;
                    }
                }
                if (source != null)
                {
                    var locator = source.GetComponent<ModelLocator>();
                    var details = string.Join(", ", source.GetComponentsInChildren<Renderer>(true)
                        .Select(renderer => renderer.name + ":" + renderer.GetType().Name));
                    Log.Error("Could not capture shrine icon for " + source.name +
                        "; referenced model=" + (locator != null && locator.modelTransform != null
                            ? locator.modelTransform.name : "none") +
                        "; child renderers=[" + details + "]; " + lastFailure);
                }
            }
            finally
            {
                pending.Remove(definition.type);
            }
        }

        private static Texture2D Render(GameObject source)
        {
            var preview = new GameObject("StageRecapShrinePreview");
            preview.hideFlags = HideFlags.HideAndDontSave;
            RenderTexture target = null;
            var previousTarget = RenderTexture.active;
            var bakedMeshes = new System.Collections.Generic.List<Mesh>();
            try
            {
                // Copy model geometry only; bake animated meshes without gameplay components.
                var hasBounds = false;
                var bounds = new Bounds();
                var renderers = source.GetComponentsInChildren<Renderer>(true).AsEnumerable();
                var locator = source.GetComponent<ModelLocator>();
                if (locator != null && locator.modelTransform != null)
                {
                    renderers = renderers.Concat(locator.modelTransform.GetComponentsInChildren<Renderer>(true));
                }
                foreach (var renderer in renderers.Distinct())
                {
                    Mesh mesh = null;
                    var skinned = renderer as SkinnedMeshRenderer;
                    if (skinned != null && skinned.sharedMesh != null)
                    {
                        mesh = new Mesh();
                        bakedMeshes.Add(mesh);
                        skinned.BakeMesh(mesh);
                    }
                    else if (renderer is MeshRenderer)
                    {
                        var filter = renderer.GetComponent<MeshFilter>();
                        if (filter != null) mesh = filter.sharedMesh;
                    }
                    if (mesh == null) continue;
                    var part = new GameObject("ModelPart", typeof(MeshFilter), typeof(MeshRenderer));
                    part.layer = 31;
                    part.transform.SetParent(preview.transform, false);
                    part.transform.position = renderer.transform.position - source.transform.position;
                    part.transform.rotation = renderer.transform.rotation;
                    part.transform.localScale = renderer.transform.lossyScale;
                    part.GetComponent<MeshFilter>().sharedMesh = mesh;
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
                    throw new InvalidOperationException("The shrine has no visible mesh after initialization.");
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
                texture.name = "StageRecapShrineModel";
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
                foreach (var mesh in bakedMeshes) UnityEngine.Object.Destroy(mesh);
            }
        }
    }
}
