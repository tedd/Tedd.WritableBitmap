const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

if (!reducedMotion && 'IntersectionObserver' in window) {
  const observer = new IntersectionObserver((entries) => {
    for (const entry of entries) {
      if (!entry.isIntersecting) continue;
      entry.target.classList.add('visible');
      observer.unobserve(entry.target);
    }
  }, { threshold: 0.12 });

  document.querySelectorAll('.reveal').forEach((element) => observer.observe(element));
} else {
  document.querySelectorAll('.reveal').forEach((element) => element.classList.add('visible'));
}

const tabs = [...document.querySelectorAll('[data-package-tab]')];
const panels = [...document.querySelectorAll('[data-package-panel]')];

function selectTab(selected) {
  const packageName = selected.dataset.packageTab;

  for (const tab of tabs) {
    const active = tab === selected;
    tab.setAttribute('aria-selected', String(active));
    tab.tabIndex = active ? 0 : -1;
  }

  for (const panel of panels) {
    panel.hidden = panel.dataset.packagePanel !== packageName;
  }
}

tabs.forEach((tab, index) => {
  tab.addEventListener('click', () => selectTab(tab));
  tab.addEventListener('keydown', (event) => {
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault();
    let nextIndex = index;
    if (event.key === 'ArrowLeft') nextIndex = (index - 1 + tabs.length) % tabs.length;
    if (event.key === 'ArrowRight') nextIndex = (index + 1) % tabs.length;
    if (event.key === 'Home') nextIndex = 0;
    if (event.key === 'End') nextIndex = tabs.length - 1;
    selectTab(tabs[nextIndex]);
    tabs[nextIndex].focus();
  });
});

async function copyText(button, value) {
  await navigator.clipboard.writeText(value);
  const original = button.textContent;
  button.textContent = 'Copied';
  window.setTimeout(() => { button.textContent = original; }, 1400);
}

document.querySelectorAll('[data-copy]').forEach((button) => {
  button.addEventListener('click', () => copyText(button, button.dataset.copy));
});

document.querySelectorAll('[data-copy-target]').forEach((button) => {
  button.addEventListener('click', () => {
    const target = document.getElementById(button.dataset.copyTarget);
    if (target) copyText(button, target.innerText);
  });
});
