using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Consist
{
    // Small native UI mesh: no texture allocation, coroutine or timeScale dependency.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WaitingLights : MaskableGraphic
    {
        bool running;
        float startedAt, phase;
        public int ActiveDot => DotAt(phase);
        public bool Running => running;
        public static int DotAt(float seconds)
        {
            int step = Mathf.FloorToInt(Mathf.Max(0, seconds)) % 4;
            return step == 3 ? 1 : step;
        }
        public void SetWaiting(bool value)
        {
            if (running == value) return;
            running = value; startedAt = Time.unscaledTime; phase = 0;
            SetVerticesDirty();
        }
        public void Preview(float seconds) { phase = Mathf.Max(0, seconds); SetVerticesDirty(); }
        protected override void OnEnable() { base.OnEnable(); raycastTarget = false; startedAt = Time.unscaledTime; phase = 0; }
        void Update()
        {
            if (!Application.isPlaying || !running) return;
            phase = Time.unscaledTime - startedAt; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            // A compact dark capsule completely covers the three painted static dots.
            var track = new Color(.018f, .03f, .045f, 1);
            Quad(mesh, 20, -40, 112, 0, track);
            Disc(mesh, new Vector2(20, -20), 20, track, track);
            Disc(mesh, new Vector2(112, -20), 20, track, track);
            int current = DotAt(phase), previous = DotAt(Mathf.Max(0, Mathf.Floor(phase) - 1));
            float blend = phase < 1 ? 1 : Mathf.SmoothStep(0, 1, (phase % 1) / .22f);
            for (int i = 0; i < 3; i++)
            {
                var center = new Vector2(28 + i * 37.5f, -20);
                float strength = running ? (i == current ? blend : 0) + (i == previous ? 1 - blend : 0) : 0;
                Disc(mesh, center, 18, new Color(0, .7f, 1, .55f * strength), new Color(0, .6f, 1, 0));
                Disc(mesh, center, 13, new Color(.015f, .04f, .06f), new Color(.015f, .04f, .06f));
                var fill = Color.Lerp(new Color(.18f, .28f, .36f), new Color(.02f, .82f, 1), strength);
                Disc(mesh, center, 10.5f, Color.Lerp(fill, new Color(.6f, .96f, 1), strength * .6f), fill);
            }
        }
        static void Disc(VertexHelper mesh, Vector2 center, float radius, Color inner, Color outer)
        {
            const int segments = 40;
            int first = mesh.currentVertCount;
            mesh.AddVert(center, inner, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, outer, Vector2.zero);
                if (i > 0) mesh.AddTriangle(first, first + i, first + i + 1);
            }
        }
        static void Quad(VertexHelper mesh, float left, float bottom, float right, float top, Color color)
        {
            int n = mesh.currentVertCount;
            mesh.AddVert(new Vector2(left, bottom), color, Vector2.zero); mesh.AddVert(new Vector2(left, top), color, Vector2.zero);
            mesh.AddVert(new Vector2(right, top), color, Vector2.zero); mesh.AddVert(new Vector2(right, bottom), color, Vector2.zero);
            mesh.AddTriangle(n, n + 1, n + 2); mesh.AddTriangle(n, n + 2, n + 3);
        }
    }
}
