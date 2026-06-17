import { ReactNode, useEffect, useState } from 'react';
import { Building2, LayoutGrid, Moon, Sun } from 'lucide-react';
import { MODULE_LINKS, currentModulePath } from './modules';
import { LANGS } from './i18n';
import { getLang } from './phaseI18n';

// Shared top bar wrapped around every standalone phase page (Phase 7–17) so they
// are no longer dead-ends: a link back to the console, a quick-jump dropdown
// between modules, and a theme toggle that stays in sync with the console.
export function PhaseChrome({ children }: { children: ReactNode }) {
  const [dark, setDark] = useState(() => localStorage.getItem('theme') === 'dark');

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
    localStorage.setItem('theme', dark ? 'dark' : 'light');
  }, [dark]);

  const current = currentModulePath(window.location.pathname);

  return (
    <>
      <div className="phase-chrome">
        <a className="phase-chrome-home" href="/" title="Về Console">
          <span className="phase-chrome-logo"><Building2 size={16} color="#fff" /></span>
          <span>HRM AI Ops</span>
        </a>

        <div className="phase-chrome-jump">
          <LayoutGrid size={14} />
          <select value={current} onChange={e => { window.location.href = e.target.value; }}>
            {MODULE_LINKS.map(m => (
              <option key={m.path} value={m.path}>{m.label}</option>
            ))}
          </select>
        </div>

        <select className="phase-chrome-lang" defaultValue={getLang()}
          onChange={e => { localStorage.setItem('lang', e.target.value); window.location.reload(); }}>
          {LANGS.map(l => (
            <option key={l.code} value={l.code}>{l.flag} {l.code.toUpperCase()}</option>
          ))}
        </select>

        <button className="phase-chrome-theme" onClick={() => setDark(d => !d)}
          title={dark ? 'Light mode' : 'Dark mode'}>
          {dark ? <Sun size={15} /> : <Moon size={15} />}
        </button>
      </div>
      {children}
    </>
  );
}
