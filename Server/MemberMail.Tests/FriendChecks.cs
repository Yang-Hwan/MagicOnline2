using MagicOnline.GameApi;

static class FriendChecks
{
    public static async Task Run(Database db,GameData game,Guid a,Guid b)
    {
        var friends=new Friends(db,game);
        int hall=(await game.Chart("SkillMatch")).EnumerateArray().First().GetProperty("HallIdx").GetInt32();
        async Task MustFail(Func<Task> action)
        {
            try {await action();}catch(ApiFault){return;}
            throw new Exception("Expected social action to be rejected");
        }
        void Check(bool ok,string note){if(!ok)throw new Exception(note);Console.WriteLine("PASS "+note);}
        await friends.Request(a,b);
        await friends.Request(a,b);
        Check((await friends.Poll(b,false)).From==a,"request survives leaving the room");
        await friends.Answer(a,b,true);
        Check(!(await friends.List(a)).Rows.Any(),"sender cannot accept their own request");
        await friends.Answer(b,a,false);
        Check((await friends.Poll(b,true)).From==null,"decline clears pending request");
        await friends.Request(a,b);
        await friends.Answer(b,a,true);
        Check((await friends.List(a)).Rows.Single().Id==b && (await friends.List(b)).Rows.Single().Id==a,"mutual friendship stored once");
        await friends.Poll(a,true);
        await friends.Poll(b,false);
        await MustFail(()=>friends.Challenge(a,new(b,hall)));
        Check((await friends.List(a)).Rows.Single().Status=="busy","busy friend cannot be challenged");
        using(var offline=db.Source.CreateCommand("UPDATE friend_presence SET seen_at=now()-interval '60 seconds'"))await offline.ExecuteNonQueryAsync();
        Check((await friends.List(a)).Rows.Single().Status=="offline","expired presence displays offline");
        await MustFail(()=>friends.Challenge(a,new(b,hall)));
        await friends.Poll(a,true);await friends.Poll(b,true);
        var first=await friends.Challenge(a,new(b,hall));
        await MustFail(()=>friends.Challenge(b,new(a,hall)));
        await MustFail(()=>friends.Decide(a,first.Id,true));
        await friends.Poll(a,false);
        await MustFail(()=>friends.Decide(b,first.Id,true));
        await friends.Decide(a,first.Id,false);
        Check((await friends.Poll(b,true)).Invite==null,"sender cancellation and availability recheck");
        await friends.Poll(a,true);
        var before=await game.Profile(a);
        using(var noEntry=db.Source.CreateCommand("UPDATE wallets SET coin=0 WHERE user_id=$1"))
        {
            noEntry.Parameters.AddWithValue(a);
            await noEntry.ExecuteNonQueryAsync();
        }
        var next=await friends.Challenge(a,new(b,hall));
        await friends.Decide(b,next.Id,true);
        var matchA=(await friends.Poll(a,true)).Invite;
        var matchB=(await friends.Poll(b,true)).Invite;
        Check(matchA?.Id==next.Id && matchB?.Id==next.Id && matchB.Hall==hall &&
            matchA.Other==b && matchB.Other==a && matchA.State=="accepted","both seats see the same accepted hall ticket");
        var after=await game.Profile(a);
        Check(after.Coin==0 && before.LvProc==after.LvProc && before.LvVel==after.LvVel,"friendly invitation works at zero balance without changing progression or wallet");
        using var restore=db.Source.CreateCommand("UPDATE wallets SET coin=$2 WHERE user_id=$1");
        restore.Parameters.AddWithValue(a);restore.Parameters.AddWithValue(before.Coin);
        await restore.ExecuteNonQueryAsync();
        var stranger=Guid.Parse((await new Accounts(db).Register(new("friend_search_test","test_password"))).UserId);
        using(var rename=db.Source.CreateCommand("UPDATE users SET nickname='검색_Test회원' WHERE id=$1"))
        {rename.Parameters.AddWithValue(stranger);await rename.ExecuteNonQueryAsync();}
        Check((await friends.Search(a,"_test회원")).Rows.Single().Id==stranger &&
            !(await friends.Search(stranger,"검색_Test회원")).Rows.Any(),"partial nickname search ignores case and excludes self");
        await Task.WhenAll(friends.Request(a,stranger),friends.Request(a,stranger));
        Check((await friends.Search(a,"검색_Test회원")).Rows.Single().Relation=="요청 중" &&
            (await friends.Poll(stranger,true)).From==a,"search sends an inbox request without match history");
        await friends.Answer(stranger,a,true);
        await friends.Poll(a,true);
        using(var close=db.Source.CreateCommand("UPDATE friend_challenges SET state='closed' WHERE id=$1"))
        {close.Parameters.AddWithValue(next.Id);await close.ExecuteNonQueryAsync();}
        var pending=await friends.Challenge(a,new(stranger,hall));
        await friends.Remove(b,stranger);
        Check((await friends.List(stranger)).Rows.Any(p=>p.Id==a),"third party cannot break a friendship");
        await friends.Remove(stranger,a);
        await friends.Remove(stranger,a);
        Check(!(await friends.List(a)).Rows.Any(p=>p.Id==stranger) && !(await friends.List(stranger)).Rows.Any() &&
            (await friends.Poll(a,true)).Invite==null,"removal updates both lists and closes the pending challenge");
        await MustFail(()=>friends.Decide(stranger,pending.Id,true));
        await MustFail(()=>friends.Challenge(a,new(stranger,hall)));
        await friends.Request(stranger,a);
        Check((await friends.Poll(a,true)).From==stranger,"former friends can request again");
    }
}
