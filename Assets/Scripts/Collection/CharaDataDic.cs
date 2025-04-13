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
/// �Q�[���I�u�W�F�N�g�̃C���X�^���XID���L�[�Ƃ��A�������ɊǗ�����f�[�^����
/// �f���T�C�Y�̃o�P�b�g�ƒP�����W�����@�Ńn�b�V���Փ˂��ŏ���
/// UnsafeList���g�p����GC���ׂ��팸
/// �X�e�[�W�̕ς��ڂƂ���Resize���āA�傫���Ȃ肷���Ă��爳���x�܂ŏk�߂�H�@���ꂩ���g�p�G���g���������߂��Ă���
/// �g������͐�΂�Dispose����
/// </summary>
/// <typeparam name="T">�i�[����f�[�^�̌^�iunmanaged����t���j</typeparam>
public class CharaDataDic<T> : IDisposable, IEnumerable where T : unmanaged
{

    //    �C���X�^���XID�̓�������

    //��{�I�ȓ���:

    //�T���v���̑SID�͕��̒l(100%)
    //�SID������(100%)
    //�l�͈̔͂� -382662 ~ -310670 (�͈�: 71992)
    //���ʂ̌��ɓ����I�ȃp�^�[��������i0, 2, 4, 6, 8�̖������ϓ��ɕ��z�j


    //���z�̓���:
    //�Ō�̌���0, 2, 4, 6, 8�̂݁i����S�����݂��Ȃ��j
    //����3���ɂ͓���̒l���ϓ��ɏo���i�e72��j
    //10000�P�ʂ̋�Ԃłقڋϓ��ɕ��z���Ă���

    //�œK�ȃn�b�V���֐��̑I��
    //�e�X�g���ʂ���A�ȉ��̏��Ŋe�n�b�V���֐��̃p�t�H�[�}���X���������Ƃ��킩��܂����F

    //�f���o�P�b�g�T�C�Y + �P�����W�����@:
    //�o�P�b�g�T�C�Y8209�̏ꍇ�A�Փ˗�8.79%�ōł��Ⴂ
    //�ϓ��ȕ��z�������i�g�p��100%�j


    // �G���g���\���� - �L�[�ƒl�ƃ`�F�[�������i�[
    private struct Entry
    {
        /// <summary>
        /// �Q�[���I�u�W�F�N�g��ID�i�L�[�j
        /// </summary>
        public int InstanceId;

        /// <summary>
        /// �l�z����̃C���f�b�N�X
        /// </summary>
        public int ValueIndex;

        /// <summary>
        /// �����o�P�b�g���̎��̃G���g���ւ̃C���f�b�N�X
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
            if ( TryGetValue(gameObject, out T value, out _) )
                return value;
            throw new KeyNotFoundException($"GameObject {gameObject.name} not found in store");
        }
    }

    /// <summary>
    /// �C���f�N�T - �C���X�^���XID����̒l�A�N�Z�X
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
    /// �C���f�N�T - �l�C���f�b�N�X����̒��ڃA�N�Z�X�i�Q�Ƃ�Ԃ��j
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

        return Add(obj.GetInstanceID(), data);
    }

    /// <summary>
    /// �C���X�^���XID�ƃf�[�^��ǉ��܂��͍X�V���A�l�̃C���f�b�N�X��Ԃ�
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Add(int instanceId, T data)
    {
        // ���׌W���`�F�b�N - �K�v�ɉ����ă��T�C�Y
        if ( (_count - _freeCount) >= _entries.Length * LOAD_FACTOR )
        {
            Resize(_entries.Length * 2);
        }

        // �o�P�b�g�C���f�b�N�X���v�Z�i�P�����W�����@�j
        int bucketIndex = GetBucketIndex(instanceId);

        // �����L�[�����ɑ��݂��邩����
        int entryIndex = _buckets[bucketIndex];
        while ( entryIndex != -1 )
        {
            ref Entry entry = ref _entries.ElementAt(entryIndex);
            if ( entry.InstanceId == instanceId )
            {
                // �����G���g�����X�V
                _values[entry.ValueIndex] = data;
                return entry.ValueIndex;
            }
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
                bucketIndex = GetBucketIndex(instanceId);
            }
            newIndex = _count;
            _count++;
        }

        // �G���g���ƒl�̃��X�g���\���ȑ傫���ɂȂ�悤�g��
        EnsureCapacity(newIndex);

        // �V�����G���g���̐ݒ�
        Entry newEntry;
        newEntry.InstanceId = instanceId;
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

        return TryGetValue(obj.GetInstanceID(), out data, out index);
    }

    /// <summary>
    /// �C���X�^���XID����f�[�^�Ɠ����C���f�b�N�X���擾
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(int instanceId, out T data, out int index)
    {
        int bucketIndex = GetBucketIndex(instanceId);
        int entryIndex = _buckets[bucketIndex];

        while ( entryIndex != -1 )
        {
            // �o�P�b�g������o�����C���f�b�N�X�̃G���g�����擾�B
            ref Entry entry = ref _entries.ElementAt(entryIndex);

            // �G���g���ɋL�^���ꂽID�Ɠ����Ȃ瓯���A�C�e�����Ɣ��f����B
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
    /// �C���X�^���XID�Ɋ֘A�t����ꂽ�f�[�^���폜
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
    /// �Q�[���I�u�W�F�N�g�Ɋ֘A�t����ꂽ�f�[�^���폜
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(GameObject obj)
    {
        return obj != null && Remove(obj.GetInstanceID());
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

    /// <summary>
    /// �����z��̃T�C�Y��ύX
    /// </summary>
    private void Resize(int newCapacity)
    {
        // �f���T�C�Y�ɒ���
        int newPrimeSize = GetNextPrimeSize(newCapacity);

        // �e�R���N�V�����̃T�C�Y���g���i�����f�[�^��ێ��j
        // �T�C�Y��ύX���A�V�K�m�ۃ������͏���������B
        _entries.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);
        _values.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);
        _indexForValue.Resize(newPrimeSize, NativeArrayOptions.ClearMemory);

        // �o�P�b�g���X�g��j�����ĐV�����T�C�Y�ōč쐬
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
                int bucket = Math.Abs(entry.InstanceId) % newPrimeSize;
                entry.NextInBucket = _buckets[bucket];
                _buckets[bucket] = i;
            }
        }
    }

    /// <summary>
    /// �C���X�^���XID����o�P�b�g�C���f�b�N�X���擾�i�P�����W�����@�j
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetBucketIndex(int instanceId)
    {
        return ((instanceId >> 31) & instanceId ^ instanceId) % _buckets.Length;
    }

    /// <summary>
    /// �w��T�C�Y�ȏ�̍ŏ��̑f�����擾
    /// </summary>
    private int GetNextPrimeSize(int minSize)
    {
        // �T�C�Y���\����������Αf���e�[�u������T��
        foreach ( int prime in PrimeSizes )
        {
            if ( prime >= minSize )
                return prime;
        }

        // �e�[�u���ɏ\���ȑ傫���̑f�����Ȃ���Όv�Z
        return CalculateNextPrime(minSize);
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

    /// <summary>
    /// ���ׂĂ̗L���ȃG���g���ɑ΂��ď��������s
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
    /// �L���ȃC���f�b�N�X�݂̂��
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

    public IEnumerator GetEnumerator()
    {
        throw new NotImplementedException();
    }
}