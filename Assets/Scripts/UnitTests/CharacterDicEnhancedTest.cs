using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

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
        this.gameObjects = new GameObject[this.objectCount];
        this.testData = new DicTestData[this.objectCount];
        this.gameObjectHashes = new int[this.objectCount];
        this.selectedForDeletion = new HashSet<int>();

        // �����̃I�u�W�F�N�g�����ŃC���X�^���X��
        var tasks = new List<AsyncOperationHandle<GameObject>>(this.objectCount);

        for ( int i = 0; i < this.objectCount; i++ )
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
        for ( int i = 0; i < this.objectCount; i++ )
        {
            this.gameObjects[i] = tasks[i].Result;
            this.gameObjects[i].name = $"TestObject_{i}";

            var component = this.gameObjects[i].GetComponent<MyDicTestComponent>();
            component.IDSet();
            this.testData[i] = component.data;

            // �n�b�V���R�[�h��ۑ�
            this.gameObjectHashes[i] = this.gameObjects[i].GetHashCode();

            // GetComponent����null�ł��邱�Ƃ��m�F
            Assert.IsNotNull(this.testData[i], $"�I�u�W�F�N�g {i} �� DicTestData ��������܂���");
        }

        // �폜�p�̃I�u�W�F�N�g�������_���ɑI��
        int deleteCount = this.objectCount * this.deleteRatio / 100;
        this.selectedForDeletion.Clear();
        System.Random rnd = new(42); // �Č����̂��߂ɌŒ�V�[�h
        while ( this.selectedForDeletion.Count < deleteCount )
        {
            _ = this.selectedForDeletion.Add(rnd.Next(0, this.objectCount));
        }

        // �����̏�����
        this.standardDictionary = new Dictionary<GameObject, DicTestData>(this.objectCount);
        this.customDictionary = new CharaDataDic<DicTestData>(this.objectCount);

        // Native/Unsafe�R���N�V�����̏�����
        this.nativeHashMap = new NativeHashMap<int, DicTestData>(this.objectCount, Allocator.Persistent);
        this.nativeParallelHashMap = new NativeParallelHashMap<int, DicTestData>(this.objectCount, Allocator.Persistent);
        this.unsafeHashMap = new UnsafeHashMap<int, DicTestData>(this.objectCount, Allocator.Persistent);
        this.unsafeParallelHashMap = new UnsafeParallelHashMap<int, DicTestData>(this.objectCount, Allocator.Persistent);

        // �����l���N���A
        this.dataSum = 0;
        this.errorCount = 0;
        this.stdAddTime = 0;
        this.customAddTime = 0;
        this.nativeAddTime = 0;
        this.unsafeAddTime = 0;
        this.stdSequentialTime = 0;
        this.customSequentialTime = 0;
        this.nativeSequentialTime = 0;
        this.unsafeSequentialTime = 0;
        this.stdRandomTime = 0;
        this.customRandomTime = 0;
        this.nativeRandomTime = 0;
        this.unsafeRandomTime = 0;
        this.stdDeleteTime = 0;
        this.customDeleteTime = 0;
        this.nativeDeleteTime = 0;
        this.unsafeDeleteTime = 0;
    }

    [TearDown]
    public void TearDown()
    {
        // ���������I�u�W�F�N�g�̔j��
        for ( int i = 0; i < this.objectCount; i++ )
        {
            if ( this.gameObjects[i] != null )
            {
                _ = Addressables.ReleaseInstance(this.gameObjects[i]);
            }
        }

        // Native/Unsafe�R���N�V�����̔j��
        if ( this.nativeHashMap.IsCreated )
        {
            this.nativeHashMap.Dispose();
        }

        if ( this.nativeParallelHashMap.IsCreated )
        {
            this.nativeParallelHashMap.Dispose();
        }

        if ( this.unsafeHashMap.IsCreated )
        {
            this.unsafeHashMap.Dispose();
        }

        if ( this.unsafeParallelHashMap.IsCreated )
        {
            this.unsafeParallelHashMap.Dispose();
        }

        // �����̃N���[���A�b�v
        this.standardDictionary.Clear();
        this.customDictionary.Dispose();

        // ���ʃT�}���[���o��
        UnityEngine.Debug.Log($"�e�X�g���ʊm�F�p�f�[�^���v: {this.dataSum}");
        UnityEngine.Debug.Log($"�G���[��: {this.errorCount}");
        UnityEngine.Debug.Log("=== �p�t�H�[�}���X��r ===");
        UnityEngine.Debug.Log($"�v�f�ǉ�: �W��Dictionary {this.stdAddTime:F3}ms vs CharaDataDic {this.customAddTime:F3}ms (�䗦: {this.customAddTime / this.stdAddTime:P})");
        UnityEngine.Debug.Log($"�v�f�ǉ�: �W��Dictionary {this.stdAddTime:F3}ms vs NativeHashMap {this.nativeAddTime:F3}ms (�䗦: {this.nativeAddTime / this.stdAddTime:P})");
        UnityEngine.Debug.Log($"�v�f�ǉ�: �W��Dictionary {this.stdAddTime:F3}ms vs UnsafeHashMap {this.unsafeAddTime:F3}ms (�䗦: {this.unsafeAddTime / this.stdAddTime:P})");
        UnityEngine.Debug.Log($"�����A�N�Z�X: �W��Dictionary {this.stdSequentialTime:F3}ms vs CharaDataDic {this.customSequentialTime:F3}ms (�䗦: {this.customSequentialTime / this.stdSequentialTime:P})");
        UnityEngine.Debug.Log($"�����A�N�Z�X: �W��Dictionary {this.stdSequentialTime:F3}ms vs NativeHashMap {this.nativeSequentialTime:F3}ms (�䗦: {this.nativeSequentialTime / this.stdSequentialTime:P})");
        UnityEngine.Debug.Log($"�����A�N�Z�X: �W��Dictionary {this.stdSequentialTime:F3}ms vs UnsafeHashMap {this.unsafeSequentialTime:F3}ms (�䗦: {this.unsafeSequentialTime / this.stdSequentialTime:P})");
        UnityEngine.Debug.Log($"�����_���A�N�Z�X: �W��Dictionary {this.stdRandomTime:F3}ms vs CharaDataDic {this.customRandomTime:F3}ms (�䗦: {this.customRandomTime / this.stdRandomTime:P})");
        UnityEngine.Debug.Log($"�����_���A�N�Z�X: �W��Dictionary {this.stdRandomTime:F3}ms vs NativeHashMap {this.nativeRandomTime:F3}ms (�䗦: {this.nativeRandomTime / this.stdRandomTime:P})");
        UnityEngine.Debug.Log($"�����_���A�N�Z�X: �W��Dictionary {this.stdRandomTime:F3}ms vs UnsafeHashMap {this.unsafeRandomTime:F3}ms (�䗦: {this.unsafeRandomTime / this.stdRandomTime:P})");
        UnityEngine.Debug.Log($"�v�f�폜: �W��Dictionary {this.stdDeleteTime:F3}ms vs CharaDataDic {this.customDeleteTime:F3}ms (�䗦: {this.customDeleteTime / this.stdDeleteTime:P})");
        UnityEngine.Debug.Log($"�v�f�폜: �W��Dictionary {this.stdDeleteTime:F3}ms vs NativeHashMap {this.nativeDeleteTime:F3}ms (�䗦: {this.nativeDeleteTime / this.stdDeleteTime:P})");
        UnityEngine.Debug.Log($"�v�f�폜: �W��Dictionary {this.stdDeleteTime:F3}ms vs UnsafeHashMap {this.unsafeDeleteTime:F3}ms (�䗦: {this.unsafeDeleteTime / this.stdDeleteTime:P})");

        string absolutePath = Path.Combine(
Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop\\qiita�L��\\����R���N�V��������\\���\�e�X�g����.txt");

        // �t�@�C���ɐV�����s��ǉ�����
        using ( StreamWriter sw = new(absolutePath, true) )
        {
            // ���ʃT�}���[���o��
            sw.WriteLine($"�e�X�g���ʊm�F�p�f�[�^���v: {this.dataSum}");
            sw.WriteLine($"�G���[��: {this.errorCount}");
            sw.WriteLine("=== �p�t�H�[�}���X��r ===");
            sw.WriteLine($"�v�f�ǉ�: �W��Dictionary {this.stdAddTime:F3}ms vs CharaDataDic {this.customAddTime:F3}ms (�䗦: {this.customAddTime / this.stdAddTime:P})");
            sw.WriteLine($"�v�f�ǉ�: �W��Dictionary {this.stdAddTime:F3}ms vs NativeHashMap {this.nativeAddTime:F3}ms (�䗦: {this.nativeAddTime / this.stdAddTime:P})");
            sw.WriteLine($"�v�f�ǉ�: �W��Dictionary {this.stdAddTime:F3}ms vs UnsafeHashMap {this.unsafeAddTime:F3}ms (�䗦: {this.unsafeAddTime / this.stdAddTime:P})");
            sw.WriteLine($"�����A�N�Z�X: �W��Dictionary {this.stdSequentialTime:F3}ms vs CharaDataDic {this.customSequentialTime:F3}ms (�䗦: {this.customSequentialTime / this.stdSequentialTime:P})");
            sw.WriteLine($"�����A�N�Z�X: �W��Dictionary {this.stdSequentialTime:F3}ms vs NativeHashMap {this.nativeSequentialTime:F3}ms (�䗦: {this.nativeSequentialTime / this.stdSequentialTime:P})");
            sw.WriteLine($"�����A�N�Z�X: �W��Dictionary {this.stdSequentialTime:F3}ms vs UnsafeHashMap {this.unsafeSequentialTime:F3}ms (�䗦: {this.unsafeSequentialTime / this.stdSequentialTime:P})");
            sw.WriteLine($"�����_���A�N�Z�X: �W��Dictionary {this.stdRandomTime:F3}ms vs CharaDataDic {this.customRandomTime:F3}ms (�䗦: {this.customRandomTime / this.stdRandomTime:P})");
            sw.WriteLine($"�����_���A�N�Z�X: �W��Dictionary {this.stdRandomTime:F3}ms vs NativeHashMap {this.nativeRandomTime:F3}ms (�䗦: {this.nativeRandomTime / this.stdRandomTime:P})");
            sw.WriteLine($"�����_���A�N�Z�X: �W��Dictionary {this.stdRandomTime:F3}ms vs UnsafeHashMap {this.unsafeRandomTime:F3}ms (�䗦: {this.unsafeRandomTime / this.stdRandomTime:P})");
            sw.WriteLine($"�v�f�폜: �W��Dictionary {this.stdDeleteTime:F3}ms vs CharaDataDic {this.customDeleteTime:F3}ms (�䗦: {this.customDeleteTime / this.stdDeleteTime:P})");
            sw.WriteLine($"�v�f�폜: �W��Dictionary {this.stdDeleteTime:F3}ms vs NativeHashMap {this.nativeDeleteTime:F3}ms (�䗦: {this.nativeDeleteTime / this.stdDeleteTime:P})");
            sw.WriteLine($"�v�f�폜: �W��Dictionary {this.stdDeleteTime:F3}ms vs UnsafeHashMap {this.unsafeDeleteTime:F3}ms (�䗦: {this.unsafeDeleteTime / this.stdDeleteTime:P})");
            sw.WriteLine(string.Empty);
        }

        absolutePath = Path.Combine(
Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop\\qiita�L��\\����R���N�V��������\\�������e�X�g����.txt");

        // �t�@�C���ɐV�����s��ǉ�����
        using ( StreamWriter sw = new(absolutePath, true) )
        {
            // ���ʃT�}���[���o��
            for ( int i = 0; i < this.matchingTest.Length; i++ )
            {
                sw.WriteLine(this.matchingTest[i]);
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
        this.standardDictionary.Clear();
        this.customDictionary.Clear();
        this.nativeHashMap.Clear();
        this.unsafeHashMap.Clear();

        // �v�f��ǉ�
        for ( int i = 0; i < this.objectCount; i++ )
        {
            this.standardDictionary.Add(this.gameObjects[i], this.testData[i]);
            _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
            this.nativeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            this.unsafeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
        }

        // �v�f�Ɖ�e�X�g�p�̔z��
        this.matchingTest = new string[this.gameObjects.Length + 1];

        // ���ׂĂ̗v�f�ɐ������A�N�Z�X�ł��邩�m�F
        for ( int i = 0; i < this.objectCount; i++ )
        {
            DicTestData stdData = this.standardDictionary[this.gameObjects[i]];
            DicTestData customData = this.customDictionary[this.gameObjects[i]];
            DicTestData nativeData = this.nativeHashMap[this.gameObjectHashes[i]];
            DicTestData unsafeData = this.unsafeHashMap[this.gameObjectHashes[i]];

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
                this.unMatchCount++;
                this.matchingTest[i + 1] = $"���ʁF�s��v (Dictionary�F{stdData.TestValue}) CharaDataDic�F({customData.TestValue}) NativeHashMap�F({nativeData.TestValue}) UnsafeHashMap�F({unsafeData.TestValue})";
            }
            else
            {
                this.matchingTest[i + 1] = $"���ʁF��v ({stdData.TestValue})";
            }
        }

        this.matchingTest[0] = $"�s��v:{this.unMatchCount} ��";

        // �n�b�V���R�[�h�ɂ��A�N�Z�X���m�F
        for ( int i = 0; i < this.objectCount; i++ )
        {
            int hash = this.gameObjectHashes[i];

            bool custResult = this.customDictionary.TryGetValueByHash(hash, out _, out _);

            bool nativeResult = this.nativeHashMap.TryGetValue(hash, out _);

            bool unsafeResult = this.unsafeHashMap.TryGetValue(hash, out _);

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
        this.standardDictionary.Clear();
        this.customDictionary.Clear();
        this.nativeHashMap.Clear();
        this.unsafeHashMap.Clear();

        for ( int i = 0; i < this.objectCount; i++ )
        {
            this.standardDictionary.Add(this.gameObjects[i], this.testData[i]);
            _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
            this.nativeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            this.unsafeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
        }

        // �I�����ꂽ�I�u�W�F�N�g���폜
        foreach ( int idx in this.selectedForDeletion )
        {
            bool stdResult = this.standardDictionary.Remove(this.gameObjects[idx]);
            bool customResult = this.customDictionary.Remove(this.gameObjects[idx]);
            bool nativeResult = this.nativeHashMap.Remove(this.gameObjectHashes[idx]);
            bool unsafeResult = this.unsafeHashMap.Remove(this.gameObjectHashes[idx]);

            Assert.IsTrue(stdResult, $"�W��Dictionary����v�f {idx} �̍폜�Ɏ��s���܂���");
            Assert.IsTrue(customResult, $"CharaDataDic����v�f {idx} �̍폜�Ɏ��s���܂���");
            Assert.IsTrue(nativeResult, $"NativeHashMap����v�f {idx} �̍폜�Ɏ��s���܂���");
            Assert.IsTrue(unsafeResult, $"UnsafeHashMap����v�f {idx} �̍폜�Ɏ��s���܂���");
        }

        // �폜���ꂽ���m�F
        foreach ( int idx in this.selectedForDeletion )
        {
            bool stdContains = this.standardDictionary.ContainsKey(this.gameObjects[idx]);
            bool customContains = this.customDictionary.TryGetValue(this.gameObjects[idx], out _, out _);
            bool nativeContains = this.nativeHashMap.ContainsKey(this.gameObjectHashes[idx]);
            bool unsafeContains = this.unsafeHashMap.ContainsKey(this.gameObjectHashes[idx]);

            Assert.IsFalse(stdContains, $"�W��Dictionary�ɍ폜�����͂��̗v�f {idx} ���܂܂�Ă��܂�");
            Assert.IsFalse(customContains, $"CharaDataDic�ɍ폜�����͂��̗v�f {idx} ���܂܂�Ă��܂�");
            Assert.IsFalse(nativeContains, $"NativeHashMap�ɍ폜�����͂��̗v�f {idx} ���܂܂�Ă��܂�");
            Assert.IsFalse(unsafeContains, $"UnsafeHashMap�ɍ폜�����͂��̗v�f {idx} ���܂܂�Ă��܂�");
        }

        // ���̗v�f�͂܂����݂��邩�m�F
        for ( int i = 0; i < this.objectCount; i++ )
        {
            if ( !this.selectedForDeletion.Contains(i) )
            {
                bool stdContains = this.standardDictionary.ContainsKey(this.gameObjects[i]);
                bool customContains = this.customDictionary.TryGetValue(this.gameObjects[i], out _, out _);
                bool nativeContains = this.nativeHashMap.ContainsKey(this.gameObjectHashes[i]);
                bool unsafeContains = this.unsafeHashMap.ContainsKey(this.gameObjectHashes[i]);

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
            this.standardDictionary.Clear();
            this.customDictionary.Clear();
            this.nativeHashMap.Clear();
            this.unsafeHashMap.Clear();

            // �S�v�f��ǉ�
            for ( int i = 0; i < this.objectCount; i++ )
            {
                this.standardDictionary.Add(this.gameObjects[i], this.testData[i]);
                _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
                this.nativeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
                this.unsafeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            }

            // �����̗v�f���폜
            for ( int i = 0; i < this.objectCount; i += 2 )
            {
                _ = this.standardDictionary.Remove(this.gameObjects[i]);
                _ = this.customDictionary.Remove(this.gameObjects[i]);
                _ = this.nativeHashMap.Remove(this.gameObjectHashes[i]);
                _ = this.unsafeHashMap.Remove(this.gameObjectHashes[i]);
            }

            // �폜�����v�f���Ēǉ�
            for ( int i = 0; i < this.objectCount; i += 2 )
            {
                this.standardDictionary[this.gameObjects[i]] = this.testData[i];
                _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
                this.nativeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
                this.unsafeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            }

            // �S�v�f�ɃA�N�Z�X���Ĉ�v���m�F
            for ( int i = 0; i < this.objectCount; i++ )
            {
                DicTestData stdData = this.standardDictionary[this.gameObjects[i]];
                DicTestData customData = this.customDictionary[this.gameObjects[i]];
                DicTestData nativeData = this.nativeHashMap[this.gameObjectHashes[i]];
                DicTestData unsafeData = this.unsafeHashMap[this.gameObjectHashes[i]];

                if ( stdData.TestValue != customData.TestValue ||
                     stdData.TestValue != nativeData.TestValue ||
                     stdData.TestValue != unsafeData.TestValue )
                {
                    this.errorCount++;
                }
            }
        }

        Assert.AreEqual(0, this.errorCount, "�J��Ԃ��̒ǉ��폜�e�X�g���ɃG���[���������܂���");
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
            this.standardDictionary.Clear();
            for ( int i = 0; i < this.objectCount; i++ )
            {
                this.standardDictionary.Add(this.gameObjects[i], this.testData[i]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("StandardDictionary_Add", SampleUnit.Millisecond))
        .Run();

        // �W��Dictionary�̌��ʂ�ۑ�
        this.stdAddTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "StandardDictionary_Add")?.Median ?? 0;

        // CharaDataDic�ǉ��e�X�g
        Measure.Method(() =>
        {
            this.customDictionary.Clear();
            for ( int i = 0; i < this.objectCount; i++ )
            {
                _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("CharaDataDic_Add", SampleUnit.Millisecond))
        .Run();

        // CharaDataDic�̌��ʂ�ۑ�
        this.customAddTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "CharaDataDic_Add")?.Median ?? 0;

        // NativeHashMap�ǉ��e�X�g
        Measure.Method(() =>
        {
            this.nativeHashMap.Clear();
            for ( int i = 0; i < this.objectCount; i++ )
            {
                this.nativeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("NativeHashMap_Add", SampleUnit.Millisecond))
        .Run();

        // NativeHashMap�̌��ʂ�ۑ�
        this.nativeAddTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "NativeHashMap_Add")?.Median ?? 0;

        // UnsafeHashMap�ǉ��e�X�g
        Measure.Method(() =>
        {
            this.unsafeHashMap.Clear();
            for ( int i = 0; i < this.objectCount; i++ )
            {
                this.unsafeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("UnsafeHashMap_Add", SampleUnit.Millisecond))
        .Run();

        // UnsafeHashMap�̌��ʂ�ۑ�
        this.unsafeAddTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "UnsafeHashMap_Add")?.Median ?? 0;
    }

    /// <summary>
    /// �p�t�H�[�}���X�e�X�g - �A���A�N�Z�X
    /// </summary>
    [Test, Performance]
    public void PerformanceTest_SequentialAccess()
    {
        // ���O�����F�����ɗv�f��ǉ�
        this.PrepareForAccessTest();

        // �W��Dictionary�A���A�N�Z�X�e�X�g
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < this.accessTestRuns; run++ )
            {
                int idx = run % this.objectCount;
                DicTestData data = this.standardDictionary[this.gameObjects[idx]];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("StandardDictionary_Sequential", SampleUnit.Millisecond))
        .Run();

        // �W��Dictionary�̌��ʂ�ۑ�
        this.stdSequentialTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "StandardDictionary_Sequential")?.Median ?? 0;

        // CharaDataDic�A���A�N�Z�X�e�X�g
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < this.accessTestRuns; run++ )
            {
                int idx = run % this.objectCount;
                DicTestData data = this.customDictionary[this.gameObjects[idx]];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("CharaDataDic_Sequential", SampleUnit.Millisecond))
        .Run();

        // CharaDataDic�̌��ʂ�ۑ�
        this.customSequentialTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "CharaDataDic_Sequential")?.Median ?? 0;

        // NativeHashMap�A���A�N�Z�X�e�X�g
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < this.accessTestRuns; run++ )
            {
                int idx = run % this.objectCount;
                DicTestData data = this.nativeHashMap[this.gameObjectHashes[idx]];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("NativeHashMap_Sequential", SampleUnit.Millisecond))
        .Run();

        // NativeHashMap�̌��ʂ�ۑ�
        this.nativeSequentialTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "NativeHashMap_Sequential")?.Median ?? 0;

        // UnsafeHashMap�A���A�N�Z�X�e�X�g
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < this.accessTestRuns; run++ )
            {
                int idx = run % this.objectCount;
                DicTestData data = this.unsafeHashMap[this.gameObjectHashes[idx]];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("UnsafeHashMap_Sequential", SampleUnit.Millisecond))
        .Run();

        // UnsafeHashMap�̌��ʂ�ۑ�
        this.unsafeSequentialTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "UnsafeHashMap_Sequential")?.Median ?? 0;
    }

    /// <summary>
    /// �p�t�H�[�}���X�e�X�g - �����_���A�N�Z�X
    /// </summary>
    [Test, Performance]
    public void PerformanceTest_RandomAccess()
    {
        // ���O�����F�����ɗv�f��ǉ�
        this.PrepareForAccessTest();

        // �����_���C���f�b�N�X�𐶐�
        System.Random rnd = new(42);
        int[] randomIndices = new int[this.accessTestRuns];
        for ( int i = 0; i < this.accessTestRuns; i++ )
        {
            randomIndices[i] = rnd.Next(0, this.objectCount);
        }

        // �W��Dictionary�����_���A�N�Z�X�e�X�g
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < this.accessTestRuns; run++ )
            {
                int idx = randomIndices[run];
                DicTestData data = this.standardDictionary[this.gameObjects[idx]];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("StandardDictionary_Random", SampleUnit.Millisecond))
        .Run();

        // �W��Dictionary�̌��ʂ�ۑ�
        this.stdRandomTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "StandardDictionary_Random")?.Median ?? 0;

        // CharaDataDic�����_���A�N�Z�X�e�X�g
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < this.accessTestRuns; run++ )
            {
                int idx = randomIndices[run];
                DicTestData data = this.customDictionary[this.gameObjects[idx]];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("CharaDataDic_Random", SampleUnit.Millisecond))
        .Run();

        // CharaDataDic�̌��ʂ�ۑ�
        this.customRandomTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "CharaDataDic_Random")?.Median ?? 0;

        // NativeHashMap�����_���A�N�Z�X�e�X�g
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < this.accessTestRuns; run++ )
            {
                int idx = randomIndices[run];
                DicTestData data = this.nativeHashMap[this.gameObjectHashes[idx]];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("NativeHashMap_Random", SampleUnit.Millisecond))
        .Run();

        // NativeHashMap�̌��ʂ�ۑ�
        this.nativeRandomTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "NativeHashMap_Random")?.Median ?? 0;

        // UnsafeHashMap�����_���A�N�Z�X�e�X�g
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < this.accessTestRuns; run++ )
            {
                int idx = randomIndices[run];
                DicTestData data = this.unsafeHashMap[this.gameObjectHashes[idx]];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("UnsafeHashMap_Random", SampleUnit.Millisecond))
        .Run();

        // UnsafeHashMap�̌��ʂ�ۑ�
        this.unsafeRandomTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "UnsafeHashMap_Random")?.Median ?? 0;
    }

    /// <summary>
    /// �p�t�H�[�}���X�e�X�g - �v�f�폜
    /// </summary>
    [Test, Performance]
    public void PerformanceTest_Remove()
    {
        // �W��Dictionary�폜�e�X�g
        this.PrepareForAccessTest();
        Measure.Method(() =>
        {
            foreach ( int idx in this.selectedForDeletion )
            {
                _ = this.standardDictionary.Remove(this.gameObjects[idx]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("StandardDictionary_Remove", SampleUnit.Millisecond))
        .Run();

        // �W��Dictionary�̌��ʂ�ۑ�
        this.stdDeleteTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "StandardDictionary_Remove")?.Median ?? 0;

        // CharaDataDic�폜�e�X�g
        this.PrepareForAccessTest();
        Measure.Method(() =>
        {
            foreach ( int idx in this.selectedForDeletion )
            {
                _ = this.customDictionary.Remove(this.gameObjects[idx]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("CharaDataDic_Remove", SampleUnit.Millisecond))
        .Run();

        // CharaDataDic�̌��ʂ�ۑ�
        this.customDeleteTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "CharaDataDic_Remove")?.Median ?? 0;

        // NativeHashMap�폜�e�X�g
        this.PrepareForAccessTest();
        Measure.Method(() =>
        {
            foreach ( int idx in this.selectedForDeletion )
            {
                _ = this.nativeHashMap.Remove(this.gameObjectHashes[idx]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("NativeHashMap_Remove", SampleUnit.Millisecond))
        .Run();

        // NativeHashMap�̌��ʂ�ۑ�
        this.nativeDeleteTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "NativeHashMap_Remove")?.Median ?? 0;

        // UnsafeHashMap�폜�e�X�g
        this.PrepareForAccessTest();
        Measure.Method(() =>
        {
            foreach ( int idx in this.selectedForDeletion )
            {
                _ = this.unsafeHashMap.Remove(this.gameObjectHashes[idx]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("UnsafeHashMap_Remove", SampleUnit.Millisecond))
        .Run();

        // UnsafeHashMap�̌��ʂ�ۑ�
        this.unsafeDeleteTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "UnsafeHashMap_Remove")?.Median ?? 0;
    }

    /// <summary>
    /// �p�t�H�[�}���X�e�X�g - �n�b�V�����ڃA�N�Z�X
    /// </summary>
  // [Test, Performance]
    public void PerformanceTest_HashCodeAccess()
    {
        // ���O�����F�����ɗv�f��ǉ�
        this.PrepareForAccessTest();

        // �Q�[���I�u�W�F�N�g�̃n�b�V���R�[�h�����W
        int[] hashes = new int[this.objectCount];
        for ( int i = 0; i < this.objectCount; i++ )
        {
            hashes[i] = this.gameObjects[i].GetHashCode();
        }

        // �ʏ�̕��@�ŃA�N�Z�X�i�Q�Ɗ�j
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int i = 0; i < this.accessTestRuns; i++ )
            {
                int idx = i % this.objectCount;
                DicTestData data = this.customDictionary[this.gameObjects[idx]];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("GameObjectAccess", SampleUnit.Millisecond))
        .Run();

        // CharaDataDic�̃n�b�V���R�[�h���g�������ڃA�N�Z�X
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int i = 0; i < this.accessTestRuns; i++ )
            {
                int idx = i % this.objectCount;
                int hash = hashes[idx];
                _ = this.customDictionary.TryGetValueByHash(hash, out DicTestData data, out _);
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("CharaDataDic_HashCodeAccess", SampleUnit.Millisecond))
        .Run();

        // NativeHashMap�̃L�[�Ƃ��Ẵn�b�V���R�[�h�A�N�Z�X
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int i = 0; i < this.accessTestRuns; i++ )
            {
                int idx = i % this.objectCount;
                int hash = hashes[idx];
                DicTestData data = this.nativeHashMap[hash];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("NativeHashMap_HashCodeAccess", SampleUnit.Millisecond))
        .Run();

        // UnsafeHashMap�̃L�[�Ƃ��Ẵn�b�V���R�[�h�A�N�Z�X
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int i = 0; i < this.accessTestRuns; i++ )
            {
                int idx = i % this.objectCount;
                int hash = hashes[idx];
                DicTestData data = this.unsafeHashMap[hash];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
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
        this.PrepareForAccessTest();

        // �C���f�b�N�X�����W
        List<int> indices = new(this.objectCount);
        for ( int i = 0; i < this.objectCount; i++ )
        {
            int index = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
            indices.Add(index);
        }

        // �ʏ�̕��@�ŃA�N�Z�X�i�Q�Ɗ�j
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int i = 0; i < this.accessTestRuns; i++ )
            {
                int idx = i % this.objectCount;
                DicTestData data = this.customDictionary[this.gameObjects[idx]];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("NormalAccess", SampleUnit.Millisecond))
        .Run();

        // �C���f�b�N�X�ł̒��ڃA�N�Z�X
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int i = 0; i < this.accessTestRuns; i++ )
            {
                int idx = i % this.objectCount;
                int valueIndex = indices[idx];
                ref DicTestData data = ref this.customDictionary.GetDataByIndex(valueIndex);
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
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
            this.nativeHashMap.Clear();
            for ( int i = 0; i < this.objectCount; i++ )
            {
                this.nativeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("NativeHashmap", SampleUnit.Millisecond))
        .Run();

        // ����HashMap�ł̏���
        Measure.Method(() =>
        {
            this.nativeParallelHashMap.Clear();
            for ( int i = 0; i < this.objectCount; i++ )
            {
                this.nativeParallelHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("NativeParallelHashMap", SampleUnit.Millisecond))
        .Run();

        // UnsafeHashMap�ł̏���
        Measure.Method(() =>
        {
            this.unsafeHashMap.Clear();
            for ( int i = 0; i < this.objectCount; i++ )
            {
                this.unsafeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("UnsafeHashMap_Standard", SampleUnit.Millisecond))
        .Run();

        // Unsafe����HashMap�ł̏���
        Measure.Method(() =>
        {
            this.unsafeParallelHashMap.Clear();
            for ( int i = 0; i < this.objectCount; i++ )
            {
                this.unsafeParallelHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
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
        this.standardDictionary.Clear();
        this.customDictionary.Clear();
        this.nativeHashMap.Clear();
        this.unsafeHashMap.Clear();

        // 1. �S�v�f��ǉ�
        for ( int i = 0; i < this.objectCount; i++ )
        {
            this.standardDictionary.Add(this.gameObjects[i], this.testData[i]);
            _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
            this.nativeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            this.unsafeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
        }

        // 2. �����_���ȃA�N�Z�X
        System.Random rnd = new(123);
        for ( int i = 0; i < this.accessTestRuns; i++ )
        {
            int idx = rnd.Next(this.objectCount);
            bool stdExists = this.standardDictionary.TryGetValue(this.gameObjects[idx], out DicTestData stdData);

            bool customExists = this.customDictionary.TryGetValue(this.gameObjects[idx], out DicTestData customData, out _);
            bool nativeExists = this.nativeHashMap.TryGetValue(this.gameObjectHashes[idx], out DicTestData nativeData);
            bool unsafeExists = this.unsafeHashMap.TryGetValue(this.gameObjectHashes[idx], out DicTestData unsafeData);

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
        int deleteCount = this.objectCount / 3;
        HashSet<int> deleteIndices = new();
        while ( deleteIndices.Count < deleteCount )
        {
            _ = deleteIndices.Add(rnd.Next(this.objectCount));
        }

        foreach ( int idx in deleteIndices )
        {
            _ = this.standardDictionary.Remove(this.gameObjects[idx]);
            _ = this.customDictionary.Remove(this.gameObjects[idx]);
            _ = this.nativeHashMap.Remove(this.gameObjectHashes[idx]);
            _ = this.unsafeHashMap.Remove(this.gameObjectHashes[idx]);
        }

        // 4. �����_���ɗv�f��ǉ�
        int addCount = deleteCount / 2;
        for ( int i = 0; i < addCount; i++ )
        {
            int idx = rnd.Next(this.objectCount);
            if ( this.standardDictionary.ContainsKey(this.gameObjects[idx]) )
            {
                continue;
            }

            this.standardDictionary[this.gameObjects[idx]] = this.testData[idx];
            _ = this.customDictionary.Add(this.gameObjects[idx], this.testData[idx]);
            this.nativeHashMap.Add(this.gameObjectHashes[idx], this.testData[idx]);
            this.unsafeHashMap.Add(this.gameObjectHashes[idx], this.testData[idx]);
        }

        // 5. �������m�F - �W��Dictionary�̊e�L�[�����̃R���N�V�����ɂ����݂��邩
        foreach ( var kvp in this.standardDictionary )
        {
            bool customExists = this.customDictionary.TryGetValue(kvp.Key, out DicTestData customData, out _);
            bool nativeExists = this.nativeHashMap.ContainsKey(kvp.Key.GetHashCode());
            bool unsafeExists = this.unsafeHashMap.ContainsKey(kvp.Key.GetHashCode());

            Assert.IsTrue(customExists, "CharaDataDic�ɕW��Dictionary�̃L�[��������܂���");
            Assert.IsTrue(nativeExists, "NativeHashMap�ɕW��Dictionary�̃L�[��������܂���");
            Assert.IsTrue(unsafeExists, "UnsafeHashMap�ɕW��Dictionary�̃L�[��������܂���");

            if ( customExists && nativeExists && unsafeExists )
            {
                DicTestData nativeData = this.nativeHashMap[kvp.Key.GetHashCode()];
                DicTestData unsafeData = this.unsafeHashMap[kvp.Key.GetHashCode()];

                Assert.AreEqual(kvp.Value.TestValue, customData.TestValue, "CharaDataDic�̒l����v���܂���");
                Assert.AreEqual(kvp.Value.TestValue, nativeData.TestValue, "NativeHashMap�̒l����v���܂���");
                Assert.AreEqual(kvp.Value.TestValue, unsafeData.TestValue, "UnsafeHashMap�̒l����v���܂���");
            }
        }
    }

    // �e�X�g�p�̃w���p�[���\�b�h
    private void PrepareForAccessTest()
    {
        this.standardDictionary.Clear();
        this.customDictionary.Clear();
        this.nativeHashMap.Clear();
        this.unsafeHashMap.Clear();

        for ( int i = 0; i < this.objectCount; i++ )
        {
            this.standardDictionary.Add(this.gameObjects[i], this.testData[i]);
            _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
            this.nativeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            this.unsafeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
        }
    }
}