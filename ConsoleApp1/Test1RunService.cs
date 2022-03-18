using System.Diagnostics;

namespace ConsoleApp1;

public class Test1RunService
{
    private readonly RedisService _redisService = new();

    public async Task RunAsync()
    {
        var sw = new Stopwatch();
        Console.WriteLine("[Test1RunService] Stopwatch Start");
        sw.Restart();

        // 預期要執行 5 秒
        var tasks = Enumerable.Range(1, 5)
                              .Select(i => RunAsync2(i));

        var results = await Task.WhenAll(tasks);
        sw.Stop();
        Console.WriteLine("[Test1RunService] Stopwatch Stop");

        var runResults = results.Select(r => r.runResult)
                                .GroupBy(r => r)
                                .ToDictionary(kv => kv.Key, kv => kv.Count());

        var lockResults = results.Select(r => r.lockResult)
                                 .GroupBy(r => r)
                                 .ToDictionary(kv => kv.Key, kv => kv.Count());

        // runResults false 就是
        // 1. 未達成所需要作業
        Console.WriteLine($"runResults true: {runResults.GetValueOrDefault(true)} false: {runResults.GetValueOrDefault(false)}");

        // lockResult false 就是
        // 1. 達到 retry max count
        Console.WriteLine($"lockResults true: {lockResults.GetValueOrDefault(true)} false: {lockResults.GetValueOrDefault(false)}");

        Console.WriteLine(sw.Elapsed.ToString("g"));
    }


    /// <summary>
    /// 不到二秒執行完，所以 LockTakeAsync 本身沒有 Delay 的機制
    /// </summary>
    private async Task<(bool runResult, bool lockResult)> RunAsync1(int no)
    {
        var runResult = false;
        var lockResult = await _redisService.LockTakeAsync(key: "RunAsync1",
                                                           expiry: TimeSpan.FromMilliseconds(10),
                                                           func: async () =>
                                                                 {
                                                                     Console.WriteLine($"> Task Index:{no} Start");
                                                                     await Task.Delay(1000);
                                                                     runResult = true;
                                                                     Console.WriteLine($"> Task Index:{no} Completed");
                                                                 },
                                                           maxRetryCount: 20,
                                                           retryDelayMs: 10);

        Console.WriteLine($"- Task Index:{no} runResult:{lockResult}");
        return (runResult: lockResult, lockResult: lockResult);
    }

    /// <summary>
    /// 思考點：
    /// 1. 先進入的資源，不會是先處理完
    /// 2. 會有亂數的 no 會執行失敗
    ///    runResult 執行失敗的原因是：未執行 func 內的作業
    ///    lockResult 執行失敗的原因是：達到 max retry count
    /// </summary>
    private async Task<(bool runResult, bool lockResult)> RunAsync2(int no)
    {
        var runResult = false;
        var lockResult = await _redisService.LockTakeAsync(key: "RunAsync1",
                                                           expiry: TimeSpan.FromMilliseconds(5000),
                                                           func: async () =>
                                                                 {
                                                                     Console.WriteLine($"> Task Index:{no} Start");
                                                                     await Task.Delay(1000);
                                                                     runResult = true;
                                                                     Console.WriteLine($"> Task Index:{no} Completed");
                                                                 },
                                                           maxRetryCount: 20,
                                                           // 以預估完成時間來設定 retry delay 時間
                                                           retryDelayMs: 100);

        Console.WriteLine($"- Task Index:{no} runResult:{lockResult}");
        return (runResult: lockResult, lockResult: lockResult);
    }
}
