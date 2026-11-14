"""Real local HTTP league match: isolated temporary accounts, cleaned through admin deletion."""
import json,os,uuid
from pathlib import Path
from urllib.parse import urljoin
import requests,psycopg
from bs4 import BeautifulSoup

ROOT=Path(__file__).resolve().parents[2]
API='http://127.0.0.1:5080';ADMIN='http://127.0.0.1:5082'
run=uuid.uuid4().hex[:8];out=ROOT/'Logs/GuildLeague'/run;out.mkdir(parents=True)
admin=requests.Session();users=[];private_hall=None;result={'Ok':False,'Checks':0,'Cleanup':[]}
def api(method,path,user=None,data=None,status=200):
    r=requests.request(method,API+path,json=data,headers={'Authorization':'Bearer '+user['AccessToken']} if user else {},timeout=20)
    assert r.status_code==status,f'{method} {path}: {r.status_code} {r.text[:250]}'
    result['Checks']+=1
    return r.json() if r.content else None
def form(url,field):
    r=admin.get(url,timeout=20);r.raise_for_status();soup=BeautifulSoup(r.text,'html.parser');f=soup.select_one(f'[name="{field}"]').find_parent('form')
    return urljoin(r.url,f.get('action') or r.url),[(i['name'],i.get('value','')) for i in f.select('input[name]')]
def submit(url,values,extra):
    r=admin.post(url,data=[(k,v) for k,v in values if k not in extra]+list(extra.items()),timeout=20,allow_redirects=False)
    assert r.status_code in (302,303),f'Admin form {r.status_code}'
try:
    secrets=json.loads((Path(os.environ['APPDATA'])/'Microsoft/UserSecrets/MagicCushion-Local-GameApi/secrets.json').read_text(encoding='utf-8-sig'))
    parts={k.lower():v for k,v in (x.split('=',1) for x in secrets['ConnectionStrings:GameDb'].split(';') if '=' in x)}
    assert parts['host'] in ('localhost','127.0.0.1')
    with psycopg.connect(host=parts['host'],port=parts.get('port',5432),dbname=parts['database'],user=parts['username'],password=parts['password']) as db:
        with db.cursor() as c:
            c.execute("SELECT login_id FROM admin_users WHERE role='Owner' AND NOT disabled ORDER BY created_at LIMIT 1");owner=c.fetchone()[0]
    action,values=form(ADMIN+'/admin/login','LoginId');submit(action,values,{'LoginId':owner})
    api('GET','/api/guild-league',status=401)
    for role in ('A','B'):
        login='league_test_'+role+'_'+run;u=api('POST','/api/auth/register',data={'LoginId':login,'Password':'League-'+uuid.uuid4().hex});u.update(Login=login,Role=role);users.append(u)
        api('POST','/api/guilds',u,{'Name':'공식전검증'+role+run,'Tag':role+run[:5].upper(),'Description':'검증 후 정리','JoinMode':'Open','RequestId':str(uuid.uuid4())})
    a,b=users
    initial=api('GET','/api/guild-league',a)
    assert initial['EndsAt']-initial['StartsAt']==28*86400000
    halls=api('GET','/api/charts/SkillMatch',a)
    hall=next(x for x in halls if x['MatchBall']==4 and 1<=x['MatchTotMin']<=30).copy()
    candidate=next(i for i in range(9900,9999) if all(x['HallIdx']!=i for x in halls))
    hall.update(HallIdx=candidate,HallName='길드전 검증 '+run,TargetHit=10,MatchTotMin=3,PrizeCoin=1,MaxCoin=1000000,MatchCushion=0,FinishMission=0)
    action,values=form(ADMIN+'/admin/charts/edit?name=SkillMatch','Revision')
    submit(action,values,dict({'Reason':'길드전 HTTP 검증용 경기장'},**{'Values['+k+']':str(v) for k,v in hall.items()}))
    private_hall=candidate
    assert api('POST','/api/guild-league/queue',a,{'HallId':hall['HallIdx']})['State']=='Searching'
    ticket=api('POST','/api/guild-league/queue',b,{'HallId':hall['HallIdx']})
    assert ticket['State']=='Matched'
    assert api('POST','/api/guild-league/queue',a,{'HallId':hall['HallIdx']})['MatchId']==ticket['MatchId']
    match=ticket['MatchId'];result['MatchId']=match
    rules={'Version':2,'Runtime':0,'HallId':hall['HallIdx'],'Ball':hall['MatchBall'],'TargetScore':hall['TargetHit'],'Cushions':hall['MatchCushion'],'MatchSeconds':hall['MatchTotMin']*60,'TurnSeconds':30,'FinishMission':hall['FinishMission'],'HeartbeatSeconds':15,'ReconnectGraceSeconds':30,'CoinPolicy':1,'EntryCoin':0,'MaxCoin':0}
    api('POST','/api/matches',a,{'RequestId':match,'OpponentUserId':b['UserId'],'HallId':hall['HallIdx'],'Rules':rules})
    api('POST',f'/api/matches/{match}/ready',a,{'Rules':rules});api('POST',f'/api/matches/{match}/ready',b,{'Rules':rules})
    report={'ScoreA':hall['TargetHit'],'ScoreB':0,'WinnerSeat':0,'CoinEvents':''}
    assert api('POST',f'/api/matches/{match}/result',a,report) is None
    api('POST',f'/api/matches/{match}/result',b,report);api('POST',f'/api/matches/{match}/result',b,report)
    state=api('GET',f'/api/matches/{match}',a);assert state['Phase']=='Applied' and state['Receipt']['EntryA']==0
    own=api('GET','/api/guild-league',a);other=api('GET','/api/guild-league',b)
    assert own['Own']['Points']==1024 and other['Own']['Points']==988
    assert own['MyGames']==1 and own['MyPoints']==24 and own['Recent'][0]['Result']=='Win'
    assert other['MyPoints']==-12 and other['MyGames']==1
    api('POST',f"/api/guild-league/rewards/{initial['Season']}",a,status=409)
    api('GET','/api/guild-league/rewards',a)
    api('POST','/api/guild-league/queue',a,{'HallId':hall['HallIdx']});assert api('POST','/api/guild-league/queue',b,{'HallId':hall['HallIdx']})['State']=='Searching'
    api('DELETE','/api/guild-league/queue',a);api('DELETE','/api/guild-league/queue',b)
    result['Ok']=True
except Exception as e:result['Error']=str(e)
finally:
    if private_hall is not None:
        try:
            halls=api('GET','/api/charts/SkillMatch',users[0]);index=next(i for i,x in enumerate(halls) if x['HallIdx']==private_hall)
            action,values=form(ADMIN+f'/admin/charts/delete?name=SkillMatch&row={index}','Revision');submit(action,values,{'Reason':'길드전 검증 전용 경기장 정리'})
            result['Cleanup'].append('Private hall')
        except Exception as e:result['CleanupError']=str(e)
    for u in users:
        try:
            if result.get('MatchId'):
                try:api('POST','/api/matches/'+result['MatchId']+'/surrender',u)
                except Exception:pass
            action,values=form(ADMIN+'/admin/members/delete?id='+u['UserId'],'Confirmation');assert [v for k,v in values if k=='ExpectedLogins']==[u['Login']]
            submit(action,values,{'Reason':'공식 길드전 HTTP 검증 계정 정리','Confirmation':'삭제'});result['Cleanup'].append(u['Role'])
        except Exception as e:result['CleanupError']=str(e)
    (out/'http.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(result,ensure_ascii=False))
raise SystemExit(0 if result['Ok'] and not result.get('CleanupError') else 1)
