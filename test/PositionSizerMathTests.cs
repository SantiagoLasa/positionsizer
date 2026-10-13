using PositionSizerTool;

namespace PositionSizerTool.Tests
{
    // Tests for the pure position-sizing math. No NinjaTrader dependency.
    // Values mirror the spec (section 6.1). ES: PointValue 50, TickSize 0.25.
    public class PositionSizerMathTests
    {
        private const int Prec = 6; // double comparison precision (digits)

        // --- RiskPerContract -------------------------------------------------

        [Fact]
        public void RiskPerContract_ES_10points_is_500()
        {
            // 10 full points * $50/point = $500 per contract
            double result = PositionSizerMath.RiskPerContract(5000, 4990, 50);
            Assert.Equal(500d, result, Prec);
        }

        [Fact]
        public void RiskPerContract_is_absolute_regardless_of_direction()
        {
            double longSide = PositionSizerMath.RiskPerContract(5000, 4990, 50);
            double shortSide = PositionSizerMath.RiskPerContract(5000, 5010, 50);
            Assert.Equal(longSide, shortSide, Prec);
        }

        // --- Contracts -------------------------------------------------------

        [Fact]
        public void Contracts_rounds_down_never_exceeds_risk()
        {
            // $432 budget, $120 per contract -> 3.6 -> 3 (not 4)
            Assert.Equal(3, PositionSizerMath.Contracts(432, 120));
        }

        [Fact]
        public void Contracts_exact_division()
        {
            Assert.Equal(1, PositionSizerMath.Contracts(500, 500));
        }

        [Fact]
        public void Contracts_zero_when_budget_smaller_than_one_contract()
        {
            Assert.Equal(0, PositionSizerMath.Contracts(100, 120));
        }

        [Fact]
        public void Contracts_zero_when_risk_per_contract_is_zero()
        {
            Assert.Equal(0, PositionSizerMath.Contracts(500, 0));
        }

        [Fact]
        public void Contracts_zero_when_budget_not_positive()
        {
            Assert.Equal(0, PositionSizerMath.Contracts(0, 120));
            Assert.Equal(0, PositionSizerMath.Contracts(-50, 120));
        }

        // --- ActualRisk ------------------------------------------------------

        [Fact]
        public void ActualRisk_is_contracts_times_per_contract()
        {
            Assert.Equal(360d, PositionSizerMath.ActualRisk(3, 120), Prec);
        }

        // --- Direction -------------------------------------------------------

        [Fact]
        public void Dir_long_when_stop_below_entry()
        {
            Assert.Equal(Direction.Long, PositionSizerMath.Dir(5000, 4990));
        }

        [Fact]
        public void Dir_short_when_stop_above_entry()
        {
            Assert.Equal(Direction.Short, PositionSizerMath.Dir(5000, 5010));
        }

        [Fact]
        public void Dir_flat_when_equal()
        {
            Assert.Equal(Direction.Flat, PositionSizerMath.Dir(5000, 5000));
        }

        // --- TargetPrice -----------------------------------------------------

        [Theory]
        [InlineData(1, 5010)]
        [InlineData(2, 5020)]
        [InlineData(3, 5030)]
        public void TargetPrice_long_adds_distance_above(int n, double expected)
        {
            // entry 5000, stop 4990 -> distance 10, long -> targets above
            Assert.Equal(expected, PositionSizerMath.TargetPrice(5000, 4990, n), Prec);
        }

        [Theory]
        [InlineData(1, 4990)]
        [InlineData(2, 4980)]
        [InlineData(3, 4970)]
        public void TargetPrice_short_subtracts_distance_below(int n, double expected)
        {
            // entry 5000, stop 5010 -> distance 10, short -> targets below
            Assert.Equal(expected, PositionSizerMath.TargetPrice(5000, 5010, n), Prec);
        }

        // --- TargetProfit ----------------------------------------------------

        [Fact]
        public void TargetProfit_is_n_times_actual_risk()
        {
            // contracts 3, per-contract 120 -> actual risk 360; 2R -> 720
            Assert.Equal(720d, PositionSizerMath.TargetProfit(3, 120, 2), Prec);
        }

        [Fact]
        public void TargetProfit_1R_equals_actual_risk()
        {
            Assert.Equal(360d, PositionSizerMath.TargetProfit(3, 120, 1), Prec);
        }

        // --- DistanceTicks ---------------------------------------------------

        [Fact]
        public void DistanceTicks_ES_10points_is_40_ticks()
        {
            Assert.Equal(40d, PositionSizerMath.DistanceTicks(5000, 4990, 0.25), Prec);
        }

        [Fact]
        public void DistanceTicks_zero_tick_size_is_zero()
        {
            Assert.Equal(0d, PositionSizerMath.DistanceTicks(5000, 4990, 0), Prec);
        }

        // --- RoundToTick -----------------------------------------------------

        [Fact]
        public void RoundToTick_quarter_tick()
        {
            Assert.Equal(5000.25d, PositionSizerMath.RoundToTick(5000.30, 0.25), Prec);
        }

        [Fact]
        public void RoundToTick_whole_tick()
        {
            Assert.Equal(5001d, PositionSizerMath.RoundToTick(5000.6, 1.0), Prec);
        }

        [Fact]
        public void RoundToTick_zero_tick_returns_input()
        {
            Assert.Equal(5000.3d, PositionSizerMath.RoundToTick(5000.3, 0), Prec);
        }
    }
}
