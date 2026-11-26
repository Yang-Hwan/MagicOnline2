"""Apply the additive daily-cue migration to this machine's local DB and verify admin listing.
Requires the existing local API/admin servers; never reads or changes member wallets.
"""
import hashlib,json,os,uuid
from pathlib import Path
from urllib.parse import urljoin
import psycopg,requests
from bs4 import BeautifulSoup

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Logs/DailyCues';OUT.mkdir(parents=True,exist_ok=True)
rows=json.loads((ROOT/'Docs/DailyCueSkins20261126.json').read_text(encoding='utf-8'))
prefix='Gameplay/DailyCues20261126/'
for row in rows:
    assert (ROOT/'Assets/Resources'/prefix/(row['key']+'.png')).is_file(),row['key']
secrets=json.loads((Path(os.environ['APPDATA'])/'Microsoft/UserSecrets/MagicCushion-Local-GameApi/secrets.json').read_text(encoding='utf-8-sig'))
cs=secrets.get('ConnectionStrings:GameDb') or secrets['ConnectionStrings']['GameDb']
parts={k.strip().lower():v for k,v in (x.split('=',1) for x in cs.split(';') if '=' in x)}
assert parts['host'] in ('localhost','127.0.0.1'),'Local DB only'
file=ROOT/'Server/GameApi/Migrations/028_daily_cue_skins_20261126.sql'
sql=file.read_text(encoding='utf-8');checksum=hashlib.sha256(sql.encode('utf-8')).hexdigest().upper()
with psycopg.connect(host=parts['host'],port=parts.get('port',5432),dbname=parts['database'],user=parts['username'],password=parts['password']) as db:
    with db.cursor() as c:
        c.execute('SELECT pg_advisory_xact_lock(687451003)')
        c.execute("SELECT p.id,p.name,p.revision FROM shop_products p WHERE NOT EXISTS(SELECT FROM shop_cues q WHERE q.product_id=p.id AND q.visual LIKE %s) ORDER BY p.id",(prefix+'%',));before=c.fetchall()
        c.execute('SELECT checksum FROM schema_migrations WHERE name=%s',(file.name,));previous=c.fetchone()
        if previous:assert previous[0]==checksum,'Previously applied migration differs'
        else:
            c.execute(sql)
            c.execute('INSERT INTO schema_migrations(name,checksum) VALUES(%s,%s)',(file.name,checksum))
        # Idempotency is checked inside the same transaction.
        c.execute(sql)
        c.execute("SELECT p.id,p.name,p.price_gem,q.visual,p.visible,p.purchasable,q.ability FROM shop_products p JOIN shop_cues q ON q.product_id=p.id WHERE q.visual LIKE %s ORDER BY p.sort_order,p.id",(prefix+'%',));products=c.fetchall()
        assert len(products)==39 and len({p[3] for p in products})==39
        expected={prefix+r['key']:r for r in rows}
        for p in products:
            r=expected[p[3]];assert p[1]==r['name']+' 큐' and p[2]==r['priceGem'] and p[4] and p[5]
            assert p[6]['Initial']==[50]*5 and p[6]['Maximum']==[100]*5
        c.execute("SELECT p.id,p.name,p.revision FROM shop_products p WHERE NOT EXISTS(SELECT FROM shop_cues q WHERE q.product_id=p.id AND q.visual LIKE %s) ORDER BY p.id",(prefix+'%',));assert c.fetchall()==before,'Existing product changed'
        c.execute("SELECT login_id FROM admin_users WHERE role='Owner' AND NOT disabled ORDER BY created_at LIMIT 1");owner=c.fetchone()[0]
        catalog_sql=(ROOT/'Server/GameApi/ShopCatalog.cs').read_text(encoding='utf-8').split('"""')[1]
        catalog_sql=catalog_sql.replace('$1',"'00000000-0000-0000-0000-000000000000'::uuid").replace('$2','false')
        c.execute(catalog_sql);catalog=json.loads(c.fetchone()[0])
        assert len([p for p in catalog['Products'] if p['Visual'].startswith(prefix)])==39
        (OUT/'catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2),encoding='utf-8')
    db.commit()
session=requests.Session();base='http://127.0.0.1:5082'
r=session.get(base+'/admin/login',timeout=20);r.raise_for_status();soup=BeautifulSoup(r.text,'html.parser');form=soup.select_one('[name="LoginId"]').find_parent('form')
values=[(i['name'],i.get('value','')) for i in form.select('input[name]') if i['name']!='LoginId']+[('LoginId',owner)]
r=session.post(urljoin(r.url,form.get('action') or r.url),data=values,timeout=20);r.raise_for_status();assert '/admin/login' not in r.url
r=session.get(base+'/admin/charts/shop?kind=Cue',timeout=20);r.raise_for_status();soup=BeautifulSoup(r.text,'html.parser');listed=[t.get_text(' ',strip=True) for t in soup.select('tbody tr')]
assert all(any(str(p[0]) in text and p[1] in text for text in listed) for p in products)
result={'passed':True,'products':39,'existingProductsPreserved':len(before),'duplicateMigrationSafe':True,'adminListed':39,'apiCatalogQueryListed':39,'priceMin':min(p[2] for p in products),'priceMax':max(p[2] for p in products)}
(OUT/'registration.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'products.json').write_text(json.dumps([{'Id':p[0],'Name':p[1],'PriceGem':p[2],'Visual':p[3]} for p in products],ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(result,ensure_ascii=False))
