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
using Unity.Burst;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine.ResourceManagement.ResourceLocations;
using Unity.Mathematics;

/// <summary>
/// AITestJob�̃p�t�H�[�}���X�e�X�g
/// </summary>
public class AIPerformanceTestsDebug
{
    // �e�X�g�p�̃f�[�^
    private UnsafeList<CharacterData> _characterData;
    private UnsafeList<MovementInfo> _judgeResultJob;
    private UnsafeList<MovementInfo> _judgeResultNonJob;
    private NativeHashMap<int2, int> _teamHate;
    private NativeArray<int> _relationMap;
    //private NativeArray<FunctionPointer<SkipJudgeDelegate>> _skipFunctions;
    //private NativeArray<FunctionPointer<TargetJudgeDelegate>> _targetFunctions;

    // ��������Ԃ�ǐՂ���t���O
    private bool _dataInitialized = false;
    private bool _charactersInitialized = false;
    private bool _aiInstancesInitialized = false;

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
                relationMap = _relationMap
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

        Debug.Log($"�C���X�^���X�����N�G�X�g����: {tasks.Count}��");

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
                (JobAITestStatus, GameObject) mat = aiComponent.MakeTestData();
                data = new CharacterData(mat.Item1, mat.Item2);
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

    /// <summary>
    /// �e�X�g�f�[�^�̃��������
    /// </summary>
    private void DisposeTestData()
    {
        Debug.Log("�J�n: DisposeTestData");
        try
        {
            // UnsafeList�̉��
            if ( _characterData.IsCreated )
            {
                Debug.Log($"_characterData�̉���J�n: Length={_characterData.Length}");
                // �e�L�����N�^�[�f�[�^���̃l�C�e�B�u�R���e�i�����
                for ( int i = 0; i < _characterData.Length; i++ )
                {
                    CharacterData data = _characterData[i];
                    data.Dispose();
                }

                _characterData.Dispose();
                Debug.Log("_characterData�̉������");
            }

            if ( _judgeResultJob.IsCreated )
            {
                _judgeResultJob.Dispose();
                Debug.Log("_judgeResultJob�̉������");
            }

            if ( _judgeResultNonJob.IsCreated )
            {
                _judgeResultNonJob.Dispose();
                Debug.Log("_judgeResultNonJob�̉������");
            }

            // �`�[���w�C�g�}�b�v�̉��
            if ( _teamHate.IsCreated )
            {
                Debug.Log($"_teamHate�̉���J�n: Length={_teamHate.Count}");

                _teamHate.Dispose();
                Debug.Log("_teamHate�̉������");
            }

            // ���̑���NativeArray�̉��
            if ( _relationMap.IsCreated )
            {
                _relationMap.Dispose();
                Debug.Log("_relationMap�̉������");
            }

            _dataInitialized = false;
            _charactersInitialized = false;
            _aiInstancesInitialized = false;
        }
        catch ( Exception ex )
        {
            Debug.LogError($"DisposeTestData�ł̃G���[: {ex.Message}\n{ex.StackTrace}");
        }
        Debug.Log("����: DisposeTestData");
    }

    /// <summary>
    /// �e�e�X�g���s�O�̃X�e�[�^�X�����_����
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        Debug.Log("�J�n: SetUp");

        // �������`�F�b�N
        if ( !_dataInitialized || !_charactersInitialized || !_aiInstancesInitialized )
        {
            Debug.LogError($"���������������Ă��܂���: Data={_dataInitialized}, Characters={_charactersInitialized}, AI={_aiInstancesInitialized}");
        }

        // _teamHate�̏�Ԋm�F
        if ( _teamHate.IsCreated )
        {
            for ( int i = 0; i < _teamHate.Count; i++ )
            {
                Debug.Log($"SetUp�ł�_teamHate[{i}]: IsCreated={_teamHate.IsCreated}, Count={_teamHate.Count}");
            }
        }
        else
        {
            Debug.LogError("SetUp��_teamHate������������Ă��܂���");
        }

