using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Burst;
using Unity.PerformanceTesting;
using System.Linq;

/// <summary>
/// 様々なネイティブコンテナとマネージドコレクションのパフォーマンス比較テスト
/// - 連続アクセス速度の比較
/// - Jobシステムでの実行速度比較
/// </summary>
public class NativeContainerPerformanceTest
{
    // テストの要素数
    private const int ElementCount = 100000;

    // テスト用のコレクション
    private Dictionary<int, int> dictionary;
    private List<int> list;
    private NativeList<int> nativeList;
    private NativeParallelHashMap<int, int> parallelHashMap;
    private NativeHashMap<int, int> nativeHashMap;
    private UnsafeList<int> unsafeList;
    private UnsafeHashMap<int, int> unsafeHashMap;
    private NativeArray<int> nativeArray;
    private int[] regularArray; // 通常の配列
    private unsafe UnsafePtrList<int> unsafePtrList; // UnsafePtrListを追加
    private unsafe NativeArray<IntPtr> valueHolders; // ポインタ格納用バッファ

    // 各コレクションの合計値を保存（正確性検証用）
    private long dictionarySum;
    private long listSum;
    private long nativeListSum;
    private long unsafeListSum;
    private long parallelHashMapSum;
    private long nativeHashMapSum;
    private long unsafeHashMapSum;
    private long nativeArraySum;
    private long regularArraySum; // 通常の配列
    private long spanSum; // Span
    private long nativeListJobSum;
    private long unsafeListJobSum;
    private long parallelHashMapJobSum;
    private long nativeHashMapJobSum;
    private long unsafeHashMapJobSum;
    private long nativeArrayJobSum;
    private long unsafePtrListSum; // UnsafePtrList用の合計値
    private long unsafePtrListJobSum; // UnsafePtrListのJob用の合計値


    // 測定回数設定
    private const int WarmupCount = 3;
    private const int MeasurementCount = 10;
    private const int IterationsPerMeasurement = 5;

