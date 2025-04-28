using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;
using Unity.Collections;
using Unity.PerformanceTesting;
using System.Collections;
using System.IO;
using System.Data;
using Unity.Collections.LowLevel.Unsafe;

/// <summary>
/// CharaDataDic�ƕW��Dictionary�ANativeHashMap�AUnsafeHashMap�̐��m���ƃp�t�H�[�}���X��O��I�ɔ�r����e�X�g
/// </summary>
[Serializable]
public class CharacterDicEnhancedTest
{
    [SerializeField]
    public AssetReference testObj;

    // �e�X�g�ݒ�
    private int objectCount = 50000;       // �e�X�g�p�I�u�W�F�N�g��
    private int warmupCount = 3;           // �E�H�[���A�b�v��
    private int measurementCount = 10;     // �e�X�g�v����
    private int deleteRatio = 30;          // �폜�e�X�g�ō폜����䗦�i���j
    private int accessTestRuns = 10000;    // �A�N�Z�X�e�X�g�̌J��Ԃ���

    // �e�X�g�p�I�u�W�F�N�g
    private GameObject[] gameObjects;
    private DicTestData[] testData;
    private int[] gameObjectHashes;         // �n�b�V���R�[�h�ۑ��p
    private HashSet<int> selectedForDeletion; // �폜�p�ɑI�����ꂽ�C���f�b�N�X

    // �e�X�g�Ώۂ̃R���N�V����
    private Dictionary<GameObject, DicTestData> standardDictionary;
    private CharaDataDic<DicTestData> customDictionary;
    private NativeHashMap<int, DicTestData> nativeHashMap;
    private NativeParallelHashMap<int, DicTestData> nativeParallelHashMap;
    private UnsafeHashMap<int, DicTestData> unsafeHashMap;
    private UnsafeParallelHashMap<int, DicTestData> unsafeParallelHashMap;

    // �v�����ʂ̈ꎞ�ۑ��p
    private string[] matchingTest;
    private int unMatchCount;
    private double stdAddTime;
    private double customAddTime;
    private double nativeAddTime;
    private double unsafeAddTime;
    private double stdSequentialTime;
    private double customSequentialTime;
    private double nativeSequentialTime;
    private double unsafeSequentialTime;
    private double stdRandomTime;
    private double customRandomTime;
    private double nativeRandomTime;
    private double unsafeRandomTime;
    private double stdDeleteTime;
    private double customDeleteTime;
    private double nativeDeleteTime;
    private double unsafeDeleteTime;

    // �œK���h�~�p�̕ϐ�
    private long dataSum;
    private int errorCount;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // �e�X�g�I�u�W�F�N�g�̐���
        gameObjects = new GameObject[objectCount];
        testData = new DicTestData[objectCount];
        gameObjectHashes = new int[objectCount];
        selectedForDeletion = new HashSet<int>();

        // �����̃I�u�W�F�N�g�����ŃC���X�^���X��
        var tasks = new List<AsyncOperationHandle<GameObject>>(objectCount);

        for ( int i = 0; i < objectCount; i++ )
        {
            // Addressables���g�p���ăC���X�^���X��
            var task = Addressables.InstantiateAsync("Assets/Prefab/TestPrefab/MyDicTest.prefab");
            tasks.Add(task);
        }

        // ���ׂẴI�u�W�F�N�g�����������̂�҂�
        foreach ( var task in tasks )
        {
            yield return task;
        }

        // �������ꂽ�I�u�W�F�N�g�ƕK�v�ȃR���|�[�l���g���擾
        for ( int i = 0; i < objectCount; i++ )
        {
            gameObjects[i] = tasks[i].Result;
            gameObjects[i].name = $"TestObject_{i}";

            var component = gameObjects[i].GetComponent<MyDicTestComponent>();
            component.IDSet();
            testData[i] = component.data;

            // �n�b�V���R�[�h��ۑ�
            gameObjectHashes[i] = gameObjects[i].GetHashCode();

            // GetComponent����null�ł��邱�Ƃ��m�F
            Assert.IsNotNull(testData[i], $"�I�u�W�F�N�g {i} �� DicTestData ��������܂���");
        }

        // �폜�p�̃I�u�W�F�N�g�������_���ɑI��
        int deleteCount = objectCount * deleteRatio / 100;
        selectedForDeletion.Clear();
        System.Random rnd = new System.Random(42); // �Č����̂��߂ɌŒ�V�[�h
        while ( selectedForDeletion.Count < deleteCount )
        {
            selectedForDeletion.Add(rnd.Next(0, objectCount));
        }

        // �����̏�����
        standardDictionary = new Dictionary<GameObject, DicTestData>(objectCount);
        customDictionary = new CharaDataDic<DicTestData>(objectCount);

        // Native/Unsafe�R���N�V�����̏�����
        nativeHashMap = new NativeHashMap<int, DicTestData>(objectCount, Allocator.Persistent);
        nativeParallelHashMap = new NativeParallelHashMap<int, DicTestData>(objectCount, Allocator.Persistent);
        unsafeHashMap = new UnsafeHashMap<int, DicTestData>(objectCount, Allocator.Persistent);
        unsafeParallelHashMap = new UnsafeParallelHashMap<int, DicTestData>(objectCount, Allocator.Persistent);

