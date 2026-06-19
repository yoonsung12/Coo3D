using UnityEngine;

// 선풍기, 우산, 횃불 등 모든 도구의 공통 기반 클래스다.
public abstract class BaseTool : MonoBehaviour
{
    public virtual void Use() { }
    public virtual void StopUsing() { }
}
