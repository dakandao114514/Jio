using UnityEngine;

public interface IConductive
{
    /// <summary>
    /// 被电弧/漏电命中时调用
    /// </summary>
    /// <param name="origin">电弧来源位置</param>
    /// <param name="intensity">电弧强度</param>
    /// <param name="chainDepth">连锁深度，用于静电过载增幅</param>
    void OnDischarge(Vector3 origin, float intensity, int chainDepth);
}
