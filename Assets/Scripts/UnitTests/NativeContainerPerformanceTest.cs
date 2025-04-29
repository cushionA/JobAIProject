using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.PerformanceTesting;
using UnityEngine;

/// <summary>
/// 様々なネイティブコンテナとマネージドコレクションのパフォーマンス比較テスト
/// - 連続アクセス速度の比較
/// - Jobシステムでの実行速度比較
/// </summary>
public class NativeContainerPerformanceTest
{
    // テストの要素数
    private const int ElementCount = 80;

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
        this.dictionary = new Dictionary<int, int>(ElementCount);
        this.list = new List<int>(ElementCount);
        this.regularArray = new int[ElementCount]; // 通常の配列の初期化

        // ネイティブコンテナの初期化
        this.nativeList = new NativeList<int>(ElementCount, Allocator.Persistent);
        this.unsafeList = new UnsafeList<int>(ElementCount, Allocator.Persistent);
        this.parallelHashMap = new NativeParallelHashMap<int, int>(ElementCount, Allocator.Persistent);
        this.nativeHashMap = new NativeHashMap<int, int>(ElementCount, Allocator.Persistent);
        this.unsafeHashMap = new UnsafeHashMap<int, int>(ElementCount, Allocator.Persistent);
        this.nativeArray = new NativeArray<int>(ElementCount, Allocator.Persistent);