    [OneTimeSetUp]
    public void Setup()
    {
        // 標準コレクションの初期化
        dictionary = new Dictionary<int, int>(ElementCount);
        list = new List<int>(ElementCount);
        regularArray = new int[ElementCount]; // 通常の配列の初期化

        // ネイティブコンテナの初期化
        nativeList = new NativeList<int>(ElementCount, Allocator.Persistent);
        unsafeList = new UnsafeList<int>(ElementCount, Allocator.Persistent);
        parallelHashMap = new NativeParallelHashMap<int, int>(ElementCount, Allocator.Persistent);
        nativeHashMap = new NativeHashMap<int, int>(ElementCount, Allocator.Persistent);
        unsafeHashMap = new UnsafeHashMap<int, int>(ElementCount, Allocator.Persistent);
        nativeArray = new NativeArray<int>(ElementCount, Allocator.Persistent);

        // UnsafePtrList用の初期化
        unsafePtrList = new UnsafePtrList<int>(ElementCount, Allocator.Persistent);
        valueHolders = new NativeArray<IntPtr>(ElementCount, Allocator.Persistent);

        unsafe
        {
            // ポインタを持つための値をネイティブメモリに確保
            for ( int i = 0; i < ElementCount; i++ )
            {
                // IntPtrを使用して各値を個別に確保
                IntPtr ptr = (IntPtr)UnsafeUtility.Malloc(sizeof(int), UnsafeUtility.AlignOf<int>(), Allocator.Persistent);
                // 値を設定
                *(int*)ptr = i;
                // 後で解放できるようにポインタを保存
                valueHolders[i] = ptr;
                // UnsafePtrListに追加
                unsafePtrList.Add((int*)ptr);
            }
        }

        Debug.Log($"Setup complete with {ElementCount} elements capacity");
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        // 正確性の検証 - 各コレクションの合計値をNativeArrayの結果と比較
        Debug.Log("正確性検証結果:");

        if ( nativeArraySum == 0 )
        {
            Debug.Log("NativeArrayの合計値が0のため、検証をスキップします。連続アクセステストを実行してください。");
        }
        else
        {
            long expectedSum = (long)ElementCount * (ElementCount - 1) / 2; // 理論上の合計値: 0+1+2+...+(n-1)

            Debug.Log($"理論上の合計値: {expectedSum}");

            if ( dictionarySum != nativeArraySum )
                Debug.LogWarning($"不一致: Dictionary={dictionarySum}, NativeArray={nativeArraySum}");

            if ( listSum != nativeArraySum )
                Debug.LogWarning($"不一致: List={listSum}, NativeArray={nativeArraySum}");

            if ( nativeListSum != nativeArraySum )
                Debug.LogWarning($"不一致: NativeList={nativeListSum}, NativeArray={nativeArraySum}");

            if ( unsafeListSum != nativeArraySum )
                Debug.LogWarning($"不一致: UnsafeList={unsafeListSum}, NativeArray={nativeArraySum}");

            if ( unsafePtrListSum != nativeArraySum )
                Debug.LogWarning($"不一致: UnsafePtrList={unsafePtrListSum}, NativeArray={nativeArraySum}");


            if ( parallelHashMapSum != nativeArraySum )
                Debug.LogWarning($"不一致: ParallelHashMap={parallelHashMapSum}, NativeArray={nativeArraySum}");

            if ( nativeHashMapSum != nativeArraySum )
                Debug.LogWarning($"不一致: NativeHashMap={nativeHashMapSum}, NativeArray={nativeArraySum}");

            if ( unsafeHashMapSum != nativeArraySum )
                Debug.LogWarning($"不一致: UnsafeHashMap={unsafeHashMapSum}, NativeArray={nativeArraySum}");


            if ( nativeArrayJobSum != nativeArraySum )
                Debug.LogWarning($"不一致: (Job)NativeArray={nativeArrayJobSum}, NativeArray={nativeArraySum}");

            if ( nativeListJobSum != nativeArraySum )
                Debug.LogWarning($"不一致: (Job)NativeList={nativeListJobSum}, NativeArray={nativeArraySum}");

            if ( unsafeListJobSum != nativeArraySum )
                Debug.LogWarning($"不一致: (Job)UnsafeList={unsafeListJobSum}, NativeArray={nativeArraySum}");

            if ( unsafePtrListJobSum != 0 && unsafePtrListJobSum != nativeArraySum )
                Debug.LogWarning($"不一致: (Job)UnsafePtrList={unsafePtrListJobSum}, NativeArray={nativeArraySum}");

            if ( parallelHashMapJobSum != nativeArraySum )
                Debug.LogWarning($"不一致: (Job)ParallelHashMap={parallelHashMapJobSum}, NativeArray={nativeArraySum}");

            if ( nativeHashMapJobSum != nativeArraySum )
                Debug.LogWarning($"不一致: (Job)NativeHashMap={nativeHashMapJobSum}, NativeArray={nativeArraySum}");

            if ( unsafeHashMapJobSum != nativeArraySum )
                Debug.LogWarning($"不一致: (Job)UnsafeHashMap={unsafeHashMapJobSum}, NativeArray={nativeArraySum}");



            if ( regularArraySum != nativeArraySum )
                Debug.LogWarning($"不一致: RegularArray={regularArraySum}, NativeArray={nativeArraySum}");

            if ( spanSum != nativeArraySum )
                Debug.LogWarning($"不一致: Span={spanSum}, NativeArray={nativeArraySum}");

            if ( nativeArraySum != expectedSum )
                Debug.LogWarning($"不一致: NativeArray={nativeArraySum}, 理論値={expectedSum}");
            else
                Debug.Log($"一致: NativeArrayの合計値 {nativeArraySum} は理論値と一致しています");
        }

        // ネイティブコンテナの解放
        if ( nativeList.IsCreated )
            nativeList.Dispose();

        if ( unsafeList.IsCreated )
            unsafeList.Dispose();

        if ( parallelHashMap.IsCreated )
            parallelHashMap.Dispose();

        if ( nativeHashMap.IsCreated )
            nativeHashMap.Dispose();

        if ( unsafeHashMap.IsCreated )
            unsafeHashMap.Dispose();

        if ( nativeArray.IsCreated )
            nativeArray.Dispose();

        Debug.Log("All resources disposed");
    }

    #region 連続アクセステスト

