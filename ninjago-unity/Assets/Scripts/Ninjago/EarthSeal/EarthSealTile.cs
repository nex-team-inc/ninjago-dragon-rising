#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One stone tile of the wall. Intact stone; a crack shakes, glows at the edges and shows a bright seam, with a
    /// monster in the seam once it pushes; a held crack grows an earth crust in the holder's color; sealed tiles go
    /// dull gold and broken tiles stay dark.
    /// </summary>
    public class EarthSealTile : MonoBehaviour
    {
        [Header("Shake Root")]
        [Tooltip("Parent of every visual; offset to shake the tile.")]
        [SerializeField] Transform shakeRoot = null!;
        [Header("Stone")]
        [SerializeField] MeshRenderer stone = null!;
        [Header("Seam")]
        [SerializeField] GameObject seam = null!;
        [Header("Edge Glow")]
        [SerializeField] GameObject edgeGlow = null!;
        [Header("Crust")]
        [SerializeField] Transform crust = null!;
        [Header("Crust Renderer")]
        [SerializeField] MeshRenderer crustRenderer = null!;
        [Header("Monster")]
        [SerializeField] Transform monster = null!;
        [Header("Hole")]
        [SerializeField] GameObject hole = null!;
        [Header("Width Stretched")]
        [Tooltip("Scaled and placed along x by the tile's aspect (authored for a square tile).")]
        [SerializeField] Transform[] widthStretched = null!;
        [Header("Width Shifted")]
        [Tooltip("Placed along x by the tile's aspect, never scaled.")]
        [SerializeField] Transform[] widthShifted = null!;
        [Header("Stone Color")]
        [SerializeField] Color stoneColor = new(0.62f, 0.56f, 0.5f);
        [Header("Sealed Color")]
        [SerializeField] Color sealedColor = new(0.74f, 0.62f, 0.32f);
        [Header("Broken Color")]
        [SerializeField] Color brokenColor = new(0.13f, 0.11f, 0.11f);
        [Header("Crust Color")]
        [SerializeField] Color crustColor = new(0.66f, 0.47f, 0.18f);
        [Header("Crust Player Tint")]
        [Tooltip("How much of the holding player's color goes into the brown-gold crust.")]
        [SerializeField, Range(0f, 1f)] float crustPlayerTint = 0.45f;
        [Header("Shake Amplitude")]
        [Tooltip("Shake offset in tile heights while a crack warns; it grows while the monster pushes.")]
        [SerializeField] float shakeAmplitude = 0.025f;

        static readonly int baseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock block = null!;
        Vector3 crustBaseScale;
        Vector3 monsterBaseScale;
        Vector3 monsterBasePosition;
        float aspect = 1f;
        SealWall.TileState? shownState;

        #region Initialization

        /// <summary>Sizes a tile authored as a unit square: uniform scale by height, stretched parts by the aspect.</summary>
        public void Initialize(float width, float height)
        {
            block = new MaterialPropertyBlock();
            aspect = width / height;
            transform.localScale = Vector3.one * height;
            foreach (var part in widthStretched)
            {
                part.localScale = new Vector3(part.localScale.x * aspect, part.localScale.y, part.localScale.z);
                part.localPosition = new Vector3(part.localPosition.x * aspect, part.localPosition.y, part.localPosition.z);
            }

            foreach (var part in widthShifted)
            {
                part.localPosition = new Vector3(part.localPosition.x * aspect, part.localPosition.y, part.localPosition.z);
            }

            crustBaseScale = crust.localScale;
            crustBaseScale.x *= aspect;
            monsterBaseScale = monster.localScale;
            monsterBasePosition = monster.localPosition;
            ShowIntact();
            shownState = SealWall.TileState.Intact;
        }

        #endregion

        #region Public API

        /// <param name="pushProgress">0..1 of the breakthrough timer while the monster pushes.</param>
        /// <param name="holderColor">Color of the player whose crust this is.</param>
        public void Show(SealWall.Tile tile, float pushProgress, Color holderColor)
        {
            // Only a live crack changes from frame to frame.
            if (!tile.IsLive && shownState == tile.State) return;
            shownState = tile.State;
            switch (tile.State)
            {
                case SealWall.TileState.Intact:
                    ShowIntact();
                    break;
                case SealWall.TileState.Warning:
                case SealWall.TileState.Pushing:
                    ShowCrack(tile, pushProgress, holderColor);
                    break;
                case SealWall.TileState.Sealed:
                    ShowResolved(sealedColor, true, holderColor);
                    break;
                case SealWall.TileState.Broken:
                    ShowResolved(brokenColor, false, holderColor);
                    break;
            }
        }

        #endregion

        #region Visual States

        void ShowIntact()
        {
            shakeRoot.localPosition = Vector3.zero;
            SetColor(stone, stoneColor);
            seam.SetActive(false);
            edgeGlow.SetActive(false);
            crust.gameObject.SetActive(false);
            monster.gameObject.SetActive(false);
            hole.SetActive(false);
        }

        void ShowCrack(SealWall.Tile tile, float pushProgress, Color holderColor)
        {
            var pushing = tile.State == SealWall.TileState.Pushing;
            var time = Time.unscaledTime;
            var amplitude = shakeAmplitude * (pushing ? 1f + pushProgress * 1.5f : 1f);
            shakeRoot.localPosition = new Vector3(Mathf.Sin(time * 47f) * amplitude, Mathf.Sin(time * 61f + 1.3f) * amplitude * 0.6f, 0f);
            SetColor(stone, stoneColor);
            seam.SetActive(true);
            edgeGlow.SetActive(Mathf.Repeat(time * (pushing ? 7f : 4f), 1f) < 0.65f);
            ShowCrust(tile.Fill, holderColor);
            monster.gameObject.SetActive(pushing);
            if (pushing)
            {
                monster.localScale = monsterBaseScale * Mathf.Lerp(0.45f, 1.15f, pushProgress);
                monster.localPosition = monsterBasePosition + new Vector3(0f, 0f, -0.25f * pushProgress);
            }

            hole.SetActive(false);
        }

        void ShowResolved(Color color, bool sealedTile, Color holderColor)
        {
            shakeRoot.localPosition = Vector3.zero;
            SetColor(stone, color);
            seam.SetActive(false);
            edgeGlow.SetActive(false);
            monster.gameObject.SetActive(false);
            hole.SetActive(!sealedTile);
            if (sealedTile) ShowCrust(1f, Color.Lerp(holderColor, sealedColor, 0.6f));
            else crust.gameObject.SetActive(false);
        }

        void ShowCrust(float fill, Color holderColor)
        {
            crust.gameObject.SetActive(fill > 0f);
            if (fill <= 0f) return;
            var size = Mathf.Lerp(0.15f, 1f, fill);
            crust.localScale = new Vector3(crustBaseScale.x * size, crustBaseScale.y * size, crustBaseScale.z);
            SetColor(crustRenderer, Color.Lerp(crustColor, holderColor, crustPlayerTint));
        }

        void SetColor(Renderer target, Color color)
        {
            target.GetPropertyBlock(block);
            block.SetColor(baseColorId, color);
            target.SetPropertyBlock(block);
        }

        #endregion
    }
}
