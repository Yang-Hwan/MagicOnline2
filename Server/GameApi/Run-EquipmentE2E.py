"""Opt-in local API + two real development players; never uses existing member accounts.
Run after Tools/Magic Online/New Match Test/Build Windows Test Player.
Credentials stay in memory and child environments. Artifacts contain no tokens.
"""
import json, os, re, subprocess, time, uuid
from pathlib import Path
from urllib.parse import urljoin
import requests
from bs4 import BeautifulSoup
import psycopg
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
API, ADMIN = 'http://127.0.0.1:5080', 'http://127.0.0.1:5082'
run = uuid.uuid4().hex[:12]
out = ROOT / 'Logs' / 'EquipmentE2E' / run
out.mkdir(parents=True)
fixtures, children, hall = [], [], None
admin = requests.Session()
summary = {'Run': run, 'Cleanup': [], 'Ok': False}

def api(method, path, user=None, data=None):
    r = requests.request(method, API+path, json=data,
                         headers={'Authorization': 'Bearer '+user['AccessToken']} if user else {}, timeout=15)
    if not r.ok:
        raise RuntimeError(f'{method} {path}: {r.status_code} {r.text[:300]}')
    return r.json()

def form(url, field):
    r = admin.get(url, timeout=15); r.raise_for_status()
    soup = BeautifulSoup(r.text, 'html.parser')
    tag = soup.select_one(f'[name="{field}"]')
    if tag is None: raise RuntimeError(f'Missing {field} form at {r.url}')
    f = tag.find_parent('form')
    values = [(i['name'], i.get('value', '')) for i in f.select('input[name]')]
    return urljoin(r.url, f.get('action') or r.url), values

def submit(url, values, extra):
    values = [(k,v) for k,v in values if k not in extra] + list(extra.items())
    r = admin.post(url, data=values, timeout=15, allow_redirects=False)
    if r.status_code not in (302,303):
        s = BeautifulSoup(r.text, 'html.parser').select_one('.error')
        raise RuntimeError(f'Admin post {r.status_code}: {s.get_text() if s else r.url}')

def report(role):
    try: return json.loads((out/(role+'.json')).read_text(encoding='utf-8-sig'))
    except (OSError,ValueError): return {}

def wait(predicate, seconds):
    deadline = time.monotonic()+seconds
    previous = None
    while time.monotonic()<deadline:
        a,b = report('A'),report('B')
        stages = (a.get('Stage'), b.get('Stage'))
        if stages != previous:
            print('Stages:', *stages, flush=True); previous=stages
        for r in (a,b):
            if r.get('Error'): raise RuntimeError(r['Error'])
        if predicate(a,b): return a,b
        for p in children:
            if p.poll() is not None: raise RuntimeError(f'Player exited: {p.returncode}')
        time.sleep(1)
    raise TimeoutError('Two-client stage timeout')

