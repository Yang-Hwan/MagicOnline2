"""Local API smoke: premium cues purchased/equipped by isolated fixtures, then admin-cleaned."""
import json,os,uuid
from pathlib import Path
from urllib.parse import urljoin
import requests,psycopg
from bs4 import BeautifulSoup
ROOT=Path(__file__).resolve().parents[2];API='http://127.0.0.1:5080';ADMIN='http://127.0.0.1:5082'
run=uuid.uuid4().hex[:10];out=ROOT/'Logs/PremiumCues'/run;out.mkdir(parents=True)
admin=requests.Session();users=[];result={'Ok':False,'Checks':0,'Cleanup':[]}
def api(method,path,user=None,data=None):
    r=requests.request(method,API+path,json=data,headers={'Authorization':'Bearer '+user['AccessToken']} if user else {},timeout=20)
    assert r.ok,f'{method} {path}: {r.status_code} {r.text[:200]}'
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
    with psycopg.connect(host=parts['host'],port=parts.get('port',5432),dbname=parts['database'],user=parts['username'],password=parts['password'],autocommit=True) as db:
        with db.cursor() as c:
            c.execute("SELECT login_id FROM admin_users WHERE role='Owner' AND NOT disabled ORDER BY created_at LIMIT 1");owner=c.fetchone()[0]
        action,values=form(ADMIN+'/admin/login','LoginId');submit(action,values,{'LoginId':owner})
        for role in ('A','B'):
            login='premium_test_'+role+'_'+run;u=api('POST','/api/auth/register',data={'LoginId':login,'Password':'Premium-'+uuid.uuid4().hex});u.update(Login=login,Role=role);users.append(u)
        a,b=users
        with db.transaction(),db.cursor() as c:
            c.execute('SELECT user_id FROM wallets WHERE user_id=%s FOR UPDATE',(a['UserId'],));c.execute('UPDATE wallets SET jem=jem+10000 WHERE user_id=%s',(a['UserId'],))
            c.execute('INSERT INTO wallet_ledger(user_id,request_key,coin_delta,jem_delta) VALUES(%s,%s,0,10000)',(a['UserId'],'Test:Premium:'+run))
        products=[p for p in api('GET','/api/shop/products',a)['Products'] if (p['Visual'] or '').startswith('Gameplay/PremiumCues/')]
        assert len(products)==3 and {p['Name'] for p in products}=={'뇌신 큐','성운 큐','홍염 큐'}
        result['Products']=[{'Name':p['Name'],'PriceGem':p['PriceGem']} for p in products]
        for p in products:
            request={'RequestId':str(uuid.uuid4()),'ProductId':p['Id'],'ExpectedPriceGem':p['PriceGem'],'ExpectedRevision':p['Revision']}
            api('POST','/api/shop/purchases',a,request);assert api('POST','/api/shop/purchases',a,request)['AlreadyProcessed']
            api('POST','/api/me/equipment',a,{'Slot':'Cue','ProductId':p['Id'],'Equipped':True})
            owned=next(x for x in api('GET','/api/me/inventory',a)['Products'] if x['Id']==p['Id']);assert owned['Equipped']
            match=uuid.uuid4().hex
            host=api('POST','/api/matches/'+match+'/equipment',a,{'OpponentUserId':b['UserId']})
            peer=api('GET','/api/matches/'+match+'/equipment',b)
            assert host==peer and peer['Players'][0]['CueVisual']==p['Visual']
            assert peer['Players'][0]['Power']==0 and peer['Players'][0]['Tip']==0
        result['Ok']=True
except Exception as e:result['Error']=str(e)
finally:
    for u in users:
        try:
            action,values=form(ADMIN+'/admin/members/delete?id='+u['UserId'],'Confirmation');assert [v for k,v in values if k=='ExpectedLogins']==[u['Login']]
            submit(action,values,{'Reason':'프리미엄 큐대 구매·장착·스냅샷 검증 계정 정리','Confirmation':'삭제'});result['Cleanup'].append(u['Role'])
        except Exception as e:result['CleanupError']=str(e)
    (out/'http.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(result,ensure_ascii=False))
raise SystemExit(0 if result['Ok'] and not result.get('CleanupError') else 1)
