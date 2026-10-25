/* Ghost patch archive 1.2.0. Three categories; suite patches come first. */
(() => {
  'use strict';
  const dialog = document.getElementById('ghost-update-history');
  if (!dialog) return;
  const list = dialog.querySelector('[data-history-list]');
  const status = dialog.querySelector('[data-history-status]');
  const tabs = [...dialog.querySelectorAll('[data-history-kind]')];
  const labels = {mods: '게임 업데이트', r2: '런처 업데이트', site: '서비스 소식'};
  let history = null, category = 'mods', opener = null, pending = null, fetchedAt = 0;
  const opened = new Map();
  const node = (tag, text, cls) => {
    const element = document.createElement(tag);
    if (text != null) element.textContent = text;
    if (cls) element.className = cls;
    return element;
  };
  const validEntry = r => r && /^\d+\.\d+\.\d+$/.test(r.version) &&
    typeof r.title === 'string' && Array.isArray(r.changes) &&
    r.changes.every(x => typeof x === 'string') &&
    (!r.notes || Array.isArray(r.notes) && r.notes.every(x => typeof x === 'string'));
  const validHistory = h => h && h.schema === 1 && /^\d+\.\d+\.\d+$/.test(h.currentVersion) && Array.isArray(h.entries) &&
    h.entries.every(validEntry) && (!h.siteUpdates ||
    Array.isArray(h.siteUpdates) && h.siteUpdates.every(validEntry));
  const versionOrder = (a, b) => {
    const x = a.version.split('.').map(Number), y = b.version.split('.').map(Number);
    return y[0] - x[0] || y[1] - x[1] || y[2] - x[2];
  };
  function records(h, kind) {
    const rows = kind === 'mods' ? h.entries.map(r => ({...r, id: 'suite:' + r.version, type: '통합팩'})) :
      (h.siteUpdates || []).filter(r => {
        const launcher = r.component === 'launcher' || r.kind === 'launcher' || /^launcher:|^r2:/.test(r.id || '');
        return kind === 'r2' ? launcher : !launcher;
      }).map(r => ({...r, id: r.id || r.kind + ':' + r.version, type: kind === 'r2' ? '런처' : r.kind === 'board' ? '커뮤니티' : '웹사이트'}));
    const unique = new Map(rows.map(r => [r.id, r]));
    return [...unique.values()].sort((a, b) => kind === 'mods' ? versionOrder(a, b) :
      (b.date || '').localeCompare(a.date || '') || versionOrder(a, b) || a.id.localeCompare(b.id));
  }
  function updateSummary() {
    if (!history) return;
    const version = window.ghostRelease?.version || history.currentVersion;
    const record = history.entries.find(r => r.version === version);
    if (record) document.querySelectorAll('[data-release-title]').forEach(e => { e.textContent = record.title; });
  }
  function render() {
    if (!history) return;
    const rows = records(history, category);
    tabs.forEach(tab => {
      const selected = tab.dataset.historyKind === category;
      tab.setAttribute('aria-selected', String(selected));
      tab.tabIndex = selected ? 0 : -1;
      tab.querySelector('[data-tab-count]').textContent = records(history, tab.dataset.historyKind).length;
    });
    list.setAttribute('aria-labelledby', 'gh-tab-' + category);
    dialog.querySelector('[data-history-count]').textContent = labels[category] + ' · ' + rows.length + '개 기록';
    const fragment = document.createDocumentFragment();
    const expanded = opened.get(category);
    const recordCategory = category;
    rows.forEach((r, index) => {
      const row = node('details', null, 'gh-record');
      row.dataset.key = r.id;
      const latest = category === 'mods' && index === 0;
      row.classList.toggle('gh-latest', latest);
      row.open = latest || (expanded ? expanded.has(r.id) : index === 0);
      const summary = node('summary');
      summary.setAttribute('aria-label', r.type + ' ' + r.version + ' · ' + r.title);
      const top = node('span', null, 'gh-record-top');
      top.append(node('strong', r.type + ' v' + r.version));
      if (latest) top.append(node('span', '최신 통합팩', 'gh-current'));
      if (r.date && /^\d{4}-\d{2}-\d{2}$/.test(r.date)) {
        const date = node('time', r.date.replaceAll('-', '.')); date.dateTime = r.date; top.append(date);
      }
      summary.append(top, node('span', r.title, 'gh-record-title'), node('span', row.open ? '접기' : '자세히', 'gh-expand'));
      const body = node('div', null, 'gh-record-body');
      const changes = node('ul');
      r.changes.forEach(text => changes.append(node('li', text)));
      body.append(changes);
      if (r.notes?.length) {
        const notes = node('div', null, 'gh-record-note');
        r.notes.forEach(text => notes.append(node('p', text))); body.append(notes);
      }
      row.append(summary, body);
      row.addEventListener('toggle', () => {
        if (!row.isConnected || category !== recordCategory) return;
        summary.querySelector('.gh-expand').textContent = row.open ? '접기' : '자세히';
        opened.set(recordCategory, new Set([...list.querySelectorAll('details[open]')].map(e => e.dataset.key)));
      });
      fragment.append(row);
    });
    if (!rows.length) fragment.append(node('p', '아직 등록된 패치 기록이 없습니다.', 'gh-empty'));
    list.replaceChildren(fragment);
  }
  function accept(value) {
    if (!validHistory(value)) throw new Error('Invalid patch archive');
    if (history) {
      const entries = new Map(history.entries.map(r => [r.version, r]));
      value.entries.forEach(r => entries.set(r.version, r));
      const sites = new Map((history.siteUpdates || []).map(r => [r.id || r.kind + ':' + r.version, r]));
      (value.siteUpdates || []).forEach(r => sites.set(r.id || r.kind + ':' + r.version, r));
      const currentVersion = versionOrder({version: history.currentVersion}, {version: value.currentVersion}) < 0 ? history.currentVersion : value.currentVersion;
      value = {...value, entries: [...entries.values()], siteUpdates: [...sites.values()], currentVersion};
    }
    history = value; updateSummary(); if (dialog.open) render();
  }
  try { accept(JSON.parse(document.getElementById('ghost-update-history-data').textContent)); } catch { /* Fetch can recover missing fallback data. */ }
  async function load(force = false) {
    if (pending) return pending;
    if (!force && fetchedAt && Date.now() - fetchedAt < 60000) return;
    if (location.protocol === 'file:') { status.textContent = '저장된 패치 기록입니다.'; return; }
    status.textContent = '최신 소식을 불러오는 중입니다…';
    pending = (async () => {
      const abort = new AbortController(), timeout = setTimeout(() => abort.abort(), 6000);
      try {
        const response = await fetch('update-history.json', {cache: 'no-cache', signal: abort.signal});
        if (!response.ok) throw new Error('Patch archive unavailable');
        accept(await response.json()); fetchedAt = Date.now();
        status.textContent = '최신 소식부터 확인하세요.';
      } catch {
        status.textContent = history ? '저장된 기록입니다. 최신 기록은 잠시 후 다시 확인해 주세요.' : '패치 기록을 불러오지 못했습니다.';
      } finally { clearTimeout(timeout); pending = null; }
    })();
    return pending;
  }
  function select(kind, focus = false) {
    category = kind; render(); dialog.scrollTop = 0;
    if (focus) tabs.find(t => t.dataset.historyKind === kind).focus();
  }
  tabs.forEach((tab, index) => {
    tab.addEventListener('click', () => select(tab.dataset.historyKind));
    tab.addEventListener('keydown', event => {
      let next;
      if (event.key === 'ArrowRight') next = (index + 1) % tabs.length;
      if (event.key === 'ArrowLeft') next = (index + tabs.length - 1) % tabs.length;
      if (event.key === 'Home') next = 0;
      if (event.key === 'End') next = tabs.length - 1;
      if (next != null) { event.preventDefault(); select(tabs[next].dataset.historyKind, true); }
    });
  });
  document.addEventListener('click', event => {
    const target = event.target.closest('[data-update-history]');
    if (!target) return;
    event.preventDefault(); opener = target; category = 'mods'; opened.delete('mods');
    if (!dialog.open) dialog.showModal(); select('mods'); void load();
  });
  dialog.querySelector('[data-history-close]').addEventListener('click', () => dialog.close());
  dialog.addEventListener('close', () => { if (opener?.isConnected) opener.focus(); });
  dialog.addEventListener('click', event => {
    if (event.target !== dialog) return;
    const r = dialog.getBoundingClientRect();
    if (event.clientX < r.left || event.clientX > r.right || event.clientY < r.top || event.clientY > r.bottom) dialog.close();
  });
  window.addEventListener('ghost-release-updated', () => {
    updateSummary();
    if (window.ghostRelease && !history?.entries.some(r => r.version === window.ghostRelease.version)) void load(true);
  });
})();
