using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.PerformanceTesting;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using System;
using static JTestAIBase;
using static CombatManager;
using static JobAITestStatus;
using static AiFunctionLibrary;
using Unity.Burst;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

/// <summary>
/// AITestJob�̃p�t�H�[�}���X�e�X�g
/// </summary>
public class AIPerformanceTests
{
    // �e�X�g�p�̃f�[�^
    private UnsafeList<CharacterData> _characterData;
    private UnsafeList<MovementInfo> _judgeResultJob;
    private UnsafeList<MovementInfo> _judgeResultNonJob;
    private NativeArray<NativeHashMap<int, int>> _teamHate;
    private NativeArray<int> _relationMap;
    //private NativeArray<FunctionPointer<SkipJudgeDelegate>> _skipFunctions;
    //private NativeArray<FunctionPointer<TargetJudgeDelegate>> _targetFunctions;

    /// <summary>
    /// 
    /// </summary>
    private JTestAIBase[] characters;

    /// <summary>
    /// �����I�u�W�F�N�g�̔z��B
    /// </summary>
    private string[] types = new string[] { "Assets/Prefab/JobAI/TypeA.prefab", "Assets/Prefab/JobAI/TypeB.prefab", "Assets/Prefab/JobAI/TypeC.prefab" };

    // �e�X�g�p�̃p�����[�^
    private int _characterCount = 1000;
    private float _nowTime = 10.0f;

