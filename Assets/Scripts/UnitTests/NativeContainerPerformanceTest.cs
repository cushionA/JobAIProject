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
/// �l�X�ȃl�C�e�B�u�R���e�i�ƃ}�l�[�W�h�R���N�V�����̃p�t�H�[�}���X��r�e�X�g
/// - �A���A�N�Z�X���x�̔�r
/// - Job�V�X�e���ł̎��s���x��r
/// </summary>
public class NativeContainerPerformanceTest
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
    private NativeArray<int> nativeArray;
    private int[] regularArray; // �ʏ�̔z��
    private unsafe UnsafePtrList<int> unsafePtrList; // UnsafePtrList��ǉ�
    private unsafe NativeArray<IntPtr> valueHolders; // �|�C���^�i�[�p�o�b�t�@

    // �e�R���N�V�����̍��v�l��ۑ��i���m�����ؗp�j
    private long dictionarySum;
    private long listSum;
    private long nativeListSum;
    private long unsafeListSum;
    private long parallelHashMapSum;
    private long nativeHashMapSum;
    private long unsafeHashMapSum;
    private long nativeArraySum;
    private long regularArraySum; // �ʏ�̔z��
    private long spanSum; // Span
    private long nativeListJobSum;
    private long unsafeListJobSum;
    private long parallelHashMapJobSum;
    private long nativeHashMapJobSum;
    private long unsafeHashMapJobSum;
    private long nativeArrayJobSum;
    private long unsafePtrListSum; // UnsafePtrList�p�̍��v�l
    private long unsafePtrListJobSum; // UnsafePtrList��Job�p�̍��v�l


    // ����񐔐ݒ�
    private const int WarmupCount = 3;
    private const int MeasurementCount = 10;
    private const int IterationsPerMeasurement = 5;

    [OneTimeSetUp]
    public void Setup()
    {
        // �W���R���N�V�����̏�����
        dictionary = new Dictionary<int, int>(ElementCount);
        list = new List<int>(ElementCount);
        regularArray = new int[ElementCount]; // �ʏ�̔z��̏�����

        // �l�C�e�B�u�R���e�i�̏�����
        nativeList = new NativeList<int>(ElementCount, Allocator.Persistent);
        unsafeList = new UnsafeList<int>(ElementCount, Allocator.Persistent);
        parallelHashMap = new NativeParallelHashMap<int, int>(ElementCount, Allocator.Persistent);
        nativeHashMap = new NativeHashMap<int, int>(ElementCount, Allocator.Persistent);
        unsafeHashMap = new UnsafeHashMap<int, int>(ElementCount, Allocator.Persistent);
        nativeArray = new NativeArray<int>(ElementCount, Allocator.Persistent);

        // UnsafePtrList�p�̏�����
        unsafePtrList = new UnsafePtrList<int>(ElementCount, Allocator.Persistent);
        valueHolders = new NativeArray<IntPtr>(ElementCount, Allocator.Persistent);

        unsafe
        {
            // �|�C���^�������߂̒l���l�C�e�B�u�������Ɋm��
            for ( int i = 0; i < ElementCount; i++ )
            {
                // IntPtr���g�p���Ċe�l���ʂɊm��
                IntPtr ptr = (IntPtr)UnsafeUtility.Malloc(sizeof(int), UnsafeUtility.AlignOf<int>(), Allocator.Persistent);
                // �l��ݒ�
                *(int*)ptr = i;
                // ��ŉ���ł���悤�Ƀ|�C���^��ۑ�
                valueHolders[i] = ptr;
                // UnsafePtrList�ɒǉ�
                unsafePtrList.Add((int*)ptr);
            }
        }

        Debug.Log($"Setup complete with {ElementCount} elements capacity");
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        // ���m���̌��� - �e�R���N�V�����̍��v�l��NativeArray�̌��ʂƔ�r
        Debug.Log("���m�����،���:");

        if ( nativeArraySum == 0 )
        {
            Debug.Log("NativeArray�̍��v�l��0�̂��߁A���؂��X�L�b�v���܂��B�A���A�N�Z�X�e�X�g�����s���Ă��������B");
        }
        else
        {
            long expectedSum = (long)ElementCount * (ElementCount - 1) / 2; // ���_��̍��v�l: 0+1+2+...+(n-1)

            Debug.Log($"���_��̍��v�l: {expectedSum}");

            if ( dictionarySum != nativeArraySum )
                Debug.LogWarning($"�s��v: Dictionary={dictionarySum}, NativeArray={nativeArraySum}");

            if ( listSum != nativeArraySum )
                Debug.LogWarning($"�s��v: List={listSum}, NativeArray={nativeArraySum}");

            if ( nativeListSum != nativeArraySum )
                Debug.LogWarning($"�s��v: NativeList={nativeListSum}, NativeArray={nativeArraySum}");

            if ( unsafeListSum != nativeArraySum )
                Debug.LogWarning($"�s��v: UnsafeList={unsafeListSum}, NativeArray={nativeArraySum}");

            if ( unsafePtrListSum != nativeArraySum )
                Debug.LogWarning($"�s��v: UnsafePtrList={unsafePtrListSum}, NativeArray={nativeArraySum}");


            if ( parallelHashMapSum != nativeArraySum )
                Debug.LogWarning($"�s��v: ParallelHashMap={parallelHashMapSum}, NativeArray={nativeArraySum}");

            if ( nativeHashMapSum != nativeArraySum )
                Debug.LogWarning($"�s��v: NativeHashMap={nativeHashMapSum}, NativeArray={nativeArraySum}");

            if ( unsafeHashMapSum != nativeArraySum )
                Debug.LogWarning($"�s��v: UnsafeHashMap={unsafeHashMapSum}, NativeArray={nativeArraySum}");


            if ( nativeArrayJobSum != nativeArraySum )
                Debug.LogWarning($"�s��v: (Job)NativeArray={nativeArrayJobSum}, NativeArray={nativeArraySum}");

            if ( nativeListJobSum != nativeArraySum )
                Debug.LogWarning($"�s��v: (Job)NativeList={nativeListJobSum}, NativeArray={nativeArraySum}");

            if ( unsafeListJobSum != nativeArraySum )
                Debug.LogWarning($"�s��v: (Job)UnsafeList={unsafeListJobSum}, NativeArray={nativeArraySum}");

            if ( unsafePtrListJobSum != 0 && unsafePtrListJobSum != nativeArraySum )
                Debug.LogWarning($"�s��v: (Job)UnsafePtrList={unsafePtrListJobSum}, NativeArray={nativeArraySum}");

            if ( parallelHashMapJobSum != nativeArraySum )
                Debug.LogWarning($"�s��v: (Job)ParallelHashMap={parallelHashMapJobSum}, NativeArray={nativeArraySum}");

            if ( nativeHashMapJobSum != nativeArraySum )
                Debug.LogWarning($"�s��v: (Job)NativeHashMap={nativeHashMapJobSum}, NativeArray={nativeArraySum}");

            if ( unsafeHashMapJobSum != nativeArraySum )
                Debug.LogWarning($"�s��v: (Job)UnsafeHashMap={unsafeHashMapJobSum}, NativeArray={nativeArraySum}");



            if ( regularArraySum != nativeArraySum )
                Debug.LogWarning($"�s��v: RegularArray={regularArraySum}, NativeArray={nativeArraySum}");

            if ( spanSum != nativeArraySum )
                Debug.LogWarning($"�s��v: Span={spanSum}, NativeArray={nativeArraySum}");

            if ( nativeArraySum != expectedSum )
                Debug.LogWarning($"�s��v: NativeArray={nativeArraySum}, ���_�l={expectedSum}");
            else
                Debug.Log($"��v: NativeArray�̍��v�l {nativeArraySum} �͗��_�l�ƈ�v���Ă��܂�");
        }

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

        if ( nativeArray.IsCreated )
            nativeArray.Dispose();

        Debug.Log("All resources disposed");
    }

    #region �A���A�N�Z�X�e�X�g

    [Test, Performance]
    public unsafe void SequentialAccess_UnsafePtrList()
    {
        Measure.Method(() =>
        {
            long sum = 0;
            for ( int i = 0; i < unsafePtrList.Length; i++ )
            {
                // �|�C���^����l���擾���č��v
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
        // ����O�Ƀf�[�^������
        for ( int i = 0; i < ElementCount; i++ )
        {
            regularArray[i] = i;
        }

        Measure.Method(() =>
        {
            long sum = 0;
            // ��葽���̌J��Ԃ� (��: 10��) �Ōv���l������
            for ( int i = 0; i < regularArray.Length; i++ )
            {
                sum += regularArray[i];
            }
            regularArraySum = sum; // ���ϒl��ۑ�
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void SequentialAccess_Span()
    {
        // ����O�Ƀf�[�^�������iSpan�͒ʏ�̔z�񂩂�쐬�j
        for ( int i = 0; i < ElementCount; i++ )
        {
            regularArray[i] = i;
        }

        Measure.Method(() =>
        {
            Span<int> span = regularArray.AsSpan();
            long sum = 0;
            // ��葽���̌J��Ԃ� (��: 10��) �Ōv���l������
            for ( int i = 0; i < span.Length; i++ )
            {
                sum += span[i];
            }
            spanSum = sum; // ���ϒl��ۑ�
        })
        .WarmupCount(WarmupCount)
        .MeasurementCount(MeasurementCount)
        .IterationsPerMeasurement(IterationsPerMeasurement)
        .Run();
    }

    [Test, Performance]
    public void SequentialAccess_Dictionary()
    {
        // ����O�Ƀf�[�^������
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
            // �L�^�p�t�B�[���h�ϐ��ɋL�^�B
            // �R���p�C���̉ߏ�œK���\�h�����˂Ēl���g�p���A�Ō�ɍ��v�l�̐��x�`�F�b�N�Ɏg�p�B
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

    #region Job�V�X�e���̃e�X�g

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
        // ����O�Ƀf�[�^������
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

            // �W���u�����s
            job.Schedule().Complete();

            // ���x�`�F�b�N�̂��߂ɍ��v�l���擾
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

            // �L�[�����O�ɏ���
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

            // �L�[�����O�ɏ���
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

            // �L�[�����O�ɏ���
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
    //    // ����O�Ƀf�[�^������
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

    #region Job�\���̂̒�`

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
                // �|�C���^����l��ǂݎ���č��v
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
            // �e�X���b�h����̃A�N�Z�X�𓯊����邽�߂ɃA�g�~�b�N���Z���g�p
            // NativeArray�̒l�����ʔz��ɑ����Ă���
            for ( int i = 0; i < Input.Length; i++ )
            {
                Result[0] += Input[i];
            }
        }
    }

    #endregion
}