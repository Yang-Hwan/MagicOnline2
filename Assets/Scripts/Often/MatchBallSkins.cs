using System;
using UnityEngine;

namespace Assets.Scripts.Often
{
    public static class MatchBallSkins
    {
        static readonly string[] Styles = { "Dots", "Stripe", "Stars" };
        static readonly string[] Colors = { "White", "Yellow", "Red" };
        static readonly Material[,] Materials = new Material[3,3];

        // Online peers receive the same per-round GUID; cosmetic choice uses no physics RNG.
        public static int Choose(string matchId)
        {
            if (string.IsNullOrEmpty(matchId)) matchId = Guid.NewGuid().ToString("N");
            uint hash = 2166136261u;
            foreach (char ch in matchId) hash = unchecked((hash ^ ch) * 16777619u);
            return (int)(hash % 3);
        }

        public static void Apply(Transform ball, int style)
        {
            var source = ball.GetComponent<MeshRenderer>();
            if (!source || !source.sharedMaterial) return;
            // Use the ball's actual material: the practice and Pavilion scenes assign
            // different colours to the same ball ID.
            string current = source.sharedMaterial.name;
            int color = Array.FindIndex(Colors, c => current.StartsWith("Ball"+c, StringComparison.Ordinal));
            if (color < 0) return;
            var material = Materials[style,color];
            if (!material)
            {
                material = Resources.Load<Material>("BallSkins/Ball"+Colors[color]+"_"+Styles[style]);
                if (!material) return;
                Materials[style,color] = material;
            }
            var filter = ball.GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh) return;
            // Each scene's display mesh preserves the FBX vertices and normals. Only
            // its UVs change: the original pool-ball atlas folds different faces together.
            string meshName = filter.sharedMesh.name;
            string resource = meshName == "Ball01" ? "PavilionBallSkin" : meshName == "Ball16" ? "PracticeBallSkin" : null;
            if (resource != null)
            {
                var mapped = Resources.Load<Mesh>("BallSkins/"+resource);
                if (!mapped) return;
                filter.sharedMesh = mapped;
            }
            source.sharedMaterial = material;
            // Practice can create this renderer before or after match setup.
            var visual = ball.Find("Interpolated Ball");
            if (visual && visual.TryGetComponent<MeshRenderer>(out var mesh))
            {
                mesh.sharedMaterial = material;
                visual.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            }

            foreach (Transform child in ball)
            {
                if (child.name=="ring" || child.name=="dot" || child.name.StartsWith("dot (", StringComparison.Ordinal))
                    foreach (var mark in child.GetComponentsInChildren<MeshRenderer>(true)) mark.enabled = false;
            }
        }
    }
}
