"""Local, isolated-schema HTTP checks; no live member/product writes.
Build: dotnet build Server/GameApi/GameApi.csproj --output Logs/CueAbilities/GameApiV3
Run: python Server/GameApi/Run-CueAbilitiesSmoke.py
Override the DLL path with CUE_API_DLL when needed.
"""
import os,json,time,uuid,subprocess
from pathlib import Path
from urllib.parse import urljoin
import requests,psycopg
from psycopg import sql
from bs4 import BeautifulSoup
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'Logs/CueAbilities';OUT.mkdir(parents=True,exist_ok=True)
secrets=json.loads((Path(os.environ['APPDATA'])/'Microsoft/UserSecrets/MagicCushion-Local-GameApi/secrets.json').read_text(encoding='utf-8-sig'))
cs=secrets.get('ConnectionStrings:GameDb') or secrets['ConnectionStrings']['GameDb']
parts={k.strip().lower():v for k,v in (item.split('=',1) for item in cs.split(';') if '=' in item)}
assert parts['host'] in ('localhost','127.0.0.1')
db=psycopg.connect(host=parts['host'],port=parts.get('port',5432),dbname=parts['database'],user=parts['username'],password=parts['password'],autocommit=True)
schema='cue_http_test_'+uuid.uuid4().hex
with db.cursor() as c:c.execute(sql.SQL('CREATE SCHEMA {}').format(sql.Identifier(schema)))
base='http://127.0.0.1:5093';admin=requests.Session();child=None;checks=0;result={'passed':False}
def check(ok,label):
 global checks
 if not ok:raise RuntimeError(label)
 checks+=1;print('PASS',label,flush=True)
def form(url):
 r=admin.get(url,timeout=30);r.raise_for_status();soup=BeautifulSoup(r.text,'html.parser');f=next((f for f in soup.find_all('form') if f.select_one('[name="Input.Kind"],[name="LoginId"],[name="Revision"]')),None);assert f
 values=[]
 for el in f.select('input[name],select[name],textarea[name]'):
  name=el['name']
  if el.name=='select':value=(el.select_one('option[selected]') or el.select_one('option')).get('value','')
  elif el.name=='textarea':value=el.text
  elif el.get('type')=='checkbox':
   if not el.has_attr('checked'):continue
   value=el.get('value','true')
  else:value=el.get('value','')
  values.append((name,value))
 return urljoin(r.url,f.get('action') or r.url),values,soup
def post_form(url,values,extra,valid=True):
 values=[(k,v) for k,v in values if k not in extra]+list(extra.items());r=admin.post(url,data=values,allow_redirects=False,timeout=30)
 if valid and r.status_code not in (302,303):
  err=BeautifulSoup(r.text,'html.parser').select_one('.error');raise RuntimeError('Form failed: '+str(r.status_code)+' '+(err.get_text(' ',strip=True) if err else ''))
 return r
def api(method,path,user=None,data=None):
 r=requests.request(method,base+path,json=data,headers={'Authorization':'Bearer '+user['AccessToken']} if user else {},timeout=30);r.raise_for_status();return r.json()
