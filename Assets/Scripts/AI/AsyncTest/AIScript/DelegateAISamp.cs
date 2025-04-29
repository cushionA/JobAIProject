using System;
using UnityEngine;

public class DelegateAISamp : MonoBehaviour
{
    #region 定義
    /// <summary>
    /// 攻撃する際の条件判断の基準となる設定の列挙型。
    /// </summary>
    private enum JudgeConditions
    {
        一定距離内にいるかで判定 = 0,// 一定以内の距離にいる敵を攻撃
        HP量で判定 = 1,// HPが一定以上か
        MP量で判定 = 2,// MPが一定以上か
        攻撃力で判定 = 3 // 攻撃力が一定以上か 
    }

    /// <summary>
    /// 位置やHPなど、敵のデータを格納するクラス。<br></br>
    /// 今回は中身がないダミー。
    /// </summary>
    public class TargetData
    {

    }
    #endregion 定義

    #region インスタンス変数
    /// <summary>
    /// このAIがどんな条件で攻撃を行うか、という設定。<br></br>
    /// インスペクタから入れる想定。<br></br>
    /// たとえばこの変数の値が 一定距離内にいるかで判定 であれば距離が一定以内のものを攻撃する。
    /// </summary>
    [SerializeField]
    private JudgeConditions useCondition;

    /// <summary>
    /// デリゲートの配列。<br></br>
    /// ここに判断処理を格納し、JudgeConditions列挙子の値でアクセスできるようにする。
    /// </summary>
    private Func<TargetData[], TargetData>[] judgeArray = new Func<TargetData[], TargetData>[4];

    #endregion インスタンス変数

    #region 処理

    /// <summary>
    /// 初期化処理<br></br>
    /// デリゲート配列を作成。
    /// </summary>
    private void Start()
    {

        // 各条件に対応するローカルメソッドを作成し、デリゲート配列に格納する。

        // 距離判定用のローカルメソッド。
        // JudgeConditions.一定距離内にいるかで判定 に対応（処理内容はダミー）
        TargetData distanceJudge(TargetData[] tData)
        {
            return tData[0];
        }

        // HP判定用のローカルメソッド。
        // JudgeConditions.HP量で判定 に対応（処理内容はダミー）
        TargetData hpJudge(TargetData[] tData)
        {
            return tData[0];
        }

        // MP判定用のローカルメソッド。
        // JudgeConditions.MP量で判定 に対応（処理内容はダミー）
        TargetData mpJudge(TargetData[] tData)
        {
            return tData[0];
        }

        // 攻撃力判定用のローカルメソッド。
        // JudgeConditions.攻撃力で判定 に対応（処理内容はダミー）
        TargetData atkJudge(TargetData[] tData)
        {
            return tData[0];
        }

        // 各条件を配列に格納。
        this.judgeArray[(int)JudgeConditions.一定距離内にいるかで判定] = distanceJudge;
        this.judgeArray[(int)JudgeConditions.HP量で判定] = hpJudge;
        this.judgeArray[(int)JudgeConditions.MP量で判定] = mpJudge;
        this.judgeArray[(int)JudgeConditions.攻撃力で判定] = atkJudge;
    }

    /// <summary>
    /// メインループ
    /// </summary>
    private void Update()
    {

        // 使用する設定で配列のどの要素にアクセスするか、が決定する。
        TargetData target = this.judgeArray[(int)this.useCondition](new TargetData[10]);

        // 判断後に攻撃対象がいれば攻撃する。
        if ( target != null )
        {
            this.Attack(target);
        }
    }

    /// <summary>
    /// 攻撃する処理。
    /// </summary>
    /// <param name="target">攻撃ターゲット</param>
    private void Attack(TargetData target)
    {

    }
    #endregion 処理
}