try:
    build = json.loads((ROOT/'Logs/practice-test-build.json').read_text(encoding='utf-8-sig'))
    assert build['result']=='Succeeded', 'A fresh successful player build is required'
    from datetime import datetime, timezone
    built = datetime.fromisoformat(build['startedUtc'].replace('Z','+00:00')).timestamp()
    assert all(p.stat().st_mtime<=built for folder in ('Assets/Scripts','Assets/Editor') for p in (ROOT/folder).rglob('*.cs')), 'Sources changed since build'
    assert api('GET','/health')['Ok']
    secrets = json.loads((Path(os.environ['APPDATA'])/'Microsoft/UserSecrets/MagicCushion-Local-GameApi/secrets.json').read_text(encoding='utf-8-sig'))
    cs = secrets.get('ConnectionStrings:GameDb') or secrets['ConnectionStrings']['GameDb']
    parts = dict(x.split('=',1) for x in cs.split(';') if '=' in x)
    parts = {k.lower():v for k,v in parts.items()}
    assert parts['host'] in ('localhost','127.0.0.1'), 'Local database required'
    db = psycopg.connect(host=parts['host'], port=parts.get('port',5432), dbname=parts['database'], user=parts['username'], password=parts['password'], autocommit=True)
    with db.cursor() as c:
        c.execute("SELECT login_id FROM admin_users WHERE role='Owner' AND NOT disabled ORDER BY created_at LIMIT 1")
        owner = c.fetchone()[0]
    action,values = form(ADMIN+'/admin/login','LoginId')
    submit(action,values,{'LoginId':owner})
    for role in ('A','B'):
        login,password = 'eq_live_'+role+'_'+run, 'E2E-'+uuid.uuid4().hex
        auth=api('POST','/api/auth/register',data={'LoginId':login,'Password':password})
        auth.update(Login=login, Password=password, Role=role); fixtures.append(auth)
        with db.transaction(), db.cursor() as c:
            c.execute('SELECT user_id FROM wallets WHERE user_id=%s FOR UPDATE',(auth['UserId'],))
            c.execute('UPDATE wallets SET jem=jem+1500 WHERE user_id=%s',(auth['UserId'],))
            c.execute('INSERT INTO wallet_ledger(user_id,request_key,coin_delta,jem_delta) VALUES(%s,%s,0,1500)',(auth['UserId'],'Test:EquipmentLive:'+run))
        products=api('GET','/api/shop/products',auth)['Products']
        cue=next(p for p in products if p['Kind']=='Cue' and p['Power']==(20 if role=='A' else -10))
        characters=sorted([p for p in products if p['Kind']=='Character'],key=lambda p:p['PriceGem'])
        character=characters[0 if role=='A' else 1]
        messages=sorted([p for p in products if p['Kind']=='MessagePack'],key=lambda p:p['PriceGem'])
        message=messages[0 if role=='A' else 1]
        auth['Gear']=[cue,character,message]
        for p in auth['Gear']:
            api('POST','/api/shop/purchases',auth,{'RequestId':str(uuid.uuid4()),'ProductId':p['Id'],'ExpectedPriceGem':p['PriceGem'],'ExpectedRevision':p['Revision']})
            api('POST','/api/me/equipment',auth,{'Slot':p['Kind'],'ProductId':p['Id'],'Equipped':True})
        assert all(p['Equipped'] for p in api('GET','/api/me/inventory',auth)['Products'])
    rows=api('GET','/api/charts/SkillMatch',fixtures[0])
    if isinstance(rows,dict): rows=rows.get('Rows',rows.get('Data',rows))
    hall=next(i for i in range(9900,9999) if all(int(r['HallIdx'])!=i for r in rows))
    clone=next(r for r in rows if int(r['MatchBall'])==4).copy()
    clone.update(HallIdx=hall,HallName='장비 통합 검증 '+run,TargetHit=99,MatchTotMin=3,PrizeCoin=1,MaxCoin=1000000,MatchCushion=0,FinishMission=0)
    action,values=form(ADMIN+'/admin/charts/edit?name=SkillMatch','Revision')
    submit(action,values,dict({'Reason':'두 클라이언트 장비 실경기 검증'},**{'Values['+k+']':str(v) for k,v in clone.items()}))
    summary['HallId']=hall
    summary['Fixtures']=[{'UserId':u['UserId'],'Role':u['Role'],'Gear':u['Gear']} for u in fixtures]
    print('Artifacts:',out,flush=True)
    for u in fixtures:
        role=u['Role']; env=os.environ.copy()
        env.update(MAGIC_EQUIPMENT_LOGIN=u['Login'],MAGIC_EQUIPMENT_PASSWORD=u['Password'])
        # Real rendered windows are required: SW_HIDE produces black Unity screenshots.
        # Show without stealing the user's keyboard focus.
        startup=subprocess.STARTUPINFO(); startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW; startup.wShowWindow=4
        args=[build['executable'],'-local-api-public-test','-equipment-match-e2e','-equipment-role',role,'-equipment-hall',str(hall),'-equipment-output',str(out/(role+'.json')),'-logFile',str(out/(role+'.log')),'-screen-fullscreen','0','-screen-width','1280','-screen-height','720']
        children.append(subprocess.Popen(args,cwd=ROOT,env=env,startupinfo=startup))
        if role=='A': wait(lambda a,b:a.get('Stage') in ('waiting','ready'),110)
    a,b=wait(lambda a,b:a.get('Ok') and b.get('Ok'),420)
    # Keep both players alive beyond delayed notification cleanup after leaving Pavilion.
    time.sleep(4)
    for role in ('A','B'):
        log=(out/f'{role}.log').read_text(encoding='utf-8',errors='replace')
        assert not re.search(r'^\w*Exception:',log,re.MULTILINE), f'Unhandled exception in {role}.log'
    summary['PostExitObservationSeconds']=4
    assert a['MatchId']==b['MatchId'] and a['Snapshot']==b['Snapshot']
    assert all(r[k] for r in (a,b) for k in ('NeutralImages','LocalMessage','RemoteMessage','LocalEmotion','RemoteEmotion'))
    assert all(abs(r['TurnSeconds']-r['ExpectedTurnSeconds'])<.01 and abs(r['TipLimit']-r['ExpectedTipLimit'])<.01 for r in (a,b))
    for seat in (0,1):
        sa=next(s for s in a['Shots'] if s['Seat']==seat); sb=next(s for s in b['Shots'] if s['Seat']==seat)
        assert abs(sa['Magnitude']-sb['Magnitude'])<.003 and abs(sa['Follow']-sb['Follow'])<.001
        assert all(abs(sa['Impulse'][axis]-sb['Impulse'][axis])<.003 for axis in ('x','y','z'))
        assert all(abs(sa['Point'][axis]-sb['Point'][axis])<.003 for axis in ('x','y','z'))
        assert all(s['Settled'] and s['MaxDisplacement']>.01 for s in (sa,sb))
        assert all(s['ReactionSeen'] for s in (sa,sb)), 'Automatic character reaction missing'
        assert sa['Emotion']==sb['Emotion'], 'Different automatic reactions for the same shot'
    for role in ('A','B'):
        for stage in ('neutral','messages','shots'):
            with Image.open(out/f'{role}-{stage}.png') as picture:
                assert picture.size==(1280,720) and max(hi-lo for lo,hi in picture.convert('RGB').getextrema())>100, 'Rendered screenshot missing'
    summary.update(Ok=True,MatchId=a['MatchId'],Reports=['A.json','B.json'])
