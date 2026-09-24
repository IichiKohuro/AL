'use strict';

// ---------- константы ----------

const TAU = Math.PI * 2;
const FLAG_BITING = 1;
const FLAG_SELECTED = 2;
const SPEEDS = [1, 4, 16, 0];

const SECTORS = ['←←', '←', '↑', '→', '→→'];
const INPUT_LABELS = [
  ...SECTORS.map(s => `растение ${s}`),
  ...SECTORS.map(s => `мясо ${s}`),
  ...SECTORS.map(s => `существо ${s}`),
  ...SECTORS.map(s => `чужак ${s}`),
  'энергия', 'скорость', 'возраст', 'часы', 'касание', 'боль', 'память 1', 'память 2',
];
const OUTPUT_LABELS = ['тяга', 'поворот', 'атака', 'размножение', 'память 1', 'память 2'];

const COLORS = {
  background: '#0a0f15',
  world: '#0e151e',
  border: '#1f2c3a',
  plant: '#3f9f5a',
  meat: '#b4533a',
  herb: '#4ade80',
  omni: '#facc15',
  carn: '#f87171',
  accent: '94, 234, 212',
  positive: '251, 146, 60',
  negative: '96, 165, 250',
};
const DIET_NAMES = { Herbivore: 'травоядное', Omnivore: 'всеядное', Carnivore: 'хищник' };

const $ = id => document.getElementById(id);
const fmt = (n, digits = 0) => n.toLocaleString('ru-RU', { minimumFractionDigits: digits, maximumFractionDigits: digits });
const clamp = (v, min, max) => Math.min(max, Math.max(min, v));

const worldCanvas = $('world');
const ctx = worldCanvas.getContext('2d');

// ---------- состояние ----------

const view = { cx: null, cy: null, zoom: 1, follow: false };
let frame = null;     // последний кадр с сервера
let dirty = true;     // нужно ли перерисовать мир
let sim = null;       // состояние симуляции (пауза, скорость, сид)
let selected = null;  // подробности о выбранном существе

// ---------- поток кадров ----------

function connect() {
  const protocol = location.protocol === 'https:' ? 'wss' : 'ws';
  const socket = new WebSocket(`${protocol}://${location.host}/ws`);
  socket.binaryType = 'arraybuffer';
  socket.onmessage = e => {
    frame = decodeFrame(e.data);
    dirty = true;
    renderStatus();
  };
  socket.onclose = () => {
    $('status').textContent = 'нет связи с сервером, переподключение…';
    setTimeout(connect, 1000);
  };
}

// Формат кадра описан в FrameEncoder.cs.
function decodeFrame(buffer) {
  const v = new DataView(buffer);
  let p = 1; // версия
  const tick = v.getInt32(p, true); p += 4;
  const width = v.getFloat32(p, true); p += 4;
  const height = v.getFloat32(p, true); p += 4;

  const oases = [];
  const oasisCount = v.getUint8(p); p += 1;
  for (let i = 0; i < oasisCount; i++, p += 16) {
    oases.push({
      x: v.getFloat32(p, true),
      y: v.getFloat32(p + 4, true),
      r: v.getFloat32(p + 8, true),
      fertility: v.getFloat32(p + 12, true),
    });
  }

  const creatureCount = v.getInt32(p, true); p += 4;
  const creatures = new Array(creatureCount);
  for (let i = 0; i < creatureCount; i++, p += 14) {
    creatures[i] = {
      x: v.getFloat32(p, true),
      y: v.getFloat32(p + 4, true),
      angle: v.getUint8(p + 8) / 255 * TAU - Math.PI,
      r: v.getUint8(p + 9) / 10,
      hue: v.getUint8(p + 10) / 255 * 360,
      diet: v.getUint8(p + 11) / 255,
      energy: v.getUint8(p + 12) / 255,
      flags: v.getUint8(p + 13),
    };
  }

  const foodCount = v.getInt32(p, true); p += 4;
  return { tick, width, height, oases, creatures, food: new DataView(buffer, p, foodCount * 5), foodCount };
}

