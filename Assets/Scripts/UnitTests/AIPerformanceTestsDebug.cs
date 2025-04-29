using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.TestTools;
using static CombatManager;
using static JobAITestStatus;
using static JTestAIBase;

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
            this.InitializeTestData();
            Debug.Log($"�e�X�g�f�[�^�̏���������: teamHate.IsCreated={this._teamHate.IsCreated}, Length={this._teamHate.Count}");
        }
        catch ( Exception ex )
        {
            Debug.LogError($"�e�X�g�f�[�^���������̃G���[: {ex.Message}\n{ex.StackTrace}");
            yield break;
        }

        // �L�����N�^�[�f�[�^�̏����� - IEnumerator�Ȃ̂� yield return����
        yield return this.InitializeCharacterData();

        if ( !this._charactersInitialized )
        {
            Debug.LogError("�L�����N�^�[�f�[�^�̏������Ɏ��s���܂���");
            yield break;
        }

        Debug.Log($"�L�����N�^�[�f�[�^�̏���������: characterData.Length={this._characterData.Length}");

        // teamHate�̒��g���m�F
        for ( int i = 0; i < this._teamHate.Count; i++ )
        {
            Debug.Log($"teamHate[{i}].Count={this._teamHate.Count}, IsCreated={this._teamHate.IsCreated}");
        }

        // AI�C���X�^���X�̏�����
        try
        {
            this.InitializeAIInstances();
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
        this.DisposeTestData();
        Debug.Log("����: OneTimeTearDown");
    }

    /// <summary>
    /// �e�X�g�f�[�^�̏�����
    /// </summary>
    private void InitializeTestData()
    {
        Debug.Log($"�J�n: InitializeTestData (CharacterCount={this._characterCount})");
        try
        {
            // UnsafeList�̏�����
            this._characterData = new UnsafeList<CharacterData>(this._characterCount, Allocator.Persistent);
            this._characterData.Resize(this._characterCount, NativeArrayOptions.ClearMemory);
            Debug.Log($"_characterData����������: Length={this._characterData.Length}, IsCreated={this._characterData.IsCreated}");

            this._judgeResultJob = new UnsafeList<MovementInfo>(this._characterCount, Allocator.Persistent);
            this._judgeResultJob.Resize(this._characterCount, NativeArrayOptions.ClearMemory);

            this._judgeResultNonJob = new UnsafeList<MovementInfo>(this._characterCount, Allocator.Persistent);
            this._judgeResultNonJob.Resize(this._characterCount, NativeArrayOptions.ClearMemory);

            // �`�[�����Ƃ̃w�C�g�}�b�v��������
            this._teamHate = new NativeHashMap<int2, int>(3, Allocator.Persistent);
            Debug.Log($"_teamHate�z�񏉊�������: Length={this._teamHate.Count}, IsCreated={this._teamHate.IsCreated}");

            // �w�c�֌W�}�b�v��������
            this._relationMap = new NativeArray<int>(3, Allocator.Persistent);

            for ( int i = 0; i < this._relationMap.Length; i++ )
            {
                // �v���C���[�͓G�ɓG�΁A�G�̓v���C���[�ɓG�΁A���͒����Ȃ�
                switch ( (CharacterSide)i )
                {
                    case CharacterSide.�v���C���[:
                        this._relationMap[i] = 1 << (int)CharacterSide.����;  // �v���C���[�͓G�ɓG��
                        break;
                    case CharacterSide.����:
                        this._relationMap[i] = 1 << (int)CharacterSide.�v���C���[;  // �G�̓v���C���[�ɓG��
                        break;
                    case CharacterSide.���̑�:
                    default:
                        this._relationMap[i] = 0;  // �����͒N�ɂ��G�΂��Ȃ�
                        break;
                }
            }

            this._dataInitialized = true;
        }
        catch ( Exception ex )
        {
            Debug.LogError($"InitializeTestData�ł̃G���[: {ex.Message}\n{ex.StackTrace}");
            this._dataInitialized = false;
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
            if ( !this._teamHate.IsCreated )
            {
                Debug.LogError("teamHate������������Ă��܂���");
                return;
            }

            if ( !this._characterData.IsCreated )
            {
                Debug.LogError("characterData������������Ă��܂���");
                return;
            }

            if ( !this._relationMap.IsCreated )
            {
                Debug.LogError("relationMap������������Ă��܂���");
                return;
            }

            // �`�[���w�C�g�̊e�v�f���m�F
            for ( int i = 0; i < this._teamHate.Count; i++ )
            {
                if ( !this._teamHate.IsCreated )
                {
                    Debug.LogError($"teamHate[{i}]������������Ă��܂���");
                    return;
                }

                Debug.Log($"teamHate[{i}].Count={this._teamHate.Count}, IsCreated={this._teamHate.IsCreated}");
            }

            // AITestJob�̏�����
            this._aiTestJob = new AITestJob
            {
                teamHate = this._teamHate,
                characterData = this._characterData,
                nowTime = this._nowTime,
                judgeResult = this._judgeResultJob,
                relationMap = this._relationMap
            };

            // NonJobAI�̏�����
            this._nonJobAI = new NonJobAI
            {
                teamHate = this._teamHate,
                characterData = this._characterData,
                nowTime = this._nowTime,
                judgeResult = this._judgeResultNonJob,
                relationMap = this._relationMap
            };

            this._aiInstancesInitialized = true;
        }
        catch ( Exception ex )
        {
            Debug.LogError($"InitializeAIInstances�ł̃G���[: {ex.Message}\n{ex.StackTrace}");
            this._aiInstancesInitialized = false;
        }

        Debug.Log($"����: InitializeAIInstances (����={this._aiInstancesInitialized})");
    }

    /// <summary>
    /// �L�����N�^�[�f�[�^�̏�����
    /// </summary>
    private IEnumerator InitializeCharacterData()
    {
        Debug.Log($"�J�n: InitializeCharacterData (CharacterCount={this._characterCount})");

        // _characterData������������Ă��邩�m�F
        if ( !this._characterData.IsCreated )
        {
            Debug.LogError("_characterData������������Ă��܂���");
            this._charactersInitialized = false;
            yield break;
        }

        // _teamHate������������Ă��邩�m�F
        if ( !this._teamHate.IsCreated )
        {
            Debug.LogError("_teamHate������������Ă��܂���");
            this._charactersInitialized = false;
            yield break;
        }

        // �v���n�u�̑��݊m�F
        bool allPrefabsValid = true;
        for ( int i = 0; i < this.types.Length; i++ )
        {
            AsyncOperationHandle<IList<IResourceLocation>> checkOp = Addressables.LoadResourceLocationsAsync(this.types[i]);
            yield return checkOp;

            if ( checkOp.Result.Count == 0 )
            {
                Debug.LogError($"�v���n�u��������܂���: {this.types[i]}");
                allPrefabsValid = false;
            }
            else
            {
                Debug.Log($"�v���n�u���m�F: {this.types[i]}");
            }
        }

        if ( !allPrefabsValid )
        {
            Debug.LogError("�ꕔ�̃v���n�u��������܂���ł���");
            this._charactersInitialized = false;
            yield break;
        }

        // �����̃I�u�W�F�N�g�����ŃC���X�^���X��
        var tasks = new List<AsyncOperationHandle<GameObject>>(this._characterCount);

        for ( int i = 0; i < this._characterCount; i++ )
        {
            // Addressables���g�p���ăC���X�^���X��
            var task = Addressables.InstantiateAsync(this.types[i % 3]);
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
            this._charactersInitialized = false;
            yield break;
        }

        Debug.Log("�S�I�u�W�F�N�g�̃C���X�^���X������");

        // �`�F�b�N�|�C���g�F_teamHate�̏�Ԃ��m�F
        for ( int i = 0; i < this._teamHate.Count; i++ )
        {
            Debug.Log($"�L�����������_teamHate[{i}]: IsCreated={this._teamHate.IsCreated}, Count={this._teamHate.Count}");
        }

        // �������ꂽ�I�u�W�F�N�g�ƕK�v�ȃR���|�[�l���g���擾
        int successCount = 0;

        for ( int i = 0; i < tasks.Count; i++ )
        {
            GameObject obj;
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

            CharacterData data;
            try
            {
                (JobAITestStatus, GameObject) mat = aiComponent.MakeTestData();
                data = new CharacterData(mat.Item1, mat.Item2);
                this._characterData[i] = data;
            }
            catch ( Exception ex )
            {
                Debug.LogError($"�f�[�^�������ɃG���[: {ex.Message}");
                continue;
            }

            successCount++;

            // �w�C�g�}�b�v��������
            int teamNum = (int)data.liveData.belong;

            if ( !this._teamHate.IsCreated )
            {
                Debug.LogError($"_teamHate[{teamNum}]�������ł�");
                continue;
            }

            int2 hateKey = new(teamNum, data.hashCode);

            try
            {
                if ( this._teamHate.ContainsKey(hateKey) )
                {
                    Debug.LogWarning($"�d������hashCode: {data.hashCode} (�`�[��: {teamNum})");
                    continue;
                }

                this._teamHate.Add(hateKey, 10);
            }
            catch ( Exception ex )
            {
                Debug.LogError($"�w�C�g�}�b�v�X�V���ɃG���[: {ex.Message}");
                continue;
            }

            // 100���ƂɃ��O
            if ( i % 100 == 0 )
            {
                Debug.Log($"�L�����N�^�[�������i��: {i}/{tasks.Count}, teamHate[{teamNum}].Count={this._teamHate.Count}");
            }
        }

        Debug.Log($"�L�����N�^�[�f�[�^����������: ������={successCount}/{tasks.Count}");

        // �eteamHate�̍ŏI��Ԃ��m�F
        for ( int i = 0; i < this._teamHate.Count; i++ )
        {
            Debug.Log($"�ŏI_teamHate[{i}]: IsCreated={this._teamHate.IsCreated}, Count={this._teamHate.Count}");
        }

        // �L�����N�^�[�f�[�^�̃X�e�[�^�X�������_����
        Debug.Log("�L�����N�^�[�f�[�^�̃����_�������J�n");

        for ( int i = 0; i < this._characterData.Length; i++ )
        {
            CharacterData data = this._characterData[i];

            try
            {
                CharacterDataRandomizer.RandomizeCharacterData(ref data, this._characterData);
                this._characterData[i] = data;
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

        this._charactersInitialized = successCount > 0;
        Debug.Log($"����: InitializeCharacterData (����={this._charactersInitialized})");
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
            if ( this._characterData.IsCreated )
            {
                Debug.Log($"_characterData�̉���J�n: Length={this._characterData.Length}");
                // �e�L�����N�^�[�f�[�^���̃l�C�e�B�u�R���e�i�����
                for ( int i = 0; i < this._characterData.Length; i++ )
                {
                    CharacterData data = this._characterData[i];
                    data.Dispose();
                }

                this._characterData.Dispose();
                Debug.Log("_characterData�̉������");
            }

            if ( this._judgeResultJob.IsCreated )
            {
                this._judgeResultJob.Dispose();
                Debug.Log("_judgeResultJob�̉������");
            }

            if ( this._judgeResultNonJob.IsCreated )
            {
                this._judgeResultNonJob.Dispose();
                Debug.Log("_judgeResultNonJob�̉������");
            }

            // �`�[���w�C�g�}�b�v�̉��
            if ( this._teamHate.IsCreated )
            {
                Debug.Log($"_teamHate�̉���J�n: Length={this._teamHate.Count}");

                this._teamHate.Dispose();
                Debug.Log("_teamHate�̉������");
            }

            // ���̑���NativeArray�̉��
            if ( this._relationMap.IsCreated )
            {
                this._relationMap.Dispose();
                Debug.Log("_relationMap�̉������");
            }

            this._dataInitialized = false;
            this._charactersInitialized = false;
            this._aiInstancesInitialized = false;
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
        if ( !this._dataInitialized || !this._charactersInitialized || !this._aiInstancesInitialized )
        {
            Debug.LogError($"���������������Ă��܂���: Data={this._dataInitialized}, Characters={this._charactersInitialized}, AI={this._aiInstancesInitialized}");
        }

        // _teamHate�̏�Ԋm�F
        if ( this._teamHate.IsCreated )
        {
            for ( int i = 0; i < this._teamHate.Count; i++ )
            {
                Debug.Log($"SetUp�ł�_teamHate[{i}]: IsCreated={this._teamHate.IsCreated}, Count={this._teamHate.Count}");
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
            for ( int i = 0; i < this._characterData.Length; i++ )
            {
                CharacterData data = this._characterData[i];
                CharacterDataRandomizer.RandomizeCharacterData(ref data, this._characterData);
                this._characterData[i] = data;
            }

            // AI�C���X�^���X�̎��Ԃ��X�V
            this._aiTestJob.nowTime = this._nowTime;
            this._nonJobAI.nowTime = this._nowTime;
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
        if ( this._teamHate.IsCreated )
        {
            Debug.Log($"�e�X�g��_teamHate: Length={this._teamHate.Count}, IsCreated={this._teamHate.IsCreated}");
            for ( int i = 0; i < this._teamHate.Count; i++ )
            {
                Debug.Log($"�e�X�g��_teamHate[{i}]: Count={this._teamHate.Count}, IsCreated={this._teamHate.IsCreated}");

                // �L�[�̃T���v�������O�i�ő�5�j
                var enumerator = this._teamHate.GetEnumerator();
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
            this._aiTestJob = new AITestJob
            {
                teamHate = this._teamHate,
                characterData = this._characterData,
                nowTime = this._nowTime,
                judgeResult = this._judgeResultJob,
                relationMap = this._relationMap
            };

            Debug.Log("AITestJob���ď��������܂���");

            // �l�C�e�B�u�R���e�i���L�����ŏI�m�F
            Debug.Log($"���s�O�̍ŏI�`�F�b�N: teamHate.IsCreated={this._teamHate.IsCreated}, �`�[����={this._teamHate.Count}");
            if ( this._teamHate.IsCreated )
            {
                for ( int i = 0; i < this._teamHate.Count; i++ )
                {
                    Debug.Log($"_teamHate[{i}].IsCreated={this._teamHate.IsCreated}, Count={this._teamHate.Count}");
                }
            }

            Measure.Method(() =>
            {
                // JobSystem��AI�������s
                JobHandle handle = this._aiTestJob.Schedule(this._characterCount, 64);
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
        if ( !this._dataInitialized || !this._charactersInitialized || !this._aiInstancesInitialized )
        {
            Debug.LogError($"���������������Ă��܂���: Data={this._dataInitialized}, Characters={this._charactersInitialized}, AI={this._aiInstancesInitialized}");
            Assert.Fail("���������������Ă��Ȃ����߁A�e�X�g�����s�ł��܂���");
            return;
        }

        // _teamHate�̏�Ԋm�F
        if ( this._teamHate.IsCreated )
        {
            for ( int i = 0; i < this._teamHate.Count; i++ )
            {
                Debug.Log($"���؃e�X�g��_teamHate[{i}]: IsCreated={this._teamHate.IsCreated}, Count={this._teamHate.Count}");
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
            this._aiTestJob = new AITestJob
            {
                teamHate = this._teamHate,
                characterData = this._characterData,
                nowTime = this._nowTime,
                judgeResult = this._judgeResultJob,
                relationMap = this._relationMap
            };

            // �f�[�^�������_����
            for ( int i = 0; i < this._characterData.Length; i++ )
            {
                CharacterData data = this._characterData[i];
                CharacterDataRandomizer.RandomizeCharacterData(ref data, this._characterData);
                this._characterData[i] = data;
            }

            // ������AI���������s
            this._nonJobAI.ExecuteAIDecision();
            Debug.Log("NonJobAI���s����");

            // �W���u���s�O�̍ŏI�`�F�b�N

            Debug.Log($"�W���u���s�O_teamHate: IsCreated={this._teamHate.IsCreated}, Count={this._teamHate.Count}");

            JobHandle handle = this._aiTestJob.Schedule(this._characterCount, 64);
            handle.Complete();
            Debug.Log("JobSystemAI���s����");

            // ���ʂ��r
            bool resultsMatch = true;
            string mismatchInfo = "";

            for ( int i = 0; i < this._characterCount; i++ )
            {
                MovementInfo nonJobResult = this._judgeResultNonJob[i];
                MovementInfo jobResult = this._judgeResultJob[i];

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