except Exception as e:
    summary['Error']=str(e)
    print('FAILED:',str(e),flush=True)
finally:
    for p in children:
        if p.poll() is None: p.terminate()
    for p in children:
        try:p.wait(timeout=10)
        except subprocess.TimeoutExpired:p.kill()
    try:
        if len(fixtures)==2:
            with db.cursor() as c:
                c.execute('SELECT id FROM matches WHERE phase<>2 AND user_a=ANY(%s::uuid[]) AND user_b=ANY(%s::uuid[])',([u['UserId'] for u in fixtures],[u['UserId'] for u in fixtures]))
                unfinished=[r[0] for r in c.fetchall()]
            for match in unfinished: api('POST','/api/matches/'+match+'/surrender',fixtures[0],{})
        if hall is not None:
            rows=api('GET','/api/charts/SkillMatch',fixtures[0])
            if isinstance(rows,dict):rows=rows.get('Rows',rows.get('Data',rows))
            index=next((i for i,r in enumerate(rows) if int(r['HallIdx'])==hall),None)
            if index is not None:
                action,values=form(ADMIN+f'/admin/charts/delete?name=SkillMatch&row={index}','Revision')
                submit(action,values,{'Reason':'실경기 검증 전용 항목 정리'})
                summary['Cleanup'].append('Temporary hall removed')
        for u in fixtures:
            action,values=form(ADMIN+'/admin/members/delete?id='+u['UserId'],'Confirmation')
            assert [v for k,v in values if k=='ExpectedLogins']==[u['Login']]
            submit(action,values,{'Reason':'두 클라이언트 실경기 검증 계정 정리','Confirmation':'삭제'})
            summary['Cleanup'].append('Fixture '+u['Role']+' deleted through administrator form')
    except Exception as e:
        summary['CleanupError']=str(e); print('CLEANUP:',str(e),flush=True)
    (out/'result.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Result:',out/'result.json',flush=True)
raise SystemExit(0 if summary['Ok'] and not summary.get('CleanupError') else 1)
