using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echoes
{
    // Static, pixel-accurate ownership of map sprites. Each opaque prop pixel stores
    // its real collider's grid footprint, rather than the ground behind its artwork.
    internal static class PropSurfaceMap
    {
        public static RenderTexture Create(DungeonMap map)
        {
            const int pixelsPerUnit = 16;
            var target = new RenderTexture(map.MapWidth * pixelsPerUnit, map.MapHeight * pixelsPerUnit,
                0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                { name = "PropSurfaceFootprints", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            target.Create();
            var material = new Material(Resources.Load<Shader>("PropSurfaceMap"));
            var meshes = new Dictionary<Sprite, Mesh>();
            var commands = new CommandBuffer { name = "Map prop surface footprints" };
            commands.SetRenderTarget(target);
            commands.ClearRenderTarget(false, true, Color.clear);
            // Use the current generated root directly: the run root also contains
            // the cover, and the previous floor survives until deferred destruction.
            var renderers = map.GeometryRoot.GetComponentsInChildren<SpriteRenderer>();
            Array.Sort(renderers, (a, b) => a.sortingOrder != b.sortingOrder ?
                a.sortingOrder.CompareTo(b.sortingOrder) : a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            Physics2D.SyncTransforms();
            try
            {
                foreach (var renderer in renderers)
                {
                    var sprite = renderer.sprite;
                    if (!renderer.enabled || !sprite) continue;
                    if (!meshes.TryGetValue(sprite, out var mesh))
                    {
                        mesh = new Mesh { name = sprite.name + "Surface" };
                        var vertices = sprite.vertices;
                        mesh.vertices = Array.ConvertAll(vertices, v => new Vector3(v.x, v.y, 0));
                        mesh.uv = sprite.uv;
                        mesh.triangles = Array.ConvertAll(sprite.triangles, index => (int)index);
                        meshes.Add(sprite, mesh);
                    }
                    Vector4 footprint = Vector4.zero;
                    var box = renderer.GetComponent<BoxCollider2D>();
                    if (box && renderer.name.StartsWith("Obstacle_"))
                    {
                        var bounds = box.bounds;
                        footprint = new Vector4(Mathf.Round(bounds.min.x + .5f), Mathf.Round(bounds.min.y + .5f),
                            Mathf.Round(bounds.size.x), Mathf.Round(bounds.size.y));
                    }
                    var properties = new MaterialPropertyBlock();
                    properties.SetTexture("_MainTex", sprite.texture);
                    properties.SetFloat("_Opacity", renderer.color.a);
                    properties.SetVector("_Footprint", footprint / 255f);
                    properties.SetVector("_MapSize", new Vector4(map.MapWidth, map.MapHeight, 0, 0));
                    // Non-prop sprites reset ownership where they cover a prop, using
                    // the same draw order and transparent holes as the actual map.
                    commands.DrawMesh(mesh, renderer.localToWorldMatrix, material, 0, 0, properties);
                }
                Graphics.ExecuteCommandBuffer(commands);
            }
            finally
            {
                commands.Release();
                foreach (var mesh in meshes.Values) UnityEngine.Object.Destroy(mesh);
                UnityEngine.Object.Destroy(material);
            }
            return target;
        }
    }
}
