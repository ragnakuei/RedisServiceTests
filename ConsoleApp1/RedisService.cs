using StackExchange.Redis;

namespace ConsoleApp1;

public class RedisService
{
    static RedisService()
    {
        _lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
                                                          {
                                                              var conn = ConnectionMultiplexer.Connect("localhost:6379");
                                                              conn.ConfigurationChanged += (object sender, EndPointEventArgs e) =>
                                                                                           {
                                                                                               Console.WriteLine("配置更改");
                                                                                           };
                                                              conn.ConfigurationChangedBroadcast += (object sender, EndPointEventArgs e) =>
                                                                                                    {
                                                                                                        Console.WriteLine("通過發布訂閱更新配置");
                                                                                                    };
                                                              conn.ConnectionFailed += (object sender, ConnectionFailedEventArgs e) =>
                                                                                       {
                                                                                           Console.WriteLine("連接失敗");
                                                                                       };
                                                              conn.ConnectionRestored += (object sender, ConnectionFailedEventArgs e) =>
                                                                                         {
                                                                                             Console.WriteLine("重新建立連接到之前出錯的連接");
                                                                                         };
                                                              conn.ErrorMessage += (object sender, RedisErrorEventArgs e) =>
                                                                                   {
                                                                                       Console.WriteLine("發生錯誤");
                                                                                   };
                                                              conn.HashSlotMoved += (object sender, HashSlotMovedEventArgs e) =>
                                                                                    {
                                                                                        Console.WriteLine("更改集群");
                                                                                    };
                                                              conn.InternalError += (object sender, InternalErrorEventArgs e) =>
                                                                                    {
                                                                                        Console.WriteLine("redis庫內部錯誤");
                                                                                    };
                                                              return conn;
                                                          });
    }

    private static Lazy<ConnectionMultiplexer> _lazyConnection;

    private IDatabase GetDatabase()
    {
        return _lazyConnection.Value.GetDatabase();
    }

    /// <summary>
    /// Key Lock
    /// </summary>
    /// <param name="key">key 名稱</param>
    /// <param name="expiry">Lock 自動 release 的時間長度</param>
    /// <param name="func">Lock 後要執行的動作</param>
    /// <param name="maxRetryCount">最大重試次數</param>
    /// <param name="retryDelayMs">每次重試的延遲等待時間，單位：ms</param>
    /// <param name="exitWhenExceptionOccurs">Exception 發生時，是否直接跳出</param>
    /// <returns></returns>
    public async Task<bool> LockTakeAsync(string     key,
                                          TimeSpan   expiry,
                                          Func<Task> func,
                                          int        maxRetryCount           = 20,
                                          int        retryDelayMs            = 1000,
                                          bool       exitWhenExceptionOccurs = true)

    {
        // Lock失敗就等 1000 毫秒，再重試，最多 20 次
        var lockKey           = $"Lock_{key}";
        var currentRetryCount = 0;
        do
        {
            try
            {
                var database = GetDatabase();
                if (await database.LockTakeAsync(lockKey, Environment.MachineName, expiry))
                {
                    await func.Invoke();

                    // 執行完所需的動作才 Release
                    await database.LockReleaseAsync(lockKey, Environment.MachineName);
                    return true;
                }
                else
                {
                    Console.WriteLine($"LockTakeAsync False, retry count: {currentRetryCount}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Redis Service Exception Occurs: {e.GetType().Name} {e.Message}");

                if (exitWhenExceptionOccurs)
                {
                    return false;
                }
            }

            await Task.Delay(retryDelayMs);
            currentRetryCount++;
        } while (currentRetryCount < maxRetryCount);

        return false;
    }
}
