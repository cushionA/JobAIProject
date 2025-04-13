using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Collections;
using System;
using System.Linq;
using System.Diagnostics;
using Unity.Jobs;
using Unity.Burst;
using Unity.PerformanceTesting;
using UnityEngine.Profiling;

/// <summary>
/// 異なる配列タイプ（通常配列、NativeArray、Span）のパフォーマンス比較テスト
/// このテストでは単純反復、ランダムアクセス、Jobシステムでの処理など
/// 様々なシナリオでの各配列タイプの性能を測定します
/// </summary>
public class NativeArrayTest
{
    // 現実的なテストサイズ - 大きすぎると時間がかかりすぎ、小さすぎると測定が不正確になります
    const int ElementCount = 500000;

    // テスト対象の配列
    NativeArray<int> nativeArray;

    /// <summary>
    /// 並列ジョブ用
    /// </summary>
    NativeArray<int> parallelArray;

    int[] regularArray;

    int countOfRun = 50;
    int countOfMeasure = 10;
    int workerCount = 16;

    /// <summary>
    /// 各テスト実行前の準備処理
    /// 配列の初期化とストップウォッチの準備を行います
    /// </summary>
    [OneTimeSetUp]
    public void Setup()
    {

        // 通常の配列を連続した整数で初期化（0〜ElementCount-1）
        regularArray = Enumerable.Range(0, ElementCount).ToArray();

        UnityEngine.Debug.Log($"Setup complete with {ElementCount} elements");

        // Measureクラスをカスタムしてメモリも計測するようにする。
        //var allocated = new SampleGroup("TotalAllocatedMemory", SampleUnit.Megabyte);
        //var reserved = new SampleGroup("TotalReservedMemory", SampleUnit.Megabyte);
        //Measure.Custom(allocated, Profiler.GetTotalAllocatedMemoryLong() / 1048576f);
        //Measure.Custom(reserved, Profiler.GetTotalReservedMemoryLong() / 1048576f);
    }

    /// <summary>
    /// 各テスト実行後のクリーンアップ処理
    /// 特にNativeArrayのメモリリークを防ぐために必要です
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        // NativeArrayは明示的なメモリ解放が必要なため、IsCreatedでチェックしてから解放
        if ( nativeArray.IsCreated )
            nativeArray.Dispose();

        if ( parallelArray.IsCreated )
            parallelArray.Dispose();

