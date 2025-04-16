using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using static Unity.Collections.AllocatorManager;

/// <summary>
/// �Q�[���I�u�W�F�N�g��GetHashCode()���L�[�Ƃ��A�������ɊǗ�����f�[�^����
/// Unity�ł�GetHashCode()��GetInstanceID()�Ɠ����l��Ԃ����߁A��Ӑ����ۏ؂���Ă���B
/// UnsafeList���g�p����GC���ׂ��팸
/// </summary>
/// <typeparam name="T">�i�[����f�[�^�̌^�iJobSystem���ӎ�����unmanaged����t���j</typeparam>
public class CharaDataDic<T> : IDisposable where T : unmanaged
{
    /// <summary>
    /// �G���g���\���� - �L�[�ƒl�ƃ`�F�[�������i�[<br></br>
    /// �n�b�V���R�[�h���o�P�b�gID���G���g�������ۂ̃C���f�b�N�X�Ƃ�������B
    /// </summary>
    private struct Entry
    {
        /// <summary>
        /// �Q�[���I�u�W�F�N�g�̃n�b�V���R�[�h�iGetInstanceID()�Ɠ���j
        /// </summary>
        public int HashCode;

        /// <summary>
        /// �l�z����̃C���f�b�N�X
        /// </summary>
        public int ValueIndex;

        /// <summary>
        /// �����o�P�b�g���̎��̃G���g���ւ̃C���f�b�N�X�B
        /// </summary>
        public int NextInBucket;

        /// <summary>
        /// ���̃G���g�����g�p�����ǂ���
        /// </summary>
        public bool IsOccupied;
    }

    // �����f�[�^�\���iUnsafeList�x�[�X�j
    private UnsafeList<int> _buckets;           // �o�P�b�g�z��i�e�v�f�̓G���g���ւ̃C���f�b�N�X�A-1�͋�j
    private UnsafeList<Entry> _entries;         // �G���g���̔z��
    private UnsafeList<T> _values;              // ���ۂ̃f�[�^���i�[����z��
    private UnsafeList<int> _indexForValue;     // �l�C���f�b�N�X���G���g���C���f�b�N�X�̋t����

    private int _count;                  // �g�p���̃G���g����
    private int _freeListHead;           // �폜�ς݃G���g���̍ė��p���X�g�擪
    private int _freeCount;              // �ė��p�\�ȃG���g����

    private bool _isDisposed;            // ����ς݃t���O
    private readonly Allocator _allocator; // �������A���P�[�^

    // �f���e�[�u�� - �悭�g����T�C�Y�ɋ߂��f��
    private static readonly int[] PrimeSizes = {
        17, 37, 79, 163, 331, 673, 1361, 2729, 5471, 10949, 21911, 43853,
        87719, 175447, 350899, 701819, 1403641, 2807303, 5614657, 11229331
    };

    // �萔
    private const int DEFAULT_CAPACITY = 1031;    // �f�t�H���g�e�ʁi�f���j
    private const float LOAD_FACTOR = 0.75f;      // ���׌W���i���̒l�𒴂���ƃ��T�C�Y�j

    /// <summary>
    /// �i�[����Ă���v�f��
    /// </summary>
    public int Count => _count - _freeCount;

    /// <summary>
    /// �o�P�b�g�̗e��
    /// </summary>
    public int Capacity => _entries.Length;

    /// <summary>
    /// �C���f�N�T - �Q�[���I�u�W�F�N�g����̒l�A�N�Z�X
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
    /// �C���f�N�T - �n�b�V���R�[�h/�C���X�^���XID����̒l�A�N�Z�X
    /// </summary>
    public T this[int hashOrInstanceId]
    {
        get
        {
            if ( TryGetValueByHash(hashOrInstanceId, out T value, out _) )
                return value;
            throw new KeyNotFoundException($"HashCode/InstanceID {hashOrInstanceId} not found in store");
        }
        set => AddByHash(hashOrInstanceId, value);
    }

