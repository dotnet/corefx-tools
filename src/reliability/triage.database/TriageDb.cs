using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace triage.database
{
    public static class TriageDb
    {
        private static string g_connStr;

        public static void Init(string connStr)
        {
            g_connStr = connStr;
        }

        public static async Task AddDumpAsync(Dump dump)
        {
            using (var context = new TriageDbContext(g_connStr))
            {
                await context.SaveChangesAsync();
            }
        }

        private const string BUCKET_DATA_QUERY = @"
WITH [BucketHits]([BucketId], [HitCount], [StartTime], [EndTime]) AS
(
    SELECT [B].[Id] AS [BucketId], COUNT([D].[Id]) AS [HitCount], @p0 AS [StartTime], @p1 AS [EndTime]
    FROM [Dumps] [D]
    JOIN [Buckets] [B]
        ON [D].[BucketId] = [B].[BucketId]
        AND [D].[DumpTime] >= @p0
        AND [D].[DumpTime] <= @p1
    GROUP BY [B].[Id]
)
SELECT [B].*, [H].[HitCount], [H].[Start], [H].[End]
FROM [Buckets] AS [B]
JOIN [BucketHits] AS [H]
    ON [B].[BucketId] = [H].[BucketId]
";
        public static async Task<IEnumerable<BucketData>> GetBucketDataAsync(DateTime start, DateTime end)
        {
            using (var context = new TriageDbContext(g_connStr))
            {
                return await context.Database.SqlQuery<BucketData>(BUCKET_DATA_QUERY, start, end).ToArrayAsync();
            }
        }

        private const string BUCKET_DATA_DUMPS_QUERY = @"
SELECT * 
FROM [Dumps]
WHERE [BucketId] = @p0
    AND [DumpTime] >= @p1
    AND [DumpTime] <= @p2
";
        public static async Task<IEnumerable<Dump>> GetBucketDataDumpsAysnc(BucketData bucketData)
        {
            using (var context = new TriageDbContext(g_connStr))
            {
                return await context.Dumps.SqlQuery(BUCKET_DATA_DUMPS_QUERY, bucketData.BucketId, bucketData.StartTime, bucketData.EndTime).ToArrayAsync();
            }
        }
    }
}
