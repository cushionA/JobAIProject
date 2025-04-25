using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.PerformanceTesting;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using static JTestAIBase;
using static CombatManager;
using static JobAITestStatus;
using static AiFunctionLibrary;
using Unity.Burst;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Unity.Mathematics;
using System.Text;
using UnityEngine.ResourceManagement.ResourceLocations;

/// <summary>
/// AITestJob�̃p�t�H�[�}���X�e�X�g
/// </summary>
public class AIPerformanceTests
{
    // �e�X�g�p�̃f�[�^
    private UnsafeList<CharacterData> _characterData;
    private UnsafeList<MovementInfo> _judgeResultJob;
    private UnsafeList<MovementInfo> _judgeResultNonJob;
    private List<MovementInfo> _judgeResultStandard; // StandardAI�p�̌��ʃ��X�g
    private NativeHashMap<int2, int> _teamHate;
    private NativeArray<int> _relationMap;

    // ��������Ԃ�ǐՂ���t���O
    private bool _dataInitialized = false;
    private bool _charactersInitialized = false;
    private bool _aiInstancesInitialized = false;

    int jobBatchCount = 50;

    /// <summary>
    /// 
    /// </summary>
    private JTestAIBase[] characters;

    /// <summary>
    /// �����I�u�W�F�N�g�̔z��B
    /// </summary>
    private string[] types = new string[] { "Assets/Prefab/JobAI/TypeA.prefab", "Assets/Prefab/JobAI/TypeB.prefab", "Assets/Prefab/JobAI/TypeC.prefab" };

    // �e�X�g�p�̃p�����[�^
    private int _characterCount = 18;
    private float _nowTime = 100.0f;

    // AI�e�X�g�p�̃C���X�^���X
    private AITestJob _aiTestJob;
    private NonJobAI _nonJobAI;
    private StandardAI _standardAI; // �W���R���N�V�������g�p����AI

    [UnitySetUp]
    public IEnumerator OneTimeSetUp()
    {
        Debug.Log("�J�n: OneTimeSetUp");

        // �e�X�g�f�[�^�̏�����
        try
        {
            InitializeTestData();
            Debug.Log($"�e�X�g�f�[�^�̏���������: teamHate.IsCreated={_teamHate.IsCreated}, Length={_teamHate.Count}");
        }
        catch ( Exception ex )
        {
            Debug.LogError($"�e�X�g�f�[�^���������̃G���[: {ex.Message}\n{ex.StackTrace}");
            yield break;
        }

        // �L�����N�^�[�f�[�^�̏����� - IEnumerator�Ȃ̂� yield return����
        yield return InitializeCharacterData();

        if ( !_charactersInitialized )
        {
            Debug.LogError("�L�����N�^�[�f�[�^�̏������Ɏ��s���܂���");
            yield break;
        }

        Debug.Log($"�L�����N�^�[�f�[�^�̏���������: characterData.Length={_characterData.Length}");

        // teamHate�̒��g���m�F
        for ( int i = 0; i < _teamHate.Count; i++ )
        {
            Debug.Log($"teamHate[{i}].Count={_teamHate.Count}, IsCreated={_teamHate.IsCreated}");
        }

        // AI�C���X�^���X�̏�����
        try
        {
            InitializeAIInstances();
            Debug.Log("AI�C���X�^���X�̏���������");
        }
        catch ( Exception ex )
        {
            Debug.LogError($"AI�C���X�^���X���������̃G���[: {ex.Message}\n{ex.StackTrace}");
            yield break;
        }

        Debug.Log("����: OneTimeSetUp");
    }

    [TearDown]
    public void OneTimeTearDown()
    {
        Debug.Log("�J�n: OneTimeTearDown");
        // ���������\�[�X�̉��
        DisposeTestData();
        Debug.Log("����: OneTimeTearDown");
    }

