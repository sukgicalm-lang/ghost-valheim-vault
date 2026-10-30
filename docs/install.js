'use strict';
(() => {
  const tabs = [...document.querySelectorAll('[data-install-tab]')];
  const panels = {new: document.getElementById('new-install'), existing: document.getElementById('returning-install')};
  function selectTab(name, changeUrl = false, focus = false) {
    for (const tab of tabs) {
      const selected = tab.dataset.installTab === name;
      tab.setAttribute('aria-selected', String(selected));
      tab.tabIndex = selected ? 0 : -1;
      if (selected && focus) tab.focus();
    }
    for (const [key, panel] of Object.entries(panels)) panel.hidden = key !== name;
    if (changeUrl) {
      const url = new URL(location.href);
      url.hash = name === 'existing' ? 'updates' : '';
      history.replaceState(null, '', url);
    }
  }
  tabs.forEach((tab, index) => {
    tab.addEventListener('click', () => selectTab(tab.dataset.installTab, true));
    tab.addEventListener('keydown', event => {
      let next;
      if (event.key === 'ArrowRight') next = (index + 1) % tabs.length;
      if (event.key === 'ArrowLeft') next = (index + tabs.length - 1) % tabs.length;
      if (event.key === 'Home') next = 0;
      if (event.key === 'End') next = tabs.length - 1;
      if (next != null) { event.preventDefault(); selectTab(tabs[next].dataset.installTab, true, true); }
    });
  });
  function applyHash() {
    selectTab(location.hash === '#updates' ? 'existing' : 'new');
    if (location.hash === '#manual-downloads') document.getElementById('manual-downloads').open = true;
  }
  applyHash();
  window.addEventListener('hashchange', applyHash);
  document.querySelector('[data-show-install]').addEventListener('click', () => {
    selectTab('new', true, true);
    document.getElementById('new-install').scrollIntoView({block: 'start'});
  });
  const menu = document.querySelector('.menu-toggle');
  menu.addEventListener('click', () => {
    const open = document.querySelector('.header nav').classList.toggle('open');
    menu.setAttribute('aria-expanded', String(open));
    menu.setAttribute('aria-label', open ? '메뉴 닫기' : '메뉴 열기');
  });
  let toastTimer;
  function toast(message) {
    const el = document.querySelector('.toast');
    el.textContent = message; el.classList.add('show');
    clearTimeout(toastTimer); toastTimer = setTimeout(() => el.classList.remove('show'), 5000);
  }
  document.querySelectorAll('.install-link').forEach(link => link.addEventListener('click', () => {
    toast('브라우저의 Ghost r2 열기를 허용하고, 런처에서 설치를 진행하세요.');
  }));
  const origin = 'https://sukgicalm-lang.github.io/ghost-valheim-vault';
  const releases = 'https://github.com/sukgicalm-lang/ghost-valheim-vault/releases/download/';
  function validVersion(data) { return data && /^\d+\.\d+\.\d+$/.test(data.version); }
  async function fetchFeed(file) {
    const abort = new AbortController(), timer = setTimeout(() => abort.abort(), 8000);
    try {
      const response = await fetch(origin + '/' + file, {cache:'no-store', signal:abort.signal});
      if (!response.ok) throw new Error('Feed unavailable');
      const data = await response.json();
      if (!validVersion(data)) throw new Error('Invalid feed');
      return data;
    } finally { clearTimeout(timer); }
  }
  async function refreshDownloads() {
    if (location.protocol === 'file:') return;
    const results = await Promise.allSettled([
      fetchFeed('client-release.json'), fetchFeed('release.json'), fetchFeed('smelter-release.json')
    ]);
    const [clientResult, suiteResult, smelterResult] = results;
    if (clientResult.status === 'fulfilled') {
      const c = clientResult.value;
      if (c.downloadUrl === `${releases}client-${c.version}/GhostR2-Windows-${c.version}.zip`) {
        document.querySelectorAll('.client-download').forEach(a => a.href = `${releases}client-${c.version}/GhostR2-Install-${c.version}.zip`);
        document.querySelectorAll('.client-source').forEach(a => a.href = `${releases}client-${c.version}/GhostR2-Source-${c.version}.zip`);
        document.querySelectorAll('[data-client-version]').forEach(el => el.textContent = c.version);
      }
    }
    for (const [result, prefix, name, selector] of [
      [suiteResult, 'suite', 'Ghost-GhostValheimSuite', '.suite-download'],
      [smelterResult, 'smelter', 'Ghost-GhostSmelterControl', '.smelter-download']
    ]) {
      if (result.status !== 'fulfilled') continue;
      const r = result.value;
      const expected = `${releases}${prefix}-${r.version}/${name}-${r.version}.zip`;
      if (r.downloadUrl === expected) document.querySelector(selector).href = expected;
    }
    if (results.some(result => result.status === 'rejected')) {
      const message = document.createElement('p');
      message.className = 'download-hint'; message.setAttribute('role', 'status');
      message.textContent = '일부 최신 배포 정보를 불러오지 못해 기본 다운로드를 표시합니다. 잠시 후 다시 확인해 주세요.';
      document.querySelector('.download-intro').append(message);
    }
  }
  void refreshDownloads();
})();