// ---------- мир ----------

function fitCanvas(canvas) {
  const dpr = window.devicePixelRatio || 1;
  const width = Math.round(canvas.clientWidth * dpr);
  const height = Math.round(canvas.clientHeight * dpr);
  if (canvas.width !== width || canvas.height !== height) {
    canvas.width = width;
    canvas.height = height;
  }
  return dpr;
}

function scale() {
  const fit = Math.min(worldCanvas.clientWidth / frame.width, worldCanvas.clientHeight / frame.height);
  return fit * view.zoom;
}

function screenToWorld(sx, sy) {
  const s = scale();
  return {
    x: (sx - worldCanvas.clientWidth / 2) / s + view.cx,
    y: (sy - worldCanvas.clientHeight / 2) / s + view.cy,
  };
}

function dietColor(diet) {
  return diet < 1 / 3 ? COLORS.herb : diet > 2 / 3 ? COLORS.carn : COLORS.omni;
}

function drawWorld() {
  const f = frame;
  const dpr = fitCanvas(worldCanvas);
  const width = worldCanvas.clientWidth;
  const height = worldCanvas.clientHeight;

  if (view.cx === null) {
    view.cx = f.width / 2;
    view.cy = f.height / 2;
  }
  const me = f.creatures.find(c => c.flags & FLAG_SELECTED);
  if (view.follow && me) {
    view.cx = me.x;
    view.cy = me.y;
  }

  const s = scale();
  ctx.setTransform(1, 0, 0, 1, 0, 0);
  ctx.fillStyle = COLORS.background;
  ctx.fillRect(0, 0, worldCanvas.width, worldCanvas.height);
  ctx.setTransform(s * dpr, 0, 0, s * dpr, (width / 2 - view.cx * s) * dpr, (height / 2 - view.cy * s) * dpr);

  ctx.fillStyle = COLORS.world;
  ctx.fillRect(0, 0, f.width, f.height);

  ctx.save();
  ctx.beginPath();
  ctx.rect(0, 0, f.width, f.height);
  ctx.clip();
  drawOases(f);
  drawFood(f);
  drawCreatures(f, s);
  if (me) drawSelection(me, s);
  ctx.restore();

  ctx.strokeStyle = COLORS.border;
  ctx.lineWidth = 1 / s;
  ctx.strokeRect(0, 0, f.width, f.height);
}

function drawOases(f) {
  for (const o of f.oases) {
    const alpha = 0.03 + 0.05 * Math.min(o.fertility, 2);
    // Мир — тор: оазис у края виден и с противоположной стороны.
    for (const dx of [-f.width, 0, f.width]) {
      for (const dy of [-f.height, 0, f.height]) {
        const x = o.x + dx;
        const y = o.y + dy;
        if (x + o.r < 0 || x - o.r > f.width || y + o.r < 0 || y - o.r > f.height) continue;
        const gradient = ctx.createRadialGradient(x, y, 0, x, y, o.r);
        gradient.addColorStop(0, `rgba(74, 222, 128, ${alpha})`);
        gradient.addColorStop(1, 'rgba(74, 222, 128, 0)');
        ctx.fillStyle = gradient;
        ctx.fillRect(x - o.r, y - o.r, o.r * 2, o.r * 2);
      }
    }
  }
}

function drawFood(f) {
  for (const [kind, color] of [[0, COLORS.plant], [1, COLORS.meat]]) {
    ctx.beginPath();
    for (let i = 0, p = 0; i < f.foodCount; i++, p += 5) {
      if (f.food.getUint8(p + 4) !== kind) continue;
      const x = f.food.getUint16(p, true);
      const y = f.food.getUint16(p + 2, true);
      ctx.moveTo(x + 2.2, y);
      ctx.arc(x, y, 2.2, 0, TAU);
    }
    ctx.fillStyle = color;
    ctx.fill();
  }
}

