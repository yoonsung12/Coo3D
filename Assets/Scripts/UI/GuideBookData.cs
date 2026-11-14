using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

// 가이드북(F1)에 들어가는 모든 글과 이미지를 담는 데이터 에셋이다.
// 게임 씬마다 GameCanvas가 따로 있어서(총 7개) 글을 프리팹/씬에 직접 넣으면 고칠 때마다 씬을 7번 열어야 한다.
// 그래서 글은 이 에셋 하나에 모아 두고, 동료가 씬을 건드리지 않고 에셋만 수정할 수 있게 한다.
[CreateAssetMenu(fileName = "GuideBook", menuName = "UI/GuideBookData")]
public class GuideBookData : ScriptableObject
{
    [Title("첫 화면 (자동으로 펼쳐지는 장)")]
    [LabelText("첫 화면 왼쪽")]
    public GuidePage welcomeLeft = new GuidePage();

    [LabelText("첫 화면 오른쪽")]
    public GuidePage welcomeRight = new GuidePage();

    [Title("책갈피(탭) 목록")]
    [LabelText("탭 목록")]
    [ListDrawerSettings(ShowFoldout = true)]
    public List<GuideTab> tabs = new List<GuideTab>();
}

// 책갈피 하나(예: 조작법). 여러 장(spread)을 가질 수 있다.
[Serializable]
public class GuideTab
{
    [LabelText("탭 이름")]
    public string tabName;

    [LabelText("탭 색")]
    public Color tabColor = Color.white;

    [LabelText("펼침 목록")]
    public List<GuideSpread> spreads = new List<GuideSpread>();
}

// 책을 펼쳤을 때 보이는 왼쪽/오른쪽 두 쪽이다.
[Serializable]
public class GuideSpread
{
    [LabelText("왼쪽 쪽")]
    public GuidePage left = new GuidePage();

    [LabelText("오른쪽 쪽")]
    public GuidePage right = new GuidePage();
}

// 한 쪽에 들어가는 내용. 비워 둔 항목은 화면에서 자동으로 숨겨진다.
[Serializable]
public class GuidePage
{
    [LabelText("제목")]
    public string title;

    [LabelText("부제목")]
    public string subtitle;

    [LabelText("큰 그림"), PreviewField(60, ObjectFieldAlignment.Left)]
    public Sprite heroImage;

    [LabelText("그림 높이")]
    public float heroHeight = 300f;
    // 큰 그림이 차지하는 세로 길이(px)다. 가로는 비율에 맞춰 자동으로 정해진다.

    [LabelText("설명"), TextArea(2, 8)]
    public string description;

    [LabelText("항목 목록")]
    [ListDrawerSettings(ShowFoldout = true)]
    public List<GuideEntry> entries = new List<GuideEntry>();

    [LabelText("팁 제목")]
    public string tipTitle;

    [LabelText("팁 내용"), TextArea(2, 5)]
    public string tipText;
}

// 한 쪽 안의 항목 한 줄 (아이콘 + 제목 + 키 배지 + 설명 + 강조 박스).
[Serializable]
public class GuideEntry
{
    [LabelText("아이콘"), PreviewField(50, ObjectFieldAlignment.Left)]
    public Sprite icon;
    // 비워 두면 아이콘 상자가 아예 나오지 않는다.

    [LabelText("제목")]
    public string title;

    [LabelText("키 표시")]
    public string keyLabel;
    // 제목 옆에 작은 어두운 배지로 표시된다. 비우면 안 나온다.

    [LabelText("설명"), TextArea(1, 5)]
    public string body;

    [LabelText("강조 문구")]
    public string highlight;
    // 설명 아래 연두색 박스로 표시된다. 비우면 안 나온다.
}
