using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.PerformanceTesting;
using UnityEngine;

/// <summary>
/// �l�X�ȃl�C�e�B�u�R���e�i�ƃ}�l�[�W�h�R���N�V�����̒ǉ��E�폜�p�t�H�[�}���X��r�e�X�g
/// </summary>
public class NativeContainerAddRemoveTest
{
    // �e�X�g�̗v�f��
    private const int ElementCount = 100000;

    // �e�X�g�p�̃R���N�V����
    private Dictionary<int, int> dictionary;
    private List<int> list;
    private NativeList<int> nativeList;
    private NativeParallelHashMap<int, int> parallelHashMap;
    private NativeHashMap<int, int> nativeHashMap;
    private UnsafeList<int> unsafeList;
    private UnsafeHashMap<int, int> unsafeHashMap;
    private unsafe UnsafePtrList<int> unsafePtrList; // UnsafePtrList��ǉ�
    private unsafe NativeArray<IntPtr> ptrHolders; // �|�C���^�ێ��p

    // ����񐔐ݒ�
    private const int WarmupCount = 3;
    private const int MeasurementCount = 10;
    // �e����ŏ��������܂ނ̂�1��ɐݒ�
    private const int IterationsPerMeasurement = 1;

    [OneTimeSetUp]
    public unsafe void Setup()
    {
        // �W���R���N�V�����̏�����
        this.dictionary = new Dictionary<int, int>(ElementCount);
        this.list = new List<int>(ElementCount);

        // �l�C�e�B�u�R���e�i�̏�����
        this.nativeList = new NativeList<int>(ElementCount, Allocator.Persistent);
        this.unsafeList = new UnsafeList<int>(ElementCount, Allocator.Persistent);
        this.parallelHashMap = new NativeParallelHashMap<int, int>(ElementCount, Allocator.Persistent);
        this.nativeHashMap = new NativeHashMap<int, int>(ElementCount, Allocator.Persistent);
        this.unsafeHashMap = new UnsafeHashMap<int, int>(ElementCount, Allocator.Persistent);

        // UnsafePtrList�p�̏�����
        this.unsafePtrList = new UnsafePtrList<int>(ElementCount, Allocator.Persistent);
        this.ptrHolders = new NativeArray<IntPtr>(ElementCount, Allocator.Persistent);

        Debug.Log($"AddRemoveTests: Setup complete with {ElementCount} elements capacity");
    }

    [OneTimeTearDown]
    public unsafe void TearDown()
    {
        // UnsafePtrList�Ŋm�ۂ����������̉��
        for ( int i = 0; i < this.ptrHolders.Length; i++ )
        {
            if ( this.ptrHolders[i] != IntPtr.Zero )
            {
                UnsafeUtility.Free((void*)this.ptrHolders[i], Allocator.Persistent);
                this.ptrHolders[i] = IntPtr.Zero;
            }
        }

        // �R���e�i�̉��
        if ( this.ptrHolders.IsCreated )
        {
            this.ptrHolders.Dispose();
        }

        if ( this.unsafePtrList.IsCreated )
        {
            this.unsafePtrList.Dispose();
        }

        // �l�C�e�B�u�R���e�i�̉��
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

    #region �l�̒ǉ��e�X�g

    //[Test, Performance]
    //public void AddValues_Dictionary()
    //{
    //    Measure.Method(() =>
    //    {
    //        // �e����̍ŏ��ɃN���A
    //        dictionary.Clear();

    //        // �v�f��ǉ�
    //        for ( int i = 0; i < ElementCount; i++ )
    //        {
    //            dictionary[i] = i;
    //        }

    //        // �m�F�i�����j
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
    //        // �e����̍ŏ��ɃN���A
    //        list.Clear();

    //        // �v�f��ǉ�
    //        for ( int i = 0; i < ElementCount; i++ )
    //        {
    //            list.Add(i);
    //        }

    //        // �m�F�i�����j
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
    //        // �e����̍ŏ��ɃN���A
    //        nativeList.Clear();

    //        // �v�f��ǉ�
    //        for ( int i = 0; i < ElementCount; i++ )
    //        {
    //            nativeList.Add(i);
    //        }

    //        // �m�F�i�����j
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
    //        // �e����̍ŏ��ɃN���A
    //        unsafeList.Clear();

    //        // �v�f��ǉ�
    //        for ( int i = 0; i < ElementCount; i++ )
    //        {
    //            unsafeList.Add(i);
    //        }

    //        // �m�F�i�����j
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
    //        // �e����̍ŏ��ɃN���A
    //        unsafePtrList.Clear();
    //        
    //        // �����̃|�C���^�����ׂĉ��
    //        for (int i = 0; i < ptrHolders.Length; i++)
    //        {
    //            if (ptrHolders[i] != IntPtr.Zero)
    //            {
    //                UnsafeUtility.Free((void*)ptrHolders[i], Allocator.Persistent);
    //                ptrHolders[i] = IntPtr.Zero;
    //            }
    //        }

    //        // �V�����|�C���^�𐶐����Ēǉ�
    //        for (int i = 0; i < ElementCount; i++)
    //        {
    //            IntPtr ptr = (IntPtr)UnsafeUtility.Malloc(sizeof(int), UnsafeUtility.AlignOf<int>(), Allocator.Persistent);
    //            *(int*)ptr = i; // �l�̐ݒ�
    //            ptrHolders[i] = ptr; // ��ŉ�����邽�߂ɕۑ�
    //            unsafePtrList.Add((int*)ptr);
    //        }

    //        // �m�F�i�����j
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
    //        // �e����̍ŏ��ɃN���A
    //        parallelHashMap.Clear();

    //        // �v�f��ǉ�
    //        for ( int i = 0; i < ElementCount; i++ )
    //        {
    //            parallelHashMap.Add(i, i);
    //        }

    //        // �m�F�i�����j
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
    //        // �e����̍ŏ��ɃN���A
    //        nativeHashMap.Clear();

    //        // �v�f��ǉ�
    //        for ( int i = 0; i < ElementCount; i++ )
    //        {
    //            nativeHashMap.Add(i, i);
    //        }

    //        // �m�F�i�����j
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
    //        // �e����̍ŏ��ɃN���A
    //        unsafeHashMap.Clear();

    //        // �v�f��ǉ�
    //        for ( int i = 0; i < ElementCount; i++ )
    //        {
    //            unsafeHashMap.Add(i, i);
    //        }

    //        // �m�F�i�����j
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

    #region �l�̍폜�e�X�g

    [Test, Performance]
    public void RemoveValues_Dictionary()
    {
        Measure.Method(() =>
        {
            // ����O�Ƀf�[�^������
            this.dictionary.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                this.dictionary[i] = i;
            }

            // �S�Ă̗v�f���폜
            for ( int i = 0; i < ElementCount; i++ )
            {
                _ = this.dictionary.Remove(i);
            }

            // �m�F�i�����j
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
            // �e�X�g�p�Ƀf�[�^������
            this.list.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                this.list.Add(i);
            }

            // ���X�g����ɂȂ�܂ōŌ�̗v�f���폜��������
            while ( this.list.Count > 0 )
            {
                this.list.RemoveAt(this.list.Count - 1);
            }

            // �m�F�i�����j
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
            // �e�X�g�p�Ƀf�[�^������
            this.nativeList.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                this.nativeList.Add(i);
            }

            // ���X�g����ɂȂ�܂ōŌ�̗v�f���폜��������
            while ( this.nativeList.Length > 0 )
            {
                this.nativeList.RemoveAt(this.nativeList.Length - 1);
            }

            // �m�F�i�����j
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
            // �e�X�g�p�Ƀf�[�^������
            this.unsafeList.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                this.unsafeList.Add(i);
            }