function drawCreatures(f, s) {
  const ring = Math.max(0.8, 1.4 / s);
  const minRadius = 2.5 / s; // чтобы при сильном отдалении существа не превращались в точки
  for (const c of f.creatures) {
    const cos = Math.cos(c.angle);
    const sin = Math.sin(c.angle);
    const r = Math.max(c.r, minRadius);

    ctx.beginPath();
    ctx.arc(c.x, c.y, r, 0, TAU);
    ctx.fillStyle = `hsl(${c.hue} 70% ${28 + c.energy * 37}%)`;
    ctx.fill();
    ctx.lineWidth = ring;
    ctx.strokeStyle = dietColor(c.diet);
    ctx.stroke();

    // «Нос» показывает направление.
    ctx.beginPath();
    ctx.moveTo(c.x + cos * r * 0.3, c.y + sin * r * 0.3);
    ctx.lineTo(c.x + cos * (r + 3), c.y + sin * (r + 3));
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.75)';
    ctx.stroke();

    if (c.flags & FLAG_BITING) {
      ctx.beginPath();
      ctx.arc(c.x, c.y, r + 2.5, c.angle - 0.9, c.angle + 0.9);
      ctx.strokeStyle = COLORS.carn;
      ctx.lineWidth = ring * 1.5;
      ctx.stroke();
    }
  }
}

function drawSelection(me, s) {
  if (selected && !selected.isDead) {
    const range = selected.genome.vision;
    const half = selected.genome.fieldOfView / 2;

    ctx.beginPath();
    ctx.moveTo(me.x, me.y);
    ctx.arc(me.x, me.y, range, me.angle - half, me.angle + half);
    ctx.closePath();
    ctx.fillStyle = `rgba(${COLORS.accent}, 0.07)`;
    ctx.fill();
    ctx.strokeStyle = `rgba(${COLORS.accent}, 0.4)`;
    ctx.lineWidth = 1 / s;
    ctx.stroke();

    // Границы секторов зрения — те самые входы «←←, ←, ↑, →, →→».
    ctx.beginPath();
    for (let k = 1; k < SECTORS.length; k++) {
      const a = me.angle - half + 2 * half * k / SECTORS.length;
      ctx.moveTo(me.x, me.y);
      ctx.lineTo(me.x + Math.cos(a) * range, me.y + Math.sin(a) * range);
    }
    ctx.strokeStyle = `rgba(${COLORS.accent}, 0.15)`;
    ctx.stroke();
  }

  ctx.beginPath();
  ctx.arc(me.x, me.y, me.r + 5, 0, TAU);
  ctx.strokeStyle = '#ecfeff';
  ctx.lineWidth = 1.5 / s;
  ctx.stroke();
}

function loop() {
  requestAnimationFrame(loop);
  if (frame && dirty) {
    dirty = false;
    drawWorld();
  }
}

// ---------- мышь и клавиатура ----------

let drag = null;

worldCanvas.addEventListener('pointerdown', e => {
  drag = { x: e.clientX, y: e.clientY, cx: view.cx, cy: view.cy, moved: false };
  worldCanvas.setPointerCapture(e.pointerId);
});

worldCanvas.addEventListener('pointermove', e => {
  if (!drag || !frame) return;
  const dx = e.clientX - drag.x;
  const dy = e.clientY - drag.y;
  if (Math.abs(dx) + Math.abs(dy) > 4) drag.moved = true;
  if (!drag.moved) return;
  const s = scale();
  view.cx = drag.cx - dx / s;
  view.cy = drag.cy - dy / s;
  setFollow(false);
  dirty = true;
});

worldCanvas.addEventListener('pointerup', e => {
  if (drag && !drag.moved && frame) {
    const rect = worldCanvas.getBoundingClientRect();
    const point = screenToWorld(e.clientX - rect.left, e.clientY - rect.top);
    select(point.x, point.y);
  }
  drag = null;
});

