using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Profiling;

public class GameManager : MonoBehaviour
{

    private enum TestType
    {
        同期,
        非同期,
        別処理非同期,
        マルチスレッド
    }

    /// <summary>
    /// テストを終了するまでの時間。
    /// </summary>
    [SerializeField]
    private float endTime;

    /// <summary>
    /// 生成するキャラの数。
    /// </summary>
    [SerializeField]
    private int genNumber;

    /// <summary>
    /// 非同期テストをするか。
    /// </summary>
    [SerializeField]
    private TestType caseType;

    /// <summary>
    /// テストごとに生成するオブジェクト
    /// </summary>
    [SerializeField]
    private AssetReference[] objReference;

    /// <summary>
    /// キャラを配置する位置。
    /// </summary>
    [SerializeField]
    private Vector3[] spawnPositions = new Vector3[3];

    /// <summary>
    /// 結果を書きこむファイルパス。
    /// </summary>
    [SerializeField]
    private string resultPath;

    /// <summary>
    /// シングルトンのインスタンス
    /// </summary>
    [HideInInspector]
    public static GameManager instance;

    /// <summary>
    /// 真であればまだテストが続いている。
    /// </summary>
    [HideInInspector]
    public bool isTest = false;

    /// <summary>
    /// 読み取り専用プロパティ。
    /// </summary>
    [HideInInspector]
    public float NowTime => this.nowTime;

    /// <summary>
    /// キャンセルトークン。
    /// </summary>
    [HideInInspector]
    public CancellationTokenSource cToken;

    /// <summary>
    /// 現在の時間
    /// 毎フレーム取得。
    /// </summary>
    private float nowTime;

    /// <summary>
    /// テスト開始時間。<br></br>
    /// 終了時間をはかるために必要。
    /// </summary>
    private float startTime;

    // 処理時間計測
    private Recorder rec;

    // 処理時間のカウント用
    private long totalTime;

    /// <summary>
    /// プロファイラから情報をもらう。
    /// </summary>
   // ProfilerProperty profiler = new UnityEditorInternal.ProfilerProperty();

    /// <summary>
    /// 起動時にシングルトンのインスタンス作成。
    /// </summary>
    private void Awake()
    {
        if ( instance == null )
        {
            instance = this; // this を代入
            DontDestroyOnLoad(this); // シーン遷移時に破棄されないようにする（必要であれば）
                                     // Application.targetFrameRate = 60; // 固定フレームで
        }
        else
        {
            Destroy(this);
        }
    }

    /// <summary>
    /// 初期化処理。
    /// </summary>
    private void Start()
    {
        // 非同期か、同期かで生成するオブジェクトを変更。
        AssetReference reference = this.objReference[(int)this.caseType];

        int posCount = this.spawnPositions.Length;

        for ( int i = 0; i < this.genNumber; i++ )
        {
            // 配置位置に順番に並べていく。
            _ = reference.InstantiateAsync(this.spawnPositions[i % posCount], Quaternion.identity);

            if ( i % 100 == 0 )
            {
                //Debug.Log($"{i / 100}00 オブジェクト生成");
            }
        }

        // キャンセルトークンを作成。
        this.cToken = new CancellationTokenSource();

        // 現在の時間を取得。
        this.nowTime = Time.time; // 現在のゲーム内時間

        this.startTime = Time.time; // 開始時間

        // テスト開始。
        this.isTest = true;

        //Debug.Log($"{genNumber}個のオブジェクトを生成し、{caseType.ToString()}テスト開始。");

        // オブジェクト生成後に観測を有効化
        this.rec = Recorder.Get("PlayerLoop");
        this.rec.enabled = true;
    }

    /// <summary>
    /// 毎フレーム現在の時間を更新する。
    /// </summary>
    private void Update()
    {
        //   //Debug.Log($"テスト中：{GameManager.instance.isTest}");

        this.nowTime = Time.time;

        this.totalTime += this.rec.elapsedNanoseconds * 100;

        // オブジェクト生成時間の分だけ終了時間に加算して終了時間をはかる。
        if ( this.nowTime - this.startTime >= this.endTime )
        {
            // 終了処理開始。
            this.isTest = false;
            this.cToken.Cancel();

            AsyncTestBase[] objects = FindObjectsByType<AsyncTestBase>(sortMode: FindObjectsSortMode.None);

            // 基準となる判断回数の期待値を取得。終了時間を判断間隔で割る。
            int baseCount = (int)Mathf.Floor(this.endTime / objects[0].JudgeInterval);

            // 期待値と実際の判断回数との間の差異を格納する。
            long divide = 0;

            for ( int i = 0; i < objects.Length; i++ )
            {
                // 期待値 - 実判断回数 を加算し続ける。
                divide += baseCount - objects[i].judgeCount;
            }

            // ファイルに新しい行を追加する
            //using ( StreamWriter sw = new StreamWriter(resultPath, true) )
            //{
            //    sw.WriteLine($"テスト区分：{caseType.ToString()}");
            //    sw.WriteLine($"実行日時：{DateTime.Now.ToString()}");
            //    sw.WriteLine($"期待値：{baseCount * genNumber}回 に対して実測値：{divide}回 の差異。");
            //    sw.WriteLine($"平均誤差：{divide / genNumber} オブジェクト数：{genNumber}個");
            //    sw.WriteLine($"総処理時間：{totalTime}");
            //    sw.WriteLine(string.Empty);// 空行
            //}

            UnityEngine.Debug.Log($"テスト区分：{this.caseType.ToString()} 実行日時：{DateTime.Now.ToString()}\n" +
            $"期待値：{baseCount * this.genNumber}回 に対して実測値：{divide}回 の差異。\n" +
            $"平均誤差：{divide / this.genNumber} オブジェクト数：{this.genNumber}個 総処理時間：{this.totalTime}");// 空行

            // テスト終了。
            //EditorApplication.ExitPlaymode();
        }

    }
}