    /// <summary>
    /// �e�X�g�f�[�^�̏�����
    /// </summary>
    private void InitializeTestData()
    {
        Debug.Log($"�J�n: InitializeTestData (CharacterCount={_characterCount})");
        try
        {
            // UnsafeList�̏�����
            _characterData = new UnsafeList<CharacterData>(_characterCount, Allocator.Persistent);
            _characterData.Resize(_characterCount, NativeArrayOptions.ClearMemory);
            Debug.Log($"_characterData����������: Length={_characterData.Length}, IsCreated={_characterData.IsCreated}");

            _judgeResultJob = new UnsafeList<MovementInfo>(_characterCount, Allocator.Persistent);
            _judgeResultJob.Resize(_characterCount, NativeArrayOptions.ClearMemory);

            _judgeResultNonJob = new UnsafeList<MovementInfo>(_characterCount, Allocator.Persistent);
            _judgeResultNonJob.Resize(_characterCount, NativeArrayOptions.ClearMemory);

            // StandardAI�p�̌��ʃ��X�g��������
            _judgeResultStandard = new List<MovementInfo>(_characterCount);
            for ( int i = 0; i < _characterCount; i++ )
            {
                _judgeResultStandard.Add(new MovementInfo());
            }

            // �`�[�����Ƃ̃w�C�g�}�b�v��������
            _teamHate = new NativeHashMap<int2, int>(3, Allocator.Persistent);
            Debug.Log($"_teamHate�z�񏉊�������: Length={_teamHate.Count}, IsCreated={_teamHate.IsCreated}");

            // �w�c�֌W�}�b�v��������
            _relationMap = new NativeArray<int>(3, Allocator.Persistent);

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

            _dataInitialized = true;
        }
        catch ( Exception ex )
        {
            Debug.LogError($"InitializeTestData�ł̃G���[: {ex.Message}\n{ex.StackTrace}");
            _dataInitialized = false;
        }
        Debug.Log("����: InitializeTestData");
    }

    /// <summary>
    /// AI�C���X�^���X�̏�����
    /// </summary>
    private void InitializeAIInstances()
    {
        Debug.Log("�J�n: InitializeAIInstances");
        try
        {
            // �e�R���e�i�̏�Ԋm�F
            if ( !_teamHate.IsCreated )
            {
                Debug.LogError("teamHate������������Ă��܂���");
                return;
            }

            if ( !_characterData.IsCreated )
            {
                Debug.LogError("characterData������������Ă��܂���");
                return;
            }

            if ( !_relationMap.IsCreated )
            {
                Debug.LogError("relationMap������������Ă��܂���");
                return;
            }

            // �`�[���w�C�g�̊e�v�f���m�F
            for ( int i = 0; i < _teamHate.Count; i++ )
            {
                if ( !_teamHate.IsCreated )
                {
                    Debug.LogError($"teamHate[{i}]������������Ă��܂���");
                    return;
                }
                Debug.Log($"teamHate[{i}].Count={_teamHate.Count}, IsCreated={_teamHate.IsCreated}");
            }

            // AITestJob�̏�����
            _aiTestJob = new AITestJob
            {
                teamHate = _teamHate,
                characterData = _characterData,
                nowTime = _nowTime,
                judgeResult = _judgeResultJob,
                relationMap = _relationMap,
            };

            // NonJobAI�̏�����
            _nonJobAI = new NonJobAI
            {
                teamHate = _teamHate,
                characterData = _characterData,
                nowTime = _nowTime,
                judgeResult = _judgeResultNonJob,
                relationMap = _relationMap
            };

            // StandardAI�̏������iNativeContainer����f�[�^���R�s�[�j
            _standardAI = new StandardAI(_teamHate, _characterData, _nowTime, _relationMap);
            _standardAI.judgeResult = _judgeResultStandard;

            _aiInstancesInitialized = true;
        }
        catch ( Exception ex )
        {
            Debug.LogError($"InitializeAIInstances�ł̃G���[: {ex.Message}\n{ex.StackTrace}");
            _aiInstancesInitialized = false;
        }
        Debug.Log($"����: InitializeAIInstances (����={_aiInstancesInitialized})");
    }