    // AI�e�X�g�p�̃C���X�^���X
    private AITestJob _aiTestJob;
    private NonJobAI _nonJobAI;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // �e�X�g�f�[�^�̏�����
        await InitializeTestData();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        // ���������\�[�X�̉��
        DisposeTestData();
    }

    /// <summary>
    /// �e�X�g�f�[�^�̏�����
    /// </summary>
    private async UniTask InitializeTestData()
    {
        // UnsafeList�̏�����
        _characterData = new UnsafeList<CharacterData>(_characterCount, Allocator.Persistent);
        _characterData.Resize(_characterCount, NativeArrayOptions.ClearMemory);

        _judgeResultJob = new UnsafeList<MovementInfo>(_characterCount, Allocator.Persistent);
        _judgeResultJob.Resize(_characterCount, NativeArrayOptions.ClearMemory);

        _judgeResultNonJob = new UnsafeList<MovementInfo>(_characterCount, Allocator.Persistent);
        _judgeResultNonJob.Resize(_characterCount, NativeArrayOptions.ClearMemory);

        // �`�[�����Ƃ̃w�C�g�}�b�v��������
        _teamHate = new NativeArray<NativeHashMap<int, int>>(Enum.GetValues(typeof(CharacterSide)).Length, Allocator.Persistent);
        for ( int i = 0; i < _teamHate.Length; i++ )
        {
            _teamHate[i] = new NativeHashMap<int, int>(_characterCount, Allocator.Persistent);
        }

        // �w�c�֌W�}�b�v��������
        _relationMap = new NativeArray<int>(Enum.GetValues(typeof(CharacterSide)).Length, Allocator.Persistent);
        for ( int i = 0; i < _relationMap.Length; i++ )
        {
            // �v���C���[�͓G�ɓG�΁A�G�̓v���C���[�ɓG�΁A���͒����Ȃ�
            switch ( (CharacterSide)i )
            {
                case CharacterSide.�v���C���[:
                    _relationMap[i] = 1 << (int)CharacterSide.����;  // �v���C���[�͓G�ɓG��
                    break;
                case CharacterSide.����:
                    _relationMap[i] = 1 << (int)CharacterSide.�v���C���[;  // �G�̓v���C���[�ɓG��
                    break;
                case CharacterSide.���̑�:
                default:
                    _relationMap[i] = 0;  // �����͒N�ɂ��G�΂��Ȃ�
                    break;
            }
        }

        // �L�����N�^�[�f�[�^�̏�����
        await InitializeCharacterData();

        // AI�C���X�^���X�̏�����
        InitializeAIInstances();
    }

    /// <summary>
    /// AI�C���X�^���X�̏�����
    /// </summary>
    private void InitializeAIInstances()
    {
        // AITestJob�̏�����
        _aiTestJob = new AITestJob
        {
            teamHate = _teamHate,
            characterData = _characterData,
            nowTime = _nowTime,
            judgeResult = _judgeResultJob,
            relationMap = _relationMap,
            skipFunctions = AiFunctionLibrary.skipFunctions,
            targetFunctions = AiFunctionLibrary.targetFunctions
        };

        // NonJobAI�̏�����
        _nonJobAI = new NonJobAI
        {
            teamHate = _teamHate,
            characterData = _characterData,
            nowTime = _nowTime,
            judgeResult = _judgeResultNonJob,
            relationMap = _relationMap,
            skipFunctions = AiFunctionLibrary.skipFunctions,
            targetFunctions = AiFunctionLibrary.targetFunctions
        };
    }

    /// <summary>
    /// �L�����N�^�[�f�[�^�̏�����
    /// </summary>
    private async UniTask InitializeCharacterData()
    {
        // �����̃I�u�W�F�N�g�����ŃC���X�^���X��
        var tasks = new List<Task<GameObject>>();
        var instantiatedObjects = new List<GameObject>();

        for ( int i = 0; i < _characterCount; i++ )
        {
            // Addressables���g�p���ăC���X�^���X���^�X�N���쐬���A���X�g�ɒǉ�
            var task = Addressables.InstantiateAsync(types[i % 3]).Task
                .ContinueWith(t =>
                {
                    // ����������GameObject���擾
                    return t.Result.gameObject;
                });

            tasks.Add(task);
        }

        // ���ׂẴC���X�^���X���^�X�N����������̂�ҋ@
        var results = await Task.WhenAll(tasks);

        // �C���X�^���X�����ꂽ�I�u�W�F�N�g��ۑ�
        instantiatedObjects.AddRange(results);

        // �L�����f�[�^�쐬
        for ( int i = 0; i < instantiatedObjects.Count; i++ )
        {
            _characterData.Add(instantiatedObjects[i].GetComponent<JTestAIBase>().MakeTestData());

            // �w�C�g�}�b�v��������
            int teamNum = (int)_characterData[i].liveData.belong;
            _teamHate[teamNum].Add(_characterData[i].hashCode, 10);
        }

        // �L�����N�^�[�f�[�^�̃X�e�[�^�X�������_����
        // �X��CharacterData����������`�ɕύX
        for ( int i = 0; i < _characterData.Length; i++ )
        {
            CharacterData data = _characterData[i];
            CharacterDataRandomizer.RandomizeCharacterData(ref data, _characterData);
            _characterData[i] = data;
        }
    }

    /// <summary>
    /// �e�X�g�f�[�^�̃��������
    /// </summary>
    private void DisposeTestData()
    {
        // UnsafeList�̉��
        if ( _characterData.IsCreated )
        {
            // �e�L�����N�^�[�f�[�^���̃l�C�e�B�u�R���e�i�����
            for ( int i = 0; i < _characterData.Length; i++ )
            {
                CharacterData data = _characterData[i];
                data.Dispose();
            }

            _characterData.Dispose();
        }

        if ( _judgeResultJob.IsCreated )
            _judgeResultJob.Dispose();

        if ( _judgeResultNonJob.IsCreated )
            _judgeResultNonJob.Dispose();

        // �`�[���w�C�g�}�b�v�̉��
        if ( _teamHate.IsCreated )
        {
            for ( int i = 0; i < _teamHate.Length; i++ )
            {
                if ( _teamHate[i].IsCreated )
                    _teamHate[i].Dispose();
            }

            _teamHate.Dispose();
        }

        // ���̑���NativeArray�̉��
        if ( _relationMap.IsCreated )
            _relationMap.Dispose();
    }

    /// <summary>
    /// �e�e�X�g���s�O�̃X�e�[�^�X�����_����
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        // �L�����N�^�[�f�[�^�̃X�e�[�^�X�������_����
        // �X��CharacterData����������`�ɕύX
        for ( int i = 0; i < _characterData.Length; i++ )
        {
            CharacterData data = _characterData[i];
            CharacterDataRandomizer.RandomizeCharacterData(ref data, _characterData);
            _characterData[i] = data;
        }

        // AI�C���X�^���X�̎��Ԃ��X�V
        _aiTestJob.nowTime = _nowTime;
        _nonJobAI.nowTime = _nowTime;
    }

    /// <summary>
    /// ��JobSystem��AI�����p�t�H�[�}���X�e�X�g
    /// </summary>
    [Test, Performance]
    public void NonJobAI_Performance_Test()
    {
        Measure.Method(() =>
        {
            // ��JobSystem��AI�������s
            _nonJobAI.ExecuteAIDecision();
        })
        .WarmupCount(3)       // �E�H�[���A�b�v��
        .MeasurementCount(10) // �v����
        .IterationsPerMeasurement(1) // 1��̌v��������̎��s��
        .GC()                 // GC�̌v�����s��
        .Run();
    }

    /// <summary>
    /// JobSystem��AI�����p�t�H�[�}���X�e�X�g
    /// </summary>
    [Test, Performance]
    public void JobSystemAI_Performance_Test()
    {
        Measure.Method(() =>
        {
            // JobSystem��AI�������s
            JobHandle handle = _aiTestJob.Schedule(_characterCount, 64);
            handle.Complete();
        })
        .WarmupCount(3)       // �E�H�[���A�b�v��
        .MeasurementCount(10) // �v����
        .IterationsPerMeasurement(1) // 1��̌v��������̎��s��
        .GC()                 // GC�̌v�����s��
        .Run();
    }

    /// <summary>
    /// �قȂ�L�����N�^�[���ł̃p�t�H�[�}���X��r�e�X�g
    /// </summary>
    [UnityTest, Performance]
    public IEnumerator Compare_Different_Character_Counts()
    {
        // �e�X�g�p�̃L�����N�^�[���̔z��
        int[] characterCounts = { 100, 500, 1000, 5000, 10000 };

        foreach ( int count in characterCounts )
        {
            // �e�X�g�P�[�X����ݒ�
            using ( Measure.Scope($"Character Count: {count}") )
            {
                // �L�����N�^�[���̍X�V�ƍď�����
                UniTask recreateTask = RecreateTestData(count);

                // UniTask�̊�����ҋ@
                while ( !recreateTask.Status.IsCompleted() )
                {
                    yield return null;
                }

                // ��JobSystem�e�X�g
                using ( Measure.Scope("NonJobAI") )
                {
                    _nonJobAI.ExecuteAIDecision();
                }

                // �t���[���X�L�b�v
                yield return null;

                // JobSystem�e�X�g
                using ( Measure.Scope("JobSystemAI") )
                {
                    JobHandle handle = _aiTestJob.Schedule(count, 64);
                    handle.Complete();
                }
            }

            // ���̃e�X�g�̑O�Ƀt���[�����X�L�b�v
            yield return null;
        }
    }

    /// <summary>
    /// �e�X�g�f�[�^�̍č쐬�i�L�����N�^�[���ύX���j
    /// </summary>
    private async UniTask RecreateTestData(int newCharacterCount)
    {
        // ���݂̃f�[�^�����
        DisposeTestData();

        // �L�����N�^�[�����X�V
        _characterCount = newCharacterCount;

        // �V�����f�[�^�����������Ċ�����ҋ@
        await InitializeTestData();
    }

    /// <summary>
    /// ���ʂ̌��؃e�X�g�i���������������ʂ��o�����m�F�j
    /// </summary>
    [Test]
    public void Verify_Results_Are_Same()
    {
        // �f�[�^�������_����
        // �X��CharacterData����������`�ɕύX
        for ( int i = 0; i < _characterData.Length; i++ )
        {
            CharacterData data = _characterData[i];
            CharacterDataRandomizer.RandomizeCharacterData(ref data, _characterData);
            _characterData[i] = data;
        }

        // ������AI���������s
        _nonJobAI.ExecuteAIDecision();

        JobHandle handle = _aiTestJob.Schedule(_characterCount, 64);
        handle.Complete();

        // ���ʂ��r
        bool resultsMatch = true;
        string mismatchInfo = "";

        for ( int i = 0; i < _characterCount; i++ )
        {
            MovementInfo nonJobResult = _judgeResultNonJob[i];
            MovementInfo jobResult = _judgeResultJob[i];

            // ���ʂ���v���邩�m�F
            if ( nonJobResult.result != jobResult.result ||
                nonJobResult.actNum != jobResult.actNum ||
                nonJobResult.targetHash != jobResult.targetHash )
            {
                resultsMatch = false;
                mismatchInfo = $"Mismatch at index {i}: NonJob({nonJobResult.result}, {nonJobResult.actNum}, {nonJobResult.targetHash}) " +
                               $"vs Job({jobResult.result}, {jobResult.actNum}, {jobResult.targetHash})";
                break;
            }
        }

        // ����
        Assert.IsTrue(resultsMatch, mismatchInfo);
    }
}