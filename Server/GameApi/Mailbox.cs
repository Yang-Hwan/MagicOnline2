using Npgsql;

namespace MagicOnline.GameApi;

public sealed class Mailbox(Database db)
{
    public sealed record Item(long PostId, string Message, string Currency, long Amount, long ExpiresAt);
    public sealed record Inbox(long ServerTime, List<Item> Rows);

    public async Task<Inbox> List(Guid user)
    {
        await using var cmd=db.Source.CreateCommand("""
            SELECT post_id,message,currency_name,reward_val,expires_at,statement_timestamp()
            FROM user_post_reward WHERE user_id=$1 AND claimed_at IS NULL AND expires_at>statement_timestamp()
            ORDER BY expires_at,post_id
            """);
        cmd.Parameters.AddWithValue(user);
        await using var r=await cmd.ExecuteReaderAsync();
        var rows=new List<Item>();
        var now=DateTimeOffset.UtcNow;
        while(await r.ReadAsync())
        {
            now=r.GetFieldValue<DateTimeOffset>(5);
            rows.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetInt64(3),r.GetFieldValue<DateTimeOffset>(4).ToUnixTimeSeconds()));
        }
        return new(now.ToUnixTimeSeconds(),rows);
    }

    public async Task<object> Claim(Guid user,long? id)
    {
        await using var c=await db.Source.OpenConnectionAsync();
        await using var tx=await c.BeginTransactionAsync();
        // Serialize with other claims and all other wallet writers before checking expiry.
        await using(var wallet=Database.Command(c,"SELECT user_id FROM wallets WHERE user_id=$1 FOR UPDATE",user))
            if(await wallet.ExecuteScalarAsync()==null) throw new ApiFault(404,"회원을 찾을 수 없습니다.");
        string filter=id.HasValue ? " AND post_id=$2" : "";
        using var claim=new NpgsqlCommand("""
            WITH claimed AS (
                UPDATE user_post_reward SET claimed_at=statement_timestamp()
                WHERE user_id=$1 AND claimed_at IS NULL AND expires_at>statement_timestamp()
            """+filter+" "+"""
                RETURNING post_id,event_key,currency_name,reward_val,claimed_at
            ), ledger AS (
                INSERT INTO wallet_ledger(user_id,request_key,coin_delta,jem_delta,cash_delta,gold_delta)
                SELECT $1,'Mail:'||post_id,CASE WHEN currency_name='Coin' THEN reward_val ELSE 0 END,
                    CASE WHEN currency_name='Jem' THEN reward_val ELSE 0 END,
                    CASE WHEN currency_name='Cash' THEN reward_val ELSE 0 END,
                    CASE WHEN currency_name='Gold' THEN reward_val ELSE 0 END FROM claimed
                RETURNING coin_delta,jem_delta,cash_delta,gold_delta
            ), attendance_receipts AS (
                UPDATE user_attendance a SET take_dt=m.claimed_at FROM claimed m
                WHERE a.user_id=$1 AND m.event_key='Attendance:'||to_char(a.give_date,'YYYY-MM-DD') AND a.take_dt IS NULL
            ), totals AS (
                SELECT count(*) AS count, sum(coin_delta) coin,sum(jem_delta) jem,sum(cash_delta) cash,sum(gold_delta) gold FROM ledger
            )
            UPDATE wallets w SET coin=w.coin+t.coin,jem=w.jem+t.jem,cash=w.cash+t.cash,gold=w.gold+t.gold
            FROM totals t WHERE w.user_id=$1 AND t.count>0 RETURNING t.count
            """,c,tx);
        claim.Parameters.AddWithValue(user);
        if(id.HasValue)claim.Parameters.AddWithValue(id.Value);
        long count=(long?)await claim.ExecuteScalarAsync() ?? 0;
        await tx.CommitAsync();
        return new { Claimed=count };
    }
}
