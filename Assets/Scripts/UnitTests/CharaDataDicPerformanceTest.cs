using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

public class CharacterDataDictionaryPerformanceTest
{
    private GameObject[] gameObjects;
    private DicTestData[] components1;
    private MyDicTestComponent[] components2; // T2�̃}�l�[�W�h�^�R���|�[�l���g

    // �e�X�g�ݒ�
    private int objectCount = 50000;  // �e�X�g�p�I�u�W�F�N�g��
    private int warmupCount = 3;      // �E�H�[���A�b�v��
    private int measurementCount = 10; // �e�X�g�v����

    private Dictionary<GameObject, DicTestData> standardDictionary;
    private CharaDataDic<DicTestData> customDictionary;
    private CharacterDataDictionary<DicTestData, MyDicTestComponent> dualDictionary;

    // �œK���h�~�p�̕ϐ�
    private long dataSum;
    private long customSum;
    private long charaSum;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // �e�X�g�I�u�W�F�N�g�̃��[�h�Ɛ���
        this.gameObjects = new GameObject[this.objectCount];
        this.components1 = new DicTestData[this.objectCount];
        this.components2 = new MyDicTestComponent[this.objectCount];

        // �����̃I�u�W�F�N�g�����ŃC���X�^���X��
        var tasks = new List<AsyncOperationHandle<GameObject>>(this.objectCount);

        for ( int i = 0; i < this.gameObjects.Length; i++ )
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
        for ( int i = 0; i < this.gameObjects.Length; i++ )
        {
            this.gameObjects[i] = tasks[i].Result;
            this.gameObjects[i].name = $"TestObject_{i}";

            this.components2[i] = this.gameObjects[i].GetComponent<MyDicTestComponent>(); // �C�ӂ�MyDicTestComponent�R���|�[�l���g
            this.components2[i].IDSet();
            this.components1[i] = this.components2[i].data;

            // GetComponent����null�ł��邱�Ƃ��m�F
            Assert.IsNotNull(this.components1[i], $"�I�u�W�F�N�g {i} �� DicTestData ��������܂���");
            Assert.IsNotNull(this.components2[i], $"�I�u�W�F�N�g {i} �� MyDicTestComponent ��������܂���");
        }

        // �����̏�����
        this.standardDictionary = new Dictionary<GameObject, DicTestData>(this.objectCount);
        this.customDictionary = new CharaDataDic<DicTestData>(this.objectCount);
        this.dualDictionary = new CharacterDataDictionary<DicTestData, MyDicTestComponent>(this.objectCount);

