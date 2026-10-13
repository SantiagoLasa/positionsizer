using System;

namespace PositionSizerTool
{
    // Trade direction inferred from entry/stop placement.
    public enum Direction { Long, Short, Flat }

    // Pure position-sizing math. No NinjaTrader dependency so it can be unit tested
    // outside NT8. Kept to C# 7.3-compatible syntax so it compiles inside NinjaTrader
    // (which targets .NET Framework 4.8 / C# 7.3).
    public static class PositionSizerMath
    {
        // Dollar risk of a single contract for the given stop distance.
        public static double RiskPerContract(double entry, double stop, double pointValue)
        {
            return Math.Abs(entry - stop) * pointValue;
        }

        // Whole contracts that fit the dollar budget, rounded down. Never negative.
        public static int Contracts(double riskDollars, double riskPerContract)
        {
            if (riskPerContract <= 0 || riskDollars <= 0)
                return 0;

            return (int)Math.Floor(riskDollars / riskPerContract);
        }

        // Dollar risk actually taken with the rounded contract count.
        public static double ActualRisk(int contracts, double riskPerContract)
        {
            return contracts * riskPerContract;
        }

        // Long if stop is below entry, Short if above, Flat if equal.
        public static Direction Dir(double entry, double stop)
        {
            if (stop < entry)
                return Direction.Long;
            if (stop > entry)
                return Direction.Short;
            return Direction.Flat;
        }

        // Price of the n-th R-multiple target (n = 1,2,3,...), on the correct side.
        public static double TargetPrice(double entry, double stop, int n)
        {
            double distance = Math.Abs(entry - stop);
            Direction dir = Dir(entry, stop);

            if (dir == Direction.Long)
                return entry + n * distance;
            if (dir == Direction.Short)
                return entry - n * distance;
            return entry;
        }

        // Dollar profit if the n-th target is hit, for the given contract count.
        public static double TargetProfit(int contracts, double riskPerContract, int n)
        {
            return n * ActualRisk(contracts, riskPerContract);
        }

        // Stop distance expressed in ticks.
        public static double DistanceTicks(double entry, double stop, double tickSize)
        {
            if (tickSize <= 0)
                return 0;

            return Math.Abs(entry - stop) / tickSize;
        }

        // Round a price to the nearest tick. Returns the input unchanged if tickSize <= 0.
        public static double RoundToTick(double price, double tickSize)
        {
            if (tickSize <= 0)
                return price;

            return Math.Round(price / tickSize, MidpointRounding.AwayFromZero) * tickSize;
        }
    }
}