            // ���X�g����ɂȂ�܂ōŌ�̗v�f���폜��������
            while ( this.unsafeList.Length > 0 )
            {
                this.unsafeList.RemoveAt(this.unsafeList.Length - 1);
            }

            // �m�F�i�����j
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
            // �e�X�g�p�Ƀf�[�^������ - �e����ɂ��V���Ƀ|�C���^���쐬
            this.unsafePtrList.Clear();

            // �����̃|�C���^�����ׂĉ��
            for ( int i = 0; i < this.ptrHolders.Length; i++ )
            {
                if ( this.ptrHolders[i] != IntPtr.Zero )
                {
                    UnsafeUtility.Free((void*)this.ptrHolders[i], Allocator.Persistent);
                    this.ptrHolders[i] = IntPtr.Zero;
                }
            }

            // �V�����|�C���^�𐶐����Ēǉ�
            for ( int i = 0; i < ElementCount; i++ )
            {
                IntPtr ptr = (IntPtr)UnsafeUtility.Malloc(sizeof(int), UnsafeUtility.AlignOf<int>(), Allocator.Persistent);
                *(int*)ptr = i; // �l�̐ݒ�
                this.ptrHolders[i] = ptr; // ��ŉ�����邽�߂ɕۑ�
                this.unsafePtrList.Add((int*)ptr);
            }

            // ���X�g����ɂȂ�܂ōŌ�̗v�f���폜��������i������������s���j
            while ( this.unsafePtrList.Length > 0 )
            {
                int index = this.unsafePtrList.Length - 1;
                int* ptr = this.unsafePtrList[index];
                this.unsafePtrList.RemoveAt(index);

                // �Ή�����ptrHolders�̃G���g���������A�����������
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

            // �m�F�i�����j
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
            // ����O�Ƀf�[�^������
            this.parallelHashMap.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                this.parallelHashMap.Add(i, i);
            }

            // �S�Ă̗v�f���폜
            for ( int i = 0; i < ElementCount; i++ )
            {
                _ = this.parallelHashMap.Remove(i);
            }

            // �m�F�i�����j
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
            // ����O�Ƀf�[�^������
            this.nativeHashMap.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                this.nativeHashMap.Add(i, i);
            }

            // �S�Ă̗v�f���폜
            for ( int i = 0; i < ElementCount; i++ )
            {
                _ = this.nativeHashMap.Remove(i);
            }

            // �m�F�i�����j
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
            // ����O�Ƀf�[�^������
            this.unsafeHashMap.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                this.unsafeHashMap.Add(i, i);
            }

            // �S�Ă̗v�f���폜
            for ( int i = 0; i < ElementCount; i++ )
            {
                _ = this.unsafeHashMap.Remove(i);
            }

            // �m�F�i�����j
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