        try
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
        catch ( Exception ex )
        {
            Debug.LogError($"SetUp�ł̃G���[: {ex.Message}\n{ex.StackTrace}");
        }
        Debug.Log("����: SetUp");
    }

    /// <summary>
    /// JobSystem��AI�����p�t�H�[�}���X�e�X�g
    /// </summary>
    [Test, Performance]
    public void JobSystemAI_Performance_Test()
    {
        Debug.Log("�J�n: JobSystemAI_Performance_Test");

        // _teamHate�̏�Ԃ��ڍׂɃ��O
        if ( _teamHate.IsCreated )
        {
            Debug.Log($"�e�X�g��_teamHate: Length={_teamHate.Count}, IsCreated={_teamHate.IsCreated}");
            for ( int i = 0; i < _teamHate.Count; i++ )
            {
                Debug.Log($"�e�X�g��_teamHate[{i}]: Count={_teamHate.Count}, IsCreated={_teamHate.IsCreated}");

                // �L�[�̃T���v�������O�i�ő�5�j
                var enumerator = _teamHate.GetEnumerator();
                int count = 0;
                while ( enumerator.MoveNext() && count < 5 )
                {
                    Debug.Log($"  �T���v���L�[: {enumerator.Current.Key}, �l: {enumerator.Current.Value}");
                    count++;
                }

            }
        }
        else
        {
            Debug.LogError("�e�X�g����_teamHate�������ł�");
        }

        // AITestJob���ď��������Ă݂�
        try
        {
            // AITestJob�̏�����
            _aiTestJob = new AITestJob
            {
                teamHate = _teamHate,
                characterData = _characterData,
                nowTime = _nowTime,
                judgeResult = _judgeResultJob,
                relationMap = _relationMap
            };

            Debug.Log("AITestJob���ď��������܂���");

            // �l�C�e�B�u�R���e�i���L�����ŏI�m�F
            Debug.Log($"���s�O�̍ŏI�`�F�b�N: teamHate.IsCreated={_teamHate.IsCreated}, �`�[����={_teamHate.Count}");
            if ( _teamHate.IsCreated )
            {
                for ( int i = 0; i < _teamHate.Count; i++ )
                {
                    Debug.Log($"_teamHate[{i}].IsCreated={_teamHate.IsCreated}, Count={_teamHate.Count}");
                }
            }

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
        catch ( Exception ex )
        {
            Debug.LogError($"JobSystemAI_Performance_Test�ł̃G���[: {ex.ToString()}");
        }

        Debug.Log("����: JobSystemAI_Performance_Test");
    }

    /// <summary>
    /// ���ʂ̌��؃e�X�g�i���������������ʂ��o�����m�F�j
    /// </summary>
    [Test]
    public void Verify_Results_Are_Same()
    {
        Debug.Log("�J�n: Verify_Results_Are_Same");

        // ��������Ԃ̃`�F�b�N
        if ( !_dataInitialized || !_charactersInitialized || !_aiInstancesInitialized )
        {
            Debug.LogError($"���������������Ă��܂���: Data={_dataInitialized}, Characters={_charactersInitialized}, AI={_aiInstancesInitialized}");
            Assert.Fail("���������������Ă��Ȃ����߁A�e�X�g�����s�ł��܂���");
            return;
        }

        // _teamHate�̏�Ԋm�F
        if ( _teamHate.IsCreated )
        {
            for ( int i = 0; i < _teamHate.Count; i++ )
            {
                Debug.Log($"���؃e�X�g��_teamHate[{i}]: IsCreated={_teamHate.IsCreated}, Count={_teamHate.Count}");
            }
        }
        else
        {
            Debug.LogError("���؃e�X�g����_teamHate������������Ă��܂���");
            Assert.Fail("_teamHate������������Ă��Ȃ����߁A�e�X�g�����s�ł��܂���");
            return;
        }

        try
        {
            // AIJob�̍ď�����
            _aiTestJob = new AITestJob
            {
                teamHate = _teamHate,
                characterData = _characterData,
                nowTime = _nowTime,
                judgeResult = _judgeResultJob,
                relationMap = _relationMap
            };

            // �f�[�^�������_����
            for ( int i = 0; i < _characterData.Length; i++ )
            {
                CharacterData data = _characterData[i];
                CharacterDataRandomizer.RandomizeCharacterData(ref data, _characterData);
                _characterData[i] = data;
            }

            // ������AI���������s
            _nonJobAI.ExecuteAIDecision();
            Debug.Log("NonJobAI���s����");

            // �W���u���s�O�̍ŏI�`�F�b�N

            Debug.Log($"�W���u���s�O_teamHate: IsCreated={_teamHate.IsCreated}, Count={_teamHate.Count}");


            JobHandle handle = _aiTestJob.Schedule(_characterCount, 64);
            handle.Complete();
            Debug.Log("JobSystemAI���s����");

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
        catch ( Exception ex )
        {
            Debug.LogError($"���؃e�X�g���s���̃G���[: {ex.ToString()}");
            Assert.Fail($"�e�X�g���s���ɃG���[������: {ex.Message}");
        }
        Debug.Log("����: Verify_Results_Are_Same");
    }
}