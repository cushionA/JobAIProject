using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using static Unity.Collections.AllocatorManager;

/// <summary>
/// ゲームオブジェクトのGetHashCode()とGetInstanceID()をハイブリッド方式で使用するデータ辞書
/// GetHashCode()で高速検索し、GetInstanceID()で一意性を確保
/// UnsafeListを使用してGC負荷を削減
/// </summary>
/// <typeparam name="T">格納するデータの型（unmanaged制約付き）</typeparam>
public class CharaDataDic<T> : IDisposable, IEnumerable where T : unmanaged
{
    // エントリ構造体 - キーと値とチェーン情報を格納
    private struct Entry
    {
        /// <summary>
        /// ゲームオブジェクトのハッシュコード（主キー）
        /// </summary>
        public int HashCode;

        /// <summary>
        /// ゲームオブジェクトのインスタンスID（衝突解決用補助キー）
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
            if ( gameObject == null )
                throw new ArgumentNullException(nameof(gameObject));

            if ( TryGetValue(gameObject, out T value, out _) )
                return value;
            throw new KeyNotFoundException($"GameObject {gameObject.name} not found in store");
        }
        set => Add(gameObject, value);
    }

    /// <summary>
    /// インデクサ - インスタンスIDからの値アクセス
    /// </summary>
    public T this[int instanceId]
    {
        get
        {
            if ( TryGetValueByInstanceId(instanceId, out T value, out _) )
                return value;
            throw new KeyNotFoundException($"InstanceID {instanceId} not found in store");
        }
    }

    /// <summary>
    /// インデクサ - 値インデックスからの直接アクセス
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

        // ハイブリッドキーを使用
        return AddInternal(obj.GetHashCode(), obj.GetInstanceID(), data);
    }

    /// <summary>
    /// ハッシュコードとインスタンスIDを使ってデータを追加または更新
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddInternal(int hashCode, int instanceId, T data)
    {
        // 負荷係数チェック - 必要に応じてリサイズ
        if ( (_count - _freeCount) >= _entries.Length * LOAD_FACTOR )
        {
            Resize(_entries.Length * 2);
        }

        // バケットインデックスを計算（単純モジュロ法）
        int bucketIndex = GetBucketIndex(hashCode);

        // 同じキーが既に存在するか検索
        int entryIndex = _buckets[bucketIndex];
        while ( entryIndex != -1 )
        {
            ref Entry entry = ref _entries.ElementAt(entryIndex);
            // ハッシュコードが一致してもインスタンスIDも確認
            if ( entry.HashCode == hashCode && entry.InstanceId == instanceId )
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
                bucketIndex = GetBucketIndex(hashCode);
            }
            newIndex = _count;
            _count++;
        }

        // エントリと値のリストが十分な大きさになるよう拡張
        EnsureCapacity(newIndex);

        // 新しいエントリの設定
        Entry newEntry;
        newEntry.HashCode = hashCode;
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

        // ハイブリッドキーを使用
        return TryGetValueByBoth(obj.GetHashCode(), obj.GetInstanceID(), out data, out index);
    }

    /// <summary>
    /// インスタンスIDからデータと内部インデックスを取得（完全走査なので注意）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValueByInstanceId(int instanceId, out T data, out int index)
    {
        // インスタンスIDによる線形探索
        for ( int i = 0; i < _count; i++ )
        {
            if ( _entries[i].IsOccupied && _entries[i].InstanceId == instanceId )
            {
                data = _values[_entries[i].ValueIndex];
                index = _entries[i].ValueIndex;
                return true;
            }
        }

        data = default;
        index = -1;
        return false;
    }

    /// <summary>
    /// ハッシュコードとインスタンスIDの両方を使ってデータと内部インデックスを取得
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetValueByBoth(int hashCode, int instanceId, out T data, out int index)
    {
        int bucketIndex = GetBucketIndex(hashCode);
        int entryIndex = _buckets[bucketIndex];

        while ( entryIndex != -1 )
        {
            ref Entry entry = ref _entries.ElementAt(entryIndex);
            // ハッシュコードとインスタンスIDの両方をチェック
            if ( entry.HashCode == hashCode && entry.InstanceId == instanceId )
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
    /// ゲームオブジェクトに関連付けられたデータを削除
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(GameObject obj)
    {
        if ( obj == null )
            return false;

        // ハイブリッドキーを使用
        return RemoveByBoth(obj.GetHashCode(), obj.GetInstanceID());
    }

    /// <summary>
    /// インスタンスIDに関連付けられたデータを削除
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool RemoveByInstanceId(int instanceId)
    {
        // まずインスタンスIDでエントリを見つける
        for ( int i = 0; i < _count; i++ )
        {
            if ( _entries[i].IsOccupied && _entries[i].InstanceId == instanceId )
            {
                return RemoveByBoth(_entries[i].HashCode, instanceId);
            }
        }
        return false;
    }

    /// <summary>
    /// ハッシュコードとインスタンスIDの両方を使ってデータを削除
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool RemoveByBoth(int hashCode, int instanceId)
    {
        if ( _count == 0 )
            return false;

        int bucketIndex = GetBucketIndex(hashCode);
        int entryIndex = _buckets[bucketIndex];
        int prevIndex = -1;

        while ( entryIndex != -1 )
        {
            ref Entry entry = ref _entries.ElementAt(entryIndex);

            // ハッシュコードとインスタンスIDの両方をチェック
            if ( entry.HashCode == hashCode && entry.InstanceId == instanceId )
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
        _entries.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);
        _values.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);
        _indexForValue.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);

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
                int bucket = GetBucketIndex(entry.HashCode);
                entry.NextInBucket = _buckets[bucket];
                _buckets[bucket] = i;
            }
        }
    }

    /// <summary>
    /// ハッシュコードからバケットインデックスを取得（単純モジュロ法）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetBucketIndex(int hashCode)
    {
        // 高速版の絶対値＋モジュロ
        return (hashCode & 0x7FFFFFFF) % _buckets.Length;
    }

    /// <summary>
    /// 指定サイズ以上の最小の素数を取得
    /// </summary>
    private int GetNextPrimeSize(int minSize)
    {
        // バイナリサーチで素数テーブルから適切な値を探す
        int index = Array.BinarySearch(PrimeSizes, minSize);

        if ( index >= 0 )
        {
            // ぴったり一致する素数が見つかった
            return PrimeSizes[index];
        }
        else
        {
            // 一致する値がない場合、~index は挿入すべき位置を表す
            int insertIndex = ~index;

            if ( insertIndex < PrimeSizes.Length )
            {
                // テーブル内の次に大きい素数を返す
                return PrimeSizes[insertIndex];
            }
            else
            {
                // テーブル内の最大値より大きい場合は計算する
                return CalculateNextPrime(minSize);
            }
        }
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