        // �����l���N���A
        dataSum = 0;
        errorCount = 0;
        stdAddTime = 0;
        customAddTime = 0;
        nativeAddTime = 0;
        unsafeAddTime = 0;
        stdSequentialTime = 0;
        customSequentialTime = 0;
        nativeSequentialTime = 0;
        unsafeSequentialTime = 0;
        stdRandomTime = 0;
        customRandomTime = 0;
        nativeRandomTime = 0;
        unsafeRandomTime = 0;
        stdDeleteTime = 0;
        customDeleteTime = 0;
        nativeDeleteTime = 0;
        unsafeDeleteTime = 0;
    }

    [TearDown]
    public void TearDown()
    {
        // ���������I�u�W�F�N�g�̔j��
        for ( int i = 0; i < objectCount; i++ )
        {
            if ( gameObjects[i] != null )
            {
                Addressables.ReleaseInstance(gameObjects[i]);
            }
        }

        // Native/Unsafe�R���N�V�����̔j��
        if ( nativeHashMap.IsCreated )
            nativeHashMap.Dispose();
        if ( nativeParallelHashMap.IsCreated )
            nativeParallelHashMap.Dispose();
        if ( unsafeHashMap.IsCreated )
            unsafeHashMap.Dispose();
        if ( unsafeParallelHashMap.IsCreated )
            unsafeParallelHashMap.Dispose();

        // �����̃N���[���A�b�v
        standardDictionary.Clear();
        customDictionary.Dispose();

        // ���ʃT�}���[���o��
        UnityEngine.Debug.Log($"�e�X�g���ʊm�F�p�f�[�^���v: {dataSum}");
        UnityEngine.Debug.Log($"�G���[��: {errorCount}");
        UnityEngine.Debug.Log("=== �p�t�H�[�}���X��r ===");
        UnityEngine.Debug.Log($"�v�f�ǉ�: �W��Dictionary {stdAddTime:F3}ms vs CharaDataDic {customAddTime:F3}ms (�䗦: {customAddTime / stdAddTime:P})");
        UnityEngine.Debug.Log($"�v�f�ǉ�: �W��Dictionary {stdAddTime:F3}ms vs NativeHashMap {nativeAddTime:F3}ms (�䗦: {nativeAddTime / stdAddTime:P})");
        UnityEngine.Debug.Log($"�v�f�ǉ�: �W��Dictionary {stdAddTime:F3}ms vs UnsafeHashMap {unsafeAddTime:F3}ms (�䗦: {unsafeAddTime / stdAddTime:P})");
        UnityEngine.Debug.Log($"�����A�N�Z�X: �W��Dictionary {stdSequentialTime:F3}ms vs CharaDataDic {customSequentialTime:F3}ms (�䗦: {customSequentialTime / stdSequentialTime:P})");
        UnityEngine.Debug.Log($"�����A�N�Z�X: �W��Dictionary {stdSequentialTime:F3}ms vs NativeHashMap {nativeSequentialTime:F3}ms (�䗦: {nativeSequentialTime / stdSequentialTime:P})");
        UnityEngine.Debug.Log($"�����A�N�Z�X: �W��Dictionary {stdSequentialTime:F3}ms vs UnsafeHashMap {unsafeSequentialTime:F3}ms (�䗦: {unsafeSequentialTime / stdSequentialTime:P})");
        UnityEngine.Debug.Log($"�����_���A�N�Z�X: �W��Dictionary {stdRandomTime:F3}ms vs CharaDataDic {customRandomTime:F3}ms (�䗦: {customRandomTime / stdRandomTime:P})");
        UnityEngine.Debug.Log($"�����_���A�N�Z�X: �W��Dictionary {stdRandomTime:F3}ms vs NativeHashMap {nativeRandomTime:F3}ms (�䗦: {nativeRandomTime / stdRandomTime:P})");
        UnityEngine.Debug.Log($"�����_���A�N�Z�X: �W��Dictionary {stdRandomTime:F3}ms vs UnsafeHashMap {unsafeRandomTime:F3}ms (�䗦: {unsafeRandomTime / stdRandomTime:P})");
        UnityEngine.Debug.Log($"�v�f�폜: �W��Dictionary {stdDeleteTime:F3}ms vs CharaDataDic {customDeleteTime:F3}ms (�䗦: {customDeleteTime / stdDeleteTime:P})");
        UnityEngine.Debug.Log($"�v�f�폜: �W��Dictionary {stdDeleteTime:F3}ms vs NativeHashMap {nativeDeleteTime:F3}ms (�䗦: {nativeDeleteTime / stdDeleteTime:P})");
        UnityEngine.Debug.Log($"�v�f�폜: �W��Dictionary {stdDeleteTime:F3}ms vs UnsafeHashMap {unsafeDeleteTime:F3}ms (�䗦: {unsafeDeleteTime / stdDeleteTime:P})");

        string absolutePath = Path.Combine(
Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop\\qiita�L��\\����R���N�V��������\\���\�e�X�g����.txt");

        // �t�@�C���ɐV�����s��ǉ�����
        using ( StreamWriter sw = new StreamWriter(absolutePath, true) )
        {
            // ���ʃT�}���[���o��
            sw.WriteLine($"�e�X�g���ʊm�F�p�f�[�^���v: {dataSum}");
            sw.WriteLine($"�G���[��: {errorCount}");
            sw.WriteLine("=== �p�t�H�[�}���X��r ===");
            sw.WriteLine($"�v�f�ǉ�: �W��Dictionary {stdAddTime:F3}ms vs CharaDataDic {customAddTime:F3}ms (�䗦: {customAddTime / stdAddTime:P})");
            sw.WriteLine($"�v�f�ǉ�: �W��Dictionary {stdAddTime:F3}ms vs NativeHashMap {nativeAddTime:F3}ms (�䗦: {nativeAddTime / stdAddTime:P})");
            sw.WriteLine($"�v�f�ǉ�: �W��Dictionary {stdAddTime:F3}ms vs UnsafeHashMap {unsafeAddTime:F3}ms (�䗦: {unsafeAddTime / stdAddTime:P})");
            sw.WriteLine($"�����A�N�Z�X: �W��Dictionary {stdSequentialTime:F3}ms vs CharaDataDic {customSequentialTime:F3}ms (�䗦: {customSequentialTime / stdSequentialTime:P})");
            sw.WriteLine($"�����A�N�Z�X: �W��Dictionary {stdSequentialTime:F3}ms vs NativeHashMap {nativeSequentialTime:F3}ms (�䗦: {nativeSequentialTime / stdSequentialTime:P})");
            sw.WriteLine($"�����A�N�Z�X: �W��Dictionary {stdSequentialTime:F3}ms vs UnsafeHashMap {unsafeSequentialTime:F3}ms (�䗦: {unsafeSequentialTime / stdSequentialTime:P})");
            sw.WriteLine($"�����_���A�N�Z�X: �W��Dictionary {stdRandomTime:F3}ms vs CharaDataDic {customRandomTime:F3}ms (�䗦: {customRandomTime / stdRandomTime:P})");
            sw.WriteLine($"�����_���A�N�Z�X: �W��Dictionary {stdRandomTime:F3}ms vs NativeHashMap {nativeRandomTime:F3}ms (�䗦: {nativeRandomTime / stdRandomTime:P})");
            sw.WriteLine($"�����_���A�N�Z�X: �W��Dictionary {stdRandomTime:F3}ms vs UnsafeHashMap {unsafeRandomTime:F3}ms (�䗦: {unsafeRandomTime / stdRandomTime:P})");
            sw.WriteLine($"�v�f�폜: �W��Dictionary {stdDeleteTime:F3}ms vs CharaDataDic {customDeleteTime:F3}ms (�䗦: {customDeleteTime / stdDeleteTime:P})");
            sw.WriteLine($"�v�f�폜: �W��Dictionary {stdDeleteTime:F3}ms vs NativeHashMap {nativeDeleteTime:F3}ms (�䗦: {nativeDeleteTime / stdDeleteTime:P})");
            sw.WriteLine($"�v�f�폜: �W��Dictionary {stdDeleteTime:F3}ms vs UnsafeHashMap {unsafeDeleteTime:F3}ms (�䗦: {unsafeDeleteTime / stdDeleteTime:P})");
            sw.WriteLine(string.Empty);
        }

        absolutePath = Path.Combine(
Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop\\qiita�L��\\����R���N�V��������\\�������e�X�g����.txt");

        // �t�@�C���ɐV�����s��ǉ�����
        using ( StreamWriter sw = new StreamWriter(absolutePath, true) )
        {
            // ���ʃT�}���[���o��
            for ( int i = 0; i < matchingTest.Length; i++ )
            {
                sw.WriteLine(matchingTest[i]);
            }
            sw.WriteLine(string.Empty);
        }
    }

    /// <summary>
    /// ���m���e�X�g - �ǉ�&�A�N�Z�X
    /// </summary>
    [Test]
    public void CorrectnessTest_AddAndAccess()
    {
        // �����̎�����������
        standardDictionary.Clear();
        customDictionary.Clear();
        nativeHashMap.Clear();
        unsafeHashMap.Clear();

        // �v�f��ǉ�
        for ( int i = 0; i < objectCount; i++ )
        {
            standardDictionary.Add(gameObjects[i], testData[i]);
            customDictionary.Add(gameObjects[i], testData[i]);
            nativeHashMap.Add(gameObjectHashes[i], testData[i]);
            unsafeHashMap.Add(gameObjectHashes[i], testData[i]);
        }

        // �v�f�Ɖ�e�X�g�p�̔z��
        matchingTest = new string[gameObjects.Length + 1];

        // ���ׂĂ̗v�f�ɐ������A�N�Z�X�ł��邩�m�F
        for ( int i = 0; i < objectCount; i++ )
        {
            DicTestData stdData = standardDictionary[gameObjects[i]];
            DicTestData customData = customDictionary[gameObjects[i]];
            DicTestData nativeData = nativeHashMap[gameObjectHashes[i]];
            DicTestData unsafeData = unsafeHashMap[gameObjectHashes[i]];

            // �����f�[�^���擾�ł��邱�Ƃ��m�F
            Assert.AreEqual(stdData.TestValue, customData.TestValue,
                $"Index {i}: �W��Dictionary ({stdData.TestValue}) �� CharaDataDic ({customData.TestValue}) �ňقȂ�l���Ԃ���܂���");
            Assert.AreEqual(stdData.TestValue, nativeData.TestValue,
                $"Index {i}: �W��Dictionary ({stdData.TestValue}) �� NativeHashMap ({nativeData.TestValue}) �ňقȂ�l���Ԃ���܂���");
            Assert.AreEqual(stdData.TestValue, unsafeData.TestValue,
                $"Index {i}: �W��Dictionary ({stdData.TestValue}) �� UnsafeHashMap ({unsafeData.TestValue}) �ňقȂ�l���Ԃ���܂���");

            // ��v���Ȃ��ꍇ
            if ( stdData.TestValue != customData.TestValue ||
                 stdData.TestValue != nativeData.TestValue ||
                 stdData.TestValue != unsafeData.TestValue )
            {
                unMatchCount++;
                matchingTest[i + 1] = $"���ʁF�s��v (Dictionary�F{stdData.TestValue}) CharaDataDic�F({customData.TestValue}) NativeHashMap�F({nativeData.TestValue}) UnsafeHashMap�F({unsafeData.TestValue})";
            }
            else
            {
                matchingTest[i + 1] = $"���ʁF��v ({stdData.TestValue})";
            }
        }

        matchingTest[0] = $"�s��v:{unMatchCount} ��";

        // �n�b�V���R�[�h�ɂ��A�N�Z�X���m�F
        for ( int i = 0; i < objectCount; i++ )
        {
            int hash = gameObjectHashes[i];
            bool custResult = customDictionary.TryGetValueByHash(hash, out DicTestData custData, out _);
            bool nativeResult = nativeHashMap.TryGetValue(hash, out DicTestData nativeData);
            bool unsafeResult = unsafeHashMap.TryGetValue(hash, out DicTestData unsafeData);

            Assert.IsTrue(custResult, $"Index {i}: CharaDataDic�Ńn�b�V���R�[�h {hash} �ł̌����Ɏ��s���܂���");
            Assert.IsTrue(nativeResult, $"Index {i}: NativeHashMap�Ńn�b�V���R�[�h {hash} �ł̌����Ɏ��s���܂���");
            Assert.IsTrue(unsafeResult, $"Index {i}: UnsafeHashMap�Ńn�b�V���R�[�h {hash} �ł̌����Ɏ��s���܂���");
        }
    }

    /// <summary>
    /// ���m���e�X�g - �폜
    /// </summary>
    [Test]
    public void CorrectnessTest_Remove()
    {
        // ���������������ėv�f��ǉ�
        standardDictionary.Clear();
        customDictionary.Clear();
        nativeHashMap.Clear();
        unsafeHashMap.Clear();

        for ( int i = 0; i < objectCount; i++ )
        {
            standardDictionary.Add(gameObjects[i], testData[i]);
            customDictionary.Add(gameObjects[i], testData[i]);
            nativeHashMap.Add(gameObjectHashes[i], testData[i]);
            unsafeHashMap.Add(gameObjectHashes[i], testData[i]);
        }

        // �I�����ꂽ�I�u�W�F�N�g���폜
        foreach ( int idx in selectedForDeletion )
        {
            bool stdResult = standardDictionary.Remove(gameObjects[idx]);
            bool customResult = customDictionary.Remove(gameObjects[idx]);
            bool nativeResult = nativeHashMap.Remove(gameObjectHashes[idx]);
            bool unsafeResult = unsafeHashMap.Remove(gameObjectHashes[idx]);

            Assert.IsTrue(stdResult, $"�W��Dictionary����v�f {idx} �̍폜�Ɏ��s���܂���");
            Assert.IsTrue(customResult, $"CharaDataDic����v�f {idx} �̍폜�Ɏ��s���܂���");
            Assert.IsTrue(nativeResult, $"NativeHashMap����v�f {idx} �̍폜�Ɏ��s���܂���");
            Assert.IsTrue(unsafeResult, $"UnsafeHashMap����v�f {idx} �̍폜�Ɏ��s���܂���");
        }

        // �폜���ꂽ���m�F
        foreach ( int idx in selectedForDeletion )
        {
            bool stdContains = standardDictionary.ContainsKey(gameObjects[idx]);
            bool customContains = customDictionary.TryGetValue(gameObjects[idx], out _, out _);
            bool nativeContains = nativeHashMap.ContainsKey(gameObjectHashes[idx]);
            bool unsafeContains = unsafeHashMap.ContainsKey(gameObjectHashes[idx]);

            Assert.IsFalse(stdContains, $"�W��Dictionary�ɍ폜�����͂��̗v�f {idx} ���܂܂�Ă��܂�");
            Assert.IsFalse(customContains, $"CharaDataDic�ɍ폜�����͂��̗v�f {idx} ���܂܂�Ă��܂�");
            Assert.IsFalse(nativeContains, $"NativeHashMap�ɍ폜�����͂��̗v�f {idx} ���܂܂�Ă��܂�");
            Assert.IsFalse(unsafeContains, $"UnsafeHashMap�ɍ폜�����͂��̗v�f {idx} ���܂܂�Ă��܂�");
        }

        // ���̗v�f�͂܂����݂��邩�m�F
        for ( int i = 0; i < objectCount; i++ )
        {
            if ( !selectedForDeletion.Contains(i) )
            {
                bool stdContains = standardDictionary.ContainsKey(gameObjects[i]);
                bool customContains = customDictionary.TryGetValue(gameObjects[i], out _, out _);
                bool nativeContains = nativeHashMap.ContainsKey(gameObjectHashes[i]);
                bool unsafeContains = unsafeHashMap.ContainsKey(gameObjectHashes[i]);

                Assert.IsTrue(stdContains, $"�W��Dictionary�������ėv�f {i} ���폜����Ă��܂�");
                Assert.IsTrue(customContains, $"CharaDataDic�������ėv�f {i} ���폜����Ă��܂�");
                Assert.IsTrue(nativeContains, $"NativeHashMap�������ėv�f {i} ���폜����Ă��܂�");
                Assert.IsTrue(unsafeContains, $"UnsafeHashMap�������ėv�f {i} ���폜����Ă��܂�");
            }
        }
    }

    /// <summary>
    /// ���m���e�X�g - �ϋv���i��ʒǉ��ƍ폜���J��Ԃ��j
    /// </summary>
    [Test]
    public void CorrectnessTest_Durability()
    {
        // 5��J��Ԃ��ǉ��ƍ폜���s��
        for ( int run = 0; run < 5; run++ )
        {
            // ������������
            standardDictionary.Clear();
            customDictionary.Clear();
            nativeHashMap.Clear();
            unsafeHashMap.Clear();

            // �S�v�f��ǉ�
            for ( int i = 0; i < objectCount; i++ )
            {
                standardDictionary.Add(gameObjects[i], testData[i]);
                customDictionary.Add(gameObjects[i], testData[i]);
                nativeHashMap.Add(gameObjectHashes[i], testData[i]);
                unsafeHashMap.Add(gameObjectHashes[i], testData[i]);
            }

            // �����̗v�f���폜
            for ( int i = 0; i < objectCount; i += 2 )
            {
                standardDictionary.Remove(gameObjects[i]);
                customDictionary.Remove(gameObjects[i]);
                nativeHashMap.Remove(gameObjectHashes[i]);
                unsafeHashMap.Remove(gameObjectHashes[i]);
            }

            // �폜�����v�f���Ēǉ�
            for ( int i = 0; i < objectCount; i += 2 )
            {
                standardDictionary[gameObjects[i]] = testData[i];
                customDictionary.Add(gameObjects[i], testData[i]);
                nativeHashMap.Add(gameObjectHashes[i], testData[i]);
                unsafeHashMap.Add(gameObjectHashes[i], testData[i]);
            }

            // �S�v�f�ɃA�N�Z�X���Ĉ�v���m�F
            for ( int i = 0; i < objectCount; i++ )
            {
                DicTestData stdData = standardDictionary[gameObjects[i]];
                DicTestData customData = customDictionary[gameObjects[i]];
                DicTestData nativeData = nativeHashMap[gameObjectHashes[i]];
                DicTestData unsafeData = unsafeHashMap[gameObjectHashes[i]];

                if ( stdData.TestValue != customData.TestValue ||
                     stdData.TestValue != nativeData.TestValue ||
                     stdData.TestValue != unsafeData.TestValue )
                {
                    errorCount++;
                }
            }
        }

        Assert.AreEqual(0, errorCount, "�J��Ԃ��̒ǉ��폜�e�X�g���ɃG���[���������܂���");
    }

    /// <summary>
    /// �p�t�H�[�}���X�e�X�g - �v�f�ǉ�
    /// </summary>
    [Test, Performance]
    public void PerformanceTest_Add()
    {
        // �W��Dictionary�ǉ��e�X�g
        Measure.Method(() =>
        {
            standardDictionary.Clear();
            for ( int i = 0; i < objectCount; i++ )
            {
                standardDictionary.Add(gameObjects[i], testData[i]);
            }
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("StandardDictionary_Add", SampleUnit.Millisecond))
        .Run();

        // �W��Dictionary�̌��ʂ�ۑ�
        stdAddTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "StandardDictionary_Add")?.Median ?? 0;

        // CharaDataDic�ǉ��e�X�g
        Measure.Method(() =>
        {
            customDictionary.Clear();
            for ( int i = 0; i < objectCount; i++ )
            {
                customDictionary.Add(gameObjects[i], testData[i]);
            }
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("CharaDataDic_Add", SampleUnit.Millisecond))
        .Run();

        // CharaDataDic�̌��ʂ�ۑ�
        customAddTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "CharaDataDic_Add")?.Median ?? 0;

        // NativeHashMap�ǉ��e�X�g
        Measure.Method(() =>
        {
            nativeHashMap.Clear();
            for ( int i = 0; i < objectCount; i++ )
            {
                nativeHashMap.Add(gameObjectHashes[i], testData[i]);
            }
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("NativeHashMap_Add", SampleUnit.Millisecond))
        .Run();

        // NativeHashMap�̌��ʂ�ۑ�
        nativeAddTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "NativeHashMap_Add")?.Median ?? 0;

        // UnsafeHashMap�ǉ��e�X�g
        Measure.Method(() =>
        {
            unsafeHashMap.Clear();
            for ( int i = 0; i < objectCount; i++ )
            {
                unsafeHashMap.Add(gameObjectHashes[i], testData[i]);
            }
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("UnsafeHashMap_Add", SampleUnit.Millisecond))
        .Run();

        // UnsafeHashMap�̌��ʂ�ۑ�
        unsafeAddTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "UnsafeHashMap_Add")?.Median ?? 0;
    }

    /// <summary>
    /// �p�t�H�[�}���X�e�X�g - �A���A�N�Z�X
    /// </summary>
    [Test, Performance]
    public void PerformanceTest_SequentialAccess()
    {
        // ���O�����F�����ɗv�f��ǉ�
        PrepareForAccessTest();

        // �W��Dictionary�A���A�N�Z�X�e�X�g
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < accessTestRuns; run++ )
            {
                int idx = run % objectCount;
                DicTestData data = standardDictionary[gameObjects[idx]];
                localSum += data.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("StandardDictionary_Sequential", SampleUnit.Millisecond))
        .Run();

        // �W��Dictionary�̌��ʂ�ۑ�
        stdSequentialTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "StandardDictionary_Sequential")?.Median ?? 0;

        // CharaDataDic�A���A�N�Z�X�e�X�g
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < accessTestRuns; run++ )
            {
                int idx = run % objectCount;
                DicTestData data = customDictionary[gameObjects[idx]];
                localSum += data.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("CharaDataDic_Sequential", SampleUnit.Millisecond))
        .Run();

        // CharaDataDic�̌��ʂ�ۑ�
        customSequentialTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "CharaDataDic_Sequential")?.Median ?? 0;

        // NativeHashMap�A���A�N�Z�X�e�X�g
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < accessTestRuns; run++ )
            {
                int idx = run % objectCount;
                DicTestData data = nativeHashMap[gameObjectHashes[idx]];
                localSum += data.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("NativeHashMap_Sequential", SampleUnit.Millisecond))
        .Run();

        // NativeHashMap�̌��ʂ�ۑ�
        nativeSequentialTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "NativeHashMap_Sequential")?.Median ?? 0;

        // UnsafeHashMap�A���A�N�Z�X�e�X�g
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < accessTestRuns; run++ )
            {
                int idx = run % objectCount;
                DicTestData data = unsafeHashMap[gameObjectHashes[idx]];
                localSum += data.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("UnsafeHashMap_Sequential", SampleUnit.Millisecond))
        .Run();

        // UnsafeHashMap�̌��ʂ�ۑ�
        unsafeSequentialTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "UnsafeHashMap_Sequential")?.Median ?? 0;
    }

    /// <summary>
    /// �p�t�H�[�}���X�e�X�g - �����_���A�N�Z�X
    /// </summary>
    [Test, Performance]
    public void PerformanceTest_RandomAccess()
    {
        // ���O�����F�����ɗv�f��ǉ�
        PrepareForAccessTest();

        // �����_���C���f�b�N�X�𐶐�
        System.Random rnd = new System.Random(42);
        int[] randomIndices = new int[accessTestRuns];
        for ( int i = 0; i < accessTestRuns; i++ )
        {
            randomIndices[i] = rnd.Next(0, objectCount);
        }

        // �W��Dictionary�����_���A�N�Z�X�e�X�g
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < accessTestRuns; run++ )
            {
                int idx = randomIndices[run];
                DicTestData data = standardDictionary[gameObjects[idx]];
                localSum += data.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("StandardDictionary_Random", SampleUnit.Millisecond))
        .Run();

        // �W��Dictionary�̌��ʂ�ۑ�
        stdRandomTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "StandardDictionary_Random")?.Median ?? 0;

        // CharaDataDic�����_���A�N�Z�X�e�X�g
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < accessTestRuns; run++ )
            {
                int idx = randomIndices[run];
                DicTestData data = customDictionary[gameObjects[idx]];
                localSum += data.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("CharaDataDic_Random", SampleUnit.Millisecond))
        .Run();

        // CharaDataDic�̌��ʂ�ۑ�
        customRandomTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "CharaDataDic_Random")?.Median ?? 0;

        // NativeHashMap�����_���A�N�Z�X�e�X�g
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < accessTestRuns; run++ )
            {
                int idx = randomIndices[run];
                DicTestData data = nativeHashMap[gameObjectHashes[idx]];
                localSum += data.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("NativeHashMap_Random", SampleUnit.Millisecond))
        .Run();

        // NativeHashMap�̌��ʂ�ۑ�
        nativeRandomTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "NativeHashMap_Random")?.Median ?? 0;

        // UnsafeHashMap�����_���A�N�Z�X�e�X�g
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < accessTestRuns; run++ )
            {
                int idx = randomIndices[run];
                DicTestData data = unsafeHashMap[gameObjectHashes[idx]];
                localSum += data.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("UnsafeHashMap_Random", SampleUnit.Millisecond))
        .Run();

        // UnsafeHashMap�̌��ʂ�ۑ�
        unsafeRandomTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "UnsafeHashMap_Random")?.Median ?? 0;
    }

    /// <summary>
    /// �p�t�H�[�}���X�e�X�g - �v�f�폜
    /// </summary>
    [Test, Performance]
    public void PerformanceTest_Remove()
    {
        // �W��Dictionary�폜�e�X�g
        PrepareForAccessTest();
        Measure.Method(() =>
        {
            foreach ( int idx in selectedForDeletion )
            {
                standardDictionary.Remove(gameObjects[idx]);
            }
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("StandardDictionary_Remove", SampleUnit.Millisecond))
        .Run();

        // �W��Dictionary�̌��ʂ�ۑ�
        stdDeleteTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "StandardDictionary_Remove")?.Median ?? 0;

        // CharaDataDic�폜�e�X�g
        PrepareForAccessTest();
        Measure.Method(() =>
        {
            foreach ( int idx in selectedForDeletion )
            {
                customDictionary.Remove(gameObjects[idx]);
            }
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("CharaDataDic_Remove", SampleUnit.Millisecond))
        .Run();

        // CharaDataDic�̌��ʂ�ۑ�
        customDeleteTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "CharaDataDic_Remove")?.Median ?? 0;

        // NativeHashMap�폜�e�X�g
        PrepareForAccessTest();
        Measure.Method(() =>
        {
            foreach ( int idx in selectedForDeletion )
            {
                nativeHashMap.Remove(gameObjectHashes[idx]);
            }
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("NativeHashMap_Remove", SampleUnit.Millisecond))
        .Run();

        // NativeHashMap�̌��ʂ�ۑ�
        nativeDeleteTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "NativeHashMap_Remove")?.Median ?? 0;

        // UnsafeHashMap�폜�e�X�g
        PrepareForAccessTest();
        Measure.Method(() =>
        {
            foreach ( int idx in selectedForDeletion )
            {
                unsafeHashMap.Remove(gameObjectHashes[idx]);
            }
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("UnsafeHashMap_Remove", SampleUnit.Millisecond))
        .Run();

        // UnsafeHashMap�̌��ʂ�ۑ�
        unsafeDeleteTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "UnsafeHashMap_Remove")?.Median ?? 0;
    }


    /// <summary>
    /// �p�t�H�[�}���X�e�X�g - �n�b�V�����ڃA�N�Z�X
    /// </summary>
  // [Test, Performance]
    public void PerformanceTest_HashCodeAccess()
    {
        // ���O�����F�����ɗv�f��ǉ�
        PrepareForAccessTest();

        // �Q�[���I�u�W�F�N�g�̃n�b�V���R�[�h�����W
        int[] hashes = new int[objectCount];
        for ( int i = 0; i < objectCount; i++ )
        {
            hashes[i] = gameObjects[i].GetHashCode();
        }

        // �ʏ�̕��@�ŃA�N�Z�X�i�Q�Ɗ�j
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int i = 0; i < accessTestRuns; i++ )
            {
                int idx = i % objectCount;
                DicTestData data = customDictionary[gameObjects[idx]];
                localSum += data.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("GameObjectAccess", SampleUnit.Millisecond))
        .Run();

        // CharaDataDic�̃n�b�V���R�[�h���g�������ڃA�N�Z�X
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int i = 0; i < accessTestRuns; i++ )
            {
                int idx = i % objectCount;
                int hash = hashes[idx];
                customDictionary.TryGetValueByHash(hash, out DicTestData data, out _);
                localSum += data.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("CharaDataDic_HashCodeAccess", SampleUnit.Millisecond))
        .Run();

        // NativeHashMap�̃L�[�Ƃ��Ẵn�b�V���R�[�h�A�N�Z�X
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int i = 0; i < accessTestRuns; i++ )
            {
                int idx = i % objectCount;
                int hash = hashes[idx];
                DicTestData data = nativeHashMap[hash];
                localSum += data.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("NativeHashMap_HashCodeAccess", SampleUnit.Millisecond))
        .Run();

        // UnsafeHashMap�̃L�[�Ƃ��Ẵn�b�V���R�[�h�A�N�Z�X
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int i = 0; i < accessTestRuns; i++ )
            {
                int idx = i % objectCount;
                int hash = hashes[idx];
                DicTestData data = unsafeHashMap[hash];
                localSum += data.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("UnsafeHashMap_HashCodeAccess", SampleUnit.Millisecond))
        .Run();
    }

    /// <summary>
    /// �p�t�H�[�}���X�e�X�g - �C���f�b�N�X���ڃA�N�Z�X
    /// </summary>
   // [Test, Performance]
    public void PerformanceTest_DirectIndexAccess()
    {
        // ���O�����F�����ɗv�f��ǉ�
        PrepareForAccessTest();

        // �C���f�b�N�X�����W
        List<int> indices = new List<int>(objectCount);
        for ( int i = 0; i < objectCount; i++ )
        {
            int index = customDictionary.Add(gameObjects[i], testData[i]);
            indices.Add(index);
        }

        // �ʏ�̕��@�ŃA�N�Z�X�i�Q�Ɗ�j
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int i = 0; i < accessTestRuns; i++ )
            {
                int idx = i % objectCount;
                DicTestData data = customDictionary[gameObjects[idx]];
                localSum += data.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("NormalAccess", SampleUnit.Millisecond))
        .Run();

        // �C���f�b�N�X�ł̒��ڃA�N�Z�X
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int i = 0; i < accessTestRuns; i++ )
            {
                int idx = i % objectCount;
                int valueIndex = indices[idx];
                ref DicTestData data = ref customDictionary.GetDataByIndex(valueIndex);
                localSum += data.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("DirectIndexAccess", SampleUnit.Millisecond))
        .Run();
    }

    /// <summary>
    /// NativeHashMap��UnsafeHashMap�̕��񏈗����\��r
    /// </summary>
