using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class BolBot : Robot
    {
        // ロットサイズ
        [Parameter("Quantity (Lots)", DefaultValue = 1, MinValue = 0.01, Step = 0.01)]
        public double Quantity { get; set; }
        // オーダー距離
        [Parameter("Order Distance", DefaultValue = 5)]
        public int OrderDist { get; set; }
        // オーダータイムアウト
        [Parameter("Order Timeout Minutes", DefaultValue = 15)]
        public int TimeoutMinutes { get; set; }
        // 短期BBの期間
        [Parameter("Period1", DefaultValue = 20)]
        public int Period1 { get; set; }
        // 短期BBの閾値
        [Parameter("SD1", DefaultValue = 2.0)]
        public double SD1 { get; set; }
        // 長期BBの期間
        [Parameter("Period2", DefaultValue = 100)]
        public int Period2 { get; set; }
        // 長期BBの閾値
        [Parameter("SD2", DefaultValue = 2.5)]
        public double SD2 { get; set; }
        // エントリー開始時間
        [Parameter("Start Hour", DefaultValue = 16, MaxValue = 23)]
        public int StartHour { get; set; }
        // エントリー終了時間
        [Parameter("Stop Hour", DefaultValue = 4, MaxValue = 23)]
        public int StopHour { get; set; }
        // 保有時間
        [Parameter("ExitBars", DefaultValue = 20)]
        public int ExitBars { get; set; }
        //--- 
        private const string label = "ZScoreBot cBot";
        //---
        private BollingerBands bol;
        private BollingerBands slowBol;

        //---
        private int entryBar = 0;
        private int orderBar = 0;
        //---
        protected override void OnStart()
        {
            //--- エントリーイベント
            Positions.Opened += OnPositionOpened;
            //--- BB
            bol = Indicators.BollingerBands(MarketSeries.Close, Period1, SD1, MovingAverageType.Simple);
            slowBol = Indicators.BollingerBands(MarketSeries.Close, Period2, SD2, MovingAverageType.Simple);

        }

        protected override void OnTick()
        {

            int bars = MarketSeries.Close.Count - 1;

            var longPosition = Positions.Find(label, Symbol, TradeType.Buy);
            var shortPosition = Positions.Find(label, Symbol, TradeType.Sell);

            // ロングポジションクローズ判定            
            if (longPosition != null && (bars - entryBar) > ExitBars)  ClosePosition(longPosition);
            
            // ショートポジションクローズ判定            
            if (shortPosition != null && (bars - entryBar) > ExitBars) ClosePosition(shortPosition);

            //--- ポジションがあるか、エントリー時間外なら抜ける
            if (longPosition != null || shortPosition != null || !GetTradingHour())    return;

            //--- 同じバーでオーダーは1回だけ
           // if (orderBar == bars)  return;

            //--- オーダー済み
            foreach (var order in PendingOrders)
                if (order.Label == label) return;



            //---　インジケーターや価格情報を取得
            double slowTop0 = slowBol.Top.Last(1);
            double slowBtm0 = slowBol.Bottom.Last(1);
            double top0 = bol.Top.Last(1);
            double btm0 = bol.Bottom.Last(1);

            double h0 = MarketSeries.High.Last(0);
            double h1 = MarketSeries.High.Last(1);
            double l0 = MarketSeries.Low.Last(0);
            double l1 = MarketSeries.Low.Last(1);
            double hmax = Math.Max(h0, h1);
            double lmin = Math.Min(l0, l1);
            
            //エントリーの判定
            double dist = OrderDist * Symbol.PipSize;
            if (Symbol.Ask + dist / 2 < btm0 && lmin > slowBtm0)
            {
                long volumeInUnits = Symbol.QuantityToVolume(Quantity);
                double targetPrice = Symbol.Ask + dist;

                DateTime expirationTime = MarketSeries.OpenTime.LastValue.AddMinutes(TimeoutMinutes);
                PlaceStopOrder(TradeType.Buy, Symbol, volumeInUnits, targetPrice, label, null, null, expirationTime);
                orderBar = bars;
            }
            
            if (Symbol.Bid - dist / 2 > top0 && hmax < slowTop0)
            {
                long volumeInUnits = Symbol.QuantityToVolume(Quantity);
                double targetPrice = Symbol.Bid - dist;

                DateTime expirationTime = MarketSeries.OpenTime.LastValue.AddMinutes(TimeoutMinutes);
                PlaceStopOrder(TradeType.Sell, Symbol, volumeInUnits, targetPrice, label, null, null, expirationTime);
                orderBar = bars;
            }

        }
        //+------------------------------------------------------------------+
        //| OnPositionOpened                                                 |
        //+------------------------------------------------------------------+
        protected void OnPositionOpened(PositionOpenedEventArgs args)
        {
            var position = args.Position;
            if (position.Label == label && position.SymbolCode == Symbol.Code)
            {
                entryBar = MarketSeries.Close.Count - 1;
            }
        }

        //+------------------------------------------------------------------+
        //| Get Trading hour                                                 |
        //+------------------------------------------------------------------+
        private bool GetTradingHour()
        {
            bool res = true;
            if (StartHour != StopHour)
            {
                if ((StopHour > StartHour && Time.Hour >= StartHour && Time.Hour < StopHour) 
                    || (StartHour > StopHour && (Time.Hour >= StartHour || Time.Hour < StopHour)))
                    res = true;
                else
                    res = false;
            }
            else
            {
                res = false;
            }
            return (res);
        }




    }
}
