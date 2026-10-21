"""HTTP regression checks. Requires Python 3.9+, .NET 10 and dotnet-ef 10.
Run from the repository: python3 checks/PaymentHistoryChecks/verify.py
Creates its own temporary build output/database/server; never uses an existing DB.
"""
import html
import http.cookiejar
import os
from pathlib import Path
import re
import secrets
import shutil
import socket
import sqlite3
import subprocess
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from datetime import datetime, timedelta
from zoneinfo import ZoneInfo

REPO = Path(__file__).resolve().parents[2]
ROOT = Path(tempfile.mkdtemp(prefix='fivedollar-history-'))
DB = ROOT / 'test.db'
ARTIFACTS = ROOT / 'artifacts'
ENV = dict(os.environ, ASPNETCORE_ENVIRONMENT='Development', DOTNET_ENVIRONMENT='Development',
           ConnectionStrings__DefaultConnection=f'Data Source={DB}', ArtifactsPath=str(ARTIFACTS))
EF = shutil.which('dotnet-ef') or str(Path.home() / '.dotnet/tools/dotnet-ef')
passed = 0
failures = []


def command(args):
    result = subprocess.run(args, cwd=REPO, env=ENV, capture_output=True, text=True, timeout=120)
    if result.returncode:
        raise RuntimeError(result.stdout + result.stderr)


def check(condition, name):
    global passed
    if condition:
        passed += 1
        print('PASS:', name, flush=True)
    else:
        failures.append(name)
        print('FAIL:', name, flush=True)


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


class Browser:
    def __init__(self):
        self.client = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()), NoRedirect())

    def request(self, path, data=None):
        request = urllib.request.Request(BASE + path,
            data=urllib.parse.urlencode(data).encode() if data is not None else None)
        try:
            response = self.client.open(request, timeout=15)
        except urllib.error.HTTPError as error:
            response = error
        return response.code, response.headers.get('Location', ''), response.read().decode()

    def post(self, path, data, token_page='/Plan'):
        status, _, body = self.request(token_page)
        assert status == 200, 'Token page unavailable'
        token = html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', body)[1])
        return self.request(path, dict(data, __RequestVerificationToken=token))


def read(sql, parameters=()):
    with sqlite3.connect(DB.as_uri() + '?mode=ro', uri=True) as connection:
        return connection.execute(sql, parameters).fetchall()


def sections(body):
    return {key.lower(): value for key, value in re.findall(
        r'<section[^>]*id="event-([^"]+)"[^>]*>(.*?)</section>', body, re.S)}


def denied(response):
    return response[0] == 403 or (response[0] == 302 and '/Account/AccessDenied' in response[1])


def anchor(response, plan, event):
    url = urllib.parse.urlsplit(response[1])
    return (response[0] == 302 and url.path.lower() == '/payment/details/' + plan.lower()
            and urllib.parse.parse_qs(url.query).get('eventId', [''])[0].lower() == event.lower()
            and url.fragment.lower() == 'event-' + event.lower())


command(['dotnet', 'build', 'FiveDollarPlan.csproj', '--artifacts-path', str(ARTIFACTS), '--verbosity', 'quiet'])
assert not DB.exists()
command([EF, 'database', 'update', '--no-build', '--connection', f'Data Source={DB}'])
with socket.socket() as probe:
    probe.bind(('127.0.0.1', 0))
    port = probe.getsockname()[1]
