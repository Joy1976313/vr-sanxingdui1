/// <summary>
/// 全局任务进度（静态类，跨场景存活，停止 Play 自动清零）。
/// 神树对话、关卡回收、神树复苏表现都读这里。
/// </summary>
public static class GameProgress
{
    /// <summary>神树是否已授予主线任务</summary>
    public static bool TasksGiven;

    /// <summary>已回收文物数（0~3）</summary>
    public static int ArtifactsReturned => (MaskReturned ? 1 : 0) + (RodReturned ? 1 : 0) + (BirdReturned ? 1 : 0);

    public static bool MaskReturned;   // 纵目面具（关卡①）
    public static bool RodReturned;    // 黄金权杖（关卡②）
    public static bool BirdReturned;   // 神鸟（关卡③）

    /// <summary>开发用：手动重置</summary>
    public static void Reset()
    {
        TasksGiven = false;
        MaskReturned = false;
        RodReturned = false;
        BirdReturned = false;
    }
}