    /// <summary>
    /// �L�����N�^�[�f�[�^�̏�����
    /// </summary>
    private IEnumerator InitializeCharacterData()
    {
        Debug.Log($"�J�n: InitializeCharacterData (CharacterCount={_characterCount})");

        // _characterData������������Ă��邩�m�F
        if ( !_characterData.IsCreated )
        {
            Debug.LogError("_characterData������������Ă��܂���");
            _charactersInitialized = false;
            yield break;
        }

        // _teamHate������������Ă��邩�m�F
        if ( !_teamHate.IsCreated )
        {
            Debug.LogError("_teamHate������������Ă��܂���");
            _charactersInitialized = false;
            yield break;
        }

        // �v���n�u�̑��݊m�F
        bool allPrefabsValid = true;
        for ( int i = 0; i < types.Length; i++ )
        {
            AsyncOperationHandle<IList<IResourceLocation>> checkOp = default;
            checkOp = Addressables.LoadResourceLocationsAsync(types[i]);
            yield return checkOp;

            if ( checkOp.Result.Count == 0 )
            {
                Debug.LogError($"�v���n�u��������܂���: {types[i]}");
                allPrefabsValid = false;
            }
            else
            {
                Debug.Log($"�v���n�u���m�F: {types[i]}");
            }
        }

        if ( !allPrefabsValid )
        {
            Debug.LogError("�ꕔ�̃v���n�u��������܂���ł���");
            _charactersInitialized = false;
            yield break;
        }

        // �����̃I�u�W�F�N�g�����ŃC���X�^���X��
        var tasks = new List<AsyncOperationHandle<GameObject>>(_characterCount);

        for ( int i = 0; i < _characterCount; i++ )
        {
            // Addressables���g�p���ăC���X�^���X��
            var task = Addressables.InstantiateAsync(types[i % 3]);
            tasks.Add(task);

            // 100���ƂɃt���[���X�L�b�v�i�p�t�H�[�}���X�΍�j
            if ( i % 100 == 0 && i > 0 )
            {
                yield return null;
            }
        }

        Debug.Log($"�C���X�^���X�����N�G�X�g����: {tasks.Count}�� {_characterCount}");

        // ���ׂẴI�u�W�F�N�g�����������̂�҂�
        bool allTasksCompleted = true;
        foreach ( var task in tasks )
        {
            yield return task;
            if ( task.Status != AsyncOperationStatus.Succeeded )
            {
                allTasksCompleted = false;
                Debug.LogError("�I�u�W�F�N�g�̃C���X�^���X���Ɏ��s���܂���");
            }
        }

        if ( !allTasksCompleted )
        {
            Debug.LogError("�ꕔ�̃I�u�W�F�N�g�̃C���X�^���X���Ɏ��s���܂���");
            _charactersInitialized = false;
            yield break;
        }

        Debug.Log("�S�I�u�W�F�N�g�̃C���X�^���X������");

        // �`�F�b�N�|�C���g�F_teamHate�̏�Ԃ��m�F
        for ( int i = 0; i < _teamHate.Count; i++ )
        {
            Debug.Log($"�L�����������_teamHate[{i}]: IsCreated={_teamHate.IsCreated}, Count={_teamHate.Count}");
        }

        // �������ꂽ�I�u�W�F�N�g�ƕK�v�ȃR���|�[�l���g���擾
        int successCount = 0;

        for ( int i = 0; i < tasks.Count; i++ )
        {
            GameObject obj = null;
            try
            {
                obj = tasks[i].Result;
            }
            catch ( Exception ex )
            {
                Debug.LogError($"�I�u�W�F�N�g�擾���ɃG���[: {ex.Message}");
                continue;
            }

            if ( obj == null )
            {
                Debug.LogError($"�C���f�b�N�X{i}�̃I�u�W�F�N�g��null�ł�");
                continue;
            }

            var aiComponent = obj.GetComponent<JTestAIBase>();
            if ( aiComponent == null )
            {
                Debug.LogError($"JTestAIBase�R���|�[�l���g��������܂���: {obj.name}");
                continue;
            }

            CharacterData data = default;
            try
            {
                data = aiComponent.MakeTestData();
                _characterData[i] = data;
            }
            catch ( Exception ex )
            {
                Debug.LogError($"�f�[�^�������ɃG���[: {ex.Message}");
                continue;
            }

            successCount++;

            // �w�C�g�}�b�v��������
            int teamNum = (int)data.liveData.belong;

            if ( !_teamHate.IsCreated )
            {
                Debug.LogError($"_teamHate[{teamNum}]�������ł�");
                continue;
            }

            int2 hateKey = new int2(teamNum, data.hashCode);

            try
            {
                if ( _teamHate.ContainsKey(hateKey) )
                {
                    Debug.LogWarning($"�d������hashCode: {data.hashCode} (�`�[��: {teamNum})");
                    continue;
                }

                _teamHate.Add(hateKey, 10);
            }
            catch ( Exception ex )
            {
                Debug.LogError($"�w�C�g�}�b�v�X�V���ɃG���[: {ex.Message}");
                continue;
            }

            // 100���ƂɃ��O
            if ( i % 100 == 0 )
            {
                Debug.Log($"�L�����N�^�[�������i��: {i}/{tasks.Count}, teamHate[{teamNum}].Count={_teamHate.Count}");
            }
        }

        Debug.Log($"�L�����N�^�[�f�[�^����������: ������={successCount}/{tasks.Count}");

        // �eteamHate�̍ŏI��Ԃ��m�F
        for ( int i = 0; i < _teamHate.Count; i++ )
        {
            Debug.Log($"�ŏI_teamHate[{i}]: IsCreated={_teamHate.IsCreated}, Count={_teamHate.Count}");
        }

        // �L�����N�^�[�f�[�^�̃X�e�[�^�X�������_����
        Debug.Log("�L�����N�^�[�f�[�^�̃����_�������J�n");

        for ( int i = 0; i < _characterData.Length; i++ )
        {
            CharacterData data = _characterData[i];

            try
            {
                CharacterDataRandomizer.RandomizeCharacterData(ref data, _characterData);
                _characterData[i] = data;
            }
            catch ( Exception ex )
            {
                Debug.LogError($"�f�[�^�����_�������ɃG���[ (index={i}): {ex.Message}");
            }

            // 100���ƂɃt���[���X�L�b�v
            if ( i % 100 == 0 && i > 0 )
            {
                yield return null;
            }
        }

        Debug.Log("�L�����N�^�[�f�[�^�̃����_��������");

        _charactersInitialized = successCount > 0;
        Debug.Log($"����: InitializeCharacterData (����={_charactersInitialized})");
    }

