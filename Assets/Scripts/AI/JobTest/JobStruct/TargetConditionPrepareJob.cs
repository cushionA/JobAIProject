using Unity.Jobs;
using UnityEngine;
using Unity.Collections;
using static JTestAIBase;
using Unity.Collections.LowLevel.Unsafe;
using static CombatManager;
using System.ComponentModel;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

/// <summary>
/// 陣営ごとに攻撃力が一番高いやつ、とかをキャッシュしとけばAI速くなるかと思った。
/// でもよく考えたら陣営や特徴でフィルタリングするので、もう線形した方がいいな
/// </summary>
public class TargetConditionPrepareJob
{

}
