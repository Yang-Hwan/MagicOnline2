"""HTTP checks in a disposable PostgreSQL schema and separate image directory.

Uses only the existing local DB credentials. Never changes live shop/member data.
Run after building: python Server/GameApi/Run-ShopImagesSmoke.py
"""
import hashlib
import json
import os
from pathlib import Path
import shutil
import struct
import subprocess
import time
import uuid
import zlib

import psycopg
from psycopg import sql
import requests
from bs4 import BeautifulSoup

ROOT = Path(__file__).resolve().parents[2]
run = uuid.uuid4().hex[:12]
schema = 'shop_images_test_' + run
out = ROOT / 'Logs' / 'ShopImages' / run
out.mkdir(parents=True)
base = 'http://127.0.0.1:5083'
secrets = json.loads((Path(os.environ['APPDATA']) / 'Microsoft/UserSecrets/MagicCushion-Local-GameApi/secrets.json').read_text(encoding='utf-8-sig'))
connection = secrets.get('ConnectionStrings:GameDb') or secrets['ConnectionStrings']['GameDb']
parts = {k.strip().lower(): v for k, v in (p.split('=', 1) for p in connection.split(';') if '=' in p)}
assert parts['host'] in ('localhost', '127.0.0.1'), 'Local database only'
db = psycopg.connect(host=parts['host'], port=parts.get('port', 5432), dbname=parts['database'], user=parts['username'], password=parts['password'], autocommit=True)
checks = []
process = None


def check(ok, label):
    assert ok, label
    checks.append(label)
    print('PASS:', label, flush=True)


def form(session, path):
    response = session.get(base + path, timeout=30)
    response.raise_for_status()
    soup = BeautifulSoup(response.text, 'html.parser')
    target = soup.select_one('#shop-edit-form') or soup.select_one('input[name="LoginId"]').find_parent('form')
    values = {}
    for field in target.select('input[name],textarea[name],select[name]'):
        name = field['name']
        if field.get('type') in ('file', 'submit'):
            continue
        if field.get('type') in ('checkbox', 'radio') and not field.has_attr('checked'):
            continue
        if field.name == 'textarea':
            values[name] = field.text
        elif field.name == 'select':
            selected = field.select_one('option[selected]') or field.select_one('option')
            values[name] = selected.get('value', selected.text)
        else:
            values[name] = field.get('value', '')
    return response, soup, values


def login(name):
    session = requests.Session()
    _, _, fields = form(session, '/admin/login')
    fields['LoginId'] = name
    result = session.post(base + '/admin/login', data=fields, timeout=30, allow_redirects=False)
    check(result.status_code == 302, name + ' login')
    return session


