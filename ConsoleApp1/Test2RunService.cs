using System.Diagnostics;

namespace ConsoleApp1;

public class Test2RunService
{
    private readonly RedisService _redisService = new();

    public async Task RunAsync()
    {
        var sw = new Stopwatch();
        Console.WriteLine("[Test2RunService] Stopwatch Start");
        sw.Restart();

        // 預期要執行 5 秒
        var tasks = Enumerable.Range(1, 6)
                              .Select(i => RunAsync1(i));

        var results = await Task.WhenAll(tasks);
        sw.Stop();
        Console.WriteLine("[Test2RunService] Stopwatch Stop");

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
    /// 以 no % 2 為 key 的一部份
    /// 預期約三秒要跑完
    /// </summary>
    private async Task<(bool runResult, bool lockResult)> RunAsync1(int no)
    {
        var keyPart = no % 2;

        var runResult = false;
        var lockResult = await _redisService.LockTakeAsync(key: $"RunAsync1_{keyPart}",
                                                           // 設成 maxRetryCount * retryDelayMs 比較剛好、簡單 !
                                                           // 意指：經過 maxRetryCount * retryDelayMs 所經過的時間，也就可以釋放 Lock 了 !
                                                           expiry: TimeSpan.FromMilliseconds(5000),
                                                           func: async () =>
                                                                 {
                                                                     Console.WriteLine($"> Task Index:{no} Start");
                                                                     await Task.Delay(1000);
                                                                     runResult = true;
                                                                     Console.WriteLine($"> Task Index:{no} Completed");
                                                                 },
                                                           maxRetryCount: 50,
                                                           retryDelayMs: 100);

        Console.WriteLine($"- Task Index:{no} runResult:{lockResult}");
        return (runResult: lockResult, lockResult: lockResult);
    }
}