        // 注意: 通常配列やSpanはGCが自動的に管理するため、明示的解放は不要
    }

    [Test]
    [Performance]
    public void BenchimMarkArray()
    {
        // メソッドのパフォーマンスを計測するにはMeasure.Method()を使う
        Measure.Method(() =>
        {
            SimpleIteration_RegularArray();

        })
            .WarmupCount(5) // 記録する前に何回か処理を走らせる（安定性を向上させるため）
            .IterationsPerMeasurement(countOfRun) // 計測一回辺りに走らせる処理の回数
            .MeasurementCount(countOfMeasure) // 計測数
            .Run();
    }

    [Test]
    [Performance]
    public void BenchimMarkNativeArray()
    {
        nativeArray = new NativeArray<int>(regularArray, Allocator.Temp);

        Measure.Method(() =>
        {
            SimpleIteration_NativeArray();

        })
            .WarmupCount(5) // 記録する前に何回か処理を走らせる（安定性を向上させるため）
            .IterationsPerMeasurement(countOfRun) // 計測一回辺りに走らせる処理の回数
            .MeasurementCount(countOfMeasure) // 計測数
            .Run();
    }

    [Test]
    [Performance]
    public void BenchimMarkSpan()
    {
        // メソッドのパフォーマンスを計測するにはMeasure.Method()を使う
        Measure.Method(() =>
        {
            SimpleIteration_Span();

        })
            .WarmupCount(5) // 記録する前に何回か処理を走らせる（安定性を向上させるため）
            .IterationsPerMeasurement(countOfRun) // 計測一回辺りに走らせる処理の回数
            .MeasurementCount(countOfMeasure) // 計測数
            .Run();
    }

    [Test]
    [Performance]
    public void BenchimMarkNativeArrayJob()
    {
        // メソッドのパフォーマンスを計測するにはMeasure.Method()を使う
        Measure.Method(() =>
        {
            NativeArray_WithJobs();

        })
            .WarmupCount(5) // 記録する前に何回か処理を走らせる（安定性を向上させるため）
            .IterationsPerMeasurement(countOfRun) // 計測一回辺りに走らせる処理の回数
            .MeasurementCount(countOfMeasure) // 計測数
            .Run();
    }

    [Test]
    [Performance]
    public void BenchimMarkNativeArrayParallelJob()
    {
        // メソッドのパフォーマンスを計測するにはMeasure.Method()を使う
        Measure.Method(() =>
        {
            NativeArray_WithParallelJobs();

        })
            .WarmupCount(5) // 記録する前に何回か処理を走らせる（安定性を向上させるため）
            .IterationsPerMeasurement(countOfRun) // 計測一回辺りに走らせる処理の回数
            .MeasurementCount(countOfMeasure) // 計測数
            .Run();
    }

    /// <summary>
    /// 通常配列での単純反復テスト
    /// 最も基本的な配列アクセスパターンでの性能を測定します
    /// </summary>
   // [Test]
    public void SimpleIteration_RegularArray()
    {
        long sum = 0;

        // 単純な先頭から末尾までの反復処理
        for ( int i = 0; i < regularArray.Length; i++ )
        {
            sum += regularArray[i]; // シーケンシャルアクセス
        }


        // 結果のログ出力（処理時間と計算結果）
        // 計算結果を出力するのは最適化によって処理が省略されるのを防ぐため
        UnityEngine.Debug.Log($"Regular Array  Sum: {sum}");
    }

    /// <summary>
    /// NativeArrayでの単純反復テスト
    /// 単純反復でもNativeArrayのキャッシュフレンドリーな特性が現れるか確認
    /// </summary>
 //   [Test]
    public void SimpleIteration_NativeArray()
    {
        long sum = 0;

        // NativeArrayの反復処理 - 通常配列と同じ手法
        for ( int i = 0; i < nativeArray.Length; i++ )
        {
            sum += nativeArray[i];
        }


        UnityEngine.Debug.Log($"NativeArray Sum: {sum}");

        // 注：この単純な使用方法ではNativeArrayの真の強みは現れない可能性があります
        // BurstコンパイラやJobシステムと組み合わせた場合に本領が発揮されます
    }

    /// <summary>
    /// Spanでの単純反復テスト
    /// Spanは参照セマンティクスを持ちながらも値型の特性を持つため、
    /// 通常配列と似たパフォーマンスが期待できます
    /// </summary>
    //[Test]
    public void SimpleIteration_Span()
    {
        long sum = 0;

        // 既存配列からSpanを作成（コピーではなく参照のラップ）
        Span<int> span = regularArray.AsSpan();

        for ( int i = 0; i < span.Length; i++ )
        {
            sum += span[i];
        }


        UnityEngine.Debug.Log($"Span Sum: {sum}");

        // Spanのメリット：配列のサブセット操作が効率的、値型の特性を持つため
        // メソッド間でのデータ参照が効率的、しかしJobsシステムとは連携できない
    }

    /// <summary>
    /// ランダムアクセステスト（全配列タイプ）
    /// ランダムアクセスは連続アクセスと異なるパフォーマンス特性を示します
    /// メモリキャッシュの効率性の差が顕著に現れるケースです
    /// </summary>
  //  [Test]
    public void RandomAccess_Tests()
    {
        // 乱数生成器（再現性のために固定シード使用）
        System.Random random = new System.Random(42);

        // 100万回のランダムアクセス用インデックス配列を事前生成
        int[] indices = Enumerable.Range(0, 1000000)
            .Select(_ => random.Next(0, ElementCount))
            .ToArray();

        // 各配列タイプでのランダムアクセステスト実行
        // ラムダ式を使って同じテストロジックを再利用
        TestRandomAccess("Regular Array", indices, i => regularArray[i]);
        TestRandomAccess("NativeArray", indices, i => nativeArray[i]);
        TestRandomAccess("Span", indices, i => regularArray.AsSpan()[i]);
    }

    /// <summary>
    /// ランダムアクセステスト用のヘルパーメソッド
    /// 同じテストロジックを各配列タイプに適用します
    /// </summary>
    /// <param name="name">テスト名（ログ用）</param>
    /// <param name="indices">アクセスするインデックスの配列</param>
    /// <param name="accessor">要素にアクセスするための関数</param>
    private void TestRandomAccess(string name, int[] indices, Func<int, int> accessor)
    {
        long sum = 0;

        // 事前生成したインデックスを使ってランダムアクセス
        foreach ( int index in indices )
        {
            sum += accessor(index); // 関数を通して配列にアクセス
        }


        UnityEngine.Debug.Log($"{name} random access:  Sum: {sum}");

        // ランダムアクセスはキャッシュミスを誘発し、パフォーマンスに大きく影響
        // メモリレイアウトや内部実装の違いが顕著に表れる場合があります
    }

    /// <summary>
    /// NativeArrayをJobシステムで処理するテスト
    /// これはNativeArrayの最大の強みを示すテストケースです
    /// </summary>
    public void NativeArray_WithJobs()
    {
        nativeArray = new NativeArray<int>(regularArray, Allocator.TempJob);
        var results = new NativeArray<long>(1, Allocator.TempJob);

        var job = new ProcessArrayJob
        {
            Input = nativeArray,
            Result = results
        };

        job.Schedule().Complete();
        UnityEngine.Debug.Log($"NativeArray with Jobs: Sum: {results[0]}");

        results.Dispose();
        nativeArray.Dispose();
    }
    /// <summary>
    /// NativeArrayを並列Jobシステムで処理するテスト
    /// これはNativeArrayの最大の強みを示すテストケースです
    /// </summary>
    public void NativeArray_WithParallelJobs()
    {
        parallelArray = new NativeArray<int>(regularArray, Allocator.TempJob);

        // スレッド数以上の要素数を確保（32は十分な数）
        var partialResults = new NativeArray<long>(workerCount, Allocator.TempJob);

        var job = new ProcessArrayParallelJob
        {
            Input = parallelArray,
            PartialResults = partialResults
        };

        job.Schedule(parallelArray.Length, workerCount).Complete();

        // 部分結果を集計
        long totalSum = 0;
        for ( int i = 0; i < partialResults.Length; i++ )
        {
            totalSum += partialResults[i];
        }

        UnityEngine.Debug.Log($"NativeArray with ParallelJobs: Sum: {totalSum}");

        partialResults.Dispose();
        parallelArray.Dispose();
    }

    /// <summary>
    /// 配列処理用のJob構造体
    /// BurstCompileアトリビュートを適用することで高度に最適化されたネイティブコードに変換されます
    /// </summary>
    [BurstCompile]
    public struct ProcessArrayJob : IJob
    {
        [ReadOnly] public NativeArray<int> Input;
        // 結果はNativeArrayにする
        public NativeArray<long> Result;

        public void Execute()
        {
            long sum = 0;
            for ( int i = 0; i < Input.Length; i++ )
            {
                sum += Input[i];
            }
            Result[0] = sum; // 一つの要素に結果を格納
        }
    }

    /// <summary>
    /// 配列処理用の並列Job構造体 - 各要素を並列に処理
    /// </summary>
    [BurstCompile]
    public struct ProcessArrayParallelJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> Input;
        // 各スレッドの部分結果用配列（スレッドごとに異なるインデックスに書き込む）
        [NativeDisableParallelForRestriction]
        public NativeArray<long> PartialResults;

        public void Execute(int index)
        {
            // 部分結果配列に足し込む
            PartialResults[index % PartialResults.Length] += Input[index];
        }
    }

    // 追加のテストアイデア（必要に応じて実装）：
    // 1. 配列書き込み性能の比較
    // 2. 部分配列（スライス）処理の比較
    // 3. 配列のソートやフィルタリング性能
    // 4. IJobParallelForを使った並列処理テスト
}