try:
    db.execute(sql.SQL('CREATE SCHEMA {}').format(sql.Identifier(schema)))
    env = os.environ.copy()
    env.update(ASPNETCORE_ENVIRONMENT='Development', ConnectionStrings__GameDb=connection.rstrip(';') + ';Search Path=' + schema,
               ShopImages__UploadDirectory=str(out / 'uploads'))
    shutil.copytree(ROOT / 'Server/GameApi/wwwroot', out / 'wwwroot')
    binary = ROOT / 'Server/GameApi/bin/ShopImageReview/GameApi.dll'
    with (out / 'server.log').open('w', encoding='utf-8') as log:
        process = subprocess.Popen(['dotnet', str(binary), '--urls', base, '--contentRoot', str(out)], cwd=out, env=env, stdout=log, stderr=log, creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
        for _ in range(90):
            if process.poll() is not None:
                raise AssertionError('Test server stopped; see isolated server.log')
            try:
                if requests.get(base + '/health', timeout=2).ok:
                    break
            except requests.RequestException:
                pass
            time.sleep(1)
        else:
            raise AssertionError('Test server did not start')
        db.execute(sql.SQL('SET search_path TO {}').format(sql.Identifier(schema)))
        owner = login('image_test_owner')
        # Development-only fixture roles stay inside the disposable schema.
        for name, role in [('image_test_operator', 'Operator'), ('image_test_viewer', 'Viewer')]:
            db.execute('INSERT INTO admin_users(id,login_id,normalized_login,password_hash,role,development_only) VALUES(%s,%s,%s,\'\',%s,true)', (uuid.uuid4(), name, name.upper(), role))
        operator = login('image_test_operator')
        viewer = login('image_test_viewer')
        route = '/admin/charts/shop/edit?kind=GemPack'
        check(requests.get(base + route, allow_redirects=False).status_code == 302, 'Anonymous edit denied')
        check(viewer.get(base + route, allow_redirects=False).status_code == 403, 'Viewer edit denied')
        response, soup, fields = form(owner, route)
        options = soup.select('.shop-image-option input')
        check(len([o for o in options if 'ShopCurrencyPacks20261126/GemPack' in o['value']]) == 5, 'Five gem pack images selectable')
        check(len([o for o in options if 'ShopCurrencyPacks20261126/CoinPack' in o['value']]) == 5, 'Five coin pack images selectable')
        check('multipart/form-data' in response.text and soup.select_one('input[type="file"]'), 'PNG upload control')
        check("script-src 'self'" in response.headers['Content-Security-Policy'] and "blob:" in response.headers['Content-Security-Policy'], 'Scoped local preview CSP')
        check(requests.get(base + '/admin-assets/shop-images.js').ok, 'External preview script served')
        pack = 'Gameplay/ShopCurrencyPacks20261126/GemPack03'
        asset = ROOT / ('Assets/Resources/' + pack + '.png')
        image = asset.read_bytes()
        preview = requests.get(base + '/shop-images/builtin/' + pack)
        check(preview.status_code == 200 and preview.content == image and preview.headers['Content-Type'].startswith('image/png'), 'Generated image served byte-for-byte')
        fields.update({'Input.Name': '이미지 선택 검사', 'Input.Amount': '100', 'Input.Bonus': '10', 'Input.Visible': 'true', 'Input.Purchasable': 'false', 'SelectedImage': pack})
        check(owner.post(base + route, data={k: v for k, v in fields.items() if k != '__RequestVerificationToken'}, allow_redirects=False).status_code == 400, 'CSRF token required')
        check(viewer.post(base + route, data=fields, allow_redirects=False).status_code in (400, 403), 'Viewer write denied')
        result = owner.post(base + route, data=fields, allow_redirects=False)
        check(result.status_code == 302, 'Save selected built-in image')
        product, key, revision = db.execute("SELECT id,thumbnail,revision FROM shop_products WHERE name='이미지 선택 검사'").fetchone()
        check(key == pack, 'Selected image persisted')
        edit = '/admin/charts/shop/edit/' + str(product)
        response, soup, fields = form(owner, edit)
        check(soup.select_one('#shop-image-preview')['src'].endswith(pack), 'Persisted image preview')
        _, _, operator_fields = form(operator, edit)
        upload = operator.post(base + edit, data=operator_fields, files={'ImageFile': ('custom.png', image, 'image/png')}, allow_redirects=False)
        check(upload.status_code == 302, 'Operator uploads image')
        key, revision = db.execute('SELECT thumbnail,revision FROM shop_products WHERE id=%s', (product,)).fetchone()
        expected = '/shop-images/uploads/' + hashlib.sha256(image).hexdigest() + '.png'
        check(key == expected, 'Uploaded image stored as immutable relative URL')
        check(requests.get(base + key).content == image, 'Public game image endpoint returns upload')
        check(db.execute('SELECT content_hash,width,height,content_type FROM shop_content_assets WHERE asset_key=%s', (key,)).fetchone() == (hashlib.sha256(image).hexdigest(), 1254, 1254, 'image/png'), 'Upload metadata persisted')
        check('shop-product-thumb' in owner.get(base + '/admin/charts/shop?kind=GemPack').text, 'Catalog thumbnail displayed')
        response, soup, current = form(owner, edit)
        check(any(o['value'] == key for o in soup.select('.shop-image-option input')), 'Upload available for reuse')
        again = owner.post(base + edit, data=current, files={'ImageFile': ('same.png', image, 'image/png')}, allow_redirects=False)
        check(again.status_code == 302 and len(list((out / 'uploads').glob('*.png'))) == 1, 'Identical upload deduplicated')
        conflict = owner.post(base + edit, data=current, allow_redirects=False)
        check(conflict.status_code == 409, 'Stale product revision rejected')
        _, _, fields = form(owner, edit)
        snapshot = db.execute('SELECT thumbnail,revision FROM shop_products WHERE id=%s', (product,)).fetchone()
        for label, payload in [('HTML disguised as PNG', b'<script>alert(1)</script>'), ('Truncated PNG', image[:100]), ('Corrupt PNG chunk', image[:100] + bytes([image[100] ^ 1]) + image[101:])]:
            rejected = owner.post(base + edit, data=fields, files={'ImageFile': ('bad.png', payload, 'image/png')})
            check(rejected.status_code == 200 and 'validation-summary-errors' in rejected.text, label + ' rejected')
            check(db.execute('SELECT thumbnail,revision FROM shop_products WHERE id=%s', (product,)).fetchone() == snapshot, label + ' preserves product')
        oversized = bytearray(image)
        oversized[16:20] = struct.pack('>I', 5000)
        oversized[29:33] = struct.pack('>I', zlib.crc32(oversized[12:29]))
        rejected = owner.post(base + edit, data=fields, files={'ImageFile': ('huge.png', bytes(oversized), 'image/png')})
        check('4096' in rejected.text and 'validation-summary-errors' in rejected.text, 'Excessive dimensions rejected')
        invalid = dict(fields, **{'Input.Amount': '-1'})
        rejected = owner.post(base + edit, data=invalid, files={'ImageFile': ('new.png', (ROOT / 'Assets/Resources/Gameplay/ShopCurrencyPacks20261126/CoinPack01.png').read_bytes(), 'image/png')})
        check('validation-summary-errors' in rejected.text and len(list((out / 'uploads').glob('*.png'))) == 1, 'Invalid form does not publish upload')
        fields['SelectedImage'] = 'Gameplay/not-a-real-image'
        check('validation-summary-errors' in owner.post(base + edit, data=fields).text, 'Unknown picker key rejected')
        fields['SelectedImage'] = '__clear'
        cleared = owner.post(base + edit, data=fields, allow_redirects=False)
        check(cleared.status_code == 302 and db.execute('SELECT thumbnail FROM shop_products WHERE id=%s', (product,)).fetchone()[0] is None, 'Representative image can be removed')
        check(requests.get(base + key).status_code == 200, 'Removing reference preserves reusable image')
        for invalid_path in ['/shop-images/uploads/not-a-hash.png', '/shop-images/builtin/not-real', '/shop-images/builtin/..%2f..%2fappsettings']:
            check(requests.get(base + invalid_path).status_code == 404, 'Unregistered path denied: ' + invalid_path)
        dashboard = owner.get(base + '/admin')
        check("script-src 'none'" in dashboard.headers['Content-Security-Policy'], 'Other admin pages retain strict CSP')
        check(db.execute("SELECT count(*) FROM admin_audit_logs WHERE action IN ('ShopProductAdded','ShopProductUpdated') AND target=%s", ('ShopProduct/' + str(product),)).fetchone()[0] == 4, 'Successful changes audited; rejected changes excluded')
        result = {'passed': True, 'checks': len(checks), 'schema': schema, 'liveDataChanged': False}
        (out / 'result.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
        print(json.dumps(result), flush=True)
finally:
    if process is not None:
        process.terminate()
        try:
            process.wait(timeout=15)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
    # This exact schema was created above solely for this run.
    db.execute('SET search_path TO public')
    db.execute(sql.SQL('DROP SCHEMA IF EXISTS {} CASCADE').format(sql.Identifier(schema)))
    db.close()
