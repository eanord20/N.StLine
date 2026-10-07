using System.Runtime.ExceptionServices;
using NStLine.Core.Contracts;
using NStLine.Core.Models;

namespace NStLine.Cutting.Services;

public sealed class CuttingEngine : ICuttingEngine
{
    private const int SolverStackSizeBytes = 64 * 1024 * 1024;

    public CuttingResult Solve(CuttingTask task, CuttingOptions options, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        Validate(task);
        cancellationToken.ThrowIfCancellationRequested();

        // The heuristic runs first: its result seeds the exact solver with an
        // incumbent bound, which prunes most of the branch-and-bound tree.
        var heuristicResult = FirstFitDecreasingSolver.Solve(task, options);

        var exactSolver = new ExactPatternSolver(task, options, cancellationToken);
        exactSolver.SetIncumbent(heuristicResult.TotalBarCount, heuristicResult.TotalRemainder);

        var exactResult = RunWithLargeStack(() =>
        {
            try
            {
                return exactSolver.Solve();
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        });

        if (exactResult == null)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            // Exhaustive search completed without finding anything better than
            // the heuristic: the heuristic result is proven optimal.
            if (exactSolver.SearchCompleted)
            {
                heuristicResult.Status = CalculationStatus.Optimal;
                heuristicResult.Method = "Exact branch-and-bound";
                heuristicResult.BestFoundReason = BestFoundReason.None;
            }
            else
            {
                heuristicResult.Status = CalculationStatus.BestFound;
                heuristicResult.BestFoundReason = exactSolver.PatternLimitReached
                    ? BestFoundReason.PatternLimitReached
                    : exactSolver.TimeLimitReached
                        ? BestFoundReason.TimeLimitReached
                        : BestFoundReason.ExactNotCompleted;
            }

            heuristicResult.ExploredNodes = exactSolver.ExploredNodes;
            heuristicResult.GeneratedPatterns = exactSolver.GeneratedPatterns;
            heuristicResult.PrunedByBarBound = exactSolver.PrunedByBarBound;
            heuristicResult.PrunedByRemainderBound = exactSolver.PrunedByRemainderBound;
            heuristicResult.ElapsedSeconds = exactSolver.ElapsedSeconds;

            return heuristicResult;
        }

        // If the exact solver proved optimality, prefer it even if the heuristic is equally good.
        if (exactResult.Status == CalculationStatus.Optimal)
            return exactResult;

        // Both ran but neither is proven optimal: pick the better result, keeping the honest status.
        bool exactBetter = exactResult.TotalBarCount < heuristicResult.TotalBarCount ||
                           (exactResult.TotalBarCount == heuristicResult.TotalBarCount &&
                            exactResult.TotalRemainder < heuristicResult.TotalRemainder);

        if (exactBetter)
            return exactResult;

        if (exactResult.Status == CalculationStatus.BestFound)
            heuristicResult.BestFoundReason = exactResult.BestFoundReason;
        return heuristicResult;
    }

    private static CuttingResult? RunWithLargeStack(Func<CuttingResult?> work)
    {
        // The branch-and-bound recursion depth equals the pattern count and can
        // overflow the default 1 MB stack on large inputs. A dedicated thread
        // with an enlarged stack turns a fatal StackOverflowException into a
        // normal deep recursion.
        CuttingResult? result = null;
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = work();
            }
            catch (Exception e)
            {
                failure = e;
            }
        }, SolverStackSizeBytes)
        {
            IsBackground = true
        };

        thread.Start();
        thread.Join();

        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();

        return result;
    }

    private static void Validate(CuttingTask task)
    {
        if (task.Material == null)
            throw new ArgumentException("Material is not selected.", nameof(task));

        if (task.BarLength <= 0)
            throw new ArgumentException("Bar length must be greater than zero.", nameof(task));

        if (task.CutWidth < 0 || task.CutWidth >= task.BarLength)
            throw new ArgumentException("Cut width must be non-negative and less than bar length.", nameof(task));

        if (task.Parts == null || task.Parts.Count == 0)
            throw new ArgumentException("At least one part is required.", nameof(task));

        foreach (var part in task.Parts)
        {
            if (part == null)
                continue;

            if (part.Length <= 0)
                throw new ArgumentException($"Part '{part.Name}' length must be greater than zero.", nameof(task));

            if (part.Quantity <= 0)
                throw new ArgumentException($"Part '{part.Name}' quantity must be greater than zero.", nameof(task));

            if (part.Length + task.CutWidth > task.BarLength)
                throw new ArgumentException($"Part '{part.Name}' does not fit into a single bar.", nameof(task));
        }
    }
}