worldCanvas.addEventListener('wheel', e => {
  e.preventDefault();
  if (!frame) return;
  const rect = worldCanvas.getBoundingClientRect();
  const sx = e.clientX - rect.left;
  const sy = e.clientY - rect.top;
  const before = screenToWorld(sx, sy);
  view.zoom = clamp(view.zoom * Math.exp(-e.deltaY * 0.0015), 0.5, 25);
  const after = screenToWorld(sx, sy);
  if (!view.follow) {
    view.cx += before.x - after.x;
    view.cy += before.y - after.y;
  }
  dirty = true;
}, { passive: false });

new ResizeObserver(() => { dirty = true; }).observe(worldCanvas);

document.addEventListener('keydown', e => {
  // Иначе пробел ещё и «нажмёт» кнопку, на которой остался фокус.
  if (e.target instanceof HTMLButtonElement) e.target.blur();
  if (e.code === 'Space') {
    e.preventDefault();
    control({ paused: !sim?.paused });
  } else if (/^Digit[1-4]$/.test(e.code)) {
    control({ speed: SPEEDS[Number(e.code.at(-1)) - 1], paused: false });
  } else if (e.code === 'KeyF') {
    setFollow(!view.follow);
  } else if (e.code === 'Escape') {
    clearSelection();
  }
});

// ---------- API ----------

async function api(method, url, body) {
  const response = await fetch(url, {
    method,
    headers: body ? { 'Content-Type': 'application/json' } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  });
  if (response.status === 204) return null;
  if (!response.ok) throw new Error(`${method} ${url}: ${response.status}`);
  return response.json();
}

async function control(body) {
  sim = await api('POST', '/api/control', body);
  renderControls();
}

$('pause').addEventListener('click', () => control({ paused: !sim?.paused }));
document.querySelectorAll('[data-speed]').forEach(button =>
  button.addEventListener('click', () => control({ speed: Number(button.dataset.speed), paused: false })));
$('reset').addEventListener('click', async () => {
  clearSelection();
  sim = await api('POST', '/api/reset', {});
  view.cx = null;
  view.zoom = 1;
  renderControls();
  refreshStats();
});
$('follow').addEventListener('click', () => setFollow(!view.follow));
$('deselect').addEventListener('click', () => clearSelection());

// ---------- статистика ----------

async function refreshStats() {
  try {
    const data = await api('GET', '/api/stats');
    sim = data.state;
    renderControls();
    renderStats(data.stats);
    drawChart(data.history);
  } catch {
    // Сервер недоступен — об этом сообщит WebSocket.
  }
}

function renderStatus() {
  if (!sim) return;
  const tick = frame ? frame.tick : sim.tick;
  $('status').textContent =
    `тик ${fmt(tick)} · ${fmt(sim.ticksPerSecond)} тиков/с · сид ${sim.seed}${sim.paused ? ' · пауза' : ''}`;
}

function renderControls() {
  if (!sim) return;
  $('pause').textContent = sim.paused ? '▶' : '❚❚';
  $('pause').classList.toggle('active', sim.paused);
  document.querySelectorAll('[data-speed]').forEach(button =>
    button.classList.toggle('active', Number(button.dataset.speed) === sim.speed));
  renderStatus();
}

function renderList(element, rows) {
  element.replaceChildren(...rows.flatMap(([name, value]) => {
    const dt = document.createElement('dt');
    const dd = document.createElement('dd');
    dt.textContent = name;
    dd.textContent = value;
    return [dt, dd];
  }));
}

function renderStats(st) {
  $('population').textContent = fmt(st.population);
  $('herbivores').textContent = fmt(st.herbivores);
  $('omnivores').textContent = fmt(st.omnivores);
  $('carnivores').textContent = fmt(st.carnivores);
  renderList($('stats'), [
    ['Растения · мясо', `${fmt(st.plants)} · ${fmt(st.meat)}`],
    ['Поколение', `ср. ${fmt(st.averageGeneration, 1)} · макс. ${fmt(st.maxGeneration)}`],
    ['Рождений · смертей', `${fmt(st.births)} · ${fmt(st.deaths)}`],
    ['Убито в схватках', fmt(st.kills)],
    ['Средний размер', fmt(st.averageSize, 2)],
    ['Средняя скорость', fmt(st.averageSpeed, 2)],
    ['Среднее зрение', fmt(st.averageVision)],
    ['Средний рацион', fmt(st.averageDiet, 2)],
  ]);
}

