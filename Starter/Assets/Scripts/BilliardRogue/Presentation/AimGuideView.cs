#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Dotted aim line (LineRenderer + BilliardRogue/AimGuide material) through the predicted polyline, glowing
    /// bounce markers at every reflection and a ghost ball at the launch origin, tinted with the active shooter's colour.
    /// </summary>
    public sealed class AimGuideView : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Wiring (WorldPrefabsBuilder)")]
        [SerializeField] LineRenderer line = null!;
        [SerializeField] Transform ghostBall = null!;
        [SerializeField] Renderer ghostRenderer = null!;
        [SerializeField] Transform[] bounceMarkers = System.Array.Empty<Transform>();
        [SerializeField] Renderer[] bounceMarkerRenderers = System.Array.Empty<Renderer>();

        ArenaLayout layout = null!;
        JuiceConfig.AimGuideSettings settings = null!;
        MaterialPropertyBlock block = null!;
        Vector3[] worldPoints = System.Array.Empty<Vector3>();
        float height;
        Color color = Color.white;

        #region Life Cycle

        public void Initialize(ArenaLayout aLayout, JuiceConfig juice)
        {
            layout = aLayout;
            settings = juice.AimGuide;
            block ??= new MaterialPropertyBlock();
            height = settings.height * layout.CellSize;
            line.useWorldSpace = true;
            line.widthMultiplier = settings.width * layout.CellSize;
            line.positionCount = 0;
            var radius = layout.Rules.ballRadius * layout.CellSize;
            ghostBall.localScale = Vector3.one * (radius * 2f);
            var markerSize = settings.bounceMarkerSize * layout.CellSize;
            for (var i = 0; i < bounceMarkers.Length; i++)
            {
                bounceMarkers[i].localScale = Vector3.one * markerSize;
                bounceMarkers[i].gameObject.SetActive(false);
            }

            SetColor(juice.Player1Color);
            SetVisible(false);
        }

        #endregion

        #region Public Methods

        public void SetColor(Color playerColor)
        {
            color = playerColor;
            var hdr = playerColor * settings.intensity;
            block.Clear();
            block.SetColor(ColorId, hdr);
            block.SetColor(BaseColorId, hdr);
            line.SetPropertyBlock(block);
            for (var i = 0; i < bounceMarkerRenderers.Length; i++)
            {
                bounceMarkerRenderers[i].SetPropertyBlock(block);
            }

            block.Clear();
            block.SetColor(BaseColorId, new Color(playerColor.r, playerColor.g, playerColor.b, 0.6f) * 1.5f);
            block.SetColor(ColorId, new Color(playerColor.r, playerColor.g, playerColor.b, 0.6f) * 1.5f);
            ghostRenderer.SetPropertyBlock(block);
        }

        /// <summary>Predicted sim-space polyline (origin first, count points).</summary>
        public void SetPath(int count, Vector2[] simPoints)
        {
            if (worldPoints.Length < simPoints.Length) worldPoints = new Vector3[simPoints.Length];
            var n = Mathf.Min(count, simPoints.Length);
            for (var i = 0; i < n; i++)
            {
                worldPoints[i] = layout.ToWorld(simPoints[i], settings.height);
            }

            line.positionCount = n;
            if (n > 0) line.SetPositions(worldPoints);
            for (var i = 0; i < bounceMarkers.Length; i++)
            {
                // Interior points are reflections; the first is the origin and the last the end of the trace.
                var pointIndex = i + 1;
                var show = pointIndex < n - 1;
                bounceMarkers[i].gameObject.SetActive(show);
                if (show) bounceMarkers[i].position = worldPoints[pointIndex];
            }

            if (n > 0) ghostBall.position = layout.ToWorld(simPoints[0], layout.Rules.ballRadius);
        }

        public void SetVisible(bool visible)
        {
            line.enabled = visible;
            ghostBall.gameObject.SetActive(visible);
            if (visible) return;
            for (var i = 0; i < bounceMarkers.Length; i++)
            {
                bounceMarkers[i].gameObject.SetActive(false);
            }
        }

        public Color Color => color;

        #endregion
    }
}
