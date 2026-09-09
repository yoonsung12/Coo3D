using System;

// Boss의 평범한 패턴(부리 쪼기/깃털 부채꼴 발사/날개 돌풍)이 공통으로 구현하는 인터페이스다.
// BossBasicPattern이 어떤 공격인지 몰라도 동일한 방식으로 실행을 맡기고 완료 통보만 받기 위해 사용한다.
public interface IBossBasicAttack
{
    // dir: 보스가 바라보는 방향(+1 오른쪽 / -1 왼쪽). onFinished: 공격 연출/판정이 모두 끝나면 호출한다.
    void Execute(float dir, Action onFinished);
}