//    [Test, Performance]
    public void PerformanceTest_ParallelAccess()
    {
        // �W����HashMap�ł̏���
        Measure.Method(() =>
        {
            nativeHashMap.Clear();
            for ( int i = 0; i < objectCount; i++ )
            {
                nativeHashMap.Add(gameObjectHashes[i], testData[i]);
            }
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("NativeHashmap", SampleUnit.Millisecond))
        .Run();

        // ����HashMap�ł̏���
        Measure.Method(() =>
        {
            nativeParallelHashMap.Clear();
            for ( int i = 0; i < objectCount; i++ )
            {
                nativeParallelHashMap.Add(gameObjectHashes[i], testData[i]);
            }
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("NativeParallelHashMap", SampleUnit.Millisecond))
        .Run();

        // UnsafeHashMap�ł̏���
        Measure.Method(() =>
        {
            unsafeHashMap.Clear();
            for ( int i = 0; i < objectCount; i++ )
            {
                unsafeHashMap.Add(gameObjectHashes[i], testData[i]);
            }
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("UnsafeHashMap_Standard", SampleUnit.Millisecond))
        .Run();

        // Unsafe����HashMap�ł̏���
        Measure.Method(() =>
        {
            unsafeParallelHashMap.Clear();
            for ( int i = 0; i < objectCount; i++ )
            {
                unsafeParallelHashMap.Add(gameObjectHashes[i], testData[i]);
            }
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .SampleGroup(new SampleGroup("UnsafeParallelHashMap", SampleUnit.Millisecond))
        .Run();
    }

    /// <summary>
    /// �X�g���X�e�X�g - �A����������̑g�ݍ��킹
    /// </summary>
    [Test]
    public void StressTest_MixedOperations()
    {
        // ������������
        standardDictionary.Clear();
        customDictionary.Clear();
        nativeHashMap.Clear();
        unsafeHashMap.Clear();

        // 1. �S�v�f��ǉ�
        for ( int i = 0; i < objectCount; i++ )
        {
            standardDictionary.Add(gameObjects[i], testData[i]);
            customDictionary.Add(gameObjects[i], testData[i]);
            nativeHashMap.Add(gameObjectHashes[i], testData[i]);
            unsafeHashMap.Add(gameObjectHashes[i], testData[i]);
        }

        // 2. �����_���ȃA�N�Z�X
        System.Random rnd = new System.Random(123);
        for ( int i = 0; i < accessTestRuns; i++ )
        {
            int idx = rnd.Next(objectCount);
            bool stdExists = standardDictionary.TryGetValue(gameObjects[idx], out DicTestData stdData);
            bool customExists = customDictionary.TryGetValue(gameObjects[idx], out DicTestData customData, out int index);
            bool nativeExists = nativeHashMap.TryGetValue(gameObjectHashes[idx], out DicTestData nativeData);
            bool unsafeExists = unsafeHashMap.TryGetValue(gameObjectHashes[idx], out DicTestData unsafeData);

            // �S�Ẵf�B�N�V���i���Ō��ʂ���v���邩�m�F
            Assert.AreEqual(stdExists, customExists,
                $"���݃`�F�b�N�s��v: �W��Dictionary({stdExists}) vs CharaDataDic({customExists})");
            Assert.AreEqual(stdExists, nativeExists,
                $"���݃`�F�b�N�s��v: �W��Dictionary({stdExists}) vs NativeHashMap({nativeExists})");
            Assert.AreEqual(stdExists, unsafeExists,
                $"���݃`�F�b�N�s��v: �W��Dictionary({stdExists}) vs UnsafeHashMap({unsafeExists})");

            if ( stdExists && customExists && nativeExists && unsafeExists )
            {
                Assert.AreEqual(stdData.TestValue, customData.TestValue,
                    $"�f�[�^�l�s��v: �W��Dictionary({stdData.TestValue}) vs CharaDataDic({customData.TestValue})");
                Assert.AreEqual(stdData.TestValue, nativeData.TestValue,
                    $"�f�[�^�l�s��v: �W��Dictionary({stdData.TestValue}) vs NativeHashMap({nativeData.TestValue})");
                Assert.AreEqual(stdData.TestValue, unsafeData.TestValue,
                    $"�f�[�^�l�s��v: �W��Dictionary({stdData.TestValue}) vs UnsafeHashMap({unsafeData.TestValue})");
            }
        }

        // 3. �����_���ɗv�f���폜
        int deleteCount = objectCount / 3;
        HashSet<int> deleteIndices = new HashSet<int>();
        while ( deleteIndices.Count < deleteCount )
        {
            deleteIndices.Add(rnd.Next(objectCount));
        }

        foreach ( int idx in deleteIndices )
        {
            standardDictionary.Remove(gameObjects[idx]);
            customDictionary.Remove(gameObjects[idx]);
            nativeHashMap.Remove(gameObjectHashes[idx]);
            unsafeHashMap.Remove(gameObjectHashes[idx]);
        }

        // 4. �����_���ɗv�f��ǉ�
        int addCount = deleteCount / 2;
        for ( int i = 0; i < addCount; i++ )
        {
            int idx = rnd.Next(objectCount);
            if ( standardDictionary.ContainsKey(gameObjects[idx]) )
            {
                continue;
            }

            standardDictionary[gameObjects[idx]] = testData[idx];
            customDictionary.Add(gameObjects[idx], testData[idx]);
            nativeHashMap.Add(gameObjectHashes[idx], testData[idx]);
            unsafeHashMap.Add(gameObjectHashes[idx], testData[idx]);
        }

        // 5. �������m�F - �W��Dictionary�̊e�L�[�����̃R���N�V�����ɂ����݂��邩
        foreach ( var kvp in standardDictionary )
        {
            bool customExists = customDictionary.TryGetValue(kvp.Key, out DicTestData customData, out _);
            bool nativeExists = nativeHashMap.ContainsKey(kvp.Key.GetHashCode());
            bool unsafeExists = unsafeHashMap.ContainsKey(kvp.Key.GetHashCode());

            Assert.IsTrue(customExists, "CharaDataDic�ɕW��Dictionary�̃L�[��������܂���");
            Assert.IsTrue(nativeExists, "NativeHashMap�ɕW��Dictionary�̃L�[��������܂���");
            Assert.IsTrue(unsafeExists, "UnsafeHashMap�ɕW��Dictionary�̃L�[��������܂���");

            if ( customExists && nativeExists && unsafeExists )
            {
                DicTestData nativeData = nativeHashMap[kvp.Key.GetHashCode()];
                DicTestData unsafeData = unsafeHashMap[kvp.Key.GetHashCode()];

                Assert.AreEqual(kvp.Value.TestValue, customData.TestValue, "CharaDataDic�̒l����v���܂���");
                Assert.AreEqual(kvp.Value.TestValue, nativeData.TestValue, "NativeHashMap�̒l����v���܂���");
                Assert.AreEqual(kvp.Value.TestValue, unsafeData.TestValue, "UnsafeHashMap�̒l����v���܂���");
            }
        }
    }

    // �e�X�g�p�̃w���p�[���\�b�h
    private void PrepareForAccessTest()
    {
        standardDictionary.Clear();
        customDictionary.Clear();
        nativeHashMap.Clear();
        unsafeHashMap.Clear();

        for ( int i = 0; i < objectCount; i++ )
        {
            standardDictionary.Add(gameObjects[i], testData[i]);
            customDictionary.Add(gameObjects[i], testData[i]);
            nativeHashMap.Add(gameObjectHashes[i], testData[i]);
            unsafeHashMap.Add(gameObjectHashes[i], testData[i]);
        }
    }
}