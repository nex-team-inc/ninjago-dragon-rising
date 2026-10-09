#nullable enable

using System;
using Nex.BilliardRogue.Simulation;

namespace Nex.BilliardRogue
{
    /// <summary>The single bridge from ScriptableObject configs to the simulation's plain GameRules.</summary>
    public static class RulesFactory
    {
        public static GameRules Build(BilliardRogueConfig config)
        {
            var rules = new GameRules
            {
                arena = config.Arena.Rules,
                balance = config.Balance.Rules,
                balls = new BallRules[SimConstants.BallTypeCount],
                enemies = new EnemyRules[SimConstants.EnemyTypeCount],
                acts = new ActRules[config.Acts.Length],
            };

            for (var i = 0; i < SimConstants.BallTypeCount; i++)
            {
                var type = (BallType)i;
                var definition = config.Balls.Definitions[type];
                if (definition == null) throw new InvalidOperationException($"BallCatalog has no definition for {type}");
                rules.balls[i] = definition.Rules;
                rules.balls[i].type = type;
            }

            for (var i = 0; i < SimConstants.EnemyTypeCount; i++)
            {
                var type = (EnemyType)i;
                var definition = config.Enemies.Definitions[type];
                if (definition == null) throw new InvalidOperationException($"EnemyCatalog has no definition for {type}");
                rules.enemies[i] = definition.Rules;
                rules.enemies[i].type = type;
            }

            for (var i = 0; i < config.Acts.Length; i++)
            {
                rules.acts[i] = config.Acts[i].Rules;
                rules.acts[i].actIndex = i;
            }

            return rules;
        }
    }
}