        // �����l���N���A
        this.dataSum = 0;
    }

    [TearDown]
    public void TearDown()
    {
        // ���������I�u�W�F�N�g�̔j��
        for ( int i = 0; i < this.gameObjects.Length; i++ )
        {
            if ( this.gameObjects[i] != null )
            {
                _ = Addressables.ReleaseInstance(this.gameObjects[i]);
            }
        }

        this.dataSum = 0;

        this.PrepareSequentialTest();

        string[] matchingTest = new string[this.objectCount + 1];
        int unMatchCount = 0;

        for ( int i = 0; i < this.gameObjects.Length; i++ )
        {
            this.dataSum += this.standardDictionary[this.gameObjects[i]].TestValue;
            this.customSum += this.customDictionary[this.gameObjects[i]].TestValue;
            this.charaSum += this.dualDictionary[this.gameObjects[i]].TestValue;

            // ��v���Ȃ��ꍇ
            if ( this.standardDictionary[this.gameObjects[i]].TestValue != this.customDictionary[this.gameObjects[i]].TestValue
                || this.dualDictionary[this.gameObjects[i]].TestValue != this.customDictionary[this.gameObjects[i]].TestValue )
            {
                unMatchCount++;
                matchingTest[i + 1] = $"���ʁF�s��v (Dictionary�F{this.standardDictionary[this.gameObjects[i]].TestValue}) CharaDataDic�F({this.customDictionary[this.gameObjects[i]].TestValue}) CharacterDataDic�F({this.dualDictionary[this.gameObjects[i]].TestValue})";
            }
            else
            {
                matchingTest[i + 1] = $"���ʁF��v ({this.standardDictionary[this.gameObjects[i]].TestValue})";
            }

        }

        matchingTest[0] = $"�s��v:{unMatchCount} ��";

        string absolutePath = Path.Combine(
Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop\\qiita�L��\\����R���N�V��������\\�������e�X�g2����.txt");

        // �t�@�C���ɐV�����s��ǉ�����
        using ( StreamWriter sw = new(absolutePath, true) )
        {
            // ���ʃT�}���[���o��
            for ( int i = 0; i < matchingTest.Length; i++ )
            {
                sw.WriteLine(matchingTest[i]);
            }

            sw.WriteLine(string.Empty);
        }

        // �����̃N���[���A�b�v
        this.standardDictionary.Clear();
        this.customDictionary.Dispose();
        this.dualDictionary.Dispose();

        // �Ō�ɏo�͂��邱�Ƃŉߏ�œK����h��
        UnityEngine.Debug.Log($"�e�X�g���ʊm�F�p�f�[�^���v: {this.dataSum}");
    }

    //
    // �v�f�ǉ��e�X�g
    //

    [Test, Performance]
    public void Test_01_Add_StandardDictionary()
    {
        Measure.Method(() =>
        {
            this.standardDictionary.Clear();
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                this.standardDictionary.Add(this.gameObjects[i], this.components1[i]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_02_Add_CustomDictionary()
    {
        Measure.Method(() =>
        {
            this.customDictionary.Clear();
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                _ = this.customDictionary.Add(this.gameObjects[i], this.components1[i]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_03_Add_CharacterDataDictionary()
    {
        Measure.Method(() =>
        {
            this.dualDictionary.Clear();
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                _ = this.dualDictionary.Add(this.gameObjects[i], this.components1[i], this.components2[i]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    //
    // �����A�N�Z�X�e�X�g
    //

    [Test, Performance]
    public void Test_04_SequentialAccess_StandardDictionary()
    {
        // �e�X�g�O�̏���
        this.PrepareSequentialTest();

        Measure.Method(() =>
        {
            int localSum = 0;
            // �Q�[���I�u�W�F�N�g����l���擾���āAlocalSum�ɉ��Z���Ă����B
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                DicTestData comp = this.standardDictionary[this.gameObjects[i]];
                localSum += comp.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_05_SequentialAccess_CustomDictionary()
    {
        // �e�X�g�O�̏���
        this.PrepareSequentialTest();

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                DicTestData comp = this.customDictionary[this.gameObjects[i]];
                localSum += comp.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_06_SequentialAccess_CharacterDataDictionary()
    {
        // �e�X�g�O�̏���
        this.PrepareSequentialTest();

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                // T1�^�f�[�^�̂݃A�N�Z�X
                DicTestData comp = this.dualDictionary[this.gameObjects[i]];
                localSum += comp.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_07_SequentialAccess_CharacterDataDictionary_T2()
    {
        // �e�X�g�O�̏���
        this.PrepareSequentialTest();

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                _ = this.dualDictionary.TryGetValue(this.gameObjects[i], out MyDicTestComponent comp2, out int index);
                localSum += comp2.data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    //
    // �����_���A�N�Z�X�e�X�g
    //

    [Test, Performance]
    public void Test_10_RandomAccess_StandardDictionary()
    {
        List<int> randomIndices = this.PrepareRandomTest();

        // ���O�����F�����ɗv�f��ǉ�
        this.standardDictionary.Clear();

        for ( int i = 0; i < this.gameObjects.Length; i++ )
        {
            this.standardDictionary.Add(this.gameObjects[i], this.components1[i]);
        }

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                int idx = randomIndices[i];
                DicTestData comp = this.standardDictionary[this.gameObjects[idx]];
                localSum += comp.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_11_RandomAccess_CustomDictionary()
    {
        List<int> randomIndices = this.PrepareRandomTest();

        // ���O�����F�����ɗv�f��ǉ�
        this.customDictionary.Clear();

        for ( int i = 0; i < this.gameObjects.Length; i++ )
        {
            _ = this.customDictionary.Add(this.gameObjects[i], this.components1[i]);
        }

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                int idx = randomIndices[i];
                DicTestData comp = this.customDictionary[this.gameObjects[idx]];
                localSum += comp.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_12_RandomAccess_CharacterDataDictionary()
    {
        List<int> randomIndices = this.PrepareRandomTest();

        // ���O�����F�����ɗv�f��ǉ�
        this.dualDictionary.Clear();

        for ( int i = 0; i < this.gameObjects.Length; i++ )
        {
            _ = this.dualDictionary.Add(this.gameObjects[i], this.components1[i], this.components2[i]);
        }

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                int idx = randomIndices[i];
                _ = this.dualDictionary.TryGetValue(this.gameObjects[idx], out DicTestData comp1, out int index);
                localSum += comp1.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_13_RandomAccess_CharacterDataDictionary_T2()
    {
        List<int> randomIndices = this.PrepareRandomTest();

        // ���O�����F�����ɗv�f��ǉ�
        this.dualDictionary.Clear();

        for ( int i = 0; i < this.gameObjects.Length; i++ )
        {
            _ = this.dualDictionary.Add(this.gameObjects[i], this.components1[i], this.components2[i]);
        }

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                int idx = randomIndices[i];
                _ = this.dualDictionary.TryGetValue(this.gameObjects[idx], out MyDicTestComponent comp2, out int index);
                localSum += comp2.data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    //
    // �w���p�[���\�b�h
    //

    private void PrepareSequentialTest()
    {
        // ���O�����F�����ɗv�f��ǉ�
        this.standardDictionary.Clear();
        this.customDictionary.Clear();
        this.dualDictionary.Clear();

        for ( int i = 0; i < this.gameObjects.Length; i++ )
        {
            this.standardDictionary.Add(this.gameObjects[i], this.components1[i]);
            _ = this.customDictionary.Add(this.gameObjects[i], this.components1[i]);
            _ = this.dualDictionary.Add(this.gameObjects[i], this.components1[i], this.components2[i]);
        }
    }

    private List<int> PrepareRandomTest()
    {
        // �V���b�t�������C���f�b�N�X���쐬
        List<int> randomIndices = new(this.objectCount);
        for ( int i = 0; i < this.gameObjects.Length; i++ )
        {
            randomIndices.Add(i);
        }

        // Fisher-Yates �V���b�t��
        System.Random random = new(42); // �Č����̂��߂ɌŒ�V�[�h
        for ( int i = randomIndices.Count - 1; i > 0; i-- )
        {
            int j = random.Next(0, i + 1);
            int temp = randomIndices[i];
            randomIndices[i] = randomIndices[j];
            randomIndices[j] = temp;
        }

        return randomIndices;
    }
}