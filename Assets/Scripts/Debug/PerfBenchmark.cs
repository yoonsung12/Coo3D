using System.Collections;
using System.Collections.Generic;
using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;

// 성능 측정 전용 컴포넌트다. 실행 인자에 -benchmark가 있을 때만 동작하고, 평소 게임에는 아무 영향이 없다.
// 플레이어를 정해 둔 측정 지점으로 차례로 옮긴 뒤, 카메라가 자리 잡으면 게임 시간을 멈추고
// 몇 초 동안 평균 프레임 시간(CPU/GPU)을 재서 로그와 텍스트 파일로 남긴다. 측정이 끝나면 게임을 종료한다.
// 실행할 때마다 같은 위치·같은 상태(시간 정지)에서 재므로, 설정을 바꾼 빌드끼리 공정하게 비교할 수 있다.
public class PerfBenchmark : MonoBehaviour
{
    [System.Serializable]
    private class MeasurePoint
    {
        [LabelText("이름")]
        public string label;

        [LabelText("플레이어 위치")]
        public Vector3 position;
        // 이 위치로 플레이어를 옮긴다. 탑다운 구역 안이면 Respawn이 시점도 탑다운으로 맞춰 준다.
    }

    [Title("측정 지점")]
    [SerializeField, LabelText("측정 지점 목록")]
    private List<MeasurePoint> points = new List<MeasurePoint>();

    [Title("측정 설정")]
    [SerializeField, LabelText("카메라 안정화 시간 (초)")]
    private float settleSeconds = 1.5f;
    // 순간이동 직후에는 카메라가 따라가는 중이라, 이 시간만큼 기다린 뒤 측정한다.

    [SerializeField, LabelText("측정 시간 (초)")]
    private float measureSeconds = 4f;
    // 값이 클수록 평균이 안정적이지만 전체 측정 시간이 길어진다.

    [SerializeField, LabelText("에디터에서도 실행")]
    private bool runInEditor;
    // 켜면 실행 인자 없이 Play 버튼만으로 측정한다. 평소에는 꺼 둔다.

    private readonly FrameTiming[] _timings = new FrameTiming[1];

    // 씬을 수정하지 않고도 모든 씬을 측정할 수 있도록, -benchmark로 실행했는데 씬에 이 컴포넌트가 없으면 하나 만든다.
    // 이렇게 만든 벤치마크는 측정 지점이 비어 있으므로 시작 위치에서 한 번만 측정한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateIfRequested()
    {
        if (!HasBenchmarkArg()) return;
        if (FindFirstObjectByType<PerfBenchmark>() != null) return;
        new GameObject("PerfBenchmark (Auto)").AddComponent<PerfBenchmark>();
    }

    private static bool HasBenchmarkArg()
    {
        foreach (string arg in System.Environment.GetCommandLineArgs())
            if (arg == "-benchmark") return true;
        return false;
    }

    private void Start()
    {
        bool requested = (runInEditor && Application.isEditor) || HasBenchmarkArg();
        if (!requested) return;
        StartCoroutine(RunBenchmark());
    }

    private IEnumerator RunBenchmark()
    {
        // 씬 로딩 직후 프레임은 셰이더 컴파일 등으로 튀므로 잠시 기다린다.
        yield return new WaitForSecondsRealtime(2f);

        var report = new StringBuilder();
        report.AppendLine($"[PerfBenchmark] {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} / {SystemInfo.graphicsDeviceName} / {SystemInfo.graphicsDeviceType} / {Screen.width}x{Screen.height}");

        // 측정 지점이 없으면(자동 생성된 경우) 플레이어를 옮기지 않고 시작 위치에서 한 번 잰다.
        if (points.Count == 0)
        {
            Time.timeScale = 0f;
            yield return MeasureFrames("시작 위치", report);
        }

        foreach (MeasurePoint point in points)
        {
            // 순간이동은 프로젝트 규칙대로 Respawn()을 거쳐야 시점(사이드뷰/탑다운)이 위치에 맞게 바뀐다.
            Time.timeScale = 1f;
            if (PlayerHealth.Instance != null)
                PlayerHealth.Instance.Respawn(point.position);

            yield return new WaitForSecondsRealtime(settleSeconds);

            // 게임 시간을 멈춰 비·적·파티클 상태가 측정 중에 바뀌지 않게 한다. 렌더링은 그대로 계속된다.
            Time.timeScale = 0f;
            yield return MeasureFrames(point.label, report);
        }

        Time.timeScale = 1f;
        string text = report.ToString();
        Debug.Log(text);

        // 빌드에서는 실행 파일 옆(_Data 폴더의 상위)에 결과를 남긴다.
        string path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "benchmark_result.txt");
        System.IO.File.WriteAllText(path, text);

        if (!Application.isEditor)
            Application.Quit();
    }

    // 지정 시간 동안 매 프레임의 CPU(전체 프레임)·GPU 시간을 모아 평균과 상위 구간을 기록한다.
    // GPU 시간은 FrameTimingManager를 쓰므로 Player Settings의 Frame Timing Stats가 켜져 있어야 값이 나온다.
    private IEnumerator MeasureFrames(string label, StringBuilder report)
    {
        var frameMs = new List<float>();
        double gpuSum = 0, mainSum = 0, renderSum = 0;
        int gpuCount = 0;
        float elapsed = 0f;

        while (elapsed < measureSeconds)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            frameMs.Add(Time.unscaledDeltaTime * 1000f);

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timings) > 0 && _timings[0].gpuFrameTime > 0)
            {
                gpuSum += _timings[0].gpuFrameTime;
                // 메인 스레드(스크립트·물리)와 렌더 스레드(그리기 명령 준비) 중 어느 쪽이 병목인지 나눠 보기 위해 함께 모은다.
                mainSum += _timings[0].cpuMainThreadFrameTime;
                renderSum += _timings[0].cpuRenderThreadFrameTime;
                gpuCount++;
            }
        }

        frameMs.Sort();
        float avg = 0f;
        foreach (float ms in frameMs) avg += ms;
        avg /= frameMs.Count;
        float p90 = frameMs[(int)(frameMs.Count * 0.9f)];
        string gpu = gpuCount > 0 ? (gpuSum / gpuCount).ToString("F2") + "ms" : "없음";
        string main = gpuCount > 0 ? (mainSum / gpuCount).ToString("F2") + "ms" : "없음";
        string render = gpuCount > 0 ? (renderSum / gpuCount).ToString("F2") + "ms" : "없음";

        report.AppendLine($"{label}: 평균 {avg:F2}ms ({1000f / avg:F0} FPS), 90% {p90:F2}ms, 메인 {main}, 렌더스레드 {render}, GPU {gpu}, 프레임 {frameMs.Count}");
    }
}
