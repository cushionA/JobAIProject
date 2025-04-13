using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using static Unity.Collections.AllocatorManager;

/// <summary>
/// ゲームオブジェクトのインスタンスIDをキーとし、高効率に管理するデータ辞書
/// 素数サイズのバケットと単純モジュロ法でハッシュ衝突を最小化
/// UnsafeListを使用してGC負荷を削減
/// ステージの変わり目とかでResizeして、大きくなりすぎてたら圧程度まで縮める？　それか未使用エントリが増え過ぎてたら
/// 使った後は絶対にDisposeする
/// </summary>
/// <typeparam name="T">格納するデータの型（unmanaged制約付き）</typeparam>
public class CharaDataDic<T> : IDisposable, IEnumerable where T : unmanaged
{

    //    インスタンスIDの特徴分析

    //基本的な特性:

    //サンプルの全IDは負の値(100%)
    //全IDが偶数(100%)
    //値の範囲は -382662 ~ -310670 (範囲: 71992)
    //下位の桁に特徴的なパターンがある（0, 2, 4, 6, 8の末尾が均等に分布）


    //分布の特徴:
    //最後の桁は0, 2, 4, 6, 8のみ（奇数が全く存在しない）
    //下位3桁には特定の値が均等に出現（各72回）
    //10000単位の区間でほぼ均等に分布している

    //最適なハッシュ関数の選択
    //テスト結果から、以下の順で各ハッシュ関数のパフォーマンスが高いことがわかりました：

    //素数バケットサイズ + 単純モジュロ法:
    //バケットサイズ8209の場合、衝突率8.79%で最も低い
    //均等な分布を実現（使用率100%）


    // エントリ構造体 - キーと値とチェーン情報を格納
    private struct Entry
    {
        /// <summary>
        /// ゲームオブジェクトのID（キー）
        /// </summary>
        public int InstanceId;

        /// <summary>
        /// 値配列内のインデックス
        /// </summary>
        public int ValueIndex;

        /// <summary>
        /// 同じバケット内の次のエントリへのインデックス
        /// </summary>
        public int NextInBucket;

        /// <summary>
        /// このエントリが使用中かどうか
        /// </summary>
        public bool IsOccupied;
    }

    // 内部データ構造（UnsafeListベース）
    private UnsafeList<int> _buckets;           // バケット配列（各要素はエントリへのインデックス、-1は空）
    private UnsafeList<Entry> _entries;         // エントリの配列
    private UnsafeList<T> _values;              // 実際のデータを格納する配列
    private UnsafeList<int> _indexForValue;     // 値インデックス→エントリインデックスの逆引き

    private int _count;                  // 使用中のエントリ数
    private int _freeListHead;           // 削除済みエントリの再利用リスト先頭
    private int _freeCount;              // 再利用可能なエントリ数

    private bool _isDisposed;            // 解放済みフラグ
    private readonly Allocator _allocator; // メモリアロケータ

    // 素数テーブル - よく使われるサイズに近い素数
    private static readonly int[] PrimeSizes = {
        17, 37, 79, 163, 331, 673, 1361, 2729, 5471, 10949, 21911, 43853,
        87719, 175447, 350899, 701819, 1403641, 2807303, 5614657, 11229331
    };

    // 定数
    private const int DEFAULT_CAPACITY = 1031;    // デフォルト容量（素数）
    private const float LOAD_FACTOR = 0.75f;      // 負荷係数（この値を超えるとリサイズ）

    /// <summary>
    /// 格納されている要素数
    /// </summary>
    public int Count => _count - _freeCount;

    /// <summary>
    /// バケットの容量
    /// </summary>
    public int Capacity => _entries.Length;

    /// <summary>
    /// インデクサ - ゲームオブジェクトからの値アクセス
    /// </summary>
    public T this[GameObject gameObject]
    {
        get
        {
            if ( TryGetValue(gameObject, out T value, out _) )
                return value;
            throw new KeyNotFoundException($"GameObject {gameObject.name} not found in store");
        }
    }

    /// <summary>
    /// インデクサ - インスタンスIDからの値アクセス
    /// </summary>
    public T this[int instanceId]
    {
        get
        {
            if ( TryGetValue(instanceId, out T value, out _) )
                return value;
            throw new KeyNotFoundException($"InstanceID {instanceId} not found in store");
        }
    }

