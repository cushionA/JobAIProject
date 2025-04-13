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
        dictionary = new Dictionary<int, int>(ElementCount);
        list = new List<int>(ElementCount);

        // �l�C�e�B�u�R���e�i�̏�����
        nativeList = new NativeList<int>(ElementCount, Allocator.Persistent);
        unsafeList = new UnsafeList<int>(ElementCount, Allocator.Persistent);
        parallelHashMap = new NativeParallelHashMap<int, int>(ElementCount, Allocator.Persistent);
        nativeHashMap = new NativeHashMap<int, int>(ElementCount, Allocator.Persistent);
        unsafeHashMap = new UnsafeHashMap<int, int>(ElementCount, Allocator.Persistent);

        // UnsafePtrList�p�̏�����
        unsafePtrList = new UnsafePtrList<int>(ElementCount, Allocator.Persistent);
        ptrHolders = new NativeArray<IntPtr>(ElementCount, Allocator.Persistent);

        Debug.Log($"AddRemoveTests: Setup complete with {ElementCount} elements capacity");
    }

    [OneTimeTearDown]
    public unsafe void TearDown()
    {
        // UnsafePtrList�Ŋm�ۂ����������̉��
        for ( int i = 0; i < ptrHolders.Length; i++ )
        {
            if ( ptrHolders[i] != IntPtr.Zero )
            {
                UnsafeUtility.Free((void*)ptrHolders[i], Allocator.Persistent);
                ptrHolders[i] = IntPtr.Zero;
            }
        }

        // �R���e�i�̉��
        if ( ptrHolders.IsCreated )
            ptrHolders.Dispose();

        if ( unsafePtrList.IsCreated )
            unsafePtrList.Dispose();

        // �l�C�e�B�u�R���e�i�̉��
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
            dictionary.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                dictionary[i] = i;
            }

            // �S�Ă̗v�f���폜
            for ( int i = 0; i < ElementCount; i++ )
            {
                dictionary.Remove(i);
            }

            // �m�F�i�����j
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
            // �e�X�g�p�Ƀf�[�^������
            list.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                list.Add(i);
            }

            // ���X�g����ɂȂ�܂ōŌ�̗v�f���폜��������
            while ( list.Count > 0 )
            {
                list.RemoveAt(list.Count - 1);
            }

            // �m�F�i�����j
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
            // �e�X�g�p�Ƀf�[�^������
            nativeList.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                nativeList.Add(i);
            }

            // ���X�g����ɂȂ�܂ōŌ�̗v�f���폜��������
            while ( nativeList.Length > 0 )
            {
                nativeList.RemoveAt(nativeList.Length - 1);
            }

            // �m�F�i�����j
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
            // �e�X�g�p�Ƀf�[�^������
            unsafeList.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                unsafeList.Add(i);
            }

            // ���X�g����ɂȂ�܂ōŌ�̗v�f���폜��������
            while ( unsafeList.Length > 0 )
            {
                unsafeList.RemoveAt(unsafeList.Length - 1);
            }

            // �m�F�i�����j
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
            // �e�X�g�p�Ƀf�[�^������ - �e����ɂ��V���Ƀ|�C���^���쐬
            unsafePtrList.Clear();

            // �����̃|�C���^�����ׂĉ��
            for ( int i = 0; i < ptrHolders.Length; i++ )
            {
                if ( ptrHolders[i] != IntPtr.Zero )
                {
                    UnsafeUtility.Free((void*)ptrHolders[i], Allocator.Persistent);
                    ptrHolders[i] = IntPtr.Zero;
                }
            }

            // �V�����|�C���^�𐶐����Ēǉ�
            for ( int i = 0; i < ElementCount; i++ )
            {
                IntPtr ptr = (IntPtr)UnsafeUtility.Malloc(sizeof(int), UnsafeUtility.AlignOf<int>(), Allocator.Persistent);
                *(int*)ptr = i; // �l�̐ݒ�
                ptrHolders[i] = ptr; // ��ŉ�����邽�߂ɕۑ�
                unsafePtrList.Add((int*)ptr);
            }

            // ���X�g����ɂȂ�܂ōŌ�̗v�f���폜��������i������������s���j
            while ( unsafePtrList.Length > 0 )
            {
                int index = unsafePtrList.Length - 1;
                int* ptr = unsafePtrList[index];
                unsafePtrList.RemoveAt(index);

                // �Ή�����ptrHolders�̃G���g���������A�����������
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

            // �m�F�i�����j
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
            // ����O�Ƀf�[�^������
            parallelHashMap.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                parallelHashMap.Add(i, i);
            }

            // �S�Ă̗v�f���폜
            for ( int i = 0; i < ElementCount; i++ )
            {
                parallelHashMap.Remove(i);
            }

            // �m�F�i�����j
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
            // ����O�Ƀf�[�^������
            nativeHashMap.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                nativeHashMap.Add(i, i);
            }

            // �S�Ă̗v�f���폜
            for ( int i = 0; i < ElementCount; i++ )
            {
                nativeHashMap.Remove(i);
            }

            // �m�F�i�����j
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
            // ����O�Ƀf�[�^������
            unsafeHashMap.Clear();
            for ( int i = 0; i < ElementCount; i++ )
            {
                unsafeHashMap.Add(i, i);
            }

            // �S�Ă̗v�f���폜
            for ( int i = 0; i < ElementCount; i++ )
            {
                unsafeHashMap.Remove(i);
            }

            // �m�F�i�����j
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