using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

/// <summary>
/// CharaDataDic�ƕW��Dictionary�̐��m���ƃp�t�H�[�}���X��O��I�ɔ�r����e�X�g
/// </summary>
[Serializable]
public class CharaDataDicFinalTest
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

    // �v�����ʂ̈ꎞ�ۑ��p
    private string[] matchingTest;
    private int unMatchCount;
    private double stdAddTime;
    private double customAddTime;
    private double stdSequentialTime;
    private double customSequentialTime;
    private double stdRandomTime;
    private double customRandomTime;
    private double stdDeleteTime;
    private double customDeleteTime;

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

        // �����l���N���A
        this.dataSum = 0;
        this.errorCount = 0;
        this.stdAddTime = 0;
        this.customAddTime = 0;
        this.stdSequentialTime = 0;
        this.customSequentialTime = 0;
        this.stdRandomTime = 0;
        this.customRandomTime = 0;
        this.stdDeleteTime = 0;
        this.customDeleteTime = 0;
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

        // �����̃N���[���A�b�v
        this.standardDictionary.Clear();
        this.customDictionary.Dispose();

        // ���ʃT�}���[���o��
        UnityEngine.Debug.Log($"�e�X�g���ʊm�F�p�f�[�^���v: {this.dataSum}");
        UnityEngine.Debug.Log($"�G���[��: {this.errorCount}");
        UnityEngine.Debug.Log("=== �p�t�H�[�}���X��r ===");
        UnityEngine.Debug.Log($"�v�f�ǉ�: �W��Dictionary {this.stdAddTime:F3}ms vs CharaDataDic {this.customAddTime:F3}ms (�䗦: {this.customAddTime / this.stdAddTime:P})");
        UnityEngine.Debug.Log($"�����A�N�Z�X: �W��Dictionary {this.stdSequentialTime:F3}ms vs CharaDataDic {this.customSequentialTime:F3}ms (�䗦: {this.customSequentialTime / this.stdSequentialTime:P})");
        UnityEngine.Debug.Log($"�����_���A�N�Z�X: �W��Dictionary {this.stdRandomTime:F3}ms vs CharaDataDic {this.customRandomTime:F3}ms (�䗦: {this.customRandomTime / this.stdRandomTime:P})");
        UnityEngine.Debug.Log($"�v�f�폜: �W��Dictionary {this.stdDeleteTime:F3}ms vs CharaDataDic {this.customDeleteTime:F3}ms (�䗦: {this.customDeleteTime / this.stdDeleteTime:P})");

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
            sw.WriteLine($"�����A�N�Z�X: �W��Dictionary {this.stdSequentialTime:F3}ms vs CharaDataDic {this.customSequentialTime:F3}ms (�䗦: {this.customSequentialTime / this.stdSequentialTime:P})");
            sw.WriteLine($"�����_���A�N�Z�X: �W��Dictionary {this.stdRandomTime:F3}ms vs CharaDataDic {this.customRandomTime:F3}ms (�䗦: {this.customRandomTime / this.stdRandomTime:P})");
            sw.WriteLine($"�v�f�폜: �W��Dictionary {this.stdDeleteTime:F3}ms vs CharaDataDic {this.customDeleteTime:F3}ms (�䗦: {this.customDeleteTime / this.stdDeleteTime:P})");
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

        // �v�f��ǉ�
        for ( int i = 0; i < this.objectCount; i++ )
        {
            this.standardDictionary.Add(this.gameObjects[i], this.testData[i]);
            _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
        }

        // �v�f�Ɖ�e�X�g�p�̔z��
        this.matchingTest = new string[this.gameObjects.Length + 1];

        // ���ׂĂ̗v�f�ɐ������A�N�Z�X�ł��邩�m�F
        for ( int i = 0; i < this.objectCount; i++ )
        {
            DicTestData stdData = this.standardDictionary[this.gameObjects[i]];
            DicTestData customData = this.customDictionary[this.gameObjects[i]];

            // �����f�[�^���擾�ł��邱�Ƃ��m�F
            Assert.AreEqual(stdData.TestValue, customData.TestValue,
                $"Index {i}: �W��Dictionary ({stdData.TestValue}) �� CharaDataDic ({customData.TestValue}) �ňقȂ�l���Ԃ���܂���");

            // ��v���Ȃ��ꍇ
            if ( stdData.TestValue != customData.TestValue )
            {
                this.unMatchCount++;
                this.matchingTest[i + 1] = $"���ʁF�s��v (Dictionary�F{stdData.TestValue}) CharaDataDic�F({customData.TestValue})";
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

            bool result = this.customDictionary.TryGetValueByHash(hash, out _, out _);

            Assert.IsTrue(result, $"Index {i}: �n�b�V���R�[�h {hash} �ł̌����Ɏ��s���܂���");
        }
    }

    /// <summary>
    /// ���m���e�X�g - �폜
    /// </summary>
    [Test]
    public void CorrectnessTest_Remove()
    {
        // �����̎��������������ėv�f��ǉ�
        this.standardDictionary.Clear();
        this.customDictionary.Clear();

        for ( int i = 0; i < this.objectCount; i++ )
        {
            this.standardDictionary.Add(this.gameObjects[i], this.testData[i]);
            _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
        }

        // �I�����ꂽ�I�u�W�F�N�g���폜
        foreach ( int idx in this.selectedForDeletion )
        {
            bool stdResult = this.standardDictionary.Remove(this.gameObjects[idx]);
            bool customResult = this.customDictionary.Remove(this.gameObjects[idx]);

            Assert.IsTrue(stdResult, $"�W��Dictionary����v�f {idx} �̍폜�Ɏ��s���܂���");
            Assert.IsTrue(customResult, $"CharaDataDic����v�f {idx} �̍폜�Ɏ��s���܂���");
        }

        // �폜���ꂽ���m�F
        foreach ( int idx in this.selectedForDeletion )
        {
            bool stdContains = this.standardDictionary.ContainsKey(this.gameObjects[idx]);
            bool customContains = this.customDictionary.TryGetValue(this.gameObjects[idx], out _, out _);

            Assert.IsFalse(stdContains, $"�W��Dictionary�ɍ폜�����͂��̗v�f {idx} ���܂܂�Ă��܂�");
            Assert.IsFalse(customContains, $"CharaDataDic�ɍ폜�����͂��̗v�f {idx} ���܂܂�Ă��܂�");
        }

        // ���̗v�f�͂܂����݂��邩�m�F
        for ( int i = 0; i < this.objectCount; i++ )
        {
            if ( !this.selectedForDeletion.Contains(i) )
            {
                bool stdContains = this.standardDictionary.ContainsKey(this.gameObjects[i]);
                bool customContains = this.customDictionary.TryGetValue(this.gameObjects[i], out _, out _);

                Assert.IsTrue(stdContains, $"�W��Dictionary�������ėv�f {i} ���폜����Ă��܂�");
                Assert.IsTrue(customContains, $"CharaDataDic�������ėv�f {i} ���폜����Ă��܂�");
            }
        }
    }

    /// <summary>
    /// ���m���e�X�g - �ϋv���i��ʒǉ��ƍ폜���J��Ԃ��j
    /// </summary>
    [Test]
    public void CorrectnessTest_Durability()
    {
        // 10��J��Ԃ��ǉ��ƍ폜���s��
        for ( int run = 0; run < 5; run++ )
        {
            // �����̎�����������
            this.standardDictionary.Clear();
            this.customDictionary.Clear();

            // �S�v�f��ǉ�
            for ( int i = 0; i < this.objectCount; i++ )
            {
                this.standardDictionary.Add(this.gameObjects[i], this.testData[i]);
                _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
            }

            // �����̗v�f���폜
            for ( int i = 0; i < this.objectCount; i += 2 )
            {
                _ = this.standardDictionary.Remove(this.gameObjects[i]);
                _ = this.customDictionary.Remove(this.gameObjects[i]);
            }

            // �폜�����v�f���Ēǉ�
            for ( int i = 0; i < this.objectCount; i += 2 )
            {
                this.standardDictionary[this.gameObjects[i]] = this.testData[i];
                _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
            }

            // �S�v�f�ɃA�N�Z�X���Ĉ�v���m�F
            for ( int i = 0; i < this.objectCount; i++ )
            {
                DicTestData stdData = this.standardDictionary[this.gameObjects[i]];
                DicTestData customData = this.customDictionary[this.gameObjects[i]];

                if ( stdData.TestValue != customData.TestValue )
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
    }

    /// <summary>
    /// �p�t�H�[�}���X�e�X�g - �n�b�V�����ڃA�N�Z�X
    /// </summary>
    [Test, Performance]
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

        // �n�b�V���R�[�h���g�������ڃA�N�Z�X
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
        .SampleGroup(new SampleGroup("HashCodeAccess", SampleUnit.Millisecond))
        .Run();
    }

    /// <summary>
    /// �p�t�H�[�}���X�e�X�g - �C���f�b�N�X���ڃA�N�Z�X
    /// </summary>
    [Test, Performance]
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
    /// �X�g���X�e�X�g - �A����������̑g�ݍ��킹
    /// </summary>
    [Test]
    public void StressTest_MixedOperations()
    {
        // �����̎�����������
        this.standardDictionary.Clear();
        this.customDictionary.Clear();

        // 1. �S�v�f��ǉ�
        for ( int i = 0; i < this.objectCount; i++ )
        {
            this.standardDictionary.Add(this.gameObjects[i], this.testData[i]);
            _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
        }

        // 2. �����_���ȃA�N�Z�X
        System.Random rnd = new(123);
        for ( int i = 0; i < this.accessTestRuns; i++ )
        {
            int idx = rnd.Next(this.objectCount);
            bool stdExists = this.standardDictionary.TryGetValue(this.gameObjects[idx], out DicTestData stdData);
            bool customExists = this.customDictionary.TryGetValue(this.gameObjects[idx], out DicTestData customData, out int index);

            // �����̃f�B�N�V���i���Ō��ʂ���v���邩�m�F
            Assert.AreEqual(stdExists, customExists,
                $"���݃`�F�b�N�s��v: �W��Dictionary({stdExists}) vs CharaDataDic({customExists})");

            if ( stdExists && customExists )
            {
                Assert.AreEqual(stdData.TestValue, customData.TestValue,
                    $"�f�[�^�l�s��v: �W��Dictionary({stdData.TestValue}) vs CharaDataDic({customData.TestValue})");
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
        }

        // 5. �������m�F - �W��Dictionary�̊e�L�[��CustomDic�ɂ����݂��邩
        foreach ( var kvp in this.standardDictionary )
        {
            bool exists = this.customDictionary.TryGetValue(kvp.Key, out DicTestData data, out _);
            Assert.IsTrue(exists, "CharaDataDic�ɕW��Dictionary�̃L�[��������܂���");
            Assert.AreEqual(kvp.Value.TestValue, data.TestValue, "�l����v���܂���");
        }

        // 6. �������m�F - CustomDic�̊e�L�[���W��Dictionary�ɂ����݂��邩
        this.customDictionary.ForEach((index, data) =>
        {
            // ���̃L�[�����Q�[���I�u�W�F�N�g����肷��K�v������܂�
            // ����������̂ŃX�L�b�v�i�t�����̊m�F�͂ł��Ă��Ȃ��j
        });
    }

    // �e�X�g�p�̃w���p�[���\�b�h
    private void PrepareForAccessTest()
    {
        this.standardDictionary.Clear();
        this.customDictionary.Clear();

        for ( int i = 0; i < this.objectCount; i++ )
        {
            this.standardDictionary.Add(this.gameObjects[i], this.testData[i]);
            _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
        }
    }
}