    /// <summary>
    /// �C���f�N�T - �l�C���f�b�N�X����̒��ڃA�N�Z�X
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
    /// �R���X�g���N�^
    /// </summary>
    /// <param name="capacity">�����e�ʁi�f���ɒ�������܂��j</param>
    /// <param name="allocator">�������A���P�[�^�i�f�t�H���g��Persistent�j</param>
    public CharaDataDic(int capacity = DEFAULT_CAPACITY, Allocator allocator = Allocator.Persistent)
    {
        // �A���P�[�^�ۑ�
        _allocator = allocator;

        // �w��e�ʈȏ�̍ŏ��̑f����I��
        int primeCapacity = GetNextPrimeSize(capacity);

        // UnsafeList�̏�����
        _buckets = new UnsafeList<int>(primeCapacity, allocator);
        _entries = new UnsafeList<Entry>(primeCapacity, allocator);
        _values = new UnsafeList<T>(primeCapacity, allocator);
        _indexForValue = new UnsafeList<int>(primeCapacity, allocator);

        // �o�P�b�g���X�g�̗e�ʊm�ۂ�-1�ŏ�����
        _buckets.Resize(primeCapacity, NativeArrayOptions.ClearMemory);
        for ( int i = 0; i < _buckets.Length; i++ )
        {
            _buckets[i] = -1;
        }

        // ���̃��X�g�̗e�ʂ��m��
        _entries.Capacity = primeCapacity;
        _values.Capacity = primeCapacity;
        _indexForValue.Capacity = primeCapacity;

        _freeListHead = -1;
        _count = 0;
        _freeCount = 0;
    }

    /// <summary>
    /// �Q�[���I�u�W�F�N�g�ƃf�[�^��ǉ��܂��͍X�V
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Add(GameObject obj, T data)
    {
        if ( obj == null )
            throw new ArgumentNullException(nameof(obj));

        // GetHashCode()���g�p�iGetInstanceID()�Ɠ����l�j
        return AddByHash(obj.GetHashCode(), data);
    }

    /// <summary>
    /// �n�b�V���R�[�h/�C���X�^���XID�ƃf�[�^��ǉ��܂��͍X�V���A�l�̃C���f�b�N�X��Ԃ�
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AddByHash(int hashCode, T data)
    {
        // ���׌W���`�F�b�N - �K�v�ɉ����ă��T�C�Y
        if ( (_count - _freeCount) >= _entries.Length * LOAD_FACTOR )
        {
            Resize(_entries.Length * 2);
        }

        // �o�P�b�g�C���f�b�N�X���v�Z�i�P�����W�����@�j
        int bucketIndex = GetBucketIndex(hashCode);

        // �����L�[�����ɑ��݂��邩����
        int entryIndex = _buckets[bucketIndex];
        while ( entryIndex != -1 )
        {
            ref Entry entry = ref _entries.ElementAt(entryIndex);
            // �n�b�V���R�[�h�݂̂Ń`�F�b�N�iGetInstanceID()�Ɠ����l�Ȃ̂ň�Ӑ����ۏ؂����j
            if ( entry.HashCode == hashCode )
            {
                // �����G���g�����X�V
                _values[entry.ValueIndex] = data;
                return entry.ValueIndex;
            }

            // �n�b�V���R�[�h���d�����Ȃ����A�����o�P�b�g�ɗv�f������ꍇ�̓o�P�b�g���̎��̗v�f�Ƃ��ĕۑ��B
            entryIndex = entry.NextInBucket;
        }

        // �V�����G���g���p�̃C���f�b�N�X���m��
        int newIndex;
        if ( _freeCount > 0 )
        {
            // �폜�ς݃G���g�����ė��p
            newIndex = _freeListHead;
            _freeListHead = _entries.ElementAt(newIndex).NextInBucket;
            _freeCount--;
        }
        else
        {
            // �V�����X���b�g���g�p
            if ( _count == _entries.Length )
            {
                Resize(_entries.Length * 2);
                bucketIndex = GetBucketIndex(hashCode);
            }
            newIndex = _count;
            _count++;
        }

        // �G���g���ƒl�̃��X�g���\���ȑ傫���ɂȂ�悤�g��
        EnsureCapacity(newIndex);

        // �V�����G���g���̐ݒ�
        Entry newEntry;
        newEntry.HashCode = hashCode;
        newEntry.ValueIndex = newIndex;
        newEntry.NextInBucket = _buckets[bucketIndex];
        newEntry.IsOccupied = true;

        // UnsafeList�̍X�V
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
    /// ����̃C���f�b�N�X�ɑΉ����郊�X�g�̗e�ʂ��m��
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int index)
    {
        // �K�v�ɉ����Ċe���X�g�̗e�ʂ��g��
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
    /// �w�肵���C���f�b�N�X���L���i�g�p���j���ǂ������m�F
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValidIndex(int index)
    {
        return index >= 0 && index < _count && _entries[index].IsOccupied;
    }

    /// <summary>
    /// �C���f�b�N�X���璼�ڃf�[�^���擾�i�Q�Ƃ�Ԃ��j
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
    /// �Q�[���I�u�W�F�N�g����f�[�^�Ɠ����C���f�b�N�X���擾
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

        // GetHashCode()���g�p�iGetInstanceID()�Ɠ����l�j
        return TryGetValueByHash(obj.GetHashCode(), out data, out index);
    }

