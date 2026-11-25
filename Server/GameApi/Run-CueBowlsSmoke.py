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
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'Logs/Bowls';OUT.mkdir(parents=True,exist_ok=True)
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
 child=subprocess.Popen(['dotnet',os.environ.get('CUE_API_DLL',str(OUT/'GameApi/GameApi.dll')),'--urls',base],cwd=ROOT/'Server/GameApi',env=env,stdout=log,stderr=subprocess.STDOUT,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
 deadline=time.monotonic()+90
 while True:
  try:
   if api('GET','/health')['Ok']:break
  except requests.RequestException:pass
  if child.poll() is not None:raise RuntimeError('Isolated HTTP server failed to start')
  if time.monotonic()>deadline:raise TimeoutError('Isolated HTTP server startup timed out')
  time.sleep(.3)
 action,values,_=form(base+'/admin/login');post_form(action,values,{'LoginId':'bowl_test_admin'})
 action,values,page=form(base+'/admin/charts/shop/edit?kind=Cue')
 check(all(page.select_one('[name="Input.BowlMaximum['+str(i)+']"]') for i in range(5)),'admin has all five bowl limit fields')
 check(page.select_one('[name="Input.BowlAuto"]') is not None,'automatic drinking is configured on the cue product')
 name='bowl-test-'+uuid.uuid4().hex
 fields={'Input.Kind':'Cue','Input.Name':name,'Input.Visual':'Gameplay/ShopCues/Thunder','Input.Price':'0','Input.Visible':'true','Input.Purchasable':'true','Input.Grade':'A','Input.Reach':'2'}
 for i in range(5):
  fields['Input.Initial['+str(i)+']']='50';fields['Input.Maximum['+str(i)+']']='100';fields['Input.BowlMaximum['+str(i)+']']=str(2 if i==0 else 1)
 r=post_form(action,values,fields|{'Input.BowlMaximum[0]':'0'},False)
 check(r.status_code==200 and bool(BeautifulSoup(r.text,'html.parser').select_one('.error').get_text(strip=True)),'admin rejects zero bowl limit')
 action,values,_=form(base+'/admin/charts/shop/edit?kind=Cue');post_form(action,values,fields)
 def register():return api('POST','/api/auth/register',data={'LoginId':'bowl_'+uuid.uuid4().hex[:16],'Password':'Test-'+uuid.uuid4().hex})
 a,b=register(),register()
 cue=next(p for p in api('GET','/api/shop/products',a)['Products'] if p['Name']==name);cid=cue['Id']
 check(cue['CueAbility']['BowlMaximum']==[2,1,1,1,1],'admin limits round-trip into catalog')
 api('POST','/api/shop/purchases',a,{'RequestId':str(uuid.uuid4()),'ProductId':cid,'ExpectedPriceGem':0,'ExpectedRevision':cue['Revision']})
 api('POST','/api/me/equipment',a,{'Slot':'Cue','ProductId':cid,'Equipped':True})
 def item():return next(p for p in api('GET','/api/me/inventory',a)['Products'] if p['Id']==cid)
 run=str(uuid.uuid4());shot=0
 def point(points=1):
  global shot
  shot+=1;data={'RunId':run,'Shot':shot,'CueId':cid,'Points':points};return api('POST','/api/me/cue-progress/practice',a,data),data
 profile,first=point(2);check(sum(item()['CueBowls']['Filled'])==2 and item()['CuePending']==0,'two scored points fill exactly two bowl units without direct progress')
 api('POST','/api/me/cue-progress/practice',a,first);check(sum(item()['CueBowls']['Filled'])==2,'duplicate point is idempotent')
 for i in range(4):point()
 full=item();check(full['CueBowls']['Filled']==[2,1,1,1,1] and full['CuePending']==0,'full manual round waits for drinking')
 for i in range(8):_,discarded=point()
 check(item()['CueProgress']==6 and item()['CuePending']==0,'full round loses later scores without a pending balance')
 action,values,_=form(base+'/admin/charts/shop/edit/'+str(cid));post_form(action,values,{'Input.BowlMaximum['+str(i)+']':'1' for i in range(5)})
 check(item()['CueBowls']['Maximum']==[2,1,1,1,1],'admin edits preserve current cycle limits')
 action0={'CueId':cid,'Operation':str(uuid.uuid4()),'Cycle':0,'Index':0}
 state=api('POST','/api/me/cue-bowls/drink',a,action0);check(state['Drunk']==[True,False,False,False,False],'manual drinking approves only chosen bowl')
 api('POST','/api/me/cue-bowls/drink',a,action0);check(item()['CuePending']==0,'repeated drink operation cannot grant progress')
 with db.cursor() as c:c.execute(sql.SQL("UPDATE {}.shop_cue_progress SET bowls=jsonb_set(bowls,'{{Filled,0}}','2'::jsonb) WHERE user_id=%s AND product_id=%s").format(sql.Identifier(schema)),(a['UserId'],cid))
 check(item()['CueBowls']['Filled'][0]==0 and item()['CuePending']==0,'legacy approved bowl is consumed visually without another growth credit')

 for i in range(1,5):state=api('POST','/api/me/cue-bowls/drink',a,action0|{'Operation':str(uuid.uuid4()),'Index':i})
 check(state['Cycle']==1 and state['Maximum']==[1]*5 and state['Filled']==[0]*5 and item()['CuePending']==1,'fifth approval grants one progress and begins updated cycle')
 api('POST','/api/me/cue-progress/practice',a,discarded);check(sum(item()['CueBowls']['Filled'])==0,'discarded score retry stays discarded after drinking')
 r=requests.post(base+'/api/me/cue-bowls/drink',headers={'Authorization':'Bearer '+a['AccessToken']},json=action0|{'Operation':str(uuid.uuid4())},timeout=30);check(r.status_code==409,'stale cycle drink rejected')
 r=requests.post(base+'/api/me/cue-bowls/auto',headers={'Authorization':'Bearer '+b['AccessToken']},json={'CueId':cid,'Operation':str(uuid.uuid4()),'Enabled':True},timeout=30);check(r.status_code==404,'member automatic-drink override endpoint is removed')
 r=requests.post(base+'/api/me/cue-bowls/drink',json=action0,timeout=30);check(r.status_code==401,'anonymous drink rejected')
 for i in range(5):point()
 check(item()['CuePending']==1,'second full manual round still requires approval')
 action,values,_=form(base+'/admin/charts/shop/edit/'+str(cid));post_form(action,values,{'Input.BowlAuto':'true'})
 state=item()['CueBowls']
 check(state['Auto'] and state['Cycle']==2 and item()['CueSteps']==1 and item()['CuePending']==0,'product auto policy approves existing full bowls and levels up at reach')
 check(item()['CueSteps']==1,'repeated inventory reads cannot approve the same round again')
 for i in range(5):point()
 check(item()['CueBowls']['Cycle']==3 and item()['CuePending']==1 and item()['CueBowls']['Auto'],'automatic drinking continues into subsequent cycles')
 # Concurrent retries are serialized under the same wallet lock.
 from concurrent.futures import ThreadPoolExecutor
 shot+=1;duplicate={'RunId':run,'Shot':shot,'CueId':cid}
 before=item()['CueProgress']
 with ThreadPoolExecutor(max_workers=5) as pool:list(pool.map(lambda _:api('POST','/api/me/cue-progress/practice',a,duplicate),range(5)))
 check(item()['CueProgress']==before+1,'concurrent duplicate scoring requests award only once')
 member=admin.get(base+'/admin/members/'+a['UserId']+'?tab=cues',timeout=30);member.raise_for_status()
 check('바가지 자동 마시기' in BeautifulSoup(member.text,'html.parser').get_text() and '켜짐' in BeautifulSoup(member.text,'html.parser').get_text(),'administrator member page displays live bowl state')
 action,values,_=form(base+'/admin/charts/shop/edit/'+str(cid));post_form(action,values,{'Input.BowlAuto':'false'})
 check(not item()['CueBowls']['Auto'],'turning off product policy restores manual mode for existing owners')
 member_url=base+'/admin/members/'+a['UserId']+'?tab=cues'
 reset_form=BeautifulSoup(member.text,'html.parser').select_one('.cue-reset form');assert reset_form
 reset_url=urljoin(member_url,reset_form['action']);reset_values=[(e['name'],e.get('value','')) for e in reset_form.select('input[name]') if e.get('type')!='checkbox']
 prior_cycle=item()['CueBowls']['Cycle'];post_form(reset_url,reset_values,{'confirmReset':'true','reason':'isolated bowl reset'})
 reset=item();check(reset['CuePending']==0 and reset['CueSteps']==0 and reset['CueBowls']['Filled']==[0]*5 and reset['CueBowls']['Cycle']==prior_cycle+1,'administrator reset clears bowls and changes the cycle')
 post_form(reset_url,reset_values,{'confirmReset':'true','reason':'isolated bowl reset'})
 check(item()['CueBowls']['Cycle']==prior_cycle+1,'administrator reset retry does not reset the next round again')
 # Only follow-through can grow. Maximum-level rows are excluded by the server.
 action,values,_=form(base+'/admin/charts/shop/edit/'+str(cid))
 changes={'Input.Reach':'1','Input.BowlAuto':'false'}
 for i in range(5):changes['Input.Initial['+str(i)+']']='50' if i==1 else '100';changes['Input.Maximum['+str(i)+']']='51' if i==1 else '100'
 post_form(action,values,changes)
 page=BeautifulSoup(admin.get(member_url,timeout=30).text,'html.parser');reset_form=page.select_one('.cue-reset form')
 reset_url=urljoin(member_url,reset_form['action']);reset_values=[(e['name'],e.get('value','')) for e in reset_form.select('input[name]') if e.get('type')!='checkbox']
 post_form(reset_url,reset_values,{'confirmReset':'true','reason':'isolated cap test'})
 current=item();check(current['CueBowls']['Completed']==[True,False,True,True,True],'maximum-level rows are marked completed on inventory read')
 _,point_data=point(2);current=item()
 check(current['CueBowls']['Filled']==[0,1,0,0,0],'only the remaining non-maxed stat receives scores; full excess is discarded')
 invalid={'CueId':cid,'Operation':str(uuid.uuid4()),'Cycle':current['CueBowls']['Cycle'],'Index':0}
 r=requests.post(base+'/api/me/cue-bowls/drink',headers={'Authorization':'Bearer '+a['AccessToken']},json=invalid,timeout=30)
 check(r.status_code==409,'maximum-level stat cannot be grown by a forged API request')
 grow_all=invalid|{'Operation':str(uuid.uuid4()),'Index':-1}
 done=api('POST','/api/me/cue-bowls/drink',a,grow_all)
 check(done['Completed']==[True]*5 and done['Filled']==[0]*5 and item()['CueSteps']==1,'atomic grow-all excludes capped rows and consumes all eligible bowls once')
 api('POST','/api/me/cue-bowls/drink',a,grow_all);check(item()['CueSteps']==1,'atomic grow-all retry cannot grant a second level')
 cycle=done['Cycle'];point(2);check(item()['CueBowls']['Cycle']==cycle and item()['CueSteps']==1,'fully maxed cue discards scores without free completed cycles')
 action,values,_=form(base+'/admin/charts/shop/edit/'+str(cid));post_form(action,values,{'Input.BowlAuto':'true'})
 check(item()['CueBowls']['Cycle']==cycle and item()['CueSteps']==1,'automatic mode cannot create growth when every stat is maxed')
 html=BeautifulSoup(admin.get(member_url,timeout=30).text,'html.parser').get_text()
 check('완료 · 배분 제외' in html,'administrator member page identifies completed excluded stats')
 # All five ready bowls are committed by one request, without waiting for UI countdowns.
 action,values,_=form(base+'/admin/charts/shop/edit/'+str(cid))
 changes={'Input.BowlAuto':'false'}
 for i in range(5):changes['Input.Initial['+str(i)+']']='50';changes['Input.Maximum['+str(i)+']']='100'
 post_form(action,values,changes)
 page=BeautifulSoup(admin.get(member_url,timeout=30).text,'html.parser');reset_form=page.select_one('.cue-reset form')
 reset_url=urljoin(member_url,reset_form['action']);reset_values=[(e['name'],e.get('value','')) for e in reset_form.select('input[name]') if e.get('type')!='checkbox']
 post_form(reset_url,reset_values,{'confirmReset':'true','reason':'isolated atomic growth test'})
 for i in range(5):point()
 current=item();batch={'CueId':cid,'Operation':str(uuid.uuid4()),'Cycle':current['CueBowls']['Cycle'],'Index':-1}
 api('POST','/api/me/cue-bowls/drink',a,batch)
 check(item()['CueSteps']==1 and item()['CueBowls']['Filled']==[0]*5,'one request persists all five approvals before any client animation')
 point();before=item()['CueProgress'];api('POST','/api/me/cue-bowls/drink',a,batch)
 check(item()['CueProgress']==before and sum(item()['CueBowls']['Filled'])==1,'replayed batch operation preserves newly earned next-cycle points')
 # Existing cycles may have approved every non-capped row under the previous rule.
 prior=item();old_state=prior['CueBowls']|{'Filled':[1,0,0,0,0],'Drunk':[False,True,True,True,True],'Completed':[False]*5}
 with db.cursor() as c:
  c.execute(sql.SQL("UPDATE {}.shop_user_inventory SET cue_ability=jsonb_set(cue_ability,'{{Initial,0}}','100'::jsonb) WHERE user_id=%s AND product_id=%s").format(sql.Identifier(schema)),(a['UserId'],cid))
  c.execute(sql.SQL("UPDATE {}.shop_cue_progress SET bowls=%s::jsonb WHERE user_id=%s AND product_id=%s").format(sql.Identifier(schema)),(json.dumps(old_state),a['UserId'],cid))
 current=item();check(current['CueSteps']==prior['CueSteps']+1 and current['CueBowls']['Cycle']==prior['CueBowls']['Cycle']+1,'legacy cycle with all eligible rows approved completes once after capped-row exclusion')
 check(item()['CueSteps']==current['CueSteps'],'repeated normalization does not grant another progress round')
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
