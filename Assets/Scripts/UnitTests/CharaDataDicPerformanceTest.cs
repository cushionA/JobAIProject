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

public class CharaDataDicPerformanceTest
{
    private GameObject[] gameObjects;
    private DicTestData[] components;

    // �e�X�g�ݒ�
    private int objectCount = 50000;  // �e�X�g�p�I�u�W�F�N�g��
    private int warmupCount = 3;      // �E�H�[���A�b�v��
    private int measurementCount = 10; // �e�X�g�v����

    private Dictionary<GameObject, DicTestData> standardDictionary;
    private CharaDataDic<DicTestData> customDictionary;

    // �œK���h�~�p�̕ϐ�
    private int dataSum;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // �e�X�g�I�u�W�F�N�g�̃��[�h�Ɛ���
        gameObjects = new GameObject[objectCount];
        components = new DicTestData[objectCount];

        // �����̃I�u�W�F�N�g�����ŃC���X�^���X��
        var tasks = new List<AsyncOperationHandle<GameObject>>(objectCount);

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            // Addressables���g�p���ăC���X�^���X��
            var task = Addressables.InstantiateAsync("MyDicTest");
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
            components[i] = gameObjects[i].GetComponent<DicTestData>();

            // GetComponent����null�ł��邱�Ƃ��m�F
            Assert.IsNotNull(components[i], $"�I�u�W�F�N�g {i} �� DicTestData ��������܂���");
        }

        // �����̏�����
        standardDictionary = new Dictionary<GameObject, DicTestData>(objectCount);
        customDictionary = new CharaDataDic<DicTestData>(objectCount);

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

        // �����̃N���[���A�b�v
        standardDictionary.Clear();
        customDictionary.Dispose();

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
                standardDictionary.Add(gameObjects[i], components[i]);
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
                customDictionary.Add(gameObjects[i], components[i]);
            }
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    //
    // �����A�N�Z�X�e�X�g
    //

    //[Test, Performance]
    //public void Test_03_SequentialAccess_GetComponent()
    //{
    //    // �e�X�g�O�̏���
    //    PrepareSequentialTest();

    //    Measure.Method(() =>
    //    {
    //        int localSum = 0;
    //        for ( int i = 0; i < gameObjects.Length; i++ )
    //        {
    //            DicTestData comp = gameObjects[i].GetComponent<DicTestData>();
    //            localSum += comp.TestValue;
    //        }
    //        dataSum += localSum;
    //    })
    //    .WarmupCount(warmupCount)
    //    .MeasurementCount(measurementCount)
    //    .Run();
    //}

    [Test, Performance]
    public void Test_04_SequentialAccess_StandardDictionary()
    {
        // �e�X�g�O�̏���
        PrepareSequentialTest();

        Measure.Method(() =>
        {
            int localSum = 0;
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
    public void Test_06_SequentialAccess_CustomDictionary_DirectIndex()
    {
        // �e�X�g�O�̏���
        standardDictionary.Clear();
        customDictionary.Clear();

        List<int> valueIndices = new List<int>(objectCount);

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            standardDictionary.Add(gameObjects[i], components[i]);
            int index = customDictionary.Add(gameObjects[i], components[i]);
            valueIndices.Add(index);
        }

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < valueIndices.Count; i++ )
            {
                int index = valueIndices[i];
                ref DicTestData comp = ref customDictionary.GetDataByIndex(index);
                localSum += comp.TestValue;
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

    //[Test, Performance]
    //public void Test_07_RandomAccess_GetComponent()
    //{
    //    List<int> randomIndices = PrepareRandomTest();

    //    Measure.Method(() =>
    //    {
    //        int localSum = 0;
    //        for ( int i = 0; i < gameObjects.Length; i++ )
    //        {
    //            int idx = randomIndices[i];
    //            DicTestData comp = gameObjects[idx].GetComponent<DicTestData>();
    //            localSum += comp.TestValue;
    //        }
    //        dataSum += localSum;
    //    })
    //    .WarmupCount(warmupCount)
    //    .MeasurementCount(measurementCount)
    //    .Run();
    //}

    [Test, Performance]
    public void Test_08_RandomAccess_StandardDictionary()
    {
        List<int> randomIndices = PrepareRandomTest();

        // ���O�����F�����ɗv�f��ǉ�
        standardDictionary.Clear();

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            standardDictionary.Add(gameObjects[i], components[i]);
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
    public void Test_09_RandomAccess_CustomDictionary()
    {
        List<int> randomIndices = PrepareRandomTest();

        // ���O�����F�����ɗv�f��ǉ�
        customDictionary.Clear();

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            customDictionary.Add(gameObjects[i], components[i]);
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

    ////
    //// �������g�p�ʃe�X�g
    ////

    //[Test, Performance]
    //public void Test_10_MemoryUsage()
    //{
    //    // �������g�p�ʂ̑���ɂ́AGC�ƃX�^�b�N�g���[�X���g�p
    //    // ���ӁF����͊��S�ɐ��m�ł͂���܂��񂪁A�X����c������̂ɖ𗧂��܂�

    //    // �W��Dictionary�̃������g�p��
    //    standardDictionary.Clear();
    //    System.GC.Collect();
    //    System.GC.WaitForPendingFinalizers();
    //    System.GC.Collect();
    //    long beforeStandard = System.GC.GetTotalMemory(true);

    //    for ( int i = 0; i < gameObjects.Length; i++ )
    //    {
    //        standardDictionary.Add(gameObjects[i], components[i]);
    //    }

    //    long afterStandard = System.GC.GetTotalMemory(true);
    //    long standardMemory = afterStandard - beforeStandard;

    //    // �J�X�^��Dictionary�̃������g�p��
    //    customDictionary.Dispose();
    //    customDictionary = new CharaDataDic<DicTestData>(objectCount);
    //    System.GC.Collect();
    //    System.GC.WaitForPendingFinalizers();
    //    System.GC.Collect();
    //    long beforeCustom = System.GC.GetTotalMemory(true);

    //    for ( int i = 0; i < gameObjects.Length; i++ )
    //    {
    //        customDictionary.Add(gameObjects[i], components[i]);
    //    }

    //    long afterCustom = System.GC.GetTotalMemory(true);
    //    long customMemory = afterCustom - beforeCustom;

    //    // ���ʂ�UnityTest��Context�ɋL�^
    //    UnityEngine.Debug.Log($"�I�u�W�F�N�g��: {objectCount}");
    //    UnityEngine.Debug.Log($"�W��Dictionary �������g�p��: {standardMemory} �o�C�g");
    //    UnityEngine.Debug.Log($"�J�X�^��Dictionary �������g�p��: {customMemory} �o�C�g");
    //    UnityEngine.Debug.Log($"�������g�p�ʍ���: {standardMemory - customMemory} �o�C�g");
    //    UnityEngine.Debug.Log($"�������팸��: {(1.0f - (float)customMemory / standardMemory) * 100}%");

    //    // ���ʂ�PerformanceTest.Report�ɋL�^
    //    Measure.Custom("�W��Dictionary(�o�C�g)", standardMemory);
    //    Measure.Custom("�J�X�^��Dictionary(�o�C�g)", customMemory);
    //    Measure.Custom("����(�o�C�g)", standardMemory - customMemory);
    //}

    //
    // �w���p�[���\�b�h
    //

    private void PrepareSequentialTest()
    {
        // ���O�����F�����ɗv�f��ǉ�
        standardDictionary.Clear();
        customDictionary.Clear();

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            standardDictionary.Add(gameObjects[i], components[i]);
            customDictionary.Add(gameObjects[i], components[i]);
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