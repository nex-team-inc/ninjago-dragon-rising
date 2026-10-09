#nullable enable

namespace Nex.BilliardRogue
{
    /// <summary>ObjectPooler for EnemyView instances; every new instance is moved to the World layer.</summary>
    public sealed class EnemyViewPool : ObjectPooler<EnemyView>
    {
        /// <summary>Layer applied to created instances (-1 keeps the prefab layer).</summary>
        public int Layer { get; set; } = -1;

        protected override EnemyView CreatePooledItem()
        {
            var item = base.CreatePooledItem();
            WorldLayers.Apply(item.gameObject, Layer);
            return item;
        }
    }
}
