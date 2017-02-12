using System;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;
using cAlgo.Indicators;

namespace cAlgo
{
    [Indicator(IsOverlay = false, TimeZone = TimeZones.UTC, ScalePrecision = 5, AccessRights = AccessRights.None)]
    public class LasyATR : Indicator
    {
        private TrueRange tr;
        private DateTime barTime;
        private double alpha;

        [Parameter(DefaultValue = 50, MinValue = 2)]
        public int Period { get; set; }


        [Output("LasyATR", Color = Colors.Orange)]
        public IndicatorDataSeries Result { get; set; }


        protected override void Initialize()
        {
            alpha = 2.0 / (Period + 1.0);
            tr = Indicators.TrueRange();
        }

        public override void Calculate(int i)
        {


            if (i <= 2)
            {
                Result[i] = tr.Result[i];
                barTime = MarketSeries.OpenTime[i];
                return;
            }
            if (barTime == MarketSeries.OpenTime[i])
                return;
            barTime = MarketSeries.OpenTime[i];
            double tr0 = tr.Result[i - 1];
            double atr1 = Result[i - 2];
            tr0 = Math.Max(atr1 * 0.75, Math.Min(tr0, atr1 * 1.333));
            Result[i - 1] = alpha * tr0 + (1.0 - alpha) * atr1;

        }
    }
}