BASE = f'http://127.0.0.1:{port}'
ENV['ASPNETCORE_URLS'] = BASE
print('Temporary database:', DB, flush=True)
with (ROOT / 'server.log').open('w') as log:
    app = subprocess.Popen(['dotnet', str(ARTIFACTS / 'bin/FiveDollarPlan/debug/FiveDollarPlan.dll')],
                           cwd=REPO, env=ENV, stdout=log, stderr=subprocess.STDOUT)
    try:
        anonymous = Browser()
        for _ in range(100):
            if app.poll() is not None:
                raise RuntimeError('Test server exited; inspect ' + str(ROOT / 'server.log'))
            try:
                if anonymous.request('/Account/Login')[0] == 200:
                    break
            except OSError:
                time.sleep(0.1)
        else:
            raise RuntimeError('Test server startup timed out')
        home_status, _, home = anonymous.request('/')
        check(home_status == 200 and 'Welcome to FiveDollarPlan!' in home, 'anonymous homepage')
        check('href="/Account/Register"' in home and 'href="/Account/Login"' in home,
              'anonymous homepage registration and login links')
        a, b, c = Browser(), Browser(), Browser()
        password = secrets.token_urlsafe(24)
        for browser, label in [(a, 'A'), (b, 'B'), (c, 'C')]:
            name = '<script>alert("member")</script>' if label == 'B' else 'History ' + label
            email = label.lower() + '@history.example'
            check(browser.post('/Account/Register', dict(Name=name, Email=email, Password=password),
                               '/Account/Register')[0] == 302, 'register ' + label)
            check(browser.post('/Account/Login', dict(Email=email, Password=password),
                               '/Account/Login')[0] == 302, 'login ' + label)
        home = a.request('/')[2]
        home = re.search(r'<main[^>]*>(.*?)</main>', home, re.S)[1]
        check('href="/Dashboard"' in home or 'href="/Dashboard/Index"' in home, 'authenticated homepage Dashboard link')
        check('href="/Plan"' in home or 'href="/Plan/Index"' in home, 'authenticated homepage Plans link')
        check('Get Started' not in home and 'href="/Account/Register"' not in home,
              'authenticated homepage does not prompt registration')
        for route in ['/Dashboard/Index', '/Dashboard/Preview']:
            response = anonymous.request(route)
            check(response[0] == 302 and '/Account/Login' in response[1], 'anonymous restriction ' + route)

        def totals(browser, income, expense, label):
            status, _, body = browser.request('/Dashboard/Index')
            values = []
            for title in ['Confirmed Income', 'Confirmed Expense']:
                match = re.search(r'<h3[^>]*>' + title + r'</h3>\s*<p[^>]*>NZ\$([0-9]+\.[0-9]{2})</p>', body)
                values.append(match[1] if match else None)
            check(status == 200 and values == [income, expense], label)

        totals(a, '0.00', '0.00', 'empty Dashboard totals')
        now = datetime.now(ZoneInfo('Pacific/Auckland'))
        plan_name = '<script>alert("plan")</script>'

        def create(name, mode):
            response = a.post('/Plan/Create', dict(Name=name, Amount='5', Mode=str(mode),
                StartAtLocal=(now - timedelta(hours=1)).strftime('%Y-%m-%dT%H:%M'),
                EndAtLocal=(now + timedelta(days=1)).strftime('%Y-%m-%dT%H:%M')), '/Plan/Create')
            check(response[0] == 302, 'Plan creation: ' + ('Multiple' if mode else 'Single'))
            return read('SELECT Id,InviteCode FROM Plans WHERE Name=?', (name,))[0]

        plan, code = create(plan_name, 1)
        other, _ = create('Empty single plan', 0)
        details = '/Payment/Details/' + plan
        check('No penalty event yet.' in a.request(details)[2], 'empty history')
        check(b.post('/Plan/Join', dict(InviteCode=code))[0] == 302, 'invitation joining')
        links = re.findall(r'href="([^"]+)"[^>]*>\s*View Payments\s*</a>', a.request('/Plan')[2])
        check(any(html.unescape(link).lower() == details.lower() for link in links), 'View Payments URL')
        check(b.request(details)[0] == 200, 'member Details access')
        response = anonymous.request(details)
        check(response[0] == 302 and '/Account/Login' in response[1], 'anonymous rejected')
        check(denied(c.request(details)), 'unrelated Plan access rejected')
        check(denied(b.post('/Payment/Trigger', dict(planId=plan))), 'member cannot trigger')

        for _ in range(2):
            check(a.post('/Payment/Trigger', dict(planId=plan))[0] == 302, 'Multiple trigger succeeds')
        events = read('SELECT Id FROM PenaltyEvents WHERE PlanId=? ORDER BY TriggeredAtUtc,Id', (plan,))
        old, new = [row[0] for row in events]
        payment = read('SELECT Id FROM PaymentRecords WHERE PenaltyEventId=?', (old,))[0][0]
        body = a.request(details)[2]
        groups = sections(body)
        check(len(groups) == 2, 'all historical events shown')
        check(body.lower().index('event-' + new.lower()) < body.lower().index('event-' + old.lower()), 'newest first')
        for event, payment_id in read('SELECT PenaltyEventId,Id FROM PaymentRecords'):
            check(payment_id.lower() in groups[event.lower()].lower()
                  and all(payment_id.lower() not in group.lower() for key, group in groups.items()
                          if key != event.lower()), 'payment associated only with own event')
        check(plan_name not in body and '<script>alert("member")</script>' not in body
              and '&lt;script&gt;' in body, 'Razor encodes Plan and recipient names')
        check(denied(b.post('/Payment/MarkPaid/' + payment, {})), 'recipient cannot mark payer payment')
        check(denied(c.post('/Payment/ConfirmReceived/' + payment, {})), 'unrelated user cannot confirm')
        check(a.request('/Payment/MarkPaid/' + payment, {})[0] == 400, 'antiforgery required')
        check(read('SELECT PaidAtUtc,ReceivedAtUtc FROM PaymentRecords WHERE Id=?', (payment,))[0] == (None, None), 'denied actions preserve state')
        check(anchor(a.post('/Payment/MarkPaid/' + payment, {}), plan, old), 'old MarkPaid redirects to event anchor')
        totals(a, '0.00', '0.00', 'MarkedPaid has no confirmed expense')
        totals(b, '0.00', '0.00', 'MarkedPaid has no confirmed income')
        group = sections(b.request(details)[2])[old.lower()]
        check(re.search(r'<td><span[^>]*>Confirmed</span></td>\s*<td><span[^>]*>Waiting</span></td>', group) is not None,
              'Paid and Received displayed independently')
        check(anchor(b.post('/Payment/ConfirmReceived/' + payment, {}), plan, old), 'old ConfirmReceived redirects to event anchor')
        groups = sections(a.request(details)[2])
        check('All payments have been confirmed.' in groups[old.lower()]
              and 'Some payments are still waiting' in groups[new.lower()], 'event confirmation independent')
        totals(a, '0.00', '5.00', 'creator confirmed Dashboard expense')
        totals(b, '5.00', '0.00', 'member confirmed Dashboard income')
        totals(c, '0.00', '0.00', 'unrelated Dashboard remains zero')
        first_time = read('SELECT ReceivedAtUtc FROM PaymentRecords WHERE Id=?', (payment,))[0][0]
        check(anchor(b.post('/Payment/ConfirmReceived/' + payment, {}), plan, old)
              and read('SELECT ReceivedAtUtc FROM PaymentRecords WHERE Id=?', (payment,))[0][0] == first_time,
              'duplicate confirmation retains timestamp and anchor')
        check(a.request(details + '?eventId=' + str(uuid.uuid4()))[0] == 404, 'unknown event ID')
        check(a.request('/Payment/Details/' + other + '?eventId=' + old)[0] == 404, 'cross-Plan event ID')
        check(a.request(details + '?eventId=not-a-guid')[0] == 400, 'malformed event ID')
        check(a.post('/Payment/Trigger', dict(planId=other))[0] == 302, 'zero-recipient event trigger')
        check('This event has no payment records.' in a.request('/Payment/Details/' + other)[2], 'zero-recipient event displayed')
        a.post('/Payment/Trigger', dict(planId=other))
        check(read('SELECT COUNT(*) FROM PenaltyEvents WHERE PlanId=?', (other,))[0][0] == 1, 'Single duplicate prevention preserved')
        # Only this script-created database is changed to make the tie deterministic.
        with sqlite3.connect(DB) as connection:
            connection.execute('UPDATE PenaltyEvents SET TriggeredAtUtc=? WHERE PlanId=?',
                               ('2026-10-20 00:00:00', plan))
        ordered = [row[0].lower() for row in read('SELECT Id FROM PenaltyEvents WHERE PlanId=? ORDER BY TriggeredAtUtc DESC,Id DESC', (plan,))]
        check(list(sections(a.request(details)[2])) == ordered, 'identical timestamps have stable ID ordering')
        check(read('PRAGMA integrity_check')[0][0] == 'ok', 'isolated database integrity')
        check(a.post('/Account/Logout', {})[0] == 302, 'logout succeeds')
        response = a.request('/Dashboard/Index')
        check(response[0] == 302 and '/Account/Login' in response[1], 'logout removes authenticated access')
        print(f'RESULT: {passed} passed, {len(failures)} failed', flush=True)
        if failures:
            raise SystemExit(1)
    finally:
        # This process was created above; no existing application is stopped.
        app.terminate()
        app.wait(timeout=15)
