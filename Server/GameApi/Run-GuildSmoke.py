"""Local HTTP guild integration smoke; creates fixtures and deletes only those via admin forms."""
import json, os, uuid
from pathlib import Path
from urllib.parse import urljoin
import requests
import psycopg
from bs4 import BeautifulSoup

ROOT=Path(__file__).resolve().parents[2]
API='http://127.0.0.1:5080'
ADMIN='http://127.0.0.1:5082'
run=uuid.uuid4().hex[:10]
out=ROOT/'Logs'/'Guilds'/run
out.mkdir(parents=True)
users=[]
admin=requests.Session()
result={'Ok':False,'Checks':0,'Cleanup':[]}

def api(method,path,user=None,data=None,status=200):
    r=requests.request(method,API+path,json=data,headers={'Authorization':'Bearer '+user['AccessToken']} if user else {},timeout=20)
    assert r.status_code==status, f'{method} {path}: {r.status_code} {r.text[:250]}'
    result['Checks']+=1
    return r.json() if r.content else None

def form(url,field):
    r=admin.get(url,timeout=20);r.raise_for_status()
    soup=BeautifulSoup(r.text,'html.parser')
    f=soup.select_one(f'[name="{field}"]').find_parent('form')
    return urljoin(r.url,f.get('action') or r.url),[(i['name'],i.get('value','')) for i in f.select('input[name]')]

def submit(url,values,extra):
    values=[(k,v) for k,v in values if k not in extra]+list(extra.items())
    r=admin.post(url,data=values,timeout=20,allow_redirects=False)
    assert r.status_code in (302,303),f'Admin form {r.status_code}'

try:
    secrets=json.loads((Path(os.environ['APPDATA'])/'Microsoft/UserSecrets/MagicCushion-Local-GameApi/secrets.json').read_text(encoding='utf-8-sig'))
    parts={k.lower():v for k,v in (x.split('=',1) for x in secrets['ConnectionStrings:GameDb'].split(';') if '=' in x)}
    assert parts['host'] in ('localhost','127.0.0.1')
    with psycopg.connect(host=parts['host'],port=parts.get('port',5432),dbname=parts['database'],user=parts['username'],password=parts['password']) as db:
        with db.cursor() as c:
            c.execute("SELECT login_id FROM admin_users WHERE role='Owner' AND NOT disabled ORDER BY created_at LIMIT 1")
            owner=c.fetchone()[0]
    url,values=form(ADMIN+'/admin/login','LoginId');submit(url,values,{'LoginId':owner})
    api('GET','/api/guilds/me',status=401)
    for role in ('A','B','C'):
        login='guild_test_'+role+'_'+run
        u=api('POST','/api/auth/register',data={'LoginId':login,'Password':'Guild-'+uuid.uuid4().hex})
        u['Login']=login;u['Role']=role;users.append(u)
    a,b,c=users
    create={'Name':'검증길드'+run,'Tag':run[:6].upper(),'Description':'검증 후 자동 정리','JoinMode':'Approval','RequestId':str(uuid.uuid4())}
    g=api('POST','/api/guilds',a,create)['GuildId']
    assert api('POST','/api/guilds',a,create)['GuildId']==g
    assert any(x['Id']==g for x in api('GET','/api/guilds?q='+create['Name'],b)['Rows'])
    api('POST',f'/api/guilds/{g}/join',b)
    assert api('GET','/api/guilds/me',a)['Applications'][0]['UserId']==b['UserId']
    api('POST',f'/api/guilds/{g}/applications/'+b['UserId'],a,{'Accept':True})
    api('POST',f'/api/guilds/{g}/invitations',a,{'UserId':c['UserId']})
    pulse=api('POST','/api/me/friends/pulse',c,{'Available':True})
    api('POST','/api/guilds/invitations/'+pulse['GuildInvite']['Id'],c,{'Accept':True})
    api('PUT',f'/api/guilds/{g}',a,{'Description':'HTTP 연결 검증','Notice':'실제 공지\n두 번째 줄','JoinMode':'Approval','Revision':1})
    assert api('GET','/api/guilds/me',c)['Guild']['Notice']=='실제 공지\n두 번째 줄'
    api('POST','/api/me/friends/pulse',a,{'Available':True});api('POST','/api/me/friends/pulse',b,{'Available':True})
    roster=api('GET',f'/api/guilds/{g}/members',a)['Rows']
    assert len(roster)==3 and all(x['Status']=='online' for x in roster)
    profile=api('GET',f'/api/guilds/{g}/members/'+b['UserId']+'/profile',a)
    assert profile['Level']==1
    msg={'RequestId':str(uuid.uuid4()),'Text':'실제 길드 채팅 검증'}
    api('POST',f'/api/guilds/{g}/chat',a,msg);api('POST',f'/api/guilds/{g}/chat',a,msg)
    assert len(api('GET',f'/api/guilds/{g}/chat',b)['Rows'])==1
    halls=api('GET','/api/charts/SkillMatch',a)
    hall=next(int(x['HallIdx']) for x in halls if int(x.get('EnterCoin',0))<=20)
    ticket=api('POST','/api/me/friends/challenge',a,{'Other':b['UserId'],'Hall':hall})['Id']
    api('POST','/api/me/friends/challenge/'+ticket,b,{'Accept':True})
    assert api('POST','/api/me/friends/pulse',a,{'Available':True})['Invite']['Id']==ticket
    assert api('POST','/api/me/friends/pulse',b,{'Available':True})['Invite']['Id']==ticket
    api('POST',f'/api/guilds/{g}/leave',b)
    assert api('POST','/api/me/friends/pulse',a,{'Available':True})['Invite'] is None
    api('GET',f'/api/guilds/{g}/chat',b,status=403)
    result['Ok']=True
except Exception as e:
    result['Error']=str(e)
finally:
    for u in users:
        try:
            url,values=form(ADMIN+'/admin/members/delete?id='+u['UserId'],'Confirmation')
            assert [v for k,v in values if k=='ExpectedLogins']==[u['Login']]
            submit(url,values,{'Reason':'길드 기능 HTTP 검증 계정 정리','Confirmation':'삭제'})
            result['Cleanup'].append(u['Role'])
        except Exception as e:result['CleanupError']=str(e)
    (out/'http-result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False))
raise SystemExit(0 if result['Ok'] and not result.get('CleanupError') else 1)