        // UnsafePtrList用の初期化
        this.unsafePtrList = new UnsafePtrList<int>(ElementCount, Allocator.Persistent);
        this.valueHolders = new NativeArray<IntPtr>(ElementCount, Allocator.Persistent);

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
                this.valueHolders[i] = ptr;
                // UnsafePtrListに追加
                this.unsafePtrList.Add((int*)ptr);
            }
        }

        Debug.Log($"Setup complete with {ElementCount} elements capacity");
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        // 正確性の検証 - 各コレクションの合計値をNativeArrayの結果と比較
        Debug.Log("正確性検証結果:");

        if ( this.nativeArraySum == 0 )
        {
            Debug.Log("NativeArrayの合計値が0のため、検証をスキップします。連続アクセステストを実行してください。");
        }
        else
        {
            long expectedSum = (long)ElementCount * (ElementCount - 1) / 2; // 理論上の合計値: 0+1+2+...+(n-1)

            Debug.Log($"理論上の合計値: {expectedSum}");

            if ( this.dictionarySum != this.nativeArraySum )
            {
                Debug.LogWarning($"不一致: Dictionary={this.dictionarySum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.listSum != this.nativeArraySum )
            {
                Debug.LogWarning($"不一致: List={this.listSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.nativeListSum != this.nativeArraySum )
            {
                Debug.LogWarning($"不一致: NativeList={this.nativeListSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.unsafeListSum != this.nativeArraySum )
            {
                Debug.LogWarning($"不一致: UnsafeList={this.unsafeListSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.unsafePtrListSum != this.nativeArraySum )
            {
                Debug.LogWarning($"不一致: UnsafePtrList={this.unsafePtrListSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.parallelHashMapSum != this.nativeArraySum )
            {
                Debug.LogWarning($"不一致: ParallelHashMap={this.parallelHashMapSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.nativeHashMapSum != this.nativeArraySum )
            {
                Debug.LogWarning($"不一致: NativeHashMap={this.nativeHashMapSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.unsafeHashMapSum != this.nativeArraySum )
            {
                Debug.LogWarning($"不一致: UnsafeHashMap={this.unsafeHashMapSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.nativeArrayJobSum != this.nativeArraySum )
            {
                Debug.LogWarning($"不一致: (Job)NativeArray={this.nativeArrayJobSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.nativeListJobSum != this.nativeArraySum )
            {
                Debug.LogWarning($"不一致: (Job)NativeList={this.nativeListJobSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.unsafeListJobSum != this.nativeArraySum )
            {
                Debug.LogWarning($"不一致: (Job)UnsafeList={this.unsafeListJobSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.unsafePtrListJobSum != 0 && this.unsafePtrListJobSum != this.nativeArraySum )
            {
                Debug.LogWarning($"不一致: (Job)UnsafePtrList={this.unsafePtrListJobSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.parallelHashMapJobSum != this.nativeArraySum )
            {
                Debug.LogWarning($"不一致: (Job)ParallelHashMap={this.parallelHashMapJobSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.nativeHashMapJobSum != this.nativeArraySum )
            {
                Debug.LogWarning($"不一致: (Job)NativeHashMap={this.nativeHashMapJobSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.unsafeHashMapJobSum != this.nativeArraySum )
            {
                Debug.LogWarning($"不一致: (Job)UnsafeHashMap={this.unsafeHashMapJobSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.regularArraySum != this.nativeArraySum )
            {
                Debug.LogWarning($"不一致: RegularArray={this.regularArraySum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.spanSum != this.nativeArraySum )
            {
                Debug.LogWarning($"不一致: Span={this.spanSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.nativeArraySum != expectedSum )
            {
                Debug.LogWarning($"不一致: NativeArray={this.nativeArraySum}, 理論値={expectedSum}");
            }
            else
            {
                Debug.Log($"一致: NativeArrayの合計値 {this.nativeArraySum} は理論値と一致しています");
            }
        }

        // ネイティブコンテナの解放
        if ( this.nativeList.IsCreated )
        {
            this.nativeList.Dispose();
        }

        if ( this.unsafeList.IsCreated )
        {
            this.unsafeList.Dispose();
        }

        if ( this.parallelHashMap.IsCreated )
        {
            this.parallelHashMap.Dispose();
        }

        if ( this.nativeHashMap.IsCreated )
        {
            this.nativeHashMap.Dispose();
        }

        if ( this.unsafeHashMap.IsCreated )
        {
            this.unsafeHashMap.Dispose();
        }

        if ( this.nativeArray.IsCreated )
        {
            this.nativeArray.Dispose();
        }

        Debug.Log("All resources disposed");
    }

    #region 連続アクセステスト

    [Test, Performance]
    public unsafe void SequentialAccess_UnsafePtrList()
    {
        Measure.Method(() =>
        {
            long sum = 0;
            for ( int i = 0; i < this.unsafePtrList.Length; i++ )
            {
                // ポインタから値を取得して合計
                sum += *this.unsafePtrList[i];
            }

            this.unsafePtrListSum = sum;
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
            this.regularArray[i] = i;
        }

        Measure.Method(() =>
        {
            long sum = 0;
            // より多くの繰り返し (例: 10回) で計測値を向上
            for ( int i = 0; i < this.regularArray.Length; i++ )
            {
                sum += this.regularArray[i];
            }

            this.regularArraySum = sum; // 平均値を保存
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
            this.regularArray[i] = i;
        }

        Measure.Method(() =>
        {
            Span<int> span = this.regularArray.AsSpan();
            long sum = 0;
            // より多くの繰り返し (例: 10回) で計測値を向上
            for ( int i = 0; i < span.Length; i++ )
            {
                sum += span[i];
            }

            this.spanSum = sum; // 平均値を保存
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
        this.dictionary.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            this.dictionary[i] = i;
        }

        Measure.Method(() =>
        {
            long sum = 0;
            for ( int i = 0; i < this.dictionary.Count; i++ )
            {
                sum += this.dictionary[i];
            }

            this.dictionarySum = sum;
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void SequentialAccess_List()
    {
        this.list.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            this.list.Add(i);
        }

        Measure.Method(() =>
        {
            long sum = 0;
            for ( int i = 0; i < this.list.Count; i++ )
            {
                sum += this.list[i];
            }

            this.listSum = sum;
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void SequentialAccess_NativeList()
    {
        this.nativeList.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            this.nativeList.Add(i);
        }

        Measure.Method(() =>
        {
            long sum = 0;
            for ( int i = 0; i < this.nativeList.Length; i++ )
            {
                sum += this.nativeList[i];
            }

            this.nativeListSum = sum;
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void SequentialAccess_UnsafeList()
    {
        this.unsafeList.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            this.unsafeList.Add(i);
        }

        Measure.Method(() =>
        {
            long sum = 0;
            for ( int i = 0; i < this.unsafeList.Length; i++ )
            {
                sum += this.unsafeList[i];
            }
            // 記録用フィールド変数に記録。
            // コンパイラの過剰最適化予防を兼ねて値を使用し、最後に合計値の精度チェックに使用。
            this.unsafeListSum = sum;
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void SequentialAccess_ParallelHashMap()
    {
        this.parallelHashMap.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            this.parallelHashMap.Add(i, i);
        }

        Measure.Method(() =>
        {
            long sum = 0;
            for ( int i = 0; i < ElementCount; i++ )
            {
                if ( this.parallelHashMap.TryGetValue(i, out int value) )
                {
                    sum += value;
                }
            }

            this.parallelHashMapSum = sum;
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void SequentialAccess_NativeHashMap()
    {
        this.nativeHashMap.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            this.nativeHashMap.Add(i, i);
        }

        Measure.Method(() =>
        {
            long sum = 0;
            for ( int i = 0; i < this.nativeHashMap.Count; i++ )
            {
                if ( this.nativeHashMap.TryGetValue(i, out int value) )
                {
                    sum += value;
                }
            }

            this.nativeHashMapSum = sum;
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void SequentialAccess_UnsafeHashMap()
    {
        this.unsafeHashMap.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            this.unsafeHashMap.Add(i, i);
        }

        Measure.Method(() =>
        {
            long sum = 0;
            for ( int i = 0; i < this.unsafeHashMap.Count; i++ )
            {
                if ( this.unsafeHashMap.TryGetValue(i, out int value) )
                {
                    sum += value;
                }
            }

            this.unsafeHashMapSum = sum;
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
            this.nativeArray[i] = i;
        }

        Measure.Method(() =>
        {
            long sum = 0;
            for ( int i = 0; i < this.nativeArray.Length; i++ )
            {
                sum += this.nativeArray[i];
            }

            this.nativeArraySum = sum;
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
                Input = this.unsafePtrList,
                Result = results
            };

            job.Schedule().Complete();
            this.unsafePtrListJobSum = results[0];
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
            this.nativeArray[i] = i;
        }

        Measure.Method(() =>
        {
            var results = new NativeArray<long>(1, Allocator.TempJob);

            var job = new NativeArraySumJob
            {
                Input = this.nativeArray,
                Result = results
            };

            job.Schedule().Complete();
            this.nativeArrayJobSum = job.Result[0];
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void JobSystem_NativeList()
    {
        this.nativeList.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            this.nativeList.Add(i);
        }

        Measure.Method(() =>
        {
            var results = new NativeArray<long>(1, Allocator.TempJob);

            var job = new NativeListSumJob
            {
                Input = this.nativeList,
                Result = results
            };

            job.Schedule().Complete();
            this.nativeListJobSum = job.Result[0];
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void JobSystem_UnsafeList()
    {
        this.unsafeList.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            this.unsafeList.Add(i);
        }

        Measure.Method(() =>
        {
            var results = new NativeArray<long>(1, Allocator.TempJob);

            var job = new UnsafeListSumJob
            {
                Input = this.unsafeList,
                Result = results
            };

            // ジョブを実行
            job.Schedule().Complete();

            // 精度チェックのために合計値を取得
            this.unsafeListJobSum = job.Result[0];
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void JobSystem_ParallelHashMap()
    {
        this.parallelHashMap.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            this.parallelHashMap.Add(i, i);
        }

        Measure.Method(() =>
        {
            var results = new NativeArray<long>(1, Allocator.TempJob);

            var job = new ParallelHashMapSumJob
            {
                Input = this.parallelHashMap,
                Keys = new NativeArray<int>(ElementCount, Allocator.TempJob),
                Result = results
            };

            // キーを事前に準備
            for ( int i = 0; i < job.Keys.Length; i++ )
            {
                job.Keys[i] = i;
            }

            job.Schedule().Complete();
            this.parallelHashMapJobSum = job.Result[0];
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
        this.nativeHashMap.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            this.nativeHashMap.Add(i, i);
        }

        Measure.Method(() =>
        {
            var results = new NativeArray<long>(1, Allocator.TempJob);

            var job = new NativeHashMapSumJob
            {
                Input = this.nativeHashMap,
                Keys = new NativeArray<int>(ElementCount, Allocator.TempJob),
                Result = results
            };

            // キーを事前に準備
            for ( int i = 0; i < job.Keys.Length; i++ )
            {
                job.Keys[i] = i;
            }

            job.Schedule().Complete();
            this.nativeHashMapJobSum = job.Result[0];
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
        this.unsafeHashMap.Clear();
        for ( int i = 0; i < ElementCount; i++ )
        {
            this.unsafeHashMap.Add(i, i);
        }

        Measure.Method(() =>
        {
            var results = new NativeArray<long>(1, Allocator.TempJob);

            var job = new UnsafeHashMapSumJob
            {
                Input = this.unsafeHashMap,
                Keys = new NativeArray<int>(ElementCount, Allocator.TempJob),
                Result = results
            };

            // キーを事前に準備
            for ( int i = 0; i < job.Keys.Length; i++ )
            {
                job.Keys[i] = i;
            }

            job.Schedule().Complete();
            this.unsafeHashMapJobSum = job.Result[0];
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
            for ( int i = 0; i < this.Input.Length; i++ )
            {
                // ポインタから値を読み取って合計
                sum += *this.Input[i];

            }

            this.Result[0] = sum;
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
            for ( int i = 0; i < this.Input.Length; i++ )
            {
                sum += this.Input[i];
            }

            this.Result[0] = sum;
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
            for ( int i = 0; i < this.Input.Length; i++ )
            {
                sum += this.Input[i];
            }

            this.Result[0] = sum;
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
            for ( int i = 0; i < this.Input.Length; i++ )
            {
                sum += this.Input[i];
            }

            this.Result[0] = sum;
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
            for ( int i = 0; i < this.Keys.Length; i++ )
            {
                if ( this.Input.TryGetValue(this.Keys[i], out int value) )
                {
                    sum += value;
                }
            }

            this.Result[0] = sum;
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
            for ( int i = 0; i < this.Keys.Length; i++ )
            {
                if ( this.Input.TryGetValue(this.Keys[i], out int value) )
                {
                    sum += value;
                }
            }

            this.Result[0] = sum;
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
            for ( int i = 0; i < this.Keys.Length; i++ )
            {
                if ( this.Input.TryGetValue(this.Keys[i], out int value) )
                {
                    sum += value;
                }
            }

            this.Result[0] = sum;
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
            for ( int i = 0; i < this.Input.Length; i++ )
            {
                this.Result[0] += this.Input[i];
            }
        }
    }

    #endregion
}