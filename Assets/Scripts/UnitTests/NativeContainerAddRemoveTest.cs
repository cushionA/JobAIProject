using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.PerformanceTesting;
using UnityEngine;

/// <summary>
/// 様々なネイティブコンテナとマネージドコレクションの追加・削除パフォーマンス比較テスト
/// </summary>
public class NativeContainerAddRemoveTest
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
    private unsafe UnsafePtrList<int> unsafePtrList; // UnsafePtrListを追加
    private unsafe NativeArray<IntPtr> ptrHolders; // ポインタ保持用

    // 測定回数設定
    private const int WarmupCount = 3;
    private const int MeasurementCount = 10;
    // 各測定で初期化も含むので1回に設定
    private const int IterationsPerMeasurement = 1;

    [OneTimeSetUp]
    public unsafe void Setup()
    {
        // 標準コレクションの初期化
        this.dictionary = new Dictionary<int, int>(ElementCount);
        this.list = new List<int>(ElementCount);

        // ネイティブコンテナの初期化
        this.nativeList = new NativeList<int>(ElementCount, Allocator.Persistent);
        this.unsafeList = new UnsafeList<int>(ElementCount, Allocator.Persistent);
        this.parallelHashMap = new NativeParallelHashMap<int, int>(ElementCount, Allocator.Persistent);
        this.nativeHashMap = new NativeHashMap<int, int>(ElementCount, Allocator.Persistent);
        this.unsafeHashMap = new UnsafeHashMap<int, int>(ElementCount, Allocator.Persistent);

        // UnsafePtrList用の初期化
        this.unsafePtrList = new UnsafePtrList<int>(ElementCount, Allocator.Persistent);
        this.ptrHolders = new NativeArray<IntPtr>(ElementCount, Allocator.Persistent);

        Debug.Log($"AddRemoveTests: Setup complete with {ElementCount} elements capacity");
    }

    [OneTimeTearDown]
    public unsafe void TearDown()
    {
        // UnsafePtrListで確保したメモリの解放
        for ( int i = 0; i < this.ptrHolders.Length; i++ )
        {
            if ( this.ptrHolders[i] != IntPtr.Zero )
            {
                UnsafeUtility.Free((void*)this.ptrHolders[i], Allocator.Persistent);
                this.ptrHolders[i] = IntPtr.Zero;
            }
        }

        // コンテナの解放
        if ( this.ptrHolders.IsCreated )
        {
            this.ptrHolders.Dispose();
        }

        if ( this.unsafePtrList.IsCreated )
        {
            this.unsafePtrList.Dispose();
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

        Debug.Log("AddRemoveTests: All resources disposed");
    }

    #region 値の追加テスト

    //[Test, Performance]
    //public void AddValues_Dictionary()
    //{
    //    Measure.Method(() =>
    //    {
    //        // 各測定の最初にクリア
    //        dictionary.Clear();

    //        // 要素を追加
    //        for ( int i = 0; i < ElementCount; i++ )
    //        {
    //            dictionary[i] = i;
    //        }

    //        // 確認（測定後）
    //        if ( dictionary.Count != ElementCount )
    //        {
    //            Debug.LogError($"Dictionary count mismatch: {dictionary.Count} != {ElementCount}");
    //        }
    //    })
    //    .WarmupCount(WarmupCount)
    //    .MeasurementCount(MeasurementCount)
    //    .IterationsPerMeasurement(IterationsPerMeasurement)
    //    .Run();
    //}

    //[Test, Performance]
    //public void AddValues_List()
    //{
    //    Measure.Method(() =>
    //    {
    //        // 各測定の最初にクリア
    //        list.Clear();

    //        // 要素を追加
    //        for ( int i = 0; i < ElementCount; i++ )
    //        {
    //            list.Add(i);
    //        }

    //        // 確認（測定後）
    //        if ( list.Count != ElementCount )
    //        {
    //            Debug.LogError($"List count mismatch: {list.Count} != {ElementCount}");
    //        }
    //    })
    //    .WarmupCount(WarmupCount)
    //    .MeasurementCount(MeasurementCount)
    //    .IterationsPerMeasurement(IterationsPerMeasurement)
    //    .Run();
    //}

    //[Test, Performance]
    //public void AddValues_NativeList()
    //{
    //    Measure.Method(() =>
    //    {
    //        // 各測定の最初にクリア
    //        nativeList.Clear();

    //        // 要素を追加
    //        for ( int i = 0; i < ElementCount; i++ )
    //        {
    //            nativeList.Add(i);
    //        }

    //        // 確認（測定後）
    //        if ( nativeList.Length != ElementCount )
    //        {
    //            Debug.LogError($"NativeList length mismatch: {nativeList.Length} != {ElementCount}");
    //        }
    //    })
    //    .WarmupCount(WarmupCount)
    //    .MeasurementCount(MeasurementCount)
    //    .IterationsPerMeasurement(IterationsPerMeasurement)
    //    .Run();
    //}

    //[Test, Performance]
    //public void AddValues_UnsafeList()
    //{
    //    Measure.Method(() =>
    //    {
    //        // 各測定の最初にクリア
    //        unsafeList.Clear();

    //        // 要素を追加
    //        for ( int i = 0; i < ElementCount; i++ )
    //        {
    //            unsafeList.Add(i);
    //        }

    //        // 確認（測定後）
    //        if ( unsafeList.Length != ElementCount )
    //        {
    //            Debug.LogError($"UnsafeList length mismatch: {unsafeList.Length} != {ElementCount}");
    //        }
    //    })
    //    .WarmupCount(WarmupCount)
    //    .MeasurementCount(MeasurementCount)
    //    .IterationsPerMeasurement(IterationsPerMeasurement)
    //    .Run();
    //}

    //[Test, Performance]
    //public unsafe void AddValues_UnsafePtrList()
    //{
    //    Measure.Method(() =>
    //    {
    //        // 各測定の最初にクリア
    //        unsafePtrList.Clear();
    //        
    //        // 既存のポインタをすべて解放
    //        for (int i = 0; i < ptrHolders.Length; i++)
    //        {
    //            if (ptrHolders[i] != IntPtr.Zero)
    //            {
    //                UnsafeUtility.Free((void*)ptrHolders[i], Allocator.Persistent);
    //                ptrHolders[i] = IntPtr.Zero;
    //            }
    //        }

    //        // 新しいポインタを生成して追加
    //        for (int i = 0; i < ElementCount; i++)
    //        {
    //            IntPtr ptr = (IntPtr)UnsafeUtility.Malloc(sizeof(int), UnsafeUtility.AlignOf<int>(), Allocator.Persistent);
    //            *(int*)ptr = i; // 値の設定
    //            ptrHolders[i] = ptr; // 後で解放するために保存
    //            unsafePtrList.Add((int*)ptr);
    //        }

    //        // 確認（測定後）
    //        if (unsafePtrList.Length != ElementCount)
    //        {
    //            Debug.LogError($"UnsafePtrList length mismatch: {unsafePtrList.Length} != {ElementCount}");
    //        }
    //    })
    //    .WarmupCount(WarmupCount)
    //    .MeasurementCount(MeasurementCount)
    //    .IterationsPerMeasurement(IterationsPerMeasurement)
    //    .Run();
    //}

    //[Test, Performance]
    //public void AddValues_ParallelHashMap()
    //{
    //    Measure.Method(() =>
    //    {
    //        // 各測定の最初にクリア
    //        parallelHashMap.Clear();

    //        // 要素を追加
    //        for ( int i = 0; i < ElementCount; i++ )
    //        {
    //            parallelHashMap.Add(i, i);
    //        }

    //        // 確認（測定後）
    //        if ( parallelHashMap.Count() != ElementCount )
    //        {
    //            Debug.LogError($"ParallelHashMap count mismatch: {parallelHashMap.Count()} != {ElementCount}");
    //        }
    //    })
    //    .WarmupCount(WarmupCount)
    //    .MeasurementCount(MeasurementCount)
    //    .IterationsPerMeasurement(IterationsPerMeasurement)
    //    .Run();
    //}

    //[Test, Performance]
    //public void AddValues_NativeHashMap()
    //{
    //    Measure.Method(() =>
    //    {
    //        // 各測定の最初にクリア
    //        nativeHashMap.Clear();

    //        // 要素を追加
    //        for ( int i = 0; i < ElementCount; i++ )
    //        {
    //            nativeHashMap.Add(i, i);
    //        }

    //        // 確認（測定後）
    //        if ( nativeHashMap.Count != ElementCount )
    //        {
    //            Debug.LogError($"NativeHashMap count mismatch: {nativeHashMap.Count} != {ElementCount}");
    //        }
    //    })
    //    .WarmupCount(WarmupCount)
    //    .MeasurementCount(MeasurementCount)
    //    .IterationsPerMeasurement(IterationsPerMeasurement)
    //    .Run();
    //}

    //[Test, Performance]
    //public void AddValues_UnsafeHashMap()
    //{
    //    Measure.Method(() =>
    //    {
    //        // 各測定の最初にクリア
    //        unsafeHashMap.Clear();

    //        // 要素を追加
    //        for ( int i = 0; i < ElementCount; i++ )
    //        {
    //            unsafeHashMap.Add(i, i);
    //        }

    //        // 確認（測定後）
    //        if ( unsafeHashMap.Count != ElementCount )
    //        {
    //            Debug.LogError($"UnsafeHashMap count mismatch: {unsafeHashMap.Count} != {ElementCount}");
    //        }
    //    })
    //    .WarmupCount(WarmupCount)
    //    .MeasurementCount(MeasurementCount)
    //    .IterationsPerMeasurement(IterationsPerMeasurement)
    //    .Run();
    //}

    #endregion

    #region 値の削除テスト

    [Test, Performance]
    public void RemoveValues_Dictionary()
    {
        Measure.Method(() =>
        {
            // 測定前にデータを準備
            this.dictionary.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                this.dictionary[i] = i;
            }

            // 全ての要素を削除
            for ( int i = 0; i < ElementCount; i++ )
            {
                _ = this.dictionary.Remove(i);
            }

            // 確認（測定後）
            if ( this.dictionary.Count != 0 )
            {
                Debug.LogError($"Dictionary still has {this.dictionary.Count} elements after removal");
            }
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void RemoveValues_List()
    {
        Measure.Method(() =>
        {
            // テスト用にデータを準備
            this.list.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                this.list.Add(i);
            }

            // リストが空になるまで最後の要素を削除し続ける
            while ( this.list.Count > 0 )
            {
                this.list.RemoveAt(this.list.Count - 1);
            }

            // 確認（測定後）
            if ( this.list.Count != 0 )
            {
                Debug.LogError($"List still has {this.list.Count} elements after removal");
            }
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void RemoveValues_NativeList()
    {
        Measure.Method(() =>
        {
            // テスト用にデータを準備
            this.nativeList.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                this.nativeList.Add(i);
            }

            // リストが空になるまで最後の要素を削除し続ける
            while ( this.nativeList.Length > 0 )
            {
                this.nativeList.RemoveAt(this.nativeList.Length - 1);
            }

            // 確認（測定後）
            if ( this.nativeList.Length != 0 )
            {
                Debug.LogError($"NativeList still has {this.nativeList.Length} elements after removal");
            }
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void RemoveValues_UnsafeList()
    {
        Measure.Method(() =>
        {
            // テスト用にデータを準備
            this.unsafeList.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                this.unsafeList.Add(i);
            }

            // リストが空になるまで最後の要素を削除し続ける
            while ( this.unsafeList.Length > 0 )
            {
                this.unsafeList.RemoveAt(this.unsafeList.Length - 1);
            }

            // 確認（測定後）
            if ( this.unsafeList.Length != 0 )
            {
                Debug.LogError($"UnsafeList still has {this.unsafeList.Length} elements after removal");
            }
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public unsafe void RemoveValues_UnsafePtrList()
    {
        Measure.Method(() =>
        {
            // テスト用にデータを準備 - 各測定につき新たにポインタを作成
            this.unsafePtrList.Clear();

            // 既存のポインタをすべて解放
            for ( int i = 0; i < this.ptrHolders.Length; i++ )
            {
                if ( this.ptrHolders[i] != IntPtr.Zero )
                {
                    UnsafeUtility.Free((void*)this.ptrHolders[i], Allocator.Persistent);
                    this.ptrHolders[i] = IntPtr.Zero;
                }
            }

            // 新しいポインタを生成して追加
            for ( int i = 0; i < ElementCount; i++ )
            {
                IntPtr ptr = (IntPtr)UnsafeUtility.Malloc(sizeof(int), UnsafeUtility.AlignOf<int>(), Allocator.Persistent);
                *(int*)ptr = i; // 値の設定
                this.ptrHolders[i] = ptr; // 後で解放するために保存
                this.unsafePtrList.Add((int*)ptr);
            }

            // リストが空になるまで最後の要素を削除し続ける（メモリ解放も行う）
            while ( this.unsafePtrList.Length > 0 )
            {
                int index = this.unsafePtrList.Length - 1;
                int* ptr = this.unsafePtrList[index];
                this.unsafePtrList.RemoveAt(index);

                // 対応するptrHoldersのエントリを見つけ、メモリを解放
                for ( int i = 0; i < this.ptrHolders.Length; i++ )
                {
                    if ( this.ptrHolders[i] == (IntPtr)ptr )
                    {
                        UnsafeUtility.Free(ptr, Allocator.Persistent);
                        this.ptrHolders[i] = IntPtr.Zero;
                        break;
                    }
                }
            }

            // 確認（測定後）
            if ( this.unsafePtrList.Length != 0 )
            {
                Debug.LogError($"UnsafePtrList still has {this.unsafePtrList.Length} elements after removal");
            }
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void RemoveValues_ParallelHashMap()
    {
        Measure.Method(() =>
        {
            // 測定前にデータを準備
            this.parallelHashMap.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                this.parallelHashMap.Add(i, i);
            }

            // 全ての要素を削除
            for ( int i = 0; i < ElementCount; i++ )
            {
                _ = this.parallelHashMap.Remove(i);
            }

            // 確認（測定後）
            if ( this.parallelHashMap.Count() != 0 )
            {
                Debug.LogError($"ParallelHashMap still has {this.parallelHashMap.Count()} elements after removal");
            }
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void RemoveValues_NativeHashMap()
    {
        Measure.Method(() =>
        {
            // 測定前にデータを準備
            this.nativeHashMap.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                this.nativeHashMap.Add(i, i);
            }

            // 全ての要素を削除
            for ( int i = 0; i < ElementCount; i++ )
            {
                _ = this.nativeHashMap.Remove(i);
            }

            // 確認（測定後）
            if ( this.nativeHashMap.Count != 0 )
            {
                Debug.LogError($"NativeHashMap still has {this.nativeHashMap.Count} elements after removal");
            }
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void RemoveValues_UnsafeHashMap()
    {
        Measure.Method(() =>
        {
            // 測定前にデータを準備
            this.unsafeHashMap.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                this.unsafeHashMap.Add(i, i);
            }

            // 全ての要素を削除
            for ( int i = 0; i < ElementCount; i++ )
            {
                _ = this.unsafeHashMap.Remove(i);
            }

            // 確認（測定後）
            if ( this.unsafeHashMap.Count != 0 )
            {
                Debug.LogError($"UnsafeHashMap still has {this.unsafeHashMap.Count} elements after removal");
            }
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    #endregion
}