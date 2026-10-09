#nullable enable

using System.Collections.Generic;
using Nex.Platform;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>What a finished mini-game reports: the result view content and the analytics summary.</summary>
    public sealed class NinjagoOutcome
    {
        public LocalizedString Title { get; }
        public IReadOnlyList<ResultLine.Data> Lines { get; }
        public string AnalyticsResult { get; }
        public GameAnalyticsProperties AnalyticsDetails { get; }

        public NinjagoOutcome(LocalizedString title, IReadOnlyList<ResultLine.Data> lines, string analyticsResult, GameAnalyticsProperties analyticsDetails)
        {
            Title = title;
            Lines = lines;
            AnalyticsResult = analyticsResult;
            AnalyticsDetails = analyticsDetails;
        }
    }
}