function drawChart(history) {
  const canvas = $('chart');
  const dpr = fitCanvas(canvas);
  const c = canvas.getContext('2d');
  const width = canvas.clientWidth;
  const height = canvas.clientHeight;
  c.setTransform(dpr, 0, 0, dpr, 0, 0);
  c.clearRect(0, 0, width, height);
  if (history.length < 2) return;

  const pad = 8;
  const top = 22; // место под подписи
  const maxPopulation = Math.max(10, ...history.map(h => Math.max(h.herbivores, h.omnivores, h.carnivores)));
  const maxPlants = Math.max(10, ...history.map(h => h.plants));
  const x = i => pad + (width - 2 * pad) * i / (history.length - 1);
  const y = (value, max) => height - pad - (height - pad - top) * value / max;

  c.beginPath();
  c.moveTo(x(0), height - pad);
  history.forEach((h, i) => c.lineTo(x(i), y(h.plants, maxPlants)));
  c.lineTo(x(history.length - 1), height - pad);
  c.closePath();
  c.fillStyle = 'rgba(63, 159, 90, 0.18)';
  c.fill();

  for (const [key, color] of [['herbivores', COLORS.herb], ['omnivores', COLORS.omni], ['carnivores', COLORS.carn]]) {
    c.beginPath();
    history.forEach((h, i) => (i ? c.lineTo : c.moveTo).call(c, x(i), y(h[key], maxPopulation)));
    c.strokeStyle = color;
    c.lineWidth = 1.5;
    c.stroke();
  }

  c.fillStyle = '#7f8e9d';
  c.font = '11px system-ui, sans-serif';
  c.textAlign = 'left';
  c.fillText(`до ${fmt(maxPopulation)} особей`, pad, 14);
  c.textAlign = 'right';
  c.fillText(`тики ${fmt(history[0].tick)} – ${fmt(history.at(-1).tick)}`, width - pad, 14);
}

// ---------- выбранное существо ----------

async function select(x, y) {
  try {
    selected = await api('POST', '/api/select', { x, y });
  } catch {
    selected = null;
  }
  if (!selected) setFollow(false);
  renderInspector();
  dirty = true;
}

function clearSelection() {
  if (selected) api('DELETE', '/api/select').catch(() => {});
  selected = null;
  setFollow(false);
  renderInspector();
  dirty = true;
}

function setFollow(on) {
  view.follow = on && !!selected;
  if (view.follow && view.zoom < 3) view.zoom = 3;
  $('follow').classList.toggle('active', view.follow);
  dirty = true;
}

async function pollSelected() {
  if (!selected) return;
  try {
    const details = await api('GET', `/api/creatures/${selected.id}`);
    if (selected && details && details.id === selected.id) {
      selected = details;
      renderInspector();
    }
  } catch {
    clearSelection();
  }
}