    /// <summary>
    /// �n�b�V���R�[�h/�C���X�^���XID����f�[�^�Ɠ����C���f�b�N�X���擾
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValueByHash(int hashCode, out T data, out int index)
    {
        int bucketIndex = GetBucketIndex(hashCode);
        int entryIndex = _buckets[bucketIndex];

        while ( entryIndex != -1 )
        {
            ref Entry entry = ref _entries.ElementAt(entryIndex);
            if ( entry.HashCode == hashCode )
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
    /// �Q�[���I�u�W�F�N�g�Ɋ֘A�t����ꂽ�f�[�^���폜
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(GameObject obj)
    {
        if ( obj == null )
            return false;

        // GetHashCode()���g�p�iGetInstanceID()�Ɠ����l�j
        return RemoveByHash(obj.GetHashCode());
    }

    /// <summary>
    /// �n�b�V���R�[�h/�C���X�^���XID�Ɋ֘A�t����ꂽ�f�[�^���폜
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool RemoveByHash(int hashCode)
    {
        if ( _count == 0 )
            return false;

        int bucketIndex = GetBucketIndex(hashCode);
        int entryIndex = _buckets[bucketIndex];
        int prevIndex = -1;

        while ( entryIndex != -1 )
        {
            ref Entry entry = ref _entries.ElementAt(entryIndex);

            if ( entry.HashCode == hashCode )
            {
                // �G���g�����o�P�b�g���X�g����폜
                if ( prevIndex != -1 )
                {
                    // �O�̃G���g���̎��̃����N���X�V
                    Entry prevEntry = _entries[prevIndex];
                    prevEntry.NextInBucket = entry.NextInBucket;
                    _entries[prevIndex] = prevEntry;
                }
                else
                {
                    _buckets[bucketIndex] = entry.NextInBucket;
                }

                // �G���g����_���I�ɍ폜���ăt���[���X�g�ɒǉ�
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
    /// ���ׂẴG���g�����N���A
    /// </summary>
    public void Clear()
    {
        if ( _count == 0 )
            return;

        // �o�P�b�g��-1�ŏ�����
        for ( int i = 0; i < _buckets.Length; i++ )
        {
            _buckets[i] = -1;
        }

        // UnsafeList���N���A
        _entries.Clear();
        _values.Clear();
        _indexForValue.Clear();

        _count = 0;
        _freeCount = 0;
        _freeListHead = -1;
    }

    #region �����f�[�^�Ǘ�����

    /// <summary>
    /// �����z��̃T�C�Y��ύX
    /// </summary>
    private void Resize(int newCapacity)
    {
        // �f���T�C�Y�ɒ���
        int newPrimeSize = GetNextPrimeSize(newCapacity);

        // �e�R���N�V�����̃T�C�Y���g���i�����f�[�^��ێ��j
        _entries.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);
        _values.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);
        _indexForValue.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);

        // �o�P�b�g���X�g��V�����T�C�Y�ō�蒼���A-1�ŏ�����
        _buckets.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);

        for ( int i = 0; i < newPrimeSize; i++ )
        {
            _buckets[i] = -1;
        }

        // ���ׂẴG���g�����ăn�b�V��
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
    /// �n�b�V���R�[�h����o�P�b�g�C���f�b�N�X���擾�i�P�����W�����@�j
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetBucketIndex(int hashCode)
    {
        // �n�b�V���R�[�h�͕��̒l�����蓾��̂ŁA��Βl�������Ɏ擾
        // hashCode & 0x7FFFFFFF �� Math.Abs(hashCode) ��荂��
        // ��Βl��Mod���Ƃ邱�Ƃ� 0�`�v�f��-1�A�̊Ԃ̃C���f�b�N�X���擾�ł���
        return (hashCode & 0x7FFFFFFF) % _buckets.Length;
    }

    /// <summary>
    /// �w��T�C�Y�ȏ�̍ŏ��̑f�����擾
    /// </summary>
    private int GetNextPrimeSize(int minSize)
    {
        // �o�C�i���T�[�`�őf���e�[�u������K�؂Ȓl��T��
        int index = Array.BinarySearch(PrimeSizes, minSize);

        if ( index >= 0 )
        {
            // �҂������v����f������������
            return PrimeSizes[index];
        }
        else
        {
            // ��v����l���Ȃ��ꍇ�A~index �͑}�����ׂ��ʒu��\��
            int insertIndex = ~index;

            if ( insertIndex < PrimeSizes.Length )
            {
                // �e�[�u�����̎��ɑ傫���f����Ԃ�
                return PrimeSizes[insertIndex];
            }
            else
            {
                // �e�[�u�����̍ő�l���傫���ꍇ�͌v�Z����
                return CalculateNextPrime(minSize);
            }
        }
    }

    /// <summary>
    /// �w��l�ȏ�̎��̑f�����v�Z
    /// </summary>
    private int CalculateNextPrime(int minSize)
    {
        // �����J�n�i������2�ȊO�f���ɂȂ�Ȃ��j
        int candidate = minSize;
        if ( candidate % 2 == 0 )
            candidate++;

        while ( !IsPrime(candidate) )
        {
            candidate += 2; // ��݂̂��`�F�b�N
        }

        return candidate;
    }

    /// <summary>
    /// �f������
    /// </summary>
    private bool IsPrime(int number)
    {
        if ( number <= 1 )
            return false;
        if ( number == 2 || number == 3 )
            return true;
        if ( number % 2 == 0 || number % 3 == 0 )
            return false;

        // 6k�}1�̌`�ŕ\����鐔�̂݃`�F�b�N�i�������j
        int limit = (int)Math.Sqrt(number);
        for ( int i = 5; i <= limit; i += 6 )
        {
            if ( number % i == 0 || number % (i + 2) == 0 )
                return false;
        }

        return true;
    }

    #endregion �����f�[�^�Ǘ�����

    /// <summary>
    /// ���ׂĂ̗L���ȃG���g���ɑ΂��ď��������s�B<br></br>
    /// IEnumerable�̑���
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
    /// �W���u�V�X�e���ŃL�����N�^�[�f�[�^���g�p���邽�߂Ƀ��X�g���������擾����B<br></br>
    /// ��΂ɂ����Ŏ󂯎�������X�g��Dispose���Ă͂Ȃ�Ȃ��B���̎���Dictionary�̓Q�[���I�����ɔj������B<br></br>
    /// �܂��A�Ӑ}�����Q�Ƃ��c��Ȃ��悤�Ƀ��[�J���ϐ��ȊO�Ŏ󂯎���Ă����߁B<br></br>
    /// ReadOnly�ɂ������Ƃ��낾���ǁA��������Ƃ��낢��g���ɂ�������d���Ȃ��B
    /// </summary>
    /// <returns></returns>
    public UnsafeList<T> GetInternalListForJob()
    {
        // �������X�g��Ԃ�
        return _values;
    }

    /// <summary>
    /// ���\�[�X�����
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




}