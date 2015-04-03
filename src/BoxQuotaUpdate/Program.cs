using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NDesk.Options;
using Nito.AsyncEx;
using NLog;

namespace BoxQuotaUpdate
{
    public class Program
    {
        private static Logger _log;
        private static BoxClient _boxClient;
        private static readonly object Locker = new object();

        // reporting
        private static readonly DateTime Start = DateTime.Now;
        private static DateTime _lastBatch = DateTime.Now;
        private static int _completedUserCount;
        private static int _totalUserCount;
        
        // command-line args
        private static string _accessToken;
        private static string _refresh;
        private static string _id;
        private static string _secret;
        private static long _quota = -1;

        private static void Main(string[] args)
        {
            if (!ParseOptions(args)) return;

            _log = LogManager.GetLogger("Program");
            _boxClient = new BoxClient(_accessToken, _refresh, _id, _secret);
            
            AsyncContext.Run(async () => await UpdateQuotaForAllActiveUsers(_quota));
            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
        }

        #region Parse command line arguments
        private static bool ParseOptions(string[] args)
        {
            bool showHelp = false;

            var p = new OptionSet
            {
                {"t|token=", "Required. A Box Access {TOKEN} with enterprise management capability.", t => _accessToken = t},
                {"q|quota:", "Optional. The new quota to be applied to all users. Defaults to -1 (unlimited)", q => _quota = ParseQuota(q)},
                {"r|refresh:", "Optional. A Box application {REFRESH} token, to facilitate automatic token refreshing.", r => _refresh = r},
                {"i|id:", "Optional. A Box application client {ID}, to facilitate automatic token refreshing.", i => _id = i},
                {"s|secret:", "Optional. A Box application client {SECRET}, to facilitate automatic token refreshing.", s => _secret = s},
                {"h|help", "show this message and exit", h => showHelp = (h != null)},
            };

            try
            {
                p.Parse(args);
                if (showHelp)
                {
                    ShowHelp(p);
                    return true;
                }
            }
            catch (Exception e)
            {
                Console.Out.WriteLine("Caught exception: " + e);
                ShowHelp(p);
                return false;
            }
            return true;
        }

        private static long ParseQuota(string q)
        {
            long quota;
            if (!long.TryParse(q, out quota)) throw new ArgumentException("quota must be a 64-bit number");
            return quota;
        }

        private static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Updates the Box quota for all users in the enterprise");
            Console.WriteLine();
            Console.WriteLine("Usage: boxuserspaceupdate -t <token> [-q <quota> -r <refresh_token> -i <client_id> -s <client_secret]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }

        #endregion

        private static async Task UpdateQuotaForAllActiveUsers(long quotaInBytes)
        {
            try
            {
                int offset = 0;
                var count = 0;
                do
                {
                    // Update quota for up to 1000 users at a time.
                    var users = await _boxClient.GetUsers(offset);
                    _totalUserCount = users.TotalCount;
                    count = users.Entries.Count();
                    offset += count;
                    await UpdateQuotaForActiveUsers(users.Entries, quotaInBytes);

                //  Repeat until all enterprise users are processed.
                } while (offset != _totalUserCount && count != 0);
            }
            catch (Exception e)
            {
                AssertAuthorized(e);
                Console.Out.WriteLine("Caught exception: " + e);
            }
        }

        private static async Task UpdateQuotaForActiveUsers(List<BoxUser> collection, long quotaInBytes)
        {
            // Set quotas for up to 5 users in parallel.
            const int maxDegreeOfParallelism = 5;

            var tasks = new List<Task>();
            var throttler = new SemaphoreSlim(maxDegreeOfParallelism);
            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            // Update quotas for multiple users in parallel
            foreach (var login in collection)
            {
                var localLogin = login;
                await throttler.WaitAsync(ct);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await SetQuotaForActiveUser(localLogin, quotaInBytes);
                        ReportProgress();
                    }
                    catch (Exception e)
                    {
                        Console.Out.WriteLine("Caught exception: " + e);
                        cts.Cancel();
                    }
                    finally
                    {
                        throttler.Release();
                    }
                }, ct));
            }
            // wait for all users in this bunch to be processed
            await Task.WhenAll(tasks);
        }

        private static void ReportProgress()
        {
            // Report elapsed time and rate (as updates/second) after every 50 users.
            lock (Locker)
            {
                _completedUserCount++;
                const int batchSize = 50;
                if ((_completedUserCount % batchSize) == 0)
                {
                    var now = DateTime.Now;
                    var lastBatchTotalSeconds = Math.Max((now - _lastBatch).TotalSeconds, 1.0);
                    var lastBatchRate = batchSize/lastBatchTotalSeconds;
                    var elapsed = now - Start;
                    Console.Out.Write("\r{0,6}/{1,6} Elapsed: {2}  Rate: {3:0.0}/sec   ", _completedUserCount, _totalUserCount, elapsed.ToString(@"hh\:mm\:ss"), lastBatchRate);
                    _lastBatch = now;
                }
            }
        }

        private static async Task SetQuotaForActiveUser(BoxUser login, long quotaInBytes)
        {
            var message = login.SpaceAmount.ToString();
            
            // Box reports 'unlimited' quota as 1E15.
            var comparisonQuota = quotaInBytes == -1 ? 1000000000000000 : quotaInBytes;
        
            // Only update quotas for active users that don't already have the new quota.
            if (login.Status == "active" && login.SpaceAmount != comparisonQuota)
            {
                try
                {
                    var update = await _boxClient.UpdateUser<BoxUser>(login.Id, quotaInBytes);
                    message = update.SpaceAmount.ToString();
                }
                catch (Exception e)
                {
                    AssertAuthorized(e);
                    message = "Error: " + e;
                }
                _log.Info("{0},{1},{2},{3}", login.Login, login.Name, login.SpaceAmount, message);
            }
        }

        private static void AssertAuthorized(Exception e)
        {
            if (e is BoxAuthorizationException)
            {
                Console.Out.WriteLine("[Error] Access token is no longer valid -- maybe it has expired?. Program will exit.");
                throw e;
            }
        }
    }
}