    /// <summary>
    /// インデクサ - 値インデックスからの直接アクセス（参照を返す）
    /// </summary>
    public T this[int valueIndex, bool isValueIndex]
    {
        get
        {
            if ( !isValueIndex )
                throw new ArgumentException("Second parameter must be true when accessing by value index");

            if ( valueIndex < 0 || valueIndex >= _count || !IsValidIndex(valueIndex) )
                throw new ArgumentOutOfRangeException(nameof(valueIndex));

            return _values[valueIndex];
        }
    }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="capacity">初期容量（素数に調整されます）</param>
    /// <param name="allocator">メモリアロケータ（デフォルトはPersistent）</param>
    public CharaDataDic(int capacity = DEFAULT_CAPACITY, Allocator allocator = Allocator.Persistent)
    {
        // アロケータ保存
        _allocator = allocator;

        // 指定容量以上の最小の素数を選択
        int primeCapacity = GetNextPrimeSize(capacity);

        // UnsafeListの初期化
        _buckets = new UnsafeList<int>(primeCapacity, allocator);
        _entries = new UnsafeList<Entry>(primeCapacity, allocator);
        _values = new UnsafeList<T>(primeCapacity, allocator);
        _indexForValue = new UnsafeList<int>(primeCapacity, allocator);

        // バケットリストの容量確保と-1で初期化
        _buckets.Resize(primeCapacity, NativeArrayOptions.ClearMemory);
        for ( int i = 0; i < _buckets.Length; i++ )
        {
            _buckets[i] = -1;
        }

        // 他のリストの容量を確保
        _entries.Capacity = primeCapacity;
        _values.Capacity = primeCapacity;
        _indexForValue.Capacity = primeCapacity;

        _freeListHead = -1;
        _count = 0;
        _freeCount = 0;
    }

    /// <summary>
    /// ゲームオブジェクトとデータを追加または更新
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Add(GameObject obj, T data)
    {
        if ( obj == null )
            throw new ArgumentNullException(nameof(obj));

        return Add(obj.GetInstanceID(), data);
    }

    /// <summary>
    /// インスタンスIDとデータを追加または更新し、値のインデックスを返す
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Add(int instanceId, T data)
    {
        // 負荷係数チェック - 必要に応じてリサイズ
        if ( (_count - _freeCount) >= _entries.Length * LOAD_FACTOR )
        {
            Resize(_entries.Length * 2);
        }

        // バケットインデックスを計算（単純モジュロ法）
        int bucketIndex = GetBucketIndex(instanceId);

        // 同じキーが既に存在するか検索
        int entryIndex = _buckets[bucketIndex];
        while ( entryIndex != -1 )
        {
            ref Entry entry = ref _entries.ElementAt(entryIndex);
            if ( entry.InstanceId == instanceId )
            {
                // 既存エントリを更新
                _values[entry.ValueIndex] = data;
                return entry.ValueIndex;
            }
            entryIndex = entry.NextInBucket;
        }

        // 新しいエントリ用のインデックスを確保
        int newIndex;
        if ( _freeCount > 0 )
        {
            // 削除済みエントリを再利用
            newIndex = _freeListHead;
            _freeListHead = _entries.ElementAt(newIndex).NextInBucket;
            _freeCount--;
        }
        else
        {
            // 新しいスロットを使用
            if ( _count == _entries.Length )
            {
                Resize(_entries.Length * 2);
                bucketIndex = GetBucketIndex(instanceId);
            }
            newIndex = _count;
            _count++;
        }

        // エントリと値のリストが十分な大きさになるよう拡張
        EnsureCapacity(newIndex);

        // 新しいエントリの設定
        Entry newEntry;
        newEntry.InstanceId = instanceId;
        newEntry.ValueIndex = newIndex;
        newEntry.NextInBucket = _buckets[bucketIndex];
        newEntry.IsOccupied = true;

        // UnsafeListの更新
        if ( newIndex < _entries.Length )
        {
            _entries[newIndex] = newEntry;
        }
        else
        {
            _entries.Add(newEntry);
        }

        if ( newIndex < _values.Length )
        {
            _values[newIndex] = data;
        }
        else
        {
            _values.Add(data);
        }

        if ( newIndex < _indexForValue.Length )
        {
            _indexForValue[newIndex] = newIndex;
        }
        else
        {
            _indexForValue.Add(newIndex);
        }

        _buckets[bucketIndex] = newIndex;

        return newIndex;
    }

