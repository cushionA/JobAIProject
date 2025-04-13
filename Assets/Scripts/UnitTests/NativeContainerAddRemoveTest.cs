using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.PerformanceTesting;
using System.Linq;

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
        dictionary = new Dictionary<int, int>(ElementCount);
        list = new List<int>(ElementCount);

        // ネイティブコンテナの初期化
        nativeList = new NativeList<int>(ElementCount, Allocator.Persistent);
        unsafeList = new UnsafeList<int>(ElementCount, Allocator.Persistent);
        parallelHashMap = new NativeParallelHashMap<int, int>(ElementCount, Allocator.Persistent);
        nativeHashMap = new NativeHashMap<int, int>(ElementCount, Allocator.Persistent);
        unsafeHashMap = new UnsafeHashMap<int, int>(ElementCount, Allocator.Persistent);

        // UnsafePtrList用の初期化
        unsafePtrList = new UnsafePtrList<int>(ElementCount, Allocator.Persistent);
        ptrHolders = new NativeArray<IntPtr>(ElementCount, Allocator.Persistent);

        Debug.Log($"AddRemoveTests: Setup complete with {ElementCount} elements capacity");
    }

    [OneTimeTearDown]
    public unsafe void TearDown()
    {
        // UnsafePtrListで確保したメモリの解放
        for ( int i = 0; i < ptrHolders.Length; i++ )
        {
            if ( ptrHolders[i] != IntPtr.Zero )
            {
                UnsafeUtility.Free((void*)ptrHolders[i], Allocator.Persistent);
                ptrHolders[i] = IntPtr.Zero;
            }
        }

        // コンテナの解放
        if ( ptrHolders.IsCreated )
            ptrHolders.Dispose();

        if ( unsafePtrList.IsCreated )
            unsafePtrList.Dispose();

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
            dictionary.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                dictionary[i] = i;
            }

            // 全ての要素を削除
            for ( int i = 0; i < ElementCount; i++ )
            {
                dictionary.Remove(i);
            }

            // 確認（測定後）
            if ( dictionary.Count != 0 )
            {
                Debug.LogError($"Dictionary still has {dictionary.Count} elements after removal");
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
            list.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                list.Add(i);
            }

            // リストが空になるまで最後の要素を削除し続ける
            while ( list.Count > 0 )
            {
                list.RemoveAt(list.Count - 1);
            }

            // 確認（測定後）
            if ( list.Count != 0 )
            {
                Debug.LogError($"List still has {list.Count} elements after removal");
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
            nativeList.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                nativeList.Add(i);
            }

            // リストが空になるまで最後の要素を削除し続ける
            while ( nativeList.Length > 0 )
            {
                nativeList.RemoveAt(nativeList.Length - 1);
            }

            // 確認（測定後）
            if ( nativeList.Length != 0 )
            {
                Debug.LogError($"NativeList still has {nativeList.Length} elements after removal");
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
            unsafeList.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                unsafeList.Add(i);
            }

            // リストが空になるまで最後の要素を削除し続ける
            while ( unsafeList.Length > 0 )
            {
                unsafeList.RemoveAt(unsafeList.Length - 1);
            }

            // 確認（測定後）
            if ( unsafeList.Length != 0 )
            {
                Debug.LogError($"UnsafeList still has {unsafeList.Length} elements after removal");
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
            unsafePtrList.Clear();

            // 既存のポインタをすべて解放
            for ( int i = 0; i < ptrHolders.Length; i++ )
            {
                if ( ptrHolders[i] != IntPtr.Zero )
                {
                    UnsafeUtility.Free((void*)ptrHolders[i], Allocator.Persistent);
                    ptrHolders[i] = IntPtr.Zero;
                }
            }

            // 新しいポインタを生成して追加
            for ( int i = 0; i < ElementCount; i++ )
            {
                IntPtr ptr = (IntPtr)UnsafeUtility.Malloc(sizeof(int), UnsafeUtility.AlignOf<int>(), Allocator.Persistent);
                *(int*)ptr = i; // 値の設定
                ptrHolders[i] = ptr; // 後で解放するために保存
                unsafePtrList.Add((int*)ptr);
            }

            // リストが空になるまで最後の要素を削除し続ける（メモリ解放も行う）
            while ( unsafePtrList.Length > 0 )
            {
                int index = unsafePtrList.Length - 1;
                int* ptr = unsafePtrList[index];
                unsafePtrList.RemoveAt(index);

                // 対応するptrHoldersのエントリを見つけ、メモリを解放
                for ( int i = 0; i < ptrHolders.Length; i++ )
                {
                    if ( ptrHolders[i] == (IntPtr)ptr )
                    {
                        UnsafeUtility.Free(ptr, Allocator.Persistent);
                        ptrHolders[i] = IntPtr.Zero;
                        break;
                    }
                }
            }

            // 確認（測定後）
            if ( unsafePtrList.Length != 0 )
            {
                Debug.LogError($"UnsafePtrList still has {unsafePtrList.Length} elements after removal");
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
            parallelHashMap.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                parallelHashMap.Add(i, i);
            }

            // 全ての要素を削除
            for ( int i = 0; i < ElementCount; i++ )
            {
                parallelHashMap.Remove(i);
            }

            // 確認（測定後）
            if ( parallelHashMap.Count() != 0 )
            {
                Debug.LogError($"ParallelHashMap still has {parallelHashMap.Count()} elements after removal");
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
            nativeHashMap.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                nativeHashMap.Add(i, i);
            }

            // 全ての要素を削除
            for ( int i = 0; i < ElementCount; i++ )
            {
                nativeHashMap.Remove(i);
            }

            // 確認（測定後）
            if ( nativeHashMap.Count != 0 )
            {
                Debug.LogError($"NativeHashMap still has {nativeHashMap.Count} elements after removal");
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
            unsafeHashMap.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                unsafeHashMap.Add(i, i);
            }

            // 全ての要素を削除
            for ( int i = 0; i < ElementCount; i++ )
            {
                unsafeHashMap.Remove(i);
            }

            // 確認（測定後）
            if ( unsafeHashMap.Count != 0 )
            {
                Debug.LogError($"UnsafeHashMap still has {unsafeHashMap.Count} elements after removal");
            }
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    #endregion
}