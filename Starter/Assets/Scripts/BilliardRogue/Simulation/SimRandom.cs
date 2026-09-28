#nullable enable

namespace Nex.BilliardRogue.Simulation
{
    /// <summary>
    /// PCG32 (O'Neill) with a fixed stream: 64-bit state, 32-bit output. The state is a plain ulong so a run
    /// can be saved mid-way and resumed deterministically. Never use UnityEngine.Random in run logic.
    /// </summary>
    public sealed class SimRandom
    {
        const ulong Multiplier = 6364136223846793005UL;
        const ulong Increment = 1442695040888963407UL;
        const float TwentyFourBitScale = 1f / 16777216f;

        ulong state;

        public SimRandom(ulong state)
        {
            this.state = state;
        }

        public ulong State => state;

        /// <summary>Uniform int in [minInclusive, maxExclusive); returns minInclusive for an empty range.</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            var span = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % span);
        }

        /// <summary>Uniform float in [0, 1) with 24 bits of precision.</summary>
        public float Value01()
        {
            return (NextUInt() >> 8) * TwentyFourBitScale;
        }

        /// <summary>True with probability chance01 (clamped to [0, 1]). Always consumes one draw.</summary>
        public bool Chance(float chance01)
        {
            return Value01() < chance01;
        }

        public uint NextUInt()
        {
            var old = state;
            state = unchecked(old * Multiplier + Increment);
            var xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            var rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        /// <summary>PCG seeding routine: two steps around the seed so nearby seeds diverge immediately.</summary>
        public static ulong SeedToState(int seed)
        {
            var s = unchecked(0UL * Multiplier + Increment);
            s = unchecked(s + (ulong)(long)seed);
            return unchecked(s * Multiplier + Increment);
        }
    }
}