    /// <summary>
    /// 特定のインデックスに対応するリストの容量を確保
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int index)
    {
        // 必要に応じて各リストの容量を拡張
        if ( _entries.Capacity <= index )
        {
            int newCapacity = Math.Max(_entries.Capacity * 2, index + 1);
            _entries.Capacity = newCapacity;
        }

        if ( _values.Capacity <= index )
        {
            int newCapacity = Math.Max(_values.Capacity * 2, index + 1);
            _values.Capacity = newCapacity;
        }

        if ( _indexForValue.Capacity <= index )
        {
            int newCapacity = Math.Max(_indexForValue.Capacity * 2, index + 1);
            _indexForValue.Capacity = newCapacity;
        }
    }

    /// <summary>
    /// 指定したインデックスが有効（使用中）かどうかを確認
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValidIndex(int index)
    {
        return index >= 0 && index < _count && _entries[index].IsOccupied;
    }

    /// <summary>
    /// インデックスから直接データを取得（参照を返す）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetDataByIndex(int index)
    {
        if ( !IsValidIndex(index) )
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range or points to a removed entry");
        }

        return ref _values.ElementAt(index);
    }

    /// <summary>
    /// ゲームオブジェクトからデータと内部インデックスを取得
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(GameObject obj, out T data, out int index)
    {
        if ( obj == null )
        {
            data = default;
            index = -1;
            return false;
        }

        return TryGetValue(obj.GetInstanceID(), out data, out index);
    }

    /// <summary>
    /// インスタンスIDからデータと内部インデックスを取得
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(int instanceId, out T data, out int index)
    {
        int bucketIndex = GetBucketIndex(instanceId);
        int entryIndex = _buckets[bucketIndex];

        while ( entryIndex != -1 )
        {
            // バケットから取り出したインデックスのエントリを取得。
            ref Entry entry = ref _entries.ElementAt(entryIndex);

            // エントリに記録されたIDと同じなら同じアイテムだと判断する。
            if ( entry.InstanceId == instanceId )
            {
                data = _values[entry.ValueIndex];
                index = entry.ValueIndex;
                return true;
            }
            entryIndex = entry.NextInBucket;
        }

        data = default;
        index = -1;
        return false;
    }

    /// <summary>
    /// インスタンスIDに関連付けられたデータを削除
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(int instanceId)
    {
        if ( _count == 0 )
            return false;

        int bucketIndex = GetBucketIndex(instanceId);
        int entryIndex = _buckets[bucketIndex];
        int prevIndex = -1;

        while ( entryIndex != -1 )
        {
            ref Entry entry = ref _entries.ElementAt(entryIndex);

            if ( entry.InstanceId == instanceId )
            {
                // エントリをバケットリストから削除
                if ( prevIndex != -1 )
                {
                    // 前のエントリの次のリンクを更新
                    Entry prevEntry = _entries[prevIndex];
                    prevEntry.NextInBucket = entry.NextInBucket;
                    _entries[prevIndex] = prevEntry;
                }
                else
                {
                    _buckets[bucketIndex] = entry.NextInBucket;
                }

                // エントリを論理的に削除してフリーリストに追加
                Entry updatedEntry = entry;
                updatedEntry.IsOccupied = false;
                updatedEntry.NextInBucket = _freeListHead;
                _entries[entryIndex] = updatedEntry;

                _freeListHead = entryIndex;
                _freeCount++;

                return true;
            }

            prevIndex = entryIndex;
            entryIndex = entry.NextInBucket;
        }

        return false;
    }

    /// <summary>
    /// ゲームオブジェクトに関連付けられたデータを削除
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(GameObject obj)
    {
        return obj != null && Remove(obj.GetInstanceID());
    }

    /// <summary>
    /// すべてのエントリをクリア
    /// </summary>
    public void Clear()
    {
        if ( _count == 0 )
            return;

        // バケットを-1で初期化
        for ( int i = 0; i < _buckets.Length; i++ )
        {
            _buckets[i] = -1;
        }

        // UnsafeListをクリア
        _entries.Clear();
        _values.Clear();
        _indexForValue.Clear();

        _count = 0;
        _freeCount = 0;
        _freeListHead = -1;
    }

    /// <summary>
    /// 内部配列のサイズを変更
    /// </summary>
    private void Resize(int newCapacity)
    {
        // 素数サイズに調整
        int newPrimeSize = GetNextPrimeSize(newCapacity);

        // 各コレクションのサイズを拡張（既存データを保持）
        // サイズを変更し、新規確保メモリは初期化する。
        _entries.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);
        _values.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);
        _indexForValue.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);

        // バケットリストを破棄して新しいサイズで再作成
        // バケットリストを新しいサイズで作り直し、-1で初期化
        _buckets.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);

        for ( int i = 0; i < newPrimeSize; i++ )
        {
            _buckets[i] = -1;
        }

        // すべてのエントリを再ハッシュ
        for ( int i = 0; i < _count; i++ )
        {
            ref Entry entry = ref _entries.ElementAt(i);
            if ( entry.IsOccupied )
            {
                int bucket = Math.Abs(entry.InstanceId) % newPrimeSize;
                entry.NextInBucket = _buckets[bucket];
                _buckets[bucket] = i;
            }
        }
    }

    /// <summary>
    /// インスタンスIDからバケットインデックスを取得（単純モジュロ法）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetBucketIndex(int instanceId)
    {
        return ((instanceId >> 31) & instanceId ^ instanceId) % _buckets.Length;
    }

    /// <summary>
    /// 指定サイズ以上の最小の素数を取得
    /// </summary>
    private int GetNextPrimeSize(int minSize)
    {
        // サイズが十分小さければ素数テーブルから探す
        foreach ( int prime in PrimeSizes )
        {
            if ( prime >= minSize )
                return prime;
        }

        // テーブルに十分な大きさの素数がなければ計算
        return CalculateNextPrime(minSize);
    }

    /// <summary>
    /// 指定値以上の次の素数を計算
    /// </summary>
    private int CalculateNextPrime(int minSize)
    {
        // 奇数から開始（偶数は2以外素数にならない）
        int candidate = minSize;
        if ( candidate % 2 == 0 )
            candidate++;

        while ( !IsPrime(candidate) )
        {
            candidate += 2; // 奇数のみをチェック
        }

        return candidate;
    }

    /// <summary>
    /// 素数判定
    /// </summary>
    private bool IsPrime(int number)
    {
        if ( number <= 1 )
            return false;
        if ( number == 2 || number == 3 )
            return true;
        if ( number % 2 == 0 || number % 3 == 0 )
            return false;

        // 6k±1の形で表される数のみチェック（効率化）
        int limit = (int)Math.Sqrt(number);
        for ( int i = 5; i <= limit; i += 6 )
        {
            if ( number % i == 0 || number % (i + 2) == 0 )
                return false;
        }

        return true;
    }

    /// <summary>
    /// すべての有効なエントリに対して処理を実行
    /// </summary>
    public void ForEach(Action<int, T> action)
    {
        if ( action == null )
            throw new ArgumentNullException(nameof(action));

        for ( int i = 0; i < _count; i++ )
        {
            if ( i < _entries.Length && _entries[i].IsOccupied )
            {
                action(i, _values.ElementAt(i));
            }
        }
    }

    /// <summary>
    /// 有効なインデックスのみを列挙
    /// </summary>
    public System.Collections.Generic.IEnumerable<int> GetValidIndices()
    {
        for ( int i = 0; i < _count; i++ )
        {
            if ( i < _entries.Length && _entries[i].IsOccupied )
            {
                yield return i;
            }
        }
    }

    /// <summary>
    /// リソースを解放
    /// </summary>
    public void Dispose()
    {
        if ( _isDisposed )
            return;

        _buckets.Dispose();
        _entries.Dispose();
        _values.Dispose();
        _indexForValue.Dispose();
        _isDisposed = true;
    }

    public IEnumerator GetEnumerator()
    {
        throw new NotImplementedException();
    }
}