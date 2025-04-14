using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;
using Unity.Collections;
using Unity.PerformanceTesting;
using System;
using System.ComponentModel;
using System.IO;

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
        gameObjects = new GameObject[objectCount];
        components1 = new DicTestData[objectCount];
        components2 = new MyDicTestComponent[objectCount];

        // �����̃I�u�W�F�N�g�����ŃC���X�^���X��
        var tasks = new List<AsyncOperationHandle<GameObject>>(objectCount);

        for ( int i = 0; i < gameObjects.Length; i++ )
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
        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            gameObjects[i] = tasks[i].Result;
            gameObjects[i].name = $"TestObject_{i}";

            components2[i] = gameObjects[i].GetComponent<MyDicTestComponent>(); // �C�ӂ�MyDicTestComponent�R���|�[�l���g
            components2[i].IDSet();
            components1[i] = components2[i].data;

            // GetComponent����null�ł��邱�Ƃ��m�F
            Assert.IsNotNull(components1[i], $"�I�u�W�F�N�g {i} �� DicTestData ��������܂���");
            Assert.IsNotNull(components2[i], $"�I�u�W�F�N�g {i} �� MyDicTestComponent ��������܂���");
        }

        // �����̏�����
        standardDictionary = new Dictionary<GameObject, DicTestData>(objectCount);
        customDictionary = new CharaDataDic<DicTestData>(objectCount);
        dualDictionary = new CharacterDataDictionary<DicTestData, MyDicTestComponent>(objectCount);

        // �����l���N���A
        dataSum = 0;
    }

    [TearDown]
    public void TearDown()
    {
        // ���������I�u�W�F�N�g�̔j��
        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            if ( gameObjects[i] != null )
            {
                Addressables.ReleaseInstance(gameObjects[i]);
            }
        }

        dataSum = 0;

        PrepareSequentialTest();

        string[] matchingTest = new string[objectCount + 1];
        int unMatchCount = 0;

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            dataSum += standardDictionary[gameObjects[i]].TestValue;
            customSum += customDictionary[gameObjects[i]].TestValue;
            charaSum += dualDictionary[gameObjects[i]].TestValue;

            // ��v���Ȃ��ꍇ
            if ( standardDictionary[gameObjects[i]].TestValue != customDictionary[gameObjects[i]].TestValue
                || dualDictionary[gameObjects[i]].TestValue != customDictionary[gameObjects[i]].TestValue )
            {
                unMatchCount++;
                matchingTest[i + 1] = $"���ʁF�s��v (Dictionary�F{standardDictionary[gameObjects[i]].TestValue}) CharaDataDic�F({customDictionary[gameObjects[i]].TestValue}) CharacterDataDic�F({dualDictionary[gameObjects[i]].TestValue})";
            }
            else
            {
                matchingTest[i + 1] = $"���ʁF��v ({standardDictionary[gameObjects[i]].TestValue})";
            }

        }

        matchingTest[0] = $"�s��v:{unMatchCount} ��";

        string absolutePath = Path.Combine(
Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop\\qiita�L��\\����R���N�V��������\\�������e�X�g2����.txt");

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

        // �����̃N���[���A�b�v
        standardDictionary.Clear();
        customDictionary.Dispose();
        dualDictionary.Dispose();



        // �Ō�ɏo�͂��邱�Ƃŉߏ�œK����h��
        UnityEngine.Debug.Log($"�e�X�g���ʊm�F�p�f�[�^���v: {dataSum}");
    }

    //
    // �v�f�ǉ��e�X�g
    //

    [Test, Performance]
    public void Test_01_Add_StandardDictionary()
    {
        Measure.Method(() =>
        {
            standardDictionary.Clear();
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                standardDictionary.Add(gameObjects[i], components1[i]);
            }
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_02_Add_CustomDictionary()
    {
        Measure.Method(() =>
        {
            customDictionary.Clear();
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                customDictionary.Add(gameObjects[i], components1[i]);
            }
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_03_Add_CharacterDataDictionary()
    {
        Measure.Method(() =>
        {
            dualDictionary.Clear();
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                dualDictionary.Add(gameObjects[i], components1[i], components2[i]);
            }
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    //
    // �����A�N�Z�X�e�X�g
    //

    [Test, Performance]
    public void Test_04_SequentialAccess_StandardDictionary()
    {
        // �e�X�g�O�̏���
        PrepareSequentialTest();

        Measure.Method(() =>
        {
            int localSum = 0;
            // �Q�[���I�u�W�F�N�g����l���擾���āAlocalSum�ɉ��Z���Ă����B
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                DicTestData comp = standardDictionary[gameObjects[i]];
                localSum += comp.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_05_SequentialAccess_CustomDictionary()
    {
        // �e�X�g�O�̏���
        PrepareSequentialTest();

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                DicTestData comp = customDictionary[gameObjects[i]];
                localSum += comp.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_06_SequentialAccess_CharacterDataDictionary()
    {
        // �e�X�g�O�̏���
        PrepareSequentialTest();

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                // T1�^�f�[�^�̂݃A�N�Z�X
                DicTestData comp = dualDictionary[gameObjects[i]];
                localSum += comp.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_07_SequentialAccess_CharacterDataDictionary_T2()
    {
        // �e�X�g�O�̏���
        PrepareSequentialTest();

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                dualDictionary.TryGetValue(gameObjects[i], out MyDicTestComponent comp2, out int index);
                localSum += comp2.data.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    //
    // �����_���A�N�Z�X�e�X�g
    //

    [Test, Performance]
    public void Test_10_RandomAccess_StandardDictionary()
    {
        List<int> randomIndices = PrepareRandomTest();

        // ���O�����F�����ɗv�f��ǉ�
        standardDictionary.Clear();

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            standardDictionary.Add(gameObjects[i], components1[i]);
        }

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                int idx = randomIndices[i];
                DicTestData comp = standardDictionary[gameObjects[idx]];
                localSum += comp.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_11_RandomAccess_CustomDictionary()
    {
        List<int> randomIndices = PrepareRandomTest();

        // ���O�����F�����ɗv�f��ǉ�
        customDictionary.Clear();

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            customDictionary.Add(gameObjects[i], components1[i]);
        }

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                int idx = randomIndices[i];
                DicTestData comp = customDictionary[gameObjects[idx]];
                localSum += comp.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_12_RandomAccess_CharacterDataDictionary()
    {
        List<int> randomIndices = PrepareRandomTest();

        // ���O�����F�����ɗv�f��ǉ�
        dualDictionary.Clear();

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            dualDictionary.Add(gameObjects[i], components1[i], components2[i]);
        }

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                int idx = randomIndices[i];
                dualDictionary.TryGetValue(gameObjects[idx], out DicTestData comp1, out int index);
                localSum += comp1.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_13_RandomAccess_CharacterDataDictionary_T2()
    {
        List<int> randomIndices = PrepareRandomTest();

        // ���O�����F�����ɗv�f��ǉ�
        dualDictionary.Clear();

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            dualDictionary.Add(gameObjects[i], components1[i], components2[i]);
        }

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                int idx = randomIndices[i];
                dualDictionary.TryGetValue(gameObjects[idx], out MyDicTestComponent comp2, out int index);
                localSum += comp2.data.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }


    //
    // �w���p�[���\�b�h
    //

    private void PrepareSequentialTest()
    {
        // ���O�����F�����ɗv�f��ǉ�
        standardDictionary.Clear();
        customDictionary.Clear();
        dualDictionary.Clear();

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            standardDictionary.Add(gameObjects[i], components1[i]);
            customDictionary.Add(gameObjects[i], components1[i]);
            dualDictionary.Add(gameObjects[i], components1[i], components2[i]);
        }
    }

    private List<int> PrepareRandomTest()
    {
        // �V���b�t�������C���f�b�N�X���쐬
        List<int> randomIndices = new List<int>(objectCount);
        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            randomIndices.Add(i);
        }

        // Fisher-Yates �V���b�t��
        System.Random random = new System.Random(42); // �Č����̂��߂ɌŒ�V�[�h
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