    [Test, Performance]
    public unsafe void SequentialAccess_UnsafePtrList()
    {
        Measure.Method(() =>
        {
            long sum = 0;
            for ( int i = 0; i < unsafePtrList.Length; i++ )
            {
                // ポインタから値を取得して合計
                sum += *(int*)unsafePtrList[i];
            }
            unsafePtrListSum = sum;
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void SequentialAccess_RegularArray()
    {
        // 測定前にデータを準備
        for ( int i = 0; i < ElementCount; i++ )
        {
            regularArray[i] = i;
        }

        Measure.Method(() =>
        {
            long sum = 0;
            // より多くの繰り返し (例: 10回) で計測値を向上
            for ( int i = 0; i < regularArray.Length; i++ )
            {
                sum += regularArray[i];
            }
            regularArraySum = sum; // 平均値を保存
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void SequentialAccess_Span()
    {
        // 測定前にデータを準備（Spanは通常の配列から作成）
        for ( int i = 0; i < ElementCount; i++ )
        {
            regularArray[i] = i;
        }

        Measure.Method(() =>
        {
            Span<int> span = regularArray.AsSpan();
            long sum = 0;
            // より多くの繰り返し (例: 10回) で計測値を向上
            for ( int i = 0; i < span.Length; i++ )
            {
                sum += span[i];
            }
            spanSum = sum; // 平均値を保存
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void SequentialAccess_Dictionary()
    {
        // 測定前にデータを準備
        dictionary.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            dictionary[i] = i;
        }

        Measure.Method(() =>
        {
            long sum = 0;
            for ( int i = 0; i < dictionary.Count; i++ )
            {
                sum += dictionary[i];
            }
            dictionarySum = sum;
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void SequentialAccess_List()
    {
        list.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            list.Add(i);
        }

        Measure.Method(() =>
        {
            long sum = 0;
            for ( int i = 0; i < list.Count; i++ )
            {
                sum += list[i];
            }
            listSum = sum;
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void SequentialAccess_NativeList()
    {
        nativeList.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            nativeList.Add(i);
        }

        Measure.Method(() =>
        {
            long sum = 0;
            for ( int i = 0; i < nativeList.Length; i++ )
            {
                sum += nativeList[i];
            }
            nativeListSum = sum;
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void SequentialAccess_UnsafeList()
    {
        unsafeList.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            unsafeList.Add(i);
        }

        Measure.Method(() =>
        {
            long sum = 0;
            for ( int i = 0; i < unsafeList.Length; i++ )
            {
                sum += unsafeList[i];
            }
            // 記録用フィールド変数に記録。
            // コンパイラの過剰最適化予防を兼ねて値を使用し、最後に合計値の精度チェックに使用。
            unsafeListSum = sum;
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void SequentialAccess_ParallelHashMap()
    {
        parallelHashMap.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            parallelHashMap.Add(i, i);
        }

        Measure.Method(() =>
        {
            long sum = 0;
            for ( int i = 0; i < ElementCount; i++ )
            {
                if ( parallelHashMap.TryGetValue(i, out int value) )
                {
                    sum += value;
                }
            }
            parallelHashMapSum = sum;
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void SequentialAccess_NativeHashMap()
    {
        nativeHashMap.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            nativeHashMap.Add(i, i);
        }

        Measure.Method(() =>
        {
            long sum = 0;
            for ( int i = 0; i < nativeHashMap.Count; i++ )
            {
                if ( nativeHashMap.TryGetValue(i, out int value) )
                {
                    sum += value;
                }
            }
            nativeHashMapSum = sum;
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void SequentialAccess_UnsafeHashMap()
    {
        unsafeHashMap.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            unsafeHashMap.Add(i, i);
        }

        Measure.Method(() =>
        {
            long sum = 0;
            for ( int i = 0; i < unsafeHashMap.Count; i++ )
            {
                if ( unsafeHashMap.TryGetValue(i, out int value) )
                {
                    sum += value;
                }
            }
            unsafeHashMapSum = sum;
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void SequentialAccess_NativeArray()
    {
        for ( int i = 0; i < ElementCount; i++ )
        {
            nativeArray[i] = i;
        }

        Measure.Method(() =>
        {
            long sum = 0;
            for ( int i = 0; i < nativeArray.Length; i++ )
            {
                sum += nativeArray[i];
            }
            nativeArraySum = sum;
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    #endregion

    #region Jobシステムのテスト

    [Test, Performance]
    public unsafe void JobSystem_UnsafePtrList()
    {
        Measure.Method(() =>
        {
            var results = new NativeArray<long>(1, Allocator.TempJob);

            var job = new UnsafePtrListSumJob
            {
                Input = unsafePtrList,
                Result = results
            };

            job.Schedule().Complete();
            unsafePtrListJobSum = results[0];
            results.Dispose();
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void JobSystem_NativeArray()
    {
        // 測定前にデータを準備
        for ( int i = 0; i < ElementCount; i++ )
        {
            nativeArray[i] = i;
        }

        Measure.Method(() =>
        {
            var results = new NativeArray<long>(1, Allocator.TempJob);

            var job = new NativeArraySumJob
            {
                Input = nativeArray,
                Result = results
            };

            job.Schedule().Complete();
            nativeArrayJobSum = job.Result[0];
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void JobSystem_NativeList()
    {
        nativeList.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            nativeList.Add(i);
        }

        Measure.Method(() =>
        {
            var results = new NativeArray<long>(1, Allocator.TempJob);

            var job = new NativeListSumJob
            {
                Input = nativeList,
                Result = results
            };

            job.Schedule().Complete();
            nativeListJobSum = job.Result[0];
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void JobSystem_UnsafeList()
    {
        unsafeList.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            unsafeList.Add(i);
        }

        Measure.Method(() =>
        {
            var results = new NativeArray<long>(1, Allocator.TempJob);

            var job = new UnsafeListSumJob
            {
                Input = unsafeList,
                Result = results
            };

            // ジョブを実行
            job.Schedule().Complete();

            // 精度チェックのために合計値を取得
            unsafeListJobSum = job.Result[0];
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void JobSystem_ParallelHashMap()
    {
        parallelHashMap.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            parallelHashMap.Add(i, i);
        }

        Measure.Method(() =>
        {
            var results = new NativeArray<long>(1, Allocator.TempJob);

            var job = new ParallelHashMapSumJob
            {
                Input = parallelHashMap,
                Keys = new NativeArray<int>(ElementCount, Allocator.TempJob),
                Result = results
            };

            // キーを事前に準備
            for ( int i = 0; i < job.Keys.Length; i++ )
            {
                job.Keys[i] = i;
            }

            job.Schedule().Complete();
            parallelHashMapJobSum = job.Result[0];
            job.Keys.Dispose();
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void JobSystem_NativeHashMap()
    {
        nativeHashMap.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            nativeHashMap.Add(i, i);
        }

        Measure.Method(() =>
        {
            var results = new NativeArray<long>(1, Allocator.TempJob);

            var job = new NativeHashMapSumJob
            {
                Input = nativeHashMap,
                Keys = new NativeArray<int>(ElementCount, Allocator.TempJob),
                Result = results
            };

            // キーを事前に準備
            for ( int i = 0; i < job.Keys.Length; i++ )
            {
                job.Keys[i] = i;
            }

            job.Schedule().Complete();
            nativeHashMapJobSum = job.Result[0];
            job.Keys.Dispose();
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void JobSystem_UnsafeHashMap()
    {
        unsafeHashMap.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            unsafeHashMap.Add(i, i);
        }

        Measure.Method(() =>
        {
            var results = new NativeArray<long>(1, Allocator.TempJob);

            var job = new UnsafeHashMapSumJob
            {
                Input = unsafeHashMap,
                Keys = new NativeArray<int>(ElementCount, Allocator.TempJob),
                Result = results
            };

            // キーを事前に準備
            for ( int i = 0; i < job.Keys.Length; i++ )
            {
                job.Keys[i] = i;
            }

            job.Schedule().Complete();
            unsafeHashMapJobSum = job.Result[0];
            job.Keys.Dispose();
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();

    }

    //[Test, Performance]
    //public void JobSystem_ForNativeArray()
    //{
    //    // 測定前にデータを準備
    //    for ( int i = 0; i < ElementCount; i++ )
    //    {
    //        nativeArray[i] = i;
    //    }

    //    Measure.Method(() =>
    //    {
    //        var results = new NativeArray<long>(1, Allocator.TempJob);

    //        var job = new NativeArrayForJob
    //        {
    //            Input = nativeArray,
    //            Result = results
    //        };
    //        nativeArrayJobSum = job.Result[0];
    //        job.Schedule().Complete();
    //    })
    //    .WarmupCount(WarmupCount)
    //    .MeasurementCount(MeasurementCount)
    //    .IterationsPerMeasurement(IterationsPerMeasurement)
    //    .Run();
    //}

    #endregion

    #region Job構造体の定義

    [BurstCompile]
    private unsafe struct UnsafePtrListSumJob : IJob
    {
        [ReadOnly] public UnsafePtrList<int> Input;
        public NativeArray<long> Result;

        public void Execute()
        {
            long sum = 0;
            for ( int i = 0; i < Input.Length; i++ )
            {
                // ポインタから値を読み取って合計
                sum += *(Input[i]);

            }
            Result[0] = sum;
        }
    }

    [BurstCompile]
    private struct NativeArraySumJob : IJob
    {
        [ReadOnly] public NativeArray<int> Input;
        public NativeArray<long> Result;

        public void Execute()
        {
            long sum = 0;
            for ( int i = 0; i < Input.Length; i++ )
            {
                sum += Input[i];
            }
            Result[0] = sum;
        }
    }

    [BurstCompile]
    private struct NativeListSumJob : IJob
    {
        [ReadOnly] public NativeList<int> Input;
        public NativeArray<long> Result;

        public void Execute()
        {
            long sum = 0;
            for ( int i = 0; i < Input.Length; i++ )
            {
                sum += Input[i];
            }
            Result[0] = sum;
        }
    }

    [BurstCompile]
    private struct UnsafeListSumJob : IJob
    {
        [ReadOnly] public UnsafeList<int> Input;
        public NativeArray<long> Result;

        public void Execute()
        {
            long sum = 0;
            for ( int i = 0; i < Input.Length; i++ )
            {
                sum += Input[i];
            }
            Result[0] = sum;
        }
    }

    [BurstCompile]
    private struct ParallelHashMapSumJob : IJob
    {
        [ReadOnly] public NativeParallelHashMap<int, int> Input;
        [ReadOnly] public NativeArray<int> Keys;
        public NativeArray<long> Result;

        public void Execute()
        {
            long sum = 0;
            for ( int i = 0; i < Keys.Length; i++ )
            {
                if ( Input.TryGetValue(Keys[i], out int value) )
                {
                    sum += value;
                }
            }
            Result[0] = sum;
        }
    }

    [BurstCompile]
    private struct NativeHashMapSumJob : IJob
    {
        [ReadOnly] public NativeHashMap<int, int> Input;
        [ReadOnly] public NativeArray<int> Keys;
        public NativeArray<long> Result;

        public void Execute()
        {
            long sum = 0;
            for ( int i = 0; i < Keys.Length; i++ )
            {
                if ( Input.TryGetValue(Keys[i], out int value) )
                {
                    sum += value;
                }
            }
            Result[0] = sum;
        }
    }

    [BurstCompile]
    private struct UnsafeHashMapSumJob : IJob
    {
        [ReadOnly] public UnsafeHashMap<int, int> Input;
        [ReadOnly] public NativeArray<int> Keys;
        public NativeArray<long> Result;

        public void Execute()
        {
            long sum = 0;
            for ( int i = 0; i < Keys.Length; i++ )
            {
                if ( Input.TryGetValue(Keys[i], out int value) )
                {
                    sum += value;
                }
            }
            Result[0] = sum;
        }
    }

    [BurstCompile]
    private struct NativeArrayForJob : IJob
    {
        [ReadOnly] public NativeArray<int> Input;
        [NativeDisableParallelForRestriction] public NativeArray<long> Result;

        public void Execute()
        {
            // 各スレッドからのアクセスを同期するためにアトミック加算を使用
            // NativeArrayの値を結果配列に足していく
            for ( int i = 0; i < Input.Length; i++ )
            {
                Result[0] += Input[i];
            }
        }
    }

    #endregion
}