try:
 env=os.environ.copy();env['ASPNETCORE_ENVIRONMENT']='Development';env['ConnectionStrings__GameDb']=cs.rstrip(';')+';Search Path='+schema
 log=open(OUT/'http-server.log','w',encoding='utf-8')
 child=subprocess.Popen(['dotnet',os.environ.get('CUE_API_DLL',str(OUT/'GameApiV3/GameApi.dll')),'--urls',base],cwd=ROOT/'Server/GameApi',env=env,stdout=log,stderr=subprocess.STDOUT,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
 deadline=time.monotonic()+90
 while True:
  try:
   if api('GET','/health')['Ok']:break
  except requests.RequestException:pass
  if child.poll() is not None:raise RuntimeError('Isolated HTTP server failed to start')
  if time.monotonic()>deadline:raise TimeoutError('Isolated HTTP server startup timed out')
  time.sleep(.3)
 action,values,_=form(base+'/admin/login');post_form(action,values,{'LoginId':'cue_test_admin'})
 action,values,page=form(base+'/admin/charts/shop/edit?kind=Cue')
 check(all(page.select_one('[name="'+n+'"]') for n in ['Input.Grade','Input.Initial[0]','Input.Maximum[3]','Input.Reach']),'administrator form contains grade, initial/max levels and reach')
 name='cue-test-'+uuid.uuid4().hex
 fields={'Input.Kind':'Cue','Input.Name':name,'Input.Visual':'Gameplay/ShopCues/Thunder','Input.Price':'0','Input.Visible':'true','Input.Purchasable':'true','Input.Grade':'B','Input.Reach':'2'}
 for i,v in enumerate([60,70,55,55]):fields['Input.Initial['+str(i)+']']=str(v)
 for i,v in enumerate([80,85,90,95]):fields['Input.Maximum['+str(i)+']']=str(v)
 invalid=fields|{'Input.Initial[0]':'101'}
 r=post_form(action,values,invalid,False);check(r.status_code==200 and bool(BeautifulSoup(r.text,'html.parser').select_one('.error').get_text(strip=True)),'invalid initial/max range rejected')
 action,values,_=form(base+'/admin/charts/shop/edit?kind=Cue');post_form(action,values,fields)
 a=api('POST','/api/auth/register',data={'LoginId':'cue_a_'+uuid.uuid4().hex[:16],'Password':'CueTest-'+uuid.uuid4().hex})
 b=api('POST','/api/auth/register',data={'LoginId':'cue_b_'+uuid.uuid4().hex[:16],'Password':'CueTest-'+uuid.uuid4().hex})
 products=api('GET','/api/shop/products',a)['Products'];cue=next(p for p in products if p['Name']==name)
 check(cue['CueAbility']['Grade']=='B' and cue['CueAbility']['Initial']==[60,70,55,55] and cue['CueAbility']['Reach']==2,'administrator save reaches actual catalog')
 action,values,page=form(base+'/admin/charts/shop/edit/'+str(cue['Id']))
 check(page.select_one('[name="Input.Initial[0]"]')['value']=='60' and page.select_one('[name="Input.Maximum[3]"]')['value']=='95','administrator edit round-trips four capability levels')
 api('POST','/api/shop/purchases',a,{'RequestId':str(uuid.uuid4()),'ProductId':cue['Id'],'ExpectedPriceGem':0,'ExpectedRevision':cue['Revision']})
 api('POST','/api/me/equipment',a,{'Slot':'Cue','ProductId':cue['Id'],'Equipped':True})
 equipped=api('GET','/api/me/cue',a)
 check(abs(equipped['PowerScale']-.54)<.00001 and abs(equipped['FollowScale']-.63)<.00001 and equipped['TipScale']==.5,'equipped server profile applies B grade to 60/70/55 levels')
 check(equipped['CueTimeMin']==20 and equipped['CueTimeMax']==40,'profile freezes Cue/Sec range')
 match=uuid.uuid4().hex;snap=api('POST','/api/matches/'+match+'/equipment',a,{'OpponentUserId':b['UserId']})
 check(snap['Players'][0]['Power']==60,'friendly match uses owned cue levels')
 run=str(uuid.uuid4());point={'RunId':run,'Shot':1,'CueId':cue['Id']}
 first=api('POST','/api/me/cue-progress/practice',a,point);retry=api('POST','/api/me/cue-progress/practice',a,point)
 check(first['CueProgress']==retry['CueProgress']==1,'point retry advances progress once')
 member=admin.get(base+'/admin/members/'+a['UserId']+'?tab=cues',timeout=30);member.raise_for_status()
 page=BeautifulSoup(member.text,'html.parser');card=page.select_one('.member-cue')
 check(card is not None and name in card.get_text() and card.select_one('progress')['value']=='1' and card.select_one('progress')['max']=='2','member cue tab shows owned cue and actual pending progress')
 other=admin.get(base+'/admin/members/'+b['UserId']+'?tab=cues',timeout=30);other.raise_for_status()
 check(name not in BeautifulSoup(other.text,'html.parser').select_one('.main').get_text(),'member cue tab isolates ownership')

 point['Shot']=2;grown=api('POST','/api/me/cue-progress/practice',a,point)
 check(grown['CueProgress']==2 and grown['Power']==61 and grown['FollowThrough']==71,'reach 2 upgrades levels after two normal scoring shots')
 member=admin.get(base+'/admin/members/'+a['UserId']+'?tab=cues',timeout=30);member.raise_for_status()
 card=BeautifulSoup(member.text,'html.parser').select_one('.member-cue')
 check(card.select_one('tbody tr strong').get_text()=='61' and card.select_one('progress')['value']=='0','member cue tab shows upgraded level and reset pending progress')

 check(api('GET','/api/matches/'+match+'/equipment',a)['Players'][0]['Power']==60,'existing match snapshot is not changed by growth')
 action,values,_=form(base+'/admin/charts/shop/edit/'+str(cue['Id']));post_form(action,values,{'Input.Reach':'50'})
 check(api('GET','/api/me/cue',a)['Power']==61,'editing reach preserves earned growth')
 check(api('GET','/api/me/cue',a)['Ability']['Reach']==2,'owned cue keeps its applied settings after catalog edit')
 action,values,_=form(base+'/admin/charts/shop/edit/'+str(cue['Id']));post_form(action,values,{'Input.Initial[0]':'70'})
 check(api('GET','/api/me/cue',a)['Power']==61,'catalog initial level edit does not change an owned cue')
 member_url=base+'/admin/members/'+a['UserId']+'?tab=cues'
 page=BeautifulSoup(admin.get(member_url,timeout=30).text,'html.parser')
 reset_form=page.select_one('.cue-reset form');assert reset_form
 reset_url=urljoin(member_url,reset_form['action'])
 reset_values=[(e['name'],e.get('value','')) for e in reset_form.select('input[name]') if e.get('type')!='checkbox']
 r=post_form(reset_url,reset_values,{},False)
 check(r.status_code==400,'cue reset requires explicit confirmation')
 post_form(reset_url,reset_values,{'confirmReset':'true','reason':'isolated test reset'})
 reset=api('GET','/api/me/cue',a)
 check(reset['Power']==70 and reset['Ability']['Reach']==50 and reset['CueProgress']==0 and reset['CueSteps']==0 and reset['CuePending']==0,'reset applies latest catalog settings and clears only cue growth')
 check(reset['CueId']==cue['Id'],'reset preserves equipped ownership')
 point['Shot']=3
 after_reset=api('POST','/api/me/cue-progress/practice',a,point)
 check(after_reset['CuePending']==1 and after_reset['CueProgress']==1,'post-reset growth uses newly applied reach')
 post_form(reset_url,reset_values,{'confirmReset':'true'})
 check(api('GET','/api/me/cue',a)['CuePending']==1,'repeated reset submission cannot erase later progress')
 point['Shot']=1
 check(api('POST','/api/me/cue-progress/practice',a,point)['CueProgress']==1,'pre-reset point retry cannot be credited twice')
 check(api('GET','/api/matches/'+match+'/equipment',a)['Players'][0]['Power']==60,'cue reset preserves running match snapshot')
 with db.cursor() as c:
  c.execute(sql.SQL("SELECT count(*) FROM {}.admin_audit_logs WHERE action='MemberCueReset'").format(sql.Identifier(schema)))
  check(c.fetchone()[0]==1,'reset creates exactly one audit record')

 r=requests.post(base+'/api/me/cue-progress/practice',json={'RunId':str(uuid.uuid4()),'Shot':1,'CueId':9223372036854775807},headers={'Authorization':'Bearer '+a['AccessToken']},timeout=30)
 check(r.status_code==403,'cannot progress an unowned cue')
 codes=api('GET','/api/charts/CommonCode',a);index=next(i for i,row in enumerate(codes) if row['GrpCd']=='Cue' and row['ComCd']=='Sec')
 action,values,_=form(base+'/admin/charts/edit?name=CommonCode&row='+str(index))
 r=post_form(action,values,{'Values[ComVal]':'50','Values[ComVal2]':'20'},False)
 check(r.status_code==400 and bool(BeautifulSoup(r.text,'html.parser').select_one('.error').get_text(strip=True)),'reversed Cue/Sec range rejected')
 action,values,_=form(base+'/admin/charts/edit?name=CommonCode&row='+str(index));post_form(action,values,{'Values[ComVal]':'20','Values[ComVal2]':'60'})
 check(api('GET','/api/me/cue',a)['CueTimeMax']==60,'edited common code reaches next equipment profile')
 check(api('GET','/api/matches/'+match+'/equipment',a)['Players'][0]['CueTimeMax']==40,'current match keeps original time range')
 result.update(passed=True,checks=checks,isolatedSchema=True,liveAccountsUntouched=True)
except Exception as e:
 result['error']=str(e);print('FAIL',str(e),flush=True)
finally:
 if child and child.poll() is None:
  child.terminate()
  try:child.wait(timeout=15)
  except subprocess.TimeoutExpired:child.kill();child.wait()
 with db.cursor() as c:c.execute(sql.SQL('DROP SCHEMA {} CASCADE').format(sql.Identifier(schema)))
 db.close();result['cleaned']=True;(OUT/'http-result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
 print('HTTP cue checks:',checks,'passed' if result['passed'] else 'FAILED',flush=True)
raise SystemExit(0 if result['passed'] else 1)
