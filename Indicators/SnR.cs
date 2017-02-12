using System;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;
using cAlgo.Indicators;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class SnR : Indicator
    {
        private DateTime barTime;

        private double up;
        private double dn;
        private double up2;
        private double dn2;

        private int bo_flg = 0;
        private LasyATR atr;

        [Parameter("Size Damashi", DefaultValue = 0.4, MinValue = 0.1)]
        public double Size1 { get; set; }
        [Parameter("Size Modoshi", DefaultValue = 1.0, MinValue = 0.1)]
        public double Size2 { get; set; }
        [Parameter("Size Minimam Range", DefaultValue = 2.0, MinValue = 0.1)]
        public double Size3 { get; set; }
        [Parameter("Size Maximam Range", DefaultValue = 6.0, MinValue = 0.1)]
        public double Size4 { get; set; }
        [Parameter("Channel Period", DefaultValue = 30, MinValue = 1)]
        public int Period { get; set; }
        [Parameter("Look Backs", DefaultValue = 120, MinValue = 1)]
        public int LookBack { get; set; }


        [Output("Support", Color = Colors.Red, PlotType = PlotType.Points, Thickness = 2)]
        public IndicatorDataSeries Sup { get; set; }
        [Output("Resistance", Color = Colors.DodgerBlue, PlotType = PlotType.Points, Thickness = 2)]
        public IndicatorDataSeries Res { get; set; }


        protected override void Initialize()
        {
            // Initialize and create nested indicators

            atr = Indicators.GetIndicator<LasyATR>(50);


        }


        public override void Calculate(int i)
        {
            if (i <= Period + 1)
            {
                up = up2 = Highest(MarketSeries.High, Period, i - 1);
                dn = dn2 = Lowest(MarketSeries.Close, Period, i - 1);
                barTime = MarketSeries.OpenTime[i];
                return;
            }
            if (barTime == MarketSeries.OpenTime[i])
                return;
            barTime = MarketSeries.OpenTime[i];
            //--- データの取得
            double atr0 = atr.Result[i - 1];
            double min0 = Lowest(MarketSeries.Close, Period, i - 1);
            double max0 = Highest(MarketSeries.High, Period, i - 1);
            double h0 = MarketSeries.High[i - 1];
            double l0 = MarketSeries.Low[i - 1];
            double c0 = MarketSeries.Close[i - 1];
            double c1 = MarketSeries.Close[i - 2];
            double size1 = Size1 * atr0;
            double size2 = Size2 * atr0;
            double size3 = Size3 * atr0;
            double size4 = Size4 * atr0;
            //---
            //+-------------------------------------------------+
            //| 高値 安値 
            //+-------------------------------------------------+

            if (h0 > up2)
            {
                up2 = h0;
            }
            //もっと上がる
            if (l0 < dn2)
            {
                dn2 = l0;
            }
            // もっと下がる                        
            //+-------------------------------------------------+
            //| expand
            //+-------------------------------------------------+

            if (bo_flg == -1 && c0 > dn2 + size2)
            {
                bo_flg = 0;
                if (up - dn2 > size4)
                {

                    if (dn - dn2 > size3 && dn > Math.Max(h0, c1))
                        up2 = up = dn;
                    else
                    {
                        double y = h0;
                        double backs = Math.Min(i, LookBack);
                        for (int j = 1; j <= backs; j++)
                        {
                            if (MarketSeries.High[i - j] > y)
                                y = MarketSeries.High[i - j];
                            if (up < y)
                                break;
                            if (y - dn2 > size2 && MarketSeries.High[i - j] < y - size2)
                            {
                                up2 = up = y;
                                break;
                            }
                        }
                    }
                }
                dn = dn2;

            }
            if (bo_flg == 1 && c0 < up2 - size2)
            {
                bo_flg = 0;
                if (up2 - dn > size4)
                {

                    if (up2 - up > size3 && up < Math.Min(l0, c1))
                    {
                        dn2 = up;
                        dn = up;
                    }
                    else
                    {
                        double y = l0;
                        double backs = Math.Min(i, LookBack);
                        for (int j = 1; j <= backs; j++)
                        {
                            if (MarketSeries.Low[i - j] < y)
                                y = MarketSeries.Low[i - j];
                            if (dn > y)
                                break;
                            if (up2 - y > size2 && MarketSeries.Low[i - j] > y + size2)
                            {
                                dn2 = dn = y;
                                break;
                            }
                        }
                    }
                }
                up = up2;

            }
            if (up - dn > (max0 - min0) * 2)
            {
                up = up2 = max0;
                dn = dn2 = min0;
            }


            if (h0 > up + size1)
            {
                bo_flg = 1;
            }
            if (l0 < dn - size1)
            {
                bo_flg = -1;
            }

            Res[i - 1] = up;
            Sup[i - 1] = dn;


        }

        //---
        private double Highest(DataSeries arr, int range, int fromIndex)
        {
            double res;
            int i;
            res = arr[fromIndex];
            for (i = fromIndex; i > fromIndex - range && i >= 0; i--)
            {
                if (res < arr[i])
                    res = arr[i];
            }
            return (res);
        }

        //---
        private double Lowest(DataSeries arr, int range, int fromIndex)
        {
            double res;
            int i;
            res = arr[fromIndex];
            for (i = fromIndex; i > fromIndex - range && i >= 0; i--)
            {
                if (res > arr[i])
                    res = arr[i];
            }
            return (res);
        }


    }
}