    public static void DebugPrintCharacterData(CharacterData data)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("===== CharacterData�ڍ׏�� =====");
        sb.AppendLine($"�n�b�V���R�[�h: {data.hashCode}");

        // ��{���
        sb.AppendLine($"�ŏI���f����: {data.lastJudgeTime}");
        sb.AppendLine($"�ŏI�ړ����f����: {data.lastMoveJudgeTime}");
        sb.AppendLine($"�ړ����f�Ԋu: {data.moveJudgeInterval}");
        sb.AppendLine($"�^�[�Q�b�g��: {data.targetingCount}");

        // ���C�u�f�[�^
        sb.AppendLine("�yLiveData���z");
        sb.AppendLine($"  ���݈ʒu: {data.liveData.nowPosition}");
        sb.AppendLine($"  ����HP: {data.liveData.currentHp}/{data.liveData.maxHp}");
        sb.AppendLine($"  ����: {data.liveData.belong}");
        sb.AppendLine($"  ���: {data.liveData.actState}");
        // ����liveData�t�B�[���h���K�v�ɉ����Ēǉ�

        // BrainData���
        sb.AppendLine("�yBrainData���z");
        if ( data.brainData.IsCreated )
        {
            sb.AppendLine($"  �o�^��: {data.brainData.Count}");
            var keys = data.brainData.GetKeyArray(Allocator.Temp);
            try
            {
                foreach ( var key in keys )
                {
                    if ( data.brainData.TryGetValue(key, out var brainStatus) )
                    {
                        sb.AppendLine($"  ���[�h[{key}]:");
                        sb.AppendLine($"    ���f�Ԋu: {brainStatus.judgeInterval}");
                        // ����brainStatus�t�B�[���h���K�v�ɉ����Ēǉ�
                    }
                }
                keys.Dispose();
            }
            catch ( Exception ex )
            {
                sb.AppendLine($"  BrainData�A�N�Z�X���ɃG���[: {ex.Message}");
                if ( keys.IsCreated )
                    keys.Dispose();
            }
        }
        else
        {
            sb.AppendLine("  BrainData�͍쐬����Ă��܂���");
        }

