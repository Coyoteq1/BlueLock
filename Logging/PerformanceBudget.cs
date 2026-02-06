using System.Diagnostics;

namespace VAuto.Core.Logging
{
    /// <summary>
    /// Lightweight helper for timing update loops and logging budget overruns.
    /// </summary>
    public static class PerformanceBudget
    {
        public const int DefaultBudgetMs = 16;

        public static long Start()
        {
            return Stopwatch.GetTimestamp();
        }

        public static void LogIfExceeded(string systemName, long startTimestamp, int budgetMs = DefaultBudgetMs)
        {
            var elapsedMs = (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
            if (elapsedMs > budgetMs)
            {
                VAutoLogger.LogWarning(systemName, $"Slow frame: {elapsedMs:F2}ms (budget {budgetMs}ms)");
            }
        }
    }
}
