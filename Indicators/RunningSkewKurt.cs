using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;
using cAlgo.Indicators;

namespace cAlgo
{

    [Indicator(IsOverlay = false, TimeZone = TimeZones.UTC, AutoRescale = true, AccessRights = AccessRights.None)]
    public class RunningSkewKurt : Indicator
    {
        [Parameter("Period", DefaultValue = 20, MinValue = 1)]
        public int Period { get; set; }

        [Parameter("HTF", DefaultValue = "Hour")]
        public TimeFrame TF { get; set; }
        [Output("Skewness", Color = Colors.Blue, PlotType = PlotType.Line, LineStyle = LineStyle.Solid, Thickness = 1)]
        public IndicatorDataSeries SKEW { get; set; }
        [Output("Kurtosis", Color = Colors.Red, PlotType = PlotType.Line, LineStyle = LineStyle.Solid, Thickness = 1)]
        public IndicatorDataSeries KURT { get; set; }
        [Output("Level", Color = Colors.Wheat, PlotType = PlotType.Line, LineStyle = LineStyle.Solid, Thickness = 1)]
        public IndicatorDataSeries LVL0 { get; set; }

        private MarketSeries M1;
        private MarketSeries HTF;
        private DateTime barTime;

        Queue<RunningStats> StatsQ;
        private RunningStats htfStats;

        private int prevIndexHtf = -1;
        private int prevIndex = -1;

        protected override void Initialize()
        {
            StatsQ = new Queue<RunningStats>();
            //---   
            prevIndex = -1;
            prevIndexHtf = -1;
            barTime = new DateTime(0);

            HTF = MarketData.GetSeries(TF);
            M1 = MarketData.GetSeries(TimeFrame.Minute);
            htfStats = new RunningStats();
        }

        public override void Calculate(int index)
        {
            LVL0[index] = 0;

            if (barTime == M1.OpenTime.LastValue)
                return;
            barTime = MarketSeries.OpenTime.LastValue;
            // １分のバーインデックスを取得する。
            // GetIndexByExactTime(時間) は、時刻からバーインデックスを取得する関数。
            int prev = M1.OpenTime.GetIndexByExactTime(MarketSeries.OpenTime.Last(1));
            int next = M1.OpenTime.GetIndexByExactTime(barTime);
            if (prev == -1 || next == -1)
                return;
            // RunningStatsクラスは、平均、偏差平方和、偏差三乗和、偏差四乗和を計算し、
            // 標準偏差、歪度、尖度を計算するクラス。また、計算結果のマージも行える。
            RunningStats tempStats = new RunningStats();
            // １分足データの統計情報をオンラインで計算する。
            for (int i = prev; i < next; i++)
            {
                tempStats.Push(M1.Close[i]);
            }
            // 上位足の計算結果とマージする。
            htfStats = htfStats + tempStats;
            tempStats = null;

            //上位の時間足の更新と重なったとき
            int newIndexHtf = HTF.OpenTime.GetIndexByTime(barTime);
            if (prevIndexHtf != newIndexHtf)
            {
                // 上位足の計算結果をリングバッファに追加する。
                StatsQ.Enqueue(new RunningStats((long)htfStats.n, htfStats.M1, htfStats.M2, htfStats.M3, htfStats.M4));
                // 指定期間以上バッファがたまったら、
                if (StatsQ.Count > Period)
                {
                    while (StatsQ.Count > Period)
                        StatsQ.Dequeue();
                    // 合計用のRunningStatsクラスを用意。
                    RunningStats totalStats = new RunningStats();
                    // 上位足指定期間分の計算結果をマージする。
                    foreach (RunningStats stats in StatsQ)
                    {
                        totalStats = totalStats + stats;
                    }
                    //上位足指定期間分の統計情報を求める。
                    double ma = totalStats.Mean();
                    //平均
                    double sd = totalStats.StdDev();
                    //標準偏差
                    double skew = totalStats.Skewness();
                    //歪度
                    double kurt = totalStats.Kurtosis();
                    //尖度
                    totalStats = null;
                    for (int j = prevIndex; j < index; j++)
                    {
                        SKEW[j] = skew;
                        KURT[j] = kurt;
                    }
                }
                prevIndex = index;
                prevIndexHtf = newIndexHtf;
                htfStats.Clear();
            }
        }
    }

    //---------------------------------------------------------------------------
    // Running Stats Class
    //---------------------------------------------------------------------------
    public class RunningStats : object
    {
        public long n { get; set; }
        public double M1 { get; set; }
        public double M2 { get; set; }
        public double M3 { get; set; }
        public double M4 { get; set; }



        public RunningStats()
        {
            Clear();
        }
        public RunningStats(long n_, double m1, double m2, double m3, double m4)
        {
            this.n = n_;
            this.M1 = m1;
            this.M2 = m2;
            this.M3 = m3;
            this.M4 = m4;
        }

        public void Clear()
        {
            n = 0;
            M1 = M2 = M3 = M4 = 0.0;
        }


        public void Push(double x)
        {
            double delta, delta_n, delta_n2, term1;

            long n1 = n;
            n++;
            delta = x - M1;
            delta_n = delta / n;
            delta_n2 = delta_n * delta_n;
            term1 = delta * delta_n * n1;
            M1 += delta_n;
            M4 += term1 * delta_n2 * (n * n - 3 * n + 3) + 6 * delta_n2 * M2 - 4 * delta_n * M3;
            M3 += term1 * delta_n * (n - 2) - 3 * delta_n * M2;
            M2 += term1;
        }
        public long Count()
        {
            return n;
        }
        public double Mean()
        {
            return M1;
        }
        public double Variance()
        {
            return M2 / (n - 1.0);
        }
        public double StdDev()
        {
            return Math.Sqrt(Variance());
        }
        public double Skewness()
        {
            return Math.Sqrt((double)n) * M3 / Math.Pow(M2, 1.5);
        }
        public double Kurtosis()
        {
            return ((double)n) * M4 / (M2 * M2) - 3.0;
        }

        public static RunningStats operator +(RunningStats a, RunningStats b)
        {
            //
            RunningStats combined = new RunningStats();

            combined.n = a.n + b.n;

            double delta = b.M1 - a.M1;
            double delta2 = delta * delta;
            double delta3 = delta * delta2;
            double delta4 = delta2 * delta2;

            combined.M1 = (a.n * a.M1 + b.n * b.M1) / combined.n;

            combined.M2 = a.M2 + b.M2 + delta2 * a.n * b.n / combined.n;

            combined.M3 = a.M3 + b.M3 + delta3 * a.n * b.n * (a.n - b.n) / (combined.n * combined.n);
            combined.M3 += 3.0 * delta * (a.n * b.M2 - b.n * a.M2) / combined.n;

            combined.M4 = a.M4 + b.M4 + delta4 * a.n * b.n * (a.n * a.n - a.n * b.n + b.n * b.n) / (combined.n * combined.n * combined.n);
            combined.M4 += 6.0 * delta2 * (a.n * a.n * b.M2 + b.n * b.n * a.M2) / (combined.n * combined.n) + 4.0 * delta * (a.n * b.M3 - b.n * a.M3) / combined.n;

            return combined;
        }
    }

}