        // �l�w�C�g���
        sb.AppendLine("�yPersonalHate���z");
        if ( data.personalHate.IsCreated )
        {
            sb.AppendLine($"  �o�^��: {data.personalHate.Count}");
            var hateKeys = data.personalHate.GetKeyArray(Allocator.Temp);
            try
            {
                foreach ( var target in hateKeys )
                {
                    if ( data.personalHate.TryGetValue(target, out var hateValue) )
                    {
                        sb.AppendLine($"  �Ώ�[{target}]: �w�C�g�l={hateValue}");
                    }
                }
                hateKeys.Dispose();
            }
            catch ( Exception ex )
            {
                sb.AppendLine($"  PersonalHate�A�N�Z�X���ɃG���[: {ex.Message}");
                if ( hateKeys.IsCreated )
                    hateKeys.Dispose();
            }
        }
        else
        {
            sb.AppendLine("  PersonalHate�͍쐬����Ă��܂���");
        }

        // �ߋ����L�����N�^�[���
        sb.AppendLine("�yShortRangeCharacter���z");
        if ( data.shortRangeCharacter.IsCreated )
        {
            sb.AppendLine($"  �o�^��: {data.shortRangeCharacter.Length}");
            try
            {
                for ( int i = 0; i < data.shortRangeCharacter.Length; i++ )
                {
                    sb.AppendLine($"  �ߋ����L����[{i}]: Hash={data.shortRangeCharacter[i]}");
                }
            }
            catch ( Exception ex )
            {
                sb.AppendLine($"  ShortRangeCharacter�A�N�Z�X���ɃG���[: {ex.Message}");
            }
        }
        else
        {
            sb.AppendLine("  ShortRangeCharacter�͍쐬����Ă��܂���");
        }

        sb.AppendLine("============================");

        // �������O�𕪊����ďo�́iUnity console�̕�������������j
        const int maxLogLength = 1000;
        for ( int i = 0; i < sb.Length; i += maxLogLength )
        {
            int length = Math.Min(maxLogLength, sb.Length - i);
            Debug.Log(sb.ToString(i, length));
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

        // StandardAI�p�̌��ʃ��X�g�͊Ǘ����ꂽ�I�u�W�F�N�g�Ȃ̂�GC����������
        _judgeResultStandard = null;

        // �`�[���w�C�g�}�b�v�̉��
        if ( _teamHate.IsCreated )
        {
            _teamHate.Dispose();
        }

        // ���̑���NativeArray�̉��
        if ( _relationMap.IsCreated )
            _relationMap.Dispose();
    }

