using System;
using System.Linq;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;
using cAlgo.Indicators;
using Combinatorics.Collections;
namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class MTA_Trend : Indicator
    {
        [Parameter(DefaultValue = 5, MinValue = 1)]
        public int MaxNumOfIntervals { get; set; }
        [Parameter(DefaultValue = 10, MinValue = 1)]
        public int MaxDepthOfTree { get; set; }
        [Parameter(DefaultValue = 0.025)]
        public double RelRmsImprovement { get; set; }

        [Parameter(DefaultValue = 4, MinValue = 1)]
        public int ZZ_Depth { get; set; }
        [Parameter(DefaultValue = 100, MinValue = 1, MaxValue = 1000)]
        public int LookBack { get; set; }

        private IndicatorDataSeries ZZ;
        private Dictionary<int, Dictionary<int, OnlineRegression>> stats;
        private List<KeyValuePair<int, double>> cachedXY;
        private DateTime barTime;
        private bool isFirst = true;

        protected override void Initialize()
        {
            isFirst = true;
            stats = new Dictionary<int, Dictionary<int, OnlineRegression>>();
            cachedXY = new List<KeyValuePair<int, double>>();
            ZZ = CreateDataSeries();
        }


        public override void Calculate(int index)
        {
            int idx = index - 1;
            if (index <= ZZ_Depth * 3 + 1)
                return;

            if (IsRealTime && isFirst)
            {

                isFirst = false;
                List<int> segments = CalcMTA();
                drawLine(segments);
                int offset = cachedXY[0].Key;
                Print(String.Join(",", segments.Select(k => k - offset).ToList()));
            }

            if (barTime == MarketSeries.OpenTime.LastValue)
                return;
            barTime = MarketSeries.OpenTime.LastValue;


            int v = findVertex(MarketSeries.Close, ZZ_Depth, idx);
            if (v != -1 && !(ZZ[v] > 0 || ZZ[v - 1] > 0 || ZZ[v + 1] > 0))
            {
                ZZ[v] = MarketSeries.Close[v];
                addCache(MarketSeries.Close, v);
                shiftCache(v - (LookBack));
                if (IsRealTime)
                {
                    List<int> segments = CalcMTA();
                    drawLine(segments);
                    int offset = cachedXY[0].Key;
                    Print(String.Join(",", segments.Select(k => k - offset).ToList()));
                }
            }
        }

        private void drawLine(List<int> segments)
        {
            ChartObjects.RemoveAllObjects();
            int sz = segments.Count;
            for (int i = 0; i < sz - 1; i++)
            {
                int j0 = segments[i];
                int j1 = segments[i + 1];
                double a = stats[j1][j0].slope();
                double b = stats[j1][j0].intercept();
                ChartObjects.DrawLine("Segment-" + j0.ToString(), j0, a * j0 + b, j1, a * j1 + b, Colors.Aqua, 2, LineStyle.Solid);
            }
        }

        private List<int> CalcMTA()
        {
            int offset = cachedXY[0].Key;

            //#計算用のバッファを初期化
            List<List<int>> approxTree = new List<List<int>>();
            approxTree.Add(new List<int>());
            double[] rmsTree = new double[MaxDepthOfTree];
            int[] segmentsNumber = new int[MaxDepthOfTree];


            approxTree[0].Add(cachedXY[0].Key);
            approxTree[0].Add(cachedXY[cachedXY.Count - 1].Key);
            rmsTree[0] = getRMS(new List<int> 
            {
                cachedXY[0].Key,
                cachedXY[cachedXY.Count - 1].Key
            });

            //#分割無しの誤差をセット
            for (int i = 1; i < MaxDepthOfTree; i++)
            {

                approxTree.Add(new List<int>());
                List<int> prevEpoch = approxTree[i - 1];
                // 一つ前の分割結果
                int currEpochesNum = prevEpoch.Count;
                // 分割数
                double[] rmsSegments = new double[currEpochesNum - 1];
                List<List<int>> newEpoches = new List<List<int>>();
                for (int j = 0; j < currEpochesNum - 1; j++)
                {
                    newEpoches.Add(new List<int>());

                    int prevBegin = prevEpoch[j];
                    int prevEnd = prevEpoch[j + 1];

                    //分割可能ならば
                    if (prevEnd - prevBegin > 2)
                    {
                        List<KeyValuePair<int, double>> selected = cachedXY.Where(x => x.Key >= prevBegin && x.Key <= prevEnd).ToList();
                        List<int> results = findapproximation(selected, MaxNumOfIntervals);
                        List<int> epoches = addEpochs(prevEpoch, results);
                        //#最新の誤差と分割結果をセット
                        rmsSegments[j] = getRMS(epoches);
                        newEpoches[j] = epoches;
                    }
                    else
                    {
                        rmsSegments[j] = rmsTree[i - 1];
                        newEpoches[j] = prevEpoch;
                    }
                }

                int segmentCount = currEpochesNum - 1;
                int imin = 0;
                double dmin = rmsSegments[0];
                for (int j = 1; j < segmentCount; j++)
                {
                    if (dmin > rmsSegments[j])
                    {
                        dmin = rmsSegments[j];
                        imin = j;
                    }
                }

                approxTree[i] = newEpoches[imin];
                segmentsNumber[i] = newEpoches[imin].Count - 1;
                rmsTree[i] = rmsSegments[imin];
            }

            int n = ChoiceSegments(rmsTree, RelRmsImprovement);
            return (n >= 0) ? approxTree[n] : new List<int>();
        }



        private List<int> addEpochs(List<int> prev, List<int> curr)
        {

            List<int> res = new List<int>();

            res.AddRange(prev);
            int sz = curr.Count;
            if (sz > 2)

                res.AddRange(curr.GetRange(1, sz - 2));

            res.Sort();
            return res;
        }

        private int ChoiceSegments(double[] rmsTree, double inprovement)
        {
            int sz = rmsTree.Length;
            //#区間候補リストの精度を計算
            double[] rmsPlot = new double[sz];
            for (int i = 0; i < sz; i++)
            {
                rmsPlot[i] = rmsTree[i] * (1.0 / rmsTree[0]);
            }

            //#条件を満たす精度の区間を取得
            int level = 0;
            for (int i = 0; i < sz - 1; i++)
            {
                if (rmsPlot[i] - rmsPlot[i + 1] < inprovement)
                {
                    level = i;
                    break;
                }
            }
            return level;
        }

        private List<int> _findmaxmin(List<KeyValuePair<int, double>> arrXY, double a, double b)
        {
            int sz = arrXY.Count;
            double dmax = 0.0;
            double dmin = 0.0;
            int imax = 0;
            int imin = 0;
            for (int i = 0; i < sz; i++)
            {
                double y = a * arrXY[i].Key + b;
                double v = arrXY[i].Value - y;
                if (v > dmax)
                {
                    dmax = v;
                    imax = i;
                }
                if (v < dmin)
                {
                    dmin = v;
                    imin = i;
                }
            }
            List<int> epoches = new List<int>();
            if (imin > 0 && imin < sz - 1)
                epoches.Add(arrXY[imin].Key);
            if (imax > 0 && imax < sz - 1)
                epoches.Add(arrXY[imax].Key);
            return epoches;

        }

        //残差が最大最小となるポイントを求める"""
        private List<int> findmaxmin(List<KeyValuePair<int, double>> arrXY)
        {
            int sz = arrXY.Count;
            int ifm = arrXY[0].Key;
            int ito = arrXY[sz - 1].Key;
            double a = stats[ito][ifm].slope();
            double b = stats[ito][ifm].intercept();
            List<int> epoches1 = _findmaxmin(arrXY, a, b);
            if (epoches1.Count > 0)
                return epoches1;

            a = (arrXY[sz - 1].Value - arrXY[0].Value) / (arrXY[sz - 1].Key - arrXY[0].Key);
            b = arrXY[0].Value - a * arrXY[0].Key;
            List<int> epoches2 = _findmaxmin(arrXY, a, b);
            return epoches2;
        }
        //区間を求める
        private List<int> findepoches(List<KeyValuePair<int, double>> arrXY, int max_intervals)
        {
            int sz = arrXY.Count;
            List<int> epoches = new List<int>();
            epoches.Add(arrXY[0].Key);
            epoches.Add(arrXY[sz - 1].Key);
            int cnt = 0;
            while (epoches.Count <= max_intervals)
            {
                int currNum = epoches.Count;
                for (int i = 0; i < currNum - 1; i++)
                {
                    int j0 = epoches[i];
                    int j1 = epoches[i + 1];
                    List<KeyValuePair<int, double>> selected = arrXY.Where(x => x.Key >= j0 && x.Key <= j1).ToList();
                    List<int> newEpoches = findmaxmin(selected);
                    epoches.AddRange(newEpoches);
                }
                epoches.Sort();
                cnt++;
                if (cnt > 10)
                    break;
            }
            return epoches;
        }

        //可能な区間の組み合わせの中から最適な区間を求める
        private List<int> findapproximation(List<KeyValuePair<int, double>> arrXY, int max_intervals)
        {

            List<int> epoches = findepoches(arrXY, max_intervals);
            int sz = epoches.Count;
            List<List<int>> combinList = new List<List<int>>();
            combinList.Add(epoches);

            for (int i = 1; i < sz - 1; i++)
            {
                combinList.Add(new List<int> 
                {
                    epoches[0],
                    epoches[i],
                    epoches[sz - 1]
                });
            }
            for (int i = 2; i < sz - 2; i++)
            {

                var c = new Combinations<int>(epoches.GetRange(1, sz - 2), i);
                foreach (List<int> v in c)
                {
                    v.Insert(0, epoches[0]);
                    v.Add(epoches[sz - 1]);
                    combinList.Add(v);
                }

            }
            int conbsz = combinList.Count;

            List<double> errorList = new List<double>();
            double maxerr = 0;
            int imax = -1;

            for (int i = 0; i < conbsz; i++)
            {
                double err = getNormError(combinList[i]);
                if (imax == -1 || err > maxerr)
                {
                    maxerr = err;
                    imax = i;
                }
            }

            return combinList[imax];


        }

        //区間分割後の誤差を求める
        private double getRMS(List<int> epoches)
        {
            int sz = epoches.Count;
            double error_fit = 0.0;
            for (int i = 0; i < sz - 1; i++)
            {


                double err = stats[epoches[i + 1]][epoches[i]].residuals();
                error_fit += err;
            }
            return Math.Sqrt(error_fit);
        }

        //区間分割後の誤差を求める
        private double getNormError(List<int> epoches)
        {

            int sz = epoches.Count;
            double error_zero = Math.Sqrt(stats[epoches[sz - 1]][epoches[0]].residuals());
            double error_fit = getRMS(epoches);
            double ret;
            if (sz - 2 > 0)
            {
                ret = -Math.Log(error_fit / error_zero) / (sz - 2);
            }
            else
            {
                ret = 0.0;
            }
            return ret;
        }


        // 与えられた値より小さいインデックスを返す
        private int getPrevIndex(int v)
        {
            int sz = cachedXY.Count;
            int prev = -1;
            for (int i = sz - 1; i >= 0; i--)
            {
                if (v > cachedXY[i].Key)
                {
                    prev = i;
                    break;
                }
            }
            return prev;
        }


        private void shiftCache(int pos)
        {
            List<int> targets = new List<int>();
            int sz = cachedXY.Count;
            for (int i = 0; i < sz; i++)
                if (cachedXY[i].Key <= pos)
                    targets.Add(i);

            sz = targets.Count;

            for (int i = 0; i < sz; i++)
            {
                stats[cachedXY[i].Key].Clear();
                stats.Remove(cachedXY[i].Key);
                cachedXY.RemoveAt(i);
            }

        }
        private void addCache(DataSeries arrY, int x)
        {
            double y = arrY[x];
            OnlineRegression temp;
            //# 新しい列を作成
            stats[x] = new Dictionary<int, OnlineRegression>();
            stats[x][x] = new OnlineRegression();
            stats[x][x].push(x, y);
            int p = getPrevIndex(x);
            int prev = (p >= 0) ? cachedXY[p].Key : -1;

            //# 対象期間の値を加算
            temp = new OnlineRegression();
            for (int j = prev + 1; j <= x; j++)
                temp.push(j, arrY[j]);


            //# 直前のデータがあれば行に合計値をセット
            if (p >= 0)
            {
                int limit = x - (int)(LookBack * 1.5);
                foreach (KeyValuePair<int, OnlineRegression> kv in stats[prev])
                {
                    if (kv.Key < limit)
                        continue;
                    stats[x][kv.Key] = kv.Value + temp;
                }
            }
            //# 以降にデータがあれば
            int sz = cachedXY.Count;
            if (p + 1 < sz)
            {

                int last = cachedXY[sz - 1].Key;
                temp = new OnlineRegression();
                for (int j = x; j <= last; j++)
                {
                    temp.push(j, arrY[j]);
                    Dictionary<int, OnlineRegression> o;
                    if (stats.TryGetValue(j, out o))
                    {
                        stats[j][x] = temp;
                    }
                }

            }

            int i_ins = p + 1;
            cachedXY.Insert(i_ins, new KeyValuePair<int, double>(x, y));
        }

        private int findVertex(DataSeries arr, int depth, int i)
        {
            int period = depth * 3;
            double a = (arr[i] - arr[i - (period - 1)]) / (period - 1);
            double diff = 0;
            int jmax = -1;
            double line = arr[i];
            for (int j = 0; j < period; j++)
            {
                double v = Math.Abs(arr[i - j] - line);
                if (v > diff)
                {
                    diff = v;
                    jmax = j;
                }
                line -= a;
            }

            if (jmax >= depth && jmax < period - depth)
                return i - jmax;
            return -1;
        }


    }
    //---------------------------------------------------------------------------
    // Running Stats Class
    //---------------------------------------------------------------------------
    public class OnlineRegression : object
    {
        public long n { get; set; }
        public double x { get; set; }
        public double xx { get; set; }
        public double y { get; set; }
        public double yy { get; set; }
        public double xy { get; set; }

        public OnlineRegression()
        {
            Clear();
        }


        public static OnlineRegression operator +(OnlineRegression a, OnlineRegression b)
        {
            OnlineRegression combined = new OnlineRegression();
            combined.n = a.n + b.n;
            combined.x = a.x + b.x;
            combined.y = a.y + b.y;
            combined.xx = a.xx + b.xx;
            combined.yy = a.yy + b.yy;
            combined.xy = a.xy + b.xy;
            return combined;
        }

        // 追加
        public void push(double x, double y)
        {
            this.n++;
            this.x += x;
            this.y += y;
            this.xx += x * x;
            this.yy += y * y;
            this.xy += x * y;
        }



        public void Clear()
        {
            this.n = 0;
            this.x = 0.0;
            this.y = 0.0;
            this.xx = 0.0;
            this.yy = 0.0;
            this.xy = 0.0;
        }
        // 残差平方和
        public double residuals()
        {
            double devsqx = this.dev_sq_x();
            return devsqx > 0 ? this.dev_sq_y() - (Math.Pow(this.dev_prod_xy(), 2) / devsqx) : 0.0;
        }


        // 標準誤差
        public double stderr()
        {
            return Math.Sqrt(this.residuals()) / (this.n - 2.0);
        }

        // 切片
        public double intercept()
        {
            return this.n > 0 ? (this.y - this.slope() * this.x) / this.n : this.mean_y();
        }
        // 傾き
        public double slope()
        {
            double devsqx = this.dev_sq_x();
            return devsqx > 0 ? this.dev_prod_xy() / devsqx : 0.0;
        }
        // 平均Ｘ
        public double mean_x()
        {
            return this.n > 0 ? this.x / this.n : 0.0;
        }
        // 平均Ｙ
        public double mean_y()
        {
            return this.n > 0 ? this.y / this.n : 0.0;
        }

        // 偏差平方和 Ｘ
        public double dev_sq_x()
        {
            return this.n > 0 ? (this.xx * this.n - this.x * this.x) / this.n : 0.0;
        }

        // 偏差平方和 Ｙ
        public double dev_sq_y()
        {
            return this.n > 0 ? (this.yy * this.n - this.y * this.y) / this.n : 0.0;
        }

        // 偏差積和 ＸＹ
        public double dev_prod_xy()
        {
            return this.n > 0 ? (this.xy * this.n - this.x * this.y) / this.n : 0.0;
        }
    }
}
