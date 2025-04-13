using UnityEngine;

public class BigAISamp : MonoBehaviour
{
    #region 定義
    /// <summary>
    /// 攻撃する際の条件判断の基準となる設定の列挙型。
    /// </summary>
    enum JudgeConditions
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
    JudgeConditions useCondition;

    #endregion インスタンス変数

    #region 処理

    /// <summary>
    /// メインループ
    /// </summary>
    void Update()
    {

        // 敵の中からターゲットを決定。
        // 引数となる敵の候補配列はシングルトンから持ってくる想定。(自分はそうしている）
        // TargetData target = Judge(GManager.instance.EnemyArray);

        //// 判断後に攻撃対象がいれば攻撃する。
        //if ( target != null )
        //{
        //    Attack();
        //}
    }

    /// <summary>
    /// 攻撃条件に当てはまったターゲットのデータを返す処理。
    /// </summary>
    /// <param name="tData">ターゲット候補のリスト</param>
    /// <returns>今回の判断の結果、攻撃対象になったターゲット。対象がいなければ null</returns>
    TargetData Judge(TargetData[] tData)
    {
        //for ( int i = 0; i < tData.Length; i++ )
        //{
        //    if ( useCondition == JudgeConditions.一定距離内にいるかで判定 )
        //    {
        //        // 距離の判定を書いて、もし一定内の距離ならそいつをターゲットに。
        //        return tData[i];
        //    }
        //    else if ( useCondition == JudgeConditions.HP量で判定 )
        //    {
        //        // HPの判定を書いて、もし一定以上ならそいつをターゲットに。
        //        return tData[i];
        //    }
        //    else if ( useCondition == JudgeConditions.MP量で判定 )
        //    {
        //        // MPの判定を書いて、もし一定以上ならそいつをターゲットに。
        //        return tData[i];
        //    }
        //    else
        //    {
        //        // 攻撃力の判定を書いて、もし一定以上ならそいつをターゲットに。
        //        return tData[i];
        //    }
        //}
        // いなければ null。
        return null;
    }

    /// <summary>
    /// 攻撃する処理。
    /// </summary>
    /// <param name="target">攻撃ターゲット</param>
    void Attack(TargetData target)
    {

    }
    #endregion 処理
}