    /// <summary>
    /// ��JobSystem��AI�����p�t�H�[�}���X�e�X�g
    /// </summary>
    [Test, Performance]
    public void NonJobAI_Performance_Test()
    {
        Debug.Log($"�e�X�g�f�[�^�̏���������: teamHate.IsCreated={_teamHate.IsCreated}, Length={_teamHate.Count}");

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
    /// StandardAI�̃p�t�H�[�}���X�e�X�g
    /// </summary>
    [Test, Performance]
    public void StandardAI_Performance_Test()
    {
        Debug.Log($"StandardAI�e�X�g�J�n: characterData.Count={_standardAI.characterData.Count}");

        Measure.Method(() =>
        {
            // StandardAI�̏������s
            _standardAI.ExecuteAIDecision();
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
        Debug.Log($"�e�X�g�f�[�^�̏���������: teamHate.IsCreated={_teamHate.IsCreated}, Length={_teamHate.Count}");

        Measure.Method(() =>
        {
            // JobSystem��AI�������s
            JobHandle handle = _aiTestJob.Schedule(_characterCount, jobBatchCount);
            handle.Complete();
        })
        .WarmupCount(3)       // �E�H�[���A�b�v��
        .MeasurementCount(10) // �v����
        .IterationsPerMeasurement(1) // 1��̌v��������̎��s��
        .GC()                 // GC�̌v�����s��
        .Run();
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
        InitializeTestData();

        Debug.Log($"�e�X�g�f�[�^�̏���������: teamHate.IsCreated={_teamHate.IsCreated}, Length={_teamHate.Count}");

        // �L�����N�^�[�f�[�^�̏�����
        await InitializeCharacterData();

        // AI�C���X�^���X�̏�����
        InitializeAIInstances();
    }

    /// <summary>
    /// ���ʂ̌��؃e�X�g�i�S�������������ʂ��o�����m�F�j
    /// </summary>
    [Test]
    public void Verify_Results_Are_Same()
    {
        // �f�[�^�������_����
        for ( int i = 0; i < _characterData.Length; i++ )
        {
            CharacterData data = _characterData[i];
            CharacterDataRandomizer.RandomizeCharacterData(ref data, _characterData);
            _characterData[i] = data;
        }

        // AI�C���X�^���X�̎��Ԃ��X�V
        _aiTestJob.nowTime = _nowTime;
        _nonJobAI.nowTime = _nowTime;
        _standardAI.nowTime = _nowTime;

        // �eAI�̏��������s
        _nonJobAI.ExecuteAIDecision();
        _standardAI.ExecuteAIDecision();

        JobHandle handle = _aiTestJob.Schedule(_characterCount, jobBatchCount);
        handle.Complete();

        // ���ʂ��r�iJob��NonJob�j
        bool jobNonJobMatch = true;
        string jobNonJobMismatchInfo = "";

        for ( int i = 0; i < _characterCount; i++ )
        {
            MovementInfo nonJobResult = _judgeResultNonJob[i];
            MovementInfo jobResult = _judgeResultJob[i];

            // ���ʂ���v���邩�m�F
            if ( nonJobResult.result != jobResult.result ||
                nonJobResult.actNum != jobResult.actNum ||
                nonJobResult.targetHash != jobResult.targetHash )
            {
                jobNonJobMatch = false;
                jobNonJobMismatchInfo = $"Job��NonJob�̕s��v(index={i}): NonJob({nonJobResult.result}, {nonJobResult.actNum}, {nonJobResult.targetHash}) " +
                                      $"vs Job({jobResult.result}, {jobResult.actNum}, {jobResult.targetHash})";
                break;
            }
        }

        // ���ʂ��r�iStandard��NonJob�j
        bool standardNonJobMatch = true;
        string standardNonJobMismatchInfo = "";

        for ( int i = 0; i < _characterCount; i++ )
        {
            MovementInfo nonJobResult = _judgeResultNonJob[i];
            MovementInfo standardResult = _judgeResultStandard[i];

            // ���ʂ���v���邩�m�F
            if ( nonJobResult.result != standardResult.result ||
                nonJobResult.actNum != standardResult.actNum ||
                nonJobResult.targetHash != standardResult.targetHash )
            {
                standardNonJobMatch = false;
                standardNonJobMismatchInfo = $"Standard��NonJob�̕s��v(index={i}): NonJob({nonJobResult.result}, {nonJobResult.actNum}, {nonJobResult.targetHash}) " +
                                           $"vs Standard({standardResult.result}, {standardResult.actNum}, {standardResult.targetHash})";
                break;
            }
        }

        // ����
        Assert.IsTrue(jobNonJobMatch, jobNonJobMismatchInfo);
        Assert.IsTrue(standardNonJobMatch, standardNonJobMismatchInfo);
    }

    /// <summary>
    /// 3��ނ�AI�������r���؂���e�X�g
    /// </summary>
//    [Test, Performance]
    public void Compare_Three_AI_Implementations()
    {
        // �f�[�^�������_����
        for ( int i = 0; i < _characterData.Length; i++ )
        {
            CharacterData data = _characterData[i];
            CharacterDataRandomizer.RandomizeCharacterData(ref data, _characterData);
            _characterData[i] = data;
        }

        // ���Ԃ��X�V�i�SAI�ɓ������Ԃ�ݒ�j
        float testTime = 200.0f; // �e�X�g�p�̎���
        _aiTestJob.nowTime = testTime;
        _nonJobAI.nowTime = testTime;
        _standardAI.nowTime = testTime;

        // �eAI�̏��������s���A�p�t�H�[�}���X�𑪒�
        using ( Measure.Scope("JobSystemAI���s����") )
        {
            JobHandle handle = _aiTestJob.Schedule(_characterCount, jobBatchCount);
            handle.Complete();
        }

        using ( Measure.Scope("NonJobAI���s����") )
        {
            _nonJobAI.ExecuteAIDecision();
        }

        using ( Measure.Scope("StandardAI���s����") )
        {
            _standardAI.ExecuteAIDecision();
        }

        // ���ʌ��ؗp�̃��O���o��
        int matchCount = 0;
        int mismatchCount = 0;

        for ( int i = 0; i < Math.Min(5, _characterCount); i++ )
        {
            // �eAI�̌��ʂ��擾
            MovementInfo jobResult = _judgeResultJob[i];
            MovementInfo nonJobResult = _judgeResultNonJob[i];
            MovementInfo standardResult = _judgeResultStandard[i];

            // ���ʂ���v���邩�m�F
            bool allMatch = jobResult.result == nonJobResult.result &&
                           jobResult.result == standardResult.result &&
                           jobResult.actNum == nonJobResult.actNum &&
                           jobResult.actNum == standardResult.actNum &&
                           jobResult.targetHash == nonJobResult.targetHash &&
                           jobResult.targetHash == standardResult.targetHash;

            if ( allMatch )
            {
                matchCount++;
            }
            else
            {
                mismatchCount++;
                Debug.LogWarning($"���ʕs��v(index={i}):\n" +
                                $"Job: {jobResult.result}, {jobResult.actNum}, {jobResult.targetHash}\n" +
                                $"NonJob: {nonJobResult.result}, {nonJobResult.actNum}, {nonJobResult.targetHash}\n" +
                                $"Standard: {standardResult.result}, {standardResult.actNum}, {standardResult.targetHash}");
            }
        }

        Debug.Log($"�T���v�����ʔ�r: ��v={matchCount}, �s��v={mismatchCount}");

        // ���؁i���ׂĂ̎����Ō��ʂ���v���邱�Ƃ��m�F�j
        Assert.AreEqual(0, mismatchCount, "�قȂ�����ԂŌ��ʂ���v���܂���");
    }

    /// <summary>
    /// �قȂ�L�����N�^�[���ł̃p�t�H�[�}���X��r�e�X�g
    /// </summary>
    //[UnityTest, Performance]
    public IEnumerator Compare_Different_Character_Counts()
    {
        // �e�X�g�p�̃L�����N�^�[���̔z��
        int[] characterCounts = { 10, 50, 100 };

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

                // StandardAI�e�X�g
                using ( Measure.Scope("StandardAI") )
                {
                    _standardAI.ExecuteAIDecision();
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
}