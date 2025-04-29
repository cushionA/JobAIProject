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
/// �l�X�ȃl�C�e�B�u�R���e�i�ƃ}�l�[�W�h�R���N�V�����̃p�t�H�[�}���X��r�e�X�g
/// - �A���A�N�Z�X���x�̔�r
/// - Job�V�X�e���ł̎��s���x��r
/// </summary>
public class NativeContainerPerformanceTest
{
    // �e�X�g�̗v�f��
    private const int ElementCount = 80;

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
        this.dictionary = new Dictionary<int, int>(ElementCount);
        this.list = new List<int>(ElementCount);
        this.regularArray = new int[ElementCount]; // �ʏ�̔z��̏�����

        // �l�C�e�B�u�R���e�i�̏�����
        this.nativeList = new NativeList<int>(ElementCount, Allocator.Persistent);
        this.unsafeList = new UnsafeList<int>(ElementCount, Allocator.Persistent);
        this.parallelHashMap = new NativeParallelHashMap<int, int>(ElementCount, Allocator.Persistent);
        this.nativeHashMap = new NativeHashMap<int, int>(ElementCount, Allocator.Persistent);
        this.unsafeHashMap = new UnsafeHashMap<int, int>(ElementCount, Allocator.Persistent);
        this.nativeArray = new NativeArray<int>(ElementCount, Allocator.Persistent);