function renderInspector() {
  $('inspector').hidden = !selected;
  if (!selected) return;

  const d = selected;
  const g = d.genome;
  const title = $('inspector-title');
  title.textContent = `Существо #${d.id}`;
  title.classList.toggle('dead', d.isDead);
  if (d.isDead) title.textContent += ` — погибло на тике ${fmt(d.deathTick)}`;
  $('energy-bar').style.width = `${clamp(d.energy / d.maxEnergy, 0, 1) * 100}%`;

  renderList($('creature'), [
    ['Рацион', `${DIET_NAMES[d.dietClass]} (${fmt(g.diet, 2)})`],
    ['Поколение', d.generation],
    ['Возраст', `${fmt(d.age)} из ${fmt(d.lifespan)}`],
    ['Энергия', `${fmt(Math.max(0, d.energy))} из ${fmt(d.maxEnergy)}`],
    ['Дети · жертвы', `${d.children} · ${d.kills}`],
    ['Съело растений · мяса', `${fmt(d.plantEnergyEaten)} · ${fmt(d.meatEnergyEaten)}`],
    ['Размер · скорость', `${fmt(g.size, 2)} · ${fmt(d.maxSpeed, 2)}`],
    ['Зрение · обзор', `${fmt(g.vision)} · ${fmt(g.fieldOfView * 180 / Math.PI)}°`],
    ['Размножается при', `${fmt(g.reproductionThreshold * 100)}% энергии`],
    ['Отдаёт потомку', `${fmt(g.childShare * 100)}% энергии`],
    ['Мутации', `${fmt(g.mutationRate * 100, 1)}%`],
    ['Родитель', d.parentId ? `#${d.parentId}` : 'случайный геном'],
  ]);
  drawBrain(d);
}

function drawBrain(d) {
  const canvas = $('brain');
  const dpr = fitCanvas(canvas);
  const c = canvas.getContext('2d');
  const width = canvas.clientWidth;
  const height = canvas.clientHeight;
  c.setTransform(dpr, 0, 0, dpr, 0, 0);
  c.clearRect(0, 0, width, height);

  const xIn = 88;
  const xOut = width - 112;
  const xHidden = (xIn + xOut) / 2;
  const at = (i, n) => 8 + (height - 16) * (i + 0.5) / n;
  const nIn = d.inputs.length;
  const nHidden = d.hidden.length;
  const nOut = d.outputs.length;

  // Связи: яркость — сколько сигнала проходит прямо сейчас (вес × активация).
  let k = 0;
  for (let h = 0; h < nHidden; h++) {
    k++; // смещение
    for (let i = 0; i < nIn; i++, k++) edge(c, xIn, at(i, nIn), xHidden, at(h, nHidden), d.weights[k] * d.inputs[i]);
  }
  for (let o = 0; o < nOut; o++) {
    k++;
    for (let h = 0; h < nHidden; h++, k++) edge(c, xHidden, at(h, nHidden), xOut, at(o, nOut), d.weights[k] * d.hidden[h]);
  }

  d.inputs.forEach((v, i) => neuron(c, xIn, at(i, nIn), v));
  d.hidden.forEach((v, h) => neuron(c, xHidden, at(h, nHidden), v));
  d.outputs.forEach((v, o) => neuron(c, xOut, at(o, nOut), v));

  c.font = '10px system-ui, sans-serif';
  c.fillStyle = '#9fb0c0';
  c.textBaseline = 'middle';
  c.textAlign = 'right';
  INPUT_LABELS.forEach((label, i) => c.fillText(label, xIn - 9, at(i, nIn)));
  c.textAlign = 'left';
  OUTPUT_LABELS.forEach((label, o) => c.fillText(`${label} ${fmt(d.outputs[o], 2)}`, xOut + 9, at(o, nOut)));
}

function edge(c, x1, y1, x2, y2, signal) {
  const alpha = Math.min(1, Math.abs(signal)) * 0.6;
  if (alpha < 0.03) return;
  c.strokeStyle = `rgba(${signal > 0 ? COLORS.positive : COLORS.negative}, ${alpha})`;
  c.lineWidth = 1;
  c.beginPath();
  c.moveTo(x1, y1);
  c.lineTo(x2, y2);
  c.stroke();
}

function neuron(c, x, y, value) {
  const strength = Math.min(1, Math.abs(value));
  c.beginPath();
  c.arc(x, y, 4.5, 0, TAU);
  c.fillStyle = `rgba(${value >= 0 ? COLORS.positive : COLORS.negative}, ${0.15 + 0.85 * strength})`;
  c.fill();
  c.strokeStyle = '#2b3b4c';
  c.lineWidth = 1;
  c.stroke();
}

// ---------- старт ----------

connect();
refreshStats();
setInterval(refreshStats, 1000);
setInterval(pollSelected, 250);
requestAnimationFrame(loop);