        // UnsafePtrList�p�̏�����
        this.unsafePtrList = new UnsafePtrList<int>(ElementCount, Allocator.Persistent);
        this.valueHolders = new NativeArray<IntPtr>(ElementCount, Allocator.Persistent);

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
                this.valueHolders[i] = ptr;
                // UnsafePtrList�ɒǉ�
                this.unsafePtrList.Add((int*)ptr);
            }
        }

        Debug.Log($"Setup complete with {ElementCount} elements capacity");
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        // ���m���̌��� - �e�R���N�V�����̍��v�l��NativeArray�̌��ʂƔ�r
        Debug.Log("���m�����،���:");

        if ( this.nativeArraySum == 0 )
        {
            Debug.Log("NativeArray�̍��v�l��0�̂��߁A���؂��X�L�b�v���܂��B�A���A�N�Z�X�e�X�g�����s���Ă��������B");
        }
        else
        {
            long expectedSum = (long)ElementCount * (ElementCount - 1) / 2; // ���_��̍��v�l: 0+1+2+...+(n-1)

            Debug.Log($"���_��̍��v�l: {expectedSum}");

            if ( this.dictionarySum != this.nativeArraySum )
            {
                Debug.LogWarning($"�s��v: Dictionary={this.dictionarySum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.listSum != this.nativeArraySum )
            {
                Debug.LogWarning($"�s��v: List={this.listSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.nativeListSum != this.nativeArraySum )
            {
                Debug.LogWarning($"�s��v: NativeList={this.nativeListSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.unsafeListSum != this.nativeArraySum )
            {
                Debug.LogWarning($"�s��v: UnsafeList={this.unsafeListSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.unsafePtrListSum != this.nativeArraySum )
            {
                Debug.LogWarning($"�s��v: UnsafePtrList={this.unsafePtrListSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.parallelHashMapSum != this.nativeArraySum )
            {
                Debug.LogWarning($"�s��v: ParallelHashMap={this.parallelHashMapSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.nativeHashMapSum != this.nativeArraySum )
            {
                Debug.LogWarning($"�s��v: NativeHashMap={this.nativeHashMapSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.unsafeHashMapSum != this.nativeArraySum )
            {
                Debug.LogWarning($"�s��v: UnsafeHashMap={this.unsafeHashMapSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.nativeArrayJobSum != this.nativeArraySum )
            {
                Debug.LogWarning($"�s��v: (Job)NativeArray={this.nativeArrayJobSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.nativeListJobSum != this.nativeArraySum )
            {
                Debug.LogWarning($"�s��v: (Job)NativeList={this.nativeListJobSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.unsafeListJobSum != this.nativeArraySum )
            {
                Debug.LogWarning($"�s��v: (Job)UnsafeList={this.unsafeListJobSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.unsafePtrListJobSum != 0 && this.unsafePtrListJobSum != this.nativeArraySum )
            {
                Debug.LogWarning($"�s��v: (Job)UnsafePtrList={this.unsafePtrListJobSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.parallelHashMapJobSum != this.nativeArraySum )
            {
                Debug.LogWarning($"�s��v: (Job)ParallelHashMap={this.parallelHashMapJobSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.nativeHashMapJobSum != this.nativeArraySum )
            {
                Debug.LogWarning($"�s��v: (Job)NativeHashMap={this.nativeHashMapJobSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.unsafeHashMapJobSum != this.nativeArraySum )
            {
                Debug.LogWarning($"�s��v: (Job)UnsafeHashMap={this.unsafeHashMapJobSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.regularArraySum != this.nativeArraySum )
            {
                Debug.LogWarning($"�s��v: RegularArray={this.regularArraySum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.spanSum != this.nativeArraySum )
            {
                Debug.LogWarning($"�s��v: Span={this.spanSum}, NativeArray={this.nativeArraySum}");
            }

            if ( this.nativeArraySum != expectedSum )
            {
                Debug.LogWarning($"�s��v: NativeArray={this.nativeArraySum}, ���_�l={expectedSum}");
            }
            else
            {
                Debug.Log($"��v: NativeArray�̍��v�l {this.nativeArraySum} �͗��_�l�ƈ�v���Ă��܂�");
            }
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

        if ( this.nativeArray.IsCreated )
        {
            this.nativeArray.Dispose();
        }

        Debug.Log("All resources disposed");
    }

    #region �A���A�N�Z�X�e�X�g

    [Test, Performance]
    public unsafe void SequentialAccess_UnsafePtrList()
    {
        Measure.Method(() =>
        {
            long sum = 0;
            for ( int i = 0; i < this.unsafePtrList.Length; i++ )
            {
                // �|�C���^����l���擾���č��v
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
        // ����O�Ƀf�[�^������
        for ( int i = 0; i < ElementCount; i++ )
        {
            this.regularArray[i] = i;
        }

        Measure.Method(() =>
        {
            long sum = 0;
            // ��葽���̌J��Ԃ� (��: 10��) �Ōv���l������
            for ( int i = 0; i < this.regularArray.Length; i++ )
            {
                sum += this.regularArray[i];
            }

            this.regularArraySum = sum; // ���ϒl��ۑ�
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
            this.regularArray[i] = i;
        }

        Measure.Method(() =>
        {
            Span<int> span = this.regularArray.AsSpan();
            long sum = 0;
            // ��葽���̌J��Ԃ� (��: 10��) �Ōv���l������
            for ( int i = 0; i < span.Length; i++ )
            {
                sum += span[i];
            }

            this.spanSum = sum; // ���ϒl��ۑ�
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
            // �L�^�p�t�B�[���h�ϐ��ɋL�^�B
            // �R���p�C���̉ߏ�œK���\�h�����˂Ēl���g�p���A�Ō�ɍ��v�l�̐��x�`�F�b�N�Ɏg�p�B
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

    #region Job�V�X�e���̃e�X�g

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
        // ����O�Ƀf�[�^������
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

            // �W���u�����s
            job.Schedule().Complete();

            // ���x�`�F�b�N�̂��߂ɍ��v�l���擾
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

            // �L�[�����O�ɏ���
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

            // �L�[�����O�ɏ���
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

            // �L�[�����O�ɏ���
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
            for ( int i = 0; i < this.Input.Length; i++ )
            {
                // �|�C���^����l��ǂݎ���č��v
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
            // �e�X���b�h����̃A�N�Z�X�𓯊����邽�߂ɃA�g�~�b�N���Z���g�p
            // NativeArray�̒l�����ʔz��ɑ����Ă���
            for ( int i = 0; i < this.Input.Length; i++ )
            {
                this.Result[0] += this.Input[i];
            }
        }
    }

    #endregion
}